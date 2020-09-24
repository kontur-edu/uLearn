﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Database.Models;
using log4net;
using Ulearn.Common;
using Ulearn.Common.Extensions;
using Ulearn.Core;
using Ulearn.Core.Courses.Slides.Exercises;
using Ulearn.Core.Courses.Slides.Exercises.Blocks;
using Ulearn.Core.RunCheckerJobApi;
using Microsoft.EntityFrameworkCore;

namespace Database.Repos
{
	public class UserSolutionsRepo : IUserSolutionsRepo
	{
		private static readonly ILog log = LogManager.GetLogger(typeof(UserSolutionsRepo));
		private readonly UlearnDb db;
		private readonly ITextsRepo textsRepo;
		private readonly IVisitsRepo visitsRepo;
		private readonly IWebCourseManager courseManager;

		private static volatile ConcurrentDictionary<int, DateTime> unhandledSubmissions = new ConcurrentDictionary<int, DateTime>();
		private static volatile ConcurrentDictionary<int, DateTime> handledSubmissions = new ConcurrentDictionary<int, DateTime>();
		private static readonly TimeSpan handleTimeout = TimeSpan.FromMinutes(3);

		public UserSolutionsRepo(UlearnDb db,
			ITextsRepo textsRepo, IVisitsRepo visitsRepo,
			IWebCourseManager courseManager)
		{
			this.db = db;
			this.textsRepo = textsRepo;
			this.visitsRepo = visitsRepo;
			this.courseManager = courseManager;
		}

		public async Task<UserExerciseSubmission> AddUserExerciseSubmission(
			string courseId, Guid slideId,
			string code, string compilationError, string output,
			string userId, string executionServiceName, string displayName,
			Language language,
			string sandbox,
			AutomaticExerciseCheckingStatus status = AutomaticExerciseCheckingStatus.Waiting)
		{
			if (string.IsNullOrWhiteSpace(code))
				code = "// no code";
			var hash = (await textsRepo.AddText(code)).Hash;
			var compilationErrorHash = (await textsRepo.AddText(compilationError)).Hash;
			var outputHash = (await textsRepo.AddText(output)).Hash;
			var exerciseBlock = ((await courseManager.FindCourseAsync(courseId))?.FindSlideById(slideId, true) as ExerciseSlide)?.Exercise;

			AutomaticExerciseChecking automaticChecking;
			if (language.HasAutomaticChecking() && (language == Language.CSharp || exerciseBlock is UniversalExerciseBlock))
			{
				automaticChecking = new AutomaticExerciseChecking
				{
					CourseId = courseId,
					SlideId = slideId,
					UserId = userId,
					Timestamp = DateTime.Now,
					CompilationErrorHash = compilationErrorHash,
					IsCompilationError = !string.IsNullOrWhiteSpace(compilationError),
					OutputHash = outputHash,
					ExecutionServiceName = executionServiceName,
					DisplayName = displayName,
					Status = status,
					IsRightAnswer = false,
				};

				db.AutomaticExerciseCheckings.Add(automaticChecking);
			}
			else
			{
				automaticChecking = null;
			}

			var submission = new UserExerciseSubmission
			{
				CourseId = courseId,
				SlideId = slideId,
				UserId = userId,
				Timestamp = DateTime.Now,
				SolutionCodeHash = hash,
				CodeHash = code.Split('\n').Select(x => x.Trim()).Aggregate("", (x, y) => x + y).GetHashCode(),
				Likes = new List<Like>(),
				AutomaticChecking = automaticChecking,
				AutomaticCheckingIsRightAnswer = automaticChecking?.IsRightAnswer ?? true,
				Language = language,
				Sandbox = sandbox
			};

			db.UserExerciseSubmissions.Add(submission);

			await db.SaveChangesAsync();

			return submission;
		}

		public async Task RemoveSubmission(UserExerciseSubmission submission)
		{
			if (submission.Likes != null)
				db.SolutionLikes.RemoveRange(submission.Likes);
			if (submission.AutomaticChecking != null)
				db.AutomaticExerciseCheckings.Remove(submission.AutomaticChecking);
			if (submission.ManualCheckings != null)
				db.ManualExerciseCheckings.RemoveRange(submission.ManualCheckings);

			db.UserExerciseSubmissions.Remove(submission);
			await db.SaveChangesAsync();
		}

		///<returns>(likesCount, isLikedByThisUsed)</returns>
		public async Task<Tuple<int, bool>> Like(int solutionId, string userId)
		{
			return await FuncUtils.TrySeveralTimesAsync(() => TryLike(solutionId, userId), 3);
		}

		private async Task<Tuple<int, bool>> TryLike(int solutionId, string userId)
		{
			using (var transaction = db.Database.BeginTransaction())
			{
				var solutionForLike = await db.UserExerciseSubmissions.FindAsync(solutionId);
				if (solutionForLike == null)
					throw new Exception("Solution " + solutionId + " not found");
				var hisLike = await db.SolutionLikes.FirstOrDefaultAsync(like => like.UserId == userId && like.SubmissionId == solutionId);
				var votedAlready = hisLike != null;
				var likesCount = solutionForLike.Likes.Count;
				if (votedAlready)
				{
					db.SolutionLikes.Remove(hisLike);
					likesCount--;
				}
				else
				{
					db.SolutionLikes.Add(new Like { SubmissionId = solutionId, Timestamp = DateTime.Now, UserId = userId });
					likesCount++;
				}

				await db.SaveChangesAsync();

				await transaction.CommitAsync();

				return Tuple.Create(likesCount, !votedAlready);
			}
		}

		public IQueryable<UserExerciseSubmission> GetAllSubmissions(string courseId, bool includeManualAndAutomaticCheckings = true)
		{
			var query = db.UserExerciseSubmissions.AsQueryable();
			if (includeManualAndAutomaticCheckings)
				query = query
					.Include(s => s.ManualCheckings)
					.Include(s => s.AutomaticChecking);
			return query.Where(x => x.CourseId == courseId);
		}

		public IQueryable<UserExerciseSubmission> GetAllSubmissions(string courseId, IEnumerable<Guid> slidesIds)
		{
			return GetAllSubmissions(courseId).Where(x => slidesIds.Contains(x.SlideId));
		}

		public IQueryable<UserExerciseSubmission> GetAllSubmissions(string courseId, IEnumerable<Guid> slidesIds, DateTime periodStart, DateTime periodFinish)
		{
			return GetAllSubmissions(courseId, slidesIds)
				.Where(x =>
					periodStart <= x.Timestamp &&
					x.Timestamp <= periodFinish
				);
		}

		public IQueryable<UserExerciseSubmission> GetAllAcceptedSubmissions(string courseId, IEnumerable<Guid> slidesIds, DateTime periodStart, DateTime periodFinish)
		{
			return GetAllSubmissions(courseId, slidesIds, periodStart, periodFinish).Where(s => s.AutomaticCheckingIsRightAnswer);
		}

		public IQueryable<UserExerciseSubmission> GetAllAcceptedSubmissions(string courseId, IEnumerable<Guid> slidesIds)
		{
			return GetAllSubmissions(courseId, slidesIds).Where(s => s.AutomaticCheckingIsRightAnswer);
		}

		public IQueryable<UserExerciseSubmission> GetAllAcceptedSubmissions(string courseId)
		{
			return GetAllSubmissions(courseId).Where(s => s.AutomaticCheckingIsRightAnswer);
		}

		public IQueryable<UserExerciseSubmission> GetAllAcceptedSubmissionsByUser(string courseId, IEnumerable<Guid> slideIds, string userId)
		{
			return GetAllAcceptedSubmissions(courseId, slideIds).Where(s => s.UserId == userId);
		}

		public IQueryable<UserExerciseSubmission> GetAllAcceptedSubmissionsByUser(string courseId, string userId)
		{
			return GetAllAcceptedSubmissions(courseId).Where(s => s.UserId == userId);
		}

		public IQueryable<UserExerciseSubmission> GetAllAcceptedSubmissionsByUser(string courseId, Guid slideId, string userId)
		{
			return GetAllAcceptedSubmissionsByUser(courseId, new List<Guid> { slideId }, userId);
		}

		public IQueryable<UserExerciseSubmission> GetAllSubmissionsByUser(string courseId, Guid slideId, string userId)
		{
			return GetAllSubmissions(courseId, new List<Guid> { slideId }).Where(s => s.UserId == userId);
		}

		public IQueryable<UserExerciseSubmission> GetAllSubmissionsByUsers(SubmissionsFilterOptions filterOptions)
		{
			var submissions = GetAllSubmissions(filterOptions.CourseId, filterOptions.SlideIds);
			if (filterOptions.IsUserIdsSupplement)
				submissions = submissions.Where(s => !filterOptions.UserIds.Contains(s.UserId));
			else
				submissions = submissions.Where(s => filterOptions.UserIds.Contains(s.UserId));
			return submissions;
		}

		public IQueryable<AutomaticExerciseChecking> GetAutomaticExerciseCheckingsByUsers(string courseId, Guid slideId, List<string> userIds)
		{
			var query = db.AutomaticExerciseCheckings.Where(c => c.CourseId == courseId && c.SlideId == slideId);
			if (userIds != null)
				query = query.Where(v => userIds.Contains(v.UserId));
			return query;
		}

		public async Task<List<AcceptedSolutionInfo>> GetBestTrendingAndNewAcceptedSolutions(string courseId, List<Guid> slidesIds)
		{
			var prepared = await GetAllAcceptedSubmissions(courseId, slidesIds)
				.GroupBy(x => x.CodeHash,
					(codeHash, ss) => new
					{
						codeHash,
						timestamp = ss.Min(s => s.Timestamp)
					})
				.Join(
					GetAllAcceptedSubmissions(courseId, slidesIds),
					g => g,
					s => new { codeHash = s.CodeHash, timestamp = s.Timestamp }, (k, s) => new { submission = s, k.timestamp })
				.Select(x => new { x.submission.Id, likes = x.submission.Likes.Count, x.timestamp })
				.ToListAsync();

			var best = prepared
				.OrderByDescending(x => x.likes);
			var timeNow = DateTime.Now;
			var trending = prepared
				.OrderByDescending(x => (x.likes + 1) / timeNow.Subtract(x.timestamp).TotalMilliseconds);
			var newest = prepared
				.OrderByDescending(x => x.timestamp);
			var selectedSubmissionsIds = best.Take(3).Concat(trending.Take(3)).Concat(newest).Distinct().Take(10).Select(x => x.Id);

			var selectedSubmissions = await db.UserExerciseSubmissions
				.Where(s => selectedSubmissionsIds.Contains(s.Id))
				.Select(s => new
				{
					s.Id,
					Code = s.SolutionCode.Text,
					Likes = s.Likes.Select(y => y.UserId)
				})
				.ToListAsync();
			return selectedSubmissions
				.Select(s => new AcceptedSolutionInfo(s.Code, s.Id, s.Likes))
				.OrderByDescending(info => info.UsersWhoLike.Count)
				.ToList();
		}

		public async Task<List<AcceptedSolutionInfo>> GetBestTrendingAndNewAcceptedSolutions(string courseId, Guid slideId)
		{
			return await GetBestTrendingAndNewAcceptedSolutions(courseId, new List<Guid> { slideId });
		}

		public async Task<int> GetAcceptedSolutionsCount(string courseId, Guid slideId)
		{
			return await GetAllAcceptedSubmissions(courseId, new List<Guid> { slideId })
				.Select(x => x.UserId)
				.Distinct()
				.CountAsync();
		}

		public async Task<bool> IsCheckingSubmissionByUser(string courseId, Guid slideId, string userId, DateTime periodStart, DateTime periodFinish)
		{
			var automaticCheckingsIds = await GetAllSubmissions(courseId, new List<Guid> { slideId }, periodStart, periodFinish)
				.Where(s => s.UserId == userId)
				.Select(s => s.AutomaticCheckingId)
				.ToListAsync();
			return await db.AutomaticExerciseCheckings
				.AnyAsync(c => automaticCheckingsIds.Contains(c.Id) && c.Status != AutomaticExerciseCheckingStatus.Done);
		}

		public async Task<HashSet<Guid>> GetIdOfPassedSlides(string courseId, string userId)
		{
			using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew, new TransactionOptions { IsolationLevel = IsolationLevel.ReadUncommitted}, TransactionScopeAsyncFlowOption.Enabled))
			{
				var ids = await db.AutomaticExerciseCheckings
					.Where(x => x.IsRightAnswer && x.CourseId == courseId && x.UserId == userId)
					.Select(x => x.SlideId)
					.Distinct()
					.ToListAsync();
				scope.Complete();
				return new HashSet<Guid>(ids);
			}
		}

		public async Task<bool> IsSlidePassed(string courseId, string userId, Guid slideId)
		{
			using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew, new TransactionOptions { IsolationLevel = IsolationLevel.ReadUncommitted}, TransactionScopeAsyncFlowOption.Enabled))
			{
				var result = await db.AutomaticExerciseCheckings
					.AnyAsync(x => x.IsRightAnswer && x.CourseId == courseId && x.UserId == userId && x.SlideId == slideId);
				scope.Complete();
				return result;
			}
		}

		public IQueryable<UserExerciseSubmission> GetAllSubmissions(int max, int skip)
		{
			return db.UserExerciseSubmissions
				.OrderByDescending(x => x.Timestamp)
				.Skip(skip)
				.Take(max);
		}

		public async Task<UserExerciseSubmission> FindNoTrackingSubmission(int id)
		{
			return await FuncUtils.TrySeveralTimesAsync(async () => await TryFindNoTrackingSubmission(id), 3, () =>  Task.Delay(200));
		}

		private async Task<UserExerciseSubmission> TryFindNoTrackingSubmission(int id)
		{
			var submission = await db.UserExerciseSubmissions
				.Include(s => s.AutomaticChecking)
				.AsNoTracking() // В core побочный эффект - отключение dinamic proxy
				.SingleOrDefaultAsync(x => x.Id == id);
			if (submission == null)
				return null;
			submission.SolutionCode = await textsRepo.GetText(submission.SolutionCodeHash);

			if (submission.AutomaticChecking != null)
			{
				submission.AutomaticChecking.Output = await textsRepo.GetText(submission.AutomaticChecking.OutputHash);
				submission.AutomaticChecking.CompilationError = await textsRepo.GetText(submission.AutomaticChecking.CompilationErrorHash);
			}

			return submission;
		}

		public async Task<UserExerciseSubmission> GetUnhandledSubmission(string agentName, List<string> sandboxes)
		{
			try
			{
				return await TryGetExerciseSubmission(agentName, sandboxes);
			}
			catch (Exception e)
			{
				log.Error("GetUnhandledSubmission() error", e);
				return null;
			}
		}

		private static volatile SemaphoreSlim getSubmissionSemaphore = new SemaphoreSlim(1);

		private async Task<UserExerciseSubmission> TryGetExerciseSubmission(string agentName, List<string> sandboxes)
		{
			var notSoLongAgo = DateTime.Now - TimeSpan.FromMinutes(15);

			var submissionsQueryable = db.UserExerciseSubmissions
				.Include(c => c.AutomaticChecking)
				.AsNoTracking() // В core побочный эффект - отключение dinamic proxy
				.Where(s =>
					s.Timestamp > notSoLongAgo
					&& s.AutomaticChecking.Status == AutomaticExerciseCheckingStatus.Waiting
					&& sandboxes.Contains(s.Sandbox));

			var maxId = await submissionsQueryable.Select(s => s.Id).MaxAsync(i => (int?)i) ?? -1;
			if (maxId == -1)
				return null;

			// NOTE: Если транзакция здесь, а не в начале метода, может возникнуть ситуация, что maxId только что кто-то взял, и мы тоже взяли.
			// То, что мы не обработаем дважды, защищает проверка на Waiting внутри транзакции ниже.
			// Мы можем не взять из-за этого другой solution. Не стращно, попробуем снова сразу же с помощью WaitAnyUnhandledSubmissions (см. RunnerController.GetSubmissions).
			// RepeatableRead блокирует от изменения те строки, которые видел.
			// Serializable отличается от него только тем, что другая транзакция не добавит другую строку, которая тоже будет подходить под запрос, даже после того, как запрос совершен.
			// Нам важно только, чтобы не менялись виденные в транзакции строки, поэтому подходит RepeatableRead.
			// Хотя, здесь делается запрос просто по Id, поэтому в любом случае заблокированных строк мало.
			// Малое количество затронутых строк должно уменьшить возможность дедлоков. Теоретически, если обе прочитают одно и то же и заходят записать, должна сработать одна транзакция и одна откатиться.
			// Дедлоки всё-таки есть в большом количестве, поэтому поставил Semaphore
			log.Debug("GetUnhandledSubmission(): trying to acquire semaphore");
			var semaphoreLocked = await getSubmissionSemaphore.WaitAsync(TimeSpan.FromSeconds(2));
			if (!semaphoreLocked)
			{
				log.Error("TryGetExerciseSubmission(): Can't lock semaphore for 2 seconds");
				return null;
			}

			log.Debug("GetUnhandledSubmission(): semaphore acquired!");
			try
			{
				UserExerciseSubmission submission;
				using (var transaction = db.Database.BeginTransaction(System.Data.IsolationLevel.RepeatableRead))
				{
					submission = await db.UserExerciseSubmissions
						.Include(s => s.AutomaticChecking)
						.Include(s => s.SolutionCode)
						.AsNoTracking() // В core побочный эффект - отключение dinamic proxy
						.FirstOrDefaultAsync(s => s.Id == maxId);
					if (submission == null)
						return null;

					if (submission.AutomaticChecking.Status != AutomaticExerciseCheckingStatus.Waiting)
						return null;

					/* Mark submission as "running" */
					submission.AutomaticChecking.Status = AutomaticExerciseCheckingStatus.Running;
					submission.AutomaticChecking.CheckingAgentName = agentName;

					await SaveAll(new List<AutomaticExerciseChecking> { submission.AutomaticChecking });

					await transaction.CommitAsync();

					db.ChangeTracker.AcceptAllChanges();
				}

				unhandledSubmissions.TryRemove(submission.Id, out _);

				return submission;
			}
			catch (Exception e)
			{
				log.Error("TryGetExerciseSubmission() error", e);
				return null;
			}
			finally
			{
				log.Debug("GetUnhandledSubmission(): trying to release semaphore");
				getSubmissionSemaphore.Release();
				log.Debug("GetUnhandledSubmission(): semaphore released");
			}
		}

		public async Task<UserExerciseSubmission> FindSubmissionById(int id)
		{
			return await db.UserExerciseSubmissions.FindAsync(id);
		}

		public async Task<UserExerciseSubmission> FindSubmissionById(string idString)
		{
			return int.TryParse(idString, out var id) ? await FindSubmissionById(id) : null;
		}

		public async Task<List<UserExerciseSubmission>> FindSubmissionsByIds(IEnumerable<int> checkingsIds)
		{
			return await db.UserExerciseSubmissions.Where(c => checkingsIds.Contains(c.Id)).ToListAsync();
		}

		private async Task UpdateIsRightAnswerForSubmission(AutomaticExerciseChecking checking)
		{
			(await db.UserExerciseSubmissions
				.Where(s => s.AutomaticCheckingId == checking.Id)
				.ToListAsync())
				.ForEach(s => s.AutomaticCheckingIsRightAnswer = checking.IsRightAnswer);
		}

		private async Task SaveAll(IEnumerable<AutomaticExerciseChecking> checkings)
		{
			foreach (var checking in checkings)
			{
				log.Info($"Обновляю статус автоматической проверки #{checking.Id}: {checking.Status}");
				db.AddOrUpdate(checking, c => c.Id == checking.Id);
				await UpdateIsRightAnswerForSubmission(checking);
			}

			db.ChangeTracker.DetectChanges();
			await db.SaveChangesAsync();
		}

		public async Task SaveResult(RunningResults result, Func<UserExerciseSubmission, Task> onSave)
		{
			using (var transaction = db.Database.BeginTransaction())
			{
				log.Info($"Сохраняю информацию о проверке решения {result.Id}");
				var submission = await FindSubmissionById(result.Id);
				if (submission == null)
				{
					log.Warn($"Не нашёл в базе данных решение {result.Id}");
					return;
				}

				var aec = await UpdateAutomaticExerciseChecking(submission.AutomaticChecking, result);
				await SaveAll(Enumerable.Repeat(aec, 1));

				await onSave(submission);

				await transaction.CommitAsync();
				db.ChangeTracker.AcceptAllChanges();

				if (!handledSubmissions.TryAdd(submission.Id, DateTime.Now))
					log.Warn($"Не удалось запомнить, что проверка {submission.Id} проверена, а результат сохранен в базу");

				log.Info($"Есть информация о следующих проверках, которые ещё не записаны в базу клиентом: [{string.Join(", ", handledSubmissions.Keys)}]");
			}
		}

		private async Task<AutomaticExerciseChecking> UpdateAutomaticExerciseChecking(AutomaticExerciseChecking checking, RunningResults result)
		{
			var compilationErrorHash = (await textsRepo.AddText(result.CompilationOutput)).Hash;
			var output = result.GetOutput().NormalizeEoln();
			var outputHash = (await textsRepo.AddText(output)).Hash;

			var isWebRunner = checking.CourseId == "web" && checking.SlideId == Guid.Empty;
			var exerciseSlide = isWebRunner
				? null
				: (ExerciseSlide)(await courseManager.GetCourseAsync(checking.CourseId))
					.GetSlideById(checking.SlideId, true);

			var isRightAnswer = IsRightAnswer(result, output, exerciseSlide?.Exercise);
			var score = exerciseSlide != null && isRightAnswer ? exerciseSlide.Scoring.PassedTestsScore : 0;

			/* For skipped slides score is always 0 */
			if (await visitsRepo.IsSkipped(checking.CourseId, checking.SlideId, checking.UserId))
				score = 0;

			var newChecking = new AutomaticExerciseChecking
			{
				Id = checking.Id,
				CourseId = checking.CourseId,
				SlideId = checking.SlideId,
				UserId = checking.UserId,
				Timestamp = checking.Timestamp,
				CompilationErrorHash = compilationErrorHash,
				IsCompilationError = result.Verdict == Verdict.CompilationError,
				OutputHash = outputHash,
				ExecutionServiceName = checking.ExecutionServiceName,
				Status = AutomaticExerciseCheckingStatus.Done,
				DisplayName = checking.DisplayName,
				Elapsed = DateTime.Now - checking.Timestamp,
				IsRightAnswer = isRightAnswer,
				Score = score,
				CheckingAgentName = checking.CheckingAgentName,
				Points = result.Points
			};

			return newChecking;
		}

		private bool IsRightAnswer(RunningResults result, string output, AbstractExerciseBlock exerciseBlock)
		{
			if (result.Verdict != Verdict.Ok)
				return false;

			/* For sandbox runner */
			if (exerciseBlock == null)
				return false;

			if (exerciseBlock.ExerciseType == ExerciseType.CheckExitCode)
				return true;

			if (exerciseBlock.ExerciseType == ExerciseType.CheckOutput)
			{
				var expectedOutput = exerciseBlock.ExpectedOutput.NormalizeEoln();
				return output.Equals(expectedOutput);
			}
			
			if (exerciseBlock.ExerciseType == ExerciseType.CheckPoints)
			{
				if (!result.Points.HasValue)
					return false;
				const float eps = 0.00001f;
				return exerciseBlock.SmallPointsIsBetter ? result.Points.Value < exerciseBlock.PassingPoints + eps : result.Points.Value > exerciseBlock.PassingPoints - eps;
			}

			throw new InvalidOperationException($"Unknown exercise type for checking: {exerciseBlock.ExerciseType}");
		}

		public async Task RunAutomaticChecking(UserExerciseSubmission submission, TimeSpan timeout, bool waitUntilChecked)
		{
			log.Info($"Запускаю автоматическую проверку решения. ID посылки: {submission.Id}");
			unhandledSubmissions.TryAdd(submission.Id, DateTime.Now);

			if (!waitUntilChecked)
			{
				log.Info($"Не буду ожидать результатов проверки посылки {submission.Id}");
				return;
			}

			var sw = Stopwatch.StartNew();
			while (sw.Elapsed < timeout)
			{
				await WaitUntilSubmissionHandled(TimeSpan.FromSeconds(5), submission.Id);
				var updatedSubmission = await FindNoTrackingSubmission(submission.Id);
				if (updatedSubmission == null)
					break;

				if (updatedSubmission.AutomaticChecking.Status == AutomaticExerciseCheckingStatus.Done)
				{
					log.Info($"Посылка {submission.Id} проверена. Результат: {updatedSubmission.AutomaticChecking.GetVerdict()}");
					return;
				}
			}

			/* If something is wrong */
			unhandledSubmissions.TryRemove(submission.Id, out _);
			throw new SubmissionCheckingTimeout();
		}

		public async Task<Dictionary<int, string>> GetSolutionsForSubmissions(IEnumerable<int> submissionsIds)
		{
			var solutionsHashes = await db.UserExerciseSubmissions
				.Where(s => submissionsIds.Contains(s.Id))
				.Select(s => new { Hash = s.SolutionCodeHash, SubmissionId = s.Id })
				.ToListAsync();
			var textsByHash = await textsRepo.GetTextsByHashes(solutionsHashes.Select(s => s.Hash));
			return solutionsHashes.ToDictSafe(
				s => s.SubmissionId,
				s => textsByHash.GetOrDefault(s.Hash, ""));
		}

		public async Task WaitAnyUnhandledSubmissions(TimeSpan timeout)
		{
			var sw = Stopwatch.StartNew();
			while (sw.Elapsed < timeout)
			{
				if (unhandledSubmissions.Count > 0)
				{
					log.Info($"Список невзятых пока на проверку решений: [{string.Join(", ", unhandledSubmissions.Keys)}]");
					ClearHandleDictionaries();
					return;
				}

				await Task.Delay(TimeSpan.FromMilliseconds(100));
			}
		}

		public async Task WaitUntilSubmissionHandled(TimeSpan timeout, int submissionId)
		{
			log.Info($"Вхожу в цикл ожидания результатов проверки решения {submissionId}. Жду {timeout.TotalSeconds} секунд");
			var sw = Stopwatch.StartNew();
			while (sw.Elapsed < timeout)
			{
				if (handledSubmissions.ContainsKey(submissionId))
				{
					DateTime value;
					handledSubmissions.TryRemove(submissionId, out value);
					return;
				}

				await Task.Delay(TimeSpan.FromMilliseconds(100));
			}
		}

		private static void ClearHandleDictionaries()
		{
			var timeout = DateTime.Now.Subtract(handleTimeout);
			ClearHandleDictionary(handledSubmissions, timeout);
			ClearHandleDictionary(unhandledSubmissions, timeout);
		}

		private static void ClearHandleDictionary(ConcurrentDictionary<int, DateTime> dictionary, DateTime timeout)
		{
			foreach (var key in dictionary.Keys)
			{
				if (dictionary.TryGetValue(key, out var value) && value < timeout)
					dictionary.TryRemove(key, out value);
			}
		}
	}

	public class SubmissionCheckingTimeout : Exception
	{
	}
}