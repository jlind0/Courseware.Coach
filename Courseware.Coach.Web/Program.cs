using Invio.Extensions.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using Microsoft.Extensions.DependencyInjection;
using Courseware.Coach.Web.Authorization;
using Courseware.Coach.Data.Core;
using Courseware.Coach.Data;
using Courseware.Coach.Core;
using Courseware.Coach.Subscriptions.Core;
using Courseware.Coach.Subscriptions;
using Stripe;
using Stripe.Checkout;
using Courseware.Coach.LLM.Core;
using Courseware.Coach.LLM;
using Courseware.Coach.ViewModels;
using Courseware.Coach.Web.Pages;
using Courseware.Coach.Business.Core;
using Courseware.Coach.Business;
using Courseware.Coach.Storage.Core;
using Courseware.Coach.Storage;
using MimeDetective;
using MimeDetective.Storage;
using System.Collections.Immutable;
using Azure.Storage.Blobs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearerQueryStringAuthentication()
    .AddMicrosoftIdentityWebApi(builder.Configuration)
                    .EnableTokenAcquisitionToCallDownstreamApi()
                        .AddSessionTokenCaches();
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration).EnableTokenAcquisitionToCallDownstreamApi();

builder.Services.AddAuthorization();
builder.Services.AddTokenAcquisition();
builder.Services.AddControllers().AddNewtonsoftJson();
builder.Services.AddMvc().AddNewtonsoftJson();
builder.Services.AddRazorPages().AddMicrosoftIdentityUI();
builder.Services.AddTelerikBlazor();
builder.Services.AddServerSideBlazor().AddHubOptions(options =>
{
    options.MaximumReceiveMessageSize = null;

}).AddMicrosoftIdentityConsentHandler();
builder.Services.AddSignalR();
StripeConfiguration.ApiKey = builder.Configuration["Stripe:APIKey"];
builder.Services.AddTransient<PriceService>();
builder.Services.AddTransient<ProductService>();
builder.Services.AddTransient<PaymentLinkService>();
builder.Services.AddTransient<SessionService>();
builder.Services.AddSingleton<BlobServiceClient>(x =>
    new BlobServiceClient(builder.Configuration.GetConnectionString("ImageBlob"))
);
builder.Services.AddSingleton<IStorageBlob, StorageBlob>();
builder.Services.AddSingleton<ContentInspector>(x =>
{
    var Extensions = new[]{
    "jpg", "jpeg","png", "gif","bmp", "tiff", "webp", "svg"
    }.ToImmutableHashSet(StringComparer.InvariantCultureIgnoreCase);
    var Inspector = new ContentInspectorBuilder()
    {

        Definitions = new MimeDetective.Definitions.ExhaustiveBuilder()
        {
            UsageType = MimeDetective.Definitions.Licensing.UsageType.PersonalNonCommercial
        }.Build().ScopeExtensions(Extensions).TrimMeta().TrimDescription().ToImmutableArray()
    }.Build();
    return Inspector;
});
builder.Services.AddScoped<ISecurityFactory, SecurityFactory>();
builder.Services.AddSingleton<IUnitOfWorkFactory<UnitOfWork>, UnitOfWorkFactory>();
builder.Services.AddSingleton<IRepository<UnitOfWork, User>, Repository<User>>();
builder.Services.AddSingleton<IRepository<UnitOfWork, Coach>, Repository<Coach>>();
builder.Services.AddSingleton<IRepository<UnitOfWork, Course>, Repository<Course>>();
builder.Services.AddSingleton<ISubscriptionManager, SubscriptionManager>();
builder.Services.AddScoped<IBusinessRepositoryFacade<Course, UnitOfWork>, CourseFacade>();
builder.Services.AddScoped<IBusinessRepositoryFacade<Coach, UnitOfWork>, CoachFacade>();
builder.Services.AddScoped<IBusinessRepositoryFacade<User, UnitOfWork>, BusinessRepositoryFacade<User, UnitOfWork>>();
builder.Services.AddSingleton<ICloneAI, CloneAI>();
builder.Services.AddSingleton<ITTS, TTS>();
builder.Services.AddSingleton<ITranslationService, TranslationService>();
builder.Services.AddTransient<AlertView.AlertViewModel>();
builder.Services.AddTransient<ClonesViewModel>();
builder.Services.AddTransient<CoachesViewModel>();
builder.Services.AddTransient<UsersViewModel>();
builder.Services.AddTransient<UserAdminLoaderViewModel>();
builder.Services.AddTransient<UserViewModel>();
builder.Services.AddTransient<CoachLoaderViewModel>();
builder.Services.AddTransient<CoursesViewModel>();
builder.Services.AddLogging();
builder.Services.AddSession();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<RolePopulationMiddleware>();

app.MapControllers();
app.UseEndpoints(endpoints => endpoints.MapControllers());
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
