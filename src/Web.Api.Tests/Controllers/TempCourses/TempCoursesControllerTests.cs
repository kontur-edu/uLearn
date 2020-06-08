﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Database.Models;
using Database.Repos;
using Database.Repos.CourseRoles;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;
using Ulearn.Core.Courses;
using Ulearn.Web.Api.Controllers;
using Ulearn.Web.Api.Models.Responses.TempCourses;
using System.Text;
using Database;
using Ionic.Zip;
using Microsoft.AspNetCore.Http;

namespace Web.Api.Tests.Controllers.TempCourses
{
	[TestFixture]
	public class TempCoursesControllerTests : BaseControllerTests
	{
		private TempCourseController tempCourseController;
		private ITempCoursesRepo tempCoursesRepo;
		private ICourseRolesRepo courseRolesRepo;
		private DirectoryInfo testCourseDirectory;
		private IWebCourseManager courseManager;
		private DirectoryInfo workingCourseDirectory;


		[OneTimeSetUp]
		public void OneTimeSetUp()
		{
			SetupTestInfrastructureAsync(services => { services.AddScoped<TempCourseController>(); }).GetAwaiter().GetResult();
			tempCourseController = GetController<TempCourseController>();
			tempCoursesRepo = serviceProvider.GetService<ITempCoursesRepo>();
			courseRolesRepo = serviceProvider.GetService<ICourseRolesRepo>();
			courseManager = serviceProvider.GetService<IWebCourseManager>();
			testCourseDirectory = new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", "TempCourses", "Help"));
			workingCourseDirectory = new DirectoryInfo(Path.Combine(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WorkingCourse")));
		}

		[SetUp]
		public void SetUp()
		{
			if (Directory.Exists(workingCourseDirectory.FullName))
			{
				DeleteNotEmptyDirectory(workingCourseDirectory.FullName);
			}

			Directory.CreateDirectory(workingCourseDirectory.FullName);
			DirectoryCopy(testCourseDirectory.FullName, workingCourseDirectory.FullName, true);
		}

		[Test]
		public async Task Create_ShouldSucceed_With_MainCase()
		{
			var baseCourse = await CreateAndConfigureBaseCourseForUser("create_mainCase");
			var result = await tempCourseController.CreateCourse(baseCourse.Object.Id).ConfigureAwait(false);
			Assert.AreEqual(ErrorType.NoErrors, result.ErrorType);
		}

		[Test]
		public async Task Create_ShouldUpdateDB_With_MainCase()
		{
			var baseCourse = await CreateAndConfigureBaseCourseForUser("create_mainCaseDB");
			await tempCourseController.CreateCourse(baseCourse.Object.Id).ConfigureAwait(false);
			var tempCourseEntity = tempCoursesRepo.Find(baseCourse.Object.Id + TestUsers.User.Id);
			Assert.NotNull(tempCourseEntity);
		}

		[Test]
		public async Task Create_ShouldReturnConflict_WhenCourseAlreadyExists()
		{
			var baseCourse = await CreateAndConfigureBaseCourseForUser("create_conflictCase");
			await tempCourseController.CreateCourse(baseCourse.Object.Id).ConfigureAwait(false);
			var result = await tempCourseController.CreateCourse(baseCourse.Object.Id).ConfigureAwait(false);
			Assert.AreEqual(ErrorType.Conflict, result.ErrorType);
		}

		[Test]
		public async Task Create_ShouldReturnForbidden_WhenUserAccessIsLowerThanCourseAdmin()
		{
			var baseCourse = new Mock<ICourse>();
			baseCourse.Setup(c => c.Id).Returns("create_forbiddenCase");
			await AuthenticateUserInControllerAsync(tempCourseController, TestUsers.User).ConfigureAwait(false);
			await tempCourseController.CreateCourse(baseCourse.Object.Id).ConfigureAwait(false);
			var result = await tempCourseController.CreateCourse(baseCourse.Object.Id).ConfigureAwait(false);
			Assert.AreEqual(ErrorType.Forbidden, result.ErrorType);
		}

		[Test]
		public async Task UploadFullCourse_ShouldSucceed_WhenCourseIsValid()
		{
			var baseCourse = await CreateAndConfigureBaseCourseForUser("upload_successCase");
			await tempCourseController.CreateCourse(baseCourse.Object.Id).ConfigureAwait(false);
			var fullCourseZip = new ZipFile(Encoding.UTF8);
			fullCourseZip.AddDirectory(testCourseDirectory.FullName);
			var file = GetFormFileFromZip(fullCourseZip);
			var uploadResult = await tempCourseController.UploadFullCourse(baseCourse.Object.Id, new List<IFormFile>() { file });
			Assert.AreEqual(ErrorType.NoErrors, uploadResult.ErrorType);
		}

		[Test]
		public async Task UploadFullCourse_ShouldReturnNotFound_WhenUserDoesNotHaveTempVersionOfCourse()
		{
			var baseCourse = new Mock<ICourse>();
			baseCourse.Setup(c => c.Id).Returns("upload_successCase");
			await AuthenticateUserInControllerAsync(tempCourseController, TestUsers.User).ConfigureAwait(false);
			var fullCourseZip = new ZipFile(Encoding.UTF8);
			fullCourseZip.AddDirectory(testCourseDirectory.FullName);
			var file = GetFormFileFromZip(fullCourseZip);
			var uploadResult = await tempCourseController.UploadFullCourse(baseCourse.Object.Id, new List<IFormFile>() { file });
			Assert.AreEqual(ErrorType.NotFound, uploadResult.ErrorType);
		}

		[Test]
		public async Task UploadFullCourse_ShouldUpdateDB_WhenCourseIsValid()
		{
			var baseCourse = await CreateAndConfigureBaseCourseForUser("upload_successDBCase");
			await tempCourseController.CreateCourse(baseCourse.Object.Id).ConfigureAwait(false);
			var tmpCourseId = baseCourse.Object.Id + TestUsers.User.Id;
			var loadTimeBeforeUpload = tempCoursesRepo.Find(tmpCourseId).LoadingTime;
			var fullCourseZip = new ZipFile(Encoding.UTF8);
			fullCourseZip.AddDirectory(workingCourseDirectory.FullName);
			var file = GetFormFileFromZip(fullCourseZip);
			await tempCourseController.UploadFullCourse(baseCourse.Object.Id, new List<IFormFile>() { file });
			var loadTimeAfterUpload = tempCoursesRepo.Find(tmpCourseId).LoadingTime;
			Assert.Less(loadTimeBeforeUpload, loadTimeAfterUpload);
		}

		[Test]
		public async Task UploadFullCourse_ShouldOverrideCourseDirectory_WhenCourseIsValid()
		{
			var baseCourse = await CreateAndConfigureBaseCourseForUser("upload_courseErrorCase");
			await tempCourseController.CreateCourse(baseCourse.Object.Id).ConfigureAwait(false);
			var tmpCourseId = baseCourse.Object.Id + TestUsers.User.Id;
			var pathToExcessFile = Path.Combine(workingCourseDirectory.FullName, "excess.txt");
			File.WriteAllText(pathToExcessFile, "");
			var fullCourseZip = new ZipFile(Encoding.UTF8);
			fullCourseZip.AddDirectory(workingCourseDirectory.FullName);
			var file = GetFormFileFromZip(fullCourseZip);
			await tempCourseController.UploadFullCourse(baseCourse.Object.Id, new List<IFormFile>() { file });
			File.Delete(pathToExcessFile);
			fullCourseZip = new ZipFile(Encoding.UTF8);
			fullCourseZip.AddDirectory(workingCourseDirectory.FullName);
			file = GetFormFileFromZip(fullCourseZip);
			await tempCourseController.UploadFullCourse(baseCourse.Object.Id, new List<IFormFile>() { file });
			var courseDirectory = courseManager.GetExtractedCourseDirectory(tmpCourseId);
			var diff = GetDirectoriesDiff(workingCourseDirectory.FullName, courseDirectory.FullName);
			Assert.IsEmpty(diff);
		}

		[Test]
		public async Task UploadFullCourse_ShouldReturnCourseError_WhenCourseIsInvalid()
		{
			var baseCourse = await CreateAndConfigureBaseCourseForUser("upload_courseErrorCase");
			await tempCourseController.CreateCourse(baseCourse.Object.Id).ConfigureAwait(false);
			var fullCourseZip = new ZipFile(Encoding.UTF8);
			BreakCourse();
			fullCourseZip.AddDirectory(workingCourseDirectory.FullName);
			var file = GetFormFileFromZip(fullCourseZip);
			var result = await tempCourseController.UploadFullCourse(baseCourse.Object.Id, new List<IFormFile>() { file });
			Assert.AreEqual(result.ErrorType, ErrorType.CourseError);
		}

		[Test]
		public async Task UploadFullCourse_ShouldNotUpdateDB_WhenCourseIsInvalid()
		{
			var baseCourse = await CreateAndConfigureBaseCourseForUser("upload_courseErrorCaseDB");
			await tempCourseController.CreateCourse(baseCourse.Object.Id).ConfigureAwait(false);
			var tmpCourseId = baseCourse.Object.Id + TestUsers.User.Id;
			var loadTimeBeforeUpload = tempCoursesRepo.Find(tmpCourseId).LoadingTime;
			var fullCourseZip = new ZipFile(Encoding.UTF8);
			BreakCourse();
			fullCourseZip.AddDirectory(workingCourseDirectory.FullName);
			var file = GetFormFileFromZip(fullCourseZip);
			await tempCourseController.UploadFullCourse(baseCourse.Object.Id, new List<IFormFile>() { file });
			var loadTimeAfterUpload = tempCoursesRepo.Find(tmpCourseId).LoadingTime;
			Assert.AreEqual(loadTimeBeforeUpload, loadTimeAfterUpload);
		}

		[Test]
		public async Task UploadFullCourse_ShouldNotUpdateDirectory_WhenCourseIsInvalid()
		{
			var baseCourse = await CreateAndConfigureBaseCourseForUser("upload_courseErrorCaseDir");
			await tempCourseController.CreateCourse(baseCourse.Object.Id).ConfigureAwait(false);
			var tmpCourseId = baseCourse.Object.Id + TestUsers.User.Id;
			var courseDirectory = courseManager.GetExtractedCourseDirectory(tmpCourseId);
			var directoryContentBeforeUpload = GetDirectoryContent(courseDirectory.FullName);
			var fullCourseZip = new ZipFile(Encoding.UTF8);
			BreakCourse();
			fullCourseZip.AddDirectory(workingCourseDirectory.FullName);
			var file = GetFormFileFromZip(fullCourseZip);
			await tempCourseController.UploadFullCourse(baseCourse.Object.Id, new List<IFormFile>() { file });
			var directoryContentAfterUpload = GetDirectoryContent(courseDirectory.FullName);
			Assert.AreEqual(directoryContentAfterUpload, directoryContentBeforeUpload);
		}

		private async Task<Mock<ICourse>> CreateAndConfigureBaseCourseForUser(string courseId)
		{
			var baseCourse = new Mock<ICourse>();
			baseCourse.Setup(c => c.Id).Returns(courseId);
			await courseRolesRepo.ToggleRoleAsync(baseCourse.Object.Id, TestUsers.User.Id, CourseRoleType.CourseAdmin, TestUsers.Admin.Id);
			await AuthenticateUserInControllerAsync(tempCourseController, TestUsers.User).ConfigureAwait(false);
			return baseCourse;
		}

		private void BreakCourse()
		{
			File.Delete(Path.Combine(workingCourseDirectory.FullName, "Slides", "Course.xml"));
		}

		private static IEnumerable<string> GetDirectoriesDiff(string path1, string path2)
		{
			var firstDirFiles = GetDirectoryContent(path1);
			var secondDirFiles = GetDirectoryContent(path2);
			var diffs = firstDirFiles.Except(secondDirFiles).Distinct().ToArray();
			return diffs;
		}

		private static IEnumerable<string> GetDirectoryContent(string path)
		{
			return
				Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
					.Select(x => TrimPrefix(x, path));
		}

		private static string TrimPrefix(string text, string prefix)
		{
			return text.Substring(text.IndexOf(prefix) + prefix.Length + 1);
		}

		private static void DeleteNotEmptyDirectory(string dirPath)
		{
			string[] files = Directory.GetFiles(dirPath);
			string[] dirs = Directory.GetDirectories(dirPath);

			foreach (string file in files)
			{
				File.SetAttributes(file, FileAttributes.Normal);
				File.Delete(file);
			}

			foreach (string dir in dirs)
			{
				DeleteNotEmptyDirectory(dir);
			}

			Directory.Delete(dirPath, false);
		}

		private static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
		{
			DirectoryInfo dir = new DirectoryInfo(sourceDirName);

			if (!dir.Exists)
			{
				throw new DirectoryNotFoundException(
					"Source directory does not exist or could not be found: "
					+ sourceDirName);
			}

			DirectoryInfo[] dirs = dir.GetDirectories();
			if (!Directory.Exists(destDirName))
			{
				Directory.CreateDirectory(destDirName);
			}

			FileInfo[] files = dir.GetFiles();
			foreach (FileInfo file in files)
			{
				string temppath = Path.Combine(destDirName, file.Name);
				file.CopyTo(temppath, false);
			}

			if (copySubDirs)
			{
				foreach (DirectoryInfo subdir in dirs)
				{
					string temppath = Path.Combine(destDirName, subdir.Name);
					DirectoryCopy(subdir.FullName, temppath, copySubDirs);
				}
			}
		}

		private static IFormFile GetFormFileFromZip(ZipFile fullCourseZip)
		{
			var name = Guid.NewGuid() + ".zip";
			fullCourseZip.Save(name);
			var byteArray = File.ReadAllBytes(name);
			var stream = new MemoryStream(byteArray);
			IFormFile file = new FormFile(stream, 0, byteArray.Length, name, name);
			return file;
		}
	}
}