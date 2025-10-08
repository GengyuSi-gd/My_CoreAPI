using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace My_RequestHandler.Models.Settings
{
    public class CoreAPISettings
    {
        private IConfiguration _configuration { get; set; }

        private static ILogger _logger { get; set; }

        public CoreAPISettings(IConfiguration configuration)
        {
            _configuration = configuration;
            
        }

        public DateTimeOffset CurrentDateTime => DateTimeOffset.Now;
        public string ApplicationName => _configuration["ApplicationName"] ?? "My_CoreAPI";
        public RabbitMQSettings RabbitMqSettings => _configuration.GetSection("RabbitMQSettings").Get<RabbitMQSettings>() ?? new RabbitMQSettings();
    }
}
