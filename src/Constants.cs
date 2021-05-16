namespace SimpleHCF
{
    using System;

    internal static class Constants
    {
        /// <summary>
        /// 2 minutes.
        /// </summary>
        public static readonly TimeSpan ConnectionLifetime = TimeSpan.FromMinutes(2);
        public const int MaxConnectionsPerServer = 20;
    }
}
