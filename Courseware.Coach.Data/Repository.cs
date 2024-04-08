using Courseware.Coach.Core;
using Courseware.Coach.Data.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Courseware.Coach.Data
{
    public class UnitOfWorkFactory : IUnitOfWorkFactory<UnitOfWork>
    {
        protected IConfiguration Config { get; }
        public UnitOfWorkFactory(IConfiguration config)
        {
            Config = config;
        }
        public UnitOfWork CreateUnitOfWork()
        {
            return new UnitOfWork(Config);
        }
    }
    public abstract class Repository : IRepository<UnitOfWork>
    {
        protected async Task Use(Func<UnitOfWork, CancellationToken, Task> worker,
            UnitOfWork? work = null, CancellationToken token = default,
            bool saveChanges = false)
        {
            bool hasWork = work != null;
            work ??= UoWFactory.CreateUnitOfWork();
            try
            {
                await worker(work, token);
            }
            finally
            {
                if (!hasWork)
                {
                    if (saveChanges)
                        await work.SaveChanges(token);
                    await work.DisposeAsync();
                }
            }
        }
        public abstract string ContainerName { get; }
        protected IUnitOfWorkFactory<UnitOfWork> UoWFactory { get; }
        protected Repository(IUnitOfWorkFactory<UnitOfWork> uowFactory)
        {
            UoWFactory = uowFactory;
        }
        public virtual UnitOfWork CreateUnitOfWork()
        {
            return UoWFactory.CreateUnitOfWork();
        }

        public abstract Task Remove(Guid id, UnitOfWork? uow = null, CancellationToken token = default);
    }
    public class Repository<T> : Repository, IRepository<UnitOfWork, T>
        where T : Item, new()
    {
        public Repository(IUnitOfWorkFactory<UnitOfWork> uowFactory) : base(uowFactory)
        {
        }

        public Task Add(T item, UnitOfWork? uow = null, CancellationToken token = default)
        {
            Validator.ValidateObject(item, new ValidationContext(item), true);
            return Use(async (w, t) =>
            {
                await w.Context.AddAsync(item, t);
            }, uow, token, true);
        }

        public async Task<T?> Get(Guid id, UnitOfWork? uow = null, CancellationToken token = default)
        {
            T? entity = null;
            await Use(async (w, t) =>
            {
                entity = await w.Context.FindAsync<T>(id, t);
                
            }, uow, token);
            return entity;
        }

        public async Task<ItemsResultSet<T>> Get(UnitOfWork? uow = null, Pager? page = null, Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null, Expression<Func<T, bool>>? filter = null, CancellationToken token = default)
        {
            ItemsResultSet<T> results = new ItemsResultSet<T>();
            bool hasWork = uow != null;
            uow ??= UoWFactory.CreateUnitOfWork();
            try
            {
                await Use(async (w, t) =>
                {
                    IQueryable<T> query = w.Context.Set<T>();
                    await HydrateResultsSet(results, query, w, t, page, filter, orderBy);
                }, uow, token);
            }
            finally
            {
                if (!hasWork)
                    await uow.DisposeAsync();
            }
            return results;
        }

        public Task Update(T item, UnitOfWork? uow = null, CancellationToken token = default)
        {
            Validator.ValidateObject(item, new ValidationContext(item), true);
            return Use((w, t) =>
            {
                w.Context.Attach(item);
                w.Context.Update(item);
                return Task.CompletedTask;
            }, uow, token, true);
        }
        public override string ContainerName => typeof(T).Name;
        public override Task Remove(Guid id, UnitOfWork? uow = null, CancellationToken token = default)
        {
            return Use(async (w, t) =>
            {
                T? entity = await w.Context.FindAsync<T>(id, token);
                if (entity != null)
                {
                    w.Context.Remove(entity);
                }

            }, uow, token, true);
        }
        protected virtual async Task HydrateResultsSet(ItemsResultSet<T> results,
            IQueryable<T> query,
            UnitOfWork w,
            CancellationToken t,
            Pager? page = null,
            Expression<Func<T, bool>>? filter = null,
            Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
            IEnumerable<T>? properites = null)
        {
            if (filter != null)
            {
                query = query.Where(filter);
            }
            results.Count = await query.CountAsync(t);
            if (page != null)
            {
                int skip = page.Value.Size * (page.Value.Page - 1);
                int take = page.Value.Size;
                results.PageSize = page.Value.Size;
                results.Page = page.Value.Page;
                
                if (orderBy != null)
                    results.Items = await orderBy(query).Skip(skip).Take(take).ToListAsync(t);
                else
                    results.Items = await query.Skip(skip).Take(take).ToListAsync(t);
            }
            else if (orderBy != null)
                results.Items = await orderBy(query).ToListAsync(t);
            else
                results.Items = await query.ToListAsync(t);
        }

        public async Task<int> Count(UnitOfWork? uow = null, Expression<Func<T, bool>>? filter = null, CancellationToken token = default)
        {
            int count = 0;
            await Use(async (w, t) =>
            {
                IQueryable<T> query = w.Context.Set<T>();
                if (filter != null)
                    query = query.Where(filter);

                count = await query.CountAsync(t);
            }, uow, token);
            return count;
        }
    }
}
