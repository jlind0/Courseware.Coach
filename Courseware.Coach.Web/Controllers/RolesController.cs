using Courseware.Coach.Core;
using Courseware.Coach.Data;
using Courseware.Coach.Data.Core;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Courseware.Coach.Web.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RolesController : ControllerBase
    {
        protected ILogger Logger { get; }
        protected IRepository<UnitOfWork, User> UserRepo { get; }
        public RolesController(ILogger<RolesController> logger, IRepository<UnitOfWork, User> userRepo)
        {
            Logger = logger;
            UserRepo = userRepo;
        }
        public class RoleBody
        {
            public string extension_Roles { get; set; } = null!;
            public string version { get; set; } = "1.0.0";
            public string action { get; set; } = "continue";
        }
        [HttpPost("/permissions")]
        public async Task<RoleBody> GetUserPermissions([FromBody] JObject message, CancellationToken token = default)
        {
            try
            {
                Logger.LogInformation(message.ToString(Formatting.None)); // Serializing with Newtonsoft.Json

                // Parsing the JSON using JObject
                var userId = (string)(message["objectId"] ?? throw new InvalidDataException());
                var users = await UserRepo.Get(filter: q => q.ObjectId == userId, token: token);
                string[] roles = [];
                if(users.Count == 0)
                {
                    var user = new User()
                    {
                        ObjectId = userId,
                        LastName = message["surname"]?.ToString(),
                        FirstName = message["givenName"]?.ToString(),
                        Email = (string)(message["email"] ?? throw new InvalidDataException()),
                        ZipCode = message["postalCode"]?.ToString(),
                        State = message["state"]?.ToString(),
                        Address = message["streetAddress"]?.ToString(),
                        Country = message["country"]?.ToString(),
                    };
                    await UserRepo.Add(user, token: token);
                }
                else
                {
                    roles = users.Items.Single().Roles.ToArray();
                }
                return new RoleBody()
                {
                    extension_Roles = roles.Length > 0 ? string.Join(',', roles) : string.Empty
                };
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                throw;
            }
        }
    }
}
