using Common.Logging;

namespace Leeroy
{
	/// <summary>
	/// <see cref="LoggerProxy"/> is a proxy that forwards log messages sent to a <see cref="Logos.Utility.Logging.Logger"/> instance
	/// to a <see cref="Common.Logging.ILog"/> logger with the same name.
	/// </summary>
	internal sealed class LoggerProxy : Logos.Utility.Logging.LoggerCore
	{
		public LoggerProxy(string name)
		{
			// create a new Common.Logging logger
			m_logger = LogManager.GetLogger(name);
		}

		protected override bool IsDebugEnabledCore
		{
			get { return m_logger.IsDebugEnabled; }
		}

		protected override bool IsInfoEnabledCore
		{
			get { return m_logger.IsInfoEnabled; }
		}

		protected override bool IsWarnEnabledCore
		{
			get { return m_logger.IsWarnEnabled; }
		}

		protected override bool IsErrorEnabledCore
		{
			get { return m_logger.IsErrorEnabled; }
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
