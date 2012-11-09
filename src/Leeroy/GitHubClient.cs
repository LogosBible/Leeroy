using System;
using System.IO;
using System.Net;
using System.Text;
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

		public static GitBlob CreateBlob(string user, string repo, GitBlob blob)
		{
			string json = JsonUtility.ToJson(blob);
			Uri url = new Uri(@"http://git/api/v3/repos/{0}/{1}/git/blobs".FormatInvariant(user, repo));

			var request = PostJson(url, json);
			return Get<GitBlob>(url, request);
		}

		public static GitCommit CreateCommit(string user, string repo, GitCreateCommit commit)
		{
			string json = JsonUtility.ToJson(commit);
			Uri url = new Uri(@"http://git/api/v3/repos/{0}/{1}/git/commits".FormatInvariant(user, repo));

			var request = PostJson(url, json);
			return Get<GitCommit>(url, request);
		}

		public static GitTree CreateTree(string user, string repo, GitCreateTree tree)
		{
			string json = JsonUtility.ToJson(tree);
			Uri url = new Uri(@"http://git/api/v3/repos/{0}/{1}/git/trees".FormatInvariant(user, repo));

			var request = PostJson(url, json);
			return Get<GitTree>(url, request);
		}

		public static GitReference UpdateReference(string user, string repo, string name, GitUpdateReference update)
		{
			string json = JsonUtility.ToJson(update);
			Uri url = new Uri(@"http://git/api/v3/repos/{0}/{1}/git/refs/heads/{2}".FormatInvariant(user, repo, name));

			var request = PostJson(url, json, "PATCH");
			return Get<GitReference>(url, request);
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
			HttpWebRequest request = Program.CreateWebRequest(uri);
			return Get<T>(uri, request);
		}

		private static T Get<T>(Uri uri, HttpWebRequest request)
		{
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
			HttpWebRequest request = Program.CreateWebRequest(uri);
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

		private static HttpWebRequest PostJson(Uri url, string json, string method = "POST")
		{
			HttpWebRequest request = Program.CreateWebRequest(url);
			AddCredentials(request);
			request.Method = method;
			request.ContentType = "application/json; charset=utf-8";
			byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
			request.ContentLength = jsonBytes.Length;
			using (Stream stream = request.GetRequestStream())
				stream.Write(jsonBytes, 0, jsonBytes.Length);
			return request;
		}

		private static void AddCredentials(WebRequest request)
		{
			// send the basic authorization info immediately (request.Credentials will wait to be challenged by the server)
			string authInfo = "user:password";
			authInfo = Convert.ToBase64String(Encoding.Default.GetBytes(authInfo));
			request.Headers["Authorization"] = "Basic " + authInfo;
		}

		static bool m_useGitData = true;

		static readonly ILog Log = LogManager.GetCurrentClassLogger();
	}
}
