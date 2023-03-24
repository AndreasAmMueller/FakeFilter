namespace AMWD.Net.Api.FakeFilter.Models
{
	/// <summary>
	/// A FakeFilter response.
	/// </summary>
	public class FakeFilterResponse
	{
		#region Header

		/// <summary>
		/// Gets a value indicating whether the request was successful.
		/// </summary>
		public bool IsSuccess { get; internal set; }

		/// <summary>
		/// Gets the error message, when <see cref="IsSuccess"/> is <c>false</c>.
		/// </summary>
		public string ErrorMessage { get; internal set; }

		/// <summary>
		/// Gets the requested string.
		/// </summary>
		public string Request { get; internal set; }

		#endregion Header

		#region API response

		/// <summary>
		/// Gets a value indicating whether it is a fake domain.
		/// </summary>
		public bool IsFakeDomain { get; internal set; }

		/// <summary>
		/// Gets more details.
		/// </summary>
		public FakeFilterDetails Details { get; internal set; }

		#endregion API response
	}
}
