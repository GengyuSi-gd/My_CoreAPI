using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using My_RequestHandler.Logics;
using My_RequestHandler.Logics.Handlers;
using My_RequestHandler.Models.Settings;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace My_RequestHandler.RabbitMQ
{
        public class ClientConfiguration
        {
            public string HostName;
            public string Username;
            public string Password;
            public string VirtualHost;
            public ushort? PrefetchCount;
            public ushort? MaxExecutingCommands;
            public bool AutomaticRecoveryEnabled;
            public int? Port { get; set; } = 5672;
            public bool UseSsl;
            public string CertSubject;
            public string RequestExchangeName;
            public string RequestQueueName;
            public string ResponseQueueName;
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
                    RequestQueueName = RequestQueueName,
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


    public class RabbitMqCommandChannelServer : ICommandChannelServer
    {
        //private readonly IRequestHandlerSettings _settings;

        private ClientConfiguration _configuration;
        private ICommandHandlerFactory _commandHandlerFactory;
        private IConnection _connection;
        private IChannel _model;
        private AsyncEventingBasicConsumer _consumer;
        private string _consumerTag;
        private Dictionary<string, IChannel> _replyTos;
        private SemaphoreSlim _semaphore;
        private bool _running;
        private bool _disconnecting;
        private Timer _reconnectionTimer;
        private readonly ILogger<RabbitMqCommandChannelServer> _logger;
        private Lazy<X509Certificate2> _lazyCert;
        private static readonly object _locker = new object();

        public RabbitMqCommandChannelServer(ILogger<RabbitMqCommandChannelServer> logger, IOptions<RabbitMQSettings> options, ICommandHandlerFactory factory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            var config = options.Value;
            _configuration = new ClientConfiguration
            {
                HostName = config.HostName,
                Username = config.UseSSL ? default(string) : config.UserName,
                Password = config.UseSSL ? default(string) : config.Password,
                Port = config.Port,
                VirtualHost = Environment.ExpandEnvironmentVariables(config.VirtualHost ?? "/"),
                RequestQueueName = config.RequestHandlerAllQueueName,
                ResponseQueueName = config.RabbitMQResponseQueueName,
                PrefetchCount = 3,
                MaxExecutingCommands = config.MaxExecutingCommands ?? 50,
                UseSsl = config.UseSSL,
                CertSubject = config.RabbitMqCertSubject,
                ExchangeDeclaration = new ExchangeDeclaration
                                      {
                                          ExchangeName = config.ExchangeName,
                                          ExchangeType = "topic",
                                          Durable = false,
                                          AutoDelete = false
                                      },
                QueueDeclaration = new QueueDeclaration
                                   {
                                       QueueName = config.RequestHandlerAllQueueName,
                                       Durable = false,
                                       AutoDelete = false,
                                       Exclusive = false
                                   },
                QueueBind = new QueueBind
                            {
                                QueueName = config.RequestHandlerAllQueueName,
                                Exchange = config.ExchangeName,
                                RoutingKey = "*"
                            }
            };

            _semaphore = new SemaphoreSlim(_configuration.MaxExecutingCommands.Value);

            _consumerTag = Environment.MachineName + "." + Process.GetCurrentProcess().Id + "." + Guid.NewGuid();
            _commandHandlerFactory = factory;
            //_settings = settings;
        }

        public void SetCommandHandlerFactory(ICommandHandlerFactory factory)
        {

        }

        public async Task Start()
        {
            CheckDisposed();

            if (_running)
                throw new InvalidOperationException("Service is already running.");

            try
            {
                await Connect();
            }
            catch (Exception)
            {
                await Disconnect();

                throw;
            }
        }

        public async Task Stop()
        {
            CheckDisposed();

            if (!_running)
                throw new InvalidOperationException("Service is not running.");

            await Disconnect();
        }

        private int _interval = 1000 * 60;

        private async Task Connect()
        {
            ConnectionFactory factory = new ConnectionFactory();

            factory.HostName = _configuration.HostName;
            factory.UserName = _configuration.Username;
            factory.Password = _configuration.Password;
            factory.VirtualHost = _configuration.VirtualHost;
            factory.Port = _configuration.Port.GetValueOrDefault();
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
            //     ? new AuthMechanismFactory[] { new ExternalMechanismFactory() }
            //    : ConnectionFactory.DefaultAuthMechanisms;
            
            _connection = await factory.CreateConnectionAsync(Environment.MachineName);

            _connection.ConnectionRecoveryErrorAsync += _connection_ConnectionRecoveryError;
            _connection.ConnectionShutdownAsync += _connection_ConnectionShutdown;
            _connection.RecoverySucceededAsync += _connection_RecoverySucceeded;

            _model = await _connection.CreateChannelAsync();
            await _model.BasicQosAsync(0, _configuration.PrefetchCount.Value, false);

            await _model.ExchangeDeclareAsync(_configuration.ExchangeDeclaration.ExchangeName,
                _configuration.ExchangeDeclaration.ExchangeType,
                _configuration.ExchangeDeclaration.Durable,
                _configuration.ExchangeDeclaration.AutoDelete);

            await _model.QueueDeclareAsync(_configuration.QueueDeclaration.QueueName,
                _configuration.QueueDeclaration.Durable,
                _configuration.QueueDeclaration.Exclusive,
                _configuration.QueueDeclaration.AutoDelete);

            await _model.QueueBindAsync(_configuration.QueueBind.QueueName,
                _configuration.QueueBind.Exchange,
                _configuration.QueueBind.RoutingKey);

            _consumer = new AsyncEventingBasicConsumer(_model);
            _consumer.ReceivedAsync += OnMessageReceived;
            await _model.BasicConsumeAsync(_configuration.RequestQueueName, false, _consumerTag, _consumer);

            _running = true;

            _logger.LogInformation("Connection to RabbitMQ established.");

        }


        private void CheckConnection()
        {
            if (_model == null || _model.IsOpen == false)
            {
                lock (_locker)
                {
                    if (_model == null || _model.IsOpen == false)
                    {
                        Connect();
                    }
                }
            }
        }

        private X509Certificate2 CertFactory()
        {
            //var storeLocation = ConfigurationHelper.GetStoreLocation();
            //using (var store = new X509Store(StoreName.My, storeLocation))
            //{
            //    store.Open(OpenFlags.ReadOnly);
            //    var certFindType = X509FindType.FindBySubjectName;
            //    var certFindValue = _configuration.CertSubject;
            //    var certificateCol = store.Certificates.Find(certFindType, certFindValue, true);
            //    store.Close();
            //    return certificateCol[0];
            //}
            return null!;
        }

        private X509Certificate ConfigOnCertificateSelection(object sender, string targetHost, X509CertificateCollection localCertificates, X509Certificate remoteCertificate, string[] acceptableIssuers)
        {
            _lazyCert = _lazyCert ?? new Lazy<X509Certificate2>(CertFactory);
            return _lazyCert.Value;
        }

        private async Task Disconnect()
        {
            if (_running)
            {
                _disconnecting = true;

                try
                {
                    await _model.BasicCancelAsync(_consumerTag);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Exception calling BasicCancel on consumer.");
                }
                finally
                {
                    _consumer = null;
                }

                Stopwatch waitTime = Stopwatch.StartNew();

                while (_semaphore.CurrentCount < _configuration.MaxExecutingCommands && waitTime.Elapsed.Seconds < 60)
                    Thread.Sleep(100);

                waitTime.Stop();

                if (_semaphore.CurrentCount < _configuration.MaxExecutingCommands)
                    _logger.LogError("Timeout waiting for all commands to complete.  Disposing of model.");

                try
                {
                    await _model.CloseAsync();
                    _model?.Dispose();
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Exception disposing model.");
                }
                finally
                {
                    _model = null;
                }

                try
                {
                    await _connection.CloseAsync();
                    _connection?.Dispose();
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Exception disposing connection.");
                }
                finally
                {
                    _connection = null;
                }

                _disconnecting = false;
                _running = false;

                _logger.LogInformation("Disconnected from RabbitMQ.");
            }
        }

        private Task _connection_ConnectionShutdown(object sender, ShutdownEventArgs e)
        {
            if (!_disconnecting)
                _logger.LogError("Connection to RabbitMQ lost.");

            return Task.CompletedTask;
        }

        private Task _connection_ConnectionRecoveryError(object sender, ConnectionRecoveryErrorEventArgs e)
        {
            _logger.LogInformation(e.Exception, "Error reconnecting to RabbitMQ."); 
         
            return Task.CompletedTask;
        }

        private Task _connection_RecoverySucceeded(object sender, AsyncEventArgs e)
        {
            _logger.LogInformation("Connection to RabbitMQ reestablished.");
            return Task.CompletedTask;
        }

        private async Task OnMessageReceived(object source, BasicDeliverEventArgs eventArgs)
        {
            ICommandHandler handler;

            try
            {
                handler = _commandHandlerFactory.CreateCommandHandler(eventArgs.BasicProperties.Type);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception calling CreateCommandHandler.");
                await _model.BasicAckAsync(eventArgs.DeliveryTag, false);
                handler = null;
            }

            if (handler != null)
            {
                Task newTask = new Task(
                    async () =>
                    {
                        try
                        {
                            Interlocked.Increment(ref _commandEntered);
                            await _model.BasicAckAsync(eventArgs.DeliveryTag, false);

                            var responseBuffer = await handler.HandleAsync(eventArgs.Body.ToArray());

                            await ReplyTo(eventArgs.BasicProperties.ReplyTo, eventArgs.BasicProperties.CorrelationId, responseBuffer);
                        }
                        catch (Exception e)
                        {
                            _logger.LogError(e, "Exception while executing command.");
                        }
                        finally
                        {
                            _semaphore.Release();
                        }
                    });

                _semaphore.Wait();

                newTask.Start();
            }
        }

        private long _commandEntered;

        private async Task ReplyTo(string replyTo, string correlationId, byte[] responseBuffer)
        {
            CheckConnection();

            var responseProperties = new BasicProperties
            {
                CorrelationId = correlationId,
                Persistent = true, 
            };

            await _model.BasicPublishAsync("", replyTo, mandatory:false, responseProperties, responseBuffer);
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

    }


}


