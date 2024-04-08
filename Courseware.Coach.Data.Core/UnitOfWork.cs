using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Courseware.Coach.Data.Core
{
    public static class ClaimsPrincipalExtentension
    {
        public static string? GetUserId(this ClaimsPrincipal? principal)
        {
            return principal?.Claims.FirstOrDefault(p => p.Type == "http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;
        }
    }
    public abstract class UnitOfWorkBase : IAsyncDisposable
    {
        protected IConfiguration Configuration { get; }
        protected UnitOfWorkBase(IConfiguration config)
        {
            Configuration = config;
        }
        public abstract Task SaveChanges(CancellationToken token = default);

        public virtual ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
    public interface IUnitOfWorkFactory<out TUoW>
        where TUoW : UnitOfWorkBase
    {
        TUoW CreateUnitOfWork();
    }
}
