using Microsoft.CognitiveServices.Speech;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Courseware.Coach.LLM.Core
{
    public interface ITTS
    {
        Task<VoiceInfo[]> GetVoices(string locale = "en-US", CancellationToken token = default);
        Task<byte[]?> GenerateSpeech(string text, string voiceName, string locale = "en-US", CancellationToken token = default);
        Task<string[]> GetLocales(CancellationToken token = default);
    }
}
