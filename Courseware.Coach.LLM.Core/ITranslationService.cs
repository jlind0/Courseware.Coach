using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Courseware.Coach.LLM.Core
{
    public interface ITranslationService
    {
        Task<string> Translate(string text, string fromLocale, string toLocale, CancellationToken token = default);
    }
}
