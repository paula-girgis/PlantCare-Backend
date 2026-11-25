using System.Drawing;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Api.DTOs;

namespace WebApplication1.Api.Controllers
{
    public class FloraCheckController(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<FloraCheckController> logger) : ApiBaseController
    {
        private const string DefaultGeminiModel = "gemini-3.5-flash-lite";
        private const string ClassifierUrl = "https://fastflorocheck.onrender.com/predict";

        [HttpPost("predict")]
        public async Task<IActionResult> Predict([FromForm] PredictionRequestDto request, CancellationToken cancellationToken)
        {
            var image = request.file;
            if (image == null || image.Length == 0)
                return BadRequest(new { details = "No image provided." });

            if (!MediaTypeHeaderValue.TryParse(image.ContentType, out var imageType)
                || imageType.MediaType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) != true)
                return BadRequest(new { details = "Uploaded file is not an image." });

            var apiKey = configuration["Gemini:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
                return StatusCode(503, new { details = "Plant-care explanations are not configured." });

            var model = configuration["Gemini:Model"];
            if (string.IsNullOrWhiteSpace(model))
                model = DefaultGeminiModel;

            try
            {
                using var client = httpClientFactory.CreateClient();
                using var upload = new MultipartFormDataContent();
                var imageContent = new StreamContent(image.OpenReadStream());
                imageContent.Headers.ContentType = imageType;
                upload.Add(imageContent, "file", image.FileName);

                using var classification = await client.PostAsync(ClassifierUrl, upload, cancellationToken);
                if (!classification.IsSuccessStatusCode)
                {
                    if (classification.StatusCode == HttpStatusCode.BadRequest
                        || classification.StatusCode == HttpStatusCode.UnprocessableEntity)
                    {
                        return BadRequest(new { details = "Upload a clear JPG or PNG of a plant leaf." });
                    }

                    logger.LogWarning("FloraCheck classifier returned HTTP {Status}.", (int)classification.StatusCode);
                    return StatusCode(502, new { details = "The plant classifier is temporarily unavailable." });
                }

                using var prediction = JsonDocument.Parse(await classification.Content.ReadAsStringAsync(cancellationToken));
                var diseaseName = prediction.RootElement.GetProperty("predicted_class").GetString();
                if (string.IsNullOrWhiteSpace(diseaseName))
                    return StatusCode(502, new { details = "The plant classifier returned an empty prediction." });

                using var explanationRequest = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model)}:generateContent");
                explanationRequest.Headers.Add("x-goog-api-key", apiKey);
                explanationRequest.Content = JsonContent.Create(new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new
                                {
                                    text = $"The plant image classifier predicted this label: {diseaseName}. Give brief, practical notes on symptoms, likely causes, and care or treatment. If the label indicates a healthy plant, describe healthy signs and preventive care. Treat the label as a prediction, and respond in plain English without HTML or Markdown."
                                }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        responseMimeType = "application/json",
                        responseJsonSchema = new
                        {
                            type = "object",
                            properties = new
                            {
                                symptoms = new { type = "string" },
                                cause = new { type = "string" },
                                treatment = new { type = "string" }
                            },
                            required = new[] { "symptoms", "cause", "treatment" }
                        },
                        maxOutputTokens = 500
                    }
                });

                using var explanationResponse = await client.SendAsync(explanationRequest, cancellationToken);
                if (!explanationResponse.IsSuccessStatusCode)
                {
                    logger.LogWarning("Gemini model {Model} returned HTTP {Status}.", model, (int)explanationResponse.StatusCode);
                    return StatusCode(502, new { details = "Plant-care explanations are temporarily unavailable." });
                }

                using var envelope = JsonDocument.Parse(await explanationResponse.Content.ReadAsStringAsync(cancellationToken));
                var parts = envelope.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts");
                var explanationText = string.Concat(parts.EnumerateArray()
                    .Where(part => !part.TryGetProperty("thought", out var thought) || thought.ValueKind != JsonValueKind.True)
                    .Select(part => part.TryGetProperty("text", out var value) ? value.GetString() : null));
                using var explanation = JsonDocument.Parse(explanationText);
                var sections = explanation.RootElement;
                var symptoms = sections.GetProperty("symptoms").GetString();
                var cause = sections.GetProperty("cause").GetString();
                var treatment = sections.GetProperty("treatment").GetString();
                if (string.IsNullOrWhiteSpace(symptoms) || string.IsNullOrWhiteSpace(cause) || string.IsNullOrWhiteSpace(treatment))
                    return StatusCode(502, new { details = "The plant-care explanation was incomplete." });

                using var imageBytes = new MemoryStream();
                await using var imageStream = image.OpenReadStream();
                await imageStream.CopyToAsync(imageBytes, cancellationToken);
                imageBytes.Position = 0;
                using var decodedImage = Image.FromStream(imageBytes);
                return Ok(new
                {
                    Image = Convert.ToBase64String(imageBytes.ToArray()),
                    ImageType = decodedImage.RawFormat.ToString(),
                    DiseaseName = diseaseName,
                    ChatbotReply = $"{EncodeExplanation(symptoms)}\nCause: {EncodeExplanation(cause)}\nTreatment: {EncodeExplanation(treatment)}"
                });
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return StatusCode(504, new { details = "The plant analysis took too long. Please try again." });
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning("FloraCheck could not complete a prediction ({ErrorType}).", ex.GetType().Name);
                return StatusCode(502, new { details = "The plant analysis could not be completed." });
            }
        }

        // The current UI decodes HTML once before displaying the three sections as HTML.
        private static string EncodeExplanation(string text) => WebUtility.HtmlEncode(WebUtility.HtmlEncode(text.Trim()));
    }
}
