namespace SimpleHCF
{
    using Polly;

    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Security.Cryptography.X509Certificates;

    /// <summary>
    /// An abstraction for a builder that can produce factories that in turn can create pre-configured <see cref="HttpClient"/> instances.
    /// </summary>
    public interface IHttpClientFactoryBuilder
    {
        /// <summary>
        /// Sets the base URL to use with the client.
        /// </summary>
        /// <param name="baseUrl">The base URL to set on the client.</param>
        IHttpClientFactoryBuilder WithBaseUrl(string baseUrl);

        /// <summary>
        /// Sets the base URL to use with the client.
        /// </summary>
        /// <param name="baseUrl">The base URL to set on the client.</param>
        IHttpClientFactoryBuilder WithBaseUrl(Uri baseUrl);

        /// <summary>
        /// Adds a default header to be sent with each request.
        /// </summary>
        /// <param name="name">Name of the header.</param>
        /// <param name="value">Value of the header.</param>
        IHttpClientFactoryBuilder WithDefaultHeader(string name, string value);

        /// <summary>
        /// Adds a collection of default headers to be sent with each request.
        /// </summary>
        /// <param name="headers">The dictionary of headers to be sent with each request.</param>
        IHttpClientFactoryBuilder WithDefaultHeaders(IDictionary<string, string> headers);

        /// <summary>
        /// Configure one or more SSL certificates to use.
        /// </summary>
        /// <param name="certificate">One or more certificates to use.</param>
        IHttpClientFactoryBuilder WithCertificate(params X509Certificate2[] certificate);

        /// <summary>
        /// Configure one or more SSL certificates to use.
        /// </summary>
        /// <param name="certificates">The collection containing certificates to use.</param>
        IHttpClientFactoryBuilder WithCertificates(IEnumerable<X509Certificate2> certificates);

        /// <summary>
        /// Adds one or more Polly policies to the error policy processing pipeline.
        /// </summary>
        /// <param name="policy">One or more policies to add.</param>
        /// <remarks>Policies will be evaluated in the order of their configuration.</remarks>
        IHttpClientFactoryBuilder WithPolicy(params IAsyncPolicy<HttpResponseMessage>[] policy);

        /// <summary>
        /// Adds multiple Polly policies to the error policy processing pipeline.
        /// </summary>
        /// <param name="policies">The collection containing policies to add.</param>
        /// <remarks>Policies will be evaluated in the order of their configuration.</remarks>
        IHttpClientFactoryBuilder WithPolicies(IEnumerable<IAsyncPolicy<HttpResponseMessage>> policies);

        /// <summary>
        /// Sets the timespan to wait before requests sent by the constructed client time out.
        /// </summary>
        /// <param name="timeout">The request timeout value.</param>
        IHttpClientFactoryBuilder WithRequestTimeout(in TimeSpan timeout);

        /// <summary>
        /// Sets the HTTP version used when performing subsequent requests with the constructed client.
        /// </summary>
        /// <param name="version">The HTTP version to set on the constructed client.</param>
        IHttpClientFactoryBuilder WithHttpVersion(in HttpVersion version);

        /// <summary>
        /// Adds one or more additional message handlers to the processing pipeline.
        /// </summary>
        /// <param name="handler">One or more message handlers to add.</param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="handler"/> is <see langword="null"/>.</exception>
        IHttpClientFactoryBuilder WithMessageHandler(params DelegatingHandler[] handler);

        /// <summary>
        /// Adds additional message handlers to the processing pipeline.
        /// </summary>
        /// <param name="handlers">The collection containing the message handlers to add.</param>
        /// <exception cref="T:System.ArgumentNullException">One of items in <paramref name="handlers"/> is <see langword="null"/>.</exception>
        IHttpClientFactoryBuilder WithMessageHandlers(IEnumerable<DelegatingHandler> handlers);

        /// <summary>
        /// Adds an exception handler to transform any thrown <see cref="HttpRequestException"/>s into more user friendly ones (or to add more details).
        /// </summary>
        /// <param name="exceptionHandlingPredicate">If the provided delegate evaluates to <see langword="false"/> when executed, the <see cref="HttpRequestException"/> is thrown as-is.</param>
        /// <param name="exceptionHandler">A delegate to transform the active exception. If it returns <see langword="null"/>, the exception will not be thrown.</param>
        /// <param name="requestExceptionEventHandler">An event handler that is called when an <see cref="HttpRequestException"/> is thrown.</param>
        /// <param name="transformedRequestExceptionEventHandler">An event handler that is called when an <see cref="HttpRequestException"/> has been successfully transformed into an <see cref="Exception"/>.</param>
        /// <remarks>This adds a call to <see cref="HttpResponseMessage.EnsureSuccessStatusCode"/>, thus ensuring that <see cref="HttpRequestException"/> will get thrown on a non-success response.</remarks>
        IHttpClientFactoryBuilder WithMessageExceptionHandler(Func<HttpRequestException, bool> exceptionHandlingPredicate, Func<HttpRequestException, Exception> exceptionHandler, EventHandler<HttpRequestException> requestExceptionEventHandler = null, EventHandler<Exception> transformedRequestExceptionEventHandler = null);

        /// <summary>
        /// Substitutes the default primary message handler with the specified one.
        /// </summary>
        /// <param name="defaultPrimaryMessageHandler">The message handler to substitute the default primary message handler with.</param>
        IHttpClientFactoryBuilder WithPrimaryMessageHandler(SocketsHttpHandler defaultPrimaryMessageHandler);

        /// <summary>
        /// Configures the primary message handler before the client is instantiated.
        /// </summary>
        /// <param name="configurator">A delegate to configure the primary message handler before the client is instantiated.</param>
        IHttpClientFactoryBuilder WithPrimaryMessageHandlerConfigurator(Action<SocketsHttpHandler> configurator);

        /// <summary>
        /// Initializes a HTTP client factory with the given configuration.
        /// </summary>
        IHttpClientFactory Build();
    }
}
