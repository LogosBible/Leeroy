using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Leeroy.Json;
using Logos.Git.GitHub;
using Logos.Utility;
using Logos.Utility.Logging;

namespace Leeroy
{
	/// <summary>
	/// Watches a single build repository, monitoring its submodules for changes.
	/// </summary>
	public class Watcher
	{
		public Watcher(BuildProject project, BuildServerClient buildServerClient, GitHubClient gitHubClient, CancellationToken token)
		{
			m_project = project;
			m_buildServerClient = buildServerClient;
			m_gitHubClient = gitHubClient;
			SplitRepoUrl(m_project.RepoUrl, out m_server, out m_user, out m_repo);
			m_branch = m_project.Branch ?? "master";
			m_token = token;
			m_submodules = new Dictionary<string, Submodule>();
			m_retryDelay = TimeSpan.FromSeconds(15);
			Log = LogManager.GetLogger("Watcher/{0}".FormatInvariant(m_project.Name));
			Log.Info("Watching '{0}' branch in {1}/{2}.", m_branch, m_user, m_repo);
		}

		public Task CreateTask()
		{
			return Task.Factory.StartNew(Program.FailOnException<object>(Run), m_token, TaskCreationOptions.AttachedToParent | TaskCreationOptions.LongRunning);
		}

		private void Run(object obj)
		{
			Dictionary<string, string> updatedSubmodules = new Dictionary<string, string>();

			while (!m_token.IsCancellationRequested)
			{
				// check for changes to the build repo itself (and reload the submodules if so)
				string commitId = m_gitHubClient.GetLatestCommitId(m_user, m_repo, m_branch, m_updatingSubmodulesFailed);
				if (commitId == null)
				{
					Log.Info("Getting last commit ID failed; assuming branch doesn't exist.");
					commitId = m_gitHubClient.GetLatestCommitId(m_user, m_repo, "master");
					if (commitId == null)
					{
						Log.Error("Getting commit ID for 'master' failed; will stop monitoring project.");
						break;
					}

					GitReference reference = m_gitHubClient.CreateReference(m_user, m_repo, m_branch, commitId);
					if (reference == null)
					{
						Log.Error("Failed to create new branch '{0}' (based on master = {1}); will stop monitoring project.", m_branch, commitId);
						break;
					}

					Log.Info("Sleeping for five seconds to allow GitHub API time to learn about the new branch.");
					Thread.Sleep(TimeSpan.FromSeconds(5));
				}

				if (commitId != m_lastBuildCommitId)
				{
					if (m_lastBuildCommitId != null)
					{
						Log.Info("Build repo commit ID has changed from {0} to {1}; reloading submodules.", m_lastBuildCommitId, commitId);
						StartBuild();
					}

					try
					{
						GetSubmodules();
					}
					catch (WatcherException ex)
					{
						Log.Error("Getting submodules failed; will stop monitoring project.", ex);
						break;
					}
					updatedSubmodules.Clear();
				}
				else
				{
					// check for changes in the submodules
					bool submoduleChanged = false;
					bool submoduleHasError = false;
					foreach (var pair in m_submodules)
					{
						Submodule submodule = pair.Value;
						commitId = m_gitHubClient.GetLatestCommitId(submodule.User, submodule.Repo, submodule.Branch, m_updatingSubmodulesFailed);
						if (commitId == null)
						{
							Log.Error("Submodule '{0}' doesn't have a latest commit for branch '{1}'; will stop monitoring project.", pair.Key, submodule.Branch);
							submoduleHasError = true;
						}
						else if (commitId != submodule.LatestCommitId && commitId != updatedSubmodules.GetValueOrDefault(pair.Key))
						{
							Log.Info("Submodule '{0}' has changed from {1} to {2}; waiting for more changes.", pair.Key, submodule.LatestCommitId.Substring(0, 8), commitId.Substring(0, 8));
							updatedSubmodules[pair.Key] = commitId;
							submoduleChanged = true;
						}
					}

					// abort if there were errors
					if (submoduleHasError)
						break;

					// pause for five seconds between each check
					m_token.WaitHandle.WaitOne(TimeSpan.FromSeconds(5));

					// if any submodule changed, loop again (to allow changes to multiple submodules to be batched)
					if (submoduleChanged)
						continue;

					// if there were updated submodules, create a new commit
					m_updatingSubmodulesFailed = false;
					if (updatedSubmodules.Count != 0)
					{
						try
						{
							if (UpdateSubmodules(updatedSubmodules))
							{
								m_retryDelay = TimeSpan.FromSeconds(15);
							}
							else
							{
								m_updatingSubmodulesFailed = true;
								m_retryDelay = m_retryDelay + m_retryDelay;
								TimeSpan maximumRetryDelay = TimeSpan.FromMinutes(30);
								if (m_retryDelay > maximumRetryDelay)
									m_retryDelay = maximumRetryDelay;
								Log.Info("Failed to update submodules; will wait {0} before trying again.", m_retryDelay);
							}
							updatedSubmodules.Clear();

							// wait for the build to start, and/or for gitdata to be updated with the new commit data
							m_token.WaitHandle.WaitOne(m_retryDelay);
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
				m_buildServerClient.QueueBuild(uri);
		}

		// Gets the list of all submodules in the build repo.
		private void GetSubmodules()
		{
			m_submodules.Clear();

			Log.Info("Getting latest commit.", m_user, m_repo, m_branch);
			m_lastBuildCommitId = m_gitHubClient.GetLatestCommitId(m_user, m_repo, m_branch);
			if (string.IsNullOrEmpty(m_lastBuildCommitId))
			{
				string message = "No latest commit for {0}/{1}/{2}.".FormatInvariant(m_user, m_repo, m_branch);
				Log.Error(message);
				throw new WatcherException(message);
			}

			m_token.ThrowIfCancellationRequested();

			Log.Info("Latest commit is {0}; getting details.", m_lastBuildCommitId);
			GitCommit gitCommit = m_gitHubClient.GetGitCommit(m_user, m_repo, m_lastBuildCommitId);
			if (gitCommit == null)
			{
				string message = "Getting commit {0} for {1}/{2}/{3} failed.".FormatInvariant(m_lastBuildCommitId, m_user, m_repo, m_branch);
				Log.Error(message);
				throw new WatcherException(message);
			}

			m_token.ThrowIfCancellationRequested();

			Log.Debug("Fetching commit tree ({0}).", gitCommit.Tree.Sha);
			GitTree tree = m_gitHubClient.GetTree(gitCommit);
			if (tree == null)
			{
				string message = "Getting tree {0} for {1}/{2}/{3} failed.".FormatInvariant(gitCommit.Tree.Sha, m_user, m_repo, m_branch);
				Log.Error(message);
				throw new WatcherException(message);
			}

			m_token.ThrowIfCancellationRequested();

			// if the new "submodules" property is specified, Leeroy will sync the submodules in the repo with the configuration file;
			//   otherwise, the contents of the build repo will be used to initialize the submodule list
			if (m_project.Submodules != null)
				SyncSubmodules(gitCommit, tree);
			else
				LoadSubmodules(tree);
		}

		private void LoadSubmodules(GitTree buildRepoTree)
		{
			string gitModulesBlob = ReadGitModulesBlob(buildRepoTree);
			if (gitModulesBlob == null)
			{
				Log.Error("Tree {0} doesn't contain a .gitmodules file.", buildRepoTree.Sha);
				return;
			}

			using (StringReader reader = new StringReader(gitModulesBlob))
			{
				foreach (var submodule in ParseConfigFile(reader).Where(x => x.Key.StartsWith("submodule ", StringComparison.Ordinal)))
				{
					// get submodule details
					string path = submodule.Value["path"];
					string url = submodule.Value["url"];
					string server, user, repo;

					if (!SplitRepoUrl(url, out server, out user, out repo))
					{
						Log.Error("{0} is not a valid Git URL".FormatInvariant(url));
						continue;
					}

					// find submodule in repo, and add it to list of tracked submodules
					GitTreeItem submoduleItem = buildRepoTree.Items.FirstOrDefault(x => x.Type == "commit" && x.Mode == "160000" && x.Path == path);
					if (submoduleItem == null)
					{
						Log.Warn(".gitmodules contains [{0}] but there is no submodule at path '{1}'.", submodule.Key, path);
					}
					else
					{
						// use specified branch; else default to tracking the build repo's branch
						string branch;
						if (m_project.SubmoduleBranches == null || !m_project.SubmoduleBranches.TryGetValue(path, out branch))
							branch = m_branch;

						Log.Info("Adding new submodule: '{0}' = '{1}'.", path, url);
						m_submodules.Add(path, new Submodule { Url = url, User = user, Repo = repo, Branch = branch, LatestCommitId = submoduleItem.Sha });
					}
				}
			}
		}

		// Loads the desired list of submodules from the configuration file.
		private void SyncSubmodules(GitCommit buildRepoCommit, GitTree buildRepoTree)
		{
			ReadSubmodulesFromConfig();

			// find submodules that should be present in the build repo, but aren't
			bool updateSubmodules = false;
			foreach (KeyValuePair<string, Submodule> pair in m_submodules)
			{
				string path = pair.Key;
				Submodule submodule = pair.Value;

				GitTreeItem submoduleItem = buildRepoTree.Items.FirstOrDefault(x => x.Type == "commit" && x.Mode == "160000" && x.Path == path);
				if (submoduleItem != null)
				{
					submodule.LatestCommitId = submoduleItem.Sha;
				}
				else
				{
					submodule.LatestCommitId = m_gitHubClient.GetLatestCommitId(submodule.User, submodule.Repo, submodule.Branch);
					if (submodule.LatestCommitId == null)
						throw new WatcherException("Submodule '{0}' doesn't have a latest commit for branch '{1}'; will stop monitoring project.".FormatInvariant(pair.Key, submodule.Branch));
					Log.Info("Submodule at path '{0}' is missing; it will be added.", pair.Key);
					updateSubmodules = true;
				}
			}

			// find extra submodules that aren't in the configuration file
			List<string> extraSubmodulePaths = buildRepoTree.Items
				.Where(x => x.Type == "commit" && x.Mode == "160000" && !m_submodules.ContainsKey(x.Path))
				.Select(x => x.Path)
				.ToList();
			if (extraSubmodulePaths.Count != 0)
			{
				Log.Info("Extra submodule paths: {0}".FormatInvariant(string.Join(", ", extraSubmodulePaths)));
				updateSubmodules = true;
			}

			// determine if .gitmodules needs to be updated
			bool rewriteGitModules = false;
			string gitModulesBlob = ReadGitModulesBlob(buildRepoTree);
			using (StringReader reader = new StringReader(gitModulesBlob ?? ""))
			{
				var existingGitModules = ParseConfigFile(reader)
					.Where(x => x.Key.StartsWith("submodule ", StringComparison.Ordinal))
					.Select(x => new { Path = x.Value["path"], Url = x.Value["url"] })
					.OrderBy(x => x.Path, StringComparer.Ordinal)
					.ToList();
				var desiredGitModules = m_submodules.Select(x => new { Path = x.Key, x.Value.Url }).OrderBy(x => x.Path, StringComparer.Ordinal).ToList();

				if (!existingGitModules.SequenceEqual(desiredGitModules))
				{
					Log.Info(".gitmodules needs to be updated.");
					rewriteGitModules = true;
				}
			}

			if (updateSubmodules || rewriteGitModules)
				UpdateSubmodules(buildRepoCommit, buildRepoTree, rewriteGitModules);
		}

		private void ReadSubmodulesFromConfig()
		{
			// read submodules configuration
			foreach (KeyValuePair<string, string> pair in m_project.Submodules)
			{
				string url = pair.Key;
				string branch = pair.Value;

				// translate shorthand "User/Repo" into "git@git:User/Repo.git"
				if (Regex.IsMatch(url, @"^[^/]+/[^/]+$"))
					url = @"git@git:" + url + ".git";

				string server, user, repo;
				SplitRepoUrl(url, out server, out user, out repo);

				Submodule submodule = new Submodule { Url = url, User = user, Repo = repo, Branch = branch };

				string path = Path.GetFileNameWithoutExtension(url);
				if (path == null)
					throw new WatcherException("Couldn't determine path for submodule. Url={0}, Branch={1}".FormatInvariant(url, branch));

				Log.Info("Adding new submodule: '{0}' = '{1}'.", path, url);
				m_submodules.Add(path, submodule);
			}
		}

		private string ReadGitModulesBlob(GitTree tree)
		{
			GitTreeItem gitModulesItem = tree.Items.FirstOrDefault(x => x.Type == "blob" && x.Path == ".gitmodules");
			if (gitModulesItem == null)
				return null;

			GitBlob gitModulesBlob = m_gitHubClient.GetBlob(gitModulesItem);
			return gitModulesBlob.GetContent();
		}

		private void UpdateSubmodules(GitCommit buildRepoCommit, GitTree buildRepoTree, bool rewriteGitModules)
		{
			// copy most items from the existing commit tree
			const string gitModulesPath = ".gitmodules";
			List<GitTreeItem> treeItems = buildRepoTree.Items
				.Where(x => (x.Type != "commit" && x.Type != "blob") || (x.Type == "blob" && (!rewriteGitModules || x.Path != gitModulesPath))).ToList();

			// add all the submodules
			treeItems.AddRange(m_submodules.Select(x => new GitTreeItem
			{
				Mode = "160000",
				Path = x.Key,
				Sha = x.Value.LatestCommitId,
				Type = "commit",
			}));

			if (rewriteGitModules)
			{
				// create the contents of the .gitmodules file
				string gitModules;
				using (StringWriter sw = new StringWriter { NewLine = "\n" })
				{
					foreach (var pair in m_submodules.OrderBy(x => x.Key, StringComparer.Ordinal))
					{
						sw.WriteLine("[submodule \"{0}\"]", pair.Key);
						sw.WriteLine("\tpath = {0}", pair.Key);
						sw.WriteLine("\turl = {0}", pair.Value.Url);
					}
					gitModules = sw.ToString();
				}

				// create the blob
				GitBlob gitModulesBlob = CreateBlob(gitModules);
				treeItems.Add(new GitTreeItem
				{
					Mode = "100644",
					Path = gitModulesPath,
					Sha = gitModulesBlob.Sha,
					Type = "blob",
				});
			}

			const string commitMessage = "Update submodules to match Leeroy configuration.";
			GitCommit newCommit = CreateNewCommit(buildRepoCommit, null, treeItems, commitMessage);
			Log.Info("Created new commit for synced submodules: {0}; moving branch.", newCommit.Sha);

			// advance the branch pointer to the new commit
			if (TryAdvanceBranch(newCommit.Sha))
			{
				Log.Info("Build repo updated successfully to commit {0}.", newCommit.Sha);
				m_lastBuildCommitId = newCommit.Sha;
			}
		}

		private bool UpdateSubmodules(IDictionary<string, string> updatedSubmodules)
		{
			Log.Info("Updating the following submodules: {0}.", string.Join(", ", updatedSubmodules.Keys));

			// get previous commit (that we will be updating)
			GitCommit commit = m_gitHubClient.GetGitCommit(m_user, m_repo, m_lastBuildCommitId);
			GitTree oldTree = m_gitHubClient.GetTree(commit);

			// add updated submodules
			List<GitTreeItem> treeItems = new List<GitTreeItem>(updatedSubmodules.Select(x => new GitTreeItem
				{
					Mode = "160000",
					Path = x.Key,
					Sha = x.Value,
					Type = "commit",
				}));

			// update build number
			const string buildNumberPath = "SolutionBuildNumber.txt";
			int buildVersion = 0;
			GitTreeItem buildVersionItem = oldTree.Items.FirstOrDefault(x => x.Type == "blob" && x.Path == buildNumberPath);
			if (buildVersionItem != null)
			{
				GitBlob buildVersionBlob = m_gitHubClient.GetBlob(buildVersionItem);
				buildVersion = int.Parse(buildVersionBlob.GetContent().Trim());
				buildVersion++;
				Log.Debug("Updating build number to {0}.", buildVersion);

				string buildVersionString = "{0}\r\n".FormatInvariant(buildVersion);
				GitBlob newBuildVersionBlob = CreateBlob(buildVersionString);

				treeItems.Add(new GitTreeItem
				{
					Mode = "100644",
					Path = buildNumberPath,
					Sha = newBuildVersionBlob.Sha,
					Type = "blob",
				});
				Log.Debug("New build number blob is {0}", buildVersionBlob.Sha);
			}

			// create commit message
			StringBuilder commitMessage = new StringBuilder();
			commitMessage.AppendLine("Build {0}".FormatInvariant(buildVersion));

			// append details for each repository
			foreach (var pair in updatedSubmodules)
			{
				string submodulePath = pair.Key;
				Submodule submodule = m_submodules[submodulePath];
				CommitComparison comparison = m_gitHubClient.CompareCommits(submodule.User, submodule.Repo, submodule.LatestCommitId, pair.Value);
				if (comparison != null)
				{
					string authors = string.Join(", ", comparison.Commits.Select(x => x.GitCommit.Author.Name + " <" + x.GitCommit.Author.Email + ">").Distinct().OrderBy(x => x, StringComparer.InvariantCultureIgnoreCase));
					if (authors.Length > 0)
					{
						commitMessage.AppendLine();
						commitMessage.AppendLine("Authors: {0}".FormatInvariant(authors));
					}

					foreach (Commit comparisonCommit in comparison.Commits.Reverse().Take(5))
					{
						// read the first line of the commit message
						string message;
						using (StringReader reader = new StringReader(comparisonCommit.GitCommit.Message ?? ""))
							message = reader.ReadLine();

						commitMessage.AppendLine();
						commitMessage.AppendLine("{0}: {1}".FormatInvariant(comparisonCommit.GitCommit.Author.Name, message));
						commitMessage.AppendLine("  {0}/{1}".FormatInvariant(submodulePath, comparisonCommit.Sha));

						Commit fullCommit = m_gitHubClient.GetCommit(submodule.User, submodule.Repo, comparisonCommit.Sha);
						if (fullCommit != null && fullCommit.Files != null)
						{
							foreach (CommitFile file in fullCommit.Files)
								commitMessage.AppendLine("  {0}".FormatInvariant(file.Filename));
						}
					}

					if (comparison.TotalCommits > 5)
					{
						commitMessage.AppendLine();
						commitMessage.AppendLine("... and {0} more commits to {1}.".FormatInvariant(comparison.TotalCommits - 5, submodulePath));
					}
				}
				else
				{
					commitMessage.AppendLine();
					commitMessage.AppendLine("Updated {0} from {1} to {2} (no details available).".FormatInvariant(submodulePath, submodule.LatestCommitId.Substring(0, 8), pair.Value.Substring(0, 8)));
				}
			}

			GitCommit newCommit = CreateNewCommit(commit, oldTree.Sha, treeItems, commitMessage.ToString());
			Log.Info("Created new commit for build {0}: {1}; moving branch.", buildVersion, newCommit.Sha);

			// advance the branch pointer to the new commit
			if (TryAdvanceBranch(newCommit.Sha))
			{
				Log.Info("Build repo updated successfully to commit {0}.", newCommit.Sha);
				m_lastBuildCommitId = newCommit.Sha;
				foreach (var pair in updatedSubmodules)
					m_submodules[pair.Key].LatestCommitId = pair.Value;
				StartBuild();

				return true;
			}
			else
			{
				return false;
			}
		}

		private GitBlob CreateBlob(string content)
		{
			string base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(content));
			GitBlob newBlob = new GitBlob { Content = base64, Encoding = "base64" };
			newBlob = m_gitHubClient.CreateBlob(m_user, m_repo, newBlob);
			if (newBlob == null)
				throw new WatcherException("Couldn't create blob with content '{0}'.".FormatInvariant(content.Substring(Math.Min(100, content.Length))));
			return newBlob;
		}

		private GitCommit CreateNewCommit(GitCommit parentCommit, string baseTreeSha, List<GitTreeItem> treeItems, string commitMessage)
		{
			// create new tree
			GitCreateTree newTree = new GitCreateTree
			{
				BaseTree = baseTreeSha,
				Tree = treeItems.ToArray()
			};
			GitTree tree = m_gitHubClient.CreateTree(m_user, m_repo, newTree);
			if (tree == null)
				throw new WatcherException("Couldn't create new tree.");
			Log.Debug("Created new tree: {0}.", tree.Sha);

			// create a commit
			GitCreateCommit createCommit = new GitCreateCommit
			{
				Message = commitMessage,
				Parents = new[] { parentCommit.Sha },
				Tree = tree.Sha,
			};
			GitCommit newCommit = m_gitHubClient.CreateCommit(m_user, m_repo, createCommit);
			if (newCommit == null)
				throw new WatcherException("Couldn't create new commit.");
			return newCommit;
		}

		private bool TryAdvanceBranch(string newSha)
		{
			// advance the branch pointer to the new commit
			GitReference reference = m_gitHubClient.UpdateReference(m_user, m_repo, m_branch, new GitUpdateReference { Sha = newSha });
			if (reference != null && reference.Object.Sha == newSha)
			{
				Log.Info("Branch '{0}' now references '{1}'.", m_branch, newSha);
				return true;
			}
			else
			{
				Log.Warn("Branch '{0}' could not be updated to reference '{1}'.", m_branch, newSha);
				return false;
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
					Log.Warn("Couldn't parse .gitmodules line: {0}", line);
				}
			}

			if (currentSection != null)
				yield return new KeyValuePair<string, Dictionary<string, string>>(currentSection, values);
		}

		private static bool SplitRepoUrl(string url, out string server, out string user, out string repo)
		{
			Match m = Regex.Match(url, @"^git@(?'server'[^:]+):(?'user'[^/]+)/(?'repo'.*?)\.git$");
			server = m.Groups["server"].Value;
			user = m.Groups["user"].Value;
			repo = m.Groups["repo"].Value;

			return m.Success;
		}

		readonly BuildProject m_project;
		readonly BuildServerClient m_buildServerClient;
		readonly GitHubClient m_gitHubClient;
		readonly string m_server;
		readonly string m_user;
		readonly string m_repo;
		readonly string m_branch;
		readonly CancellationToken m_token;
		readonly Dictionary<string, Submodule> m_submodules;
		readonly Logger Log;

		string m_lastBuildCommitId;
		TimeSpan m_retryDelay;
		bool m_updatingSubmodulesFailed;
	}
}
