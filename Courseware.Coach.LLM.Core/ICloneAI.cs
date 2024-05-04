using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Courseware.Coach.LLM.Core
{
    public class NewConversationBody
    {
        public string conversation_id { get; set; } = null!;
        public DateTime created_at { get; set; }
        
        public MessageBody[] messages { get; set; } = [];
    }
    public class MessageBody
    {
        public string text { get; set; } = null!;
        public DateTime created_at { get; set; }
        public string sender { get; set; } = null!;
    }
    public class NewConversationResponse
    {
       
        public NewConversationBody @new { get; set; } = null!;
        public string conversation_type { get; set; } = null!;
    }
    public class ConversationHistory
    {
        public MessageBody[] history { get; set; } = [];
    }
    public class ConversationRequestBody
    {
        [Required]
        public string conversation_id { get; set; } = null!;
        [Required]
        public string user_message { get; set; } = null!;
        public bool stream { get; set; } = false;
    }
    public class CloneResponseWrapper
    {
        public CloneResponse clone_response { get; set; } = null!;
    }
    public class CloneResponse
    {
        public Guid conversation_id { get; set; }
        public string text { get; set; } = null!;
        public DateTime created_at { get; set; }
        public Citation[] citations { get; set; } = [];
        public string? imageUrl { get; set; }
        public Dictionary<string, string> affiliate { get; set; } = [];
    }
    public class Citation
    {
        public string url { get; set; } = null!;
        public string title { get; set; } = null!;
        public string text { get; set; } = null!;
        public string @type { get; set; } = null!;
    }
    public class CloneBody
    {
        public Clone clone { get; set; } = null!;
    }
    public class Clone
    {
        public string name { get; set; } = null!;
        public string description { get; set; } = null!;
        public string[] tags { get; set; } = [];
        public string purpose { get; set; } = null!;
        public string? phone { get; set; }
        public string slug { get; set; } = null!;
        public string? personality { get; set; }
        public string? image_url { get; set; }
    }
    public interface ICloneAI
    {
        Task UploadUserInfo(string apiKey, string email, string slug, string? info = null, CancellationToken token = default);
        Task<NewConversationResponse?> StartConversation(string apiKey, string slug, string? instanceSlug = null, string? email = null, CancellationToken token = default);
        Task<ConversationHistory?> GetHistory(string apiKey, string conversationId, CancellationToken token = default);
        Task<CloneResponse?> GenerateResponse(string apiKey, ConversationRequestBody body, CancellationToken token = default);
        Task<Clone?> GetClone(string apiKey, string slug, CancellationToken token = default);
        ISourceBlock<string> GenerateResponseStream(string apiKey, ConversationRequestBody body, CancellationToken token = default);
    }
}
