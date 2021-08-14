namespace SimpleHCF
{
    using System.Net.Http;

    public interface IHttpClientFactory
    {
        /// <summary>
        /// Instantiates the pre-configured HTTP client.
        /// </summary>
        HttpClient CreateClient();
    }
}
