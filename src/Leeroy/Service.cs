using System;
using System.Net;
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

			ServicePointManager.DefaultConnectionLimit = 10;
		}

		internal void Start()
		{
			Log.Info("Starting service.");

			m_tokenSource = new CancellationTokenSource();
			Overseer overseer = new Overseer(m_tokenSource.Token, "BradleyGrainger", "Configuration", "master");
			m_task = Task.Factory.StartNew(Program.FailOnException<object>(overseer.Run), m_tokenSource, TaskCreationOptions.LongRunning);
		}

		internal new void Stop()
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

		protected override void OnStart(string[] args)
		{
			Start();
		}

		protected override void OnStop()
		{
			Stop();
		}

		CancellationTokenSource m_tokenSource;
		Task m_task;

		static readonly ILog Log = LogManager.GetCurrentClassLogger();
	}
}
