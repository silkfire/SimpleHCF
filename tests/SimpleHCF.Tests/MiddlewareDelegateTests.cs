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
        private const string _endpointUri = "/hello/world";

        private readonly WireMockServer _server;

        public MiddlewareDelegateTests()
        {
            _server = WireMockServer.Start();
            _server.Given(Request.Create().WithPath(_endpointUri).UsingAnyMethod())
                   .RespondWith(
                       Response.Create()
                          .WithStatusCode(HttpStatusCode.OK)
                          .WithHeader("Content-Type", "text/plain")
                          .WithBody("Hello world!"));
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
            await SingleMiddlewareHandler($"{_server.Urls[0]}{_endpointUri}", tr => HttpClientFactoryBuilder.Create().WithMessageHandler(tr));
        }

        [Fact]
        public async Task Single_middleware_handler_should_work_from_create_with_string_base_url()
        {
            await SingleMiddlewareHandler(_endpointUri, tr => HttpClientFactoryBuilder.Create(_server.Urls[0], tr));
        }

        [Fact]
        public async Task Single_middleware_handler_should_work_from_create_with_base_url()
        {
            await SingleMiddlewareHandler(_endpointUri, tr => HttpClientFactoryBuilder.Create(new Uri(_server.Urls[0]), tr));
        }

        [Fact]
        public async Task Single_middleware_handler_should_work_from_create()
        {
            await SingleMiddlewareHandler($"{_server.Urls[0]}{_endpointUri}", tr => HttpClientFactoryBuilder.Create(tr));
        }

        private static async Task SingleMiddlewareHandler(string endpoint, Func<TrafficRecorderMessageHandler, IHttpClientFactoryBuilder> factory)
        {
            var trafficRecorderMessageHandler = new TrafficRecorderMessageHandler(A.Dummy<IList<string>>());

            var client = factory(trafficRecorderMessageHandler).Build().CreateClient();

            var _ = await client.GetAsync(endpoint);

            Assert.Single(trafficRecorderMessageHandler.Traffic);
            Assert.Equal(HttpStatusCode.OK, trafficRecorderMessageHandler.Traffic[0].Item2.StatusCode);
        }

        [Fact]
        public async Task Multiple_middleware_handlers_should_work()
        {
            await MultipleMiddlewareHandlers($"{_server.Urls[0]}{_endpointUri}", (tr, e) => HttpClientFactoryBuilder.Create().WithMessageHandler(e).WithMessageHandler(tr), new[] { nameof(TrafficRecorderMessageHandler), nameof(EventMessageHandler) });
        }

        [Fact]
        public async Task Multiple_middleware_handlers_should_work_from_create()
        {
            await MultipleMiddlewareHandlers($"{_server.Urls[0]}{_endpointUri}", (tr, e) => HttpClientFactoryBuilder.Create(e, tr), new[] { nameof(TrafficRecorderMessageHandler), nameof(EventMessageHandler) });
        }

        [Fact]
        public async Task Multiple_middleware_handlers_should_work_from_create_with_string_base_url()
        {
            await MultipleMiddlewareHandlers(_endpointUri, (tr, e) => HttpClientFactoryBuilder.Create(_server.Urls[0], tr, e), new[] { nameof(EventMessageHandler), nameof(TrafficRecorderMessageHandler) });
        }

        [Fact]
        public async Task Multiple_middleware_handlers_should_work_from_create_with_base_url()
        {
            await MultipleMiddlewareHandlers(_endpointUri, (tr, e) => HttpClientFactoryBuilder.Create(new Uri(_server.Urls[0]), tr, e), new[] { nameof(EventMessageHandler), nameof(TrafficRecorderMessageHandler) });
        }

        [Fact]
        public async Task Multiple_middleware_handlers_with_reverse_order_should_work()
        {
            await MultipleMiddlewareHandlers($"{_server.Urls[0]}{_endpointUri}", (tr, e) => HttpClientFactoryBuilder.Create().WithMessageHandler(tr).WithMessageHandler(e), new[] { nameof(EventMessageHandler), nameof(TrafficRecorderMessageHandler) });
        }

        [Fact]
        public async Task Multiple_middleware_handlers_with_reverse_order_should_work_from_create()
        {
            await MultipleMiddlewareHandlers($"{_server.Urls[0]}{_endpointUri}", (tr, e) => HttpClientFactoryBuilder.Create(tr, e), new[] { nameof(EventMessageHandler), nameof(TrafficRecorderMessageHandler) });
        }

        private static async Task MultipleMiddlewareHandlers(string endpoint, Func<TrafficRecorderMessageHandler, EventMessageHandler, IHttpClientFactoryBuilder> factory, IEnumerable<string> expectedVisitedMiddleware)
        {
            var actuallyVisitedMiddleware = new List<string>();

            var trafficRecorderMessageHandler = new TrafficRecorderMessageHandler(actuallyVisitedMiddleware);
            var eventMessageHandler = new EventMessageHandler(actuallyVisitedMiddleware);

            var client = factory(trafficRecorderMessageHandler, eventMessageHandler).Build().CreateClient();

            var raisedEvent = await Assert.RaisesAsync<EventMessageHandler.RequestEventArgs>(
                h => eventMessageHandler.Request += h,
                h => eventMessageHandler.Request -= h,
                () => client.GetAsync(endpoint));

            Assert.True(raisedEvent.Arguments.Request.Headers.Contains("foobar"));
            Assert.Equal("foobar", raisedEvent.Arguments.Request.Headers.GetValues("foobar").FirstOrDefault());
            Assert.Single(trafficRecorderMessageHandler.Traffic);

            Assert.Equal(HttpStatusCode.OK, trafficRecorderMessageHandler.Traffic[0].Item2.StatusCode);
            Assert.Equal(expectedVisitedMiddleware, actuallyVisitedMiddleware);
        }
    }
}
