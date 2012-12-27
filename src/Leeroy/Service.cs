using System;
using System.Net;
using System.Reflection;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using Leeroy.Properties;

namespace Leeroy
{
	public partial class Service : ServiceBase
	{
		public Service()
		{
			InitializeComponent();
			Log.InfoFormat("Initializing service (version {0}).", Assembly.GetExecutingAssembly().GetName().Version);

			ServicePointManager.DefaultConnectionLimit = 10;
			GitHubClient.SetCredentials(Settings.Default.UserName, Settings.Default.Password);
		}

		internal void Start()
		{
			Log.Info("Starting service.");

			m_tokenSource = new CancellationTokenSource();
			BuildServerClient buildServerClient = new BuildServerClient(m_tokenSource.Token);
			Overseer overseer = new Overseer(m_tokenSource.Token, buildServerClient, "BradleyGrainger", "Configuration", "master");
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
