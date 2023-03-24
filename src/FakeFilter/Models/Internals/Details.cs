using System.Collections.Generic;
using Newtonsoft.Json;

namespace AMWD.Net.Api.FakeFilter.Models.Internals
{
	internal class Details
	{
		[JsonProperty("providers")]
		public string[] Providers { get; set; }

		[JsonProperty("hosts")]
		public Dictionary<string, HostDetails> Hosts { get; set; }
	}
}
