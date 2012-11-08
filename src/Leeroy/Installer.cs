using System.ComponentModel;
using System.ServiceProcess;

namespace Leeroy
{
	[RunInstaller(true)]
	public partial class Installer : System.Configuration.Install.Installer
	{
		public Installer()
		{
			InitializeComponent();

			// service account information
			ServiceProcessInstaller serviceProcessInstaller = new ServiceProcessInstaller
			{
				Account = ServiceAccount.LocalService,
			};
			Installers.Add(serviceProcessInstaller);

			// service information
			ServiceInstaller serviceInstaller = new ServiceInstaller
			{
				DisplayName = "Leeroy Jenkins Build Service",
				ServiceName = "Leeroy",
				StartType = ServiceStartMode.Automatic,
			};
			Installers.Add(serviceInstaller);
		}
	}
}
