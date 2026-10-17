using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace WhisperClient.Core;

public sealed record ModelReadiness(bool ServiceReachable, bool TextInstalled, bool VisionInstalled, string Message);

/// <summary>Read-only loopback check: never pulls models, captures content, or submits inference.</summary>
public static class RuntimeReadiness
{
    public static async Task<ModelReadiness> CheckAsync(HttpClient client, string textModel, string visionModel,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await client.GetAsync("http://127.0.0.1:11435/api/tags",
                HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var buffer = new MemoryStream();
            var chunk = new byte[8192];
            int count;
            while ((count = await stream.ReadAsync(chunk, cancellationToken)) > 0)
            {
                if (buffer.Length + count > 1024 * 1024) throw new IOException("Model list too large.");
                buffer.Write(chunk, 0, count);
            }
            using var document = JsonDocument.Parse(buffer.ToArray());
            if (!document.RootElement.TryGetProperty("models", out var models) || models.ValueKind != JsonValueKind.Array)
                throw new JsonException();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var model in models.EnumerateArray())
            {
                if (model.ValueKind != JsonValueKind.Object || !model.TryGetProperty("name", out var name)
                    || name.ValueKind != JsonValueKind.String) throw new JsonException();
                names.Add(Canonical(name.GetString()!));
            }
            var text = names.Contains(Canonical(textModel));
            var vision = names.Contains(Canonical(visionModel));
            return new(true, text, vision, text && vision
                ? "Both configured models are installed. Inference and microphone operation still require testing."
                : "One or more configured models are missing. Partial downloads are not installed models.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception error) when (error is HttpRequestException or IOException or JsonException or OperationCanceledException)
        {
            return new(false, false, false, "Cannot read Ollama model readiness. Check that its local service is running and try again.");
        }
    }

    private static string Canonical(string name) => name[(name.LastIndexOf('/') + 1)..].Contains(':') ? name : name + ":latest";
}
