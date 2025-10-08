using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Common.Message.Request;
using Common.Message.Response;
using Microsoft.Extensions.Logging;

namespace My_RequestHandler.Logics.Handlers
{
    public abstract class CommandHandlerBase<TRequest, TResponse> : ICommandHandler
        where TRequest : BaseRequest
        where TResponse : BaseResponse
    {
        private ILogger<CommandHandlerBase<TRequest, TResponse>> _logger;

        public CommandHandlerBase(ILogger<CommandHandlerBase<TRequest, TResponse>> logger)
        {
            _logger = logger;
        }

        public async Task<byte[]> HandleAsync(byte[] requestBuffer)
        {
            //var serializer = new JsonSerializer();
            var correlationId = BitConverter.ToInt64(Guid.NewGuid().ToByteArray(), 0);
            string requestId;
            byte[] responseBuffer;
            BaseResponse response = null; // Initialize response to avoid CS0165

            using (var ms = new MemoryStream(requestBuffer))
            {
                var request = JsonSerializer.Deserialize<TRequest>(ms);
                //requestId = request.RequestHeader.RequestId.ToString();

                //if (string.IsNullOrEmpty(requestId))
                //{
                //    requestId = Guid.NewGuid().ToString();
                //    request.RequestHeader.RequestId = Guid.Parse(requestId);
                //}

                //MappedDiagnosticsLogicalContext.Set("requestId", requestId);
                //MappedDiagnosticsLogicalContext.Set("programCode", request.ProgramCode);

                //OptionsContext.Current = OptionsContext.Current.Add("requestId", requestId);
                //OptionsContext.Current = OptionsContext.Current.Add("programCode", request.ProgramCode);

                //if (request.RequestHeader.Options != null)
                //{
                //    OptionsContext.Current = OptionsContext.Current.Add(request.RequestHeader.Options);
                //    SharedOptionsContext.Current = SharedOptionsContext.Current.Add(request.RequestHeader.Options);
                //}


                try
                {
                    //_logManager.LogRequest(correlationId, requestId, Encoding.UTF8.GetString(requestBuffer),
                    //    DateTime.Now, "RequestHandlerService", typeof(TRequest).Name, "");

                    response = await ObtainLock(request);
                    if (response == null)
                        response = await Handle(request);

                    //CreateDomainContext(request);
                    //SetDomainContext(request);

                    //response = await VerifyIdentifiers(request);
                    //if (response.ResponseHeader.StatusCode == 0)
                    //{
                    //    response = await ObtainLock(request);
                    //    if (response == null || response.ResponseHeader.StatusCode == 0)
                    //        response = await Handle(request);
                    //}
                    //else
                    //    _logManager.LogWarn(response.ResponseHeader.StatusCode, $"Identifier validation failed. {response}", typeof(TRequest).Name, null);
                }
                catch (Exception e)
                {
                    //var response = e.HandleException<BaseResponse>(e, request);
                    _logger.LogError(e, "Error processing request in {Handler}", typeof(TRequest).Name);
                }
                finally
                {
                    if (response?.ResponseHeader?.StatusCode != 409)
                    {
                        //_logManager.LogWarn(0, $"Releasing api lock", typeof(TRequest).Name, null);
                        ReleaseLock(request);
                    }
                }

                using (var responseStream = new MemoryStream())
                {
                    await JsonSerializer.SerializeAsync(responseStream, response);
                    responseBuffer = responseStream.ToArray();
                }

                _logger.LogInformation("response");
                //_logManager.log(correlationId, requestId, Encoding.UTF8.GetString(responseBuffer), DateTime.Now,
                //    response?.ResponseHeader?.StatusCode.ToString(), "", "RequestHandlerService", typeof(TRequest).Name);

                return responseBuffer;
            }
        }


        private void CreateDomainContext(TRequest request)
        {
            //if (string.IsNullOrEmpty(request.ProgramCode))
            //    DomainContext.Current = DomainContext.Empty();
            //else
            //    DomainContext.Current =
            //        new DomainContext(ProgramCode.FromString(request.ProgramCode), null, null, null, null, null, null, null);
        }

        public abstract void SetDomainContext(TRequest request);
        public abstract Task<TResponse> VerifyIdentifiers(TRequest request);
        public abstract Task<TResponse> Handle(TRequest request);

        /// <summary>
        /// ObtainLock and ReleaseLock left empty on purpose so individual handlers can implement their own functionality
        /// </summary>
        /// <param name="request"></param>
        public virtual async Task<TResponse> ObtainLock(TRequest request)
        {
            return null;
        }

        public virtual void ReleaseLock(TRequest request) { }
    }
}
