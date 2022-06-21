namespace SimpleHCF
{
    /// <summary>
    /// Defines the HTTP version number.
    /// </summary>
    public enum HttpVersion
    {
        /// <summary>
        /// Defines an unknown HTTP version.
        /// </summary>
        Unknown,

        /// <summary>
        /// Defines HTTP/1.0.
        /// </summary>
        Version1_0,

        /// <summary>
        /// Defines HTTP/1.1.
        /// </summary>
        Version1_1,

        /// <summary>
        /// Defines HTTP/2.
        /// </summary>
        Version2_0,

        /// <summary>
        /// Defines HTTP/3.
        /// </summary>
        Version3_0,
    }
}
