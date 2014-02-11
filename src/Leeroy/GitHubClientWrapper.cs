using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Logos.Utility;
using Octokit;

namespace Leeroy
{
	public sealed class GitHubClientWrapper : IGitHubClientWrapper, IDisposable
	{
		public GitHubClientWrapper(IGitHubClient client)
		{
			m_httpClient = new HttpClient();
			m_client = client;
		}

		public Task<BlobReference> CreateBlob(string owner, string name, string content)
		{
			return m_client.GitDatabase.Blob.Create(owner, name, new NewBlob { Content = Convert.ToBase64String(Encoding.UTF8.GetBytes(content)), Encoding = EncodingType.Base64 });
		}

		public Task<Reference> CreateBranch(string owner, string name, string branch, string commitId)
		{
			return m_client.GitDatabase.Reference.Create(owner, name, new NewReference("refs/heads/" + branch, commitId));
		}

		public Task<Commit> CreateCommit(string owner, string name, NewCommit newCommit)
		{
			return m_client.GitDatabase.Commit.Create(owner, name, newCommit);
		}

		public Task<TreeResponse> CreateTree(string owner, string name, NewTree newTree)
		{
			return m_client.GitDatabase.Tree.Create(owner, name, newTree);
		}

		public Task<CommitComparison> CompareCommits(string owner, string name, string firstSha, string secondSha)
		{
			var apiConnection = new ApiConnection(m_client.Connection);
			var uri = new Uri("repos/{0}/{1}/compare/{2}...{3}".FormatInvariant(owner, name, firstSha, secondSha), UriKind.Relative);
			return apiConnection.Get<CommitComparison>(uri);
		}

		public Task<Blob> GetBlob(string owner, string name, string reference)
		{
			return m_client.GitDatabase.Blob.Get(owner, name, reference);
		}

		public async Task<string> GetCommitId(string owner, string name, string branch, bool refreshCache = false)
		{
			using (var message = await m_httpClient.GetAsync("http://gitdata/commits/latest/git/{0}/{1}/{2}".FormatInvariant(owner, name, branch) + (refreshCache ? "?refreshCache=true" : "")))
			using (message.Content)
			{
				if (message.StatusCode == HttpStatusCode.OK)
					return (await message.Content.ReadAsStringAsync()).Trim();
			}

			return null;
		}

		public Task<Commit> GetCommit(string owner, string name, string reference)
		{
			return m_client.GitDatabase.Commit.Get(owner, name, reference);
		}

		public Task<FullCommit> GetFullCommit(string owner, string name, string reference)
		{
			var apiConnection = new ApiConnection(m_client.Connection);
			var uri = new Uri("repos/{0}/{1}/commits/{2}".FormatInvariant(owner, name, reference), UriKind.Relative);
			return apiConnection.Get<FullCommit>(uri);
		}

		public Task<TreeResponse> GetTree(string owner, string name, string reference)
		{
			return m_client.GitDatabase.Tree.Get(owner, name, reference);
		}

		public Task<Reference> UpdateBranch(string owner, string name, string branch, ReferenceUpdate referenceUpdate)
		{
			return m_client.GitDatabase.Reference.Update(owner, name, "refs/heads/" + branch, referenceUpdate);
		}

		public void Dispose()
		{
			m_httpClient.Dispose();
		}

		readonly HttpClient m_httpClient;
		readonly IGitHubClient m_client;
	}

	public sealed class CommitComparison
	{
		public int TotalCommits { get; set; }

		public FullCommit[] Commits { get; set; }
	}

	public sealed class FullCommit
	{
		public string Sha { get; set; }

		public Commit Commit { get; set; }

		public Uri Url { get; set; }

		public CommitFile[] Files { get; set; }
	}

	public sealed class CommitFile
	{
		public string Filename { get; set; }
	}

	public static class OctoKitExtensions
	{
		public static string GetContent(this Blob blob)
		{
			switch (blob.Encoding)
			{
				case EncodingType.Base64:
					return Encoding.UTF8.GetString(Convert.FromBase64String(blob.Content));
				case EncodingType.Utf8:
					return blob.Content;
				default:
					throw new InvalidOperationException("'encoding' type '{0}' is not supported.".FormatInvariant(blob.Encoding));
			}
		}
	}
}
