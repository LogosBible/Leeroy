using System;
using System.Collections.Generic;

namespace Leeroy.Json
{
	public sealed class BuildProject
	{
		public Uri BuildUrl { get; set; }

		public Uri[] BuildUrls { get; set; }

		public string RepoUrl { get; set; }

		public string Branch { get; set; }

		public Dictionary<string, string> SubmoduleBranches { get; set; }
	}
}
