namespace SimpleHCF.Tests
{
    using HttpVersion = HttpVersion;
    using MessageHandlers;

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

    public sealed class BasicClientBuilderTests : IDisposable
    {
        private const string EndpointUri = "/hello/world";
        private const string HttpContentValue = "Hello world!";

        private readonly WireMockServer _server;

        public BasicClientBuilderTests()
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
        public void Providing_a_null_string_base_url_should_throw_argumentnullexception()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => HttpClientFactoryBuilder.Create().WithBaseUrl(null as string));
            Assert.Equal("uriString", exception.ParamName);
        }

        [Fact]
        public void Providing_a_null_base_url_should_throw_argumentnullexception()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => HttpClientFactoryBuilder.Create().WithBaseUrl(null as Uri));
            Assert.Equal("baseUrl", exception.ParamName);
        }

        [Fact]
        public async Task Can_do_http_get_with_plain_client()
        {
            var client = HttpClientFactoryBuilder.Create().Build().CreateClient();
            var response = await client.GetAsync($"{_server.Urls[0]}{EndpointUri}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(HttpContentValue, await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Can_do_http_get_with_plain_client_with_string_base_url()
        {
            var client = HttpClientFactoryBuilder.Create(_server.Urls[0]).Build().CreateClient();
            var response = await client.GetAsync(EndpointUri);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(HttpContentValue, await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Can_do_http_get_with_plain_client_with_base_url()
        {
            var client = HttpClientFactoryBuilder.Create(new Uri(_server.Urls[0])).Build().CreateClient();
            var response = await client.GetAsync(EndpointUri);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(HttpContentValue, await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Can_do_http_get_with_plain_client_with_string_base_url_alternative_syntax()
        {
            var client = HttpClientFactoryBuilder.Create().WithBaseUrl(_server.Urls[0]).Build().CreateClient();
            var response = await client.GetAsync(EndpointUri);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(HttpContentValue, await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Can_do_http_get_with_plain_client_with_base_url_alternative_syntax()
        {
            var client = HttpClientFactoryBuilder.Create().WithBaseUrl(new Uri(_server.Urls[0])).Build().CreateClient();
            var response = await client.GetAsync(EndpointUri);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(HttpContentValue, await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public void Providing_a_null_default_header_name_should_throw_argumentnullexception()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => HttpClientFactoryBuilder.Create().WithDefaultHeader(null, "value"));
            Assert.Equal("name", exception.ParamName);
        }

        [Fact]
        public void Providing_a_null_default_header_value_should_throw_argumentnullexception()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => HttpClientFactoryBuilder.Create().WithDefaultHeader("name", null));
            Assert.Equal("value", exception.ParamName);
        }

        [Fact]
        public void Providing_a_null_default_headers_dictionary_should_throw_argumentnullexception()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => HttpClientFactoryBuilder.Create().WithDefaultHeaders(null));
            Assert.Equal("headers", exception.ParamName);
        }

        [Fact]
        public async Task Will_send_default_headers()
        {
            const string headerName = "foobar";
            const string headerValue = "xyz123";

            var trafficRecorder = new TrafficRecorderMessageHandler(new List<string>());

            var client = HttpClientFactoryBuilder
                               .Create()
                               .WithDefaultHeaders(new Dictionary<string, string> { [headerName] = headerValue })
                               .WithMessageHandler(trafficRecorder)
                               .Build()
                               .CreateClient();

            _ = await client.GetAsync($"{_server.Urls[0]}{EndpointUri}");

            var traffic = Assert.Single(trafficRecorder.Traffic); //sanity check
            Assert.True(traffic.Item1.Headers.TryGetValues(headerName, out var headerValues));
            Assert.Equal(headerValue, headerValues.FirstOrDefault());
        }

        [Fact]
        public async Task Can_do_http_post_with_plain_client()
        {
            var client = HttpClientFactoryBuilder.Create(_server.Urls[0]).Build().CreateClient();
            var response = await client.PostAsync(EndpointUri, new StringContent("{}"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(HttpContentValue, await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public void Should_set_timeout_on_client()
        {
            var timeout = TimeSpan.FromSeconds(999);

            var client = HttpClientFactoryBuilder.Create().WithRequestTimeout(timeout).Build().CreateClient();

            Assert.Equal(timeout, client.Timeout);
        }

        [Fact]
        public void Should_set_HTTP_version_on_client()
        {
            foreach (var httpVersion in Enum.GetValues<HttpVersion>())
            {
                var client = HttpClientFactoryBuilder.Create().WithHttpVersion(httpVersion).Build().CreateClient();

                Assert.Equal(HttpClientFactoryBuilder.HttpVersionMapper[httpVersion], client.DefaultRequestVersion);
            }
        }

        [Fact]
        public void Should_not_dispose_primary_message_handler_when_disposing_client()
        {
            var substitutePrimaryMessageHandler = new SocketsHttpHandler();

            HttpClient client;
            using (client = HttpClientFactoryBuilder.Create().WithPrimaryMessageHandler(substitutePrimaryMessageHandler).Build().CreateClient()) { }

            var isMessageHandlerDisposed = (bool)typeof(SocketsHttpHandler).GetField("_disposed", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(substitutePrimaryMessageHandler);

            Assert.False(isMessageHandlerDisposed);
        }


        public void Dispose() => _server.Dispose();
    }
}
