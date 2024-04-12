using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Courseware.Coach.ViewModels
{
    public interface ISecurityFactory
    {
        Task<ClaimsPrincipal?> GetPrincipal();
    }
    public class SecurityFactory : ISecurityFactory
    {
        protected IServiceProvider ServiceProvider { get; }
        public SecurityFactory(IServiceProvider provider)
        {
            ServiceProvider = provider;
        }
        public async Task<ClaimsPrincipal?> GetPrincipal()
        {
            var authState = ServiceProvider.GetService<AuthenticationStateProvider>();
            bool isBlazor = authState != null;
            if (authState != null)
            {
                try
                {
                    var state = await authState.GetAuthenticationStateAsync();
                    return state.User;
                }
                catch
                {
                    isBlazor = false;
                }
            }
            if (!isBlazor)
            {
                var httpContext = ServiceProvider.GetService<IHttpContextAccessor>();
                if (httpContext != null)
                {
                    return httpContext.HttpContext.User;
                }
                else
                    return Thread.CurrentPrincipal as ClaimsPrincipal;
            }
            return null;
        }
    }
    public class ViewModelQuery<T>
        where T : ReactiveObject
    {
        public ICollection<T> Data { get; set; } = null!;
        public int Count { get; set; }
    }
}
