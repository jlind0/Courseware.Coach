using Courseware.Coach.LLM.Core;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualBasic;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Courseware.Coach.LLM
{
    public class CloneAI : ICloneAI
    {
        protected string BaseUri { get; }
        public CloneAI(IConfiguration config)
        {
            BaseUri = config["CloneAI:BaseUri"] ?? throw new InvalidDataException();
        }
        public async Task<CloneResponse?> GenerateResponse(string apiKey, ConversationRequestBody body, CancellationToken token = default)
        {
            string route = "/clone/generate_response";
            var requestBody = JsonConvert.SerializeObject(body);
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
                return await response.Content.ReadFromJsonAsync<CloneResponse>(token);
            }
        }

        public async Task<Clone?> GetClone(string apiKey, string slug, CancellationToken token = default)
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

        public async Task<ConversationHistory?> GetHistory(string apiKey, Guid conversationId, CancellationToken token = default)
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
                return await response.Content.ReadFromJsonAsync<NewConversationResponse>(token);
            }
        }

        public async Task UploadUserInfo(string apiKey, string email, string slug, string? info = null, CancellationToken token = default)
        {
            string route = "clone/user_info";
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
    }
}
