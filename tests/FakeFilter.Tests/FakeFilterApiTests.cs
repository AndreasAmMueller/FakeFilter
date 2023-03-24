using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using AMWD.Common.Moq;

namespace FakeFilter.Tests
{
	[TestClass]
	[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
	public class FakeFilterApiTests
	{
		private HttpMessageHandlerMoq messageHandler;

		[TestInitialize]
		public void InitializeTest()
		{
			messageHandler = new HttpMessageHandlerMoq();
		}

		[DataTestMethod]
		[DataRow("")]
		[DataRow("\t")]
		[DataRow(null)]
		[ExpectedException(typeof(ArgumentNullException))]
		public void ShouldThrowInstanceOnEmptyUrl(string url)
		{
			// Arrange

			// Act
			using var api = new FakeFilterApi(url);

			// Assert - Exception
			Assert.Fail();
		}

		[TestMethod]
		[ExpectedException(typeof(ObjectDisposedException))]
		public async Task ShouldThrowOnDisposedForIsAvailable()
		{
			// Arrange
			var api = new FakeFilterApi();

			// Act
			api.Dispose();
			await api.IsAvailable();

			// Assert - Exception
			Assert.Fail();
		}

		[TestMethod]
		[ExpectedException(typeof(ObjectDisposedException))]
		public async Task ShouldThrowOnDisposedForFakeDomain()
		{
			// Arrange
			var api = new FakeFilterApi();

			// Act
			api.Dispose();
			await api.IsFakeDomain("foo.bar");

			// Assert - Exception
			Assert.Fail();
		}

		[TestMethod]
		[ExpectedException(typeof(ObjectDisposedException))]
		public async Task ShouldThrowOnDisposedForFakeMail()
		{
			// Arrange
			var api = new FakeFilterApi();

			// Act
			api.Dispose();
			await api.IsFakeEmail("test@foo.bar");

			// Assert - Exception
			Assert.Fail();
		}

		[TestMethod]
		public void ShouldAllowMultipleDispose()
		{
			// Arrange
			var api = new FakeFilterApi();

			// Act
			api.Dispose();
			api.Dispose();

			// Assert - no Exception
		}

		[TestMethod]
		public async Task ShouldReturnAvailable()
		{
			// Arrange
			messageHandler.Response.Content = new StringContent(@"{""retcode"": 200, ""message"": ""pong""}", Encoding.UTF8, "application/json");
			using var api = GetApi();

			// Act
			bool isAvailable = await api.IsAvailable();

			// Assert
			Assert.IsTrue(isAvailable);
			Assert.AreEqual(HttpMethod.Get, messageHandler.Callbacks.First().Method);
			Assert.AreEqual("https://fakefilter.net/api/ping", messageHandler.Callbacks.First().RequestUrl);
		}

		[TestMethod]
		public async Task ShouldReturnNotAvailableOnReturnCode()
		{
			// Arrange
			messageHandler.Response.Content = new StringContent(@"{""retcode"": -50}", Encoding.UTF8, "application/json");
			using var api = GetApi();

			// Act
			bool isAvailable = await api.IsAvailable();

			// Assert
			Assert.IsFalse(isAvailable);
			Assert.AreEqual(HttpMethod.Get, messageHandler.Callbacks.First().Method);
			Assert.AreEqual("https://fakefilter.net/api/ping", messageHandler.Callbacks.First().RequestUrl);
		}

		[TestMethod]
		public async Task ShouldReturnNotAvailableOnHttpError()
		{
			// Arrange
			messageHandler.Response.StatusCode = HttpStatusCode.InternalServerError;
			using var api = GetApi();

			// Act
			bool isAvailable = await api.IsAvailable();

			// Assert
			Assert.IsFalse(isAvailable);
			Assert.AreEqual(HttpMethod.Get, messageHandler.Callbacks.First().Method);
			Assert.AreEqual("https://fakefilter.net/api/ping", messageHandler.Callbacks.First().RequestUrl);
		}

		[TestMethod]
		public async Task ShouldReturnNotAvailableOnException()
		{
			// Arrange
			using var api = GetApi();

			// Act
			bool isAvailable = await api.IsAvailable();

			// Assert
			Assert.IsFalse(isAvailable);
			Assert.AreEqual(HttpMethod.Get, messageHandler.Callbacks.First().Method);
			Assert.AreEqual("https://fakefilter.net/api/ping", messageHandler.Callbacks.First().RequestUrl);
		}

		[TestMethod]
		public async Task ShouldReturnValidDomain()
		{
			// Arrange
			messageHandler.Response.Content = new StringContent(@"{""retcode"": 200, ""isFakeDomain"": false, ""details"": null}", Encoding.UTF8, "application/json");
			using var api = GetApi();

			// Act
			var result = await api.IsFakeDomain("fakefilter.net");

			// Assert
			Assert.IsNotNull(result);
			Assert.AreEqual("fakefilter.net", result.Request);

			Assert.IsTrue(result.IsSuccess);
			Assert.IsFalse(result.IsFakeDomain);
			Assert.IsNull(result.Details);
		}

		[TestMethod]
		public async Task ShouldReturnFakeDomain()
		{
			// Arrange
			messageHandler.Response.Content = new StringContent(@"{""retcode"": 200, ""isFakeDomain"": ""foo.bar"", ""details"": { ""providers"": [ ""foo.bar.provider"" ], ""hosts"": { ""foo.bar"": { ""firstseen"": 123456789, ""lastseen"": 123456789 } } } }", Encoding.UTF8, "application/json");
			using var api = GetApi();

			// Act
			var result = await api.IsFakeDomain("foo.bar");

			// Assert
			Assert.IsNotNull(result);
			Assert.AreEqual("foo.bar", result.Request);

			Assert.IsTrue(result.IsSuccess);
			Assert.IsTrue(result.IsFakeDomain);

			Assert.AreEqual(1, result.Details.Hosts.Count);
			Assert.AreEqual("foo.bar", result.Details.Hosts.First().Key);

			Assert.AreEqual(1, result.Details.Providers.Length);
			Assert.AreEqual("foo.bar.provider", result.Details.Providers.First());
		}

		[DataTestMethod]
		[DataRow("")]
		[DataRow("\n")]
		[DataRow(null)]
		[ExpectedException(typeof(ArgumentNullException))]
		public async Task ShouldThrowOnEmptyDomain(string email)
		{
			// Arrange
			using var api = GetApi();

			// Act
			await api.IsFakeDomain(email);

			// Assert - Exception
		}

		[TestMethod]
		public async Task ShouldFailOnHttpStatusForDomain()
		{
			// Arrange
			messageHandler.Response.StatusCode = HttpStatusCode.InternalServerError;
			using var api = GetApi();

			// Act
			var result = await api.IsFakeDomain("foo.bar");

			// Assert
			Assert.IsNotNull(result);
			Assert.AreEqual("foo.bar", result.Request);

			Assert.IsFalse(result.IsSuccess);
			Assert.IsFalse(result.IsFakeDomain);
			Assert.AreEqual("Error 500: Internal Server Error", result.ErrorMessage);

			Assert.IsNull(result.Details);
		}

		[TestMethod]
		public async Task ShouldFailOnReturnCodeForDomain()
		{
			// Arrange
			messageHandler.Response.Content = new StringContent(@"{""retcode"": -50}", Encoding.UTF8, "application/json");
			using var api = GetApi();

			// Act
			var result = await api.IsFakeDomain("foo.bar");

			// Assert
			Assert.IsNotNull(result);
			Assert.AreEqual("foo.bar", result.Request);

			Assert.IsFalse(result.IsSuccess);
			Assert.IsFalse(result.IsFakeDomain);
			Assert.AreEqual("Error from FakeFilter with code: -50", result.ErrorMessage);

			Assert.IsNull(result.Details);
		}

		[TestMethod]
		public async Task ShouldFailOnHttpForDomain()
		{
			// Arrange
			using var api = GetApi();

			// Act
			var result = await api.IsFakeDomain("foo.bar");

			// Assert
			Assert.IsNotNull(result);
			Assert.AreEqual("foo.bar", result.Request);

			Assert.IsFalse(result.IsSuccess);
			Assert.IsFalse(result.IsFakeDomain);
			Assert.AreEqual("Object reference not set to an instance of an object.", result.ErrorMessage);

			Assert.IsNull(result.Details);
		}

		[TestMethod]
		public async Task ShouldReturnValidEmail()
		{
			// Arrange
			messageHandler.Response.Content = new StringContent(@"{""retcode"": 200, ""isFakeDomain"": false, ""details"": null}", Encoding.UTF8, "application/json");
			using var api = GetApi();

			// Act
			var result = await api.IsFakeEmail("test@fakefilter.net");

			// Assert
			Assert.IsNotNull(result);
			Assert.AreEqual("test@fakefilter.net", result.Request);

			Assert.IsTrue(result.IsSuccess);
			Assert.IsFalse(result.IsFakeDomain);
			Assert.IsNull(result.Details);
		}

		[TestMethod]
		public async Task ShouldReturnFakeEmail()
		{
			// Arrange
			messageHandler.Response.Content = new StringContent(@"{""retcode"": 200, ""isFakeDomain"": ""foo.bar"", ""details"": { ""providers"": [ ""foo.bar.provider"" ], ""hosts"": { ""foo.bar"": { ""firstseen"": 123456789, ""lastseen"": 123456789 } } } }", Encoding.UTF8, "application/json");
			using var api = GetApi();

			// Act
			var result = await api.IsFakeEmail("test@foo.bar");

			// Assert
			Assert.IsNotNull(result);
			Assert.AreEqual("test@foo.bar", result.Request);

			Assert.IsTrue(result.IsSuccess);
			Assert.IsTrue(result.IsFakeDomain);

			Assert.AreEqual(1, result.Details.Hosts.Count);
			Assert.AreEqual("foo.bar", result.Details.Hosts.First().Key);

			Assert.AreEqual(1, result.Details.Providers.Length);
			Assert.AreEqual("foo.bar.provider", result.Details.Providers.First());
		}

		[DataTestMethod]
		[DataRow("")]
		[DataRow("\n")]
		[DataRow(null)]
		[ExpectedException(typeof(ArgumentNullException))]
		public async Task ShouldThrowOnEmptyEmail(string email)
		{
			// Arrange
			using var api = GetApi();

			// Act
			await api.IsFakeEmail(email);

			// Assert - Exception
		}

		[TestMethod]
		[ExpectedException(typeof(ArgumentException))]
		public async Task ShouldThrowOnInvalidEmail()
		{
			// Arrange
			using var api = GetApi();

			// Act
			await api.IsFakeEmail("foo.bar");

			// Assert - Exception
		}

		[TestMethod]
		public async Task ShouldFailOnHttpStatusForEmail()
		{
			// Arrange
			messageHandler.Response.StatusCode = HttpStatusCode.InternalServerError;
			using var api = GetApi();

			// Act
			var result = await api.IsFakeEmail("test@foo.bar");

			// Assert
			Assert.IsNotNull(result);
			Assert.AreEqual("test@foo.bar", result.Request);

			Assert.IsFalse(result.IsSuccess);
			Assert.IsFalse(result.IsFakeDomain);
			Assert.AreEqual("Error 500: Internal Server Error", result.ErrorMessage);

			Assert.IsNull(result.Details);
		}

		[TestMethod]
		public async Task ShouldFailOnReturnCodeForEmail()
		{
			// Arrange
			messageHandler.Response.Content = new StringContent(@"{""retcode"": -50}", Encoding.UTF8, "application/json");
			using var api = GetApi();

			// Act
			var result = await api.IsFakeEmail("test@foo.bar");

			// Assert
			Assert.IsNotNull(result);
			Assert.AreEqual("test@foo.bar", result.Request);

			Assert.IsFalse(result.IsSuccess);
			Assert.IsFalse(result.IsFakeDomain);
			Assert.AreEqual("Error from FakeFilter with code: -50", result.ErrorMessage);

			Assert.IsNull(result.Details);
		}

		[TestMethod]
		public async Task ShouldFailOnHttpForEmail()
		{
			// Arrange
			using var api = GetApi();

			// Act
			var result = await api.IsFakeEmail("test@foo.bar");

			// Assert
			Assert.IsNotNull(result);
			Assert.AreEqual("test@foo.bar", result.Request);

			Assert.IsFalse(result.IsSuccess);
			Assert.IsFalse(result.IsFakeDomain);
			Assert.AreEqual("Object reference not set to an instance of an object.", result.ErrorMessage);

			Assert.IsNull(result.Details);
		}

		private FakeFilterApi GetApi()
		{
			var api = new FakeFilterApi();

			var fi = api.GetType().GetField("httpClient", BindingFlags.Instance | BindingFlags.NonPublic);
			var httpClient = new HttpClient(messageHandler.Mock.Object);

			(fi.GetValue(api) as IDisposable).Dispose();
			fi.SetValue(api, httpClient);

			return api;
		}
	}
}
