using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FakeFilter.UI.Database.Entities
{
	public class Changelog
	{
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public int Id { get; set; }

		public DateTime Timestamp { get; set; }

		public TimeSpan Duration { get; set; }

		public List<ChangelogEntry> Entries { get; set; }

		public override string ToString()
			=> $"Changelog from {Timestamp:yyyy-MM-dd HH:mm} ({Entries?.Count ?? 0} entries)";
	}
}
