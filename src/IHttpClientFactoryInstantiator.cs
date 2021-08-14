namespace SimpleHCF
{
    using System.Net.Http;

    internal interface IHttpClientFactoryInstantiator : IHttpClientFactoryBuilder
    {
        /// <summary>
        /// Instantiates the pre-configured HTTP client.
        /// </summary>
        HttpClient CreateClient();
    }
}
