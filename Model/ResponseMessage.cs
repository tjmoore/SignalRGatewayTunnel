using MessagePack;
using System.Net;

namespace Model
{
    [MessagePackObject]
    public class ResponseMessage
    {
        [Key(0)]
        public HttpStatusCode StatusCode { get; set; }

        [Key(1)]
        public byte[]? Content { get; set; }

        [Key(2)]
        public List<KeyValuePair<string, IEnumerable<string?>>> Headers { get; set; } = [];

        [Key(3)]
        public List<KeyValuePair<string, IEnumerable<string?>>> ContentHeaders { get; set; } = [];
    }
}
