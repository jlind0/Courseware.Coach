using Courseware.Coach.LLM.Core;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Courseware.Coach.LLM
{
    public class Twitter : ITwitter
    {
        protected string ApiKey { get; }
        protected string ApiUrl { get; }
        public Twitter(IConfiguration config)
        {
            ApiKey = config["Twitter:ApiKey"] ?? throw new InvalidDataException();
            ApiUrl = config["Twitter:ApiUrl"] ?? throw new InvalidDataException();
        }
        public async Task<TwitterItem[]> GetTweets(string userId, int count = 100, DateTime? startDate = null, CancellationToken token = default)
        {
            List<TwitterItem> items = new List<TwitterItem>();
            string? nextToken = null;
            do
            {
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
                    string url = $"{ApiUrl}tweets/search/recent?query=from:{userId}&max_results={count}&tweet.fields=id,text,created_at";
                    if (startDate != null)
                        url += $"&start_time={startDate.Value.ToString("yyyy-MM-ddTHH:mm:ssZ")}";
                    if(nextToken != null)
                        url += $"&pagination_token={nextToken}";
                    HttpResponseMessage response = await client.GetAsync(url, token);
                    response.EnsureSuccessStatusCode();
                    string json = await response.Content.ReadAsStringAsync();
                    var resp = JsonConvert.DeserializeObject<TwitterResponse>(json);
                    items.AddRange(resp.data);
                    nextToken = resp.meta.next_token;
                }
            } while (items.Count < count && nextToken != null);
            return items.ToArray();
        }
        public class TwitterResponse
        {
            public List<TwitterItem> data { get; } = [];
            public TwitterMetaData meta { get; set; } = null!;
        }
        public class TwitterMetaData
        {
            public int result_count { get; set; }
            public string? next_token { get; set; }
        }
    }
}
