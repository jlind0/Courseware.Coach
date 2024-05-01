using Courseware.Coach.LLM.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Courseware.Coach.LLM
{
    public class TinyUrl : ITinyURL
    {
        protected string ApiKey { get; }
        public TinyUrl(IConfiguration config)
        {
            ApiKey = config["TinyURL:ApiKey"] ?? throw new InvalidDataException();
        }
        public async Task<string?> GetTinyUrl(string url, CancellationToken token = default)
        {
            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage())
            {
                client.Timeout = TimeSpan.FromSeconds(120);
                // Build the request.
                request.Method = HttpMethod.Post;
                request.RequestUri = new Uri("https://api.tinyurl.com/create");
                request.Content = new StringContent($"{{\"url\":\"{url}\"}}", Encoding.UTF8, "application/json");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);

                // Send the request and get response.
                HttpResponseMessage response = await client.SendAsync(request, token);
                // Read response as a string.
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync();
                dynamic resp = JsonConvert.DeserializeObject(json);
                return resp.data.tiny_url;
            }
        }
    }
}
