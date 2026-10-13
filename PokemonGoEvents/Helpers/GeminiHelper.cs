using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.GenAI;
using Google.GenAI.Types;

namespace PokemonGoEvents.Helpers
{
    public static class GeminiHelper
    {
        /// <summary>
        /// Send the provided content followed by the question to the Gemini model and return the text response.
        /// Uses a simple retry/backoff strategy similar to the implementation in the external speesh project.
        /// </summary>
        public static async Task<string?> SendContentAndQuestionAsync(string content, string question, string model = "gemini-3.6-flash")
        {
            if (string.IsNullOrWhiteSpace(content) && string.IsNullOrWhiteSpace(question))
                return null;

            var client = new Client();

            // Build a single user content that contains the provided content and question
            var combined = (content ?? string.Empty) + "\n\n" + (question ?? string.Empty);

            var contents = new List<Content>
            {
                new Content
                {
                    Role = "user",
                    Parts = { new Part { Text = combined } }
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
