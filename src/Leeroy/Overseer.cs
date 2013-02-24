using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Leeroy.Json;
using Logos.Git;
using Logos.Git.GitHub;
using Logos.Utility.Logging;

namespace Leeroy
{
	public sealed class Overseer
	{
		public Overseer(CancellationToken token, BuildServerClient buildServerClient, GitHubClient gitHubClient, string user, string repo, string branch)
		{
			m_token = token;
			m_buildServerClient = buildServerClient;
			m_gitHubClient = gitHubClient;
			m_user = user;
			m_repo = repo;
			m_branch = branch;
		}

		public void Run(object obj)
		{
			m_token.ThrowIfCancellationRequested();

			// keep checking for updates
			while (!m_token.IsCancellationRequested)
			{
				if (m_watchers == null)
				{
					List<BuildProject> projects = LoadConfiguration();
					CreateWatchers(projects);
				}

				string commitId = m_gitHubClient.GetLatestCommitId(m_user, m_repo, m_branch);
				if (commitId != m_lastConfigurationCommitId)
				{
					Log.Info("Configuration repo commit ID has changed from {0} to {1}; reloading configuration.", m_lastConfigurationCommitId, commitId);

					// cancel existing work
					m_currentConfigurationTokenSource.Cancel();
					try
					{
						Task.WaitAll(m_watchers.ToArray());
					}
					catch (AggregateException)
					{
					}
					m_currentConfigurationTokenSource.Dispose();

					// force configuration to be reloaded
					m_watchers = null;
					continue;
				}

				m_token.WaitHandle.WaitOne(TimeSpan.FromSeconds(5));
			}
		}

		private List<BuildProject> LoadConfiguration()
		{
			Log.Info("Getting latest commit for {0}/{1}/{2}.", m_user, m_repo, m_branch);
			m_lastConfigurationCommitId = m_gitHubClient.GetLatestCommitId(m_user, m_repo, m_branch);

			m_token.ThrowIfCancellationRequested();

			Log.Info("Latest commit is {0}; getting details.", m_lastConfigurationCommitId);
			GitCommit gitCommit = m_gitHubClient.GetGitCommit(m_user, m_repo, m_lastConfigurationCommitId);

			m_token.ThrowIfCancellationRequested();

			Log.Debug("Fetching commit tree ({0}).", gitCommit.Tree.Sha);
			GitTree tree = m_gitHubClient.GetTree(gitCommit);

			m_token.ThrowIfCancellationRequested();

			Log.Debug("Tree has {0} items:", tree.Items.Length);
			foreach (GitTreeItem item in tree.Items)
				Log.Debug(item.Path);

			List<BuildProject> buildProjects = new List<BuildProject>();

			foreach (GitTreeItem item in tree.Items.Where(x => x.Type == "blob" && x.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
			{
				GitBlob blob = m_gitHubClient.GetBlob(item);

				BuildProject buildProject;
				try
				{
					buildProject = JsonUtility.FromJson<BuildProject>(blob.GetContent());
				}
				catch (FormatException ex)
				{
					Log.Error("Couldn't parse '{0}': {1}", ex, item.Path, ex.Message);
					continue;
				}

				if (!buildProject.Disabled)
				{
					buildProject.Name = Path.GetFileNameWithoutExtension(item.Path);
					buildProjects.Add(buildProject);
					Log.Info("Added build project: {0}", item.Path);
				}
				else
				{
					Log.Info("Ignoring disabled build project: {0}", item.Path);
				}
			}

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
				watchers.Add(watcher.CreateTask());
			}

			m_currentConfigurationTokenSource = tokenSource;
			m_watchers = watchers;
		}

		readonly CancellationToken m_token;
		readonly BuildServerClient m_buildServerClient;
		readonly GitHubClient m_gitHubClient;
		readonly string m_user;
		readonly string m_repo;
		readonly string m_branch;

		string m_lastConfigurationCommitId;

		CancellationTokenSource m_currentConfigurationTokenSource;
		List<Task> m_watchers;

		static readonly Logger Log = LogManager.GetLogger("Overseer");
	}
}
