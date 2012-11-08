using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceProcess;

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

		[DllImport("user32.dll", CharSet = CharSet.Auto)]
		public static extern int MessageBox(IntPtr hWnd, String text, String caption, int options);
	}
}
