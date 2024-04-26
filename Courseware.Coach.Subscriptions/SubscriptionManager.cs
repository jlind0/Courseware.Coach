using Courseware.Coach.Core;
using Courseware.Coach.Data;
using Courseware.Coach.Data.Core;
using Courseware.Coach.Subscriptions.Core;
using Stripe;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sub = Courseware.Coach.Core.Subscription;
using CH = Courseware.Coach.Core.Coach;
using Stripe.Checkout;
using Microsoft.Extensions.Configuration;

namespace Courseware.Coach.Subscriptions
{
    public class SubscriptionManager : ISubscriptionManager
    {
        protected IRepository<UnitOfWork, User> UserRepo { get; }
        protected IRepository<UnitOfWork, CH> CoachRepo { get; }
        protected IRepository<UnitOfWork, Course> CourseRepo { get; }
        protected ProductService ProductService { get; }
        protected PriceService PriceService { get; }
        protected PaymentLinkService PaymentLinkService { get; }
        protected SessionService SessionService { get; }
        protected AccountService AccountService { get; }
        protected ExternalAccountService ExtAccountService { get; }
        protected PayoutService PayoutService { get; }
        protected string PaymentSuccessUri { get; }
        public SubscriptionManager(IRepository<UnitOfWork, User> userRepo, IConfiguration config,
            IRepository<UnitOfWork, CH> coachRepo, IRepository<UnitOfWork, Course> courseRepo, 
            ProductService productService, PriceService priceService, PaymentLinkService paymentLinkService, SessionService sessionService, AccountService accountService, PayoutService payoutService, ExternalAccountService extAccountService)
        {
            UserRepo = userRepo;
            CoachRepo = coachRepo;
            CourseRepo = courseRepo;
            ProductService = productService;
            PriceService = priceService;
            PaymentLinkService = paymentLinkService;
            SessionService = sessionService;
            AccountService = accountService;
            PayoutService = payoutService;
            ExtAccountService = extAccountService;
            PaymentSuccessUri = config["Stripe:SuccessUrl"] ?? throw new InvalidDataException();

        }
        public async Task<bool> IsSubscribedToCoach(Guid coachId, string objectId, CancellationToken token = default)
        {
            var users = await UserRepo.Get(filter: u => u.ObjectId == objectId, token: token);
            var user = users.Items.SingleOrDefault();
            if(user != null && user.Roles.Any(r => r == "Admin" || r == $"Admin:Coach:{coachId}"))
            {
                return true;
            }
              
            return (await GetCurrentSubscriptionForCoach(coachId, objectId, token)) != null;
        }

        public async Task<bool> IsSubscribedToCourse(Guid courseId, string objectId, CancellationToken token = default)
        {
            var users = await UserRepo.Get(filter: u => u.ObjectId == objectId, token: token);
            var user = users.Items.SingleOrDefault();
            if (user != null && user.Roles.Any(r => r == "Admin" || r == $"Admin:Course:{courseId}"))
            {
                return true;
            }
            return (await GetCurrentSubscriptionForCourse(courseId, objectId, token)) != null;
        }
        protected async Task CreateProduct(IPriced entity, bool isSubscription = false, CancellationToken token = default)
        {
            var prod = new ProductCreateOptions()
            {
                Name = entity.Name
            };
            var product = await ProductService.CreateAsync(prod, cancellationToken: token);
            if (product != null)
            {
                entity.StripeProductId = product.Id;
                await CreatePrice(entity, isSubscription, token);
            }
        }
        protected async Task CreatePrice(IPriced entity, bool isSubscription = false, CancellationToken token = default)
        {
            if (!string.IsNullOrWhiteSpace(entity.StripeProductId) && entity.Price != null)
            {
                var price = new PriceCreateOptions()
                {
                    UnitAmountDecimal = entity.Price * 100,
                    Currency = "usd",
                    Product = entity.StripeProductId
                };
                if (isSubscription)
                {
                    price.Recurring = new PriceRecurringOptions()
                    {
                        Interval = "month"
                    };
                }
                var p = await PriceService.CreateAsync(price, cancellationToken: token);
                if (p != null)
                {
                    entity.StripePriceId = p.Id;
                    var options = new PaymentLinkCreateOptions();
                    options.LineItems =
                    [
                        new PaymentLinkLineItemOptions()
                        {
                            Price = entity.StripePriceId,
                            Quantity = 1
                        }
                    ];
                    var link = await PaymentLinkService.CreateAsync(options, cancellationToken: token);
                    entity.StripeUrl = link.Id;
                }

            }
        }
        public async Task SetPriceForCoach(Guid coachId, decimal price, CancellationToken token = default)
        {
            await using(var uow = CoachRepo.CreateUnitOfWork())
            {
                var coach = await CoachRepo.Get(coachId, uow, token: token);
                if(coach == null)
                {
                    throw new ArgumentException("Coach not found");
                }
                if(coach.StripeProductId == null)
                {
                    await CreateProduct(coach, true, token);
                }
                if(coach.Price != price)
                {
                    coach.Price = price;
                    await CreatePrice(coach, true, token);
                }
                await CoachRepo.Update(coach, uow, token);
                await uow.SaveChanges(token);
            }
        }

        public async Task SetPriceForCourse(Guid courseId, decimal price, CancellationToken token = default)
        {
            await using (var uow = CourseRepo.CreateUnitOfWork())
            {
                var coach = await CourseRepo.Get(courseId, uow, token: token);
                if (coach == null)
                {
                    throw new ArgumentException("Coach not found");
                }
                if (coach.StripeProductId == null)
                {
                    await CreateProduct(coach, false, token);
                }
                if (coach.Price != price)
                {
                    coach.Price = price;
                    await CreatePrice(coach, false, token);
                }
                await CourseRepo.Update(coach, uow, token);
                await uow.SaveChanges(token);
            }
        }

        public async Task<Sub?> StartSubscribeToCoach(Guid coachId, string objectId, CancellationToken token = default)
        {
            if (await IsSubscribedToCoach(coachId, objectId, token))
                return null;
            await using(var uow = UserRepo.CreateUnitOfWork())
            {
                var users = await UserRepo.Get(filter: u => u.ObjectId == objectId, uow: uow, token: token);
                if(users.Count == 0)
                {
                    throw new ArgumentException("User not found");
                }
                var user = users.Items.Single();
                var coach = await CoachRepo.Get(coachId, uow, token: token);
                if (coach == null)
                {
                    throw new ArgumentException("Coach not found");
                }
                var sub = new Sub()
                {
                    CoachId = coachId,
                    IsFunded = false
                };
                await HydrateSubscription(sub, coach, user.ObjectId, true, token);
                user.Subscriptions.Add(sub);
                await UserRepo.Update(user, uow, token);
                await uow.SaveChanges(token);
                return sub;
            }
        }
        protected async Task HydrateSubscription(Sub subscription, IPriced priced, string objectId, bool isSubscription = false, CancellationToken token = default)
        {
            subscription.Price = priced.Price;
            var opts = new Stripe.Checkout.SessionCreateOptions()
            {
                Mode = !isSubscription ? "payment" : "subscription",
                SuccessUrl = PaymentSuccessUri,
                LineItems = new List<SessionLineItemOptions>()
                {
                    new SessionLineItemOptions()
                    {
                        Price = priced.StripePriceId,
                        Quantity = 1
                    }
                },
                Metadata = new Dictionary<string, string>()
                {
                    {nameof(Sub.Id), subscription.Id.ToString() },
                    {nameof(User.ObjectId), objectId }
                }
            };
            var session = await SessionService.CreateAsync(opts, cancellationToken: token);
            subscription.StripeSessionUrl = session.Url;
        }
        public async Task<Sub?> StartSubscribeToCourse(Guid courseId, string objectId, CancellationToken token = default)
        {
            if (await IsSubscribedToCourse(courseId, objectId, token))
                return null;
            await using (var uow = UserRepo.CreateUnitOfWork())
            {
                var users = await UserRepo.Get(filter: u => u.ObjectId == objectId, uow: uow, token: token);
                if (users.Count == 0)
                {
                    throw new ArgumentException("User not found");
                }
                var user = users.Items.Single();
                var coach = await CourseRepo.Get(courseId, uow, token: token);
                if (coach == null)
                {
                    throw new ArgumentException("Coach not found");
                }
                var sub = new Sub()
                {
                    CourseId = courseId,
                    IsFunded = false
                };
                await HydrateSubscription(sub, coach, objectId, false, token);
                user.Subscriptions.Add(sub);
                await UserRepo.Update(user, uow, token);
                await uow.SaveChanges(token);
                return sub;
            }
        }

        public async Task<Sub?> UpdateSubscription(Sub subscription, string objectId, CancellationToken token = default)
        {
            await using(var uow = UserRepo.CreateUnitOfWork())
            {
                var users = await UserRepo.Get(filter: u => u.ObjectId == objectId, uow: uow, token: token);
                if (users.Count == 0)
                {
                    throw new ArgumentException("User not found");
                }
                var user = users.Items.Single();
                var sub = user.Subscriptions.Single(s => s.Id == subscription.Id);
                sub.CurrentLessonId = subscription.CurrentLessonId;
                sub.Locale = subscription.Locale;
                sub.VoiceName = subscription.VoiceName;
                sub.ConversationId = subscription.ConversationId;
                await UserRepo.Update(user, uow, token);
                await uow.SaveChanges(token);
                return sub;
            }
        }

        public async Task<Sub?> FinishSubscription(Guid subscriptionId, string objectId, decimal total, CancellationToken token = default)
        {
            await using (var uow = UserRepo.CreateUnitOfWork())
            {
                var users = await UserRepo.Get(filter: u => u.ObjectId == objectId, uow: uow, token: token);
                if (users.Count == 0)
                {
                    throw new ArgumentException("User not found");
                }
                var user = users.Items.Single();
                var sub = user.Subscriptions.Single(s => s.Id == subscriptionId);
                IPriced? priced = null;
                int? daysToComplete = null;
                if(sub.CourseId != null)
                {
                    var course = await CourseRepo.Get(sub.CourseId.Value, uow, token: token);
                    priced = course;
                    daysToComplete = course?.DaysToComplete;
                }
                else if(sub.CoachId != null)
                {
                    priced = await CoachRepo.Get(sub.CoachId.Value, uow, token: token);
                    sub.Payments.Add(new RecurringPayment()
                    {
                        Amount = total,
                        PaymentDate = DateTime.UtcNow
                    });
                }
                if(priced == null)
                {
                    throw new ArgumentException("Course or Coach not found");
                }
                sub.Price = total;
                sub.IsFunded = true;
                sub.IsTrial = false;
                sub.StartDate = DateTime.UtcNow;
                sub.EndDate = DateTime.UtcNow.AddDays(daysToComplete ?? 30);
                await UserRepo.Update(user, uow, token);
                await uow.SaveChanges(token);
                return sub;
            }
        }

        public async Task<Sub?> GetCurrentSubscriptionForCourse(Guid courseId, string objectId, CancellationToken token = default)
        {
            var users = await UserRepo.Get(filter: u => u.ObjectId == objectId, token: token);
            if (users.Count > 0)
            {
                foreach (var sub in users.Items.Single().Subscriptions.Where(
                    s => s.CourseId == courseId && (s.IsFunded || s.IsTrial == true)).OrderByDescending(s => s.EndDate))
                {
                    if (sub.StartDate == null && sub.EndDate == null)
                        return sub;
                    if (sub.StartDate <= DateTime.UtcNow && sub.EndDate >= DateTime.UtcNow)
                    {
                        return sub;
                    }
                }
            }
            return null;
        }

        public async Task<Sub?> GetCurrentSubscriptionForCoach(Guid coachId, string objectId, CancellationToken token = default)
        {
            var users = await UserRepo.Get(filter: u => u.ObjectId == objectId, token: token);
            if (users.Count > 0)
            {
                var user = users.Items.Single();
                foreach (var sub in user.Subscriptions.Where(
                    s => s.CoachId == coachId && (s.IsFunded || s.IsTrial == true)).OrderByDescending(s => s.EndDate))
                {
                    if (sub.StartDate == null && sub.EndDate == null)
                        return sub;
                    if (sub.StartDate <= DateTime.UtcNow && sub.EndDate >= DateTime.UtcNow)
                    {
                        return sub;
                    }
                }
            }
            return null;
        }

        public async Task<PayoutAccount?> AddPayoutAccount(Guid coachId, PayoutAccount account, CancellationToken token = default)
        {
            var coach = await CoachRepo.Get(coachId, token: token);
            if(coach == null)
                throw new ArgumentException("Coach not found");
            var totalPercent = coach.PayoutAccounts.Sum(p => p.PayoutPercentage);
            totalPercent += account.PayoutPercentage;
            if(totalPercent > 1)
                throw new ArgumentException("Total payout percentage cannot exceed 100%");
            var options = new AccountCreateOptions()
            {
                Type = "custom",
                Country = account.Country,
                Email = account.Email,
                BusinessType = account.AccountType.ToString(),
                Capabilities = new AccountCapabilitiesOptions
                {
                    Transfers = new AccountCapabilitiesTransfersOptions
                    {
                        Requested = true
                    }
                }
            };
            var acct = await AccountService.CreateAsync(options, cancellationToken: token);
            if(acct == null)
                throw new InvalidOperationException("Account creation failed");
            account.AccountId = acct.Id;
            var boptions = new ExternalAccountCreateOptions()
            {

                ExternalAccount = new AccountBankAccountOptions()
                {
                    AccountNumber = account.AccountNumber,
                    Country = account.Country,
                    Currency = account.Currency,
                    AccountHolderName = account.AccountHolderName,
                    AccountHolderType = account.AccountHolderType,
                    RoutingNumber = account.RoutingNumber
                }
            };
            await ExtAccountService.CreateAsync(account.AccountId, boptions, cancellationToken: token);
            coach.PayoutAccounts.Add(account);
            await CoachRepo.Update(coach, token: token);
            return account;
        }

        public Task<decimal?> PayoutCoach(Guid coachId, CancellationToken token = default)
        {
            throw new NotImplementedException();
        }

        public async Task CancelSubscription(Guid subscriptionId, string objectId, CancellationToken token = default)
        {
            var users = await UserRepo.Get(filter: u => u.ObjectId == objectId, token: token);
            var user = users.Items.Single();
            var sub = user.Subscriptions.Single(s => s.Id == subscriptionId);
            sub.IsFunded = false;
            await UserRepo.Update(user, token: token);
        }

        public async Task<Sub?> StartCourseTrial(Guid courseId, string objectId, CancellationToken token = default)
        {
            if(await IsCourseTrialEligble(courseId, objectId, token))
            {
                var users = await UserRepo.Get(filter: u => u.ObjectId == objectId, token: token);
                var user = users.Items.Single();
                var course = await CourseRepo.Get(courseId, token: token);
                if(course == null)
                    throw new ArgumentException("Course not found");
                Sub sub = new Sub()
                {
                    CourseId = course.Id,
                    IsTrial = true,
                    StartDate = DateTime.UtcNow,
                    EndDate = DateTime.UtcNow.AddDays(1)
                };
                user.Subscriptions.Add(sub);
                await UserRepo.Update(user, token: token);
                return sub;
            }
            return null;
        }

        public async Task<Sub?> StartCoachTrial(Guid coachId, string objectId, CancellationToken token = default)
        {
            if(await IsCoachTrialEligble(coachId, objectId, token))
            {
                var users = await UserRepo.Get(filter: u => u.ObjectId == objectId, token: token);
                var user = users.Items.Single();
                var coach = await CoachRepo.Get(coachId, token: token);
                if(coach == null)
                    throw new ArgumentException("Coach not found");
                Sub sub = new Sub()
                {
                    CoachId = coachId,
                    IsTrial = true,
                    StartDate = DateTime.UtcNow,
                    EndDate = DateTime.UtcNow.AddMinutes(coach.DurationOfTrialInMinutes ?? 180)
                };
                user.Subscriptions.Add(sub);
                await UserRepo.Update(user, token: token);
                return sub;
            }
            return null;
        }

        public async Task<bool> IsCourseTrialEligble(Guid courseId, string objectId, CancellationToken token = default)
        {
            var users = await UserRepo.Get(filter: u => u.ObjectId == objectId, token: token);
            var user = users.Items.Single();
            if (user.Roles.Any(r => r == "Admin" || r == $"Admin:Course:{courseId}"))
            {
                return false;
            }
            var course = await CourseRepo.Get(courseId, token: token);
            if (course == null)
            {
                throw new ArgumentException("Course not found");
            }
            if (course.IsTrialEligible == false)
            {
                return false;
            }
            if (user.Subscriptions.Any(s => s.CourseId == courseId && (s.IsTrial == true || s.IsFunded)))
                return false;
            return true;
        }

        public async Task<bool> IsCoachTrialEligble(Guid coachId, string objectId, CancellationToken token = default)
        {
            var users = await UserRepo.Get(filter: u => u.ObjectId == objectId, token: token);
            var user = users.Items.Single();
            if (user.Roles.Any(r => r == "Admin" || r == $"Admin:Coach:{coachId}"))
            {
                return false;
            }
            var coach = await CoachRepo.Get(coachId, token: token);
            if (coach == null)
            {
                throw new ArgumentException("Coach not found");
            }
            if (coach.IsTrialEligible == false)
            {
                return false;
            }
            if (user.Subscriptions.Any(s => s.CoachId == coachId && (s.IsTrial == true || s.IsFunded)))
                return false;
            return true;
        }
    }
}
