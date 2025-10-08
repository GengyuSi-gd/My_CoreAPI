using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Message.Response
{
    public class BaseResponse
    {
        public ResponseHeader ResponseHeader { get; set; } = new ResponseHeader();
    }
}
