using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Api.DTOs;
using WebApplication1.Api.Errors;
using WebApplication1.Api.Helpers;
using WebApplication1.Core.Repositries;

namespace WebApplication1.Api.Controllers
{
    public class EviroAnalysisController(IGenericRepositry<object> repo, GeminiClient gemini) : ApiBaseController
    {
        [HttpPost("/analysisBot")]
        public async Task<IActionResult> eviroAnalysis([FromBody] EviroAnalysisRequestDto request, CancellationToken cancellationToken)
        {
            var plantName = request.plantName?.Trim();
            if (string.IsNullOrWhiteSpace(plantName))
                return BadRequest(new { details = "You must add a valid plant name." });

            if (!gemini.IsConfigured)
                return StatusCode(503, new { details = "Environmental advice is not configured." });

            var schema = new
            {
                type = "object",
                properties = new
                {
                    validPlant = new { type = "boolean" },
                    temperature = new { type = "string" },
                    humidity = new { type = "string" },
                    soilMoisture = new { type = "string" },
                    lightIntensity = new { type = "string" }
                },
                required = new[] { "validPlant", "temperature", "humidity", "soilMoisture", "lightIntensity" }
            };
            var prompt = $"Give concise environmental advice for growing {plantName}. Current measurements: temperature {request.Temperature} degrees Celsius, humidity {request.Humidity}%, soil moisture {request.soilMoisture}%, light intensity {request.lightIntensity} on the user's scale. For each measurement, give the preferred conditions and one practical adjustment if appropriate. Set validPlant to false with empty advice fields if the name does not identify a plant.";
            var reply = await gemini.GenerateAsync(prompt, cancellationToken, schema);
            if (reply == null)
                return StatusCode(502, new { details = "Environmental advice is temporarily unavailable." });

            try
            {
                using var result = JsonDocument.Parse(reply);
                var advice = result.RootElement;
                if (!advice.GetProperty("validPlant").GetBoolean())
                    return BadRequest(new { details = "You must add a valid plant name." });

                var temperature = advice.GetProperty("temperature").GetString();
                var humidity = advice.GetProperty("humidity").GetString();
                var soilMoisture = advice.GetProperty("soilMoisture").GetString();
                var lightIntensity = advice.GetProperty("lightIntensity").GetString();
                if (new[] { temperature, humidity, soilMoisture, lightIntensity }.Any(string.IsNullOrWhiteSpace))
                    return StatusCode(502, new { details = "The environmental advice was incomplete." });

                return Ok(new { temperature, humidity, soilMoisture, lightIntensity });
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
            {
                return StatusCode(502, new { details = "The environmental advice could not be read." });
            }
        }

        [HttpGet("")]
        public async Task<IActionResult> getEviroAnalysisData()
        {
            var data = await repo.getEviroAnalysisDataAsync();
            if (data == null)
                return BadRequest(new ApiErrorResponse(400, "An error occured"));
            return Ok(data);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> getAnalysisById(int id)
        {
            var data = await repo.getAnalysisByPlantIdAsync(id);
            if (data == null)
                return BadRequest(new ApiErrorResponse(401, "Data Not Found"));
            return Ok(data);
        }
    }
}
