namespace My_CoreAPI.RabbitMQ
{
    public interface ICommandChannelClient : IDisposable
    {
        Task<byte[]> GetResponseAsync(string commandType, byte[] requestBuffer);
    }
}
