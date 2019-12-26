using System;
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

            services.AddOptions();

            CancellationTokenSource cts = new CancellationTokenSource();
            services.AddSingleton(cts);

            
            //services.Configure<TransfererOptions>(configuration.GetSection("TransfererOptions"));
            //services.AddSingleton<ITransferer, Transferer>();

            services.AddTransient<Program>();
        }

        private readonly ILogger<Program> _logger;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public Program(ILogger<Program> logger, CancellationTokenSource cancellationTokenSource)
        {
            _logger = logger;
            _cancellationTokenSource = cancellationTokenSource;

            Console.CancelKeyPress += CancelKeyHandler;
        }

        private async Task Run()
        {
            _logger.LogInformation("starting");
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
