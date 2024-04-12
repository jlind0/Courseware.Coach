using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Courseware.Coach.Core;
using Microsoft.AspNetCore.Components.Authorization;

namespace Courseware.Coach.Web.Authorization
{

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
    public class RolePopulationMiddleware
    {
        private readonly RequestDelegate _next;
        public RolePopulationMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.User.Identity != null && context.User.Identity.IsAuthenticated)
            {
                var identity = context.User.Identity as ClaimsIdentity;
                if (identity != null && identity.HasClaim(c => c.Type == "extension_Roles"))
                {
                    var rolesClaim = context.User.FindFirst(c => c.Type == "extension_Roles") ?? throw new InvalidDataException();
                    var roles = rolesClaim.Value.Split(',').Select(role => role.Trim());
                    foreach (var role in roles)
                    {
                        identity.AddClaim(new Claim(ClaimTypes.Role, role));
                    }

                }
            }
            await _next(context);
        }
    }
    public static class PrincipalExtensions
    {
        public static bool IsInStartsWithRole(this ClaimsPrincipal principal, string role)
        {
            return principal.IsInRole(role) || principal.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value.StartsWith(role));
        }
    }
    public class RoleAuthorizationAttribute : AuthorizeAttribute, IAuthorizationFilter
    {
        readonly string[] _roles;

        public RoleAuthorizationAttribute(params string[] roles)
        {
            _roles = roles;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var isAuthenticated = context.HttpContext.User.Identity.IsAuthenticated;
            if (!isAuthenticated)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            var hasAllRequredClaims = _roles.Any(r => context.HttpContext.User.IsInRole(r));
            if (!hasAllRequredClaims)
            {
                context.Result = new ForbidResult();
                return;
            }
        }
    }
}
