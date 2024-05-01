using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Courseware.Coach.LLM.Core
{
    public interface ITinyURL
    {
        Task<string?> GetTinyUrl(string url, CancellationToken token = default);
    }
}
