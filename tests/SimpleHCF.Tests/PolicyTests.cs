namespace SimpleHCF.Tests
{
    using FakeItEasy;
    using Polly;
    using Polly.Timeout;
    using WireMock.RequestBuilders;
    using WireMock.ResponseBuilders;
    using WireMock.Server;
    using Xunit;

    using System;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;

    public class PolicyTests
    {
        private const string EndpointUri = "/hello/world";
        private const string EndpointUriTimeout = "/timeout";
        private const string HttpContentValue = "Hello world!";

        private readonly WireMockServer _server;

        public PolicyTests()
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
        public void Providing_null_policy_params_should_throw_argumentnullexception()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => HttpClientFactoryBuilder.Create().WithPolicy(null));
            Assert.Equal("policies", exception.ParamName);
        }

        [Fact]
        public void Providing_a_null_policy_should_throw_argumentnullexception()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => HttpClientFactoryBuilder.Create().WithPolicy(A.Fake<IAsyncPolicy<HttpResponseMessage>>(), null));
            Assert.Equal("policy", exception.ParamName);
        }

        [Fact]
        public void Providing_no_arguments_to_policy_should_throw_argumentexception()
        {
            var exception = Assert.Throws<ArgumentException>(() => HttpClientFactoryBuilder.Create().WithPolicy());
            Assert.Equal("policies", exception.ParamName);
        }

        [Fact]
        public void Providing_a_null_policy_collection_should_throw_argumentnullexception()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => HttpClientFactoryBuilder.Create().WithPolicies(null));
            Assert.Equal("policies", exception.ParamName);
        }

        [Fact]
        public async Task Client_with_retry_and_timeout_policy_should_properly_apply_policies()
        {
            //timeout after 2 seconds, then retry
            var clientWithRetry = HttpClientFactoryBuilder.Create()
                                                          .WithPolicy(
                                                                      Policy<HttpResponseMessage>
                                                                          .Handle<HttpRequestException>()
                                                                          .OrResult(result => result.StatusCode is >= HttpStatusCode.InternalServerError or HttpStatusCode.RequestTimeout)
                                                                          .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(1)))
                                                          .WithPolicy(Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(4), TimeoutStrategy.Optimistic))
                                                          .Build()
                                                          .CreateClient();

            var responseWithTimeout = await clientWithRetry.GetAsync($"{_server.Urls[0]}{EndpointUriTimeout}");
            Assert.Equal(4, _server.LogEntries.Count());
            Assert.Equal(HttpStatusCode.RequestTimeout, responseWithTimeout.StatusCode);
        }

        [Fact]
        public async Task Client_with_retry_that_wraps_timeout_policy_should_properly_apply_policies()
        {
            var clientWithRetry = HttpClientFactoryBuilder.Create()
                                                          .WithPolicy(
                                                                      Policy.WrapAsync(
                                                                                       Policy.TimeoutAsync<HttpResponseMessage>(25),
                                                                                       Policy<HttpResponseMessage>
                                                                                           .Handle<HttpRequestException>()
                                                                                           .OrResult(result => result.StatusCode is >= HttpStatusCode.InternalServerError or HttpStatusCode.RequestTimeout)
                                                                                           .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(1))))
                                                          .Build()
                                                          .CreateClient();

            var response = await clientWithRetry.GetAsync($"{_server.Urls[0]}{EndpointUriTimeout}");
            Assert.Equal(4, _server.LogEntries.Count());
            Assert.Equal(HttpStatusCode.RequestTimeout, response.StatusCode);
        }


        [Fact]
        public async Task Client_without_retry_policy_should_fail_with_timeout()
        {
            var clientWithoutRetry = HttpClientFactoryBuilder.Create().Build().CreateClient();

            var responseWithTimeout = await clientWithoutRetry.GetAsync($"{_server.Urls[0]}{EndpointUri}");

            var logEntry = Assert.Single(_server.LogEntries);
            Assert.Equal(HttpStatusCode.RequestTimeout,  (HttpStatusCode)logEntry.ResponseMessage.StatusCode);
            Assert.Equal(HttpStatusCode.RequestTimeout, responseWithTimeout.StatusCode);
        }

        [Fact]
        public async Task Retry_policy_should_work()
        {
            var clientWithRetry = HttpClientFactoryBuilder.Create()
                                                          .WithPolicy(
                                                                      Policy<HttpResponseMessage>
                                                                          .Handle<HttpRequestException>()
                                                                          .OrResult(result => result.StatusCode is >= HttpStatusCode.InternalServerError or HttpStatusCode.RequestTimeout)
                                                                          .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(1)))
                                                          .Build()
                                                          .CreateClient();

            var response = await clientWithRetry.GetAsync($"{_server.Urls[0]}{EndpointUri}");

            Assert.Equal(2, _server.LogEntries.Count());
            Assert.Single(_server.LogEntries, le => (HttpStatusCode)le.ResponseMessage.StatusCode == HttpStatusCode.OK);
            Assert.Single(_server.LogEntries, le => (HttpStatusCode)le.ResponseMessage.StatusCode == HttpStatusCode.RequestTimeout);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(HttpContentValue, await response.Content.ReadAsStringAsync());
        }
    }
}
