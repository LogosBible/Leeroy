using Newtonsoft.Json;

namespace Leeroy.Json
{
	public sealed class Commit
	{
		public string Sha { get; set; }

		[JsonProperty("commit")]
		public GitCommit GitCommit { get; set; }

		public CommitFile[] Files { get; set; }
	}
}
