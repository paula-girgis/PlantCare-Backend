using CloudinaryDotNet;
using System.Text;
using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Win32;
using OpenAI_API.Chat;
using static System.Net.WebRequestMethods;
using WebApplication1.Api.DTOs;
using System.Text.Json;
using System.Net.Http.Headers;

namespace WebApplication1.Api.Controllers
{

    public class ChatbotController : ApiBaseController
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public ChatbotController(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }
        private const string GEMINI_API_URL = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash-exp:generateContent";
        private string GEMINI_API_KEY => !string.IsNullOrWhiteSpace(_configuration["Gemini:ApiKey"]) ? _configuration["Gemini:ApiKey"]! : throw new InvalidOperationException("Configure Gemini__ApiKey before using this endpoint.");


        [HttpPost("chat")]


       
        public async Task<IActionResult> AskQuestion([FromBody] ChatbotRequestDto request)
        {
            var client = new HttpClient();

            var apiUrl = $"{GEMINI_API_URL}?key={GEMINI_API_KEY}";

            var jsonContent = $@"
        {{
            ""contents"": [
                {{
                    ""parts"":[{{""text"": ""{request.question},, summerize your answer in maximum 10 lines,, dont reply by my question ,, or okay ,, reply by your answer only "" }}]
                }}
            ]
        }}";

            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            try
            {
                var response = await client.PostAsync(apiUrl, content);

                response.EnsureSuccessStatusCode();

                
                var responseString = await response.Content.ReadAsStringAsync();

                var responseJson = JsonDocument.Parse(responseString);
                var chatbotReply = responseJson.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                var chatResponse = new chatbotResponseDto
                {
                    userQuestion = request.question,
                    Reply = chatbotReply
                };
                return Ok(chatResponse);    
            }
            catch (Exception ex)
            {
                return BadRequest($"Error: {ex.Message}");
            }
        }
    }
}