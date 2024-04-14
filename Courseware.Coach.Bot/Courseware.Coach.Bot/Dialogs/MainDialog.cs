// Generated with Bot Builder V4 SDK Template for Visual Studio CoreBot v4.22.0

using Courseware.Coach.Bot;
using Courseware.Coach.Core;
using Courseware.Coach.Data;
using Courseware.Coach.Data.Core;
using Courseware.Coach.LLM;
using Courseware.Coach.LLM.Core;
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
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
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
            if(LLMs.ContainsKey(id))
            {
                return LLMs[id];
            }
            LLMs[id] = Provider.GetRequiredService<ILLM>();
            if(response != null)
                LLMs[id].Conversation.LinkTo(response); 
            return LLMs[id];
        }
        public async Task DisconnectLLM(string id)
        {
            if (LLMs.ContainsKey(id))
            {
                await LLMs[id].DisposeAsync();
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
        protected IBotFrameworkHttpAdapter Adapter { get; }
        // Dependency injection uses this constructor to instantiate MainDialog
        public MainDialog(ILogger<MainDialog> logger, IConfiguration config, LLMFactory factory, IBotFrameworkHttpAdapter adapter, IRepository<UnitOfWork, CH> coachRepository)
            : base(nameof(MainDialog))
        { 
            Logger = logger;
            Logger.LogInformation("MainDialog constructor called.");
            MetadataUrl = config["Security:MetadataUrl"];
            IssuerUrl = config["Security:Issuer"];
            Factory = factory;
            CoachRepository = coachRepository;
            Adapter = adapter;
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
                HandleAuthenticationResultAsync,
                DisplayCoachesStep,
                HandleCoachChoiceResultAsync
            };
            var coachSteps = new WaterfallStep[] {
                StartTalkToCoach,
                RecieveTalkToCoach,
                LoopBackStep
            };
            AddDialog(new WaterfallDialog(LoginPickCoach, waterfallSteps));
            AddDialog(new WaterfallDialog(ChatWithCoach, coachSteps));
            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new ChoicePrompt(nameof(ChoicePrompt)));
            InitialDialogId = LoginPickCoach;

        }
        protected const string LoginPickCoach = nameof(LoginPickCoach);
        protected const string ChatWithCoach = nameof(ChatWithCoach);
        private async Task<DialogTurnResult> LoopBackStep(WaterfallStepContext stepContext, CancellationToken cancellationToken)
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
            if(msg == null)
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("No message received."), cancellationToken);
            }
            else
            {
                await LLM.ChatWithCoach(msg, cancellationToken);
            }
            return await stepContext.NextAsync(null, cancellationToken);

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
            await stepContext.Context.SendActivityAsync(MessageFactory.Text("Welcome to Coursware Couch! Please wait while we try to log you in."), cancellationToken);
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
        private async Task<DialogTurnResult> HandleCoachChoiceResultAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
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
            }
            await LLM.StartConversationWithCoach(coaches.Items.Single().Id, cancellationToken);
            if (LLM.CurrentConversationId == null)
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("Failed to start conversation with coach."), cancellationToken);
                return await stepContext.EndDialogAsync(null, cancellationToken);
            }
            // Continue the dialog or end it depending on your application's flow
            return await stepContext.ReplaceDialogAsync(ChatWithCoach, null, cancellationToken);
        }
        private async Task<DialogTurnResult> HandleAuthenticationResultAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var LLM = Factory.GetLLM(stepContext.Context.Activity.Conversation.Id);
            var result = stepContext.Result as TokenResponse;
            if (result != null)
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
                return await stepContext.NextAsync(result, cancellationToken);
            }

            // Authentication failed
            await stepContext.Context.SendActivityAsync(MessageFactory.Text("Authentication failed. Please try again."), cancellationToken);
            return await stepContext.NextAsync(null, cancellationToken);
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
