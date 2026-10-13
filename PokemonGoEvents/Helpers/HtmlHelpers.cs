using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace PokemonGoEvents.Helpers
{
    public static class HtmlHelpers
    {
        /// <summary>
        /// Load HTML and find the element representing current events.
        /// Tries id="current-events", class="current-events", data-id="current-events",
        /// then falls back to partial matches in id or class attributes.
        /// Returns the first matching HtmlNode or null.
        /// </summary>
        public static HtmlNode? FindCurrentEventsFromHtml(string? html)
        {
            if (string.IsNullOrEmpty(html))
                return null;

            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            return FindCurrentEvents(doc);
        }

        /// <summary>
        /// Save the provided string content to a file named 'strippedhtml.txt' inside a folder 'linksfound'
        /// located in the current working directory. Creates the folder if it does not exist.
        /// </summary>
        public static void SaveStrippedHtml(string? content)
        {
            var baseDir = Directory.GetCurrentDirectory();
            var linksDir = Path.Combine(baseDir, "linksfound");
            Directory.CreateDirectory(linksDir);
            var filePath = Path.Combine(linksDir, "strippedhtml.txt");
            File.WriteAllText(filePath, content ?? string.Empty);
        }

        /// <summary>
        /// Read all .txt files inside the linksfound folder (under the current working directory)
        /// and return their contents joined together. Files are read in alphabetical order.
        /// Returns an empty string if the folder does not exist or no files are found.
        /// </summary>
        public static string ReadAllLinksFoundFilesContent(string? directoryPath = null)
        {
            var baseDir = Directory.GetCurrentDirectory();
            var linksDir = string.IsNullOrWhiteSpace(directoryPath) ? Path.Combine(baseDir, "linksfound") : directoryPath;
            if (!Directory.Exists(linksDir))
                return string.Empty;

            var files = Directory.GetFiles(linksDir, "*.txt").OrderBy(f => f).ToArray();
            if (files.Length == 0)
                return string.Empty;

            var sb = new StringBuilder();
            foreach (var file in files)
            {
                try
                {
                    var txt = File.ReadAllText(file);
                    if (!string.IsNullOrEmpty(txt))
                    {
                        sb.AppendLine(txt);
                    }
                }
                catch
                {
                    // ignore read errors for individual files
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Create a file with the given name and content only if the file does not already exist.
        /// Returns true when the file was created, false if it already existed.
        /// </summary>
        public static bool CreateFileIfNotExists(string filePath, string content)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("filePath must be provided", nameof(filePath));

            if (File.Exists(filePath))
                return false;

            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(filePath, content ?? string.Empty);
            return true;
        }

        public static bool DoesFileExist(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            if (File.Exists(filePath))
                return true;

            return false;
        }

        /// <summary>
        /// Loop over the provided URLs, and for each URL:
        /// 1. Check if a file (under linksfound) exists for that URL using DoesFileExist.
        /// 2. If the file does not exist, download the URL content and create the file using CreateFileIfNotExists.
        /// The URL is converted to a filesystem-safe filename by URL-escaping and appended with .txt.
        /// </summary>
        public static async Task LoopUrlsAndSaveIfMissingAsync(IEnumerable<string> urls)
        {
            if (urls == null)
                return;

            var baseDir = Directory.GetCurrentDirectory();
            var linksDir = Path.Combine(baseDir, "linksfound");
            Directory.CreateDirectory(linksDir);

            using var client = new HttpClient();

            foreach (var url in urls)
            {
                if (string.IsNullOrWhiteSpace(url))
                    continue;

                // Create a filesystem-safe filename from the URL
                var fileNameSafe = Uri.EscapeDataString(url);
                var filePath = Path.Combine(linksDir, fileNameSafe + ".txt");

                try
                {
                    if (DoesFileExist(filePath))
                        continue;

                    // Download content
                    var content = await client.GetStringAsync(url);

                    content = GetContentSubPages(content);

                    // Attempt to create the file (will only create if not exists)
                    CreateFileIfNotExists(filePath, content);
                }
                catch
                {
                    // Swallow exceptions per-url to continue processing others.
                    // In a real app, consider logging the error.
                }
            }
        }

        /// <summary>
        /// Find event items with class "event-header-item-wrapper" and extract
        /// the attributes data-event-start-date-check and data-event-end-date.
        /// Returns a list of EventDateInfo objects.
        /// </summary>
        public static List<EventDateInfo> FindLinksExcludingHiddenFromHtml(string? html)
        {
            if (string.IsNullOrEmpty(html))
                return new List<EventDateInfo>();

            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            return FindLinksExcludingHidden(doc);
        }

        public static List<EventDateInfo> FindLinksExcludingHidden(HtmlDocument? doc)
        {
            var result = new List<EventDateInfo>();
            if (doc?.DocumentNode == null)
                return result;

            // Find elements that are event items by class token
            var items = doc.DocumentNode.SelectNodes("//*[contains(concat(' ', normalize-space(@class), ' '), ' event-header-item-wrapper ')]");
            if (items == null || items.Count == 0)
                return result;

            foreach (var item in items)
            {
                var startRaw = item.GetAttributeValue("data-event-start-date-check", string.Empty)?.Trim();
                if(startRaw == null)
                    startRaw = item.GetAttributeValue("data-event-start-date", string.Empty)?.Trim();

                var endRaw = item.GetAttributeValue("data-event-end-date", string.Empty)?.Trim();

                // Find the next <a> element in document order after the item and read its 'ref' (or fallback to href)
                var followingAnchor = item.SelectSingleNode("following::a[1]");
                var hrefAttr = followingAnchor?.GetAttributeValue("href", null)?.Trim();

                // Only include if at least one attribute exists
                if (string.IsNullOrEmpty(startRaw) && string.IsNullOrEmpty(endRaw))
                    continue;

                DateTime? startDt = null;
                DateTime? endDt = null;

                if (!string.IsNullOrEmpty(startRaw))
                {
                    if (DateTime.TryParse(startRaw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsedStart))
                        startDt = parsedStart;
                }

                if (!string.IsNullOrEmpty(endRaw))
                {
                    if (DateTime.TryParse(endRaw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsedEnd))
                        endDt = parsedEnd;
                }

                // If the end date is exactly one day before today (yesterday), skip this item
                if (endDt.HasValue && endDt.Value.Date == DateTime.UtcNow.Date.AddDays(-1))
                    continue;

                // If the start date is in the future (greater than today), skip this item
                if (startDt.HasValue && startDt.Value.Date > DateTime.UtcNow.Date)
                    continue;

                result.Add(new EventDateInfo
                {
                    StartCheck = startDt,
                    EndDate = endDt,
                    Ref = string.IsNullOrEmpty(hrefAttr) ? null : hrefAttr
                });
            }

            // Remove duplicates based on start, end and ref values
            return result
                .GroupBy(x => (x.StartCheck, x.EndDate, x.Ref))
                .Select(g => g.First())
                .ToList();
        }

        /// <summary>
        /// Parse the provided HTML and return the inner HTML of the first element
        /// whose class list contains the token "event-description". Returns null
        /// when the content or the element is not found.
        /// </summary>
        public static string? GetContentSubPages(string? html)
        {
            if (string.IsNullOrEmpty(html))
                return null;

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var node = doc.DocumentNode.SelectSingleNode("//*[contains(concat(' ', normalize-space(@class), ' '), ' event-description ')]");
            return node?.InnerHtml?.Trim();
        }

        /// <summary>
        /// Find the current-events element in a parsed HtmlDocument.
        /// </summary>
        public static HtmlNode? FindCurrentEvents(HtmlDocument? doc)
        {
            if (doc?.DocumentNode == null)
                return null;

            var root = doc.DocumentNode;

            // Only match elements that include the class token "current-events"
            var node = root.SelectSingleNode("//*[contains(concat(' ', normalize-space(@class), ' '), ' current-events ')]");
            return node;
        }
    }

    public class EventDateInfo
    {
        public DateTime? StartCheck { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Ref { get; set; }
    }
}
