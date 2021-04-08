﻿using System;
using System.Collections.Generic;
using System.Linq;
using AntiPlagiarism.Web.Database;
using Microsoft.EntityFrameworkCore;
using Ulearn.Common;

namespace ManualUtils.AntiPlagiarism
{

	public class FillLanguageToAntiplagiarism
	{
		private static Dictionary<Guid, Language?> taskIdToSubmission = new Dictionary<Guid, Language?>();

		private static Language? GetLanguageByTaskId(Guid taskId, AntiPlagiarismDb adb)
		{
			if (!taskIdToSubmission.ContainsKey(taskId))
			{
				var submission = adb.Submissions
					.OrderByDescending(s => s.AddingTime)
					.FirstOrDefault(s => s.TaskId == taskId);
				taskIdToSubmission[taskId] = submission?.Language;
			}
			return taskIdToSubmission[taskId];
		}

		private static void FillLanguageTasksStatisticsParameters(AntiPlagiarismDb adb)
		{
			Console.WriteLine("FillLanguageTasksStatisticsParameters");

			var parameterses = adb
				.TasksStatisticsParameters
				.Where(p => p.Language == 0)
				.ToList();

			var count = parameterses.Count;

			Console.WriteLine($"Count {parameterses.Count}");

			adb.DisableAutoDetectChanges();
			var completed = 0;
			foreach (var parameters in parameterses)
			{
				completed++;
				try
				{
					var newLanguage = GetLanguageByTaskId(parameters.TaskId, adb);
					if (newLanguage == null)
						continue;
					parameters.Language = newLanguage.Value;
					adb.TasksStatisticsParameters.Update(parameters);
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Error on id {parameters.TaskId}: {ex}");
				}

				if (count % 1000 == 0)
				{
					Console.WriteLine($"FillLanguageTasksStatisticsParameters - Completed {completed} / {count}");
					adb.SaveChanges();
				}
			}

			adb.SaveChanges();
			adb.EnableAutoDetectChanges();
		}

		private static void FillLanguageSnippetsStatistics(AntiPlagiarismDb adb)
		{
			Console.WriteLine("FillLanguageSnippetsStatistics");

			var taskIds = adb
				.SnippetsStatistics
				.Select(s => s.TaskId)
				.Distinct()
				.ToList();

			Console.WriteLine($"Count taskIds {taskIds.Count}");

			var taskIdToLanguage = new Dictionary<Guid, Language?>();
			foreach (var taskId in taskIds)
			{
				if (!taskIdToLanguage.ContainsKey(taskId))
					taskIdToLanguage[taskId] = GetLanguageByTaskId(taskId, adb);
			}

			var snippets = adb
				.SnippetsStatistics
				.AsNoTracking();

			var count = snippets.Count();

			Console.WriteLine($"Count snippets {count}");

			var changes = new List<(int Id, Language Language)>();
			var getChangesCompleted = 0;
			foreach (var snippet in snippets)
			{
				getChangesCompleted++;
				if (getChangesCompleted % 10000 == 0)
					Console.WriteLine($"getChangesCompleted {getChangesCompleted} / {count}");
				var newLanguage = taskIdToLanguage[snippet.TaskId];
				if (newLanguage == null)
					newLanguage = 0;
				if (newLanguage == snippet.Language)
					continue;
				changes.Add((snippet.Id, newLanguage.Value));
			}

			Console.WriteLine($"Found changes {changes.Count}");

			adb.DisableAutoDetectChanges();
			var completed = 0;
			foreach (var change in changes)
			{
				completed++;
				try
				{
					var snippetsStatistics = adb.SnippetsStatistics.Find(change.Id);
					snippetsStatistics.Language = change.Language;
					adb.SnippetsStatistics.Update(snippetsStatistics);
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Error on id {change.Id}: {ex}");
				}

				if (count % 1000 == 0)
				{
					Console.WriteLine($"FillLanguageSnippetsStatistics - Completed {completed} / {count}");
					adb.SaveChanges();
				}
			}

			adb.SaveChanges();
			adb.EnableAutoDetectChanges();
		}

		private static void FillLanguageManualSuspicionLevels(AntiPlagiarismDb adb)
		{
			Console.WriteLine("FillLanguageManualSuspicionLevels");

			var suspicionLevels = adb
				.ManualSuspicionLevels
				.Where(p => p.Language == 0)
				.ToList();

			var count = suspicionLevels.Count;

			Console.WriteLine($"Count {count}");

			adb.DisableAutoDetectChanges();
			var completed = 0;
			foreach (var level in suspicionLevels)
			{
				completed++;
				try
				{
					var newLanguage = GetLanguageByTaskId(level.TaskId, adb);
					if (newLanguage == null)
						continue;
					level.Language = newLanguage.Value;
					adb.ManualSuspicionLevels.Update(level);
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Error on id {level.TaskId}: {ex}");
				}

				if (count % 1000 == 0)
				{
					Console.WriteLine($"FillLanguageManualSuspicionLevels - Completed {completed} / {count}");
					adb.SaveChanges();
				}
			}

			adb.SaveChanges();
			adb.EnableAutoDetectChanges();
		}

		public static void FillLanguage(AntiPlagiarismDb adb)
		{
			FillLanguageManualSuspicionLevels(adb);
			FillLanguageTasksStatisticsParameters(adb);
			FillLanguageSnippetsStatistics(adb);
		}
	}
}