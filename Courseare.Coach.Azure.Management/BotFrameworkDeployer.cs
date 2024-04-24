using Courseware.Coach.Azure.Management.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Rest.Azure.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Newtonsoft.Json;
using Microsoft.Azure.Management.ResourceManager.Fluent.Models;
using Newtonsoft.Json.Linq;

namespace Courseare.Coach.Azure.Management
{
    public class BotFrameworkDeployer : IBotFrameworkDeployer
    {
        protected IConfiguration Config { get; }
        protected ILogger Logger { get; }
        public BotFrameworkDeployer(IConfiguration config, ILogger<BotFrameworkDeployer> logger)
        {
            Config = config;
            Logger = logger;
        }
        public async Task<string?> DeployBot(string name, Guid subject, string botType = "coach", CancellationToken token = default)
        {
            try
            {
                string clientId = Config["Portal:ClientId"] ?? throw new InvalidDataException();
                string clientSecret = Config["Portal:ClientSecret"] ?? throw new InvalidDataException();
                string tenantId = Config["Portal:TenantId"] ?? throw new InvalidDataException();
                string subscriptionId = Config["Portal:SubscriptionId"] ?? throw new InvalidDataException();
                string resourceGroupName = Config["Portal:ResourceGroupName"] ?? throw new InvalidDataException();
                string templateJson = File.ReadAllText("bot_template.json");
                string parametersJson = File.ReadAllText("bot_parameters.json");
                parametersJson = parametersJson.Replace("{{botType}}", botType);
                parametersJson = parametersJson.Replace("{{name}}", name);
                parametersJson = parametersJson.Replace("{{subject}}", subject.ToString());
                parametersJson = parametersJson.Replace("{{appId}}", Guid.NewGuid().ToString().ToLower());
                var credentials = SdkContext.AzureCredentialsFactory.FromServicePrincipal(clientId, clientSecret, tenantId, AzureEnvironment.AzureGlobalCloud);
                var azure = Microsoft.Azure.Management.Fluent.Azure.Configure().WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic).Authenticate(credentials).WithSubscription(subscriptionId);
                //var parameters = <Dictionary<string, Dictionary<string, object>>>(parametersJson, new JsonSerializerSettings());
                var deployment = await azure.Deployments.Define($"botDeployment{Guid.NewGuid()}")
                    .WithExistingResourceGroup(resourceGroupName)
                    .WithTemplate(templateJson)
                    .WithParameters(JObject.Parse(parametersJson).GetValue("parameters"))
                    .WithMode(DeploymentMode.Incremental)
                    .CreateAsync();
                return deployment.CorrelationId;
            }
            catch(Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                return null;
            }

        }
    }
}
