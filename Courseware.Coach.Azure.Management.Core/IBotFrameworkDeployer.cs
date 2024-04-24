using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Courseware.Coach.Azure.Management.Core
{
    public interface IBotFrameworkDeployer
    {
        Task<string?> DeployBot(string name, Guid subject, string botType = "coach", CancellationToken token = default);
    }
}
