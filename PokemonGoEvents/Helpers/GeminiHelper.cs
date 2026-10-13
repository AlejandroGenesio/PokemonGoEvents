using System;
using System.IO;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.Configuration;

namespace PokemonGoEvents.Helpers
{
    public static class GeminiHelper
    {

        // Simple helper: read the raw contents of a .env file if present.
        // Returns the file contents trimmed, or null if not present or unreadable.
        private static string? LoadApiKeyFromDotEnv()
        {
            try
            {
                var path = Path.Combine(Directory.GetCurrentDirectory(), ".env");
                if (!System.IO.File.Exists(path))
                {
                    path = Path.Combine(AppContext.BaseDirectory ?? string.Empty, ".env");
                    if (!System.IO.File.Exists(path))
                        return null;
                }

                var content = System.IO.File.ReadAllText(path);
                return string.IsNullOrWhiteSpace(content) ? null : content.Trim();
            }
            catch
            {
                return null;
            }
        }
        // Optional API key that can be configured from IConfiguration or read from environment.
        private static string? _apiKey;

        public static void Configure(IConfiguration configuration)
        {
            // If not found in configuration or environment, try .env file in app folders
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _apiKey = LoadApiKeyFromDotEnv();
            }
        }



        /// <summary>
        /// Send the provided content followed by the question to the Gemini model and return the text response.
        /// Uses a simple retry/backoff strategy similar to the implementation in the external speesh project.
        /// </summary>
        public static async Task<string?> SendContentAndQuestionAsync(string content, string question, string model = "gemini-3.6-flash")
        {
            if (string.IsNullOrWhiteSpace(content) && string.IsNullOrWhiteSpace(question))
                return null;

            // Ensure API key is loaded either from Configure(...) or environment.
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _apiKey = System.Environment.GetEnvironmentVariable("GEMINI_API_KEY");
            }

            // If an API key is available, also expose it as GOOGLE_API_KEY in the process
            // environment so underlying Google libraries that read that variable can pick it up.
            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                System.Environment.SetEnvironmentVariable("GOOGLE_API_KEY", _apiKey);
            }

            var client = new Client();

            // Build a single user content that contains the provided content and question
            var combined = (content ?? string.Empty) + "\n\n" + (question ?? string.Empty);

            var contents = new List<Content>
            {
                new Content
                {
                    Role = "user",
                    Parts = new List<Part>
                    {
                        new Part { Text = combined }
                    }


                }
            };

            return (await CallWithRetryAsync(client, model, contents, systemInstruction: null))?.Text;
        }

        private static async Task<GenerateContentResponse?> CallWithRetryAsync(Client client, string modelName, List<Content> contents, Content? systemInstruction)
        {
            var rnd = new Random();
            const int maxAttempts = 5;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    var config = new GenerateContentConfig {
                        SystemInstruction = systemInstruction
                    };

                    return await client.Models.GenerateContentAsync(
                        model: modelName,
                        contents: contents,
                        config: config
                    );
                }
                catch (Google.GenAI.ClientError ex)
                {
                    var message = ex.Message ?? string.Empty;
                    var isTransient = message.IndexOf("high demand", StringComparison.OrdinalIgnoreCase) >= 0
                                      || message.IndexOf("rate limit", StringComparison.OrdinalIgnoreCase) >= 0
                                      || message.IndexOf("try again later", StringComparison.OrdinalIgnoreCase) >= 0
                                      || message.IndexOf("temporarily", StringComparison.OrdinalIgnoreCase) >= 0;

                    if (!isTransient || attempt == maxAttempts)
                        throw new InvalidOperationException($"Gemini API client error: {ex.Message}", ex);

                    var backoffMs = (int)(Math.Pow(2, attempt) * 1000) + rnd.Next(0, 500);
                    await Task.Delay(backoffMs);
                    continue;
                }
                catch
                {
                    if (attempt == maxAttempts)
                        throw;

                    var backoffMs = (int)(Math.Pow(2, attempt) * 1000) + rnd.Next(0, 500);
                    await Task.Delay(backoffMs);
                    continue;
                }
            }

            throw new InvalidOperationException("Failed to get a response from Gemini API after retries.");
        }
    }
}
