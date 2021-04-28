using System;
using System.Threading;
using System.Threading.Tasks;
using Cronos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RestSharp;

namespace cron.api.invocation
{
    public class Worker : BackgroundService
    {
        private readonly ServiceSettings _settings;
        private readonly ILogger<Worker> _logger;

        private Timer _timer;
        private CancellationToken _cancellationToken;

        public Worker(IOptions<ServiceSettings> settings, ILogger<Worker> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        protected override Task ExecuteAsync(CancellationToken cancellationToken)
        {
            try
            {
                _cancellationToken = cancellationToken;
                _logger.LogInformation($"Orion Service started at: {DateTimeOffset.Now}");
                _timer = new Timer(RunJob, null, Timeout.Infinite, Timeout.Infinite);
                SetupNextRun();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Service call failed");
            }

            return Task.CompletedTask;
        }

        private void SetupNextRun()
        {
            var expression = CronExpression.Parse(_settings.Cron);
            var nextRun = expression.GetNextOccurrence(DateTimeOffset.Now, TimeZoneInfo.Local);

            if (nextRun.HasValue)
            {
                var timeTillNextRun = nextRun.Value - DateTimeOffset.Now;
                _timer.Change((int)timeTillNextRun.TotalMilliseconds, -1);
            }
            else
            {
                throw new Exception("The cron expression is not setup to run again");
            }
        }

        private async void RunJob(object state)
        {
            try
            {
                if (!_cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation($"Calling Service at: {DateTimeOffset.Now}");
                    SetupNextRun();
                    await CallService();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Service call failed");
            }
        }

        private async Task<string> GetBearerToken()
        {
            var client = new RestClient(_settings.AuthUrl);
            var request = new RestRequest(Method.POST);
            request.AddHeader("cache-control", "no-cache");
            request.AddHeader("content-type", "application/x-www-form-urlencoded");
            request.AddParameter("application/x-www-form-urlencoded", $"grant_type=client_credentials&client_id={_settings.ClientId}&client_secret={_settings.ClientSecret}", ParameterType.RequestBody);
            var response = await client.ExecuteAsync(request);

            return response.IsSuccessful ? response.Content : throw new Exception($"Failed to get bearer token due to: {response.ErrorMessage}");
        }

        private async Task CallService()
        {
            if (!_cancellationToken.IsCancellationRequested)
            {
                var token = await GetBearerToken();

                var client = new RestClient(_settings.ServiceUrl);
                var request = new RestRequest(Method.GET);
                request.AddHeader("authorization", $"Bearer {token}");
                await client.ExecuteAsync(request);
            }
        }
    }
}
