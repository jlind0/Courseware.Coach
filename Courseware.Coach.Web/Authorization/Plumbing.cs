using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Courseware.Coach.Web.Authorization
{
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
