using System;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using Common.Logging;

namespace Leeroy
{
	static class Program
	{
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		static void Main(string[] args)
		{
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
			request.UserAgent = "Leeroy/1.0";
			return request;
		}

		public static Action<T> FailOnException<T>(this Action<T> action)
		{
			return x =>
			{
				try
				{
					action(x);
				}
				catch (Exception ex)
				{
					Log.Fatal("Unhandled exception in background work.", ex);
					Environment.FailFast("Unhandled exception in background work.", ex);
				}
			};
		}

		static readonly ILog Log = LogManager.GetCurrentClassLogger();

		[DllImport("user32.dll", CharSet = CharSet.Auto)]
		private static extern int MessageBox(IntPtr hWnd, String text, String caption, int options);
	}
}
