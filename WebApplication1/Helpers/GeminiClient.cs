using System.Net.Http.Json;
using System.Text.Json;

namespace WebApplication1.Api.Helpers
{
    public sealed class GeminiClient(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<GeminiClient> logger)
    {
        private const string DefaultModel = "gemini-3.5-flash-lite";

        public bool IsConfigured => !string.IsNullOrWhiteSpace(configuration["Gemini:ApiKey"]);

        public async Task<string?> GenerateAsync(string prompt, CancellationToken cancellationToken, object? responseSchema = null)
        {
            var apiKey = configuration["Gemini:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
                return null;

            var model = configuration["Gemini:Model"];
            if (string.IsNullOrWhiteSpace(model))
                model = DefaultModel;

            var generationConfig = new Dictionary<string, object> { ["maxOutputTokens"] = 500 };
            if (responseSchema != null)
            {
                generationConfig["responseMimeType"] = "application/json";
                generationConfig["responseJsonSchema"] = responseSchema;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model)}:generateContent");
            request.Headers.Add("x-goog-api-key", apiKey);
            request.Content = JsonContent.Create(new
            {
                contents = new[] { new { parts = new[] { new { text = prompt } } } },
                generationConfig
            });

            try
            {
                using var client = httpClientFactory.CreateClient();
                using var response = await client.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("Gemini model {Model} returned HTTP {Status}.", model, (int)response.StatusCode);
                    return null;
                }

                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                if (!document.RootElement.TryGetProperty("candidates", out var candidates)
                    || candidates.ValueKind != JsonValueKind.Array || candidates.GetArrayLength() == 0
                    || !candidates[0].TryGetProperty("content", out var content)
                    || !content.TryGetProperty("parts", out var parts) || parts.ValueKind != JsonValueKind.Array)
                    return null;

                var text = string.Concat(parts.EnumerateArray()
                    .Where(part => !part.TryGetProperty("thought", out var thought) || thought.ValueKind != JsonValueKind.True)
                    .Select(part => part.TryGetProperty("text", out var value) ? value.GetString() : null));
                return string.IsNullOrWhiteSpace(text) ? null : text;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("Gemini model {Model} did not respond before the request timeout.", model);
                return null;
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException)
            {
                logger.LogWarning("Gemini model {Model} could not complete the request ({ErrorType}).", model, ex.GetType().Name);
                return null;
            }
        }
    }
}
