namespace Leeroy.Json
{
	public sealed class GitCommit
	{
		public string Message { get; set; }
		public string Sha { get; set; }
		public GitCommitTree Tree { get; set; }
		public GitCommitPerson Author { get; set; }
		public GitCommitPerson Committer { get; set; }
	}
}
