using System;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using Leeroy.Properties;
using Logos.Utility.Logging;
using Octokit;
using Octokit.Internal;

namespace Leeroy
{
	public partial class Service : ServiceBase
	{
		public Service()
		{
			InitializeComponent();
			Logos.Utility.Logging.LogManager.Initialize(x => new LoggerProxy(x));
			Log.Info("Initializing service (version {0}).", Assembly.GetExecutingAssembly().GetName().Version);

			ServicePointManager.DefaultConnectionLimit = 10;
			var gitHubClient = new GitHubClient(new ProductHeaderValue("Leeroy", Program.GetUserAgentVersion()), new InMemoryCredentialStore(new Credentials(Settings.Default.UserName, Settings.Default.Password)), new Uri("http://git/api/v3/"));
			m_gitHubClient = new GitHubClientWrapper(gitHubClient);
		}

		internal void Start()
		{
			Log.Info("Starting service.");

			m_tokenSource = new CancellationTokenSource();
			BuildServerClient buildServerClient = new BuildServerClient(m_tokenSource.Token);
			Overseer overseer = new Overseer(m_tokenSource.Token, buildServerClient, m_gitHubClient, "Build", "Configuration", "master");
			m_task = Program.FailOnException(overseer.Run());
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
			catch (AggregateException ex)
			{
				ex.Handle(e => e is OperationCanceledException);
			}

			Log.Info("Service stopped.");

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

		readonly GitHubClientWrapper m_gitHubClient;
		CancellationTokenSource m_tokenSource;
		Task m_task;

		static readonly Logger Log = LogManager.GetLogger("Service");
	}
}
