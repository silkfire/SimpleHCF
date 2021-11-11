namespace SimpleHCF
{
    using System.Net.Http;

    /// <summary>
    /// An abstraction for a component that can create <see cref="HttpClient"/> instances with custom configuration.
    /// </summary>
    public interface IHttpClientFactory
    {
        /// <summary>
        /// Instantiates the pre-configured HTTP client.
        /// </summary>
        HttpClient CreateClient();
    }
}
