using Azure;
using Azure.Search.Documents;
using Courseware.Coach.LLM.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Courseware.Coach.LLM
{
    public class Searcher : ISearcher
    {
        protected Uri Endpoint { get; }
        protected string ApiKey { get; }
        protected IChatGPT ChatGPT { get; }
        protected ILogger Logger { get; }
        public Searcher(IConfiguration config, IChatGPT chatGPT, ILogger<Searcher> logger) 
        {
            Endpoint = new Uri(config["Searcher:Endpoint"] ?? throw new InvalidDataException());
            ApiKey = config["Searcher:ApiKey"] ?? throw new InvalidDataException();
            ChatGPT = chatGPT;
            Logger = logger;
        }
        public async Task<string[]> GetImages(string index, string query, CancellationToken token = default)
        {
            try
            {
                Logger.LogInformation(index);
                AzureKeyCredential credential = new AzureKeyCredential(ApiKey);
                SearchClient client = new SearchClient(Endpoint, index, credential);
                Logger.LogInformation(query);
                var resp = await client.SearchAsync<SearchResponse>(query, cancellationToken: token);
                List<string> images = new List<string>();
                if (resp != null && resp.Value != null)
                {
                    await foreach (var res in resp.Value.GetResultsAsync())
                    {

                        Logger.LogInformation(res.Score.ToString());
                        List<int> indexes = new List<int>();
                        if (res.Document.imageData.Length == 1)
                        {
                            indexes.Add(0);
                        }
                        else
                        {
                            if (res.Document.text.Length > 0)
                            {
                                var arry = JsonConvert.SerializeObject(res.Document.text);
                                arry = JsonConvert.SerializeObject(arry);
                                string pattern = @"(?<!\\)""";
                                arry = Regex.Replace(arry, pattern, "\\\"");
                                Logger.LogInformation(arry);
                                int lngth = arry.Length;
                                var indexResult = await ChatGPT.GetRepsonse($"Identify indexes for array that match the query for array: {string.Concat(arry.Skip(2).Take(lngth - 4))} output only the numerical indexes, do not describe what you are doing, just the data extracts", query, token: token);
                                if (!string.IsNullOrWhiteSpace(indexResult))
                                {
                                    try
                                    {
                                        indexes.AddRange(indexResult.Replace("[", "").Replace("]", "").Split(',').Select(c => int.Parse(c.Trim())));
                                    }
                                    catch { }
                                }
                            }
                            if (res.Document.layoutText.Length > 0)
                            {
                                var arry = JsonConvert.SerializeObject(res.Document.layoutText);

                                arry = JsonConvert.SerializeObject(arry);
                                string pattern = @"(?<!\\)""";
                                arry = Regex.Replace(arry, pattern, "\\\"");
                                int lngth = arry.Length;
                                Logger.LogInformation(arry);
                                var indexResult = await ChatGPT.GetRepsonse($"Identify indexes for array that match the query for array: {string.Concat(arry.Skip(2).Take(lngth - 4))} output only the numerical indexes, do not describe what you are doing, just the data extracts", query, token: token);
                                if (!string.IsNullOrWhiteSpace(indexResult))
                                {
                                    try
                                    {
                                        indexes.AddRange(indexResult.Replace("[", "").Replace("]", "").Split(',').Select(c => int.Parse(c.Trim())));

                                    }
                                    catch { }
                                }
                            }
                        }
                        foreach (var idx in indexes.Distinct())
                        {
                            if (res.Document.imageData.Length > idx)
                            {
                                Logger.LogInformation($"res.Document.metadata_storage_name {idx}");
                                images.Add(res.Document.imageData[idx]);
                            }
                        }
                    }
                }
                return images.ToArray();
            }
            catch(Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                return [];
            }
        }
        protected int[] GetIndexes(string input)
        {
            string pattern = @"\[(\d+)(?:,\s*(\d+))*\]";

            // Create a Regex object
            Regex regex = new Regex(pattern);

            // Find matches
            Match match = regex.Match(input);

            // List to hold all extracted numbers
            List<int> numbers = new List<int>();

            if (match.Success)
            {
                // The first group is the entire match so we iterate from the second group onwards
                for (int i = 1; i < match.Groups.Count; i++)
                {
                    Group group = match.Groups[i];

                    // Each captured group can have multiple captures
                    foreach (Capture capture in group.Captures)
                    {
                        if (int.TryParse(capture.Value, out int number))
                        {
                            numbers.Add(number);
                        }
                    }
                }
            }
            return numbers.ToArray();
        }
    }
    internal class SearchResponse
    {
        public string metadata_storage_name { get; set; } = null!;
        public string[] imageData { get; set; } = null!;
        public string[] text { get; set; } = null!;
        public string[] layoutText { get; set; } = null!;
    }
}
