using System;
using System.IO;
using System.Net;
using Common.Logging;
using Leeroy.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Leeroy
{
	public static class GitHubClient
	{
		public static GitHubCommit GetLatestCommit(string user, string repo, string branch)
		{
			return Get<GitHubCommit>(@"http://git/api/v3/repos/{0}/{1}/commits/{2}", user, repo, branch);
		}

		public static GitCommit GetCommit(string user, string repo, string sha)
		{
			return Get<GitCommit>(@"http://git/api/v3/repos/{0}/{1}/git/commits/{2}", user, repo, sha);
		}

		public static T Get<T>(string url)
		{
			return Get<T>(new Uri(url));
		}

		public static T Get<T>(string urlPattern, params object[] args)
		{
			return Get<T>(new Uri(string.Format(urlPattern, args)));
		}

		public static T Get<T>(Uri uri)
		{
			HttpWebRequest request = (HttpWebRequest) WebRequest.Create(uri);
			request.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
			request.UserAgent = "Leeroy/1.0";

			try
			{
				using (HttpWebResponse response = (HttpWebResponse) request.GetResponse())
				using (Stream stream = response.GetResponseStream())
				using (StreamReader reader = new StreamReader(stream, System.Text.Encoding.UTF8))
				{
					return JsonUtility.FromJsonTextReader<T>(reader);
				}
			}
			catch (FormatException ex)
			{
				Log.ErrorFormat("Error requesting {0}.", ex, uri.AbsoluteUri);
			}
			catch (JsonSerializationException ex)
			{
				Log.ErrorFormat("Error requesting {0}.", ex, uri.AbsoluteUri);
			}
			catch (WebException ex)
			{
				Log.ErrorFormat("Error requesting {0}.", ex, uri.AbsoluteUri);
			}

			return default(T);
		}

		static readonly ILog Log = LogManager.GetCurrentClassLogger();
	}
}
