using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using My_RequestHandler.RabbitMQ;

namespace My_RequestHandler.HostedServices
{
    internal class RequestHandlerService : IHostedService
    {
        private readonly ILogger<RequestHandlerService> _logger;
        private readonly ICommandChannelServer _channelServer;


        public RequestHandlerService(ILogger<RequestHandlerService> logger, ICommandChannelServer channelClient)
        {
            _logger = logger;
            _channelServer = channelClient;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("{Service} is running.", nameof(RequestHandlerService));

            _channelServer.Start();

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("{Service} is stopping.", nameof(RequestHandlerService));
            _channelServer.Stop();
            return Task.CompletedTask;
        }
    }
}
