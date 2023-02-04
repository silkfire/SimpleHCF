namespace SimpleHCF
{
    using System.Net.Http;

    internal class SimpleHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClientFactoryBuilder _httpClientFactoryBuilder;

        public SimpleHttpClientFactory(HttpClientFactoryBuilder httpClientFactoryBuilder)
        {
            _httpClientFactoryBuilder = httpClientFactoryBuilder;
        }

        public HttpClient CreateClient()
        {
            return _httpClientFactoryBuilder.CreateClient();
        }
    }
}
