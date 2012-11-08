using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;

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
				var tokenSource = new CancellationTokenSource();
				Overseer overseer = new Overseer(tokenSource.Token, "BradleyGrainger", "Configuration", "master");
				var task = Task.Factory.StartNew(overseer.Run, tokenSource, TaskCreationOptions.LongRunning);

				MessageBox(IntPtr.Zero, "Leeroy is running. Click OK to stop.", "Leeroy", 0);

				tokenSource.Cancel();
				try
				{
					task.Wait();
				}
				catch (AggregateException)
				{
					// TODO: verify this contains a single OperationCanceledException
				}

				// shut down
				task.Dispose();
				tokenSource.Dispose();
			}
			else
			{
				ServiceBase.Run(new ServiceBase[] { new Service() });
			}
		}

		[DllImport("user32.dll", CharSet = CharSet.Auto)]
		public static extern int MessageBox(IntPtr hWnd, String text, String caption, int options);
	}
}
