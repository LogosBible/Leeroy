using System.ServiceProcess;
using Common.Logging;

namespace Leeroy
{
	public partial class Service : ServiceBase
	{
		public Service()
		{
			InitializeComponent();
			Log.Info("Initializing service.");
		}

		protected override void OnStart(string[] args)
		{
			Log.Info("Starting service.");
		}

		protected override void OnStop()
		{
			Log.Info("Stopping service.");
		}

		static readonly ILog Log = LogManager.GetCurrentClassLogger();
	}
}
