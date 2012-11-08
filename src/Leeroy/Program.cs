using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;

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
				Overseer overseer = new Overseer(new CancellationTokenSource().Token, "BradleyGrainger", "Configuration", "master");
				overseer.Run(null);
			}
			else
			{
				ServiceBase.Run(new ServiceBase[] { new Service() });
			}
		}
	}
}
