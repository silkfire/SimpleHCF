namespace SimpleHCF.Tests
{
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
    using System.Threading.Tasks;

    public sealed class BasicClientBuilderTests : IDisposable
    {
        private const string _endpointUri = "/hello/world";
        private const string _endpointUri2 = "/hello/world2";

        private readonly WireMockServer _server;

        public BasicClientBuilderTests()
        {
            _server = WireMockServer.Start();

            _server.Given(Request.Create().WithPath(_endpointUri).UsingAnyMethod())
                   .RespondWith(
                       Response.Create()
                          .WithStatusCode(HttpStatusCode.OK)
                          .WithHeader("Content-Type", "text/plain")
                          .WithBody("Hello world!"));

            _server.Given(Request.Create().WithPath(_endpointUri2).UsingAnyMethod())
                .RespondWith(
                    Response.Create()
                        .WithStatusCode(HttpStatusCode.OK)
                        .WithHeader("Content-Type", "text/plain")
                        .WithBody("Hello world 2!"));
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
            var response = await client.GetAsync($"{_server.Urls[0]}{_endpointUri}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("Hello world!", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Can_do_http_get_with_plain_client_with_string_base_url()
        {
            var client = HttpClientFactoryBuilder.Create(_server.Urls[0]).Build().CreateClient();
            var response = await client.GetAsync(_endpointUri);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("Hello world!", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Can_do_http_get_with_plain_client_with_base_url()
        {
            var client = HttpClientFactoryBuilder.Create(new Uri(_server.Urls[0])).Build().CreateClient();
            var response = await client.GetAsync(_endpointUri);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("Hello world!", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Can_do_http_get_with_plain_client_with_string_base_url_alternative_syntax()
        {
            var client = HttpClientFactoryBuilder.Create().WithBaseUrl(_server.Urls[0]).Build().CreateClient();
            var response = await client.GetAsync(_endpointUri);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("Hello world!", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Can_do_http_get_with_plain_client_with_base_url_alternative_syntax()
        {
            var client = HttpClientFactoryBuilder.Create().WithBaseUrl(new Uri(_server.Urls[0])).Build().CreateClient();
            var response = await client.GetAsync(_endpointUri);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("Hello world!", await response.Content.ReadAsStringAsync());
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

            _ = await client.GetAsync($"{_server.Urls[0]}{_endpointUri}");

            var traffic = Assert.Single(trafficRecorder.Traffic); //sanity check
            Assert.True(traffic.Item1.Headers.TryGetValues(headerName, out var headerValues));
            Assert.Equal(headerValue, headerValues.FirstOrDefault());
        }

        [Fact]
        public async Task Can_do_http_post_with_plain_client()
        {
            var client = HttpClientFactoryBuilder.Create(_server.Urls[0]).Build().CreateClient();
            var response = await client.PostAsync(_endpointUri, new StringContent("{}"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("Hello world!", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public void Should_set_timeout_on_the_client()
        {
            var timeout = TimeSpan.FromSeconds(999);

            var client = HttpClientFactoryBuilder.Create().WithRequestTimeout(timeout).Build().CreateClient();

            Assert.Equal(timeout, client.Timeout);
        }


        public void Dispose() => _server.Dispose();
    }
}
