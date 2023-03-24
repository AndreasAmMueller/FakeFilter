using Newtonsoft.Json;

namespace AMWD.Net.Api.FakeFilter.Models.Internals
{
	internal class PingResponse
	{
		[JsonProperty("retcode")]
		public int ReturnCode { get; set; }

		[JsonProperty("message")]
		public string Message { get; set; }
	}
}
