// Generated with Bot Builder V4 SDK Template for Visual Studio CoreBot v4.22.0

using Azure.Storage.Blobs;
using Courseware.Coach.Bot.Bots;
using Courseware.Coach.Bot.Dialogs;
using Courseware.Coach.Business.Core;
using Courseware.Coach.Business;
using Courseware.Coach.Core;
using Courseware.Coach.Data.Core;
using Courseware.Coach.Data;
using Courseware.Coach.LLM.Core;
using Courseware.Coach.LLM;
using Courseware.Coach.Storage.Core;
using Courseware.Coach.Storage;
using Courseware.Coach.Subscriptions.Core;
using Courseware.Coach.Subscriptions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Stripe;
using System.Collections.Immutable;
using Stripe.Checkout;
using CH = Courseware.Coach.Core.Coach;
using Microsoft.Extensions.Configuration;

namespace Courseware.Coach.Bot
{
    public class Startup
    {
        protected IConfiguration Configuration { get; }
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }   
        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddHttpClient().AddControllers().AddNewtonsoftJson();
            // Create the Bot Framework Authentication to be used with the Bot Adapter.
            services.AddSingleton<BotFrameworkAuthentication, ConfigurationBotFrameworkAuthentication>();

            // Create the Bot Adapter with error handling enabled.
            services.AddSingleton<IBotFrameworkHttpAdapter, AdapterWithErrorHandler>();

            // Create the storage we'll be using for User and Conversation state. (Memory is great for testing purposes.)
            services.AddSingleton<IStorage, MemoryStorage>();
            StripeConfiguration.ApiKey = Configuration["Stripe:APIKey"];
            services.AddTransient<PriceService>();
            services.AddTransient<ProductService>();
            services.AddTransient<PaymentLinkService>();
            services.AddTransient<SessionService>();
            services.AddSingleton<BlobServiceClient>(x =>
                new BlobServiceClient(Configuration.GetConnectionString("ImageBlob"))
            );
            services.AddSingleton<IStorageBlob, StorageBlob>();
            services.AddSingleton<IUnitOfWorkFactory<UnitOfWork>, UnitOfWorkFactory>();
            services.AddSingleton<IRepository<UnitOfWork, User>, Repository<User>>();
            services.AddSingleton<IRepository<UnitOfWork, CH>, Repository<CH>>();
            services.AddSingleton<IRepository<UnitOfWork, Course>, Repository<Course>>();
            services.AddSingleton<ISubscriptionManager, SubscriptionManager>();
            services.AddSingleton<ICloneAI, CloneAI>();
            services.AddSingleton<ITTS, TTS>();
            services.AddSingleton<ITranslationService, TranslationService>();
            services.AddScoped<ILLM, LLM.LLM>();
            services.AddSingleton<LLMFactory>();
            // Create the User state. (Used in this bot's Dialog implementation.)
            services.AddSingleton<UserState>();

            // Create the Conversation state. (Used by the Dialog system itself.)
            services.AddSingleton<ConversationState>();


            // The MainDialog that will be run by the bot.
            services.AddScoped<MainDialog>();

            // Create the bot as a transient. In this case the ASP Controller is expecting an IBot.
            services.AddTransient<IBot, DialogAndWelcomeBot<MainDialog>>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseDefaultFiles()
                .UseStaticFiles()
                .UseWebSockets()
                .UseRouting()
                .UseAuthorization()
                .UseEndpoints(endpoints =>
                {
                    endpoints.MapControllers();
                });

            // app.UseHttpsRedirection();
        }
    }
}
