using System;
using System.Net;

namespace Leeroy
{
	/// <summary>
	/// Provides extension methods for working with <see cref="Exception"/> objects.
	/// </summary>
	public static class ExceptionExtensions
	{
		/// <summary>
		/// Calls <see cref="IDisposable.Dispose"/> on the <see cref="WebResponse"/> returned by the
		/// <see cref="WebException.Response"/> property, if it is set.
		/// </summary>
		/// <param name="ex">The <see cref="WebException"/> that has been caught.</param>
		/// <remarks>When catching <see cref="WebException"/>, the Response property may be set to a valid
		/// <see cref="WebResponse"/> object. If this response isn't going to be used, it should be disposed to
		/// clean up any unmanaged objects that may be associated with it.</remarks>
		public static void DisposeResponse(this WebException ex)
		{
			if (ex.Response != null)
				((IDisposable) ex.Response).Dispose();
		}
	}
}
