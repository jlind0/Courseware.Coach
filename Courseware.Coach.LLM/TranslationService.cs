using Courseware.Coach.LLM.Core;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Courseware.Coach.LLM
{
    public class TranslationService : ITranslationService
    {
        protected string Key { get; }
        protected string Endpoint { get; }
        protected string Location { get; }
        public TranslationService(IConfiguration config)
        {
            Key = config["Translator:Key"] ?? throw new InvalidDataException();
            Endpoint = config["Translator:Endpoint"] ?? throw new InvalidDataException();
            Location = config["Translator:Location"] ?? throw new InvalidDataException();
        }
        public async Task<string> Translate(string text, string fromLocale, string toLocale, CancellationToken token = default)
        {
            string route = $"/translate?api-version=3.0&from={fromLocale.Split('-')[0]}&to={toLocale.Split('-')[0]}";
            object[] body = [new { Text = text }];
            var requestBody = JsonConvert.SerializeObject(body);
            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage())
            {
                // Build the request.
                request.Method = HttpMethod.Post;
                request.RequestUri = new Uri(Endpoint + route);
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                request.Headers.Add("Ocp-Apim-Subscription-Key", Key);
                // location required if you're using a multi-service or regional (not global) resource.
                request.Headers.Add("Ocp-Apim-Subscription-Region", Location);

                // Send the request and get response.
                HttpResponseMessage response = await client.SendAsync(request);
                // Read response as a string.
                return await response.Content.ReadAsStringAsync();
            }
        }
    }
}
