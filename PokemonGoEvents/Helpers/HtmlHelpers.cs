using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
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
        /// Convert a subset of Markdown syntax to HTML. This implements basic support for:
        /// - ATX headings (#..######)
        /// - Bold (**bold** / __bold__)
        /// - Italic (*italic* / _italic_)
        /// - Inline code (`code`) and fenced code blocks (```...```) rendered as &lt;pre&gt;&lt;code&gt;
        /// - Links: [text](url)
        /// - Unordered and ordered lists
        /// - Paragraphs and line breaks
        ///
        /// This is a lightweight converter intended for simple content. It HTML-encodes text to
        /// reduce XSS risk and only strips/permits basic formatting.
        /// </summary>
        public static string MarkdownToHtml(string? markdown)
        {
            if (string.IsNullOrEmpty(markdown))
                return string.Empty;

            // Normalize newlines
            var text = markdown.Replace("\r\n", "\n").Replace("\r", "\n");

            // HTML-encode to escape any existing tags
            text = WebUtility.HtmlEncode(text);

            // --- Fenced code blocks ```code``` => <pre><code>...</code></pre>
            text = Regex.Replace(text, "```([\\s\\S]*?)```", match =>
            {
                var inner = match.Groups[1].Value;
                return $"<pre><code>{inner}</code></pre>";
            }, RegexOptions.Multiline);

            // --- Headings (from 6 to 1)
            for (int i = 6; i >= 1; i--)
            {
                var pattern = "^" + new string('#', i) + "\\s+(.*)$";
                text = Regex.Replace(text, pattern, $"<h{i}>$1</h{i}>", RegexOptions.Multiline);
            }

            // --- Bold then italic
            text = Regex.Replace(text, "(\\*\\*|__)(.*?)\\1", "<strong>$2</strong>", RegexOptions.Singleline);
            text = Regex.Replace(text, "(\\*|_)(.*?)\\1", "<em>$2</em>", RegexOptions.Singleline);

            // --- Links [text](url)
            text = Regex.Replace(text, "\\[([^\\]]+)\\]\\(([^)]+)\\)", "<a href=\"$2\" target=\"_blank\" rel=\"noreferrer\">$1</a>", RegexOptions.Singleline);

            // --- Inline code `code`
            text = Regex.Replace(text, "`([^`]+)`", "<code>$1</code>", RegexOptions.Singleline);

            // --- Lists and paragraphs
            var lines = text.Split('\n');
            var sb = new StringBuilder();
            bool inUl = false, inOl = false;

            foreach (var rawLine in lines)
            {
                var line = rawLine.TrimEnd();
                if (Regex.IsMatch(line, "^\\s*([-+*])\\s+(.+)$"))
                {
                    var m = Regex.Match(line, "^\\s*([-+*])\\s+(.+)$");
                    if (!inUl)
                    {
                        // close ordered list if open
                        if (inOl) { sb.AppendLine("</ol>"); inOl = false; }
                        sb.AppendLine("<ul>");
                        inUl = true;
                    }
                    sb.AppendLine($"  <li>{m.Groups[2].Value}</li>");
                    continue;
                }

                if (Regex.IsMatch(line, "^\\s*\\d+\\.\\s+(.+)$"))
                {
                    var m = Regex.Match(line, "^\\s*\\d+\\.\\s+(.+)$");
                    if (!inOl)
                    {
                        if (inUl) { sb.AppendLine("</ul>"); inUl = false; }
                        sb.AppendLine("<ol>");
                        inOl = true;
                    }
                    sb.AppendLine($"  <li>{m.Groups[1].Value}</li>");
                    continue;
                }

                // Close any open lists when encountering a blank or non-list line
                if (inUl) { sb.AppendLine("</ul>"); inUl = false; }
                if (inOl) { sb.AppendLine("</ol>"); inOl = false; }

                // Blank line -> paragraph separator
                if (string.IsNullOrWhiteSpace(line))
                {
                    sb.AppendLine();
                    continue;
                }

                sb.AppendLine(line);
            }

            // Close lists at end
            if (inUl) sb.AppendLine("</ul>");
            if (inOl) sb.AppendLine("</ol>");

            // Wrap plain text blocks separated by blank lines into <p>...</p>
            var parts = Regex.Split(sb.ToString().Trim(), "\\n\\s*\\n");
            var outSb = new StringBuilder();
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;

                // If the block already starts with a block-level tag, keep as-is
                if (Regex.IsMatch(trimmed, "^<(h[1-6]|ul|ol|pre|blockquote|hr)", RegexOptions.IgnoreCase))
                {
                    outSb.AppendLine(trimmed);
                }
                else
                {
                    // Preserve internal line breaks as <br/>
                    var withBreaks = Regex.Replace(trimmed, "\\n", "<br/>");
                    outSb.AppendLine($"<p>{withBreaks}</p>");
                }
            }

            return outSb.ToString().Trim();
        }

        /// <summary>
        /// Check for a file under the provided folder (relative to the current working directory).
        /// If the file does not exist returns null. If the file exists and its creation time is older
        /// than the specified number of days (createdAfterDays) the file is deleted and null is returned.
        /// Otherwise the file content is returned.
        /// </summary>
        /// <param name="folder">Relative folder under current working directory.</param>
        /// <param name="file">File name (with extension) to check inside the folder.</param>
        /// <param name="createdAfterDays">Number of days. Files created earlier than now - createdAfterDays will be removed.</param>
        public static string? GetFileContentIfCreatedWithinDays(string folder, string file, int createdAfterDays)
        {
            if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(file) || createdAfterDays < 0)
                return null;

            try
            {
                var baseDir = Directory.GetCurrentDirectory();
                var dir = Path.Combine(baseDir, folder);
                var filePath = Path.Combine(dir, file);

                if (!File.Exists(filePath))
                    return null;

                // Use the file's last-modified (last write) time instead of creation time
                var lastWriteUtc = File.GetLastWriteTimeUtc(filePath);

                // Special case: if createdAfterDays == 0, consider "today" as the window.
                if (createdAfterDays == 0)
                {
                    if (lastWriteUtc.Date == DateTime.UtcNow.Date)
                    {
                        return File.ReadAllText(filePath);
                    }

                    try { File.Delete(filePath); } catch { /* ignore delete errors */ }
                    return null;
                }

                var threshold = DateTime.UtcNow.AddDays(-createdAfterDays);

                // If the file is older than the threshold, delete and return null
                if (lastWriteUtc < threshold)
                {
                    try { File.Delete(filePath); } catch { /* ignore delete errors */ }
                    return null;
                }

                // File is recent enough — return its content
                return File.ReadAllText(filePath);
            }
            catch
            {
                // On any IO error return null to keep callers simple
                return null;
            }
        }

        /// <summary>
        /// Save the provided string content to a file named 'strippedhtml.txt' inside a folder 'linksfound'
        /// located in the current working directory. Creates the folder if it does not exist.
        /// </summary>
        public static void SaveFileContent(string? content, string folderPath = "latestGeminiResponse", string fileName = "gemini_response.txt")
        {
            var baseDir = Directory.GetCurrentDirectory();
            var linksDir = Path.Combine(baseDir, folderPath);
            Directory.CreateDirectory(linksDir);
            var filePath = Path.Combine(linksDir, fileName);
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
