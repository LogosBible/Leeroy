using System.Globalization;

namespace Leeroy
{
	public static class StringExtensions
	{
		public static string FormatInvariant(this string format, params object[] args)
		{
			return string.Format(CultureInfo.InvariantCulture, format, args);
		}
	}
}
