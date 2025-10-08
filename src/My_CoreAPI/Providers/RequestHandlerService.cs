using My_CoreAPI.RabbitMQ;
using System.Text.Json;
using Common.Message.Request;
using Common.Message.Response;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace My_CoreAPI.Providers
{
    public interface IRequestHandlerService
    {
        Task<BaseResponse> GetWeatherInfo(BaseRequest request);
    }

    public class RequestHandlerService : IRequestHandlerService
    {
        private readonly ICommandChannelClient _channelClient;
        private readonly ILogger _logger;

        public RequestHandlerService(ILogger<RequestHandlerService> logger, ICommandChannelClient channelClient)
        {
            _logger = logger;
            _channelClient = channelClient;
        }


        public async Task<BaseResponse> GetWeatherInfo(BaseRequest request)
        {
            _logger.LogInformation("RequestHandlerService: Processing weather info request.");


            var response = await GetResponse<BaseRequest, BaseResponse>("GetWeatherInfo", request).ConfigureAwait(false);

            _logger.LogInformation("RequestHandlerService: Weather info request processed successfully.");

            return response;
        }


        private async Task<TResponse> GetResponse<TRequest, TResponse>(string requestType, TRequest request)
        where TRequest : BaseRequest
        where TResponse : BaseResponse
        {
            //var serializer = new JsonSerializer();
            byte[] requestBuffer;
            using (var ms = new MemoryStream())
            {
                JsonSerializer.Serialize(ms, request);
                requestBuffer = ms.ToArray();
            }

            var correlationId = BitConverter.ToInt64(Guid.NewGuid().ToByteArray(), 0);
            var requestData = new { RequestTime = DateTime.Now };


            var client = _channelClient;

            var callTimer = System.Diagnostics.Stopwatch.StartNew();
            var responseBuffer = await client.GetResponseAsync(requestType, requestBuffer).ConfigureAwait(false);
            callTimer.Stop();
            TResponse result;
            using (var ms = new MemoryStream(responseBuffer))
            {
                result = (await JsonSerializer.DeserializeAsync<TResponse>(ms))!;
            }

            //    var baseResult = result as BaseResponse;
            //    if (baseResult is null || baseResult.ResponseHeader?.StatusCode != 0)
            //    {
            //        _logger.loginf(correlationId, requestId, Encoding.UTF8.GetString(requestBuffer), requestData.RequestTime, _serviceName,
            //nameof(GetResponse), "");

            //        _logManager.LogResponse(correlationId, requestId, Encoding.UTF8.GetString(responseBuffer), DateTime.Now, "",
            //            "", _serviceName, nameof(GetResponse), "", new List<KeyValuePair<string, string>>(
            //                new[]
            //                {
            //                new KeyValuePair<string, string>(
            //                    LoggingConstants.LogKey.ResponseCallTime,
            //                    callTimer.Elapsed.TotalMilliseconds.ToString(CultureInfo.InvariantCulture))
            //                }));
            //    }

            return result;

        }

        public void Dispose()
        {
            _channelClient?.Dispose();
        }
    }
}
