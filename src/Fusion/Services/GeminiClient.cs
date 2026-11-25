using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Fusion.Services;

public enum GeminiErrorKind
{
    None,
    InvalidKey,
    QuotaExceeded,
    Network,
    Other
}

public sealed class GeminiResult
{
    public bool Success { get; init; }
    public string Text { get; init; } = string.Empty;
    public GeminiErrorKind ErrorKind { get; init; }
    public string UserMessage { get; init; } = string.Empty;
}

public sealed class GeminiClient : IDisposable
{
    public const string ModelId = "gemini-3.5-flash-lite";
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(60) };

    public async Task<GeminiResult> GenerateProjectIdeasAsync(string apiKey, string subject, int count, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new GeminiResult
            {
                Success = false,
                ErrorKind = GeminiErrorKind.InvalidKey,
                UserMessage = "Clé Gemini manquante."
            };
        }

        var prompt =
            $"""
            Tu es Fusion, un assistant créatif francophone.
            Propose exactement {count} idée(s) de projet sur le sujet : « {subject} ».
            Réponds uniquement en français, de façon structurée :
            - une courte intro d'une phrase
            - puis une liste numérotée 1 à {count}
            - chaque idée : titre en gras (markdown **) + 2-3 phrases concrètes
            Pas d'anglais, pas de préambule hors sujet.
            """;

        var url =
            $"https://generativelanguage.googleapis.com/v1beta/models/{ModelId}:generateContent?key={Uri.EscapeDataString(apiKey.Trim())}";

        var body = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[] { new { text = prompt } }
                }
            },
            generationConfig = new
            {
                temperature = 0.9,
                maxOutputTokens = 2048
            }
        };

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            var raw = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return new GeminiResult
                {
                    Success = false,
                    ErrorKind = GeminiErrorKind.InvalidKey,
                    UserMessage = "Clé Gemini invalide ou refusée."
                };
            }

            if (resp.StatusCode == HttpStatusCode.TooManyRequests ||
                raw.Contains("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase) ||
                raw.Contains("quota", StringComparison.OrdinalIgnoreCase))
            {
                return new GeminiResult
                {
                    Success = false,
                    ErrorKind = GeminiErrorKind.QuotaExceeded,
                    UserMessage = "Quota gratuit Gemini épuisé. Réessaie plus tard."
                };
            }

            if (!resp.IsSuccessStatusCode)
            {
                return new GeminiResult
                {
                    Success = false,
                    ErrorKind = GeminiErrorKind.Other,
                    UserMessage = "Erreur de l'API Gemini."
                };
            }

            using var doc = JsonDocument.Parse(raw);
            if (!doc.RootElement.TryGetProperty("candidates", out var candidates) ||
                candidates.GetArrayLength() == 0)
            {
                return new GeminiResult
                {
                    Success = false,
                    ErrorKind = GeminiErrorKind.Other,
                    UserMessage = "Réponse Gemini vide."
                };
            }

            var sb = new StringBuilder();
            foreach (var cand in candidates.EnumerateArray())
            {
                if (!cand.TryGetProperty("content", out var content)) continue;
                if (!content.TryGetProperty("parts", out var parts)) continue;
                foreach (var part in parts.EnumerateArray())
                {
                    if (part.TryGetProperty("text", out var text))
                        sb.Append(text.GetString());
                }
            }

            var textOut = sb.ToString().Trim();
            if (string.IsNullOrWhiteSpace(textOut))
            {
                return new GeminiResult
                {
                    Success = false,
                    ErrorKind = GeminiErrorKind.Other,
                    UserMessage = "Réponse Gemini vide."
                };
            }

            return new GeminiResult { Success = true, Text = textOut, ErrorKind = GeminiErrorKind.None };
        }
        catch (HttpRequestException)
        {
            return new GeminiResult
            {
                Success = false,
                ErrorKind = GeminiErrorKind.Network,
                UserMessage = "Impossible de joindre l'API Gemini."
            };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new GeminiResult
            {
                Success = false,
                ErrorKind = GeminiErrorKind.Network,
                UserMessage = "Délai dépassé auprès de Gemini."
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new GeminiResult
            {
                Success = false,
                ErrorKind = GeminiErrorKind.Other,
                UserMessage = "Erreur inattendue côté Gemini."
            };
        }
    }

    public void Dispose() => _http.Dispose();
}
