﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Leeroy.Json;
using Logos.Utility.Logging;
using Newtonsoft.Json;
using Octokit;

namespace Leeroy
{
	public sealed class Overseer
	{
		public Overseer(CancellationToken token, BuildServerClient buildServerClient, GitHubClientWrapper gitHubClient, string user, string repo, string branch)
		{
			m_token = token;
			m_buildServerClient = buildServerClient;
			m_gitHubClient = gitHubClient;
			m_user = user;
			m_repo = repo;
			m_branch = branch;
		}

		public async Task Run()
		{
			m_token.ThrowIfCancellationRequested();

			// keep checking for updates
			while (!m_token.IsCancellationRequested)
			{
				if (m_watchers == null)
				{
					List<BuildProject> projects = await LoadConfiguration();
					CreateWatchers(projects);
				}

				string commitId = await m_gitHubClient.GetCommitId(m_user, m_repo, m_branch);
				m_token.ThrowIfCancellationRequested();

				if (commitId != m_lastConfigurationCommitId)
				{
					Log.Info("Configuration repo commit ID has changed from {0} to {1}; reloading configuration.", m_lastConfigurationCommitId, commitId);

					// cancel existing work
					m_currentConfigurationTokenSource.Cancel();
					try
					{
						await Task.WhenAll(m_watchers);
					}
					catch (AggregateException)
					{
					}
					m_currentConfigurationTokenSource.Dispose();

					// force configuration to be reloaded
					m_watchers = null;
					continue;
				}

				await Task.Delay(TimeSpan.FromSeconds(5), m_token);
			}
		}

		private async Task<List<BuildProject>> LoadConfiguration()
		{
			Log.Info("Getting latest commit for {0}/{1}/{2}.", m_user, m_repo, m_branch);
			m_lastConfigurationCommitId = await m_gitHubClient.GetCommitId(m_user, m_repo, m_branch);

			m_token.ThrowIfCancellationRequested();

			Log.Info("Latest commit is {0}; getting details.", m_lastConfigurationCommitId);
			var gitCommit = await m_gitHubClient.GetCommit(m_user, m_repo, m_lastConfigurationCommitId);

			m_token.ThrowIfCancellationRequested();

			Log.Debug("Fetching commit tree ({0}).", gitCommit.Tree.Sha);
			var tree = await m_gitHubClient.GetTree(m_user, m_repo, gitCommit.Tree.Sha);

			m_token.ThrowIfCancellationRequested();

			Log.Debug("Tree has {0} items:", tree.Tree.Count);
			foreach (var item in tree.Tree)
				Log.Debug(item.Path);

			object buildProjectsLock = new object();
			List<BuildProject> buildProjects = new List<BuildProject>();
			Dictionary<string, string> buildRepoBranches = new Dictionary<string, string>();

			var tasks = tree.Tree
				.Where(x => x.Type == TreeType.Blob && x.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
				.Select(async item =>
				{
					var blob = await m_gitHubClient.GetBlob(m_user, m_repo, item.Sha);

					BuildProject buildProject;
					try
					{
						buildProject = JsonConvert.DeserializeObject<BuildProject>(blob.GetContent());
					}
					catch (JsonSerializationException ex)
					{
						Log.Error("Couldn't parse '{0}': {1}", ex, item.Path, ex.Message);
						return;
					}

					buildProject.Name = Path.GetFileNameWithoutExtension(item.Path);

					if (buildProject.Disabled)
					{
						Log.Info("Ignoring disabled build project: {0}", buildProject.Name);
						return;
					}

					if (buildProject.Submodules != null && buildProject.SubmoduleBranches != null)
					{
						Log.Error("Cannot specify both 'submodules' and 'submoduleBranches' in {0}.", buildProject.Name);
						return;
					}

					string existingProjectName;
					lock (buildProjectsLock)
					{
						string buildRepoBranch = buildProject.RepoUrl + "/" + buildProject.Branch;
						if (!buildRepoBranches.TryGetValue(buildRepoBranch, out existingProjectName))
							buildRepoBranches.Add(buildRepoBranch, buildProject.Name);
					}

					if (existingProjectName != null)
					{
						Log.Error("Project '{0}' is using the same build repo branch ({1}, {2}) as '{3}'; ignoring this project.", buildProject.Name, buildProject.RepoUrl, buildProject.Branch, existingProjectName);

						// disable the existing project, too; we don't know which one is correct and don't want spurious build commits to be pushed
						lock (buildProjectsLock)
							if (buildProjects.RemoveAll(x => x.Name == existingProjectName) != 0)
								Log.Error("Project '{0}' is using the same build repo branch ({1}, {2}) as '{3}'; ignoring this project.", existingProjectName, buildProject.RepoUrl, buildProject.Branch, buildProject.Name);

						return;
					}

					lock (buildProjectsLock)
						buildProjects.Add(buildProject);
					Log.Info("Added build project: {0}", buildProject.Name);
				});
			await Task.WhenAll(tasks);

			return buildProjects;
		}

		private void CreateWatchers(IEnumerable<BuildProject> projects)
		{
			// create a new cancellation token for all the monitors about to be created
			CancellationTokenSource tokenSource = new CancellationTokenSource();
			CancellationTokenSource linkedSource = CancellationTokenSource.CreateLinkedTokenSource(m_token, tokenSource.Token);
			CancellationToken linkedToken = linkedSource.Token;

			// create a watcher for each project
			List<Task> watchers = new List<Task>();
			foreach (BuildProject project in projects)
			{
				Watcher watcher = new Watcher(project, m_buildServerClient, m_gitHubClient, linkedToken);
				watchers.Add(watcher.Run());
			}

			m_currentConfigurationTokenSource = tokenSource;
			m_watchers = watchers;
		}

		readonly CancellationToken m_token;
		readonly BuildServerClient m_buildServerClient;
		readonly GitHubClientWrapper m_gitHubClient;
		readonly string m_user;
		readonly string m_repo;
		readonly string m_branch;

		string m_lastConfigurationCommitId;

		CancellationTokenSource m_currentConfigurationTokenSource;
		List<Task> m_watchers;

		static readonly Logger Log = LogManager.GetLogger("Overseer");
	}
}
