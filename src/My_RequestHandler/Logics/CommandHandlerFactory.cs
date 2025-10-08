using My_RequestHandler.Logics.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace My_RequestHandler.Logics
{

    public interface ICommandHandlerFactory
    {
        ICommandHandler CreateCommandHandler(string commandType);
    }
    public class CommandHandlerFactory : ICommandHandlerFactory
    {
        private ILogger _logger;

        public CommandHandlerFactory(ILogger<CommandHandlerFactory> logger, Func<string, ICommandHandler> factoryDelegate)
        {
            _logger = logger;
            _factory = factoryDelegate;
        }

        public ICommandHandler CreateCommandHandler(string commandType)
        {
            try
            {
                return _factory(commandType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unknown command type: {commandType}");
                throw new Exception($"Unknown command type: {commandType}");
            }
        }

        private readonly Func<string, ICommandHandler> _factory;

    }
}
