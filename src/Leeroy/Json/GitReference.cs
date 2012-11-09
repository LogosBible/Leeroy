using System;

namespace Leeroy.Json
{
	public sealed class GitReference
	{
		public string Ref { get; set; }
		public Uri Url { get; set; }
		public GitObject Object { get; set; }
	}
}
