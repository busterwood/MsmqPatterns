namespace BusterWood.Msmq
{
    /// <summary>
    /// The type of the <see cref="Message.Body"/>
    /// </summary>
    public enum BodyType
    {
        /// <summary>Default, same as <see cref="ByteArray"/></summary>
        None = 0,

        /// <summary>The body is an array of bytes</summary>
        ByteArray = MessageProperties.VT_VECTOR | MessageProperties.VT_I1,

        /// <summary>The body is a null terminated ASCII string</summary>
        AnsiString = MessageProperties.VT_LPSTR,

        /// <summary>The body is a null terminated UTF-16 string</summary>
        UnicodeString = MessageProperties.VT_LPWSTR,
    }
}
