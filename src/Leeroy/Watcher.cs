using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using Leeroy.Json;

namespace Leeroy
{
	/// <summary>
	/// Watches a single build repository, monitoring its submodules for changes.
	/// </summary>
	public class Watcher
	{
		public Watcher(BuildProject project, CancellationToken token)
		{
			m_project = project;
			SplitRepoUrl(m_project.RepoUrl, out m_server, out m_user, out m_repo);
			m_branch = m_project.Branch;
			m_token = token;
			m_submodules = new Dictionary<string, Submodule>();
			Log = LogManager.GetLogger("Watcher/{0}/{1}/{2}".FormatInvariant(m_user, m_repo, m_branch));
		}

		public Task CreateTask()
		{
			return Task.Factory.StartNew(Program.FailOnException<object>(Run), m_token, TaskCreationOptions.AttachedToParent | TaskCreationOptions.LongRunning);
		}

		private void Run(object obj)
		{
			HashSet<string> submodulesToUpdate = new HashSet<string>();

			while (!m_token.IsCancellationRequested)
			{
				// check for changes to the build repo itself (and reload the submodules if so)
				string commitId = GitHubClient.GetLatestCommitId(m_user, m_repo, m_branch);
				if (commitId != m_lastBuildCommitId)
				{
					if (m_lastBuildCommitId != null)
					{
						Log.InfoFormat("Build repo commit ID has changed from {0} to {1}; reloading submodules.", m_lastBuildCommitId, commitId);
						StartBuild();
					}

					GetSubmodules();
					submodulesToUpdate.Clear();
				}
				else
				{
					// check for changes in the submodules
					bool submoduleChanged = false;
					foreach (var pair in m_submodules)
					{
						Submodule submodule = pair.Value;
						commitId = GitHubClient.GetLatestCommitId(submodule.User, submodule.Repo, submodule.Branch);
						if (commitId != submodule.LatestCommitId)
						{
							Log.InfoFormat("Submodule '{0}' has changed from {1} to {2}; waiting for more changes.", pair.Key, submodule.LatestCommitId, commitId);
							submodulesToUpdate.Add(pair.Key);
							submodule.LatestCommitId = commitId;
							submoduleChanged = true;
						}
					}

					// pause for five seconds between each check
					m_token.WaitHandle.WaitOne(TimeSpan.FromSeconds(5));

					// if any submodule changed, loop again (to allow changes to multiple submodules to be batched)
					if (submoduleChanged)
						continue;

					// if there were updated submodules, create a new commit
					if (submodulesToUpdate.Count != 0)
					{
						try
						{
							UpdateSubmodules(submodulesToUpdate);
							submodulesToUpdate.Clear();
						}
						catch (WatcherException ex)
						{
							Log.Error("Updating submodules failed; will stop monitoring project.", ex);
							break;
						}
					}
				}
			}
		}

		// Triggers a Jenkins build by accessing the build URL.
		private void StartBuild()
		{
			// concatenate all build URL
			List<Uri> uris = new List<Uri>(m_project.BuildUrls ?? Enumerable.Empty<Uri>());
			if (m_project.BuildUrl != null)
				uris.Add(m_project.BuildUrl);

			foreach (Uri uri in uris)
			{
				// GET the build URL, which will start a build
				Log.InfoFormat("Starting a build via {0}.", uri.AbsoluteUri);
				HttpWebRequest request = Program.CreateWebRequest(uri);
				using (WebResponse response = request.GetResponse())
				using (Stream stream = response.GetResponseStream())
				{
				}
			}
		}

		// Gets the list of all submodules in the build repo.
		private void GetSubmodules()
		{
			m_submodules.Clear();

			Log.InfoFormat("Getting latest commit.", m_user, m_repo, m_branch);
			m_lastBuildCommitId = GitHubClient.GetLatestCommitId(m_user, m_repo, m_branch);

			m_token.ThrowIfCancellationRequested();

			Log.InfoFormat("Latest commit is {0}; getting details.", m_lastBuildCommitId);
			GitCommit gitCommit = GitHubClient.GetCommit(m_user, m_repo, m_lastBuildCommitId);

			m_token.ThrowIfCancellationRequested();

			Log.DebugFormat("Fetching commit tree ({0}).", gitCommit.Tree.Sha);
			GitTree tree = GitHubClient.Get<GitTree>(gitCommit.Tree.Url);

			m_token.ThrowIfCancellationRequested();

			GitTreeItem gitModulesItem = tree.Items.FirstOrDefault(x => x.Type == "blob" && x.Path == ".gitmodules");
			if (gitModulesItem == null)
			{
				Log.ErrorFormat("Tree {0} doesn't contain a .gitmodules file.", gitCommit.Tree.Sha);
				return;
			}

			GitBlob gitModulesBlob = GitHubClient.Get<GitBlob>(gitModulesItem.Url);
			using (StringReader reader = new StringReader(gitModulesBlob.GetContent()))
			{
				foreach (var submodule in ParseConfigFile(reader).Where(x => x.Key.StartsWith("submodule ", StringComparison.Ordinal)))
				{
					// get submodule details
					string path = submodule.Value["path"];
					string url = submodule.Value["url"];
					string server, user, repo;
					SplitRepoUrl(url, out server, out user, out repo);

					// find submodule in repo, and add it to list of tracked submodules
					GitTreeItem submoduleItem = tree.Items.FirstOrDefault(x => x.Type == "commit" && x.Mode == "160000" && x.Path == path);
					if (submoduleItem == null)
					{
						Log.WarnFormat(".gitmodules contains [{0}] but there is no submodule at path '{1}'.", submodule.Key, path);
					}
					else
					{
						// use specified branch; else default to tracking the build repo's branch
						string branch;
						if (!m_project.SubmoduleBranches.TryGetValue(path, out branch))
							branch = m_branch;

						Log.InfoFormat("Adding new submodule: '{0}' = '{1}'.", path, url);
						m_submodules.Add(path, new Submodule { Url = url, User = user, Repo = repo, Branch = branch, LatestCommitId = submoduleItem.Sha });
					}
				}
			}
		}

		private void UpdateSubmodules(ICollection<string> paths)
		{
			Log.InfoFormat("Updating the following submodules: {0}.", string.Join(", ", paths));

			// get previous commit (that we will be updating)
			GitCommit commit = GitHubClient.GetCommit(m_user, m_repo, m_lastBuildCommitId);
			GitTree oldTree = GitHubClient.Get<GitTree>(commit.Tree.Url);

			// add updated submodules
			List<GitTreeItem> treeItems = new List<GitTreeItem>(paths.Select(path => new GitTreeItem
				{
					Mode = "160000",
					Path = path,
					Sha = m_submodules[path].LatestCommitId,
					Type = "commit",
				}));

			// update build number
			string buildNumberPath = "SolutionBuildNumber.txt";
			int buildVersion = 0;
			GitTreeItem buildVersionItem = oldTree.Items.FirstOrDefault(x => x.Type == "blob" && x.Path == buildNumberPath);
			if (buildVersionItem != null)
			{
				GitBlob buildVersionBlob = GitHubClient.Get<GitBlob>(buildVersionItem.Url);
				buildVersion = int.Parse(buildVersionBlob.GetContent().Trim());
				buildVersion++;
				Log.DebugFormat("Updating build number to {0}.", buildVersion);

				string buildVersionString = "{0}\r\n".FormatInvariant(buildVersion);
				string base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(buildVersionString));
				GitBlob newBuildVersionBlob = new GitBlob { Content = base64, Encoding = "base64" };
				newBuildVersionBlob = GitHubClient.CreateBlob(m_user, m_repo, newBuildVersionBlob);
				if (newBuildVersionBlob == null)
					throw new WatcherException("Couldn't create {0} blob.".FormatInvariant(buildNumberPath));

				treeItems.Add(new GitTreeItem
				{
					Mode = "100644",
					Path = buildNumberPath,
					Sha = newBuildVersionBlob.Sha,
					Type = "blob",
				});
				Log.DebugFormat("New build number blob is {0}", buildVersionBlob.Sha);
			}

			// create new tree
			GitCreateTree newTree = new GitCreateTree
			{
				BaseTree = commit.Tree.Sha,
				Tree = treeItems.ToArray()
			};
			GitTree tree = GitHubClient.CreateTree(m_user, m_repo, newTree);
			if (tree == null)
				throw new WatcherException("Couldn't create new tree.");
			Log.DebugFormat("Created new tree: {0}.", tree.Sha);

			// create a commit
			GitCreateCommit createCommit = new GitCreateCommit
			{
				Message = "Build {0}\r\n".FormatInvariant(buildVersion),
				Parents = new[] { m_lastBuildCommitId },
				Tree = tree.Sha,
			};
			GitCommit newCommit = GitHubClient.CreateCommit(m_user, m_repo, createCommit);
			if (newCommit == null)
				throw new WatcherException("Couldn't create new commit.");
			Log.InfoFormat("Created new commit for build {0}: {1}; moving branch.", buildVersion, newCommit.Sha);

			// advance the branch pointer to the new commit
			GitReference reference = GitHubClient.UpdateReference(m_user, m_repo, m_branch, new GitUpdateReference { Sha = newCommit.Sha });
			if (reference != null && reference.Object.Sha == newCommit.Sha)
			{
				Log.InfoFormat("Build repo updated successfully to commit {0}.", newCommit.Sha);
				m_lastBuildCommitId = newCommit.Sha;
				StartBuild();

				// wait for the build to start, and for gitdata to be updated with the new commit data
				m_token.WaitHandle.WaitOne(TimeSpan.FromSeconds(15));
			}
		}

		// Parses a git configuration file into blocks.
		private IEnumerable<KeyValuePair<string, Dictionary<string, string>>> ParseConfigFile(TextReader reader)
		{
			string currentSection = null;
			Dictionary<string, string> values = null;

			string line;
			while ((line = reader.ReadLine()) != null)
			{
				if (string.IsNullOrWhiteSpace(line))
					continue;

				// check for [section heading]
				if (line[0] == '[' && line[line.Length - 1] == ']')
				{
					if (currentSection != null)
						yield return new KeyValuePair<string, Dictionary<string, string>>(currentSection, values);

					currentSection = line.Substring(1, line.Length - 2);
					values = new Dictionary<string, string>();
				}
				else if (currentSection != null && line.IndexOf('=') != -1)
				{
					// parse 'name = value' pair
					int equalsIndex = line.IndexOf('=');
					string key = line.Substring(0, equalsIndex).Trim();
					string value = line.Substring(equalsIndex + 1).Trim();
					values.Add(key, value);
				}
				else
				{
					Log.WarnFormat("Couldn't parse .gitmodules line: {0}", line);
				}
			}

			if (currentSection != null)
				yield return new KeyValuePair<string, Dictionary<string, string>>(currentSection, values);
		}

		private static void SplitRepoUrl(string url, out string server, out string user, out string repo)
		{
			Match m = Regex.Match(url, @"^git@(?'server'[^:]+):(?'user'[^/]+)/(?'repo'.*?)\.git$");
			server = m.Groups["server"].Value;
			user = m.Groups["user"].Value;
			repo = m.Groups["repo"].Value;
		}

		readonly BuildProject m_project;
		readonly string m_server;
		readonly string m_user;
		readonly string m_repo;
		readonly string m_branch;
		readonly CancellationToken m_token;
		readonly Dictionary<string, Submodule> m_submodules;
		readonly ILog Log = LogManager.GetLogger("Watcher");

		string m_lastBuildCommitId;
	}
}
