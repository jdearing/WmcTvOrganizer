using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

using WmcTvOrganizer.Core.Models;

using Xunit;

namespace WmcTvOrganizer.Core.Test
{
    public class SeriesFinderTest
    {
        private static readonly JsonSerializerSettings JsonSerializerSettings;
        private static readonly JsonSerializer JsonSerializer;

        static SeriesFinderTest()
        {
            JsonSerializerSettings = new JsonSerializerSettings
            {
                Converters = new List<JsonConverter> { new StringEnumConverter() },
                Formatting = Formatting.None,
                NullValueHandling = NullValueHandling.Ignore
            };
            
            JsonSerializer = new JsonSerializer();
        }
        [Fact]
        public async Task ProcessEpisodes_Samples()
        {
            var text = await File.ReadAllTextAsync(Path.Combine("Samples", "Sample.json"));
            var wcmItems = JsonConvert.DeserializeObject<IEnumerable<WmcItem>>(text, JsonSerializerSettings);
            int i = 0;
        }
    }
}
