using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Leeroy.Json
{
	public sealed class CommitComparison
	{
		[JsonProperty("total_commits")]
		public int TotalCommits { get; set; }

		public Commit[] Commits { get; set; }
	}
}
