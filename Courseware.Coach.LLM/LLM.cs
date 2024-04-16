using Courseware.Coach.Core;
using Courseware.Coach.Data;
using Courseware.Coach.Data.Core;
using Courseware.Coach.LLM.Core;
using Courseware.Coach.Subscriptions.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using ReactiveUI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using CH = Courseware.Coach.Core.Coach;

namespace Courseware.Coach.LLM
{
    public class LLM : ReactiveObject, ILLM
    {
        private string? currentVoiceName;
        public string? CurrentVoiceName
        {
            get => currentVoiceName;
            protected set => this.RaiseAndSetIfChanged(ref currentVoiceName, value);
        }
        private CH? currentCoach;
        public CH? CurrentCoach 
        {
            get => currentCoach;
            protected set => this.RaiseAndSetIfChanged(ref currentCoach, value);
        }
        private CoachInstance? currentCoachInstance;
        public CoachInstance? CurrentCoachInstance
        {
            get => currentCoachInstance;
            protected set => this.RaiseAndSetIfChanged(ref currentCoachInstance, value);
        }
        private Course? currentCourse;
        public Course? CurrentCourse
        {
            get => currentCourse;
            protected set => this.RaiseAndSetIfChanged(ref currentCourse, value);
        }
        private Lesson? currentLesson;
        public Lesson? CurrentLesson
        {
            get => currentLesson;
            protected set => this.RaiseAndSetIfChanged(ref currentLesson, value);
        }
        private Prompt? currentPrompt;
        public Prompt? CurrentPrompt 
        {
            get => currentPrompt;
            protected set => this.RaiseAndSetIfChanged(ref currentPrompt, value);
        }
        private User? currentUser;
        public User? CurrentUser
        {
            get => currentUser;
            protected set
            {
                this.RaiseAndSetIfChanged(ref currentUser, value);
                this.RaisePropertyChanged(nameof(IsLoggedIn));
            }
        }
        private Subscription? currentSubscription;
        public Subscription? CurrentSubscription
        {
            get => currentSubscription;
            protected set => this.RaiseAndSetIfChanged(ref currentSubscription, value);
        }
        private string? currentConversationId;
        public string? CurrentConversationId
        {
            get => currentConversationId;
            protected set => this.RaiseAndSetIfChanged(ref currentConversationId, value);
        }
        public bool IsLoggedIn { get => CurrentUser != null; }

        protected BroadcastBlock<CloneResponse> BrodcastConversation { get; }

        public ISourceBlock<CloneResponse> Conversation { get => BrodcastConversation; }

        protected BroadcastBlock<byte[]> BrodcastAudioConversation { get; }
        protected ILookup<int, Lesson>? Lessons { get; set; }
        public ISourceBlock<byte[]> AudioConversation{ get => BrodcastAudioConversation; }

        protected ICloneAI CloneAI { get; }
        protected ITTS TTS { get; }
        protected ITranslationService Translation { get; }
        protected IRepository<UnitOfWork, User> UserRepo { get; }
        protected IRepository<UnitOfWork, CH> CoachRepo { get; }
        protected IRepository<UnitOfWork, Course> CourseRepo { get; }
        protected ISubscriptionManager SubscriptionManager { get; }
        protected ILogger Logger { get; }
        private string? currentLocale;
        public string? CurrentLocale
        {
            get => currentLocale;
            protected set => this.RaiseAndSetIfChanged(ref currentLocale, value);
        }   
        protected bool IsVoiceEnabled { get; }
        public LLM(ICloneAI cloneAI, ITTS tts, ITranslationService translation, ISubscriptionManager subscriptionManager,
            IRepository<UnitOfWork, User> userRepo, IRepository<UnitOfWork, CH> coachRepo, 
            IRepository<UnitOfWork, Course> courseRepo, IConfiguration config, ILogger<LLM> logger)
        {
            IsVoiceEnabled = bool.Parse(config["LLM:VoiceEnabled"] ?? "false");
            Logger = logger;
            SubscriptionManager = subscriptionManager;
            CloneAI = cloneAI;
            TTS = tts;
            Translation = translation;
            UserRepo = userRepo;
            CoachRepo = coachRepo;
            CourseRepo = courseRepo;
            BrodcastConversation = new BroadcastBlock<CloneResponse>(null);
            BrodcastAudioConversation = new BroadcastBlock<byte[]>(null);
        }

        public async Task ChatWithCoach(string message, CancellationToken token = default)
        {
            if (CurrentCoach == null)
                throw new InvalidOperationException("No coach or conversation started");
            if(CurrentConversationId == null)
                await StartConversationWithCoach(CurrentCoach.Id, token);
            if (CurrentConversationId == null)
                throw new InvalidOperationException("No conversation started");
            Logger.LogInformation($"Current Conversation Id : {CurrentConversationId}");
            string effectiveLocale = CurrentLocale ?? CurrentSubscription?.Locale ?? CurrentUser?.Locale ?? "en-US";
            if (effectiveLocale != CurrentCoach.NativeLocale)
            {
                message = await Translation.Translate(message, effectiveLocale, CurrentCoach.NativeLocale, token);
            }
            var resp = await CloneAI.GenerateResponse(CurrentCoach.APIKey, new ConversationRequestBody()
            {
                conversation_id = CurrentConversationId,
                user_message = message

            }, token);
            if (resp != null)
                await PushText(resp, CurrentCoach.NativeLocale, CurrentCoach.DefaultVoiceName, token);
        }
        protected ConcurrentDictionary<string, string?> DefaultVoice { get; } = new ConcurrentDictionary<string, string?>();
        protected async Task PushText(CloneResponse message, string locale, string? voiceName, CancellationToken token)
        {
            
            string targetLocale = CurrentLocale ?? CurrentUser?.Locale ?? "en-US";
            
            if (targetLocale != locale)
                message.text = await Translation.Translate(message.text, locale, targetLocale, token);
            BrodcastConversation.Post(message);
            if(IsVoiceEnabled)
            {
                voiceName ??= CurrentUser?.DefaultVoiceName ?? await GetDefaultVoice(targetLocale, token);
                if (voiceName != null)
                {
                    _ = Task.Factory.StartNew(async () => 
                    {
                        var audio = await TTS.GenerateSpeech(message.text, voiceName, targetLocale, token);
                        if (audio != null)
                            BrodcastAudioConversation.Post(audio);
                    }, token);
                }
            }
        }
        protected async Task<string?> GetDefaultVoice(string locale, CancellationToken token)
        {
            if (DefaultVoice.TryGetValue(locale, out var voiceName))
                return voiceName;
            voiceName = (await TTS.GetVoices(locale, token)).FirstOrDefault()?.ShortName;
            DefaultVoice.AddOrUpdate(locale, voiceName, (k, v) => voiceName);
            return voiceName;
        }
        public async Task ChatWithCoachInstance(string message, CancellationToken token = default)
        {
            if (CurrentCoach == null)
                throw new InvalidOperationException("No coach or conversation started");
            if(CurrentCoachInstance == null)
                throw new InvalidOperationException("No coach instance or conversation started");
            if (CurrentConversationId == null)
                await StartConversationWithCoachInstance(CurrentCoachInstance.Id, token);
            if (CurrentConversationId == null)
                throw new InvalidOperationException("No conversation started");
            string effectiveLocale = CurrentLocale ?? CurrentSubscription?.Locale ?? CurrentUser?.Locale ?? "en-US";
            if(effectiveLocale != CurrentCoach.NativeLocale)
            {
                message = await Translation.Translate(message, effectiveLocale, CurrentCoach.NativeLocale, token);
            }
            var resp = await CloneAI.GenerateResponse(CurrentCoach.APIKey, new ConversationRequestBody() 
                { 
                    conversation_id = CurrentConversationId, 
                    user_message = message 
                }
                , token);
            if (resp != null)
                await PushText(resp, CurrentCoachInstance.NativeLocale, CurrentCoachInstance.DefaultVoiceName, token);
        }
        public async Task<Course?> StartCourse(Guid courseId, CancellationToken token = default)
        {
            Reset();
            CurrentCourse = await CourseRepo.Get(courseId, token: token);
            if (CurrentCourse == null)
                throw new InvalidOperationException("Course not found");
            CurrentCoach = await CoachRepo.Get(CurrentCourse.CoachId, token: token);
            if (CurrentCoach == null)
                throw new InvalidOperationException("Coach not found");
            if(CurrentCourse.InstanceId != null)
            {
                CurrentCoachInstance = CurrentCoach.Instances.SingleOrDefault(i => i.Id == CurrentCourse.InstanceId);
                if (CurrentCoachInstance == null)
                    throw new InvalidOperationException("Coach instance not found");
            }
            if (CurrentUser != null)
            {
                CurrentSubscription = await SubscriptionManager.GetCurrentSubscriptionForCourse(courseId, CurrentUser.ObjectId, token);
                string effectiveLocale = CurrentLocale ?? CurrentSubscription?.Locale ?? CurrentUser.Locale;
                if(CurrentCoach.NativeLocale != effectiveLocale)
                {
                    foreach(var lesson in CurrentCourse.Lessons)
                    {
                        lesson.Name = await Translation.Translate(lesson.Name, CurrentCoach.NativeLocale, effectiveLocale, token);
                        foreach(var prompt in lesson.Prompts)
                        {
                            prompt.Text = await Translation.Translate(prompt.Text, CurrentCoach.NativeLocale, effectiveLocale, token);
                        }
                    }
                }
            }
            if (CurrentSubscription?.ConversationId != null)
            {
                CurrentConversationId = CurrentSubscription.ConversationId;
                var history = await CloneAI.GetHistory(CurrentCoach.APIKey, CurrentConversationId, token);
                if (history != null)
                {
                    foreach (var msg in history.history.OrderByDescending(h => h.created_at).Take(10).OrderBy(p => p.created_at))
                    {
                        if (msg.sender == "USER")
                            await PushText(new CloneResponse() { text = $"You: {msg.text}" }, CurrentCoach.NativeLocale, CurrentCoach.DefaultVoiceName, token);
                        else
                            await PushText(new CloneResponse() { text = $"Coach: {msg.text}" }, CurrentCoach.NativeLocale, CurrentCoach.DefaultVoiceName, token);
                    }
                }
            }
            if(CurrentSubscription?.CurrentLessonId != null)
            {
                CurrentLesson = CurrentCourse.Lessons.SingleOrDefault(l => l.Id == CurrentSubscription.CurrentLessonId);
            }
            if (CurrentConversationId == null)
            {
                NewConversationResponse? resp = null;
                if (CurrentCoachInstance != null)
                    resp = await CloneAI.StartConversation(CurrentCoach.APIKey, CurrentCoach.Slug, CurrentCoachInstance.Slug, CurrentUser?.Email, token);
                else
                    resp = await CloneAI.StartConversation(CurrentCoach.APIKey, CurrentCoach.Slug, null, CurrentUser?.Email, token);
                CurrentConversationId = resp?.@new.conversation_id;
                if (CurrentSubscription != null)
                {
                    CurrentSubscription.ConversationId = CurrentConversationId;
                    await SubscriptionManager.UpdateSubscription(CurrentSubscription, CurrentUser!.ObjectId, token);
                }
            }
            if (CurrentConversationId == null)                
                throw new InvalidOperationException("Conversation not started");
            return CurrentCourse;
        }
        public Lesson? GetNextLesson()
        {
            CurrentPrompt = null;
            if (CurrentCourse == null)
                throw new InvalidOperationException("Course not started");
            if (CurrentLesson == null)
            {
                CurrentLesson = CurrentCourse.Lessons.OrderBy(l => l.Order).FirstOrDefault();
                return CurrentLesson;
            }
            foreach(var lesson in CurrentCourse.Lessons.OrderBy(l => l.Order))
            {
                if (lesson.Id != CurrentLesson.Id && lesson.Order >= CurrentLesson.Order)
                {
                    CurrentLesson = lesson;
                    return lesson;
                }
            }
            return null;
        }
        public Prompt? GetNextPrompt()
        {
            if (CurrentCourse == null)
                throw new InvalidOperationException("Course not started");
            if (CurrentLesson == null)
                throw new InvalidOperationException("Lesson not started");
            if(CurrentPrompt == null)
            {
                CurrentPrompt = CurrentLesson.Prompts.OrderBy(p => p.Order).FirstOrDefault();
                return CurrentPrompt;
            }
            foreach(var prompt in CurrentLesson.Prompts.OrderBy(p => p.Order))
            {
                if(prompt.Id != CurrentPrompt.Id && prompt.Order >= CurrentPrompt.Order)
                {
                    CurrentPrompt = prompt;
                    return prompt;
                }
            }
            return null;
        }
        public async Task SendMessageForCurrentPrompt(string message, CancellationToken token = default)
        {
            if(CurrentPrompt == null)
                throw new InvalidOperationException("No prompt started");
            if(CurrentConversationId == null)
                throw new InvalidOperationException("No conversation started");
            if(CurrentCoach == null)
                throw new InvalidOperationException("No coach started");
            message = $"{CurrentPrompt.Text} {message}";
            if (CurrentCoachInstance == null)
                await ChatWithCoach(message, token);
            else
                await ChatWithCoachInstance(message, token);
        }
        public async ValueTask DisposeAsync()
        {
            BrodcastAudioConversation.Complete();
            BrodcastConversation.Complete();
            await BrodcastAudioConversation.Completion;
            await BrodcastConversation.Completion;
        }

        public Task EndConversation(CancellationToken token = default)
        {
            CurrentConversationId = null;
            return Task.CompletedTask;
        }

        public async Task<User?> Login(string objectId, CancellationToken token = default)
        {
            if (!IsLoggedIn)
            {
                var users = await UserRepo.Get(filter: u => u.ObjectId == objectId, token: token);
                if (users.Items.Count == 1)
                {
                    CurrentUser = users.Items.Single();
                    if(CurrentCoach != null)
                        await CloneAI.UploadUserInfo(CurrentCoach.APIKey, CurrentUser.Email, CurrentCoach.Slug, CurrentUser.Bio, token: token);
                }
            }
            return CurrentUser;
        }

        public Task<Course?> PreviewCourse(Guid courseId, CancellationToken token = default)
        {
            return CourseRepo.Get(courseId, token: token);
        }

        public async Task<User?> Register(User user, CancellationToken token = default)
        {
            await UserRepo.Add(user, token: token);
            if(CurrentCoach != null)
                await CloneAI.UploadUserInfo(CurrentCoach.APIKey, user.Email, CurrentCoach.Slug, user.Bio, token: token);
            return user;
        }

        public Task<bool> SetLocale(string locale = "en-US", CancellationToken token = default)
        {
            try
            {
                CultureInfo cultureInfo = new CultureInfo(locale);
                CurrentLocale = locale;
                return Task.FromResult(true);
            }
            catch
            {
                return Task.FromResult(false);
            }
        }

        public async Task<bool> SetVoiceName(string voiceName, CancellationToken token = default)
        {
            var voices = await TTS.GetVoices(CurrentLocale ?? "en-US", token);
            if (voices.Any(v => v.ShortName == voiceName))
            {
                CurrentVoiceName = voiceName;
                return true;
            }
            return false;
        }
        protected void Reset()
        {
            CurrentSubscription = null;
            CurrentConversationId = null;
            CurrentCoach = null;
            CurrentCoachInstance = null;
        }
        public async Task<CH?> StartConversationWithCoach(Guid coachId, CancellationToken token = default)
        {
            Reset();
            try
            {

                CurrentCoach = await CoachRepo.Get(coachId, token: token);
                if (CurrentCoach == null)
                    return null;
                if (CurrentUser == null)
                    return null;
                CurrentSubscription = await SubscriptionManager.GetCurrentSubscriptionForCoach(coachId, CurrentUser.ObjectId, token);
                if (!await SubscriptionManager.IsSubscribedToCoach(coachId, CurrentUser.ObjectId, token))
                    return null;
                await CloneAI.UploadUserInfo(CurrentCoach.APIKey, CurrentUser.Email, CurrentCoach.Slug, GetUserInfo(), token: token);
                if(CurrentSubscription?.ConversationId != null)
                {
                    CurrentConversationId = CurrentSubscription.ConversationId;
                    var history = await CloneAI.GetHistory(CurrentCoach.APIKey, CurrentConversationId, token);
                    if (history != null)
                    {
                        foreach (var msg in history.history.OrderByDescending(h => h.created_at).Take(10).OrderBy(p => p.created_at))
                        {
                            if (msg.sender == "USER")
                                await PushText(new CloneResponse() { text = $"You: {msg.text}" }, CurrentCoach.NativeLocale, CurrentCoach.DefaultVoiceName, token);
                            else
                                await PushText(new CloneResponse() { text = $"Coach: {msg.text}" }, CurrentCoach.NativeLocale, CurrentCoach.DefaultVoiceName, token);
                        }
                    }
                    return CurrentCoach;
                }
                var resp = await CloneAI.StartConversation(CurrentCoach.APIKey, CurrentCoach.Slug, email: CurrentUser?.Email, token: token);
                Logger.LogInformation("Conversation started");
                if (resp != null)
                {
                    CurrentConversationId = resp.@new.conversation_id;
                    if(CurrentSubscription != null)
                    {
                        CurrentSubscription.ConversationId = CurrentConversationId;
                        await SubscriptionManager.UpdateSubscription(CurrentSubscription, CurrentUser!.ObjectId, token);
                    }
                    foreach(var msg in resp.@new.messages)
                    {
                        await PushText(new CloneResponse() { text = msg.text}, CurrentCoach.NativeLocale, CurrentCoach.DefaultVoiceName, token);
                    }
                }
                return CurrentCoach;
            }
            catch
            {
                Reset();
                throw;
            }
        }
        protected string? GetUserInfo()
        {
            if (CurrentUser == null)
                return null;
            return $"I am {CurrentUser.FirstName} {CurrentUser.LastName} who lives in {CurrentUser.Country ?? ""} with bio: {CurrentUser.Bio ?? ""}";

        }
        public async Task<CoachInstance?> StartConversationWithCoachInstance(Guid coachId, CancellationToken token = default)
        {
            CurrentConversationId = null;
            if(CurrentCoach == null)
                throw new InvalidOperationException("No coach or conversation started");
            var ci = CurrentCoach.Instances.Single(i => i.Id == coachId);
            CurrentCoachInstance = ci;
            var resp = await CloneAI.StartConversation(CurrentCoach.APIKey, CurrentCoach.Slug, ci.Slug, email: CurrentUser?.Email, token: token);
            if (resp != null)
            {
                CurrentConversationId = resp.@new.conversation_id;
            }
            return CurrentCoachInstance;
        }

        public async Task<Subscription?> SubscribeToCoach(Guid coachId, CancellationToken token = default)
        {
            if(!IsLoggedIn)
                throw new InvalidOperationException("No user logged in");
            CurrentSubscription = await SubscriptionManager.StartSubscribeToCoach(coachId, CurrentUser!.ObjectId, token);
            return CurrentSubscription;

        }

        public async Task<Subscription?> SubscribeToCourse(Guid courseId, CancellationToken token = default)
        {
            if(!IsLoggedIn)
                throw new InvalidOperationException("No user logged in");
            CurrentSubscription = await SubscriptionManager.StartSubscribeToCourse(courseId, CurrentUser!.ObjectId, token);
            return CurrentSubscription;
        }

        public Task<bool> IsSubscribedToCoach(Guid coachId, CancellationToken token = default)
        {
            if (!IsLoggedIn)
                throw new InvalidOperationException("No user logged in.");
            return SubscriptionManager.IsSubscribedToCoach(coachId, CurrentUser!.ObjectId, token);
        }

        public Task<bool> IsSubscribedToCourse(Guid courseId, CancellationToken token = default)
        {
            if (!IsLoggedIn)
                throw new InvalidOperationException("No user logged in.");
            return SubscriptionManager.IsSubscribedToCourse(courseId, CurrentUser!.ObjectId, token);
        }
    }
}
