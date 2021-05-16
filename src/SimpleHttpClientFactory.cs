namespace SimpleHCF
{
    using System.Net.Http;

    internal class SimpleHttpClientFactory : IHttpClientFactory
    {
        private readonly IHttpClientFactoryInstantiator _httpClientFactoryInstantiator;

        public SimpleHttpClientFactory(IHttpClientFactoryInstantiator httpClientFactoryInstantiator)
        {
            _httpClientFactoryInstantiator = httpClientFactoryInstantiator;
        }

        public HttpClient CreateClient()
        {
            return _httpClientFactoryInstantiator.CreateClient();
        }
    }
}
