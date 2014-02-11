using System;
using System.Threading.Tasks;
using Octokit;

namespace Leeroy
{
	public interface IGitHubClientWrapper
	{
		Task<BlobReference> CreateBlob(string owner, string name, string content);
		Task<Reference> CreateBranch(string owner, string name, string branch, string commitId);
		Task<Commit> CreateCommit(string owner, string name, NewCommit newCommit);
		Task<TreeResponse> CreateTree(string owner, string name, NewTree newTree);
		Task<CommitComparison> CompareCommits(string owner, string name, string firstSha, string secondSha);
		Task<Blob> GetBlob(string owner, string name, string reference);
		Task<string> GetCommitId(string owner, string name, string branch, bool refreshCache = false);
		Task<Commit> GetCommit(string owner, string name, string reference);
		Task<FullCommit> GetFullCommit(string owner, string name, string reference);
		Task<TreeResponse> GetTree(string owner, string name, string reference);
		Task<Reference> UpdateBranch(string owner, string name, string branch, ReferenceUpdate referenceUpdate);
	}
}
