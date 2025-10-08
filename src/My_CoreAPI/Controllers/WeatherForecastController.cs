using Common.Message.Request;
using Microsoft.AspNetCore.Mvc;
using My_CoreAPI.Providers;

namespace My_CoreAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private readonly ILogger<WeatherForecastController> _logger;
        private readonly IRequestHandlerService _requestHandlerService;

        public WeatherForecastController(ILogger<WeatherForecastController> logger, IRequestHandlerService requestHandlerService)
        {
            _logger = logger;
            _requestHandlerService = requestHandlerService;
        }

        [HttpGet(Name = "GetWeatherForecast")]
        public IEnumerable<WeatherForecast> Get()
        {
            _logger.LogInformation("Generating weather forecast data.");

            BaseRequest r = new BaseRequest()
            {
                ReqeustHeader = new ReqeustHeader()
                {
                    RequestId = Guid.NewGuid(),
                    Options = new Dictionary<string, string>
                    {
                        { "Client", "MyCoreAPI" },
                        { "RequestType", "GetWeatherInfo" }
                    }
                }
            };

            var response =_requestHandlerService.GetWeatherInfo(r).GetAwaiter().GetResult();

            _logger.LogInformation("Weather forecast data generated successfully.");

            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = //Summaries[Random.Shared.Next(Summaries.Length)]
                response.ResponseHeader.Message
            })
            .ToArray();
        }
    }
}
