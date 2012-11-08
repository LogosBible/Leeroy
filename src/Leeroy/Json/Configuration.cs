using System;
using Newtonsoft.Json;

namespace Leeroy.Json
{
	public sealed class Configuration
	{
		[JsonProperty("build-url")]
		public Uri BuildUrl { get; set; }
	}
}
