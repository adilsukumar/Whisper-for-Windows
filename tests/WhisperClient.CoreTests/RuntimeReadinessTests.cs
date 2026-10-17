using System.Net;
using WhisperClient.Core;

internal static class RuntimeReadinessTests
{
    public static async Task RunAsync(Action<bool, string> assert)
    {
        using var handler = new FakeHandler();
        using var client = new HttpClient(handler);
        handler.Body = "{\"models\":[{\"name\":\"llama3.1:latest\"},{\"name\":\"llama3.2-vision:latest\"}]}";
        var ready = await RuntimeReadiness.CheckAsync(client, "llama3.1", "llama3.2-vision");
        assert(ready.ServiceReachable && ready.TextInstalled && ready.VisionInstalled, "readiness canonicalizes latest tag");
        var wrongTag = await RuntimeReadiness.CheckAsync(client, "llama3.1:other", "llama3.2-vision");
        assert(!wrongTag.TextInstalled && wrongTag.VisionInstalled, "readiness requires exact model tag");
        handler.Body = "{\"models\":[]}";
        assert(!(await RuntimeReadiness.CheckAsync(client, "llama3.1", "llama3.2-vision")).VisionInstalled, "partial models cannot count as installed");
        handler.Body = "{\"models\":\"bad\"}";
        assert(!(await RuntimeReadiness.CheckAsync(client, "a", "b")).ServiceReachable, "bad model schema rejected");
        handler.Body = "secret provider failure";
        handler.Code = HttpStatusCode.InternalServerError;
        var error = await RuntimeReadiness.CheckAsync(client, "a", "b");
        assert(!error.ServiceReachable && !error.Message.Contains("secret"), "readiness errors redact response details");
        handler.Code = HttpStatusCode.OK;
        handler.Body = new string('x', 1024 * 1024 + 1);
        assert(!(await RuntimeReadiness.CheckAsync(client, "a", "b")).ServiceReachable, "readiness response is bounded");
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var propagated = false;
        try { await RuntimeReadiness.CheckAsync(client, "a", "b", cancelled.Token); }
        catch (OperationCanceledException) { propagated = true; }
        assert(propagated && handler.SafeRequests, "readiness cancellation propagates; only loopback GET used");
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        public string Body = "";
        public HttpStatusCode Code = HttpStatusCode.OK;
        public bool SafeRequests = true;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            SafeRequests &= request.Method == HttpMethod.Get && request.RequestUri!.AbsoluteUri == "http://127.0.0.1:11435/api/tags";
            return Task.FromResult(new HttpResponseMessage(Code) { Content = new StringContent(Body) });
        }
    }
}
