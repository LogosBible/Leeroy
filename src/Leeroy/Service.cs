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
		}

		internal void Start()
		{
			Log.Info("Starting service.");

			m_tokenSource = new CancellationTokenSource();

			var connection = new Connection(
				new ProductHeaderValue("Leeroy", Program.GetUserAgentVersion()),
				new Uri("http://git/api/v3/"),
				new InMemoryCredentialStore(new Credentials(Settings.Default.UserName, Settings.Default.Password)),
				new GitHubHttpClient(m_tokenSource.Token),
				new SimpleJsonSerializer());
			var gitHubClient = new GitHubClient(connection);
			m_gitHubClient = new GitHubClientWrapper(gitHubClient);

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
			m_gitHubClient = null;
		}

		protected override void OnStart(string[] args)
		{
			Start();
		}

		protected override void OnStop()
		{
			Stop();
		}

		GitHubClientWrapper m_gitHubClient;
		CancellationTokenSource m_tokenSource;
		Task m_task;

		static readonly Logger Log = LogManager.GetLogger("Service");
	}
}
