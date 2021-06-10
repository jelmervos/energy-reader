using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EnergyReader
{
    internal sealed class Program
    {
        private static async Task Main(string[] args)
        {
            await Host.CreateDefaultBuilder(args)
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                })
                .ConfigureServices((_, services) => services
                    .AddLogging()
                    .AddHostedService<ConsoleHostedService>()
                    .AddSingleton(typeof(TelegramMessageBus))
#if (DEBUG)
                    .AddTransient<ITelegramSource, UdpSource>()
                    .AddTransient<ITelegramConsumer, InfluxDbWriter>())
#else
                    .AddTransient<ITelegramSource, SerialPortSource>()
                    .AddTransient<ITelegramConsumer, FileWriter>()
                    .AddTransient<ITelegramConsumer, UdpBroadcaster>()
                    .AddTransient<ITelegramConsumer, InfluxDbWriter>())
#endif
                .RunConsoleAsync();
        }
    }

    internal sealed class ConsoleHostedService : IHostedService
    {
        private readonly ILogger logger;
        private readonly IHostApplicationLifetime appLifetime;
        private readonly TelegramMessageBus messageBus;
        private readonly AutoResetEvent applicationShutdownEvent;

        public ConsoleHostedService(ILogger<ConsoleHostedService> logger, IHostApplicationLifetime appLifetime, TelegramMessageBus messageBus)
        {
            this.logger = logger;
            this.appLifetime = appLifetime;
            this.messageBus = messageBus;
            applicationShutdownEvent = new AutoResetEvent(false);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            appLifetime.ApplicationStarted.Register(() =>
            {
                Task.Run(() =>
                {
                    try
                    {
                        messageBus.Start();
                        applicationShutdownEvent.WaitOne();
                        messageBus.Stop();
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Unhandled exception!");
                    }
                    finally
                    {
                        appLifetime.StopApplication();
                    }
                });
            });

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            applicationShutdownEvent.Set();
            return Task.CompletedTask;
        }
    }
}
