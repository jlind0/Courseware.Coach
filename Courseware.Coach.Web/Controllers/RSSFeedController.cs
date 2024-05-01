using Courseware.Coach.Core;
using Courseware.Coach.Data;
using Courseware.Coach.Data.Core;
using Courseware.Coach.LLM.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Runtime.Serialization;
using System.ServiceModel.Syndication;
using System.Text;
using System.Xml;
using CH = Courseware.Coach.Core.Coach;

namespace Courseware.Coach.Web.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RSSFeedController : ControllerBase
    {
        protected IRepository<UnitOfWork, CH> CoachRepository { get; }
        protected ILogger Logger { get; }
        public ITwitter Twitter { get; }
        protected ITinyURL TinyUrl { get; }
        public RSSFeedController(IRepository<UnitOfWork, CH> coachRepository, ILogger<RSSFeedController> logger, ITwitter twitter, ITinyURL tinyUrl)
        {
            Logger = logger;
            CoachRepository = coachRepository;
            Twitter = twitter;
            TinyUrl = tinyUrl;
        }
        [HttpGet("render-base64/{base64}/data.asp")]
        public async Task<ActionResult<string>> RenderBase64String(string base64)
        {
            string data = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            string html = $"<!DOCTYPE html><html><head><title>Base64 Encoded Data</title></head><body><pre>{data}</pre></body></html>";
            return Content(html, "text/html", Encoding.UTF8);
        }

        [HttpGet("coaches/{id:guid}")]
        public async Task<IActionResult> GetFeedForCoach(Guid id, CancellationToken token = default)
        {
            try
            {
                var coach = await CoachRepository.Get(id, token: token);
                if (coach == null)
                {
                    return NotFound();
                }
                var feed = new SyndicationFeed();
                feed.Title = SyndicationContent.CreatePlaintextContent($"{coach.Name} RSS Feed");
                feed.Description = SyndicationContent.CreatePlaintextContent(coach.Description);
                List<SyndicationItem> items = new List<SyndicationItem>();
                ConcurrentDictionary<string, string?> accountNames = new ConcurrentDictionary<string, string?>();
                if (coach != null && coach.TwitterAccounts.Count > 0)
                {
                        var data = await Twitter.GetTweets(coach.TwitterAccounts.Select(c => c.AccountName).ToArray(), startDate: DateTime.UtcNow.AddDays(-2), token: token);
                        await Parallel.ForEachAsync(data, async (tweet, tt) =>
                        {
                            var item = new SyndicationItem();
                            item.Summary = SyndicationContent.CreatePlaintextContent(tweet.text);
                            if(!accountNames.ContainsKey(tweet.author_id))
                                accountNames.TryAdd(tweet.author_id, await Twitter.GetAccountNameForId(tweet.author_id));
                            if(accountNames.TryGetValue(tweet.author_id, out string? authorName))
                                item.Authors.Add(new SyndicationPerson() { Name = authorName ?? "" });
                            item.PublishDate = DateTime.Parse(tweet.created_at);
                            var i = Convert.ToBase64String(Encoding.UTF8.GetBytes($"@{accountNames[tweet.author_id]} at {item.PublishDate.UtcDateTime.ToShortDateString()} => {item.Summary.Text}"));
                            item.Links.Add(new SyndicationLink(new Uri(await TinyUrl.GetTinyUrl($"https://courseware.coach/api/rssfeed/render-base64/{i}/data.asp") ?? throw new InvalidDataException())));
                            items.Add(item);
                        });
                }
                feed.Items = items.ToArray();
                var formatter = new Rss20FeedFormatter(feed);
                using (var stringWriter = new StringWriter())
                {
                    using (var xmlWriter = XmlWriter.Create(stringWriter, new XmlWriterSettings() { OmitXmlDeclaration = true }))
                    {
                        formatter.WriteTo(xmlWriter);
                        xmlWriter.Flush();
                    }
                    
                    return Content(stringWriter.ToString(), "application/xml", Encoding.UTF8);
                }
            }
            catch(Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                return BadRequest(ex);
            }
        }
    }
}
