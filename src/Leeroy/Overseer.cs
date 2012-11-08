using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Common.Logging;
using Leeroy.Json;

namespace Leeroy
{
	public sealed class Overseer
	{
		public Overseer(CancellationToken token, string user, string repo, string branch)
		{
			m_token = token;
			m_user = user;
			m_repo = repo;
			m_branch = branch;
		}

		public void Run(object obj)
		{
			m_token.ThrowIfCancellationRequested();

			// load initial configuration
			LoadConfiguration();

			// keep checking for updates
			while (!m_token.IsCancellationRequested)
			{
				string commitId = GitHubClient.GetLatestCommitId(m_user, m_repo, m_branch);
				if (commitId != m_lastConfigurationCommitId)
				{
					Log.InfoFormat("Configuration repo commit ID has changed from {0} to {1}; reloading configuration.", m_lastConfigurationCommitId, commitId);
					LoadConfiguration();
				}

				m_token.WaitHandle.WaitOne(5000);
			}
		}

		private List<Configuration> LoadConfiguration()
		{
			Log.InfoFormat("Getting latest commit for {0}/{1}/{2}.", m_user, m_repo, m_branch);
			m_lastConfigurationCommitId = GitHubClient.GetLatestCommitId(m_user, m_repo, m_branch);

			m_token.ThrowIfCancellationRequested();

			Log.InfoFormat("Latest commit is {0}; getting tree.", m_lastConfigurationCommitId);
			GitCommit gitCommit = GitHubClient.GetCommit(m_user, m_repo, m_lastConfigurationCommitId);

			m_token.ThrowIfCancellationRequested();

			Log.DebugFormat("Fetching commit tree ({0}).", gitCommit.Tree.Sha);
			GitTree tree = GitHubClient.Get<GitTree>(gitCommit.Tree.Url);

			m_token.ThrowIfCancellationRequested();

			Log.DebugFormat("Tree has {0} items:", tree.Items.Length);
			foreach (GitTreeItem item in tree.Items)
				Log.Debug(item.Path);

			List<Configuration> configurations = new List<Configuration>();

			foreach (GitTreeItem item in tree.Items.Where(x => x.Type == "blob" && x.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
			{
				GitBlob blob = GitHubClient.Get<GitBlob>(item.Url);

				Configuration configuration = JsonUtility.FromJson<Configuration>(blob.GetContent());
				configurations.Add(configuration);
				Log.InfoFormat("Added configuration: {0}", item.Path);
			}

			return configurations;
		}

		readonly CancellationToken m_token;
		readonly string m_user;
		readonly string m_repo;
		readonly string m_branch;

		string m_lastConfigurationCommitId;

		static readonly ILog Log = LogManager.GetLogger("Overseer");
	}
}
