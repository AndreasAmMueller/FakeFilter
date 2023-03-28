using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using AMWD.Net.Api.FakeFilter.Models;
using AMWD.Net.Api.FakeFilter.Models.Internals;
using Newtonsoft.Json;

namespace AMWD.Net.Api.FakeFilter
{
	/// <summary>
	/// Implements the FakeFilter API.
	/// </summary>
	/// <remarks>
	/// Implementation docs: <a href="https://fakefilter.net/static/docs/restful/" />
	/// </remarks>
	/// <seealso cref="IDisposable"/>
	public class FakeFilterApi : IDisposable
	{
		private static readonly DateTime unixEpoch = new(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

		private readonly string url;
		private readonly HttpClient httpClient;

		private bool isDisposed;

		/// <summary>
		/// Initializes a new instance of the <see cref="FakeFilterApi"/> class.
		/// </summary>
		/// <remarks>
		/// Uses the default API endpoint: <a href="https://fakefilter.net/api" />
		/// </remarks>
		public FakeFilterApi()
			: this(@"https://fakefilter.net/api")
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="FakeFilterApi" /> class
		/// using a custom API endpoint.
		/// </summary>
		/// <param name="apiUrl">The base API endpoint.</param>
		/// <exception cref="ArgumentNullException">The <paramref name="apiUrl"/> is <c>null</c> or whitespace only.</exception>
		public FakeFilterApi(string apiUrl)
		{
			if (string.IsNullOrWhiteSpace(apiUrl))
				throw new ArgumentNullException(nameof(apiUrl));

			url = apiUrl.Trim().TrimEnd('/').Trim();
			httpClient = new HttpClient();
		}

		/// <inheritdoc />
		public virtual void Dispose()
		{
			if (isDisposed)
				return;

			isDisposed = true;

			httpClient.Dispose();
		}

		/// <summary>
		/// Validates whether the API endpoint is available.
		/// </summary>
		/// <param name="cancellationToken">A cancellation token used to propagate a notification, that this operation should be cancelled.</param>
		/// <returns><c>true</c> when the API is available, otherwise <c>false</c>.</returns>
		/// <exception cref="ObjectDisposedException">The instance of <see cref="FakeFilterApi" /> was disposed before.</exception>
		public virtual async Task<bool> IsAvailable(CancellationToken cancellationToken = default)
		{
			if (isDisposed)
				throw new ObjectDisposedException(GetType().FullName);

			try
			{
				var httpResponse = await httpClient.GetAsync($"{url}/ping", cancellationToken);
				if (!httpResponse.IsSuccessStatusCode)
					return false;

				string json = await httpResponse.Content.ReadAsStringAsync();
				var result = JsonConvert.DeserializeObject<PingResponse>(json);

				return result.ReturnCode == 200 && result.Message.Equals("Pong", StringComparison.OrdinalIgnoreCase);
			}
			catch
			{
				return false;
			}
		}

		/// <summary>
		/// Requests the <paramref name="domain"/> against the API.
		/// </summary>
		/// <param name="domain">The domain to validate.</param>
		/// <param name="cancellationToken">A cancellation token used to propagate a notification, that this operation should be cancelled.</param>
		/// <returns>A <see cref="FakeFilterResponse"/>.</returns>
		/// <exception cref="ObjectDisposedException">The instance of <see cref="FakeFilterApi" /> was disposed before.</exception>
		/// <exception cref="ArgumentNullException">The <paramref name="domain"/> is <c>null</c> or whitespace only.</exception>
		public virtual async Task<FakeFilterResponse> IsFakeDomain(string domain, CancellationToken cancellationToken = default)
		{
			if (isDisposed)
				throw new ObjectDisposedException(GetType().FullName);

			if (string.IsNullOrWhiteSpace(domain))
				throw new ArgumentNullException(nameof(domain));

			var response = new FakeFilterResponse { Request = domain };
			try
			{
				string encodedDomainName = WebUtility.UrlEncode(domain);
				var httpResponse = await httpClient.GetAsync($"{url}/is/fakedomain/{encodedDomainName}", cancellationToken);
				if (!httpResponse.IsSuccessStatusCode)
				{
					response.ErrorMessage = $"Error {(int)httpResponse.StatusCode}: {httpResponse.ReasonPhrase}";
					return response;
				}

				string json = await httpResponse.Content.ReadAsStringAsync();
				var result = JsonConvert.DeserializeObject<FfResponse>(json);
				if (result.ReturnCode != 200)
				{
					response.ErrorMessage = $"Error from FakeFilter with code: {result.ReturnCode}";
					return response;
				}

				response.IsSuccess = true;
				response.IsFakeDomain = !bool.TryParse(result.FakeDomain, out bool isFake) || isFake;

				if (!response.IsFakeDomain)
					return response;

				response.Details = new FakeFilterDetails
				{
					Providers = result.Details.Providers,
					Hosts = new Dictionary<string, FakeFilterHostDetails>()
				};

				foreach (var kvp in result.Details.Hosts)
				{
					response.Details.Hosts.Add(kvp.Key, new FakeFilterHostDetails
					{
						Host = kvp.Key,
						FirstSeen = unixEpoch.AddSeconds(kvp.Value.FirstSeen),
						LastSeen = unixEpoch.AddSeconds(kvp.Value.LastSeen)
					});
				}
			}
			catch (Exception ex)
			{
				response.ErrorMessage = ex.InnerException?.Message ?? ex.Message;
			}

			return response;
		}

		/// <summary>
		/// Requests the <paramref name="email"/> against the API.
		/// </summary>
		/// <param name="email">The email address to validate.</param>
		/// <param name="cancellationToken">A cancellation token used to propagate a notification, that this operation should be cancelled.</param>
		/// <returns>A <see cref="FakeFilterResponse"/>.</returns>
		/// <exception cref="ObjectDisposedException">The instance of <see cref="FakeFilterApi" /> was disposed before.</exception>
		/// <exception cref="ArgumentNullException">The <paramref name="email"/> is <c>null</c> or whitespace only.</exception>
		/// <exception cref="ArgumentException">The <paramref name="email"/> does not have the correct syntax.</exception>
		public virtual async Task<FakeFilterResponse> IsFakeEmail(string email, CancellationToken cancellationToken = default)
		{
			if (isDisposed)
				throw new ObjectDisposedException(GetType().FullName);

			if (string.IsNullOrWhiteSpace(email))
				throw new ArgumentNullException(nameof(email));

			try
			{
				var mail = new MailAddress(email);
				if (mail.Address != email)
					throw new ArgumentException("e-mail address not valid", nameof(email));
			}
			catch (FormatException)
			{
				throw new ArgumentException("e-mail address not valid", nameof(email));
			}

			var response = new FakeFilterResponse { Request = email };
			try
			{
				string encodedEmailAddress = WebUtility.UrlEncode(email);
				var httpResponse = await httpClient.GetAsync($"{url}/is/fakeemail/{encodedEmailAddress}", cancellationToken);
				if (!httpResponse.IsSuccessStatusCode)
				{
					response.ErrorMessage = $"Error {(int)httpResponse.StatusCode}: {httpResponse.ReasonPhrase}";
					return response;
				}

				string json = await httpResponse.Content.ReadAsStringAsync();
				var result = JsonConvert.DeserializeObject<FfResponse>(json);
				if (result.ReturnCode != 200)
				{
					response.ErrorMessage = $"Error from FakeFilter with code: {result.ReturnCode}";
					return response;
				}

				response.IsSuccess = true;
				response.IsFakeDomain = !bool.TryParse(result.FakeDomain, out bool isFake) || isFake;

				if (!response.IsFakeDomain)
					return response;

				response.Details = new FakeFilterDetails
				{
					Providers = result.Details.Providers,
					Hosts = new Dictionary<string, FakeFilterHostDetails>()
				};

				foreach (var kvp in result.Details.Hosts)
				{
					response.Details.Hosts.Add(kvp.Key, new FakeFilterHostDetails
					{
						Host = kvp.Key,
						FirstSeen = unixEpoch.AddSeconds(kvp.Value.FirstSeen),
						LastSeen = unixEpoch.AddSeconds(kvp.Value.LastSeen)
					});
				}
			}
			catch (Exception ex)
			{
				response.ErrorMessage = ex.InnerException?.Message ?? ex.Message;
			}

			return response;
		}
	}
}
