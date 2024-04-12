using Courseware.Coach.Business.Core;
using Courseware.Coach.Core;
using Courseware.Coach.Data.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Courseware.Coach.Business
{
    public class BusinessRepositoryFacade<T, TUoW> : IBusinessRepositoryFacade<T, TUoW>
        where TUoW : UnitOfWorkBase
        where T : Item, new()
    {
        protected IRepository<TUoW, T> Repository { get; }
        public BusinessRepositoryFacade(IRepository<TUoW, T> repository)
        {
            Repository = repository;
        }
        public string? AccessCode { get; set; }

        public virtual async Task<T> Add(T entity, TUoW? work = null, CancellationToken token = default)
        {
            await Repository.Add(entity, work, token);
            return entity;
        }

        public virtual Task<int> Count(TUoW? work = null, Expression<Func<T, bool>>? filter = null, CancellationToken token = default)
        {
            return Repository.Count(work, filter, token);
        }

        public virtual TUoW? CreateUnitOfWork()
        {
            return Repository.CreateUnitOfWork();
        }

        public virtual Task Delete(Guid id, TUoW? work = null, CancellationToken token = default)
        {
            return Repository.Remove(id, work, token);
        }


        public virtual Task<ItemsResultSet<T>> Get(TUoW? work = null, Pager? page = null, Expression<Func<T, bool>>? filter = null, Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null, CancellationToken token = default)
        {
            return Repository.Get(work, page, orderBy, filter, token);
        }

        public virtual Task<T?> Get(Guid key, TUoW? work = null, CancellationToken token = default)
        {
            return Repository.Get(key, work, token);
        }

        public virtual async Task<T> Update(T entity, TUoW? work = null, CancellationToken token = default)
        {
            await Repository.Update(entity, work, token);
            return entity;
        }
    }
}
