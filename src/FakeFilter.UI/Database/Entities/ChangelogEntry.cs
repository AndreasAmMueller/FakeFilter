using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FakeFilter.UI.Enums;

namespace FakeFilter.UI.Database.Entities
{
	public class ChangelogEntry
	{
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.None)]
		public string Id { get; set; } = Guid.NewGuid().ToString();

		[ForeignKey(nameof(Changelog))]
		public int ChangelogId { get; set; }

		public Changelog Changelog { get; set; }

		public ChangelogEntryType Type { get; set; }

		public ChangelogEntryAction Action { get; set; }

		public string Domain { get; set; }

		public override string ToString()
			=> $"{Action} {Type} '{Domain}'";
	}
}
