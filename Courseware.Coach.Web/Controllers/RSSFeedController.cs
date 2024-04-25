using Courseware.Coach.Core;
using Courseware.Coach.Data;
using Courseware.Coach.Data.Core;
using Courseware.Coach.LLM.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
        public RSSFeedController(IRepository<UnitOfWork, CH> coachRepository, ILogger<RSSFeedController> logger, ITwitter twitter)
        {
            Logger = logger;
            CoachRepository = coachRepository;
            Twitter = twitter;
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
                if (coach != null && coach.TwitterAccounts.Count > 0)
                {
                    foreach(var acct in coach.TwitterAccounts)
                    {
                        foreach(var tweet in await Twitter.GetTweets(acct.AccountName, startDate: DateTime.UtcNow.AddDays(-2), token: token))
                        {
                            var item = new SyndicationItem();
                            item.Summary = SyndicationContent.CreatePlaintextContent(tweet.text);
                            item.Authors.Add(new SyndicationPerson() { Name = acct.AccountName });
                            item.Links.Add(new SyndicationLink(new Uri($"https://twitter.com/{acct.AccountName}/status/{tweet.id}")));
                            item.PublishDate = DateTime.Parse(tweet.created_at);
                            items.Add(item);
                        }
                    }
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
