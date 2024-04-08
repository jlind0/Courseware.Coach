using Courseware.Coach.Subscriptions.Core;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Checkout;
using Sub = Courseware.Coach.Core.Subscription;
using U = Courseware.Coach.Core.User;

namespace Courseware.Coach.Web.Controllers
{
    [Route("/api/stripehooks")]
    [ApiController]
    public class StripeWebhookController : Controller
    {
        protected string WebHookSecret { get; }
        protected ILogger Logger { get; }
        protected SessionService SessionService { get; }
        protected ISubscriptionManager SubscriptionManager { get; }
        public StripeWebhookController(IConfiguration configuration,
            ILogger<StripeWebhookController> logger, SessionService sessionService,
            ISubscriptionManager subscriptionManager)
        {
            WebHookSecret = configuration["Stripe:WebHookKey"] ?? throw new InvalidDataException();
            Logger = logger;
            SessionService = sessionService;
            SubscriptionManager = subscriptionManager;
        }
        [HttpPost]
        public async Task<IActionResult> Index()
        {
            try
            {
                var str = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
                Logger.LogInformation(str);
                var stripeEvent = EventUtility.ConstructEvent(str, Request.Headers["Stripe-Signature"], WebHookSecret);
                if (stripeEvent.Type == Events.CheckoutSessionCompleted)
                {
                    var session = stripeEvent.Data.Object as Session;
                    if (session == null)
                        throw new InvalidDataException();
                    var options = new SessionGetOptions();
                    options.AddExpand("line_items");

                    // Retrieve the session. If you require line items in the response, you may include them by expanding line_items.
                    var sessionWithLineItems = await SessionService.GetAsync(session.Id, options);
                    if (sessionWithLineItems.Metadata.TryGetValue(nameof(Sub.Id), out var subscriptionId)
                        && sessionWithLineItems.Metadata.TryGetValue(nameof(U.ObjectId), out var objectId))
                    {
                        await SubscriptionManager.FinishSubscription(Guid.Parse(subscriptionId), objectId);
                    }
                    else
                        throw new InvalidDataException();
                }
                return Ok();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                return BadRequest(ex);
            }
        }
    }
}
