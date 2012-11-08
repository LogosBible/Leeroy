using System;

namespace Leeroy.Json
{
	public sealed class GitTreeItem
	{
		public string Type { get; set; }

		public Uri Url { get; set; }

		public int Size { get; set; }

		public string Sha { get; set; }

		public string Path { get; set; }

		public string Mode { get; set; }
	}
}
