using NLog;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net.Security;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Channels;
using System.Threading.Tasks;
using My_CoreAPI.Models.Settings;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace My_CoreAPI.RabbitMQ
{
        public class ClientConfiguration
        {
            public string HostName;
            public string Username;
            public string Password;
            public string VirtualHost;
            public ushort? PrefetchCount;
            public bool AutomaticRecoveryEnabled;
            public int? Port { get; set; } = 5672;
            public bool UseSsl { get; set; }
            public string CertSubject { get; set; }
            public string RequestExchangeName { get; set; }
            public string ResponseQueueName { get; set; }
            public ExchangeDeclaration ExchangeDeclaration { get; set; }
            public QueueDeclaration QueueDeclaration { get; set; }
            public QueueBind QueueBind { get; set; }

            public ClientConfiguration CloneWith(Action<ClientConfiguration> configure)
            {
                var clonedConfig = new ClientConfiguration
                {
                    HostName = HostName,
                    Username = Username,
                    Password = Password,
                    VirtualHost = VirtualHost,
                    RequestExchangeName = RequestExchangeName,
                    ResponseQueueName = ResponseQueueName,
                    PrefetchCount = PrefetchCount,
                    AutomaticRecoveryEnabled = AutomaticRecoveryEnabled,
                    Port = Port,
                    UseSsl = UseSsl,
                    CertSubject = CertSubject,
                    ExchangeDeclaration = ExchangeDeclaration == null
                        ? null
                        : new ExchangeDeclaration
                        {
                            ExchangeName = ExchangeDeclaration.ExchangeName,
                            ExchangeType = ExchangeDeclaration.ExchangeType,
                            Durable = ExchangeDeclaration.Durable,
                            AutoDelete = ExchangeDeclaration.AutoDelete
                        },
                    QueueDeclaration = QueueDeclaration == null
                        ? null
                        : new QueueDeclaration
                        {
                            AutoDelete = QueueDeclaration.AutoDelete,
                            Durable = QueueDeclaration.Durable,
                            Exclusive = QueueDeclaration.Exclusive,
                            QueueName = QueueDeclaration.QueueName
                        },
                    QueueBind = QueueBind == null
                        ? null
                        : new QueueBind
                        {
                            Exchange = QueueBind.Exchange,
                            QueueName = QueueBind.QueueName,
                            RoutingKey = QueueBind.RoutingKey
                        }
                };

                configure?.Invoke(clonedConfig);

                return clonedConfig;
            }

            public ClientConfiguration GetFromAppSetting(RabbitMQSettings settings)
            {
                this.HostName = settings.HostName;
                this.Username = settings.UserName;
                this.Password = settings.Password;
                this.VirtualHost = settings.VirtualHost;
                this.PrefetchCount = 10;
                this.Port = settings.Port;
                this.UseSsl = settings.UseSSL;
                CertSubject = settings.RabbitMqCertSubject;
                RequestExchangeName = settings.ExchangeName;
                ResponseQueueName = settings.RabbitMQResponseQueueName;
                AutomaticRecoveryEnabled = settings.RabbitMqAutomaticRecoveryEnabled;
                ExchangeDeclaration = new ExchangeDeclaration
                {
                    ExchangeName = settings.ExchangeName,
                    ExchangeType = settings.ExchangeType,
                    Durable = settings.DurableRequestHandlerExchangeName,
                    AutoDelete = settings.AutoDeleteRequestHandlerExchangeName
                };
                QueueDeclaration = new QueueDeclaration
                {
                    QueueName = settings.RequestHandlerAllQueueName,
                    Durable = settings.DurableRequestHandlerAllQueueName,
                    AutoDelete = settings.AutoDeleteRequestHandlerAllQueueName,
                    Exclusive = settings.ExclusiveRequestHandlerAllQueueName
                };
                QueueBind = new QueueBind
                {
                    QueueName = settings.RequestHandlerAllQueueName,
                    Exchange = settings.ExchangeName,
                    RoutingKey = settings.RoutingKey
                };
                return this;
            }
        }

        public class ExchangeDeclaration
        {
            public string ExchangeName;
            public string ExchangeType;
            public bool Durable;
            public bool AutoDelete;
        }

        public class QueueDeclaration
        {
            public string QueueName;
            public bool Exclusive;
            public bool Durable;
            public bool AutoDelete;
        }

        public class QueueBind
        {
            public string QueueName;
            public string Exchange;
            public string RoutingKey;
        }

        [ExcludeFromCodeCoverage(Justification = "RabbitMq Connection")]
        public class RabbitMqCommandChannelClient : ICommandChannelClient
        {
            public RabbitMqCommandChannelClient(ILogger<RabbitMqCommandChannelClient> logger, CoreAPISettings settings, RabbitMQSettings rbMqSettings)
            {
                _logger = logger;
            
                var config = new ClientConfiguration().GetFromAppSetting(settings.RabbitMqSettings);

                _configuration = new ClientConfiguration
                {
                    HostName = config.HostName,
                    Username = config.UseSsl ? default(string) : config.Username,
                    Password = config.UseSsl ? default(string) : config.Password,
                    Port = config.Port.GetValueOrDefault(),
                    VirtualHost = Environment.ExpandEnvironmentVariables(config.VirtualHost),
                    RequestExchangeName = config.RequestExchangeName,
                    ResponseQueueName = config.ResponseQueueName ?? "q." + Environment.MachineName + "." + Process.GetCurrentProcess().Id + "." + Guid.NewGuid(),
                    PrefetchCount = config.PrefetchCount ?? 10,
                    UseSsl = config.UseSsl,
                    CertSubject = config.CertSubject,
                    AutomaticRecoveryEnabled = config.AutomaticRecoveryEnabled,
                    ExchangeDeclaration = config.ExchangeDeclaration,
                    QueueDeclaration = config.QueueDeclaration,
                    QueueBind = config.QueueBind
                };
                _logger.LogInformation($"RabbitMqCommandChannelClient constructor: RequestExchangeName: {_configuration.RequestExchangeName}, ResponseQueueName: {_configuration.ResponseQueueName}, config.VirtualHost: {config.VirtualHost}, _configuration.VirtualHost: {_configuration.VirtualHost}");

                _consumerTag = Environment.MachineName + "." + Process.GetCurrentProcess().Id + "." + Guid.NewGuid();

                _responseTable = new ConcurrentDictionary<string, TaskCompletionSource<byte[]>>();

                Connect().GetAwaiter().GetResult();
            }

            public async Task Connect()
            {
                CheckDisposed();

                ConnectionFactory factory = new ConnectionFactory();

                factory.HostName = _configuration.HostName;
                factory.UserName = _configuration.Username;
                factory.Password = _configuration.Password;
                factory.VirtualHost = _configuration.VirtualHost;
                factory.Port = _configuration.Port.GetValueOrDefault();
                factory.RequestedHeartbeat = TimeSpan.FromSeconds(30);
                factory.AutomaticRecoveryEnabled = _configuration.AutomaticRecoveryEnabled;
                factory.Ssl = _configuration.UseSsl ? new SslOption
                {
                    Enabled = true,
                    ServerName = factory.HostName,
                    AcceptablePolicyErrors = SslPolicyErrors.RemoteCertificateNameMismatch |
                                             SslPolicyErrors.RemoteCertificateChainErrors,
                    CertificateSelectionCallback = ConfigOnCertificateSelection,
                    Version = SslProtocols.Tls12
                } : factory.Ssl;
                factory.AuthMechanisms = ConnectionFactory.DefaultAuthMechanisms;
                    //_configuration.UseSsl
                    //? new AuthMechanismFactory[] { new ExternalMechanismFactory() }
                    //: ConnectionFactory.DefaultAuthMechanisms;

                _connection = await factory.CreateConnectionAsync(Environment.MachineName);

                _connection.ConnectionRecoveryErrorAsync += _connection_ConnectionRecoveryError;
                _connection.ConnectionShutdownAsync += _connection_ConnectionShutdown;
                _connection.RecoverySucceededAsync += _connection_RecoverySucceeded;

                _channel = await _connection.CreateChannelAsync();
                _channel.BasicQosAsync(0, _configuration.PrefetchCount.Value, false);

                //request queue
                await _channel.ExchangeDeclareAsync(_configuration.ExchangeDeclaration.ExchangeName,
                    _configuration.ExchangeDeclaration.ExchangeType,
                    _configuration.ExchangeDeclaration.Durable,
                    _configuration.ExchangeDeclaration.AutoDelete);

                await _channel.QueueDeclareAsync(_configuration.QueueDeclaration.QueueName,
                    _configuration.QueueDeclaration.Durable,
                    _configuration.QueueDeclaration.Exclusive,
                    _configuration.QueueDeclaration.AutoDelete);

                await _channel.QueueBindAsync(_configuration.QueueBind.QueueName,
                    _configuration.QueueBind.Exchange,
                    _configuration.QueueBind.RoutingKey);

                //response queue
                var responseQueueArgs = new Dictionary<string, object>
                {
                    { "x-expires", 1800000 } //30 mins ttl for queue if no activity, different than auto-delete
                };

                await _channel.QueueDeclareAsync(_configuration.ResponseQueueName, _configuration.QueueDeclaration.Durable, false, false, responseQueueArgs);

                _consumer = new AsyncEventingBasicConsumer(_channel);
                _consumer.ReceivedAsync += OnMessageReceived;
                await _channel.BasicConsumeAsync(_configuration.ResponseQueueName, false, _consumerTag, _consumer);

                _connected = true;

                _logger.LogInformation("Connection to RabbitMQ established.");
            }

            private void CheckConnect()
            {
                if (_channel == null || _channel.IsOpen == false)
                {
                    lock (_locker)
                    {
                        if (_channel == null || _channel.IsOpen == false)
                        {
                            Connect().GetAwaiter().GetResult();
                        }
                    }
                }
            }

            private X509Certificate2 CertFactory()
            {
                using (var store = new X509Store(StoreName.My, StoreLocation.CurrentUser))
                {
                    store.Open(OpenFlags.ReadOnly);
                    var certFindType = X509FindType.FindBySubjectName;
                    var certFindValue = _configuration.CertSubject;
                    var certificateCol = store.Certificates.Find(certFindType, certFindValue, true);
                    store.Close();
                    return certificateCol[0];
                }
            }

            private X509Certificate ConfigOnCertificateSelection(object sender, string targetHost, X509CertificateCollection localCertificates, X509Certificate remoteCertificate, string[] acceptableIssuers)
            {
                _lazyCert = _lazyCert ?? new Lazy<X509Certificate2>(CertFactory);
                return _lazyCert.Value;
            }

            public async Task Disconnect()
            {
                CheckDisposed();

                if (_connected)
                {
                    _disconnecting = true;

                    try
                    {
                        await _channel.CloseAsync();
                        await _connection.CloseAsync();
                        await _channel.DisposeAsync();
                        await _connection.DisposeAsync();
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Exception calling BasicCancel on consumer.");
                    }
                    finally
                    {
                        _channel = null;
                        _connection = null;
                    }


                    _disconnecting = false;
                    _connected = false;

                    _logger.LogInformation("Disconnected from RabbitMQ.");
                }
            }

            private Task _connection_ConnectionShutdown(object sender, ShutdownEventArgs e)
            {
                if (!_disconnecting)
                    _logger.LogError($"Connection to RabbitMQ lost. ShutdownEventArgs: {e}");
                return Task.CompletedTask;
            }

            private Task _connection_ConnectionRecoveryError(object sender, ConnectionRecoveryErrorEventArgs e)
            {
                _logger.LogError(e.Exception, "Error reconnecting to RabbitMQ.");
                return Task.CompletedTask;
            }

            private Task _connection_RecoverySucceeded(object sender, AsyncEventArgs asyncEventArgs)
            {
                _logger.LogInformation("Connection to RabbitMQ reestablished.");
                return Task.CompletedTask;
            }

            public Task<byte[]> GetResponseAsync(string commandType, byte[] requestBuffer)
            {
                CheckDisposed();
                CheckConnect();

                var correlationId = Guid.NewGuid().ToString();
                var tcs = new TaskCompletionSource<byte[]>();

                //CancellationContext.Current.Token.Register(() =>
                //{
                //    tcs.SetCanceled();
                //});

                _responseTable[correlationId] = tcs;

                var properties = new BasicProperties();
                properties.CorrelationId = correlationId;
                properties.ReplyTo = _configuration.ResponseQueueName;
                properties.Type = commandType;
                properties.Persistent = true;

                _logger.LogInformation($"Sending request of type {commandType} with correlation id {correlationId}, using queue {_configuration.RequestExchangeName}, virtual host: {_configuration.VirtualHost}");
                _channel.BasicPublishAsync(_configuration.RequestExchangeName, commandType, mandatory: true, properties, requestBuffer);

                return tcs.Task;

            }

            private async Task OnMessageReceived(object sender, BasicDeliverEventArgs eventArgs)
            {
                await _channel.BasicAckAsync(eventArgs.DeliveryTag, false).ConfigureAwait(false);

                TaskCompletionSource<byte[]> tcs;

                if (eventArgs.BasicProperties.CorrelationId != null && _responseTable.TryRemove(eventArgs.BasicProperties.CorrelationId, out tcs))
                {
                    tcs.SetResult(eventArgs.Body.ToArray());
                }
                else
                    Console.WriteLine("Unknown correlation id"); // TODO: Log this
            }

            #region IDisposable

            public void Dispose()
            {
                Dispose(true);
            }

            protected virtual void Dispose(bool disposing)
            {
                if (disposing)
                {
                    Disconnect();
                }

                _disposed = true;
            }

            private void CheckDisposed()
            {
                if (_disposed)
                    throw new ObjectDisposedException(GetType().Name);
            }

            private bool _disposed;

            #endregion

            private IConnection _connection;
            private IChannel _channel;
            private AsyncEventingBasicConsumer _consumer;
            private readonly ConcurrentDictionary<string, TaskCompletionSource<byte[]>> _responseTable;
            private readonly ClientConfiguration _configuration;
            private readonly string _consumerTag;
            private bool _connected;
            private bool _disconnecting;
            private readonly ILogger _logger;
            private Lazy<X509Certificate2> _lazyCert;
            private readonly object _locker = new object();
        }
    

}


