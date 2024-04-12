using Courseware.Coach.Business.Core;
using Courseware.Coach.Core;
using Courseware.Coach.Data;
using Courseware.Coach.Data.Core;
using Courseware.Coach.LLM.Core;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using CH = Courseware.Coach.Core.Coach;

namespace Courseware.Coach.ViewModels
{
    public class LoadParameters<TEntity>
        where TEntity : Item, new()
    {
        public Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? OrderBy { get; set; }
        public Expression<Func<TEntity, bool>>? Filter { get; set; }
        public Pager? Pager { get; set; }
    }
    public class ClonesViewModel : ReactiveObject
    {
        protected ICloneAI CloneAI { get; }
        public Interaction<string, bool> Alert { get; } = new Interaction<string, bool>();
        protected IBusinessRepositoryFacade<CH, UnitOfWork> CoachRepository { get; }
        public ObservableCollection<Clone> Clones { get; } = new ObservableCollection<Clone>();
        public ReactiveCommand<Unit, Unit> Load { get; }
        public ClonesViewModel(ICloneAI cloneAI, IBusinessRepositoryFacade<CH, UnitOfWork> coachRepository)
        {
            CloneAI = cloneAI;
            CoachRepository = coachRepository;
            Load = ReactiveCommand.CreateFromTask(DoLoad);
        }
        protected async Task DoLoad(CancellationToken token)
        {
            try
            {
                Clones.Clear();
                var coaches = await CoachRepository.Get(token: token);
                foreach (var coach in coaches.Items.DistinctBy(p => p.Slug))
                {
                    var clone = await CloneAI.GetClone(coach.APIKey, coach.Slug, token);
                    if (clone != null)
                        Clones.Add(clone);
                }
            }
            catch(Exception ex)
            {
                await Alert.Handle(ex.Message).GetAwaiter();
            }
        }
    }
}
