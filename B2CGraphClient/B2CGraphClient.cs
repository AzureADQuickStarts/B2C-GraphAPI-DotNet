using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

using Microsoft.IdentityModel.Clients.ActiveDirectory;

using Newtonsoft.Json;

namespace B2CGraphShell
{
    public class B2CGraphClient
    {
        private string clientId { get; set; }
        private string clientSecret { get; set; }
        private string tenant { get; set; }

        private AuthenticationContext authContext;
        private ClientCredential credential;

        public B2CGraphClient(string clientId, string clientSecret, string tenant)
        {
            // The client_id, client_secret, and tenant are pulled in from the App.config file
            this.clientId = clientId;
            this.clientSecret = clientSecret;
            this.tenant = tenant;

            // The AuthenticationContext is ADAL's primary class, in which you indicate the directory to use.
            this.authContext = new AuthenticationContext(Globals.aadInstance + tenant);

            // The ClientCredential is where you pass in your client_id and client_secret, which are 
            // provided to Azure AD in order to receive an access_token using the app's identity.
            this.credential = new ClientCredential(clientId, clientSecret);
        }

        public async Task<string> GetUserByObjectId(string objectId)
        {
            return await SendGraphGetRequest("/users/" + objectId, null);
        }

        public async Task<string> GetAllUsers(string query)
        {
            return await SendGraphGetRequest("/users", query);
        }

        public async Task<string> CreateUser(string json)
        {
            return await SendGraphPostRequest("/users", json);
        }

        public async Task<string> UpdateUser(string objectId, string json)
        {
            return await SendGraphPatchRequest("/users/" + objectId, json);
        }

        public async Task<string> DeleteUser(string objectId)
        {
            return await SendGraphDeleteRequest("/users/" + objectId);
        }

        public async Task<string> RegisterExtension(string objectId, string body)
        {
            return await SendGraphPostRequest("/applications/" + objectId + "/extensionProperties", body);
        }

        public async Task<string> UnregisterExtension(string appObjectId, string extensionObjectId)
        {
            return await SendGraphDeleteRequest("/applications/" + appObjectId + "/extensionProperties/" + extensionObjectId);
        }

        public async Task<string> GetExtensions(string appObjectId)
        {
            return await SendGraphGetRequest("/applications/" + appObjectId + "/extensionProperties", null);
        }

        public async Task<string> GetApplications(string query)
        {
            return await SendGraphGetRequest("/applications", query);
        }

        private async Task<string> SendGraphDeleteRequest(string api)
        {
            return await SendRequest(HttpMethod.Delete, api);
        }

        private async Task<string> SendGraphPatchRequest(string api, string json)
        {
            return await SendRequest(new HttpMethod("PATCH"), api, null, json);
        }

        private async Task<string> SendGraphPostRequest(string api, string json)
        {
            return await SendRequest(HttpMethod.Post, api, null, json);
        }

        public async Task<string> SendGraphGetRequest(string api, string query)
        {
            return await SendRequest(HttpMethod.Get, api, query);
        }

        private async Task<string> SendRequest(HttpMethod method, string api, string query = null, string json = null)
        {
            // NOTE: This client uses ADAL v2, not ADAL v4
            HttpClient http = new HttpClient();
            string url = Globals.aadGraphEndpoint + tenant + api + "?" + Globals.aadGraphVersion;
            if (!string.IsNullOrEmpty(query))
            {
                url += "&" + query;
            }

            // TODO: Do some lazy caching - track token expiry etc
            AuthenticationResult authenticationResult = await authContext.AcquireTokenAsync(Globals.aadGraphResourceId, credential);
            var token = authenticationResult.AccessToken;

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(method.Method + " " + url);
            Console.WriteLine("Authorization: Bearer " + token.Substring(0, 80) + "...");
            if (!string.IsNullOrEmpty(json))
            {
                Console.WriteLine("Content-Type: application/json");
                Console.WriteLine("");
                Console.WriteLine(json);
            }
            Console.WriteLine("");

            // Append the access token for the Graph API to the Authorization header of the request, using the Bearer scheme.
            HttpRequestMessage request = new HttpRequestMessage(method, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (!string.IsNullOrEmpty(json))
            {
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            HttpResponseMessage response = await http.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync();
                object formatted = JsonConvert.DeserializeObject(error);
                throw new WebException("Error Calling the Graph API: \n" + JsonConvert.SerializeObject(formatted, Formatting.Indented));
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine((int)response.StatusCode + ": " + response.ReasonPhrase);
            Console.WriteLine("");

            return await response.Content.ReadAsStringAsync();
        }
    }
}
