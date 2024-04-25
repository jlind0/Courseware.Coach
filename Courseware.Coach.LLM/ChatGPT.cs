using Courseware.Coach.LLM.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Courseware.Coach.LLM
{
    public class ChatGPT : IChatGPT
    {
        protected string ApiKey { get; }
        protected string Enpoint { get; }
        protected ILogger Logger { get; }
        public ChatGPT(IConfiguration config, ILogger<ChatGPT> logger)
        {
            ApiKey = config["ChatGPT:ApiKey"] ?? throw new InvalidDataException();
            Enpoint = config["ChatGPT:Endpoint"] ?? throw new InvalidDataException();
            Logger = logger;
        }
        public async Task<string?> GetRepsonse(string systemPrompt, string userPrompt, int maxTokens = 22, CancellationToken token = default)
        {
            try
            {

                using (var client = new HttpClient())
                using (var request = new HttpRequestMessage())
                {
                    // Build the request.
                    request.Method = HttpMethod.Post;
                    request.RequestUri = new Uri(Enpoint);
                    var requestBody = $"{{\"model\": \"gpt-4-turbo\", \"messages\": [{{\"role\": \"system\", \"content\": \"{systemPrompt.ReplaceLineEndings("")}\"}},{{\"role\": \"user\", \"content\": \"{userPrompt.ReplaceLineEndings("")}\"}}], \"temperature\": 1.32, \"max_tokens\": {maxTokens}, \"top_p\": 1, \"frequency_penalty\": 2, \"presence_penalty\": 0}}";
                    Logger.LogInformation(requestBody);
                    request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiKey);
                    // Send POST request
                    HttpResponseMessage response = await client.SendAsync(request, token);
                    // Read response as a string.
                    response.EnsureSuccessStatusCode();
                    // Read response
                    string result = await response.Content.ReadAsStringAsync();
                    Logger.LogInformation(result);
                    JObject jsonResponse = JObject.Parse(result);
                    var reply = jsonResponse["choices"][0]["message"]["content"].ToString();
                    Logger.LogInformation(reply);
                    return reply;
                }
            }
            catch(Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                return null;
            }
        }
    }
}
