using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.EventLog;
using System;

namespace cron.api.invocation
{
    public class Program
    {
        public static void Main(string[] args) => CreateHostBuilder(args).Build().Run();

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    var environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

                    var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                                                                  .AddJsonFile($"appsettings.{environmentName}.json", true, true)
                                                                  .AddEnvironmentVariables()
                                                                  .Build();

                    services
                        .AddHostedService<Worker>()
                        .Configure<EventLogSettings>(config =>
                            {
                                config.LogName = "Application";
                                config.SourceName = "PSS Cron Service Source";
                            })
                        .AddOptions()
                        .Configure<ServiceSettings>(configuration.GetSection(nameof(ServiceSettings)));
                }).UseWindowsService();
    }
}
