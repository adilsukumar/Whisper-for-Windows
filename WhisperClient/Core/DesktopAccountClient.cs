using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WhisperClient.Core;

public sealed class AccountAccessException(string message) : Exception(message);
public sealed record DesktopApproval(string RequestId, string DisplayCode, Uri ApprovalUrl, string Verifier);

/// <summary>HTTPS-only, memory-only sessions; redirects and raw provider errors are never trusted.</summary>
public sealed class DesktopAccountClient
{
    private readonly HttpClient _http;
    private readonly Uri _origin;
    public string? AccessToken { get; private set; }
    public string? Email { get; private set; }
    public DesktopAccountClient(HttpClient http, Uri origin)
    {
        if (origin.Scheme != "https" || origin.AbsolutePath != "/" || origin.Query != ""
            || origin.Fragment != "" || origin.UserInfo != "" || origin.IsLoopback)
            throw new ArgumentException("An exact public HTTPS account origin is required.");
        _http = http; _origin = origin;
    }
    private static string Base64Url(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private async Task<JsonDocument> RequestAsync(HttpMethod method, string path, object? body,
        bool authenticated, CancellationToken cancellation)
    {
        using var request = new HttpRequestMessage(method, new Uri(_origin, path));
        if (body is not null) request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        if (authenticated)
        {
            if (AccessToken is null) throw new AccountAccessException("Sign in to Whisper first.");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
        }
        try
        {
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation);
            if (!response.IsSuccessStatusCode) throw new AccountAccessException("Account access could not be verified. Sign in again or check your subscription.");
            await using var stream = await response.Content.ReadAsStreamAsync(cancellation);
            using var buffer = new System.IO.MemoryStream();
            var chunk = new byte[4096]; int count;
            while ((count = await stream.ReadAsync(chunk, cancellation)) > 0)
            {
                if (buffer.Length + count > 16384) throw new AccountAccessException("Invalid account service response.");
                buffer.Write(chunk, 0, count);
            }
            var document = JsonDocument.Parse(buffer.ToArray());
            if (document.RootElement.ValueKind != JsonValueKind.Object) { document.Dispose(); throw new AccountAccessException("Invalid account service response."); }
            return document;
        }
        catch (HttpRequestException) { throw new AccountAccessException("Account service is unavailable. Access is disabled until it can be verified."); }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        { throw new AccountAccessException("Account service timed out. Access is disabled until it can be verified."); }
        catch (JsonException) { throw new AccountAccessException("Invalid account service response."); }
    }
    public async Task<DesktopApproval> StartAsync(CancellationToken cancellation)
    {
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        using var document = await RequestAsync(HttpMethod.Post, "/desktop/start", new { challenge }, false, cancellation);
        var root = document.RootElement;
        var id = root.GetProperty("request_id").GetString() ?? "";
        var code = root.GetProperty("display_code").GetString() ?? "";
        if (!Regex.IsMatch(id, "^[A-Za-z0-9_-]{43}$") || !Regex.IsMatch(code, "^[A-F0-9]{8}$"))
            throw new AccountAccessException("Invalid sign-in approval response.");
        var expected = new Uri(_origin, "/admin?desktop=" + id);
        if (root.GetProperty("approval_url").GetString() != expected.AbsoluteUri)
            throw new AccountAccessException("Unexpected approval destination. Sign-in was stopped.");
        return new(id, code, expected, verifier);
    }
    public async Task<bool> RedeemAsync(DesktopApproval approval, CancellationToken cancellation)
    {
        using var document = await RequestAsync(HttpMethod.Post, "/desktop/token",
            new { request_id = approval.RequestId, verifier = approval.Verifier }, false, cancellation);
        var root = document.RootElement;
        var status = root.GetProperty("status").GetString();
        if (status == "pending") return false;
        var token = root.GetProperty("access_token").GetString() ?? "";
        if (status != "approved" || !Regex.IsMatch(token, "^[A-Za-z0-9_-]{43}$"))
            throw new AccountAccessException("Invalid desktop session response.");
        AccessToken = token;
        try { await RequireAccessAsync(cancellation); }
        catch { AccessToken = null; throw; }
        return true;
    }
    public async Task RequireAccessAsync(CancellationToken cancellation)
    {
        using var document = await RequestAsync(HttpMethod.Get, "/desktop/me", null, true, cancellation);
        var root = document.RootElement;
        if (!root.TryGetProperty("entitlement", out var entitlement) || entitlement.ValueKind != JsonValueKind.Object
            || !entitlement.TryGetProperty("access", out var access) || access.ValueKind != JsonValueKind.True
            || !entitlement.TryGetProperty("tier", out var tier) || tier.GetString() != "all_features")
            throw new AccountAccessException("An active paid or complimentary account is required.");
        Email = root.GetProperty("email").GetString();
    }
    public async Task LogoutAsync(CancellationToken cancellation)
    {
        try
        {
            if (AccessToken is not null)
                using (await RequestAsync(HttpMethod.Post, "/desktop/logout", new { }, true, cancellation)) { }
        }
        finally { AccessToken = null; Email = null; }
    }
}
