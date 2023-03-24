using System;

namespace AMWD.Net.Api.FakeFilter.Models
{
	/// <summary>
	/// Detailed host information.
	/// </summary>
	public class FakeFilterHostDetails
	{
		/// <summary>
		/// Gets the hostname.
		/// </summary>
		public string Host { get; internal set; }

		/// <summary>
		/// Gets the first seen time.
		/// </summary>
		public DateTime FirstSeen { get; internal set; }

		/// <summary>
		/// Gets the last seen time.
		/// </summary>
		public DateTime LastSeen { get; internal set; }
	}
}
