using System.ComponentModel.DataAnnotations.Schema;

namespace FakeFilter.UI.Database.Entities
{
	public class HostProviderMapping
	{
		[ForeignKey(nameof(Host))]
		public int HostId { get; set; }

		public virtual Host Host { get; set; }

		[ForeignKey(nameof(Provider))]
		public int ProviderId { get; set; }

		public virtual Provider Provider { get; set; }
	}
}
