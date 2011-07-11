namespace Sample.YakRiak
{
    /// <summary>
    /// Dumb container of data to make it easy to deserialise stuff
    /// from JSON using JsonConverter.
    /// </summary>
    public class YakMessage
    {
        public string key;
        public string message;
        public string name;
        public string gravatar;
        public ulong timestamp;
    }
}
