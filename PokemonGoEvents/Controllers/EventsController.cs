using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;
using PokemonGoEvents.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace PokemonGoEvents.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class EventsController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private const string BaseUrl = "https://leekduck.com";
        private const string EventsPath = "/events/";

        public EventsController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet("links")]
        public async Task<IActionResult> GetEventLinks()
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var fullUrl = BaseUrl + EventsPath;
                var html = await client.GetStringAsync(fullUrl);

                var node = HtmlHelpers.FindCurrentEventsFromHtml(html);
                var content = node?.InnerHtml ?? string.Empty;

                // First try to read a recent cached Gemini response file (1 day threshold).
                var cached = HtmlHelpers.GetFileContentIfCreatedWithinDays("latestGeminiResponse", "gemini_response.txt", 0);
                if (!string.IsNullOrEmpty(cached))
                {
                    // Return cached assistant response without further processing
                    return Ok(new { assistant = cached + "<br/><br/>Cached Response" });
                }

                // Extract links from the saved content, excluding hidden events
                var links = HtmlHelpers.FindLinksExcludingHiddenFromHtml(content);

                // Build absolute URLs from the extracted refs (fallback to href stored in Ref).
                var urls = links
                    .Select(l => l.Ref)
                    .Where(r => !string.IsNullOrEmpty(r))
                    .Select(r => r!.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                        ? r!
                        : BaseUrl.TrimEnd('/') + (r!.StartsWith("/") ? r : "/" + r))
                    .Distinct()
                    .ToList();

                // Save each URL's content to disk if missing
                await HtmlHelpers.LoopUrlsAndSaveIfMissingAsync(urls);

                string allContent = HtmlHelpers.ReadAllLinksFoundFilesContent();

                // Send content to Gemini asking for Pokemon names good for PvE
                var question = "Tell me only the name of the pokemons that I can catch and they are good for PvE";
                var geminiResponse = await GeminiHelper.SendContentAndQuestionAsync(allContent, question);

                question = "Tell me the key events. Do not include the dates";
                geminiResponse += await GeminiHelper.SendContentAndQuestionAsync(allContent, question);

                geminiResponse = HtmlHelpers.MarkdownToHtml(geminiResponse ?? string.Empty);

                HtmlHelpers.SaveFileContent(geminiResponse);

                // Print response to console and include it in the HTTP response
                if (!string.IsNullOrEmpty(geminiResponse))
                {
                    Console.WriteLine(geminiResponse);
                }

                return Ok(new { links, assistant = geminiResponse + "<br/><br/> Realtime response" });
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(502, $"Failed to retrieve events page: {ex.Message}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Unexpected error: {ex.Message}");
            }
        }
    }
}
