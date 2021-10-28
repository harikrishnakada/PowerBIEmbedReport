using Microsoft.Identity.Client;
using Microsoft.PowerBI.Api.Models;
using Microsoft.PowerBI.Api;
//using Microsoft.PowerBI.Api.V2.Models;
using Microsoft.Rest;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PowerBIEmbedReport
{
    public static class Program
    {
      
        public static readonly string RoleName  = "";
        public static readonly string RoleId  = "";

        /// <summary>
        /// Id of the Power BI report. You can obtain it after publishing the report.
        /// </summary>
        public static readonly string ReportId  = "";
        public static async Task Main(string[] args)
        {

            try
            {
                var reportIdG = Guid.Parse(ReportId);

                dynamic reportViewerObject = await GetPowerBiEmbed(reportIdG);

                Console.WriteLine("EmbedUrl: ");
                Console.WriteLine(reportViewerObject.embedUrl);
                Console.WriteLine("AccessToken: ");
                Console.WriteLine(reportViewerObject.accessToken);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private static async Task<dynamic> GetPowerBiEmbed(Guid reportIdG)
        {
            dynamic reportViewerObject;

            using (var client = await GetPowerBIClient())
            {
                var groupId = Guid.Parse(Environment.GetEnvironmentVariable("POWERBI_WORKSPACE_ID"));
                var reportsResponse = await client.Reports.GetReportsInGroupAsync(groupId);
                var powerBIReport = reportsResponse.Value.FirstOrDefault(r => r.Id == reportIdG);
                EmbedToken tokenResponse;

                try
                {
                    var generateTokenRequestParameters = Program.GenerateTokenRequestParametersWithRls(powerBIReport);

                    tokenResponse = await client.Reports.GenerateTokenInGroupAsync(groupId, powerBIReport.Id, generateTokenRequestParameters);
                }
                catch (Exception ex)
                {
                    var generateTokenRequestParameters = new GenerateTokenRequest(accessLevel: "view", powerBIReport.DatasetId);
                    tokenResponse = await client.Reports.GenerateTokenInGroupAsync(groupId, powerBIReport.Id, generateTokenRequestParameters);
                }

                //Generate Embed Configuration.
                reportViewerObject = new
                {
                    accessToken = tokenResponse.Token,
                    embedUrl = powerBIReport.EmbedUrl,
                };
            }

            return reportViewerObject;
        }

        private static GenerateTokenRequest GenerateTokenRequestParametersWithRls(Report powerBIReport)
        {
            var rls = new List<EffectiveIdentity> { new EffectiveIdentity(username: RoleId.ToString(), roles: new List<string> { RoleName },
                            datasets: new List<string> { powerBIReport.DatasetId.ToString().ToLower() }) };

            var tokenRequestParameters = new GenerateTokenRequest(
                accessLevel: TokenAccessLevel.View,
                datasetId: powerBIReport.DatasetId.ToString(),
                identities: rls
             );
            return tokenRequestParameters;
        }

        public static async Task<IPowerBIClient> GetPowerBIClient()
        {
            string accessToken = await GetPowerBIAccessTokenUsingServicePrincipal();

            var apiUrl = Environment.GetEnvironmentVariable("POWERBI_API_URL");
            var tokenCredentials = new TokenCredentials(accessToken, "Bearer");
            var client = new PowerBIClient(tokenCredentials)
            {
                BaseUri = new Uri(apiUrl)
            };

            return client;
        }

        private static async Task<string> GetPowerBIAccessTokenUsingServicePrincipal()
        {
            // For app only authentication, we need the specific tenant id in the authority url
            var tenantSpecificUrl = string.Format(Environment.GetEnvironmentVariable("AUTHORITY_URI").ToString(),
                Environment.GetEnvironmentVariable("MAIN_TENANT_ID").ToString());

            // Create a confidetial client to authorize the app with the AAD app
            IConfidentialClientApplication clientApp = ConfidentialClientApplicationBuilder.Create(Environment.GetEnvironmentVariable("POWERBI_APP_ID")).WithClientSecret(Environment.GetEnvironmentVariable("POWERBI_APP_SECRET")).WithAuthority(tenantSpecificUrl).Build();
            var scopes = Environment.GetEnvironmentVariable("POWERBI_SCOPE") != null ? Environment.GetEnvironmentVariable("POWERBI_SCOPE").Split(',')?.ToArray() : null;

            // Make a client call if Access token is not available in cache
            AuthenticationResult authenticationResult = clientApp.AcquireTokenForClient(scopes).ExecuteAsync().Result;
            await Task.CompletedTask;
            return authenticationResult.AccessToken;
        }

    }
}
