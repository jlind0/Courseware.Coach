using Courseware.Coach.Core;
using Courseware.Coach.Data;
using Courseware.Coach.Data.Core;
using Courseware.Coach.Subscriptions.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CH = Courseware.Coach.Core.Coach;

namespace Courseware.Coach.Business
{
    public class CourseFacade : BusinessRepositoryFacade<Course, UnitOfWork>
    {
        protected ISubscriptionManager SubscriptionManager { get; } 
        public CourseFacade(IRepository<UnitOfWork, Course> repository, ISubscriptionManager subManager) : base(repository)
        {
            SubscriptionManager = subManager;
        }
        public override async Task<Course> Add(Course entity, UnitOfWork? work = null, CancellationToken token = default)
        {
            var course = await base.Add(entity, work, token);
            if (course != null && course.Price != null)
                await SubscriptionManager.SetPriceForCourse(course.Id, course.Price.Value, token);
            return course ?? throw new InvalidDataException();
        }
        public override async Task<Course> Update(Course entity, UnitOfWork? work = null, CancellationToken token = default)
        {
            var oldCourse = await Get(entity.Id, work, token);
            if(oldCourse == null)
                throw new InvalidDataException();
            if (oldCourse.Price != entity.Price)
                await SubscriptionManager.SetPriceForCourse(entity.Id, entity.Price ?? 0m, token);
            return await base.Update(entity, work, token);
        }
    }
    public class CoachFacade : BusinessRepositoryFacade<CH, UnitOfWork>
    {
        protected ISubscriptionManager SubscriptionManager { get; }
        public CoachFacade(IRepository<UnitOfWork, CH> repository, ISubscriptionManager subscriptionManager) : base(repository)
        {
            SubscriptionManager = subscriptionManager;
        }
        public override async Task<CH> Add(CH entity, UnitOfWork? work = null, CancellationToken token = default)
        {
            var coach = await base.Add(entity, work, token);
            if (coach != null && coach.Price != null)
                await SubscriptionManager.SetPriceForCoach(coach.Id, coach.Price.Value, token);
            return coach ?? throw new InvalidDataException();
        }
        public override async Task<CH> Update(CH entity, UnitOfWork? work = null, CancellationToken token = default)
        {
            var oldCoach = await Get(entity.Id, work, token);
            if (oldCoach == null)
                throw new InvalidDataException();
            if (oldCoach.Price != entity.Price)
                await SubscriptionManager.SetPriceForCoach(entity.Id, entity.Price ?? 0m, token);
            return await base.Update(entity, work, token);
        }
    }
}
