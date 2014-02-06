using System;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading.Tasks;
using Logos.Utility;
using Logos.Utility.Logging;

namespace Leeroy
{
	static class Program
	{
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		static void Main(string[] args)
		{
			AppDomain.CurrentDomain.UnhandledException += (s, e) => Log.Error("Unhandled exception in background work: {0}", e.ExceptionObject);
			TaskScheduler.UnobservedTaskException += (s, e) => Log.Error("Unobserved exception in background work: {0}", e.Exception);

			if (args.FirstOrDefault() == "/test")
			{
				Service service = new Service();
				service.Start();

				MessageBox(IntPtr.Zero, "Leeroy is running. Click OK to stop.", "Leeroy", 0);

				service.Stop();
			}
			else
			{
				ServiceBase.Run(new ServiceBase[] { new Service() });
			}
		}

		public static HttpWebRequest CreateWebRequest(Uri uri)
		{
			HttpWebRequest request = (HttpWebRequest) WebRequest.Create(uri);
			request.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
			request.UserAgent = "Leeroy/" + GetUserAgentVersion();
			return request;
		}

		public static string GetUserAgentVersion()
		{
			var version = Assembly.GetExecutingAssembly().GetName().Version;
			return "{0}.{1}".FormatInvariant(version.Major, version.Minor);
		}

		public static Action<T> FailOnException<T>(this Action<T> action)
		{
			return x =>
			{
				try
				{
					action(x);
				}
				catch (OperationCanceledException)
				{
					// work was canceled; ignore
				}
				catch (Exception ex)
				{
					Log.Error("Unhandled exception in background work: {0}", ex);
					Environment.FailFast("Unhandled exception in background work.", ex);
				}
			};
		}

		public static Task FailOnException(Task task)
		{
			task.ContinueWith(x =>
			{
				var exception = x.Exception;
				if (exception != null && !(exception.InnerException is OperationCanceledException))
				{
					Log.Error("Unhandled exception in background work: {0}", exception);
					Environment.FailFast("Unhandled exception in background work.", exception);
				}
			}, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
			return task;
		}

		static readonly Logger Log = LogManager.GetLogger("Program");

		[DllImport("user32.dll", CharSet = CharSet.Auto)]
		private static extern int MessageBox(IntPtr hWnd, String text, String caption, int options);
	}
}
