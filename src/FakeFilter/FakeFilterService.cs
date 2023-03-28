using System;
using System.Collections.Generic;
using System.Linq;
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
	/// A small service to use "offline" data from github repository provided every night.
	/// </summary>
	public class FakeFilterService : IDisposable
	{
		private static readonly DateTime unixEpoch = new(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

		private readonly FakeFilterApi api;
		private readonly HttpClient httpClient;

		private readonly ReaderWriterLockSlim rwLock = new();
		private readonly Dictionary<string, Details> domains = new();

		private bool isDisposed;

		/// <summary>
		/// Initializes a new instance of the <see cref="FakeFilterService"/> class.
		/// </summary>
		public FakeFilterService()
		{
			api = new FakeFilterApi();
			httpClient = new HttpClient();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="FakeFilterService"/> class with custom API url.
		/// </summary>
		/// <param name="apiUrl">The custom API url.</param>
		public FakeFilterService(string apiUrl)
		{
			api = new FakeFilterApi(apiUrl);
			httpClient = new HttpClient();
		}

		/// <summary>
		/// Gets or sets the url to the "offline" data.
		/// </summary>
		public virtual string DataUrl { get; set; } = "https://raw.githubusercontent.com/7c/fakefilter/main/json/data_version2.json";

		/// <summary>
		/// Gets the timestamp of the latest "offline" data (provided by the <see cref="DataUrl"/> file).
		/// </summary>
		public virtual DateTime? LastUpdatedAt { get; private set; }

		/// <inheritdoc />
		public virtual void Dispose()
		{
			if (isDisposed)
				return;

			isDisposed = true;

			api.Dispose();
			httpClient.Dispose();
			rwLock.Dispose();
			domains.Clear();
		}

		/// <summary>
		/// Load the information from <see cref="DataUrl"/> and update the internal dictionary.
		/// </summary>
		/// <param name="cancellationToken">A cancellation token used to propagate a notification, that this operation should be cancelled.</param>
		/// <returns><c>true</c> on success, otherwise <c>false</c>.</returns>
		/// <exception cref="ObjectDisposedException">The instance of <see cref="FakeFilterService" /> was disposed before.</exception>
		/// <exception cref="ArgumentNullException">The <see cref="DataUrl"/> is not set.</exception>
		public virtual async Task<bool> UpdateData(CancellationToken cancellationToken = default)
		{
			if (isDisposed)
				throw new ObjectDisposedException(GetType().FullName);

			if (string.IsNullOrWhiteSpace(DataUrl))
				throw new ArgumentNullException(nameof(DataUrl));

			try
			{
				var httpResponse = await httpClient.GetAsync(DataUrl, cancellationToken);
				if (!httpResponse.IsSuccessStatusCode)
					return false;

				string json = await httpResponse.Content.ReadAsStringAsync();
				var data = JsonConvert.DeserializeObject<FfData>(json);
				if (data.Version != 2)
					return false;

				rwLock.EnterWriteLock();
				try
				{
					domains.Clear();
					foreach (var kvp in data.Domains)
						domains.Add(kvp.Key, kvp.Value);

					LastUpdatedAt = unixEpoch.AddSeconds(data.TimestampUnix);
					return true;
				}
				finally
				{
					rwLock.ExitWriteLock();
				}
			}
			catch
			{
				return false;
			}
		}

		/// <summary>
		/// Requests whether a domain is used for fake actions.
		/// </summary>
		/// <param name="domain">The domain to request.</param>
		/// <param name="useOnlyOfflineData">A value to indicate whether to use the API.</param>
		/// <param name="cancellationToken">A cancellation token used to propagate a notification, that this operation should be cancelled.</param>
		/// <returns>A <see cref="FakeFilterResponse"/>.</returns>
		/// <exception cref="ObjectDisposedException">The instance of <see cref="FakeFilterService" /> was disposed before.</exception>
		/// <exception cref="ArgumentNullException">The <paramref name="domain"/> is <c>null</c> or whitespace only.</exception>
		public virtual async Task<FakeFilterResponse> IsFakeDomain(string domain, bool useOnlyOfflineData = false, CancellationToken cancellationToken = default)
		{
			if (isDisposed)
				throw new ObjectDisposedException(GetType().FullName);

			if (string.IsNullOrWhiteSpace(domain))
				throw new ArgumentNullException(nameof(domain));

			if (!useOnlyOfflineData && await api.IsAvailable(cancellationToken))
			{
				var apiReponse = await api.IsFakeDomain(domain, cancellationToken);
				if (apiReponse.IsSuccess)
					return apiReponse;
			}

			var response = new FakeFilterResponse
			{
				Request = domain
			};

			rwLock.EnterReadLock();
			try
			{
				var entries = domains
					.Where(d => domain.EndsWith(d.Key, StringComparison.OrdinalIgnoreCase))
					.OrderByDescending(d => d.Key.Length) // length descending => best match
					.ThenBy(d => d.Key) // alphabetically ascending
					.ToList();

				if (entries.Any())
				{
					response.IsFakeDomain = true;
					response.Details = new FakeFilterDetails
					{
						Providers = entries.First().Value.Providers.ToArray(),
						Hosts = new Dictionary<string, FakeFilterHostDetails>()
					};

					foreach (var kvp in entries.First().Value.Hosts)
					{
						response.Details.Hosts.Add(kvp.Key, new FakeFilterHostDetails
						{
							Host = kvp.Key,
							FirstSeen = unixEpoch.AddSeconds(kvp.Value.FirstSeen),
							LastSeen = unixEpoch.AddSeconds(kvp.Value.LastSeen)
						});
					}
				}

				response.IsSuccess = true;
			}
			finally
			{
				rwLock.ExitReadLock();
			}

			return response;
		}

		/// <summary>
		/// Requests whether an e-mail address is used for fake actions.
		/// </summary>
		/// <param name="email">The e-mail address to request.</param>
		/// <param name="useOnlyOfflineData">A value to indicate whether to use the API.</param>
		/// <param name="cancellationToken">A cancellation token used to propagate a notification, that this operation should be cancelled.</param>
		/// <returns>A <see cref="FakeFilterResponse"/>.</returns>
		/// <exception cref="ObjectDisposedException">The instance of <see cref="FakeFilterService" /> was disposed before.</exception>
		/// <exception cref="ArgumentNullException">The <paramref name="email"/> is <c>null</c> or whitespace only.</exception>,
		/// <exception cref="ArgumentException">The <paramref name="email"/> does not have the correct syntax.</exception>
		public virtual async Task<FakeFilterResponse> IsFakeEmail(string email, bool useOnlyOfflineData = false, CancellationToken cancellationToken = default)
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

			if (!useOnlyOfflineData && await api.IsAvailable(cancellationToken))
			{
				var apiReponse = await api.IsFakeEmail(email, cancellationToken);
				if (apiReponse.IsSuccess)
					return apiReponse;
			}

			var mailAddress = new MailAddress(email);
			var response = await IsFakeDomain(mailAddress.Host, useOnlyOfflineData: true, cancellationToken);
			response.Request = email;

			return response;
		}

		/// <summary>
		/// Gets all domains currently listed in this instance.
		/// </summary>
		/// <exception cref="ObjectDisposedException">The instance of <see cref="FakeFilterService" /> was disposed before.</exception>
		public virtual List<string> GetAllDomains()
		{
			if (isDisposed)
				throw new ObjectDisposedException(GetType().FullName);

			rwLock.EnterReadLock();
			try
			{
				return domains.Keys.ToList();
			}
			finally
			{
				rwLock.ExitReadLock();
			}
		}

		/// <summary>
		/// Gets all providers for a <paramref name="domain"/>.
		/// </summary>
		/// <param name="domain">The domain to request.</param>
		/// <exception cref="ObjectDisposedException">The instance of <see cref="FakeFilterService" /> was disposed before.</exception>
		public virtual List<string> GetProvidersForDomain(string domain)
		{
			if (isDisposed)
				throw new ObjectDisposedException(GetType().FullName);

			rwLock.EnterReadLock();
			try
			{
				return domains
					.Where(kvp => kvp.Key.Equals(domain, StringComparison.OrdinalIgnoreCase))
					.FirstOrDefault()
					.Value
						?.Providers
						.ToList();
			}
			finally
			{
				rwLock.ExitReadLock();
			}
		}
	}
}
