namespace SimpleHCF.Tests
{
    using FakeItEasy;
    using MessageHandlers;

    using Polly;
    using Polly.Timeout;
    using WireMock.RequestBuilders;
    using WireMock.ResponseBuilders;
    using WireMock.Server;
    using Xunit;

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;

    public class MiddlewareAndPolicyTests
    {
        private const string EndpointUri = "/hello/world";
        private const string EndpointUriTimeout = "/timeout";
        private const string HttpContentValue = "Hello world!";

        private readonly WireMockServer _server;

        public MiddlewareAndPolicyTests()
        {
            _server = WireMockServer.Start();

            _server
                .Given(Request.Create()
                    .WithPath(EndpointUri)
                    .UsingGet())
                .InScenario("Timeout-then-resolved")
                .WillSetStateTo("Transient issue resolved")
                .RespondWith(Response.Create()
                    .WithStatusCode(HttpStatusCode.RequestTimeout));

            _server
                .Given(Request.Create()
                    .WithPath(EndpointUri)
                    .UsingGet())
                .InScenario("Timeout-then-resolved")
                .WhenStateIs("Transient issue resolved")
                .WillSetStateTo("All ok")
                .RespondWith(Response.Create()
                    .WithStatusCode(HttpStatusCode.OK)
                    .WithHeader("Content-Type", "text/plain")
                    .WithBody(HttpContentValue));

            _server
                .Given(Request.Create()
                    .WithPath(EndpointUriTimeout)
                    .UsingGet())
                .RespondWith(Response.Create()
                    .WithStatusCode(HttpStatusCode.RequestTimeout));
        }

        [Fact]
        public async Task Client_with_retry_and_timeout_policy_should_properly_apply_policies_with_single_middleware()
        {
            var actuallyVisitedMiddleware = new List<string>();

            var requestEventHandler = A.Fake<EventHandler<EventMessageHandler.RequestEventArgs>>();

            var eventMessageHandler = new EventMessageHandler(actuallyVisitedMiddleware);
            eventMessageHandler.Request += requestEventHandler;

            //timeout after 2 seconds, then retry
            var clientWithRetry = HttpClientFactoryBuilder.Create()
                                                          .WithPolicy(
                                                                      Policy<HttpResponseMessage>
                                                                          .Handle<HttpRequestException>()
                                                                          .OrResult(result => result.StatusCode is >= HttpStatusCode.InternalServerError or HttpStatusCode.RequestTimeout)
                                                                          .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(1)))
                                                          .WithPolicy(
                                                                      Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(4), TimeoutStrategy.Optimistic))
                                                          .WithMessageHandler(eventMessageHandler)
                                                          .Build()
                                                          .CreateClient();

            var response = await clientWithRetry.GetAsync($"{_server.Urls[0]}{EndpointUriTimeout}");

            A.CallTo(() => requestEventHandler.Invoke(eventMessageHandler, A<EventMessageHandler.RequestEventArgs>.That.IsNotNull())).MustHaveHappenedOnceExactly();

            Assert.Equal(4, _server.LogEntries.Count());
            Assert.Equal(new[] { nameof(EventMessageHandler) }, actuallyVisitedMiddleware);
            Assert.Equal(HttpStatusCode.RequestTimeout, response.StatusCode);
        }


        [Fact]
        public async Task Client_with_retry_and_timeout_policy_should_properly_apply_policies_with_multiple_middlewares()
        {
            var actuallyVisitedMiddleware = new List<string>();

            var requestEventHandler = A.Fake<EventHandler<EventMessageHandler.RequestEventArgs>>();

            var eventMessageHandler = new EventMessageHandler(actuallyVisitedMiddleware);
            eventMessageHandler.Request += requestEventHandler;

            var trafficRecorderMessageHandler = new TrafficRecorderMessageHandler(actuallyVisitedMiddleware);

            var clientWithRetry = HttpClientFactoryBuilder.Create()
                                                          .WithPolicy(
                                                                      Policy<HttpResponseMessage>
                                                                          .Handle<HttpRequestException>()
                                                                          .OrResult(result => result.StatusCode is >= HttpStatusCode.InternalServerError or HttpStatusCode.RequestTimeout)
                                                                          .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(1)))
                                                          .WithPolicy(
                                                                      Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(4), TimeoutStrategy.Optimistic))
                                                          .WithMessageHandler(eventMessageHandler)
                                                          .WithMessageHandler(trafficRecorderMessageHandler)
                                                          .Build()
                                                          .CreateClient();

            var response = await clientWithRetry.GetAsync($"{_server.Urls[0]}{EndpointUriTimeout}");

            A.CallTo(() => requestEventHandler.Invoke(eventMessageHandler, A<EventMessageHandler.RequestEventArgs>.That.Matches(e => e.Request.Headers.Single(h => h.Key == TrafficRecorderMessageHandler.HeaderName).Value.FirstOrDefault() == TrafficRecorderMessageHandler.HeaderValue))).MustHaveHappenedOnceExactly();

            Assert.Single(trafficRecorderMessageHandler.Traffic);
            Assert.Equal(4, _server.LogEntries.Count());
            Assert.Equal(new[] { nameof(TrafficRecorderMessageHandler), nameof(EventMessageHandler) }, actuallyVisitedMiddleware);
            Assert.Equal(HttpStatusCode.RequestTimeout, response.StatusCode);
        }


        [Fact]
        public async Task Retry_policy_should_work_with_multiple_middleware()
        {
            var actuallyVisitedMiddleware = new List<string>();

            var requestEventHandler = A.Fake<EventHandler<EventMessageHandler.RequestEventArgs>>();

            var eventMessageHandler = new EventMessageHandler(actuallyVisitedMiddleware);
            eventMessageHandler.Request += requestEventHandler;

            var trafficRecorderMessageHandler = new TrafficRecorderMessageHandler(actuallyVisitedMiddleware);

            var clientWithRetry = HttpClientFactoryBuilder.Create()
                                                          .WithPolicy(
                                                                      Policy<HttpResponseMessage>
                                                                          .Handle<HttpRequestException>()
                                                                          .OrResult(result => result.StatusCode is >= HttpStatusCode.InternalServerError or HttpStatusCode.RequestTimeout)
                                                                          .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(1)))
                                                          .WithMessageHandler(eventMessageHandler)
                                                          .WithMessageHandler(trafficRecorderMessageHandler)
                                                          .Build()
                                                          .CreateClient();

            var response = await clientWithRetry.GetAsync($"{_server.Urls[0]}{EndpointUri}");
            var eventManager = new string('c', 78);
            A.CallTo(() => requestEventHandler.Invoke(eventMessageHandler, A<EventMessageHandler.RequestEventArgs>.That.Matches(e => e.Request.Headers.Single(h => h.Key == TrafficRecorderMessageHandler.HeaderName).Value.FirstOrDefault() == TrafficRecorderMessageHandler.HeaderValue))).MustHaveHappenedOnceExactly();

            Assert.Single(trafficRecorderMessageHandler.Traffic);

            Assert.Equal(2, _server.LogEntries.Count());
            Assert.Single(_server.LogEntries, le => (HttpStatusCode)le.ResponseMessage.StatusCode == HttpStatusCode.OK);
            Assert.Single(_server.LogEntries, le => (HttpStatusCode)le.ResponseMessage.StatusCode == HttpStatusCode.RequestTimeout);
            Assert.Equal(new[] { nameof(TrafficRecorderMessageHandler), nameof(EventMessageHandler) }, actuallyVisitedMiddleware);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(HttpContentValue, await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Retry_policy_should_work_with_single_middleware()
        {
            var actuallyVisitedMiddleware = new List<string>();

            var requestEventHandler = A.Fake<EventHandler<EventMessageHandler.RequestEventArgs>>();

            var eventMessageHandler = new EventMessageHandler(actuallyVisitedMiddleware);
            eventMessageHandler.Request += requestEventHandler;

            var clientWithRetry = HttpClientFactoryBuilder.Create()
                                                          .WithPolicy(
                                                                      Policy<HttpResponseMessage>
                                                                          .Handle<HttpRequestException>()
                                                                          .OrResult(result => result.StatusCode is >= HttpStatusCode.InternalServerError or HttpStatusCode.RequestTimeout)
                                                                          .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(1)))
                                                          .WithMessageHandler(eventMessageHandler)
                                                          .Build()
                                                          .CreateClient();

            var response = await clientWithRetry.GetAsync($"{_server.Urls[0]}{EndpointUri}");

            A.CallTo(() => requestEventHandler.Invoke(eventMessageHandler, A<EventMessageHandler.RequestEventArgs>.That.IsNotNull())).MustHaveHappenedOnceExactly();

            Assert.Equal(2, _server.LogEntries.Count());
            Assert.Single(_server.LogEntries, le => (HttpStatusCode)le.ResponseMessage.StatusCode == HttpStatusCode.OK);
            Assert.Single(_server.LogEntries, le => (HttpStatusCode)le.ResponseMessage.StatusCode == HttpStatusCode.RequestTimeout);
            Assert.Equal(new[] { nameof(EventMessageHandler) }, actuallyVisitedMiddleware);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(HttpContentValue, await response.Content.ReadAsStringAsync());
        }
    }
}
