﻿using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;
using Ulearn.Core.Courses.Slides.Blocks;

namespace Ulearn.Web.Api.Models.Responses.SlideBlocks
{
	[DataContract]
	[DisplayName("imageGallery")]
	public class ImageGalleryBlockResponse : IApiSlideBlock
	{
		[DefaultValue(false)]
		[DataMember(Name = "hide", EmitDefaultValue = false)]
		public bool Hide { get; set; }

		[DataMember(Name = "imageUrls")]
		public string[] ImageUrls { get; set; }

		public ImageGalleryBlockResponse(ImageGalleryBlock imageGalleryBlock, string baseUrlApi, string courseId, string unitPathRelativeToCourse)
		{
			Hide = imageGalleryBlock.Hide;
			ImageUrls = imageGalleryBlock.GetAbsoluteImageUrls(baseUrlApi, courseId, unitPathRelativeToCourse).ToArray();
		}

		public ImageGalleryBlockResponse()
		{
		}
	}
}