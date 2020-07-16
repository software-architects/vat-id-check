using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace VatIDChecker
{
    public class VatIDCheck
    {
        private readonly HttpClient client;
        private readonly EuVatChecker euVatChecker;

        public VatIDCheck(IHttpClientFactory clientFactory, EuVatChecker euVatChecker)
        {
            client = clientFactory.CreateClient();
            this.euVatChecker = euVatChecker;
        }

        [FunctionName("VatIDCheck")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            try
            {
                // For details about the structure of the HTTP request sent
                // from Billomat see https://www.billomat.com/api/webhooks/.
                var clientId = await GetClientIdFromRequestBody(req.Body);
                if (string.IsNullOrEmpty(clientId))
                {
                    log.LogError("Cannot find client id in JSON body.");
                    return new BadRequestResult();
                }
                
                var billomatClient = await GetClientFromBillomat(clientId, log);
                var countryCode = billomatClient.country_code;
                var vatNumber = billomatClient.vat_number.Substring(2).Replace(" ", string.Empty);
                var clientName = billomatClient.name;
                var street = billomatClient.street;
                var zip = billomatClient.zip;
                var city = billomatClient.city;
                var clientAddress = street + " " + countryCode + "-" + zip + " " + city;
                
                var xmlContent = await euVatChecker.PostXMLToEU(countryCode, vatNumber);
                var soapResponse = XDocument.Parse(xmlContent.ToString());

                var valParam = euVatChecker.GetValidEUParam(soapResponse);

                // UST_ID Validation
                if (string.IsNullOrEmpty(valParam.valid))
                {
                    log.LogError("Cannot find vat information of company" + clientName);

                    return new NotFoundObjectResult("Cannot find vat information of company" + clientName);
                }

                (var userResponse, var foundError) = ValidateVatInformation(countryCode, vatNumber, clientName, clientAddress, valParam);

                var sendSlackMessageOnSuccess = Environment.GetEnvironmentVariable("SENDMESSAGEONSUCCESS", EnvironmentVariableTarget.Process);
                if (foundError || sendSlackMessageOnSuccess == "true")
                {
                    await PostToSlack(userResponse, log);
                }

                return new OkObjectResult(userResponse);
            }
            catch (Exception ex)
            {
                const string sendError = "Error while checking VAT ID";
                log.LogError(ex, sendError);
                await PostToSlack(sendError, log);
                return new OkResult();
            }
        }

        internal (string userResponse, bool foundError) ValidateVatInformation(string countryCode, string vatNumber, string clientName, string clientAddress, ValidationParams valParam)
        {
            var userResponse = string.Empty;
            bool foundError = false;

            if (valParam.valid == "true")
            {
                static string CleanupIdentifier(string id) => id.ToLower().Replace("\n", " ").Replace("ß", "ss");
                static bool CompareIdentifiers(string euCheck, string input) =>
                    euCheck != null && CleanupIdentifier(euCheck) == CleanupIdentifier(input) && euCheck != "---";

                if (CompareIdentifiers(valParam.name, clientName))
                {
                    userResponse = "\nCorrect company name: " + CleanupIdentifier(valParam.name);
                }
                else
                {
                    userResponse += $"\nIncorrect company name: {CleanupIdentifier(valParam.name)} != {CleanupIdentifier(clientName)}";
                    foundError |= true;
                }

                if (CompareIdentifiers(valParam.address, clientAddress))
                {
                    userResponse += "\nCorrect address: " + CleanupIdentifier(valParam.address);
                }
                else
                {
                    userResponse += $"\nIncorrect address: {CleanupIdentifier(valParam.address)} != {CleanupIdentifier(clientAddress)}";
                    foundError |= true;
                }

                if (valParam.cCode != null && valParam.cCode != "---" && valParam.cCode == countryCode)
                {
                    userResponse += "\nCorrect country code: " + valParam.cCode;
                }
                else
                {
                    userResponse += $"\nIncorrect country code: {valParam.cCode} != {countryCode}";
                    foundError |= true;
                }

                if (valParam.vatNum != null && valParam.vatNum != "---" && valParam.vatNum == vatNumber)
                {
                    userResponse += "\nCorrect vat-number: " + valParam.vatNum;
                }
                else
                {
                    userResponse += $"\nIncorrect vat-number: {valParam.vatNum} != {vatNumber}";
                    foundError |= true;
                }
            }
            else
            {
                userResponse = "\nNothing's valid";
                foundError |= true;
            }

            return (userResponse, foundError);
        }
        private async Task<string> GetClientIdFromRequestBody(Stream body)
        {
            var requestBody = await new StreamReader(body).ReadToEndAsync();
            var invoiceObject = JsonSerializer.Deserialize<InvoiceObject>(requestBody);
            var clientId = invoiceObject?.invoice?.client_id;
            return clientId;
        }
        private async Task<string> PostToSlack(string var, ILogger log)
        {
            var urlSlack = @"https://slack.com/api/chat.postMessage";
            var slackAuthorization = Environment.GetEnvironmentVariable("SLACKAUTHORIZATIONKEY", EnvironmentVariableTarget.Process);
            var slackChannel = Environment.GetEnvironmentVariable("SLACKCHANNEL", EnvironmentVariableTarget.Process);
            var slackUser = Environment.GetEnvironmentVariable("SLACKUSER", EnvironmentVariableTarget.Process);
            const string sendError = "Bad request, check your configurations and Webhook";

            var slackPostRequest = new HttpRequestMessage
            {
                RequestUri = new Uri(urlSlack),
                Method = HttpMethod.Post,
                Headers = {
                    { "Authorization", slackAuthorization },
                    { HttpRequestHeader.Accept.ToString(), "application/json" },
                    { "Timeout", "1000000000" },
                },
                Content = new StringContent(
                    JsonSerializer.Serialize(new { channel = $"{slackChannel}", text = $"{slackUser}{var}" }), Encoding.UTF8, "application/json")
            };

            var postResponse = await client.SendAsync(slackPostRequest);

            if (!postResponse.IsSuccessStatusCode)
            {
                log.LogError(sendError);
                await PostToSlack(sendError, log);
            }
            
            postResponse.EnsureSuccessStatusCode();
            var postContent = postResponse.Content;
            var postXmlContent = postContent.ReadAsStringAsync().Result;
            return postXmlContent;
        }
        private async Task<Client> GetClientFromBillomat(string clientId, ILogger log)
        {
            // Billomat GET Request. For details see https://www.billomat.com/api/kunden/.
            // URL: https://{BillomatID}.billomat.net/api/clients/{string}

            var apiKey = Environment.GetEnvironmentVariable("APIKEY", EnvironmentVariableTarget.Process);
            var billomatID = Environment.GetEnvironmentVariable("BILLOMATID", EnvironmentVariableTarget.Process);
            var urlClient = $"https://{billomatID}.billomat.net/api/clients/{clientId}";
            const string sendError = "Bad request, check your configurations and Webhook";

            // Send Header Information via await client.SendAsync(webGetRequest)
            var webGetRequest = new HttpRequestMessage
            {
                RequestUri = new Uri(urlClient),
                Method = HttpMethod.Get,
                Headers = {
                    { "X-BillomatApiKey", apiKey },
                    { HttpRequestHeader.ContentType.ToString(), "application/json;charset='utf-8'"},
                    { HttpRequestHeader.Accept.ToString(), "application/json" },
                    { "Timeout", "1000000000"},
                },
            };
            using var getResponse = await client.SendAsync(webGetRequest);

            if (!getResponse.IsSuccessStatusCode)
            {
                log.LogError(sendError);
                await PostToSlack(sendError, log);
            }

            var getContent = getResponse.Content;
            var getJsonContent = getContent.ReadAsStringAsync().Result;

            return JsonSerializer.Deserialize<ClientObject>(getJsonContent).client;
        }

    }
}

