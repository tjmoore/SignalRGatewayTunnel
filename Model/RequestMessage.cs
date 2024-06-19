using MessagePack;

namespace Model
{
    [MessagePackObject]
    public class RequestMessage
    {
        [Key(0)]
        public Uri? RequestUri { get; set; }

        [Key(1)]
        public string Method { get; set; } = "GET";

        [Key(2)]
        public byte[]? Content { get; set; }

        [Key(3)]
        public List<KeyValuePair<string, IEnumerable<string?>>> Headers { get; set; } = [];

        [Key(4)]
        public List<KeyValuePair<string, IEnumerable<string?>>> ContentHeaders { get; set; } = [];
    }
}
