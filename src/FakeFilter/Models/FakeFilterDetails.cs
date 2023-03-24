using System.Collections.Generic;

namespace AMWD.Net.Api.FakeFilter.Models
{
	/// <summary>
	/// Detail information about the fake domain.
	/// </summary>
	public class FakeFilterDetails
	{
		/// <summary>
		/// Gets a list of providers using the fake domain.
		/// </summary>
		public string[] Providers { get; internal set; }

		/// <summary>
		/// Gets a list of hosts with their first/last seen information.
		/// </summary>
		public Dictionary<string, FakeFilterHostDetails> Hosts { get; internal set; }
	}
}
