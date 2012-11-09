using System;

namespace Leeroy.Json
{
	public sealed class GitObject
	{
		public string Type { get; set; }
		public string Sha { get; set; }
		public Uri Url { get; set; }
	}
}
