using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;
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
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; GitHub-Copilot/1.0)");

            using var res = await client.GetAsync(BaseUrl + EventsPath);
            if (!res.IsSuccessStatusCode)
                return StatusCode((int)res.StatusCode);

            var html = await res.Content.ReadAsStringAsync();

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Find nodes that contain class 'event-item'
            var eventNodes = doc.DocumentNode.SelectNodes("//*[contains(@class,'event-item')]");
            if (eventNodes == null || eventNodes.Count == 0)
                return Ok(Array.Empty<string>());

            var urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var node in eventNodes)
            {
                var anchors = node.SelectNodes(".//a[@href]");
                if (anchors == null) continue;

                foreach (var a in anchors)
                {
                    var href = a.GetAttributeValue("href", string.Empty)?.Trim();
                    if (string.IsNullOrEmpty(href)) continue;

                    // Normalize to absolute URL
                    if (!Uri.TryCreate(href, UriKind.Absolute, out var uri))
                    {
                        if (Uri.TryCreate(BaseUrl, UriKind.Absolute, out var baseUri) && Uri.TryCreate(baseUri, href, out var resolved))
                        {
                            uri = resolved;
                        }
                    }

                    if (uri != null)
                        urls.Add(uri.ToString());
                }
            }

            return Ok(urls.OrderBy(u => u));
        }
    }
}
