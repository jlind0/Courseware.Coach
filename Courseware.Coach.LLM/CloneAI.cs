using Courseware.Coach.LLM.Core;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Courseware.Coach.LLM
{
    public class CloneAI : ICloneAI
    {
        protected string BaseUri { get; }
        protected ILogger Logger { get; }
        public CloneAI(IConfiguration config, ILogger<CloneAI> logger)
        {
            BaseUri = config["CloneAI:BaseUri"] ?? throw new InvalidDataException();
            Logger = logger;
        }
        public async Task<CloneResponse?> GenerateResponse(string apiKey, ConversationRequestBody body, CancellationToken token = default)
        {
            body.stream = false;
            string route = "/clone/generate_response";
            var requestBody = JsonConvert.SerializeObject(body);
            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage())
            {
                client.Timeout = TimeSpan.FromSeconds(120);
                // Build the request.
                request.Method = HttpMethod.Post;
                request.RequestUri = new Uri(BaseUri + route);
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                request.Headers.Add("x-api-key", apiKey);

                // Send the request and get response.
                HttpResponseMessage response = await client.SendAsync(request, token);
                // Read response as a string.
                response.EnsureSuccessStatusCode();
                var resp = await response.Content.ReadAsStringAsync();
                Logger.LogInformation(resp);
                return JsonConvert.DeserializeObject<CloneResponseWrapper>(resp)?.clone_response;
            }
        }

        public async Task<Clone?> GetClone(string apiKey, string slug, CancellationToken token = default)
        {
            try
            {
                string route = $"/clone/?slug={slug}";
                using (var client = new HttpClient())
                using (var request = new HttpRequestMessage())
                {
                    // Build the request.
                    request.Method = HttpMethod.Get;
                    request.RequestUri = new Uri(BaseUri + route);
                    request.Headers.Add("x-api-key", apiKey);

                    // Send the request and get response.
                    HttpResponseMessage response = await client.SendAsync(request, token);
                    // Read response as a string.
                    response.EnsureSuccessStatusCode();
                    return (await response.Content.ReadFromJsonAsync<CloneBody>(token))?.clone;
                }
            }
            catch(Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                return null;
            }
        }

        public async Task<ConversationHistory?> GetHistory(string apiKey, string conversationId, CancellationToken token = default)
        {
            string route = $"/conversation/history?id={conversationId}";
            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage())
            {
                // Build the request.
                request.Method = HttpMethod.Get;
                request.RequestUri = new Uri(BaseUri + route);
                request.Headers.Add("x-api-key", apiKey);

                // Send the request and get response.
                HttpResponseMessage response = await client.SendAsync(request);
                // Read response as a string.
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<ConversationHistory>(token);
            }
        }

        public async Task<NewConversationResponse?> StartConversation(string apiKey, string slug, string? instanceSlug = null, string? email = null, CancellationToken token = default)
        {
            string route = "/conversation/new";
            var body = new
            {
                slug = slug,
                instance_slug = instanceSlug,
                user_email = email
            };
            var requestBody = JsonConvert.SerializeObject(body);
            Logger.LogInformation(requestBody);
            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage())
            {
                // Build the request.
                request.Method = HttpMethod.Post;
                request.RequestUri = new Uri(BaseUri + route);
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                request.Headers.Add("x-api-key", apiKey);

                // Send the request and get response.
                HttpResponseMessage response = await client.SendAsync(request, token);
                // Read response as a string.
                response.EnsureSuccessStatusCode();
                var resp = await response.Content.ReadAsStringAsync();
                Logger.LogInformation(resp);
                return JsonConvert.DeserializeObject<NewConversationResponse>(resp);
            }
        }

        public async Task UploadUserInfo(string apiKey, string email, string slug, string? info = null, CancellationToken token = default)
        {
            string route = "/clone/user_info";
            if (string.IsNullOrWhiteSpace(info))
                return;
            if (info != null)
            {
                Regex wordPattern = new Regex(@"\b\w+\b");
                info = string.Join(' ', wordPattern.Matches(info).Select(m => m.Value).Take(300));
            }
            var body = new
            {
                user_email = email,
                slug = slug,
                info = info
            };
            var requestBody = JsonConvert.SerializeObject(body);
            Logger.LogInformation(requestBody);
            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage())
            {
                // Build the request.
                request.Method = HttpMethod.Post;
                request.RequestUri = new Uri(BaseUri + route);
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                request.Headers.Add("x-api-key", apiKey);

                // Send the request and get response.
                HttpResponseMessage response = await client.SendAsync(request, token);
                // Read response as a string.
                response.EnsureSuccessStatusCode();
            }
        }

        public ISourceBlock<string> GenerateResponseStream(string apiKey, ConversationRequestBody body, CancellationToken token = default)
        {
            string route = "/clone/generate_response";
            var url = BaseUri + route;
            body.stream = true;
            // Validate request body
            var context = new ValidationContext(body, serviceProvider: null, items: null);
            var validationResults = new List<ValidationResult>();
            if (!Validator.TryValidateObject(body, context, validationResults, true))
            {
                throw new ValidationException("Request body contains invalid data");
            }

            string jsonParameters = JsonConvert.SerializeObject(body);

            var broadcastBlock = new BroadcastBlock<string>(null);

            _= Task.Run(async () =>
            {
                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
                    var content = new StringContent(jsonParameters, System.Text.Encoding.UTF8, "application/json");

                    try
                    {
                        using (var response = await httpClient.PostAsync(url, content))
                        {
                            if (response.IsSuccessStatusCode)
                            {
                                var stream = await response.Content.ReadAsStreamAsync();
                                using (var reader = new System.IO.StreamReader(stream))
                                {
                                    string? line;
                                    string resp = "";
                                    while ((line = await reader.ReadLineAsync()) != null)
                                    {                                        
                                        if (line.StartsWith("data:"))
                                        {
                                            var event_data = line.Substring(5);
                                            var eventObj = JsonConvert.DeserializeObject<Dictionary<string, string>>(event_data);
                                            var currentToken = eventObj["current_token"];

                                            if (currentToken == "[DONE]")
                                            {
                                                if (!string.IsNullOrWhiteSpace(resp))
                                                    broadcastBlock.Post(resp);
                                                broadcastBlock.Complete();
                                                break;
                                            }
                                            else if (currentToken.Contains("\n\n") || currentToken.Contains('?'))
                                            {
                                                resp += currentToken.Replace("\n", "");
                                                Logger.LogInformation(resp);
                                                broadcastBlock.Post(resp);
                                                resp = "";
                                            }
                                            else
                                            {
                                                resp += currentToken;
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                broadcastBlock.Complete();
                            }
                        }
                    }
                    catch (Exception)
                    {
                        broadcastBlock.Complete();
                    }
                }
            });

            return broadcastBlock;
        }
    }
}
