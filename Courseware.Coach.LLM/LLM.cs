using Courseware.Coach.Core;
using Courseware.Coach.Data;
using Courseware.Coach.Data.Core;
using Courseware.Coach.LLM.Core;
using Courseware.Coach.Subscriptions.Core;
using Microsoft.Extensions.Configuration;
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
        private Guid? currentConversationId;
        public Guid? CurrentConversationId
        {
            get => currentConversationId;
            protected set => this.RaiseAndSetIfChanged(ref currentConversationId, value);
        }
        public bool IsLoggedIn { get => CurrentUser != null; }

        protected BroadcastBlock<string> BrodcastConversation { get; }

        public ITargetBlock<string> Conversation { get => BrodcastConversation; }

        protected BroadcastBlock<byte[]> BrodcastAudioConversation { get; }

        public ITargetBlock<byte[]> AudioConversation{ get => BrodcastAudioConversation; }

        protected ICloneAI CloneAI { get; }
        protected ITTS TTS { get; }
        protected ITranslationService Translation { get; }
        protected IRepository<UnitOfWork, User> UserRepo { get; }
        protected IRepository<UnitOfWork, CH> CoachRepo { get; }
        protected IRepository<UnitOfWork, Course> CourseRepo { get; }
        protected ISubscriptionManager SubscriptionManager { get; }
        private string? currentLocale;
        public string? CurrentLocale
        {
            get => currentLocale;
            protected set => this.RaiseAndSetIfChanged(ref currentLocale, value);
        }   
        protected bool IsVoiceEnabled { get; }
        public LLM(ICloneAI cloneAI, ITTS tts, ITranslationService translation, ISubscriptionManager subscriptionManager,
            IRepository<UnitOfWork, User> userRepo, IRepository<UnitOfWork, CH> coachRepo, 
            IRepository<UnitOfWork, Course> courseRepo, IConfiguration config)
        {
            IsVoiceEnabled = bool.Parse(config["LLM:VoiceEnabled"] ?? "false");
            SubscriptionManager = subscriptionManager;
            CloneAI = cloneAI;
            TTS = tts;
            Translation = translation;
            UserRepo = userRepo;
            CoachRepo = coachRepo;
            CourseRepo = courseRepo;
            BrodcastConversation = new BroadcastBlock<string>(null);
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
            if(CurrentSubscription == null && CurrentCoach.Price > 0)
                throw new InvalidOperationException("No subscription");
            var resp = await CloneAI.GenerateResponse(CurrentCoach.APIKey, new ConversationRequestBody()
            {
                conversation_id = CurrentConversationId.Value,
                user_message = message

            }, token);
            if (resp != null)
                await PushText($"{resp.text}", CurrentCoach.NativeLocale, CurrentCoach.DefaultVoiceName, token);
        }
        protected ConcurrentDictionary<string, string?> DefaultVoice { get; } = new ConcurrentDictionary<string, string?>();
        protected async Task PushText(string message, string locale, string? voiceName, CancellationToken token)
        {
            
            string targetLocale = CurrentLocale ?? CurrentUser?.Locale ?? "en-US";
            
            if (targetLocale != locale)
                message = await Translation.Translate(message, locale, targetLocale, token);
            BrodcastConversation.Post(message);
            if(IsVoiceEnabled)
            {
                voiceName ??= CurrentUser?.DefaultVoiceName ?? await GetDefaultVoice(targetLocale, token);
                if (voiceName != null)
                {
                    _ = Task.Factory.StartNew(async () => 
                    {
                        var audio = await TTS.GenerateSpeech(message, voiceName, targetLocale, token);
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
            if (CurrentSubscription == null && CurrentCoach.Price > 0)
                throw new InvalidOperationException("No subscription");
            if (CurrentConversationId == null)
                await StartConversationWithCoachInstance(CurrentCoachInstance.Id, token);
            if (CurrentConversationId == null)
                throw new InvalidOperationException("No conversation started");
            
            var resp = await CloneAI.GenerateResponse(CurrentCoach.APIKey, new ConversationRequestBody() 
                { 
                    conversation_id = CurrentConversationId.Value, 
                    user_message = message 
                }
                , token);
            if (resp != null)
                await PushText($"{resp.text}", CurrentCoachInstance.NativeLocale, CurrentCoachInstance.DefaultVoiceName, token);
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

        public ITargetBlock<Lesson> FollowLessons(ISourceBlock<bool> moveBlock, CancellationToken token = default)
        {
            if (CurrentCourse?.Lessons == null)
                throw new InvalidOperationException("No course or lessons available.");

            var lessonsLookup = CurrentCourse.Lessons.ToLookup(l => l.Order).OrderBy(l => l.Key);
            var lessonsEnumerator = lessonsLookup.GetEnumerator();

            if (!lessonsEnumerator.MoveNext())
                throw new InvalidOperationException("No lessons to follow.");

            var bufferBlock = new BufferBlock<Lesson>(new DataflowBlockOptions { CancellationToken = token });
            int currentLessonOrder = lessonsEnumerator.Current.Key;

            moveBlock.LinkTo(new ActionBlock<bool>(async _ =>
            {
                if (lessonsEnumerator.Current != null)
                {
                    foreach (var lesson in lessonsEnumerator.Current)
                    {
                        await bufferBlock.SendAsync(lesson, token);
                    }

                    if (!lessonsEnumerator.MoveNext())
                    {
                        bufferBlock.Complete(); // No more lessons, complete the block
                    }
                }
            }), new DataflowLinkOptions { PropagateCompletion = true });

            return bufferBlock;

        }

        public ITargetBlock<Prompt> FollowPrompts(ISourceBlock<Lesson> moveBlock, CancellationToken token = default)
        {
            var bufferBlock = new BufferBlock<Prompt>(new DataflowBlockOptions { CancellationToken = token });
            moveBlock.LinkTo(new ActionBlock<Lesson>(async lesson =>
            {
                if (lesson.Prompts != null)
                {
                    foreach (var prompt in lesson.Prompts)
                    {
                        await bufferBlock.SendAsync(prompt, token);
                    }
                }
                bufferBlock.Complete();
            }), new DataflowLinkOptions { PropagateCompletion = true });
            return bufferBlock;
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
                        await CloneAI.UploadUserInfo(CurrentCoach.APIKey, CurrentUser.Email, CurrentCoach.Slug, token: token);
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
                await CloneAI.UploadUserInfo(CurrentCoach.APIKey, user.Email, CurrentCoach.Slug, token: token);
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
                if (CurrentUser != null)
                    CurrentSubscription = await SubscriptionManager.GetCurrentSubscriptionForCoach(coachId, CurrentUser.ObjectId, token);
                if (CurrentCoach.Price > 0 && CurrentSubscription == null)
                    throw new InvalidOperationException("No subscription");
                var resp = await CloneAI.StartConversation(CurrentCoach.APIKey, CurrentCoach.Slug, email: CurrentUser?.Email, token: token);
                if (resp != null)
                {
                    CurrentConversationId = resp.new_conversation.conversation_id;
                }
                return CurrentCoach;
            }
            catch
            {
                Reset();
                throw;
            }
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
                CurrentConversationId = resp.new_conversation.conversation_id;
            }
            return CurrentCoachInstance;
        }

        public Task<Subscription?> SubscribeToCoach(Guid coachId, CancellationToken token = default)
        {
            if(!IsLoggedIn)
                throw new InvalidOperationException("No user logged in");
            return SubscriptionManager.StartSubscribeToCoach(coachId, CurrentUser!.ObjectId, token);
            
        }

        public Task<Subscription?> SubscribeToCourse(Guid courseId, CancellationToken token = default)
        {
            if(!IsLoggedIn)
                throw new InvalidOperationException("No user logged in");
            return SubscriptionManager.StartSubscribeToCourse(courseId, CurrentUser!.ObjectId, token);
        }
    }
}
