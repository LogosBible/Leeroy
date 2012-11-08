using System;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
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

			m_tokenSource = new CancellationTokenSource();
			Overseer overseer = new Overseer(m_tokenSource.Token, "BradleyGrainger", "Configuration", "master");
			m_task = Task.Factory.StartNew(overseer.Run, m_tokenSource, TaskCreationOptions.LongRunning);
		}

		protected override void OnStop()
		{
			Log.Info("Stopping service.");

			// cancel and wait for all work
			m_tokenSource.Cancel();
			try
			{
				m_task.Wait();
			}
			catch (AggregateException)
			{
				// TODO: verify this contains a single OperationCanceledException
			}

			// shut down
			m_task.Dispose();
			m_tokenSource.Dispose();
		}

		CancellationTokenSource m_tokenSource;
		Task m_task;

		static readonly ILog Log = LogManager.GetCurrentClassLogger();
	}
}
