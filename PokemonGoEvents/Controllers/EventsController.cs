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
            // Intentionally return a blank response for this endpoint.
            return Content(string.Empty, "text/html");
        }
    }
}
