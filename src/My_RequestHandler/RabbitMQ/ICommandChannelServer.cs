using My_RequestHandler.Logics;

namespace My_RequestHandler.RabbitMQ
{
    public interface ICommandChannelServer : IDisposable
    {
        void SetCommandHandlerFactory(ICommandHandlerFactory factory);
        Task Start();
        Task Stop();
    }
}
