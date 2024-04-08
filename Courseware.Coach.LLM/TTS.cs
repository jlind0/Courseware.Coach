using Courseware.Coach.LLM.Core;
using Microsoft.Azure.Cosmos.Serialization.HybridRow;
using Microsoft.CognitiveServices.Speech;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Courseware.Coach.LLM
{
    public class TTS : ITTS
    {
        protected string ApiKey { get; }
        protected string Region { get; }
        public TTS(IConfiguration config)
        {
            ApiKey = config["AzureSpeech:Key"] ?? throw new InvalidDataException();
            Region = config["AzureSpeech:Region"] ?? throw new InvalidDataException();
        }
        protected SpeechConfig GetConfig()
        {
            return SpeechConfig.FromSubscription(ApiKey, Region);
        }
        public async Task<byte[]?> GenerateSpeech(string text, string voiceName, string locale = "en-US", CancellationToken token = default)
        {
            var config = SpeechConfig.FromSubscription(ApiKey, Region);
            config.SpeechSynthesisVoiceName = voiceName;
            config.SpeechSynthesisLanguage = locale;
            using (var speech = new SpeechSynthesizer(config))
            {
                SpeechSynthesisResult result = await speech.SpeakTextAsync(text);
                if (result.Reason == ResultReason.SynthesizingAudioCompleted)
                    return result.AudioData;
            }
            return null;
        }
        public async Task<VoiceInfo[]> GetVoices(string locale = "en-US", CancellationToken token = default)
        {
            var config = GetConfig();
            using (var speech = new SpeechSynthesizer(config))
            {
                List<VoiceInfo> voices = new List<VoiceInfo>();
                var result = await speech.GetVoicesAsync(locale);
                voices.AddRange(result.Voices.ToArray());
                return voices.ToArray();
            }
        }
    }
}
