using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
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
                // If the item itself is marked hidden via class 'hide-event', skip it
                var cls = item.GetAttributeValue("class", string.Empty);
                if (!string.IsNullOrWhiteSpace(cls))
                {
                    var tokens = cls.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                    if (tokens.Contains("hide-event"))
                        continue;
                }

                var startRaw = item.GetAttributeValue("data-event-start-date-check", string.Empty)?.Trim();
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
