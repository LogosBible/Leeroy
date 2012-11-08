using System;
using System.IO;
using System.Net;
using Common.Logging;
using Leeroy.Json;
using Newtonsoft.Json;

namespace Leeroy
{
	public static class GitHubClient
	{
		public static string GetLatestCommitId(string user, string repo, string branch)
		{
			if (m_useGitData)
			{
				Uri url = new Uri(@"http://gitdata.lrscorp.net/commits/latest/git/{0}/{1}/{2}".FormatInvariant(user, repo, branch));
				return GetString(url).Trim();
			}
			else
			{
				return Get<GitHubCommit>(@"http://git/api/v3/repos/{0}/{1}/commits/{2}", user, repo, branch).Sha;
			}
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
			HttpWebRequest request = CreateWebRequest(uri);
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

		private static string GetString(Uri uri)
		{
			HttpWebRequest request = CreateWebRequest(uri);
			try
			{
				using (HttpWebResponse response = (HttpWebResponse) request.GetResponse())
				using (Stream stream = response.GetResponseStream())
				using (StreamReader reader = new StreamReader(stream, System.Text.Encoding.UTF8))
					return reader.ReadToEnd();
			}
			catch (WebException ex)
			{
				Log.ErrorFormat("Error requesting {0}.", ex, uri.AbsoluteUri);
			}

			return null;
		}

		private static HttpWebRequest CreateWebRequest(Uri uri)
		{
			HttpWebRequest request = (HttpWebRequest) WebRequest.Create(uri);
			request.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
			request.UserAgent = "Leeroy/1.0";
			return request;
		}

		static bool m_useGitData = true;

		static readonly ILog Log = LogManager.GetCurrentClassLogger();
	}
}
