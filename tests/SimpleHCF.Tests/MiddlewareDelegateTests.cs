namespace SimpleHCF.Tests
{
    using MessageHandlers;

    using FakeItEasy;
    using WireMock.RequestBuilders;
    using WireMock.ResponseBuilders;
    using WireMock.Server;
    using Xunit;

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Reflection;
    using System.Threading.Tasks;

    public class MiddlewareDelegateTests
    {
        private const string EndpointUri = "/hello/world";
        private const string HttpContentValue = "Hello world!";

        private readonly WireMockServer _server;

        public MiddlewareDelegateTests()
        {
            _server = WireMockServer.Start();
            _server.Given(Request.Create().WithPath(EndpointUri).UsingAnyMethod())
                   .RespondWith(
                       Response.Create()
                          .WithStatusCode(HttpStatusCode.OK)
                          .WithHeader("Content-Type", "text/plain")
                          .WithBody(HttpContentValue));
        }

        [Fact]
        public void Providing_null_message_handler_params_should_throw_argumentnullexception()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => HttpClientFactoryBuilder.Create().WithMessageHandler(null));
            Assert.Equal("handlers", exception.ParamName);
        }

        [Fact]
        public void Providing_a_null_message_handler_should_throw_argumentnullexception()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => HttpClientFactoryBuilder.Create().WithMessageHandler(A.Dummy<DelegatingHandler>(), null));
            Assert.Equal("handler", exception.ParamName);
        }

        [Fact]
        public void Providing_no_arguments_to_message_handler_should_throw_argumentexception()
        {
            var exception = Assert.Throws<ArgumentException>(() => HttpClientFactoryBuilder.Create().WithMessageHandler());
            Assert.Equal("handlers", exception.ParamName);
        }

        [Fact]
        public void Providing_a_null_message_handler_collection_should_throw_argumentnullexception()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => HttpClientFactoryBuilder.Create().WithMessageHandlers(null));
            Assert.Equal("handlers", exception.ParamName);
        }

        [Fact]
        public void Providing_a_null_primary_message_handler_configurator_should_throw_argumentnullexception()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => HttpClientFactoryBuilder.Create().WithPrimaryMessageHandlerConfigurator(null));
            Assert.Equal("configurator", exception.ParamName);
        }

        [Fact]
        public void Last_primary_message_handler_configurator_should_be_invoked_on_client_creation()
        {
            var configuratorFirst  = A.Fake<Action<SocketsHttpHandler>>();
            var configuratorSecond = A.Fake<Action<SocketsHttpHandler>>();

            HttpClientFactoryBuilder.Create().WithPrimaryMessageHandlerConfigurator(configuratorFirst).WithPrimaryMessageHandlerConfigurator(configuratorSecond).Build().CreateClient();

            A.CallTo(() => configuratorFirst.Invoke(A<SocketsHttpHandler>._)).MustNotHaveHappened();
            A.CallTo(() => configuratorSecond.Invoke(A<SocketsHttpHandler>.That.IsNotNull())).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public void Providing_a_null_substitute_primary_message_handler_should_throw_argumentnullexception()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => HttpClientFactoryBuilder.Create().WithPrimaryMessageHandler(null).Build().CreateClient());
            Assert.Equal("defaultPrimaryMessageHandler", exception.ParamName);
        }

        [Fact]
        public void Should_use_provided_substitute_primary_message_handler_when_no_additional_message_handlers_provided()
        {
            var substitutePrimaryMessageHandler = new SocketsHttpHandler();
            var client = HttpClientFactoryBuilder.Create().WithPrimaryMessageHandler(substitutePrimaryMessageHandler).Build().CreateClient();

            var handlerField = typeof(HttpMessageInvoker).GetField("_handler", BindingFlags.Instance | BindingFlags.NonPublic);
            var value = handlerField.GetValue(client);

            Assert.Same(substitutePrimaryMessageHandler, value);
        }

        [Fact]
        public async Task Single_middleware_handler_should_work()
        {
            await SingleMiddlewareHandler($"{_server.Urls[0]}{EndpointUri}", trmh => HttpClientFactoryBuilder.Create().WithMessageHandler(trmh));
        }

        [Fact]
        public async Task Single_middleware_handler_should_work_from_create_with_string_base_url()
        {
            await SingleMiddlewareHandler(EndpointUri, trmh => HttpClientFactoryBuilder.Create(_server.Urls[0], trmh));
        }

        [Fact]
        public async Task Single_middleware_handler_should_work_from_create_with_base_url()
        {
            await SingleMiddlewareHandler(EndpointUri, trmh => HttpClientFactoryBuilder.Create(new Uri(_server.Urls[0]), trmh));
        }

        [Fact]
        public async Task Single_middleware_handler_should_work_from_create()
        {
            await SingleMiddlewareHandler($"{_server.Urls[0]}{EndpointUri}", trmh => HttpClientFactoryBuilder.Create(trmh));
        }

        private static async Task SingleMiddlewareHandler(string endpoint, Func<TrafficRecorderMessageHandler, IHttpClientFactoryBuilder> factory)
        {
            var trafficRecorderMessageHandler = new TrafficRecorderMessageHandler(A.Dummy<IList<string>>());

            var client = factory(trafficRecorderMessageHandler).Build().CreateClient();

            await client.GetAsync(endpoint);

            Assert.Single(trafficRecorderMessageHandler.Traffic);
            Assert.Equal(HttpStatusCode.OK, trafficRecorderMessageHandler.Traffic[0].Item2.StatusCode);
        }

        [Fact]
        public async Task Multiple_middleware_handlers_should_work()
        {
            await MultipleMiddlewareHandlers($"{_server.Urls[0]}{EndpointUri}", (emh, trmh) => HttpClientFactoryBuilder.Create().WithMessageHandler(trmh).WithMessageHandler(emh), new[] { nameof(EventMessageHandler), nameof(TrafficRecorderMessageHandler) });
        }

        [Fact]
        public async Task Multiple_middleware_handlers_should_work_from_create()
        {
            await MultipleMiddlewareHandlers($"{_server.Urls[0]}{EndpointUri}", (emh, trmh) => HttpClientFactoryBuilder.Create(trmh, emh), new[] { nameof(EventMessageHandler), nameof(TrafficRecorderMessageHandler) });
        }

        [Fact]
        public async Task Multiple_middleware_handlers_should_work_from_create_with_string_base_url()
        {
            await MultipleMiddlewareHandlers(EndpointUri, (emh, trmh) => HttpClientFactoryBuilder.Create(_server.Urls[0], emh, trmh), new[] { nameof(TrafficRecorderMessageHandler), nameof(EventMessageHandler) });
        }

        [Fact]
        public async Task Multiple_middleware_handlers_should_work_from_create_with_base_url()
        {
            await MultipleMiddlewareHandlers(EndpointUri, (emh, trmh) => HttpClientFactoryBuilder.Create(new Uri(_server.Urls[0]), emh, trmh), new[] { nameof(TrafficRecorderMessageHandler), nameof(EventMessageHandler) });
        }

        [Fact]
        public async Task Multiple_middleware_handlers_with_reverse_order_should_work()
        {
            await MultipleMiddlewareHandlers($"{_server.Urls[0]}{EndpointUri}", (emh, trmh) => HttpClientFactoryBuilder.Create().WithMessageHandler(emh).WithMessageHandler(trmh), new[] { nameof(TrafficRecorderMessageHandler), nameof(EventMessageHandler) });
        }

        [Fact]
        public async Task Multiple_middleware_handlers_with_reverse_order_should_work_from_create()
        {
            await MultipleMiddlewareHandlers($"{_server.Urls[0]}{EndpointUri}", (emh, trmh) => HttpClientFactoryBuilder.Create(emh, trmh), new[] { nameof(TrafficRecorderMessageHandler), nameof(EventMessageHandler) });
        }

        private static async Task MultipleMiddlewareHandlers(string endpoint, Func<EventMessageHandler, TrafficRecorderMessageHandler, IHttpClientFactoryBuilder> factory, IEnumerable<string> expectedVisitedMiddleware)
        {
            var actuallyVisitedMiddleware = new List<string>();

            var trafficRecorderMessageHandler = new TrafficRecorderMessageHandler(actuallyVisitedMiddleware);
            var requestEventHandler = A.Fake<EventHandler<EventMessageHandler.RequestEventArgs>>();
            var responseEventHandler = A.Fake<EventHandler<EventMessageHandler.ResponseEventArgs>>();

            var eventMessageHandler = new EventMessageHandler(actuallyVisitedMiddleware);
            eventMessageHandler.Request += requestEventHandler;
            eventMessageHandler.Response += responseEventHandler;

            var client = factory(eventMessageHandler, trafficRecorderMessageHandler).Build().CreateClient();

            await client.GetAsync(endpoint);

                  A.CallTo(() => requestEventHandler .Invoke(eventMessageHandler, A<EventMessageHandler.RequestEventArgs> .That.Matches(e => e.Request .Headers.Single(h => h.Key == TrafficRecorderMessageHandler.HeaderName).Value.FirstOrDefault() == TrafficRecorderMessageHandler.HeaderValue))).MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => responseEventHandler.Invoke(eventMessageHandler, A<EventMessageHandler.ResponseEventArgs>.That.Matches(e => e.Response.Headers.Single(h => h.Key == TrafficRecorderMessageHandler.HeaderName).Value.FirstOrDefault() == TrafficRecorderMessageHandler.HeaderValue))).MustHaveHappenedOnceExactly());

            Assert.Single(trafficRecorderMessageHandler.Traffic);

            Assert.Equal(HttpStatusCode.OK, trafficRecorderMessageHandler.Traffic[0].Item2.StatusCode);
            Assert.Equal(expectedVisitedMiddleware, actuallyVisitedMiddleware);
        }
    }
}
