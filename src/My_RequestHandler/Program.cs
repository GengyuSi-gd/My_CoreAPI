using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using My_RequestHandler.HostedServices;
using My_RequestHandler.Logics;
using My_RequestHandler.Logics.Handlers;
using My_RequestHandler.Models.Settings;
using My_RequestHandler.RabbitMQ;

namespace My_RequestHandler
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");

            var envName = Environment.GetEnvironmentVariable("ENVIRONMENT") ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"); // ENVIRONMENT vs ASPNETCORE_ENVIRONEMNT
            Console.WriteLine($"envName = {envName}");
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsetting.json", optional: false, reloadOnChange: true)
                //.AddJsonFile($"appsetting.{envName}.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables($"{AppDomain.CurrentDomain.FriendlyName}:")
                //.AddJsonFile("/config/app-config.json", optional: true, reloadOnChange: true)
                .Build();


            HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
            //config
            builder.Services.Configure<RabbitMQSettings>(config.GetSection("RabbitMQSettings"));
            builder.Services.AddSingleton<RabbitMQSettings>();

            //services
            builder.Services.AddSingleton<ICommandChannelServer, RabbitMqCommandChannelServer>();
            builder.Services.AddSingleton<GetWeatherInfoHandler>();
            builder.Services.AddSingleton< Func<string, ICommandHandler>>(serviceProvider => key =>
            {
                switch (key)
                {
                    case "GetWeatherInfo":
                        return serviceProvider.GetRequiredService<GetWeatherInfoHandler>();
                    //case "AnotherCommand":
                    //    return serviceProvider.GetRequiredService<AnotherCommandHandler>();
                    default:
                        throw new KeyNotFoundException(); // or return null, up to you
                }
            });

            builder.Services.AddSingleton<ICommandHandlerFactory, CommandHandlerFactory>();
            //hosted services
            builder.Services.AddHostedService<TimerService>();
            builder.Services.AddHostedService<RequestHandlerService>();

            IHost host = builder.Build();
            host.Run();
        }
    }
}
