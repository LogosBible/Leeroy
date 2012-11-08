namespace Leeroy.Json
{
	public sealed class GitCommit
	{
		public string Sha { get; set; }

		public GitCommitTree Tree { get; set; }
	}
}
