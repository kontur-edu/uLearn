﻿using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Options;
using Serilog;
using Ulearn.Common.Api;
using Ulearn.VideoAnnotations.Api.Client;
using Ulearn.VideoAnnotations.Api.Models.Parameters.Annotations;
using Ulearn.VideoAnnotations.Api.Models.Responses.Annotations;
using Web.Api.Configuration;

namespace Ulearn.Web.Api.Clients
{
	public interface IUlearnVideoAnnotationsClient
	{
		Task<Annotation> GetVideoAnnotations(string videoAnnotationsGoogleDoc, string videoId);
	}

	public class UlearnVideoAnnotationsClient : IUlearnVideoAnnotationsClient
	{
		private readonly ILogger logger;
		[CanBeNull]
		private readonly VideoAnnotationsClient instance;

		public UlearnVideoAnnotationsClient(ILogger logger, IOptions<WebApiConfiguration> options)
		{
			var videoAnnotationsClientConfiguration = options.Value.VideoAnnotationsClient;
			instance = string.IsNullOrEmpty(videoAnnotationsClientConfiguration?.Endpoint)
				? null
				: new VideoAnnotationsClient(
					logger,
					videoAnnotationsClientConfiguration.Endpoint
				);
		}

		[ItemCanBeNull]
		public async Task<Annotation> GetVideoAnnotations(string videoAnnotationsGoogleDoc, string videoId)
		{
			if (string.IsNullOrEmpty(videoAnnotationsGoogleDoc))
				return null;
			var client = instance;
			if (client == null)
				return null;
			try
			{
				var annotationsResponse = await client.GetAnnotationsAsync(new AnnotationsParameters
				{
					GoogleDocId = videoAnnotationsGoogleDoc,
					VideoId = videoId
				});
				return annotationsResponse.Annotation;
			}
			catch (ApiClientException e)
			{
				return null;
			}
		}
	}
}