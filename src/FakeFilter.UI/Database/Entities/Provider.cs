using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AMWD.Common.EntityFrameworkCore.Attributes;

namespace FakeFilter.UI.Database.Entities
{
	public class Provider
	{
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public int Id { get; set; }

		[DatabaseIndex(IsUnique = true)]
		public string Name { get; set; }

		public override string ToString()
			=> $"Provider '{Name}'";
	}
}
