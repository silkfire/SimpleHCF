namespace SimpleHCF
{
    using System;

    internal static class Constants
    {
        public static readonly TimeSpan ConnectionLifeTime = TimeSpan.FromMinutes(1);
        public const int MaxConnectionsPerServer = 20;
    }
}
