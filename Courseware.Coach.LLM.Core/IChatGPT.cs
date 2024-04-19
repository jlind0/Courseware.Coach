using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Courseware.Coach.LLM.Core
{
    public interface IChatGPT
    {
        Task<string?> GetRepsonse(string systemPrompt, string userPrompt, CancellationToken token = default);
    }
}
