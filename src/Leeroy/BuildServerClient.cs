using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Leeroy.Properties;
using Logos.Utility.Logging;
using Logos.Utility.Net;
using Newtonsoft.Json;

namespace Leeroy
{
	public sealed class BuildServerClient
	{
		public BuildServerClient(CancellationToken token)
		{
			m_lock = new object();
			m_uris = new List<UriTime>();
			m_hostCrumbs = new Dictionary<string, CrumbResponse>();
			token.Register(Stop);
			Task.Factory.StartNew(Program.FailOnException<object>(Run), token, TaskCreationOptions.LongRunning);
		}

		public void QueueBuild(Uri uri)
		{
			QueueBuild(uri, TimeSpan.Zero);
		}

		public void QueueBuild(Uri uri, TimeSpan delay)
		{
			Log.Debug("Queueing build URL with delay of {0}: {1}", delay, uri.AbsoluteUri);
			DateTime time = DateTime.UtcNow + delay;
			lock (m_lock)
			{
				int insertIndex = m_uris.Count;
				for (int index = 0; index < m_uris.Count; index++)
				{
					if (m_uris[index].Uri == uri)
					{
						// the URI is already in the list; if we found an earlier place to insert it, remove this copy
						if (insertIndex != m_uris.Count)
							m_uris.RemoveAt(index);
						else
							insertIndex = -1;

						break;
					}
					else if (insertIndex == m_uris.Count && time < m_uris[index].Time)
					{
						insertIndex = index;
					}
				}

				if (insertIndex != -1)
				{
					Log.Debug("Queueing build URI '{0}' at index {1}.", uri.AbsoluteUri, insertIndex);
					m_uris.Insert(insertIndex, new UriTime(uri, time));
					Monitor.Pulse(m_lock);
				}
			}
		}

		private void Run(object obj)
		{
			Log.Info("Starting BuildServerClient background task.");
			CancellationToken token = (CancellationToken) obj;
			while (!token.IsCancellationRequested)
			{
				Uri uri = null;
				lock (m_lock)
				{
					int timeToWait;
					if (m_uris.Count > 0)
					{
						timeToWait = (int) (m_uris[0].Time - DateTime.UtcNow).TotalMilliseconds;
						if (timeToWait <= 0)
						{
							uri = m_uris[0].Uri;
							m_uris.RemoveAt(0);
						}
					}
					else
					{
						timeToWait = Timeout.Infinite;
					}

					if (uri == null)
					{
						Monitor.Wait(m_lock, timeToWait);
						continue;
					}
				}

				StartBuild(uri);
			}
		}

		private void Stop()
		{
			// wake up the thread so it will exit (the cancellation token has already been canceled)
			lock (m_lock)
				Monitor.Pulse(m_lock);
		}

		private void StartBuild(Uri uri)
		{
			var host = uri.Host;
			if (!m_hostCrumbs.TryGetValue(host, out var crumb))
				m_hostCrumbs[host] = crumb = GetHostCrumb(uri);

			// POST to the build URL, which will start a build
			Log.Info("Starting a build via: {0}", uri.AbsoluteUri);
			var request = Program.CreateWebRequest(uri);
			request.Method = "POST";
			request.Timeout = (int) TimeSpan.FromSeconds(5).TotalMilliseconds;
			AddAuthorization(request);

			// add CSRF crumb
			if (crumb != null)
				request.Headers.Add(crumb.CrumbRequestField, crumb.Crumb);

			bool failed = true;
			HttpStatusCode? statusCode = null;
			WebException exception = null;

			try
			{
				using (HttpWebResponse response = request.GetHttpResponse())
				{
					statusCode = response.StatusCode;
					if (statusCode == HttpStatusCode.OK || statusCode == HttpStatusCode.Created)
					{
						failed = false;
					}
					else if (statusCode == HttpStatusCode.NotFound)
					{
						Log.Warn("Jenkins build doesn't exist at {0}", uri.AbsoluteUri);
						failed = false;
					}
					else if (statusCode == HttpStatusCode.Forbidden)
					{
						Log.Info("Got HTTP response status {0} for build at {1}; assuming CSRF token failure", statusCode, uri.AbsoluteUri);
						m_hostCrumbs[host] = GetHostCrumb(uri);
					}
					else if (statusCode == HttpStatusCode.InternalServerError || statusCode == HttpStatusCode.Conflict)
					{
						using (Stream stream = response.GetResponseStream())
						using (StreamReader reader = new StreamReader(stream, Encoding.ASCII))
						{
							string line = reader.ReadLine();
							if (!string.IsNullOrWhiteSpace(line) && Regex.IsMatch(line.Trim(), @"^java.io.IOException: .*? is not buildable$"))
							{
								Log.Warn("Project is disabled; not starting build.");
								failed = false;
							}
						}
					}
				}
			}
			catch (WebException ex)
			{
				ex.DisposeResponse();
				exception = ex;
			}

			if (failed)
			{
				if (exception != null)
					Log.Warn("Couldn't start build at {0}: {1}", exception, uri.AbsoluteUri, statusCode);
				else
					Log.Warn("Couldn't start build at {0}: {1}", uri.AbsoluteUri, statusCode);

				// try again after a delay
				QueueBuild(uri, TimeSpan.FromSeconds(30));
			}
		}

		private CrumbResponse GetHostCrumb(Uri uri)
		{
			var crumbUri = new Uri(uri, "/crumbIssuer/api/json");

			// POST to the build URL, which will start a build
			Log.Info("Requesting crumb from {0}", crumbUri.AbsoluteUri);
			var request = Program.CreateWebRequest(crumbUri);
			request.Method = "GET";
			request.Timeout = (int) TimeSpan.FromSeconds(5).TotalMilliseconds;
			AddAuthorization(request);

			try
			{
				using (HttpWebResponse response = request.GetHttpResponse())
				{
					var statusCode = response.StatusCode;
					Log.Info("Got HTTP response status {0} from {1}", statusCode, crumbUri.AbsoluteUri);
					if (statusCode == HttpStatusCode.OK)
					{
						using (var stream = response.GetResponseStream())
						using (var reader = new StreamReader(stream))
							return JsonConvert.DeserializeObject<CrumbResponse>(reader.ReadToEnd());
					}
				}
			}
			catch (WebException ex)
			{
				if (ex.Status == WebExceptionStatus.ProtocolError)
				{
					var webResponse = (HttpWebResponse) ex.Response;
					Log.Info("Got HTTP response status {0} from {1}", webResponse.StatusCode, crumbUri.AbsoluteUri);
				}
				else
				{
					Log.Error("Got HTTP error {0} from {1}", ex.Message, crumbUri.AbsoluteUri);
				}
				ex.DisposeResponse();
			}

			return null;
		}

		private void AddAuthorization(HttpWebRequest request)
		{
			request.Headers["Authorization"] = "Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes(Settings.Default.BuildServerUserName + ":" + Settings.Default.BuildServerPassword));
		}

		private struct UriTime
		{
			public UriTime(Uri uri, DateTime time)
			{
				Uri = uri;
				Time = time;
			}

			public Uri Uri { get; }
			public DateTime Time { get; }
		}

		private sealed class CrumbResponse
		{
			public string Crumb { get; set; }
			public string CrumbRequestField { get; set; }
		}

		readonly object m_lock;
		readonly List<UriTime> m_uris;
		readonly Dictionary<string, CrumbResponse> m_hostCrumbs;

		static readonly Logger Log = LogManager.GetLogger("BuildServerClient");
	}
}
