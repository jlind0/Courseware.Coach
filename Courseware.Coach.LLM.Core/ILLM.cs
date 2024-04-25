using Courseware.Coach.Core;
using Courseware.Coach.Data.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Quic;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using CH = Courseware.Coach.Core.Coach;

namespace Courseware.Coach.LLM.Core
{
    public interface ILLM : IAsyncDisposable, INotifyPropertyChanged, INotifyPropertyChanging
    {
        string? CurrentConversationId { get; }
        Subscription? CurrentSubscription { get; }
        CH? CurrentCoach { get; }
        CoachInstance? CurrentCoachInstance { get; }
        Course? CurrentCourse { get; }
        Lesson? CurrentLesson { get; }
        Prompt? CurrentPrompt { get; }
        User? CurrentUser { get; }
        Quiz? CurrentQuiz { get; }
        QuizQuestion? CurrentQuizQuestion { get; }
        bool IsLoggedIn { get; }
        string? CurrentVoiceName { get; }
        string? CurrentLocale { get; }
        ISourceBlock<CloneResponse> Conversation { get; }
        ISourceBlock<byte[]> AudioConversation { get; }
        Task<bool> SetLocale(string locale = "en-US", CancellationToken token = default);
        Task<bool> SetVoiceName(string voiceName, CancellationToken token = default);
        Task<User?> Register(User user, CancellationToken token = default);
        Task<User?> Login(string objectId, CancellationToken token = default);
        Task<CH?> StartConversationWithCoach(Guid coachId, CancellationToken token = default);
        Task<CoachInstance?> StartConversationWithCoachInstance(Guid coachId, CancellationToken token = default);
        Task<Subscription?> SubscribeToCoach(Guid coachId, CancellationToken token = default);
        Task<Subscription?> SubscribeToCourse(Guid courseId, CancellationToken token = default);
        Task ChatWithCoach(string message, string? locale = null, CancellationToken token = default);
        Task ChatWithCoachInstance(string message, string? locale = null, CancellationToken token = default);
        Task EndConversation(CancellationToken token = default);
        Task<Lesson?> GetNextLesson();
        Prompt? GetNextPrompt();
        QuizQuestion? GetNextQuizQuestion();
        Task<bool> SubmitAnswer(string answerOption, CancellationToken token = default);
        Task<Course?> StartCourse(Guid courseId, CancellationToken token = default);
        Task SendMessageForCurrentPrompt(string message, CancellationToken token = default);
        Task<bool> IsSubscribedToCoach(Guid coachId, CancellationToken token = default);
        Task<bool> IsSubscribedToCourse(Guid courseId, CancellationToken token = default);
        Task<bool> Logout(CancellationToken token = default);
        Task SuggestNewTopic(CancellationToken token = default);
        ISourceBlock<string> ImageConversation { get; }
    }
    
}
