using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using JetBrains.Annotations;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using LibGit2Sharp.Ssh;
using Serilog;
using Ulearn.Common.Extensions;

namespace GitCourseUpdater
{
	public interface IGitRepo : IDisposable
	{
		MemoryStream GetCurrentStateAsZip();
		CommitInfo GetMasterLastCommitInfo();
		CommitInfo GetCommitInfo(string hash);
		List<string> GetChangedFiles(string fromHash, string toHash);
		void CheckoutCommit(string hash);
		void CheckoutBranchAndPull(string name);
	}
	
	public class GitRepo : IGitRepo
	{
		private string url;
		private CredentialsHandler credentialsHandler;
		private string reposBaseDir;
		private string repoDirName; 
		private Repository repo;
		private ILogger logger;
		private static object @lock = new object(); // Потокобезопасность не гарантируется библиотекой libgit2

		// url example ssh://git@github.com:user/myrepo.git
		public GitRepo(string url, string reposBaseDir, FileInfo publicKeyPath, FileInfo privateKeyPath, string privateKeyPassphrase, ILogger logger)
		{
			this.url = url;
			this.reposBaseDir = reposBaseDir;
			this.logger = logger;
			var reposFolderInfo = new DirectoryInfo(reposBaseDir);
			if (!reposFolderInfo.Exists)
				reposFolderInfo.Create();
			credentialsHandler = (_, __, ___) => new SshUserKeyCredentials
			{
				Username = "git",
				Passphrase = privateKeyPassphrase,
				PublicKey = publicKeyPath.ToString(),
				PrivateKey = privateKeyPath.ToString(),
			};
			Monitor.Enter(@lock);
			try
			{
				if (!TryUpdateExistingRepo())
					Clone();
			}
			catch(Exception)
			{
				Dispose(); // Если объект не создан, извне Dispose не вызовут
				throw;
			}
		}
		
		public MemoryStream GetCurrentStateAsZip()
		{
			logger.Information($"Start load '{repoDirName}' to zip");
			var zip = ZipHelper.CreateFromDirectory(Path.Combine(reposBaseDir, repoDirName), CompressionLevel.Optimal, false, Encoding.UTF8,
				s => s.StartsWith(".git"));
			logger.Information($"Successfully load '{repoDirName}' to zip");
			return zip;
		}
		
		public CommitInfo GetMasterLastCommitInfo()
		{
			var originMaster = repo.Branches["origin/master"];
			var lastCommit = originMaster.Tip;
			return ToCommitInfo(lastCommit);
		}
		
		[CanBeNull]
		public CommitInfo GetCommitInfo(string hash)
		{
			var commit = repo.Lookup<Commit>(hash);
			if (commit == null)
			{
				logger.Warning($"Commit not found repo '{repoDirName}' hash '{hash}'");
				return null;
			}

			return ToCommitInfo(commit);
		}
		

		// null, если коммит не найден
		[CanBeNull]
		public List<string> GetChangedFiles(string fromHash, string toHash)
		{
			var commitFrom = repo.Lookup<Commit>(fromHash);
			var commitTo = repo.Lookup<Commit>(toHash);
			if (commitFrom == null || commitTo == null)
				
				return null;
			var treeChanges = repo.Diff.Compare<TreeChanges>(commitFrom.Tree, commitTo.Tree);
			return treeChanges.Select(c => c.Path).ToList();
		}
		
		public void CheckoutCommit(string hash)
		{
			logger.Information($"Start checkout '{url}' commit '{hash}' in '{repoDirName}'");
			Commands.Checkout(repo, hash);
			logger.Information($"Successfully checkout '{url}' commit '{hash}' in '{repoDirName}'");
		}

		public void CheckoutBranchAndPull(string name)
		{
			name = name.Replace("origin/", "");
			var remoteBranch = repo.Branches[$"origin/{name}"];
			if (remoteBranch == null)
				throw new ArgumentException($"Branch 'origin/{name}' does not exist");
			if (repo.Branches[name] == null)
			{
				var localBranch = repo.CreateBranch(name, remoteBranch.Tip);
				repo.Branches.Update(localBranch, b =>
				{
					b.Remote = "origin";
					b.UpstreamBranch = $"refs/heads/{name}";
				});
			}
			logger.Information($"Start checkout '{url}' branch '{name}' in '{repoDirName}'");
			Commands.Checkout(repo, name);
			logger.Information($"Successfully checkout '{url}' branch '{name}' in '{repoDirName}'");
			Pull();
		}
		
		private static CommitInfo ToCommitInfo(Commit commit)
		{
			return new CommitInfo
			{
				Hash = commit.Sha,
				Message = commit.Message,
				AuthorName = commit.Author.Name,
				AuthorEmail = commit.Author.Email,
				Time = commit.Author.When,
			};
		}

		// Пытается найти уже склоненную версию репозитория на диске и обновить её из удаленного репозитория
		private bool TryUpdateExistingRepo()
		{
			try
			{
				repoDirName = GetExistingRepoFolderName();
				if (repoDirName == null)
					return false;
				var repoPath = Path.Combine(reposBaseDir, repoDirName);
				repo = new Repository(repoPath);
				HardReset();
				if(HasUncommittedChanges())
					throw new LibGit2SharpException($"Has uncommited changes in '{repoDirName}'");
				FetchAll();
				CheckoutBranchAndPull("master");
			}
			catch (Exception ex)
			{
				logger.Warning(ex, $"Could not update existing repository '{repoDirName}'");
				return false;
			}
			return true;
		}
		
		private string GetExistingRepoFolderName()
		{
			var names = new DirectoryInfo(reposBaseDir).GetDirectories(Url2Name() + "@*").Select(d => d.Name).ToList();
			if (names.Count == 0)
				return null;
			return names.Max();
		}

		// Создает чистую папку и клонирует в неё
		private void Clone()
		{
			repoDirName = Url2Name() + "@" + DateTime.Now.ToSortable();
			var repoPath = Path.Combine(reposBaseDir, repoDirName);
			logger.Information($"Start clone '{url}' into '{repoDirName}'");
			Repository.Clone(url, repoPath, new CloneOptions { CredentialsProvider = credentialsHandler });
			repo = new Repository(repoPath);
			logger.Information($"Successfully clone '{url}' into '{repoDirName}'");
		}

		private string Url2Name()
		{
			var parts = url.Split(':');
			var name = parts.Last().Substring(0, parts.Last().Length - 4).Replace('/', '_');
			return GetSafeFilename(name);
		}

		[CanBeNull]
		private static string GetSafeFilename(string filename)
		{
			return string.Join("_", filename.Split(Path.GetInvalidFileNameChars()));
		}

		private void FetchAll()
		{
			var logMessage = "";
			var options = new FetchOptions { CredentialsProvider = credentialsHandler };
			logger.Information($"Start fetch all '{url}' in '{repoDirName}'");
			foreach (var remote in repo.Network.Remotes)
			{
				var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification);
				Commands.Fetch(repo, remote.Name, refSpecs, options, logMessage);
			}
			logger.Information($"Successfully fetch all '{url}' in '{repoDirName}'");
		}

		private void Pull()
		{
			var options = new PullOptions { FetchOptions = new FetchOptions { CredentialsProvider = credentialsHandler } };
			// Так как сделан hard reset, мерджа не должно случиться
			var signature = new Signature(new Identity("MERGE_USER_NAME", "MERGE_USER_EMAIL"), DateTimeOffset.Now);
			logger.Information($"Start pull '{url}' in '{repoDirName}'");
			var result = Commands.Pull(repo, signature, options);
			logger.Information($"Successfully pull '{url}' in '{repoDirName}' with status {result.Status}");
			if (result.Status == MergeStatus.Conflicts)
				throw new LibGit2SharpException($"Pull status is {result.Status} for '{repoDirName}'");
		}

		// git reset --hard origin/master
		private void HardReset()
		{
			var originMaster = repo.Branches["origin/master"];
			logger.Information($"Start reset '{url}' in '{repoDirName}'");
			repo.Reset(ResetMode.Hard, originMaster.Tip);
			logger.Information($"Successfully reset '{url}' in '{repoDirName}'");
		}
		
		public bool HasUncommittedChanges()
		{
			var status = repo.RetrieveStatus();
			return status.IsDirty;
			
		}

		public void Dispose()
		{
			repo?.Dispose();
			Monitor.Exit(@lock);
		}
	}
}