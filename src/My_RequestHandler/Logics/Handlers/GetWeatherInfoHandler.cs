using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Common.Message.Request;
using Common.Message.Response;
using Microsoft.Extensions.Logging;

namespace My_RequestHandler.Logics.Handlers
{
    public class GetWeatherInfoHandler : CommandHandlerBase<BaseRequest,BaseResponse>
    {

        public GetWeatherInfoHandler(ILogger<CommandHandlerBase<BaseRequest, BaseResponse>> logger) : base(logger)
        {
        }


        public override void SetDomainContext(BaseRequest request)
        {
            
        }

        public override Task<BaseResponse> VerifyIdentifiers(BaseRequest request)
        {
            return Task.FromResult<BaseResponse>(null);
        }

        public override Task<BaseResponse> Handle(BaseRequest request)
        {
            return Task.FromResult(new BaseResponse()
            {
                ResponseHeader = new Common.Message.Response.ResponseHeader()
                {
                    ResponseId = request?.ReqeustHeader?.RequestId ?? Guid.NewGuid(),
                    StatusCode = 0,
                    SubStatusCode = 0,
                    Message = $"Weather info processed successfully {DateTime.Now}"
                }
            });
        }
    }
}
