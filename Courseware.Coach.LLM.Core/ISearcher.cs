using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Courseware.Coach.LLM.Core
{
    public interface ISearcher
    {
        public Task<string[]> GetImages(string index, string query, CancellationToken token = default);
    }
}
