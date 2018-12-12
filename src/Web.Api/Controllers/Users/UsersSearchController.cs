using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Database;
using Database.Models;
using Database.Repos.CourseRoles;
using Database.Repos.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using Swashbuckle.AspNetCore.Annotations;
using Ulearn.Web.Api.Models.Parameters.Users;
using Ulearn.Web.Api.Models.Responses;
using Ulearn.Web.Api.Models.Responses.Users;

namespace Ulearn.Web.Api.Controllers.Users
{
	[Route("/users/search")]
	[Authorize]	
	public class UsersSearchController : BaseController
	{
		private readonly IUserSearcher userSearcher;
		private readonly ICourseRolesRepo courseRolesRepo;

		public UsersSearchController(ILogger logger, IWebCourseManager courseManager, UlearnDb db, IUsersRepo usersRepo, IUserSearcher userSearcher, ICourseRolesRepo courseRolesRepo)
			: base(logger, courseManager, db, usersRepo)
		{
			this.userSearcher = userSearcher;
			this.courseRolesRepo = courseRolesRepo;
		}

		[HttpGet]
		public async Task<ActionResult<UsersSearchResponse>> Search([FromQuery] UsersSearchParameters parameters)
		{
			var words = parameters.Query?.Split(' ', '\t').ToList() ?? new List<string>();
			if (words.Count > 10)
				return BadRequest(new ErrorResponse("Too many words in query"));
			
			var currentUser = await usersRepo.FindUserByIdAsync(UserId).ConfigureAwait(false);
			var isSystemAdministrator = usersRepo.IsSystemAdministrator(currentUser);

			if (!string.IsNullOrEmpty(parameters.CourseId))
			{
				if (!parameters.CourseRoleType.HasValue)
					return BadRequest(new ErrorResponse("You should specify course_role with course_id"));
				if (parameters.CourseRoleType == CourseRoleType.Student)
					return BadRequest(new ErrorResponse("You can not search students by this method: there are too many students"));
				
				/* Only instructors can search by course role */
				var isInstructor = await courseRolesRepo.HasUserAccessToCourseAsync(UserId, parameters.CourseId, CourseRoleType.Instructor).ConfigureAwait(false);
				if (!isInstructor)
					return StatusCode((int) HttpStatusCode.Unauthorized, new ErrorResponse("Only instructors can search by course role")); 
			}
			else if (parameters.CourseRoleType.HasValue)
			{
				/* Only sys-admins can search all instructors or all course-admins */
				if (!isSystemAdministrator)
					return StatusCode((int) HttpStatusCode.Unauthorized, new ErrorResponse("Only system administrator can search by course role without specified course_id"));
			}

			if (parameters.LmsRoleType.HasValue)
			{
				if (!isSystemAdministrator)
					return StatusCode((int) HttpStatusCode.Unauthorized, new ErrorResponse("Only system administrator can search by lms role"));
			}

			var request = new UserSearchRequest
			{
				CurrentUser = currentUser,
				Words = words,
				CourseId = parameters.CourseId,
				MinCourseRoleType = parameters.CourseRoleType,
				LmsRole = parameters.LmsRoleType,
			};
			var strictUsers = await userSearcher.SearchUsersAsync(request, strict: true).ConfigureAwait(false);
			var nonStrictUsers = await userSearcher.SearchUsersAsync(request, strict: false).ConfigureAwait(false);
			
			/* Make copy */
			var users = strictUsers.ToList();
			/* ... and add to the copy all non-strict users if there is no this user in strict users list */
			foreach (var user in nonStrictUsers)
			{
				var alreadyExistUser = strictUsers.FirstOrDefault(u => u.User.Id == user.User.Id);
				if (alreadyExistUser != null)
					alreadyExistUser.Fields.UnionWith(user.Fields);
				else
					users.Add(user);
			}
			return new UsersSearchResponse
			{
				Users = users.Select(u => new FoundUserResponse
				{
					User = BuildShortUserInfo(u.User, discloseLogin: u.Fields.Contains(SearchField.Login), discloseEmail: u.Fields.Contains(SearchField.Email)),
					Fields = u.Fields.ToList(),
				}).ToList(),
			};
		}
	}
}