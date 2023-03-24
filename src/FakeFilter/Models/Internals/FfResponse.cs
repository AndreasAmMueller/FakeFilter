using Newtonsoft.Json;

namespace AMWD.Net.Api.FakeFilter.Models.Internals
{
	internal class FfResponse
	{
		[JsonProperty("retcode")]
		public int ReturnCode { get; set; }

		[JsonProperty("isFakeDomain")]
		public string FakeDomain { get; set; }

		[JsonProperty("details")]
		public Details Details { get; set; }
	}
}
