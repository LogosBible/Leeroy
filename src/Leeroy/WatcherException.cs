using System;

namespace Leeroy
{
	public sealed class WatcherException : Exception
	{
		public WatcherException(string message)
			: base(message)
		{
		}
	}
}
