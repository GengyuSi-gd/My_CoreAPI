using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace My_RequestHandler.Logics.Handlers
{
    public interface ICommandHandler
    {
        Task<byte[]> HandleAsync(byte[] requestBuffer);

    }
}
