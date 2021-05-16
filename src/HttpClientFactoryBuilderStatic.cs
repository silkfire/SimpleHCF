namespace SimpleHCF
{
    using System;
    using System.Net.Http;

    //credit: some code is adapted from https://github.com/NimaAra/Easy.Common/blob/master/Easy.Common/RestClient.cs
    /// <summary>
    /// Provides static methods for creating a factory that produces pre-configured <see cref="HttpClient"/> instances.
    /// </summary>
    public partial class HttpClientFactoryBuilder
    {
        /// <summary>
        /// Instantiates a new HTTP client factory builder.
        /// </summary>
        public static IHttpClientFactoryBuilder Create() => new HttpClientFactoryBuilder();

        /// <summary>
        /// Instantiates a new HTTP client factory builder with the specified additional message handlers added to its processing pipeline.
        /// </summary>
        public static IHttpClientFactoryBuilder Create(params DelegatingHandler[] handlers) => new HttpClientFactoryBuilder().WithMessageHandlers(handlers);

        /// <summary>
        /// Instantiates a new HTTP client factory builder with the specified base URL.
        /// </summary>
        public static IHttpClientFactoryBuilder Create(string baseUrl) => new HttpClientFactoryBuilder().WithBaseUrl(baseUrl);

        /// <summary>
        /// Instantiates a new HTTP client factory builder with the specified base URL and additional message handlers added to its processing pipeline.
        /// </summary>
        public static IHttpClientFactoryBuilder Create(Uri baseUrl) => new HttpClientFactoryBuilder().WithBaseUrl(baseUrl);

        /// <summary>
        /// Instantiates a new HTTP client factory builder with the specified base URL.
        /// </summary>
        public static IHttpClientFactoryBuilder Create(Uri baseUrl, params DelegatingHandler[] handlers) => new HttpClientFactoryBuilder().WithBaseUrl(baseUrl).WithMessageHandlers(handlers);

        /// <summary>
        /// Instantiates a new HTTP client factory builder with the specified base URL and additional message handlers added to its processing pipeline.
        /// </summary>
        public static IHttpClientFactoryBuilder Create(string baseUrl, params DelegatingHandler[] handlers) => new HttpClientFactoryBuilder().WithBaseUrl(baseUrl).WithMessageHandlers(handlers);
    }
}
