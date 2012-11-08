using System;
using Newtonsoft.Json;

namespace Leeroy.Json
{
	public sealed class GitTree
	{
		public Uri Url { get; set; }

		public string Sha { get; set; }

		[JsonProperty("tree")]
		public GitTreeItem[] Items { get; set; }
	}
}
