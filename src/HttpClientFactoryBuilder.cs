namespace SimpleHCF
{
    using MessageHandlers;

    using Polly;

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Security;
    using System.Runtime.CompilerServices;
    using System.Security.Cryptography.X509Certificates;

    public partial class HttpClientFactoryBuilder : IHttpClientFactoryInstantiator
    {        
        private Uri _baseUrl;
        private readonly Dictionary<string, string> _defaultHeaders = new();
        private readonly List<X509Certificate2> _certificates = new();
        private readonly List<IAsyncPolicy<HttpResponseMessage>> _policies = new();
        private TimeSpan? _timeout;
        private readonly List<DelegatingHandler> _middlewareHandlers = new();
        private SocketsHttpHandler _customPrimaryMessageHandler;
        private Action<SocketsHttpHandler> _primaryMessageHandlerConfigurator;


        internal HttpClientFactoryBuilder() { }

        public IHttpClientFactoryBuilder WithBaseUrl(string baseUrl)
        {
            return WithBaseUrl(new Uri(baseUrl));
        }

        public IHttpClientFactoryBuilder WithBaseUrl(Uri baseUrl)
        {
            _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));

            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IHttpClientFactoryBuilder WithDefaultHeader(string name, string value)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (value == null) throw new ArgumentNullException(nameof(value));

            if (!_defaultHeaders.ContainsKey(name))
                _defaultHeaders.Add(name, value);

            return this;
        }

        public IHttpClientFactoryBuilder WithDefaultHeaders(IDictionary<string, string> headers)
        {
            if (headers == null) throw new ArgumentNullException(nameof(headers));

            foreach(var kvp in headers)
                WithDefaultHeader(kvp.Key, kvp.Value);

            return this;
        }

        private void WithCertificate(X509Certificate2 certificate)
        {
            if (certificate == null) throw new ArgumentNullException(nameof(certificate));
            
            _certificates.Add(certificate);
        }

        public IHttpClientFactoryBuilder WithCertificate(params X509Certificate2[] certificate)
        {
            return WithCertificates(certificate);
        }

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

        public IHttpClientFactoryBuilder WithPolicy(params IAsyncPolicy<HttpResponseMessage>[] policy)
        {
            return WithPolicies(policy);
        }

        public IHttpClientFactoryBuilder WithPolicies(IEnumerable<IAsyncPolicy<HttpResponseMessage>> policies)
        {
            if (policies == null) throw new ArgumentNullException(nameof(policies));

            var policyList = policies.ToList();

            if (!policyList.Any()) throw new ArgumentException("The provided collection must contain at least one policy", nameof(policies));

            foreach (var policy in policyList)
                WithPolicy(policy);

            return this;
        }

        public IHttpClientFactoryBuilder WithRequestTimeout(in TimeSpan timeout)
        {
            _timeout = timeout;

            return this;
        }

        private IHttpClientFactoryBuilder WithMessageHandler(DelegatingHandler handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            if (_middlewareHandlers.Count > 0) _middlewareHandlers.Last().InnerHandler = handler;

            _middlewareHandlers.Add(handler);

            return this;
        }

        public IHttpClientFactoryBuilder WithMessageHandler(params DelegatingHandler[] handler)
        {
            return WithMessageHandlers(handler);
        }

        public IHttpClientFactoryBuilder WithMessageHandlers(IEnumerable<DelegatingHandler> handlers)
        {
            if (handlers == null) throw new ArgumentNullException(nameof(handlers));

            var handlerList = handlers.ToList();

            if (!handlerList.Any()) throw new ArgumentException("The provided collection must contain at least one message handler", nameof(handlers));

            foreach (var handler in handlerList)
                WithMessageHandler(handler);

            return this;
        }

        public IHttpClientFactoryBuilder WithMessageExceptionHandler(Func<HttpRequestException, bool> exceptionHandlingPredicate,
                                                              Func<HttpRequestException, Exception> exceptionHandler,
                                                              EventHandler<HttpRequestException> requestExceptionEventHandler = null,
                                                              EventHandler<Exception> transformedRequestExceptionEventHandler = null) => WithMessageHandler(new ExceptionTranslatorRequestMiddleware(exceptionHandlingPredicate, exceptionHandler, requestExceptionEventHandler, transformedRequestExceptionEventHandler));

        public IHttpClientFactoryBuilder WithPrimaryMessageHandler(SocketsHttpHandler defaultPrimaryMessageHandler)
        {
            _customPrimaryMessageHandler = defaultPrimaryMessageHandler ?? throw new ArgumentNullException(nameof(defaultPrimaryMessageHandler));

            return this;
        }

        public IHttpClientFactoryBuilder WithPrimaryMessageHandlerConfigurator(Action<SocketsHttpHandler> configurator)
        {
            _primaryMessageHandlerConfigurator = configurator ?? throw new ArgumentNullException(nameof(configurator));

            return this;
        }

        public IHttpClientFactory Build()
        {
            return new SimpleHttpClientFactory(this);
        }

        public HttpClient CreateClient()
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
                    client = new HttpClient(_middlewareHandlers.FirstOrDefault(), false);
                }
                else
                    client = new HttpClient(rootPolicyHandler, false);

                return client;
            }

            HttpClient InitializeClientOnlyWithMiddleware()
            {
                lastMiddleware.InnerHandler = primaryMessageHandler;
                var client = new HttpClient(_middlewareHandlers.FirstOrDefault(), false);

                return client;
            }
        }
    }
}
