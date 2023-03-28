using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace AMWD.Net.Api.FakeFilter.Models.Internals
{
	internal class FfData
	{
		[JsonProperty("version")]
		public int Version { get; set; }

		[JsonProperty("t")]
		public int TimestampUnix { get; set; }

		[JsonProperty("domains")]
		public Dictionary<string, Details> Domains { get; set; }
	}
}
