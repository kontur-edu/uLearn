﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Database.Models;
using JetBrains.Annotations;
using Ulearn.Web.Api.Controllers;
using Ulearn.Web.Api.Models.Common;
using Ulearn.Web.Api.Models.Responses.Review;

namespace Ulearn.Web.Api.Models.Responses.Exercise
{
	[DataContract]
	public class ReviewInfo
	{
		[CanBeNull]
		[DataMember]
		public ShortUserInfo Author; // null для ulearn bot

		[DataMember]
		public int StartLine;

		[DataMember]
		public int StartPosition;

		[DataMember]
		public int FinishLine;

		[DataMember]
		public int FinishPosition;

		[DataMember]
		public string Comment;

		[DataMember]
		public DateTime? AddingTime;

		[DataMember]
		public List<ReviewCommentResponse> Comments;

		public static ReviewInfo Build(ExerciseCodeReview r, [CanBeNull] IEnumerable<ExerciseCodeReviewComment> comments, bool isUlearnBot)
		{
			return new ReviewInfo
			{
				Comment = r.Comment,
				Author = isUlearnBot ? null : BaseController.BuildShortUserInfo(r.Author),
				AddingTime = isUlearnBot || r.AddingTime <= DateTime.UnixEpoch ? (DateTime?)null : r.AddingTime,
				FinishLine = r.FinishLine,
				FinishPosition = r.FinishPosition,
				StartLine = r.StartLine,
				StartPosition = r.StartPosition,
				Comments = comments
					.EmptyIfNull()
					.OrderBy(c => c.AddingTime)
					.Select(ReviewCommentResponse.Build)
					.ToList()
			};
		}
	}
}