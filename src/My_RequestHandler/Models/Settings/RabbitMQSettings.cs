namespace My_RequestHandler.Models.Settings
{
    public class RabbitMQSettings
    {
        public string HostName { get; set; }
        public int Port { get; set; }
        public bool UseSSL { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public string RabbitMqCertSubject { get; set; }
        public string VirtualHost { get; set; }

        public string ExchangeName { get; set; }
        public string ExchangeType { get; set; }
        public string QueueName { get; set; }
        public string RoutingKey { get; set; }

        public string RabbitMQResponseQueueName { get; set; }
        public bool RabbitMqAutomaticRecoveryEnabled { get; set; }
        public bool DurableRequestHandlerExchangeName { get; set; }
        public bool AutoDeleteRequestHandlerExchangeName    { get; set; }
        public string RequestHandlerAllQueueName { get; set; }
        public bool DurableRequestHandlerAllQueueName { get; set; }
        public bool AutoDeleteRequestHandlerAllQueueName { get; set; }
        public bool ExclusiveRequestHandlerAllQueueName { get; set; }

        public ushort? MaxExecutingCommands { get; set; }
        

    }
}
