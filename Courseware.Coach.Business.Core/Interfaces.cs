using Courseware.Coach.Core;
using Courseware.Coach.Data.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Courseware.Coach.Business.Core
{
    public interface IBusinessRepositoryFacade<TUoW>
        where TUoW : UnitOfWorkBase
    {
        TUoW? CreateUnitOfWork();
        string? AccessCode { get; set; }
        Task Delete(Guid id, TUoW? work = null, CancellationToken token = default);
    }
    public interface IOBusinessRepositoryFacade<out TEntity, TUoW> : IBusinessRepositoryFacade<TUoW>
        where TEntity : Item, new()
        where TUoW : UnitOfWorkBase
    {

    }
    public interface IIBusinessRepositoryFacade<in TEntity, TUoW> : IBusinessRepositoryFacade<TUoW>
       where TEntity : Item, new()
        where TUoW : UnitOfWorkBase
    {
        

    }
    public interface IBusinessRepositoryFacade<TEntity, TUoW> :
        IOBusinessRepositoryFacade<TEntity, TUoW>,
        IIBusinessRepositoryFacade<TEntity, TUoW>
        where TEntity : Item , new()
        where TUoW : UnitOfWorkBase
    {
        Task<ItemsResultSet<TEntity>> Get(TUoW? work = null,
            Pager? page = null,
            Expression<Func<TEntity, bool>>? filter = null,
            Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
            CancellationToken token = default);
        Task<int> Count(TUoW? work = null,
            Expression<Func<TEntity, bool>>? filter = null,
            CancellationToken token = default);
        Task<TEntity?> Get(Guid key, TUoW? work = null, CancellationToken token = default);
        Task<TEntity> Add(TEntity entity, TUoW? work = null, CancellationToken token = default);
        Task<TEntity> Update(TEntity entity,
            TUoW? work = null, CancellationToken token = default);
    }
    
}
