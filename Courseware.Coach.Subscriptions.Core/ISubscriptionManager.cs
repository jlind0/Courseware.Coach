using Courseware.Coach.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Courseware.Coach.Subscriptions.Core
{
    public interface ISubscriptionManager
    {
        Task<Subscription?> StartSubscribeToCourse(Guid courseId, string objectId, CancellationToken token = default);
        Task<Subscription?> StartSubscribeToCoach(Guid coachId, string objectId, CancellationToken token = default);
        Task<Subscription?> FinishSubscription(Guid subscriptionId, string objectId, decimal total, CancellationToken token = default);
        Task<Subscription?> UpdateSubscription(Subscription subscription, string objectId, CancellationToken token = default);
        Task CancelSubscription(Guid subscriptionId, string objectId, CancellationToken token = default);
        Task<bool> IsSubscribedToCourse(Guid courseId, string objectId, CancellationToken token = default);
        Task<bool> IsSubscribedToCoach(Guid coachId, string objectId, CancellationToken token = default);
        Task<Subscription?> GetCurrentSubscriptionForCourse(Guid courseId, string objectId, CancellationToken token = default);
        Task<Subscription?> GetCurrentSubscriptionForCoach(Guid coachId, string objectId, CancellationToken token = default);
        Task SetPriceForCourse(Guid courseId, decimal price, CancellationToken token = default);
        Task SetPriceForCoach(Guid coachId, decimal price, CancellationToken token = default);
        Task<PayoutAccount?> AddPayoutAccount(Guid coachId, PayoutAccount account, CancellationToken token = default);
        Task<decimal?> PayoutCoach(Guid coachId, CancellationToken token = default);
    }
}
