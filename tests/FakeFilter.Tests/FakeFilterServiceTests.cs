using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AMWD.Common.Test;
using AMWD.Net.Api.FakeFilter.Models;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FakeFilter.Tests
{
	[TestClass]
	public class FakeFilterServiceTests
	{
		private HttpMessageHandlerMoq messageHandler;
		private Mock<FakeFilterApi> apiMock;

		private bool apiAvailable;
		private FakeFilterResponse apiResponse;

		[TestInitialize]
		public void InitializeTest()
		{
			messageHandler = new HttpMessageHandlerMoq();
			messageHandler.Response.Content = new StringContent(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "v2.json")), Encoding.UTF8, "application/json");

			apiAvailable = true;
			apiResponse = new FakeFilterResponse
			{
				IsSuccess = true
			};
		}

		[TestMethod]
		public void ShouldInitialize()
		{
			// Arrange

			// Act
			using var service = new FakeFilterService();

			// Assert
			Assert.IsNotNull(service);
		}

		[TestMethod]
		public void ShouldInitializeWithCustomUrl()
		{
			// Arrange
			string customUrl = "https://api.foo.bar/test";

			// Act
			using var service = new FakeFilterService(customUrl);

			// Assert
			var serviceField = service.GetType().GetField("api", BindingFlags.NonPublic | BindingFlags.Instance);
			var apiInstance = serviceField.GetValue(service) as FakeFilterApi;
			var apiField = apiInstance.GetType().GetField("url", BindingFlags.NonPublic | BindingFlags.Instance);
			Assert.AreEqual(customUrl, apiField.GetValue(serviceField.GetValue(service)));
		}

		#region IDisposable

		[TestMethod]
		public void ShouldAllowMultipleDispose()
		{
			// Arrange
			var service = new FakeFilterService();

			// Act
			service.Dispose();
			service.Dispose();

			// Assert
			Assert.IsNotNull(service);
		}

		[TestMethod]
		[ExpectedException(typeof(ObjectDisposedException))]
		public async Task ShouldThrowDisposedOnUpdateData()
		{
			// Arrange
			using var service = new FakeFilterService();
			service.Dispose();

			// Act
			await service.UpdateData();

			// Assert - ObjectDisposedException
			Assert.Fail();
		}

		[TestMethod]
		[ExpectedException(typeof(ObjectDisposedException))]
		public async Task ShouldThrowDisposedOnIsFakeDomain()
		{
			// Arrange
			using var service = new FakeFilterService();
			service.Dispose();

			// Act
			await service.IsFakeDomain("fakefilter.net");

			// Assert - ObjectDisposedException
			Assert.Fail();
		}

		[TestMethod]
		[ExpectedException(typeof(ObjectDisposedException))]
		public async Task ShouldThrowDisposedOnIsFakeEmail()
		{
			// Arrange
			using var service = new FakeFilterService();
			service.Dispose();

			// Act
			await service.IsFakeEmail("hello@fakefilter.net");

			// Assert - ObjectDisposedException
			Assert.Fail();
		}

		[TestMethod]
		[ExpectedException(typeof(ObjectDisposedException))]
		public void ShouldThrowDisposedOnGetAllDomains()
		{
			// Arrange
			using var service = new FakeFilterService();
			service.Dispose();

			// Act
			service.GetAllDomains();

			// Assert - ObjectDisposedException
			Assert.Fail();
		}

		[TestMethod]
		[ExpectedException(typeof(ObjectDisposedException))]
		public void ShouldThrowDisposedOnGetProvidersForDomain()
		{
			// Arrange
			using var service = new FakeFilterService();
			service.Dispose();

			// Act
			service.GetProvidersForDomain("fakefilter.net");

			// Assert - ObjectDisposedException
			Assert.Fail();
		}

		#endregion IDisposable

		#region UpdateData

		[TestMethod]
		public async Task ShouldUpdateData()
		{
			// Arrange
			using var service = GetService();

			// Act
			bool isSuccess = await service.UpdateData();

			// Assert
			Assert.IsTrue(isSuccess);
			Assert.IsNotNull(service.LastUpdatedAt);
			Assert.AreEqual("2023-03-23_09:20:30_Z", service.LastUpdatedAt.Value.ToString("yyyy-MM-dd_HH:mm:ss_K"));
		}

		[DataTestMethod]
		[DataRow("")]
		[DataRow("\t")]
		[DataRow(null)]
		[ExpectedException(typeof(ArgumentNullException))]
		public async Task ShouldThrowNullOnUpdateData(string dataUrl)
		{
			// Arrange
			using var service = GetService();
			service.DataUrl = dataUrl;

			// Act
			await service.UpdateData();

			// Assert - ArgumentNullException
			Assert.Fail();
		}

		[TestMethod]
		public async Task ShouldFailForHttpErrorOnUpdateData()
		{
			// Arrange
			messageHandler.Response.StatusCode = HttpStatusCode.InternalServerError;
			using var service = GetService();

			// Act
			bool isSuccess = await service.UpdateData();

			// Assert
			Assert.IsFalse(isSuccess);
			Assert.IsNull(service.LastUpdatedAt);
		}

		[TestMethod]
		public async Task ShouldFailForExceptionOnUpdateData()
		{
			// Arrange
			messageHandler.Response.Content = null;
			using var service = GetService();

			// Act
			bool isSuccess = await service.UpdateData();

			// Assert
			Assert.IsFalse(isSuccess);
			Assert.IsNull(service.LastUpdatedAt);
		}

		[TestMethod]
		public async Task ShouldFailForWrongVersionOnUpdateData()
		{
			// Arrange
			string json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "v2.json"));
			var jObj = JsonConvert.DeserializeObject<JObject>(json);
			jObj["version"] = 1;
			json = JsonConvert.SerializeObject(jObj);

			messageHandler.Response.Content = new StringContent(json, Encoding.UTF8, "application/json");
			using var service = GetService();

			// Act
			bool isSuccess = await service.UpdateData();

			// Assert
			Assert.IsFalse(isSuccess);
			Assert.IsNull(service.LastUpdatedAt);
		}

		#endregion UpdateData

		#region IsFakeDomain

		[TestMethod]
		public async Task ShouldRequestApiOnIsFakeDomain()
		{
			// Arrange
			apiResponse.Request = "hello.world";
			using var service = GetService();
			await service.UpdateData();

			// Act
			var response = await service.IsFakeDomain(apiResponse.Request, useOnlyOfflineData: false);

			// Assert
			Assert.IsNotNull(service.LastUpdatedAt);

			Assert.IsNotNull(response);
			Assert.IsTrue(response.IsSuccess);
			Assert.IsFalse(response.IsFakeDomain);
			Assert.IsNull(response.Details);

			apiMock.Verify(a => a.IsAvailable(It.IsAny<CancellationToken>()), Times.Once);
			apiMock.Verify(a => a.IsFakeDomain(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
			apiMock.VerifyNoOtherCalls();
		}

		[TestMethod]
		public async Task ShouldUseBackupForUnavailableApiOnIsFakeDomain()
		{
			// Arrange
			apiAvailable = false;
			apiResponse.Request = "fakefilter.net";
			using var service = GetService();
			await service.UpdateData();

			// Act
			var response = await service.IsFakeDomain(apiResponse.Request, useOnlyOfflineData: false);

			// Assert
			Assert.IsNotNull(service.LastUpdatedAt);

			Assert.IsNotNull(response);
			Assert.IsTrue(response.IsSuccess);
			Assert.IsFalse(response.IsFakeDomain);
			Assert.IsNull(response.Details);

			apiMock.Verify(a => a.IsAvailable(It.IsAny<CancellationToken>()), Times.Once);
			apiMock.Verify(a => a.IsFakeDomain(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
			apiMock.VerifyNoOtherCalls();
		}

		[TestMethod]
		public async Task ShouldUseBackupForNotSuccessfulApiOnIsFakeDomain()
		{
			// Arrange
			apiResponse.Request = "hello.world";
			apiResponse.IsSuccess = false;
			using var service = GetService();
			await service.UpdateData();

			// Act
			var response = await service.IsFakeDomain(apiResponse.Request, useOnlyOfflineData: false);

			// Assert
			Assert.IsNotNull(service.LastUpdatedAt);

			Assert.IsNotNull(response);
			Assert.IsTrue(response.IsSuccess);
			Assert.IsTrue(response.IsFakeDomain);
			Assert.IsNotNull(response.Details);

			apiMock.Verify(a => a.IsAvailable(It.IsAny<CancellationToken>()), Times.Once);
			apiMock.Verify(a => a.IsFakeDomain(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
			apiMock.VerifyNoOtherCalls();
		}

		[TestMethod]
		public async Task ShouldUseOnlyOfflineOnIsFakeDomain()
		{
			// Arrange
			apiResponse.Request = "hello.world";
			using var service = GetService();
			await service.UpdateData();

			// Act
			var response = await service.IsFakeDomain(apiResponse.Request, useOnlyOfflineData: true);

			// Assert
			Assert.IsNotNull(service.LastUpdatedAt);

			Assert.IsNotNull(response);
			Assert.IsTrue(response.IsSuccess);
			Assert.IsTrue(response.IsFakeDomain);
			Assert.IsNotNull(response.Details);
			Assert.AreEqual(3, response.Details.Providers.Length);
			Assert.AreEqual(1, response.Details.Hosts.Count);

			CollectionAssert.AreEquivalent(new string[] { "fakemail.net", "github.com", "gitlab.com" }, response.Details.Providers);
			Assert.AreEqual("hello.world", response.Details.Hosts.First().Key);
			Assert.AreEqual("hello.world", response.Details.Hosts.First().Value.Host);
			Assert.AreEqual("2023-03-22_09:20:30_Z", response.Details.Hosts.First().Value.FirstSeen.ToString("yyyy-MM-dd_HH:mm:ss_K"));
			Assert.AreEqual("2023-03-20_09:20:30_Z", response.Details.Hosts.First().Value.LastSeen.ToString("yyyy-MM-dd_HH:mm:ss_K"));

			apiMock.Verify(a => a.IsAvailable(It.IsAny<CancellationToken>()), Times.Never);
			apiMock.VerifyNoOtherCalls();
		}

		[DataTestMethod]
		[DataRow("")]
		[DataRow("\n")]
		[DataRow(null)]
		[ExpectedException(typeof(ArgumentNullException))]
		public async Task ShouldThrowForEmptyDomainOnIsFakeDomain(string domain)
		{
			// Arrange
			using var service = GetService();

			// Act
			await service.IsFakeDomain(domain);

			// Assert - ArgumentNullException
			Assert.Fail();
		}

		#endregion IsFakeDomain

		#region IsFakeEmail

		[TestMethod]
		public async Task ShouldRequestApiOnIsFakeEmail()
		{
			// Arrange
			apiResponse.Request = "test@hello.world";
			using var service = GetService();
			await service.UpdateData();

			// Act
			var response = await service.IsFakeEmail(apiResponse.Request, useOnlyOfflineData: false);

			// Assert
			Assert.IsNotNull(service.LastUpdatedAt);

			Assert.IsNotNull(response);
			Assert.IsTrue(response.IsSuccess);
			Assert.IsFalse(response.IsFakeDomain);
			Assert.IsNull(response.Details);

			apiMock.Verify(a => a.IsAvailable(It.IsAny<CancellationToken>()), Times.Once);
			apiMock.Verify(a => a.IsFakeEmail(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
			apiMock.VerifyNoOtherCalls();
		}

		[TestMethod]
		public async Task ShouldUseBackupForUnavailableApiOnIsFakeEmail()
		{
			// Arrange
			apiAvailable = false;
			apiResponse.Request = "test@fakefilter.net";
			using var service = GetService();
			await service.UpdateData();

			// Act
			var response = await service.IsFakeEmail(apiResponse.Request, useOnlyOfflineData: false);

			// Assert
			Assert.IsNotNull(service.LastUpdatedAt);

			Assert.IsNotNull(response);
			Assert.IsTrue(response.IsSuccess);
			Assert.IsFalse(response.IsFakeDomain);
			Assert.IsNull(response.Details);

			apiMock.Verify(a => a.IsAvailable(It.IsAny<CancellationToken>()), Times.Once);
			apiMock.VerifyNoOtherCalls();
		}

		[TestMethod]
		public async Task ShouldUseBackupForNotSuccessfulApiOnIsFakeEmail()
		{
			// Arrange
			apiResponse.Request = "test@hello.world";
			apiResponse.IsSuccess = false;
			using var service = GetService();
			await service.UpdateData();

			// Act
			var response = await service.IsFakeEmail(apiResponse.Request, useOnlyOfflineData: false);

			// Assert
			Assert.IsNotNull(service.LastUpdatedAt);

			Assert.IsNotNull(response);
			Assert.IsTrue(response.IsSuccess);
			Assert.IsTrue(response.IsFakeDomain);
			Assert.IsNotNull(response.Details);

			apiMock.Verify(a => a.IsAvailable(It.IsAny<CancellationToken>()), Times.Once);
			apiMock.Verify(a => a.IsFakeEmail(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
			apiMock.VerifyNoOtherCalls();
		}

		[TestMethod]
		public async Task ShouldUseOnlyOfflineOnIsFakeEmail()
		{
			// Arrange
			apiResponse.Request = "test@hello.world";
			using var service = GetService();
			await service.UpdateData();

			// Act
			var response = await service.IsFakeEmail(apiResponse.Request, useOnlyOfflineData: true);

			// Assert
			Assert.IsNotNull(service.LastUpdatedAt);

			Assert.IsNotNull(response);
			Assert.IsTrue(response.IsSuccess);
			Assert.IsTrue(response.IsFakeDomain);
			Assert.IsNotNull(response.Details);
			Assert.AreEqual(3, response.Details.Providers.Length);
			Assert.AreEqual(1, response.Details.Hosts.Count);

			CollectionAssert.AreEquivalent(new string[] { "fakemail.net", "github.com", "gitlab.com" }, response.Details.Providers);
			Assert.AreEqual("hello.world", response.Details.Hosts.First().Key);
			Assert.AreEqual("hello.world", response.Details.Hosts.First().Value.Host);
			Assert.AreEqual("2023-03-22_09:20:30_Z", response.Details.Hosts.First().Value.FirstSeen.ToString("yyyy-MM-dd_HH:mm:ss_K"));
			Assert.AreEqual("2023-03-20_09:20:30_Z", response.Details.Hosts.First().Value.LastSeen.ToString("yyyy-MM-dd_HH:mm:ss_K"));

			apiMock.Verify(a => a.IsAvailable(It.IsAny<CancellationToken>()), Times.Never);
			apiMock.VerifyNoOtherCalls();
		}

		[DataTestMethod]
		[DataRow("")]
		[DataRow("\n")]
		[DataRow(null)]
		[ExpectedException(typeof(ArgumentNullException))]
		public async Task ShouldThrowForEmptyEmailOnIsFakeEmail(string email)
		{
			// Arrange
			using var service = GetService();

			// Act
			await service.IsFakeEmail(email);

			// Assert - ArgumentNullException
			Assert.Fail();
		}

		[TestMethod]
		[ExpectedException(typeof(ArgumentException))]
		public async Task ShouldThrowForInvalidEmailOnIsFakeEmail()
		{
			// Arrange
			using var service = GetService();

			// Act
			await service.IsFakeEmail("test");

			// Assert - ArgumentException
			Assert.Fail();
		}

		#endregion IsFakeEmail

		[TestMethod]
		public async Task ShouldReturnAllDomains()
		{
			// Arrange
			using var service = GetService();
			await service.UpdateData();

			// Act
			var list = service.GetAllDomains();

			// Assert
			Assert.IsNotNull(list);
			Assert.AreEqual(3, list.Count);
			CollectionAssert.AreEquivalent(new string[] { "foo.bar", "foo.baz", "hello.world" }, list);
		}

		[DataTestMethod]
		[DataRow("foo.bar", 1)]
		[DataRow("foo.baz", 1)]
		[DataRow("hello.world", 3)]
		public async Task ShouldReturnAllProvidersForDomain(string domain, int expectedCount)
		{
			// Arrange
			using var service = GetService();
			await service.UpdateData();

			// Act
			var list = service.GetProvidersForDomain(domain);

			// Assert
			Assert.IsNotNull(list);
			Assert.AreEqual(expectedCount, list.Count);

			if (expectedCount == 1)
				Assert.AreEqual("fakemail.net", list.First());
		}

		private FakeFilterService GetService()
		{
			apiMock = new Mock<FakeFilterApi>();
			apiMock
				.Setup(a => a.IsAvailable(It.IsAny<CancellationToken>()))
				.ReturnsAsync(apiAvailable);
			apiMock
				.Setup(a => a.IsFakeDomain(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(apiResponse);
			apiMock
				.Setup(a => a.IsFakeEmail(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(apiResponse);

			var service = new FakeFilterService();

			var httpClientField = service.GetType().GetField("httpClient", BindingFlags.Instance | BindingFlags.NonPublic);
			var httpClient = new HttpClient(messageHandler.Mock.Object);

			(httpClientField.GetValue(service) as IDisposable).Dispose();
			httpClientField.SetValue(service, httpClient);

			var apiField = service.GetType().GetField("api", BindingFlags.Instance | BindingFlags.NonPublic);
			(apiField.GetValue(service) as IDisposable).Dispose();
			apiField.SetValue(service, apiMock.Object);

			return service;
		}
	}
}
