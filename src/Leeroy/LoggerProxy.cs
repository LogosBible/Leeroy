using Common.Logging;

namespace Leeroy
{
	/// <summary>
	/// <see cref="LoggerProxy"/> is a proxy that forwards log messages sent to a <see cref="Logos.Utility.Logging.Logger"/> instance
	/// to a <see cref="Common.Logging.ILog"/> logger with the same name.
	/// </summary>
	internal sealed class LoggerProxy : Logos.Utility.Logging.LoggerImpl
	{
		/// <summary>
		/// Creates a <see cref="LoggerProxy"/> for the specified <see cref="Logos.Utility.Logging.Logger"/>.
		/// </summary>
		/// <param name="logger">The logger.</param>
		public static void Create(Logos.Utility.Logging.Logger logger)
		{
			new LoggerProxy(logger).Attach();
		}

		private LoggerProxy(Logos.Utility.Logging.Logger logger)
			: base(logger)
		{
			// create a new Common.Logging logger
			m_logger = LogManager.GetLogger(logger.Name);
		}

		private void Attach()
		{
			// associate this proxy with the underlying Logos.Utility logger
			ConfigureLogger(m_logger.IsDebugEnabled, m_logger.IsInfoEnabled, m_logger.IsWarnEnabled, m_logger.IsErrorEnabled);
		}

		protected override void DebugCore(string message, object[] args)
		{
			if (args == null)
				m_logger.Debug(message);
			else
				m_logger.DebugFormat(message, args);
		}

		protected override void InfoCore(string message, object[] args)
		{
			if (args == null)
				m_logger.Info(message);
			else
				m_logger.InfoFormat(message, args);
		}

		protected override void WarnCore(string message, object[] args)
		{
			if (args == null)
				m_logger.Warn(message);
			else
				m_logger.WarnFormat(message, args);
		}

		protected override void ErrorCore(string message, object[] args)
		{
			if (args == null)
				m_logger.Error(message);
			else
				m_logger.ErrorFormat(message, args);
		}

		readonly ILog m_logger;
	}
}
