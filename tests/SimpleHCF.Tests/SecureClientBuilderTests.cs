namespace SimpleHCF.Tests
{
    using FakeItEasy;
    using Polly;
    using WireMock.RequestBuilders;
    using WireMock.ResponseBuilders;
    using WireMock.Server;
    using Xunit;

    using System;
    using System.Net;
    using System.Net.Http;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;

    public sealed class SecureClientBuilderTests : IDisposable
    {
        private const string EndpointUri = "/hello/world";
        private const string HttpContentValue = "Hello world!";

        private readonly WireMockServer _server;

        public SecureClientBuilderTests()
        {
            _server = WireMockServer.Start(ssl: true);

            _server.Given(Request.Create().WithPath(EndpointUri).UsingAnyMethod())
                   .RespondWith(
                       Response.Create()
                          .WithStatusCode(HttpStatusCode.OK)
                          .WithHeader("Content-Type", "text/plain")
                          .WithBody(HttpContentValue));
        }

        private static HttpClient CreateClient() => HttpClientFactoryBuilder.Create()
                                                                            .WithCertificate(DefaultDevCert.Get())
                                                                            .WithPolicy(
    			                                                            		Policy<HttpResponseMessage>
                                                                                        .Handle<HttpRequestException>()
                                                                                        .OrResult(result => result.StatusCode is >= HttpStatusCode.InternalServerError or HttpStatusCode.RequestTimeout)
                                                                                   .RetryAsync(3))
                                                                            .Build()
                                                                            .CreateClient();

        [Fact]
        public void Providing_null_certificate_params_should_throw_argumentnullexception()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => HttpClientFactoryBuilder.Create().WithCertificate(null));
            Assert.Equal("certificates", exception.ParamName);
        }

        [Fact]
        public void Providing_a_null_certificate_should_throw_argumentnullexception()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => HttpClientFactoryBuilder.Create().WithCertificate(A.Fake<X509Certificate2>(), null));
            Assert.Equal("certificate", exception.ParamName);
        }

        [Fact]
        public void Providing_no_arguments_to_certificate_should_throw_argumentexception()
        {
            var exception = Assert.Throws<ArgumentException>(() => HttpClientFactoryBuilder.Create().WithCertificate());
            Assert.Equal("certificates", exception.ParamName);
        }

        [Fact]
        public void Providing_a_null_certificate_collection_should_throw_argumentnullexception()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => HttpClientFactoryBuilder.Create().WithCertificates(null));
            Assert.Equal("certificates", exception.ParamName);
        }

        [Fact(Skip = "Requires local certificate setup")]
        public async Task Can_do_https_get_with_plain_client()
        {
            var client = CreateClient();
            var response = await client.GetAsync($"{_server.Urls[0]}{EndpointUri}");
            
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(HttpContentValue, await response.Content.ReadAsStringAsync());
        }

        [Fact(Skip = "Requires local certificate setup")]
        public async Task Can_do_https_post_with_plain_client()
        {
            var client = CreateClient();
            var response = await client.PostAsync($"{_server.Urls[0]}{EndpointUri}", new StringContent("{}"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(HttpContentValue, await response.Content.ReadAsStringAsync());
        }


        public void Dispose() => _server.Dispose();
    }
}
