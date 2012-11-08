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

			LoadConfiguration();
		}

		private List<Configuration> LoadConfiguration()
		{
			Log.InfoFormat("Getting latest commit for {0}/{1}/{2}.", m_user, m_repo, m_branch);
			GitHubCommit commit = GitHubClient.GetLatestCommit(m_user, m_repo, m_branch);

			m_token.ThrowIfCancellationRequested();

			Log.InfoFormat("Latest commit is {0}; getting tree.", commit.Sha);
			GitCommit gitCommit = GitHubClient.GetCommit(m_user, m_repo, commit.Sha);

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



		static readonly ILog Log = LogManager.GetLogger("Overseer");
	}
}
