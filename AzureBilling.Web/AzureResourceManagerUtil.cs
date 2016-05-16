using AzureBillingAPI.Web.Models;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Web.Helpers;
using AzureBillingAPI.Data;

namespace AzureBillingAPI.Web
{
    public static class AzureResourceManagerUtil
    {
        public static List<Organization> GetUserOrganizations()
        {
            List<Organization> organizations = new List<Organization>();

            string tenantId = ConfigurationManager.AppSettings["ida:TenantID"];
            try
            {
                AuthenticationResult result = GetFreshAuthToken(tenantId);
                
                // Get a list of Organizations of which the user is a member            
                string requestUrl = string.Format("{0}/tenants?api-version={1}", ConfigurationManager.AppSettings["ida:AzureResourceManagerUrl"],
                    ConfigurationManager.AppSettings["ida:AzureResourceManagerAPIVersion"]);

                // Make the GET request
                HttpClient client = new HttpClient();
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
                HttpResponseMessage response = client.SendAsync(request).Result;

                if (response.IsSuccessStatusCode)
                {
                    string responseContent = response.Content.ReadAsStringAsync().Result;
                    var organizationsResult = (Json.Decode(responseContent)).value;

                    foreach (var organization in organizationsResult)
                        organizations.Add(new Organization()
                        {
                            Id = organization.tenantId,
                            objectIdOfISVDemoUsageServicePrincipal =
                                AzureADGraphAPIUtil.GetObjectIdOfServicePrincipalInOrganization(organization.tenantId, ConfigurationManager.AppSettings["ida:ClientID"])
                        });
                }
            }
            catch
            {
                
            }
            return organizations;
        }
        
        public static List<Subscription> GetUserSubscriptions(string organizationId)
        {
            List<Subscription> subscriptions = null;
            try
            {
                AuthenticationResult result = GetFreshAuthToken(organizationId);

                subscriptions = new List<Subscription>();

                // Get subscriptions to which the user has some kind of access
                string requestUrl = string.Format("{0}/subscriptions?api-version={1}",
                    ConfigurationManager.AppSettings["ida:AzureResourceManagerUrl"],
                    ConfigurationManager.AppSettings["ida:AzureResourceManagerAPIVersion"]);

                // Make the GET request
                HttpClient client = new HttpClient();
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
                HttpResponseMessage response = client.SendAsync(request).Result;

                if (response.IsSuccessStatusCode)
                {
                    string responseContent = response.Content.ReadAsStringAsync().Result;
                    var subscriptionsResult = (Json.Decode(responseContent)).value;

                    foreach (var subscription in subscriptionsResult)
                        subscriptions.Add(new Subscription()
                        {
                            Id = subscription.subscriptionId,
                            DisplayName = subscription.displayName,
                            OrganizationId = organizationId
                        });
                }
            }
            catch(Exception exp)
            {
                //log exceptions
            }
            return subscriptions;
        }

        private static AuthenticationResult GetFreshAuthToken(string organizationId)
        {
            // Aquire Access Token to call Azure Resource Manager
            string signedInUserUniqueName = ConfigurationManager.AppSettings["ida:Username"];
            ClientCredential credential = new ClientCredential(ConfigurationManager.AppSettings["ida:ClientID"],
                ConfigurationManager.AppSettings["ida:Password"]);
            AuthenticationContext authContext = new AuthenticationContext(
                string.Format(ConfigurationManager.AppSettings["ida:Authority"], organizationId), new ADALTokenCache(signedInUserUniqueName));
            AuthenticationResult result = authContext.AcquireToken(ConfigurationManager.AppSettings["ida:AzureResourceManagerIdentifier"], credential);
            return result;
        }
    }
}