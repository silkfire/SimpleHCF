namespace SimpleHCF
{
    using MessageHandlers;

    using Polly;

    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Security;
    using System.Runtime.CompilerServices;
    using System.Security.Cryptography.X509Certificates;

    public partial class HttpClientFactoryBuilder : IHttpClientFactoryBuilder
    {        
        private Uri _baseUrl;
        private readonly Dictionary<string, string> _defaultHeaders = new();
        private readonly List<X509Certificate2> _certificates = new();
        private readonly List<IAsyncPolicy<HttpResponseMessage>> _policies = new();
        private TimeSpan? _timeout;
        private HttpVersion? _httpVersion;
        private readonly List<DelegatingHandler> _middlewareHandlers = new();
        private SocketsHttpHandler _customPrimaryMessageHandler;
        private Action<SocketsHttpHandler> _primaryMessageHandlerConfigurator;

        internal static IReadOnlyDictionary<HttpVersion, Version> HttpVersionMapper { get; } = new ReadOnlyDictionary<HttpVersion, Version>(new Dictionary<HttpVersion, Version>
        {
            [HttpVersion.Unknown]    = System.Net.HttpVersion.Unknown,
            [HttpVersion.Version1_0] = System.Net.HttpVersion.Version10,
            [HttpVersion.Version1_1] = System.Net.HttpVersion.Version11,
            [HttpVersion.Version2_0] = System.Net.HttpVersion.Version20,
            [HttpVersion.Version3_0] = System.Net.HttpVersion.Version30,
        });

        internal HttpClientFactoryBuilder() { }

        /// <summary>
        /// Sets the base URL to use with the client.
        /// </summary>
        /// <param name="baseUrl">The base URL to set on the client.</param>
        public IHttpClientFactoryBuilder WithBaseUrl(string baseUrl)
        {
            return WithBaseUrl(new Uri(baseUrl));
        }

        /// <summary>
        /// Sets the base URL to use with the client.
        /// </summary>
        /// <param name="baseUrl">The base URL to set on the client.</param>
        public IHttpClientFactoryBuilder WithBaseUrl(Uri baseUrl)
        {
            _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));

            return this;
        }

        /// <summary>
        /// Adds a default header to be sent with each request.
        /// </summary>
        /// <param name="name">Name of the header.</param>
        /// <param name="value">Value of the header.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IHttpClientFactoryBuilder WithDefaultHeader(string name, string value)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (value == null) throw new ArgumentNullException(nameof(value));

            if (!_defaultHeaders.ContainsKey(name))
                _defaultHeaders.Add(name, value);

            return this;
        }

        /// <summary>
        /// Adds a collection of default headers to be sent with each request.
        /// </summary>
        /// <param name="headers">The dictionary of headers to be sent with each request.</param>
        public IHttpClientFactoryBuilder WithDefaultHeaders(IDictionary<string, string> headers)
        {
            if (headers == null) throw new ArgumentNullException(nameof(headers));

            foreach(var (key, value) in headers)
                WithDefaultHeader(key, value);

            return this;
        }

        private void WithCertificate(X509Certificate2 certificate)
        {
            if (certificate == null) throw new ArgumentNullException(nameof(certificate));
            
            _certificates.Add(certificate);
        }

        /// <summary>
        /// Configure one or more SSL certificates to use.
        /// </summary>
        /// <param name="certificate">One or more certificates to use.</param>
        public IHttpClientFactoryBuilder WithCertificate(params X509Certificate2[] certificate)
        {
            return WithCertificates(certificate);
        }

        /// <summary>
        /// Configure one or more SSL certificates to use.
        /// </summary>
        /// <param name="certificates">The collection containing certificates to use.</param>
        public IHttpClientFactoryBuilder WithCertificates(IEnumerable<X509Certificate2> certificates)
        {
            if (certificates == null) throw new ArgumentNullException(nameof(certificates));

            var certificateList = certificates.ToList();

            if (!certificateList.Any()) throw new ArgumentException("The provided collection must contain at least one certificate", nameof(certificates));

            foreach (var certificate in certificateList)
                WithCertificate(certificate);

            return this;
        }

        private void WithPolicy(IAsyncPolicy<HttpResponseMessage> policy)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));

            _policies.Add(policy);
        }

        /// <summary>
        /// Adds one or more Polly policies to the error policy processing pipeline.
        /// </summary>
        /// <param name="policy">One or more policies to add.</param>
        /// <remarks>Policies will be evaluated in the order of their configuration.</remarks>
        public IHttpClientFactoryBuilder WithPolicy(params IAsyncPolicy<HttpResponseMessage>[] policy)
        {
            return WithPolicies(policy);
        }

        /// <summary>
        /// Adds multiple Polly policies to the error policy processing pipeline.
        /// </summary>
        /// <param name="policies">The collection containing policies to add.</param>
        /// <remarks>Policies will be evaluated in the order of their configuration.</remarks>
        public IHttpClientFactoryBuilder WithPolicies(IEnumerable<IAsyncPolicy<HttpResponseMessage>> policies)
        {
            if (policies == null) throw new ArgumentNullException(nameof(policies));

            var policyList = policies.ToList();

            if (!policyList.Any()) throw new ArgumentException("The provided collection must contain at least one policy", nameof(policies));

            foreach (var policy in policyList)
                WithPolicy(policy);

            return this;
        }

        /// <summary>
        /// Sets the timespan to wait before requests sent by the constructed client time out.
        /// </summary>
        /// <param name="timeout">The request timeout value.</param>
        public IHttpClientFactoryBuilder WithRequestTimeout(in TimeSpan timeout)
        {
            _timeout = timeout;

            return this;
        }

        /// <summary>
        /// Sets the HTTP version used when performing subsequent requests with the constructed client.
        /// </summary>
        /// <param name="version">The HTTP version to set on the constructed client.</param>
        public IHttpClientFactoryBuilder WithHttpVersion(in HttpVersion version)
        {
            _httpVersion = version;
            
            return this;
        }

        private IHttpClientFactoryBuilder WithMessageHandler(DelegatingHandler handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            if (_middlewareHandlers.Count > 0) _middlewareHandlers.Last().InnerHandler = handler;

            _middlewareHandlers.Add(handler);

            return this;
        }

        /// <summary>
        /// Adds one or more additional message handlers to the processing pipeline.
        /// </summary>
        /// <param name="handler">One or more message handlers to add.</param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="handler"/> is <see langword="null"/>.</exception>
        public IHttpClientFactoryBuilder WithMessageHandler(params DelegatingHandler[] handler)
        {
            return WithMessageHandlers(handler);
        }

        /// <summary>
        /// Adds additional message handlers to the processing pipeline.
        /// </summary>
        /// <param name="handlers">The collection containing the message handlers to add.</param>
        /// <exception cref="T:System.ArgumentNullException">One of items in <paramref name="handlers"/> is <see langword="null"/>.</exception>
        public IHttpClientFactoryBuilder WithMessageHandlers(IEnumerable<DelegatingHandler> handlers)
        {
            if (handlers == null) throw new ArgumentNullException(nameof(handlers));

            var handlerList = handlers.ToList();

            if (!handlerList.Any()) throw new ArgumentException("The provided collection must contain at least one message handler", nameof(handlers));

            foreach (var handler in handlerList)
                WithMessageHandler(handler);

            return this;
        }

        /// <summary>
        /// Adds an exception handler to transform any thrown <see cref="HttpRequestException"/>s into more user friendly ones (or to add more details).
        /// </summary>
        /// <param name="exceptionHandlingPredicate">If the provided delegate evaluates to <see langword="false"/> when executed, the <see cref="HttpRequestException"/> is thrown as-is.</param>
        /// <param name="exceptionHandler">A delegate to transform the active exception. If it returns <see langword="null"/>, the exception will not be thrown.</param>
        /// <param name="requestExceptionEventHandler">An event handler that is called when an <see cref="HttpRequestException"/> is thrown.</param>
        /// <param name="transformedRequestExceptionEventHandler">An event handler that is called when an <see cref="HttpRequestException"/> has been successfully transformed into an <see cref="Exception"/>.</param>
        /// <remarks>This adds a call to <see cref="HttpResponseMessage.EnsureSuccessStatusCode"/>, thus ensuring that <see cref="HttpRequestException"/> will get thrown on a non-success response.</remarks>
        public IHttpClientFactoryBuilder WithMessageExceptionHandler(Func<HttpRequestException, bool> exceptionHandlingPredicate,
                                                                     Func<HttpRequestException, Exception> exceptionHandler,
                                                                     EventHandler<HttpRequestException> requestExceptionEventHandler = null,
                                                                     EventHandler<Exception> transformedRequestExceptionEventHandler = null) => WithMessageHandler(new ExceptionTranslatorRequestMiddleware(exceptionHandlingPredicate, exceptionHandler, requestExceptionEventHandler, transformedRequestExceptionEventHandler));

        /// <summary>
        /// Substitutes the default primary message handler with the specified one.
        /// </summary>
        /// <param name="defaultPrimaryMessageHandler">The message handler to substitute the default primary message handler with.</param>
        public IHttpClientFactoryBuilder WithPrimaryMessageHandler(SocketsHttpHandler defaultPrimaryMessageHandler)
        {
            _customPrimaryMessageHandler = defaultPrimaryMessageHandler ?? throw new ArgumentNullException(nameof(defaultPrimaryMessageHandler));

            return this;
        }

        /// <summary>
        /// Configures the primary message handler before the client is instantiated.
        /// </summary>
        /// <param name="configurator">A delegate to configure the primary message handler before the client is instantiated.</param>
        public IHttpClientFactoryBuilder WithPrimaryMessageHandlerConfigurator(Action<SocketsHttpHandler> configurator)
        {
            _primaryMessageHandlerConfigurator = configurator ?? throw new ArgumentNullException(nameof(configurator));

            return this;
        }

        /// <summary>
        /// Initializes a HTTP client factory with the given configuration.
        /// </summary>
        public IHttpClientFactory Build()
        {
            return new SimpleHttpClientFactory(this);
        }

        /// <summary>
        /// Instantiates the pre-configured HTTP client.
        /// </summary>
        internal HttpClient CreateClient()
        {
            var primaryMessageHandler = _customPrimaryMessageHandler ?? new SocketsHttpHandler();
            InitializePrimaryMessageHandler(primaryMessageHandler, out var rootPolicyHandler);

            var client = ConstructClientWithMiddleware(primaryMessageHandler, rootPolicyHandler);

            return client;
        }

        private void InitializePrimaryMessageHandler(SocketsHttpHandler primaryMessageHandler, out PollyMessageMiddleware rootPolicyHandler)
        {
            rootPolicyHandler = null;

            primaryMessageHandler.MaxConnectionsPerServer = Constants.MaxConnectionsPerServer;
            primaryMessageHandler.PooledConnectionIdleTimeout = Constants.ConnectionLifetime;
            primaryMessageHandler.PooledConnectionLifetime = Constants.ConnectionLifetime;

            if (_certificates.Count > 0)
            {
                primaryMessageHandler.SslOptions = new SslClientAuthenticationOptions
                {
                    ClientCertificates = new X509CertificateCollection(_certificates.Cast<X509Certificate>().ToArray())
                };
            }

            foreach (var policy in _policies)
            {
                if (rootPolicyHandler == null)
                {
                    rootPolicyHandler = new PollyMessageMiddleware(policy, primaryMessageHandler);
                }
                else
                {
                    rootPolicyHandler = new PollyMessageMiddleware(policy, rootPolicyHandler);
                }
            }

            _primaryMessageHandlerConfigurator?.Invoke(primaryMessageHandler);
        }

        private HttpClient ConstructClientWithMiddleware<TPrimaryMessageHandler>(TPrimaryMessageHandler primaryMessageHandler, PollyMessageMiddleware rootPolicyHandler)
            where TPrimaryMessageHandler : HttpMessageHandler
        {
            var client = CreateClientInternal(primaryMessageHandler, rootPolicyHandler, _middlewareHandlers.LastOrDefault());

            InitializeDefaultHeadersIfNeeded();

            if (_timeout.HasValue) client.Timeout = _timeout.Value;
            if (_httpVersion.HasValue) client.DefaultRequestVersion = HttpVersionMapper[_httpVersion.Value];

            return client;


            void InitializeDefaultHeadersIfNeeded()
            {
                foreach (var (key, value) in _defaultHeaders)
                {
                    if (!client.DefaultRequestHeaders.Contains(key)) client.DefaultRequestHeaders.Add(key, value);
                }
            }
        }

        private HttpClient CreateClientInternal<TPrimaryMessageHandler>(TPrimaryMessageHandler primaryMessageHandler, PollyMessageMiddleware rootPolicyHandler, DelegatingHandler lastMiddleware)
            where TPrimaryMessageHandler : HttpMessageHandler
        {
            HttpClient createdClient;
            if (rootPolicyHandler != null)
                createdClient = InitializeClientWithPoliciesAndMiddleware();
            else if (_middlewareHandlers.Count > 0)
                createdClient = InitializeClientOnlyWithMiddleware();
            else
                createdClient = new HttpClient(primaryMessageHandler, false);

            if (_baseUrl != null)
                createdClient.BaseAddress = _baseUrl;

            return createdClient;


            HttpClient InitializeClientWithPoliciesAndMiddleware()
            {
                HttpClient client;

                if (_middlewareHandlers.Count > 0)
                {
                    lastMiddleware.InnerHandler = rootPolicyHandler;
                    client = new HttpClient(_middlewareHandlers.First(), false);
                }
                else
                    client = new HttpClient(rootPolicyHandler, false);

                return client;
            }

            HttpClient InitializeClientOnlyWithMiddleware()
            {
                lastMiddleware.InnerHandler = primaryMessageHandler;
                var client = new HttpClient(_middlewareHandlers.First(), false);

                return client;
            }
        }
    }
}
