﻿using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace Ulearn.Core.Configuration
{
	public static class ApplicationConfiguration
	{
		public static T Read<T>(IDictionary<string, string> initialData, bool isAppsettingsJsonOptional=false) where T : AbstractConfiguration
		{
			var configuration = GetConfiguration(initialData, isAppsettingsJsonOptional);
			return configuration.Get<T>();
		}
		
		public static T Read<T>(bool isAppsettingsJsonOptional=false) where T : AbstractConfiguration
		{
			return Read<T>(new Dictionary<string, string>(), isAppsettingsJsonOptional);
		}

		public static IConfiguration GetConfiguration(IDictionary<string, string> initialData, bool isAppsettingsJsonOptional=false)
		{
			var applicationPath = string.IsNullOrEmpty(Utils.WebApplicationPhysicalPath)
				? AppDomain.CurrentDomain.BaseDirectory
				: Utils.WebApplicationPhysicalPath;
			var configurationBuilder = new ConfigurationBuilder()
				.AddInMemoryCollection(initialData)
				.SetBasePath(applicationPath);
			configurationBuilder.AddEnvironmentVariables();
			BuildAppsettingsConfiguration(configurationBuilder);
			return configurationBuilder.Build();
		}

		public static void BuildAppsettingsConfiguration(IConfigurationBuilder configurationBuilder)
		{
			configurationBuilder.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
			var environmentName = Environment.GetEnvironmentVariable("UlearnEnvironmentName");
			if(environmentName != null && environmentName.ToLower().Contains("local"))
				configurationBuilder.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);
		}
		
		public static IConfiguration GetConfiguration(bool isAppsettingsJsonOptional=false)
		{
			return GetConfiguration(new Dictionary<string, string>(), isAppsettingsJsonOptional);
		}
		
	}
	
	public abstract class AbstractConfiguration
	{
		 
	}
	
	public class HostLogConfiguration
	{
		public bool Console { get; set; }
		
		public string PathFormat { get; set; }
		
		public string MinimumLevel { get; set; }
		
		public bool EnableEntityFrameworkLogging { get; set; }
	}
	
	public class UlearnConfiguration : AbstractConfiguration
	{
		public TelegramConfiguration Telegram { get; set; }
		
		public string BaseUrl { get; set; }
		
		public string CoursesDirectory { get; set; }
		
		public bool BuildExerciseStudentZips { get; set; }
		
		public string ExerciseStudentZipsDirectory { get; set; }
		
		public CertificateConfiguration Certificates { get; set; }
		
		public string GraphiteServiceName { get; set; }
		
		public string Database { get; set; }
		
		public GitConfiguration Git { get; set; }
	}

	public class TelegramConfiguration
	{
		public string BotToken { get; set; }
		
		public ErrorsTelegramConfiguration Errors { get; set; }
	}

	public class ErrorsTelegramConfiguration
	{
		public string Channel { get; set; }
	}
	
	public class CertificateConfiguration
	{
		public string Directory { get; set; }
	}

	public class GitConfiguration
	{
		public GitWebhookConfiguration Webhook { get; set; }
	}

	public class GitWebhookConfiguration
	{
		public string Secret { get; set; }
	}
}