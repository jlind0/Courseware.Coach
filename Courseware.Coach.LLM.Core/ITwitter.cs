using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Courseware.Coach.LLM.Core
{
    public interface ITwitter
    {
        Task<TwitterItem[]> GetTweets(string[] userIds, int count = 500, DateTime? startDate = null, CancellationToken token = default);
        Task<string?> GetAccountNameForId(string id, CancellationToken token = default);
    }
    public class TwitterItem
    {
        public string id { get; set; } = null!;
        public string text { get; set; } = null!;
        public string created_at { get; set; } = null!;
        public string author_id { get; set; } = null!;
    }
}
