namespace SimpleHCF.Tests.MessageHandlers
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Defines the <see cref="TrafficRecorderMessageHandler" />.
    /// </summary>
    internal class TrafficRecorderMessageHandler : DelegatingHandler
    {
        public const string HeaderName  = "foobar";
        public const string HeaderValue = "foobar";

        /// <summary>
        /// Gets the Traffic.
        /// </summary>
        public List<(HttpRequestMessage, HttpResponseMessage)> Traffic { get; } = new();

        /// <summary>
        /// Defines the _visitedMiddleware.
        /// </summary>
        private readonly IList<string> _visitedMiddleware;

        /// <summary>
        /// Initializes a new instance of the <see cref="TrafficRecorderMessageHandler"/> class.
        /// </summary>
        /// <param name="visitedMiddleware">A list containing the names of visited middleware. This list will continue to expand on every middleware visit.</param>
        public TrafficRecorderMessageHandler(IList<string> visitedMiddleware) => _visitedMiddleware = visitedMiddleware;

        /// <summary>
        /// The SendAsync.
        /// </summary>
        /// <param name="request">The request<see cref="HttpRequestMessage"/>.</param>
        /// <param name="cancellationToken">The cancellationToken<see cref="CancellationToken"/>.</param>
        /// <returns>The <see cref="Task{HttpResponseMessage}"/>.</returns>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.Add(HeaderName, HeaderValue);
            var response = await base.SendAsync(request, cancellationToken);
            response.Headers.Add(HeaderName, HeaderValue);
            _visitedMiddleware.Add(nameof(TrafficRecorderMessageHandler));
            Traffic.Add((request, response));

            return response;
        }
    }
}
