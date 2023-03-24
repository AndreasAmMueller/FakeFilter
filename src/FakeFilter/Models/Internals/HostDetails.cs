using Newtonsoft.Json;

namespace AMWD.Net.Api.FakeFilter.Models.Internals
{
	internal class HostDetails
	{
		[JsonProperty("firstseen")]
		public int FirstSeen { get; set; }

		[JsonProperty("lastseen")]
		public int LastSeen { get; set; }
	}
}
