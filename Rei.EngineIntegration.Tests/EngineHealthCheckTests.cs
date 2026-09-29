using System.Net;

namespace Rei.EngineIntegration.Tests;

public sealed class EngineHealthCheckTests
{
    private sealed class StubHandler(Func<int, CancellationToken, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(++Requests, cancellationToken));
    }

    [Fact]
    public async Task RetriesRequestTimeoutAndUnavailableServer()
    {
        using var handler = new StubHandler((attempt, _) => attempt switch
        {
            1 => throw new TaskCanceledException("HTTP request timed out."),
            2 => throw new HttpRequestException("Server not listening yet."),
            3 => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            _ => new HttpResponseMessage(HttpStatusCode.OK)
        });
        using var http = new HttpClient(handler);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var aliveChecks = 0;

        await EngineIntegrationHarness.WaitForHealthAsync(http, new Uri("http://localhost/health"), () => aliveChecks++, deadline.Token);

        Assert.Equal(4, handler.Requests);
        Assert.Equal(4, aliveChecks);
    }

    [Fact]
    public async Task OverallCancellationStopsRetries()
    {
        using var deadline = new CancellationTokenSource();
        using var handler = new StubHandler((_, _) =>
        {
            deadline.Cancel();
            throw new TaskCanceledException("Overall deadline expired.");
        });
        using var http = new HttpClient(handler);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            EngineIntegrationHarness.WaitForHealthAsync(http, new Uri("http://localhost/health"), () => { }, deadline.Token));

        Assert.Equal(1, handler.Requests);
    }
}
