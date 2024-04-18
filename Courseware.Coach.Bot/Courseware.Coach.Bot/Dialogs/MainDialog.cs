// Generated with Bot Builder V4 SDK Template for Visual Studio CoreBot v4.22.0

using Courseware.Coach.Bot;
using Courseware.Coach.Core;
using Courseware.Coach.Data;
using Courseware.Coach.Data.Core;
using Courseware.Coach.LLM;
using Courseware.Coach.LLM.Core;
using Courseware.Coach.Subscriptions.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Recognizers.Text.DataTypes.TimexExpression;
using Microsoft.Recognizers.Text.NumberWithUnit.English;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using static System.Net.Mime.MediaTypeNames;
using CH = Courseware.Coach.Core.Coach;

namespace Courseware.Coach.Bot.Dialogs
{
    public class LLMFactory
    {
        protected IServiceProvider Provider { get; }
        protected Dictionary<string, ILLM> LLMs { get; } = new Dictionary<string, ILLM>();  
        public LLMFactory(IServiceProvider provider)
        {
            Provider = provider;
        }
        public ILLM GetLLM(string id, ActionBlock<CloneResponse>? response = null)
        {
            if(LLMs.TryGetValue(id, out ILLM value))
                return value;
            var llm = Provider.GetRequiredService<ILLM>();
            LLMs[id] = llm;
            if (response != null)
                llm.Conversation.LinkTo(response); 
            return llm;
        }
        public async Task DisconnectLLM(string id)
        {
            if (LLMs.TryGetValue(id, out ILLM value))
            {
                await value.DisposeAsync();
                LLMs.Remove(id);
            }
        }
    }
    public class MainDialog : ComponentDialog
    {
        protected ILogger Logger { get; }
        protected string ConnectionName { get; }
        protected IRepository<UnitOfWork, CH> CoachRepository { get; }
        protected string MetadataUrl { get; }
        protected string IssuerUrl { get; }
        protected LLMFactory Factory { get; }
        protected AdapterWithErrorHandler Adapter { get; }
        protected IRepository<UnitOfWork, Course> CourseRepository { get; }
        // Dependency injection uses this constructor to instantiate MainDialog
        public MainDialog(ILogger<MainDialog> logger, IConfiguration config, LLMFactory factory, 
            IBotFrameworkHttpAdapter adapter, IRepository<UnitOfWork, CH> coachRepository, IRepository<UnitOfWork, Course> courseRepository)
            : base(nameof(MainDialog))
        { 
            Logger = logger;
            CourseRepository = courseRepository;
            Logger.LogInformation("MainDialog constructor called.");
            MetadataUrl = config["Security:MetadataUrl"];
            IssuerUrl = config["Security:Issuer"];
            Factory = factory;
            CoachRepository = coachRepository;
            Adapter = (AdapterWithErrorHandler)adapter;
            ConnectionName = config["ConnectionName"];
            
            AddDialog(new OAuthPrompt(
                nameof(OAuthPrompt),
                new OAuthPromptSettings
                {
                    ConnectionName = ConnectionName,
                    Text = "Please Sign In",
                    Title = "Sign In",
                    Timeout = 300000, // User has 5 minutes to login (1000 * 60 * 5)
                }));
            var waterfallSteps = new WaterfallStep[]
            {
                InitialStepAsync,
                HandleAuthenticationResultAsync
            };
            var coachSteps = new WaterfallStep[] {
                StartTalkToCoach,
                RecieveTalkToCoach,
                LoopBackChatWithCoachStep
            };
            var mainMenuSteps = new WaterfallStep[]
            {
                MainMenuOptions,
                MainMenuChoice
            };
            var pickCoachSteps = new WaterfallStep[]
            {
                DisplayCoachesStep,
                HandleCoachChoiceResultAsync
            };
            var pickCourseSteps = new WaterfallStep[]
            {
                DisplayCoursesStep,
                HandleCourseChoiceResultAsync
            };
            var takeLessonSteps = new WaterfallStep[]
            {
                StartLesson
            };
            var followPromptsSteps = new WaterfallStep[]
            {
                StartLessonPrompt,
                RecieveLessonPrompt
            };
            var continueWithLesson = new WaterfallStep[]
            {
                ContinueLessonPrompt,
                RecieveLessonPrompt
            };
            var finishLesson = new WaterfallStep[]
            {
                StartFinishLesson,
                RecieveFinishLesson
            };
            var subscribeToCoach = new WaterfallStep[]
            {
                StartSubscriptionToCoach,
                FinishSubscriptionToCoach
            };
            var subscribeToCourse = new WaterfallStep[]
            {
                StartSubscriptionToCourse,
                FinishSubscriptionToCourse
            };
            var takeQuiz = new WaterfallStep[]
            {
                StartQuiz,
                AnswerQuizQuestion
            };
            AddDialog(new WaterfallDialog(LoginPickCoach, waterfallSteps));
            AddDialog(new WaterfallDialog(ChatWithCoach, coachSteps));
            AddDialog(new WaterfallDialog(MainMenu, mainMenuSteps));
            AddDialog(new WaterfallDialog(PickCoach, pickCoachSteps));
            AddDialog(new WaterfallDialog(PickCourse, pickCourseSteps));
            AddDialog(new WaterfallDialog(TakeLesson, takeLessonSteps));
            AddDialog(new WaterfallDialog(FollowPrompts, followPromptsSteps));
            AddDialog(new WaterfallDialog(ContinueLesson, continueWithLesson));
            AddDialog(new WaterfallDialog(FinishLesson, finishLesson));
            AddDialog(new WaterfallDialog(SubscribeToCoach, subscribeToCoach));
            AddDialog(new WaterfallDialog(SubscribeToCourse, subscribeToCourse));
            AddDialog(new WaterfallDialog(TakeQuiz, takeQuiz));
            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new ChoicePrompt(nameof(ChoicePrompt)));
            AddDialog(new ConfirmPrompt(nameof(ConfirmPrompt)));
            InitialDialogId = LoginPickCoach;

        }
        protected const string LoginPickCoach = nameof(LoginPickCoach);
        protected const string ChatWithCoach = nameof(ChatWithCoach);
        protected const string MainMenu = nameof(MainMenu);
        protected const string PickCoach = nameof(PickCoach);
        protected const string PickCourse = nameof(PickCourse);
        protected const string TakeLesson = nameof(TakeLesson);
        protected const string FollowPrompts = nameof(FollowPrompts);
        protected const string ContinueLesson = nameof(ContinueLesson);
        protected const string FinishLesson = nameof(FinishLesson);
        protected const string SubscribeToCoach = nameof(SubscribeToCoach);
        protected const string SubscribeToCourse = nameof(SubscribeToCourse);
        protected const string TakeQuiz = nameof(TakeQuiz);
        private async Task<DialogTurnResult> StartSubscriptionToCoach(WaterfallStepContext stepContext, CancellationToken token)
        {
            var coachId = (Guid)stepContext.Options;
            var LLM = Factory.GetLLM(stepContext.Context.Activity.Conversation.Id);
            var subscription = await LLM.SubscribeToCoach(coachId, token);
            if(subscription == null)
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("Failed to subscribe to coach."), token);
                return await stepContext.EndDialogAsync(null, token);
            }
            await stepContext.Context.SendActivityAsync(MessageFactory.Text($"Please complete your [subscription]({subscription.StripeSessionUrl})"), token);
            return await stepContext.PromptAsync(nameof(ConfirmPrompt), new PromptOptions { Prompt = MessageFactory.Text("Have you completed your subscription?") }, token);
        }
        private async Task<DialogTurnResult> StartSubscriptionToCourse(WaterfallStepContext stepContext, CancellationToken token)
        {
            var coachId = (Guid)stepContext.Options;
            var LLM = Factory.GetLLM(stepContext.Context.Activity.Conversation.Id);
            var subscription = await LLM.SubscribeToCourse(coachId, token);
            if (subscription == null)
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("Failed to subscribe to coach."), token);
                return await stepContext.EndDialogAsync(null, token);
            }
            await stepContext.Context.SendActivityAsync(MessageFactory.Text($"Please complete your [subscription]({subscription.StripeSessionUrl})"), token);
            return await stepContext.PromptAsync(nameof(ConfirmPrompt), new PromptOptions { Prompt = MessageFactory.Text("Have you completed your subscription?") }, token);
        }
        private async Task<DialogTurnResult> FinishSubscriptionToCoach(WaterfallStepContext stepContext, CancellationToken token)
        {
            var LLM = Factory.GetLLM(stepContext.Context.Activity.Conversation.Id);
            bool result = (bool)stepContext.Result;
            if(LLM.CurrentSubscription?.CoachId == null)
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("No subscription found."), token);
                return await stepContext.EndDialogAsync(null, token);
            }
            if (result && await LLM.IsSubscribedToCoach(LLM.CurrentSubscription.CoachId.Value, token))
            {
                CancellationTokenSource cts = new CancellationTokenSource();
                // Start a task that sends typing activity every few seconds
                var typingTask = RepeatTypingIndicatorAsync(stepContext.Context, cts.Token);
                try
                {
                    await LLM.StartConversationWithCoach(LLM.CurrentSubscription.CoachId.Value, token);
                }
                finally 
                {
                    cts.Cancel();
                    await typingTask;
                }
                return await stepContext.ReplaceDialogAsync(ChatWithCoach, null, token);
            }
            else
                return await stepContext.ReplaceDialogAsync(MainMenu, null, token);
        }
        private async Task<DialogTurnResult> FinishSubscriptionToCourse(WaterfallStepContext stepContext, CancellationToken token)
        {
            var LLM = Factory.GetLLM(stepContext.Context.Activity.Conversation.Id);
            bool result = (bool)stepContext.Result;
            if (LLM.CurrentSubscription?.CourseId == null)
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("No subscription found."), token);
                return await stepContext.EndDialogAsync(null, token);
            }
            if (result && await LLM.IsSubscribedToCourse(LLM.CurrentSubscription.CourseId.Value, token))
            {
                await LLM.StartCourse(LLM.CurrentSubscription.CourseId.Value, token);
                return await stepContext.ReplaceDialogAsync(TakeLesson, null, token);
            }
            else
                return await stepContext.ReplaceDialogAsync(MainMenu, null, token);
        }
        private async Task<DialogTurnResult> StartQuiz(WaterfallStepContext stepContext, CancellationToken token)
        {
            var LLM = Factory.GetLLM(stepContext.Context.Activity.Conversation.Id);
            var question = LLM.GetNextQuizQuestion();
            if (question == null)
                return await stepContext.ReplaceDialogAsync(FinishLesson, null, token);
            
            string options = question.Options.Select(question => $"{question.OptionCharachter}: {question.Text}").Aggregate((a, b) => $"{a}\n{b}");
            
            return await stepContext.PromptAsync(nameof(ChoicePrompt), 
                new PromptOptions { 
                    Prompt = MessageFactory.Text($"{question.Text}\n{options}"), 
                    Choices = ChoiceFactory.ToChoices(
                        question.Options.Select(o => o.OptionCharachter.ToString()).ToList()) }, token);
        }
        private async Task<DialogTurnResult> AnswerQuizQuestion(WaterfallStepContext stepContext, CancellationToken token)
        {
            var choice = (FoundChoice)stepContext.Result;
            var LLM = Factory.GetLLM(stepContext.Context.Activity.Conversation.Id);
            bool isCorrect = await LLM.SubmitAnswer(choice.Value, token);
            if (isCorrect)
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("Correct!"), token);
            else
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("Incorrect!"), token);
            return await stepContext.ReplaceDialogAsync(TakeQuiz, null, token);
        }
        private async Task<DialogTurnResult> StartFinishLesson(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var LLM = Factory.GetLLM(stepContext.Context.Activity.Conversation.Id);
            if(LLM.CurrentQuiz != null)
            {
                return await stepContext.ReplaceDialogAsync(TakeQuiz, null, cancellationToken);
            }
            return await stepContext.PromptAsync(nameof(ConfirmPrompt), new PromptOptions { Prompt = MessageFactory.Text("You have finished the lesson, would you like to start the next one?") });
        }
        private async Task<DialogTurnResult> RecieveFinishLesson(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var result = (bool)stepContext.Result;
            if (result)
            {
                return await stepContext.ReplaceDialogAsync(TakeLesson, null, cancellationToken);
            }
            return await stepContext.ReplaceDialogAsync(MainMenu, null, cancellationToken);
        }
        private async Task<DialogTurnResult> LoopBackChatWithCoachStep(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            // Optionally add a condition to break out of the loop
            return await stepContext.ReplaceDialogAsync(ChatWithCoach, null, cancellationToken);
        }
        private async Task<DialogTurnResult> StartTalkToCoach(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions {  }, cancellationToken); 
        }
        private async Task<DialogTurnResult> RecieveTalkToCoach(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var LLM = Factory.GetLLM(stepContext.Context.Activity.Conversation.Id);
            var msg = stepContext.Result as string;
            if (msg == null)
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("No message received."), cancellationToken);
            }
            else if (!msg.StartsWith("/"))
            {
                _ = Task.Factory.StartNew(async () =>
                {
                    CancellationTokenSource cts = new CancellationTokenSource();
                    // Start a task that sends typing activity every few seconds
                    RepeatTypingIndicatorAsync(stepContext.Context.Activity.GetConversationReference(), cts.Token);

                    try
                    {
                        // Perform the long-running operation
                        await LLM.ChatWithCoach(msg, cancellationToken);
                    }
                    finally
                    {
                        // Once the operation is complete, cancel the typing task
                        cts.Cancel();
                    }
                });
            }
            return await stepContext.NextAsync(null, cancellationToken);
        }

        private async Task RepeatTypingIndicatorAsync(ITurnContext context, CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var typing = Activity.CreateTypingActivity();
                    await context.SendActivityAsync(typing, cancellationToken);
                    await Task.Delay(2000, cancellationToken); // Delay for a few seconds before sending another typing indicator
                }
            }
            catch (TaskCanceledException)
            {
                // Ignore the cancellation exception
            }
        }
        private void RepeatTypingIndicatorAsync(ConversationReference convRef, CancellationToken cancellationToken)
        {
            _ = Adapter.ContinueConversationAsync("7a754f6d-f4ef-43be-8cef-70971fbbc055", convRef, async (turnContext, token) =>
            {
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var typing = Activity.CreateTypingActivity();
                        await turnContext.SendActivityAsync(typing, token);
                        await Task.Delay(2000, cancellationToken); // Delay for a few seconds before sending another typing indicator
                    }
                }
                catch (TaskCanceledException){ }
            }, cancellationToken);
        }
        protected async Task<DialogTurnResult> StartLesson(WaterfallStepContext stepContext, CancellationToken token)
        {
            var LLM = Factory.GetLLM(stepContext.Context.Activity.Conversation.Id);
            var lesson = await LLM.GetNextLesson();
            if(lesson == null)
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("No lesson available."), token);
                return await stepContext.ReplaceDialogAsync(nameof(MainMenu), null, token);
            }
            await stepContext.Context.SendActivityAsync(MessageFactory.Text(lesson.Name), token);
            return await stepContext.ReplaceDialogAsync(FollowPrompts, null, token);
        }
        protected async Task<DialogTurnResult> StartLessonPrompt(WaterfallStepContext stepContext, CancellationToken token)
        {
            var LLM = Factory.GetLLM(stepContext.Context.Activity.Conversation.Id);
            var prompt = LLM.GetNextPrompt();
            if (prompt == null)
            {
                return await stepContext.ReplaceDialogAsync(nameof(FinishLesson), null, token);
            }
            if (prompt.Type == PromptTypes.Lecture)
            {
                _ = Task.Factory.StartNew(async () =>
                {
                    CancellationTokenSource cts = new CancellationTokenSource();
                    // Start a task that sends typing activity every few seconds
                    RepeatTypingIndicatorAsync(stepContext.Context.Activity.GetConversationReference(), cts.Token);
                    try
                    {
                        await LLM.SendMessageForCurrentPrompt("", token);
                    }
                    finally
                    {
                        // Once the operation is complete, cancel the typing task
                        cts.Cancel();
                    }
                });
                return await stepContext.ReplaceDialogAsync(ContinueLesson, null, token);
            }
            else
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text(prompt.Text), token);
                return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { }, token);
            }
        }
        protected async Task<DialogTurnResult> RecieveLessonPrompt(WaterfallStepContext stepContext, CancellationToken token)
        {
            var LLM = Factory.GetLLM(stepContext.Context.Activity.Conversation.Id);
            var msg = stepContext.Result as string;
            if (msg == null)
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("No message received."), token);
            }
            else if(msg.ToLowerInvariant() == "/next")
            {
                return await stepContext.ReplaceDialogAsync(FollowPrompts, null, token);
            }
            else if(!msg.StartsWith("/"))
            {
                _ = Task.Factory.StartNew(async () =>
                {
                    CancellationTokenSource cts = new CancellationTokenSource();
                    // Start a task that sends typing activity every few seconds
                    RepeatTypingIndicatorAsync(stepContext.Context.Activity.GetConversationReference(), cts.Token);
                    try
                    {
                        await LLM.SendMessageForCurrentPrompt(msg, token);
                    }
                    finally
                    {
                        // Once the operation is complete, cancel the typing task
                        cts.Cancel();
                    }
                });
            }
            return await stepContext.ReplaceDialogAsync(ContinueLesson, null, token);
        }
        protected async Task<DialogTurnResult> ContinueLessonPrompt(WaterfallStepContext stepContext, CancellationToken token)
        {
            return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { }, token);
        }
        protected async Task<DialogTurnResult> LoopBackLessonPrompt(WaterfallStepContext stepContext, CancellationToken token)
        {
            return await stepContext.ReplaceDialogAsync(FollowPrompts, null, token);
        }
        protected override async Task<DialogTurnResult> OnBeginDialogAsync(DialogContext innerDc, object options, CancellationToken cancellationToken = default(CancellationToken))
        {
            var result = await InterruptAsync(innerDc, cancellationToken);
            if (result != null)
            {
                return result;
            }

            return await base.OnBeginDialogAsync(innerDc, options, cancellationToken);
        }

        protected override async Task<DialogTurnResult> OnContinueDialogAsync(DialogContext innerDc, CancellationToken cancellationToken = default(CancellationToken))
        {
            var result = await InterruptAsync(innerDc, cancellationToken);
            if (result != null)
            {
                return result;
            }

            return await base.OnContinueDialogAsync(innerDc, cancellationToken);
        }

        private async Task<DialogTurnResult> InterruptAsync(DialogContext innerDc, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (innerDc.Context.Activity.Type == ActivityTypes.Message)
            {
                var text = innerDc.Context.Activity.Text.ToLowerInvariant();

                if (text == "/logout")
                {
                    // The UserTokenClient encapsulates the authentication processes.
                    var userTokenClient = innerDc.Context.TurnState.Get<UserTokenClient>();
                    await userTokenClient.SignOutUserAsync(innerDc.Context.Activity.From.Id, ConnectionName, innerDc.Context.Activity.ChannelId, cancellationToken).ConfigureAwait(false);

                    await innerDc.Context.SendActivityAsync(MessageFactory.Text("You have been signed out."), cancellationToken);
                    return await innerDc.CancelAllDialogsAsync(cancellationToken);
                }
                else if(text == "/main")
                {
                    return await innerDc.ReplaceDialogAsync(MainMenu, null, cancellationToken);
                }
                else if (text.StartsWith("/lang"))
                {
                    text = text.Replace("/lang", "").Trim();
                    var LLM = Factory.GetLLM(innerDc.Context.Activity.Conversation.Id);
                    await LLM.SetLocale(text, cancellationToken);
                }
            }

            return null;
        }
        protected async override Task OnInitializeAsync(DialogContext dc)
        {
            var ConvRef = dc.Context.Activity.GetConversationReference();
            var id = dc.Context.Activity.Conversation.Id;
            Logger.LogInformation(id);
            var adapter = (AdapterWithErrorHandler)Adapter;
            
            Factory.GetLLM(id, new ActionBlock<CloneResponse>(async response =>
            {
                Logger.LogInformation(response.text);
                try
                {
                    string pattern = @"\[[^\]]*\]";
                    response.text = Regex.Replace(response.text, pattern, string.Empty);
                    await adapter.ContinueConversationAsync("7a754f6d-f4ef-43be-8cef-70971fbbc055", ConvRef, async (turnContext, token) =>
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Text(response.text), token);
                    }, default);
                    Logger.LogInformation("Message sent.");
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, ex.Message);
                }

            }));
            await base.OnInitializeAsync(dc);
        }
        private async Task<DialogTurnResult> InitialStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            await stepContext.Context.SendActivityAsync(MessageFactory.Text("Welcome to Coursware Coach! Please wait while we try to log you in."), cancellationToken);
            return await stepContext.PromptAsync(nameof(OAuthPrompt), new PromptOptions { }, cancellationToken);
        }
        private async Task<DialogTurnResult> DisplayCoachesStep(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var coaches = await CoachRepository.Get(token: cancellationToken);
            if(coaches.Items.Count == 0)
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("No coaches available."), cancellationToken);
                return await stepContext.EndDialogAsync(null, cancellationToken);
            }
            var options = new PromptOptions
            {
                Prompt = MessageFactory.Text("Please select a coach."),
                RetryPrompt = MessageFactory.Text("That was not a valid choice, please select a coach or type 'cancel'."),
                Choices = ChoiceFactory.ToChoices(coaches.Items.Select(c => c.Name).ToList()),
            };
            return await stepContext.PromptAsync(nameof(ChoicePrompt), options, cancellationToken);
        }
        private async Task<DialogTurnResult> DisplayCoursesStep(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var coaches = await CourseRepository.Get(token: cancellationToken);
            if (coaches.Items.Count == 0)
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("No courses available."), cancellationToken);
                return await stepContext.EndDialogAsync(null, cancellationToken);
            }
            var options = new PromptOptions
            {
                Prompt = MessageFactory.Text("Please select a course."),
                RetryPrompt = MessageFactory.Text("That was not a valid choice, please select a course or type 'cancel'."),
                Choices = ChoiceFactory.ToChoices(coaches.Items.Select(c => c.Name).ToList()),
            };
            return await stepContext.PromptAsync(nameof(ChoicePrompt), options, cancellationToken);
        }
        private async Task<DialogTurnResult> MainMenuOptions(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var options = new PromptOptions
            {
                Prompt = MessageFactory.Text("Please select a mode."),
                RetryPrompt = MessageFactory.Text("That was not a valid choice, please select a coach or type 'cancel'."),
                Choices = ChoiceFactory.ToChoices(["Coach", "Course"]),
            };
            return await stepContext.PromptAsync(nameof(ChoicePrompt), options, cancellationToken);
        }
        private async Task<DialogTurnResult> MainMenuChoice(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var choice = (FoundChoice)stepContext.Result;
            if(choice.Value == "Coach")
            {
                return await stepContext.ReplaceDialogAsync(PickCoach, null, cancellationToken);
            }
            else if(choice.Value == "Course")
            {
                return await stepContext.ReplaceDialogAsync(PickCourse, null, cancellationToken);
            }
            return await stepContext.ReplaceDialogAsync(MainMenu, null, cancellationToken);
        }
        private async Task<DialogTurnResult>  HandleCoachChoiceResultAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var choice = (FoundChoice)stepContext.Result;

            var coaches = await CoachRepository.Get(filter: c => c.Name == choice.Value, token: cancellationToken);
            if(coaches.Items.Count == 0)
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("Coach not found."), cancellationToken);
                return await stepContext.EndDialogAsync(null, cancellationToken);
            }
            var LLM = Factory.GetLLM(stepContext.Context.Activity.Conversation.Id);
            if (!LLM.IsLoggedIn)
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("Not logged in."), cancellationToken);
                return await stepContext.ReplaceDialogAsync(MainMenu, null, cancellationToken);
            }
            var coach = coaches.Items.Single();
            if (!await LLM.IsSubscribedToCoach(coach.Id, cancellationToken))
            {
                return await stepContext.ReplaceDialogAsync(SubscribeToCoach, coach.Id , cancellationToken);
            }
            CancellationTokenSource cts = new CancellationTokenSource();
            // Start a task that sends typing activity every few seconds
            var typingTask = RepeatTypingIndicatorAsync(stepContext.Context, cts.Token);
            try
            {
                await LLM.StartConversationWithCoach(coach.Id, cancellationToken);
            }
            finally
            {
                cts.Cancel();
                await typingTask;
            }
            if (LLM.CurrentConversationId == null)
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("Failed to start conversation with coach."), cancellationToken);
                return await stepContext.EndDialogAsync(null, cancellationToken);
            }
            // Continue the dialog or end it depending on your application's flow
            return await stepContext.ReplaceDialogAsync(ChatWithCoach, null, cancellationToken);
        }
        private async Task<DialogTurnResult> HandleCourseChoiceResultAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var choice = (FoundChoice)stepContext.Result;

            var coaches = await CourseRepository.Get(filter: c => c.Name == choice.Value, token: cancellationToken);
            if (coaches.Items.Count == 0)
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("Course not found."), cancellationToken);
                return await stepContext.EndDialogAsync(null, cancellationToken);
            }
            var LLM = Factory.GetLLM(stepContext.Context.Activity.Conversation.Id);
            if (!LLM.IsLoggedIn)
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("Not logged in."), cancellationToken);
            }
            var coach = coaches.Items.Single();
            if (!await LLM.IsSubscribedToCourse(coach.Id, cancellationToken))
            {
                return await stepContext.ReplaceDialogAsync(SubscribeToCourse, coach.Id, cancellationToken);
            }
            CancellationTokenSource cts = new CancellationTokenSource();
            // Start a task that sends typing activity every few seconds
            var typingTask = RepeatTypingIndicatorAsync(stepContext.Context, cts.Token);
            try
            {
                await LLM.StartCourse(coach.Id, cancellationToken);
            }
            finally
            {
                cts.Cancel();
                await typingTask;
            }
            if (LLM.CurrentConversationId == null)
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("Failed to start conversation with coach."), cancellationToken);
                return await stepContext.EndDialogAsync(null, cancellationToken);
            }
            // Continue the dialog or end it depending on your application's flow
            return await stepContext.ReplaceDialogAsync(TakeLesson, null, cancellationToken);
        }
        private async Task<DialogTurnResult> HandleAuthenticationResultAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var LLM = Factory.GetLLM(stepContext.Context.Activity.Conversation.Id);
            var result = stepContext.Result as TokenResponse;
            if (result != null)
            {
                CancellationTokenSource cts = new CancellationTokenSource();
                // Start a task that sends typing activity every few seconds
                var typingTask = RepeatTypingIndicatorAsync(stepContext.Context, cts.Token);
                try
                {
                    var userId = await GetUserId(result.Token);
                    if (userId != null)
                    {
                        var user = await LLM.Login(userId, cancellationToken);
                        if (user == null)
                        {
                            await stepContext.Context.SendActivityAsync(MessageFactory.Text("Login failed. Please try again."), cancellationToken);
                            return await stepContext.EndDialogAsync(null, cancellationToken);
                        }
                        await stepContext.Context.SendActivityAsync(MessageFactory.Text($"Welcome {user.FirstName}!"), cancellationToken);
                    }
                    // User is authenticated
                    return await stepContext.ReplaceDialogAsync(MainMenu, null, cancellationToken);
                }
                finally
                {
                    cts.Cancel();
                    await typingTask;
                }
            }

            // Authentication failed
            await stepContext.Context.SendActivityAsync(MessageFactory.Text("Authentication failed. Please try again."), cancellationToken);
            return await stepContext.ReplaceDialogAsync(LoginPickCoach, null, cancellationToken);
        }
        protected async Task<string?> GetAzureADB2CPublicKey()
        {
            using(var client = new HttpClient())
            {
                var mr = await client.GetStringAsync(MetadataUrl);
                var md = JObject.Parse(mr);
                var jwks_uri = md["jwks_uri"]?.ToString();
                if(jwks_uri == null)
                {
                    return null;
                }
                return await client.GetStringAsync(jwks_uri);
            }
        }
        protected async Task<string?> GetUserId(string jwtToken)
        {
            var handler = new JwtSecurityTokenHandler();
            var jsonWebKeys = new JsonWebKeySet(await GetAzureADB2CPublicKey());
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = jsonWebKeys.GetSigningKeys(),
                ValidateIssuer = true,
                ValidIssuer = IssuerUrl ,
                ValidateAudience = false,
                ValidateLifetime = true
            };

            SecurityToken validatedToken;
            var principal = handler.ValidateToken(jwtToken, validationParameters, out validatedToken);
            var jwtTokenValidated = validatedToken as JwtSecurityToken;
            if (jwtTokenValidated != null)
            {
                return jwtTokenValidated.Claims.SingleOrDefault(c => c.Type == "oid")?.Value;
            }
            return null;
        }
        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return await stepContext.EndDialogAsync(null, cancellationToken);
        }

    }
}
