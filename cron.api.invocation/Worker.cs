using System;
using System.Text.Json;
using System.Text.Json.Serialization;
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
                _logger.LogInformation($"PSS Cron Service started at: {DateTimeOffset.Now}");
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
            _logger.LogInformation($"Setting up next run from: {_settings.Cron}");
            var expression = CronExpression.Parse(_settings.Cron);
            var nextRun = expression.GetNextOccurrence(DateTimeOffset.Now, TimeZoneInfo.Local);

            if (nextRun.HasValue)
            {
                var timeTillNextRun = nextRun.Value - DateTimeOffset.Now;
                _timer.Change((int)timeTillNextRun.TotalMilliseconds, Timeout.Infinite);
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
                    await CallService();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Service call failed");
            }
            finally
            {
                SetupNextRun();
            }
        }

        private async Task<GenerateTokenResult> GetBearerToken(RestClient client)
        {
            var request = new RestRequest(Method.POST);
            request.AddHeader("cache-control", "no-cache");
            request.AddJsonBody(new
            {
                Email = _settings.ClientId,
                Password = _settings.ClientSecret
            });
            var response = await client.ExecuteAsync<GenerateTokenResult>(request);
            if(!response.IsSuccessful)
                throw new Exception($"Failed to get bearer token due to: {response.ErrorMessage}");

            return response.Data;
        }

        private RestClient GetHttpclientIgnoreSslWarnings(string url)
        {
            var client = new RestClient(url)
            {
                //using loop back address, so we are ignoring all cert errors for now.
                RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
            };
            return client;
        }

        private async Task CallService()
        {
            if (!_cancellationToken.IsCancellationRequested)
            {
                var client = GetHttpclientIgnoreSslWarnings(_settings.AuthUrl);

                var GetTokenResult = await GetBearerToken(client);

                foreach(var target in _settings.Targets)
                {
                    var request = new RestRequest(target.Uri, ParseMethod(target));
                    request.AddHeader("Authorization", $"Bearer {GetTokenResult.AccessToken}");
                    var resp = await client.ExecuteAsync(request);
                    var noice = resp.Content;
                    _logger.LogInformation($"Call to [{target.Method}] {target.Uri}, [{resp.StatusCode} - {resp.StatusDescription}]");

                }
            }
        }

        private Method ParseMethod(ApiTarget target)
        {
            if(Enum.TryParse(target.Method, true, out Method parsed))
                return parsed;

            throw new Exception($"Failed to parse method for: {JsonSerializer.Serialize(target)}");
        }

        public class GenerateTokenResult
        {
            public string AccessToken { get; set; }
            public DateTimeOffset ValidFrom { get; set; }
            public DateTimeOffset ValidTo { get; set; }
        }
    }
}
