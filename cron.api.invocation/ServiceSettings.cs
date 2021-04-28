namespace cron.api.invocation
{
    public class ServiceSettings
    {
        public string AuthUrl { get; set; }
        public string Cron { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public ApiTarget[] Targets { get; set; }
    }

    public class ApiTarget
    {
        public string Method { get; set; }
        public string Uri { get; set; }
        public string Body { get; set; }
    }

}
