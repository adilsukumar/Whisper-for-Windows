using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WhisperClient.Core;

internal static class DesktopAccountTests
{
    public static async Task RunAsync(Action<bool, string> assert)
    {
        var id = new string('r', 43); var token = new string('t', 43); string? challenge = null; bool access = true;
        using var http = new HttpClient(new FakeHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/desktop/start")
            {
                using var payload = JsonDocument.Parse(request.Content!.ReadAsStringAsync().Result);
                challenge = payload.RootElement.GetProperty("challenge").GetString();
                return new { request_id = id, display_code = "AB123456", approval_url = "https://accounts.example.com/admin?desktop=" + id };
            }
            if (request.RequestUri.AbsolutePath == "/desktop/token")
            {
                using var payload = JsonDocument.Parse(request.Content!.ReadAsStringAsync().Result);
                var verifier = payload.RootElement.GetProperty("verifier").GetString()!;
                var computed = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
                assert(computed == challenge, "desktop redemption is PKCE-bound");
                return new { status = "approved", access_token = token };
            }
            assert(request.Headers.Authorization?.Parameter == token, "live entitlement uses bearer session");
            return new { email = "founder@example.com", entitlement = new { access, tier = "all_features" } };
        }));
        var accounts = new DesktopAccountClient(http, new Uri("https://accounts.example.com"));
        var approval = await accounts.StartAsync(default);
        assert(approval.DisplayCode == "AB123456", "matching browser approval code");
        assert(await accounts.RedeemAsync(approval, default), "approved desktop session");
        assert(accounts.Email == "founder@example.com", "email comes from verified service");
        await accounts.RequireAccessAsync(default);
        access = false;
        bool denied = false;
        try { await accounts.RequireAccessAsync(default); } catch (AccountAccessException) { denied = true; }
        assert(denied, "revocation checked live");
        foreach (var invalid in new[] { "http://accounts.example.com", "https://localhost", "https://accounts.example.com/path", "https://u:p@accounts.example.com" })
        {
            bool rejected = false;
            try { _ = new DesktopAccountClient(http, new Uri(invalid)); } catch (ArgumentException) { rejected = true; }
            assert(rejected, "untrusted origin rejected");
        }
        using var hostile = new HttpClient(new FakeHandler(_ => new { request_id = id, display_code = "AB123456", approval_url = "https://attacker.example/admin?desktop=" + id }));
        bool redirectDenied = false;
        try { await new DesktopAccountClient(hostile, new Uri("https://accounts.example.com")).StartAsync(default); }
        catch (AccountAccessException) { redirectDenied = true; }
        assert(redirectDenied, "approval URL cannot change origin");
        await accounts.LogoutAsync(default);
        assert(accounts.AccessToken is null && accounts.Email is null, "sign-out clears memory-only credentials");
        bool loggedOutDenied = false;
        try { await accounts.RequireAccessAsync(default); } catch (AccountAccessException) { loggedOutDenied = true; }
        assert(loggedOutDenied, "signed-out client cannot access inference");
    }
    private sealed class FakeHandler(Func<HttpRequestMessage, object> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(respond(request))) });
    }
}
