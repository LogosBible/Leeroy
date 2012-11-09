using Newtonsoft.Json;

namespace Leeroy.Json
{
	public sealed class GitCreateTree
	{
		[JsonProperty("base_tree")]
		public string BaseTree { get; set; }

		public GitTreeItem[] Tree { get; set; }
	}
}
