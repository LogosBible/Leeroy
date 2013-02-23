using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using Logos.Utility.Net;

namespace Leeroy
{
	public sealed class BuildServerClient
	{
		public BuildServerClient(CancellationToken token)
		{
			m_lock = new object();
			m_uris = new List<UriTime>();
			token.Register(Stop);
			Task.Factory.StartNew(Program.FailOnException<object>(Run), token, TaskCreationOptions.LongRunning);
		}

		public void QueueBuild(Uri uri)
		{
			QueueBuild(uri, TimeSpan.Zero);
		}

		public void QueueBuild(Uri uri, TimeSpan delay)
		{
			Log.DebugFormat("Queueing build URL with delay of {0}: {1}", delay, uri.AbsoluteUri);
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
					Log.DebugFormat("Queueing build URI '{0}' at index {1}.", uri.AbsoluteUri, insertIndex);
					m_uris.Insert(insertIndex, new UriTime(uri, time));
					Monitor.Pulse(m_lock);
				}
			}
		}

		private void Run(object obj)
		{
			Log.InfoFormat("Starting BuildServerClient background task.");
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
			// GET the build URL, which will start a build
			Log.InfoFormat("Starting a build via: {0}", uri.AbsoluteUri);
			HttpWebRequest request = Program.CreateWebRequest(uri);
			request.Timeout = (int) TimeSpan.FromSeconds(5).TotalMilliseconds;
			bool failed = true;
			HttpStatusCode? statusCode = null;
			WebException exception = null;

			try
			{
				using (HttpWebResponse response = request.GetHttpResponse())
				{
					statusCode = response.StatusCode;
					if (statusCode == HttpStatusCode.OK)
					{
						failed = false;
					}
					else if (statusCode == HttpStatusCode.InternalServerError)
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
					Log.WarnFormat("Couldn't start build at {0}: {1}", exception, uri.AbsoluteUri, statusCode);
				else
					Log.WarnFormat("Couldn't start build at {0}: {1}", uri.AbsoluteUri, statusCode);

				// try again after a delay
				QueueBuild(uri, TimeSpan.FromSeconds(30));
			}
		}

		private struct UriTime
		{
			public UriTime(Uri uri, DateTime time)
			{
				m_uri = uri;
				m_time = time;
			}

			public Uri Uri
			{
				get { return m_uri; }
			}

			public DateTime Time
			{
				get { return m_time; }
			}

			readonly Uri m_uri;
			readonly DateTime m_time;
		}

		readonly object m_lock;
		readonly List<UriTime> m_uris;

		static readonly ILog Log = LogManager.GetLogger("BuildServerClient");
	}
}
