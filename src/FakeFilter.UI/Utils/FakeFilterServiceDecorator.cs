using AMWD.Net.Api.FakeFilter;
using Microsoft.Extensions.Configuration;

namespace FakeFilter.UI.Utils
{
	public class FakeFilterServiceDecorator : FakeFilterService
	{
		public FakeFilterServiceDecorator(IConfiguration configuration)
			: base(configuration.GetValue("FakeFilter:API", "https://fakefilter.net/api"))
		{
			DataUrl = configuration.GetValue("FakeFilter:Data", "https://raw.githubusercontent.com/7c/fakefilter/main/json/data_version2.json");
		}
	}
}
