namespace SimpleHCF
{
    using Polly;

    using System.Net.Http;

    /// <summary>
    /// Extension methods for a HTTP message.
    /// </summary>
    public static class HttpMessageExtensions
    {
        private static readonly HttpRequestOptionsKey<Context> PolicyExecutionContextKey = new("PolicyExecutionContext");

        /// <summary>
        /// Sets a Polly policy context on an HTTP request message.
        /// <para>Do not re-use an instance of <see cref="Context"/> across more than one execution.</para>
        /// </summary>
        /// <param name="request">The HTTP request message to set the <see cref="Context"/> on.</param>
        /// <param name="policyContext">The Polly policy context to set on the request message.</param>
        public static void SetPolicyExecutionContext(this HttpRequestMessage request, Context policyContext)
        {
            request.Options.Set(PolicyExecutionContextKey, policyContext);
        }

        /// <summary>
        /// Gets a Polly policy context from an HTTP request message, if previously set.
        /// <para>Do not re-use an instance of <see cref="Context"/> across more than one execution.</para>
        /// </summary>
        /// <param name="request">The HTTP request message to get the <see cref="Context"/> from.</param>
        /// <param name="policyExecutionContext">If found, will reference the Polly policy context that has been previously set on the provided HTTP request message.</param>
        public static bool TryGetPolicyExecutionContext(this HttpRequestMessage request, out Context policyExecutionContext)
        {
            return request.Options.TryGetValue(PolicyExecutionContextKey, out policyExecutionContext);
        }
    }
}
