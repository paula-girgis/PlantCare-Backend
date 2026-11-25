using Microsoft.AspNetCore.Mvc;
using WebApplication1.Api.DTOs;
using WebApplication1.Api.Helpers;

namespace WebApplication1.Api.Controllers
{
    public class ChatbotController(GeminiClient gemini) : ApiBaseController
    {
        [HttpPost("chat")]
        public async Task<IActionResult> AskQuestion([FromBody] ChatbotRequestDto request, CancellationToken cancellationToken)
        {
            var question = request.question?.Trim();
            if (string.IsNullOrWhiteSpace(question))
                return BadRequest(new { details = "Enter a plant-care question." });

            if (!gemini.IsConfigured)
                return StatusCode(503, new { details = "Plant-care chat is not configured." });

            var prompt = $"Answer the following plant-care question in the same language as the question. Use at most four numbered points in the form '1. Heading: practical advice'. Keep the answer concise, and redirect unrelated questions to plant care. Question: {question}";
            var reply = await gemini.GenerateAsync(prompt, cancellationToken);
            if (reply == null)
                return StatusCode(502, new { details = "Plant-care chat is temporarily unavailable." });

            return Ok(new chatbotResponseDto { userQuestion = question, Reply = reply });
        }
    }
}
