namespace BusterWood.Msmq.Patterns
{
    public enum MessageCacheAction
    {
        /// <summary>Read the value for a label</summary>
        Read = 0,
        
        /// <summary>Returns a list of cached keys in the UTF-8 encoded <see cref="Message.Body"/>, one key per line</summary>
        ListKeys = 1,
        
        /// <summary>Remove a key from the cache (invalidate)</summary>
        Remove = 5,

        /// <summary>Remove all keys and values from the cache</summary>
        Clear = 9
    }
}