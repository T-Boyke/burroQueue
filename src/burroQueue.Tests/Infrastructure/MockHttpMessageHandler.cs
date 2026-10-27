using System.Net;
using System.Net.Http;

namespace burroQueue.Tests.Infrastructure;

public sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

    public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public static MockHttpMessageHandler WithJsonResponse(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new MockHttpMessageHandler(_ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        });
    }

    public static MockHttpMessageHandler WithStatusCode(HttpStatusCode statusCode)
    {
        return new MockHttpMessageHandler(_ => new HttpResponseMessage(statusCode));
    }

    public static MockHttpMessageHandler WithException(Exception ex)
    {
        return new MockHttpMessageHandler(_ => throw ex);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_handler(request));
    }
}
