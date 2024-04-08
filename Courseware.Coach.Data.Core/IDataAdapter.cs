using Courseware.Coach.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Courseware.Coach.Data.Core
{
    public struct Pager
    {
        public int Size { get; set; }
        public int Page { get; set; }
    }
    public class ItemsResultSet<T>
        where T : Item, new()
    {
        public ICollection<T> Items { get; set; } = [];
        public int? Count { get; set; }
        public int? PageSize { get; set; }
        public int? Page { get; set; }
    }
    public interface IRepository
    {
        string ContainerName { get; }
    }
    public interface IORepository<out TUoW> : IRepository
        where TUoW : UnitOfWorkBase
    {
        TUoW CreateUnitOfWork();
    }
    public interface IIRepository<in TUoW> : IRepository
        where TUoW : UnitOfWorkBase
    {
        Task Remove(Guid id, TUoW? uow = null, CancellationToken token = default);
    }
    public interface IRepository<TUoW> : IIRepository<TUoW>, IORepository<TUoW>
        where TUoW : UnitOfWorkBase
    {
    }
    public interface IIRepository<in TUoW, in T> : IIRepository<TUoW>
        where T : Item, new()
        where TUoW : UnitOfWorkBase
    {
        Task Add(T item, TUoW? uow = null, CancellationToken token = default);
        Task Update(T item, TUoW? uow = null, CancellationToken token = default);

    }
    public interface IRepository<TUoW, T> : IIRepository<TUoW, T>, IORepository<TUoW>, IRepository<TUoW>
        where TUoW : UnitOfWorkBase
        where T : Item, new()
    {
        Task<T?> Get(Guid id, TUoW? uow = null, CancellationToken token = default);
        Task<ItemsResultSet<T>> Get(
            TUoW? uow = null, 
            Pager? page = null,
            Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
            Expression<Func<T, bool>>? filter = null,
            CancellationToken token = default);
        Task<int> Count(TUoW? uow = null,
            Expression<Func<T, bool>>? filter = null,
            CancellationToken token = default);

    }
}
