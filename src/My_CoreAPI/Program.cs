

using Microsoft.Extensions.DependencyInjection;
using My_CoreAPI.Models.Settings;
using My_CoreAPI.Providers;
using My_CoreAPI.RabbitMQ;
using NLog;
using NLog.Extensions.Logging;
using NLog.Web;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;


//config
var _logger = LogManager.Setup().LoadConfigurationFromFile("NLog.config").GetCurrentClassLogger();

_logger.Debug("init main");

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Add services to the container.
    // Configure NLog
    builder.Services.AddLogging(logging =>
    {
        logging.ClearProviders();
        logging.SetMinimumLevel(LogLevel.Trace);
    });

    //add config
    // Build envName
    var envName = Environment.GetEnvironmentVariable("ENVIRONMENT") ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"); // ENVIRONMENT vs ASPNETCORE_ENVIRONEMNT
    Console.WriteLine($"envName = {envName}");
    var config = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile($"appsettings.{envName}.json", optional: false, reloadOnChange: true)
        .AddEnvironmentVariables($"{AppDomain.CurrentDomain.FriendlyName}:")
        .AddJsonFile("/config/app-config.json", optional: true, reloadOnChange: true)
        .Build();
    builder.Services.Configure<CoreAPISettings>(builder.Configuration);
    builder.Services.AddSingleton<CoreAPISettings>();
    builder.Services.Configure<RabbitMQSettings>(builder.Configuration.GetSection("RabbitMQSettings"));
    builder.Services.AddSingleton<RabbitMQSettings>();
    //builder.Services.AddSingleton<RabbitMQSettings>(a=> a.GetService< CoreAPISettings>()!.RabbitMqSettings);

    //add services
    builder.Services.AddSingleton<ILoggerProvider, NLogLoggerProvider>();
    builder.Services.AddSingleton<ICommandChannelClient, RabbitMqCommandChannelClient>();
    builder.Services.AddSingleton<IRequestHandlerService, RequestHandlerService>();

    builder.Services.AddControllers();
    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();




    var app = builder.Build();
    
    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();

    app.UseAuthorization();

    app.MapControllers();

    app.Run();


}
catch (Exception e)
{
    Console.WriteLine(e);
    throw;
}