using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Octokit;
using Octokit.Internal;

namespace Leeroy
{
	/// <summary>
	/// Implementation of <see cref="IHttpClient"/> that supports passing a <see cref="CancellationToken"/> to <c>HttpClient.Send</c>.
	/// </summary>
	public sealed class GitHubHttpClient : IHttpClient
	{
		public GitHubHttpClient(CancellationToken token)
		{
			m_token = token;
			m_httpClientAdapter = new OurHttpClientAdapter();
		}

		public async Task<IResponse<T>> Send<T>(IRequest request)
		{
			var httpOptions = new HttpClientHandler
			{
				AllowAutoRedirect = request.AllowAutoRedirect
			};
			if (httpOptions.SupportsAutomaticDecompression)
				httpOptions.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

			var http = new HttpClient(httpOptions)
			{
				BaseAddress = request.BaseAddress,
				Timeout = request.Timeout
			};

			using (var requestMessage = m_httpClientAdapter.BuildRequestMessage(request))
			{
				var responseMessage = await http.SendAsync(requestMessage, HttpCompletionOption.ResponseContentRead, m_token).ConfigureAwait(false);
				return await m_httpClientAdapter.BuildResponse<T>(responseMessage).ConfigureAwait(false);
			}
		}

		// We can't derive from HttpClientAdapter because we need to override Send (which is non-virtual), but we can access
		// the rest of its functionality by using a helper derived class.
		sealed class OurHttpClientAdapter : HttpClientAdapter
		{
			public new HttpRequestMessage BuildRequestMessage(IRequest request)
			{
				return base.BuildRequestMessage(request);
			}

			public new Task<IResponse<T>> BuildResponse<T>(HttpResponseMessage responseMessage)
			{
				return base.BuildResponse<T>(responseMessage);
			}
		}

		readonly CancellationToken m_token;
		readonly OurHttpClientAdapter m_httpClientAdapter;
	}
}
