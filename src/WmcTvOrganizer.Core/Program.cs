using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace WmcTvOrganizer.Core
{
    public class Program
    {
        public static readonly CultureInfo EnUsCulture = CultureInfo.CreateSpecificCulture("en-US");
        public const string TvDbApi = "TvDbApi";

        public static async Task Main(string[] args)
        {
            var services = new ServiceCollection();
            ConfigureServices(services);

            var serviceProvider = services.BuildServiceProvider();

            await serviceProvider.GetService<Program>().Run();
        }

        private static void ConfigureServices(ServiceCollection services)
        {
            string environment = Environment.GetEnvironmentVariable("ENVIRONMENT");

            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", true)
                .AddJsonFile($"appsettings.{environment}.json", true)
                .AddEnvironmentVariables()
                .Build();

            
            services.AddLogging(
               builder =>
               {
                   builder.AddDebug();
                   builder.AddConsole();
               });

            services.AddHttpClient(TvDbApi, c =>
            {
                c.BaseAddress = new Uri("https://api.thetvdb.com");
                //c.DefaultRequestHeaders.Add("Content-Type", "application/json"); 
            });

            services.AddOptions();

            CancellationTokenSource cts = new CancellationTokenSource();
            services.AddSingleton(cts);
            
            services.Configure<FileReaderOptions>(configuration.GetSection("FileReaderOptions"));
            services.AddTransient<IFileReader, FileReader>();

            services.AddSingleton<ISettings, Settings>();

            services.AddTransient<Program>();
        }

        private readonly ILogger<Program> _logger;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly IFileReader _fileReader;
        private ISettings _settings;

        public Program(IFileReader fileReader, ISettings settings, ILogger<Program> logger, CancellationTokenSource cancellationTokenSource)
        {
            _fileReader = fileReader;
            _settings = settings;
            _logger = logger;
            _cancellationTokenSource = cancellationTokenSource;

            Console.CancelKeyPress += CancelKeyHandler;
        }

        private async Task Run()
        {
            _logger.LogInformation("starting");
            await _settings.Load();
            var files = _fileReader.FindFiles();

            await _cancellationTokenSource.Token.WhenCanceled();
            _logger.LogInformation("exiting");
        }

        private void CancelKeyHandler(object sender, ConsoleCancelEventArgs e)
        {
            _logger.LogInformation("stopping");
            _cancellationTokenSource.Cancel();
        }
    }

    public static class CancellationTokenExtensions
    {
        public static Task WhenCanceled(this CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }
    }
}
