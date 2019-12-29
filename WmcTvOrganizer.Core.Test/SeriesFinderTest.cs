using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

using WmcTvOrganizer.Core.Models;

using Xunit;

namespace WmcTvOrganizer.Core.Test
{
    public class SeriesFinderTest
    {
        private static readonly JsonSerializerSettings JsonSerializerSettings;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly ServiceProvider _serviceProvider;

        static SeriesFinderTest()
        {
            JsonSerializerSettings = new JsonSerializerSettings
            {
                Converters = new List<JsonConverter> { new StringEnumConverter() },
                Formatting = Formatting.None,
                NullValueHandling = NullValueHandling.Ignore
            };
        }

        public SeriesFinderTest()
        {
            var serviceCollection = new ServiceCollection()
                .AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.AddDebug();
                })
                .AddHttpClient();

            _serviceProvider = serviceCollection.BuildServiceProvider();
           
            _cancellationTokenSource = new CancellationTokenSource();
        }
        
        [Fact]
        public async Task ProcessEpisodes_Samples()
        {
            var logger = _serviceProvider.GetRequiredService<ILogger<SeriesFinder>>();
            var tvDbClient = CreateTvDbClient();
            var settings = new MockSettings();
            var seriesFinder = new SeriesFinder(settings, tvDbClient, null, logger, _cancellationTokenSource);

            var text = await File.ReadAllTextAsync(Path.Combine("Samples", "Sample.json"));
            var wmcItems = JsonConvert.DeserializeObject<IEnumerable<WmcItem>>(text, JsonSerializerSettings);

            var cancellationTokenSource = new CancellationTokenSource();

            _ = Task.Run(() => MonitorConsole(cancellationTokenSource.Token));

            await seriesFinder.ProcessEpisodes(wmcItems);

            Assert.True(true);
        }

        private void MonitorConsole(CancellationToken cancellationToken)
        {
            StringBuilder builder = new StringBuilder();

            while (!cancellationToken.IsCancellationRequested)
            {
                builder.Append(Console.Read());
                int i = 0;
            }
        }


        private TvDbClient CreateTvDbClient()
        {
            var options = Options.Create(
                new TvDbClientOptions
                {
                    UserKey = Environment.GetEnvironmentVariable("TvDbClientOptions__UserKey"),
                    Username = Environment.GetEnvironmentVariable("TvDbClientOptions__Username"),
                    ApiKey = Environment.GetEnvironmentVariable("TvDbClientOptions__ApiKey")
                });

            var logger = _serviceProvider.GetRequiredService<ILogger<TvDbClient>>();
            var httpClient = _serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient();

            return new TvDbClient(options, httpClient, logger, _cancellationTokenSource);
        }
    }
}
