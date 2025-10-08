using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Common.Message.Response
{
    public class ResponseHeader
    {

        //[JsonProperty("responseId")]
        public Guid ResponseId { get; set; }

        //[JsonProperty("statusCode")]
        public int StatusCode { get; set; }

        //[JsonProperty("subStatusCode")]
        public int SubStatusCode { get; set; }

        //[JsonProperty("message")]
        public string Message { get; set; }

        //[JsonProperty("details")]
        public string Details { get; set; }

        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }
}
