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

                // Save the stripped HTML to linksfound/strippedhtml.txt
                // HtmlHelpers.SaveStrippedHtml(content);

                // Extract links from the saved content, excluding hidden events
                var links = HtmlHelpers.FindLinksExcludingHiddenFromHtml(content);

                // Return the found links as JSON
                return Ok(links);
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
