using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using ServiceReference1;
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
            string clientId = null, contactId = null, fullVatNumber = null, info = null;
            try
            {
                // For details about the structure of the HTTP request sent
                // from Billomat see https://www.billomat.com/api/webhooks/.
                (clientId, contactId) = await GetClientIdFromRequestBody(req.Body);
                if (string.IsNullOrEmpty(clientId) && string.IsNullOrEmpty(contactId))
                {
                    log.LogError("Cannot find client/contact id in JSON body.");
                    return new BadRequestResult();
                }

                info = clientId;

                if (!string.IsNullOrWhiteSpace(contactId))
                {
                    info = $"{clientId} - {contactId}";
                }

                log.LogInformation($"Got ids: {info}");

                string clientAddress = null, countryCode = null, clientName = null, vatNumber = null;
                var billomatClient = await GetClientFromBillomat(clientId, log);
                fullVatNumber = billomatClient?.vat_number ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(fullVatNumber) && fullVatNumber.Length >= 2)
                {
                    vatNumber = fullVatNumber.Substring(2).Replace(" ", string.Empty);
                    info = $"{fullVatNumber} - {info}";
                }

                if (!string.IsNullOrEmpty(contactId))
                {
                    log.LogInformation($"Using contact info instead of client.");
                    var billomatContact = await GetContactFromBillomat(contactId, log);
                    countryCode = billomatContact.country_code;
                    clientName = billomatContact.name;
                    var street = billomatContact.street;
                    var zip = billomatContact.zip;
                    var city = billomatContact.city;
                    clientAddress = street + " " + countryCode + "-" + zip + " " + city;
                }
                else
                {
                    countryCode = billomatClient.country_code;
                    clientName = billomatClient.name;
                    var street = billomatClient.street;
                    var zip = billomatClient.zip;
                    var city = billomatClient.city;
                    clientAddress = street + " " + countryCode + "-" + zip + " " + city;
                }

                var client = new checkVatPortTypeClient();
                await client.OpenAsync();
                var result = await client.checkVatAsync(new checkVatRequest(countryCode, vatNumber));

                var sendSlackMessageOnSuccess = Environment.GetEnvironmentVariable("SENDMESSAGEONSUCCESS", EnvironmentVariableTarget.Process) == "true";
                (var userResponse, var foundError) = ValidateVatInformation(countryCode, vatNumber, clientName, clientAddress, result, sendSlackMessageOnSuccess);

                if (foundError || sendSlackMessageOnSuccess)
                {
                    await PostToSlack(userResponse, log);
                }

                return new OkObjectResult(userResponse);
            }
            catch (Exception ex)
            {
                var sendError = $"Error while checking VAT ID ({info}): {ex.Message}";
                log.LogError(ex, sendError);
                await PostToSlack(sendError, log);
                return new OkResult();
            }
        }

        private async Task<Contact> GetContactFromBillomat(string contactId, ILogger log)
        {
            // Billomat GET Request. For details see https://www.billomat.com/en/api/clients/contacts/
            // URL: https://{BillomatID}.billomat.net/api/contacts/{string}

            var apiKey = Environment.GetEnvironmentVariable("APIKEY", EnvironmentVariableTarget.Process);
            var billomatID = Environment.GetEnvironmentVariable("BILLOMATID", EnvironmentVariableTarget.Process);
            var urlContact = $"https://{billomatID}.billomat.net/api/contacts/{contactId}";
            const string sendError = "Bad request, check your configurations and Webhook";

            var webGetRequest = new HttpRequestMessage
            {
                RequestUri = new Uri(urlContact),
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

            return JsonSerializer.Deserialize<ContactObject>(getJsonContent).contact;
        }

        internal (string userResponse, bool foundError) ValidateVatInformation(string countryCode, string vatNumber, string clientName, string clientAddress, checkVatResponse valParam, bool messageOnSuccess = true)
        {
            var userResponse = string.Empty;
            bool foundError = false;
            var empty = "---";

            if (valParam.valid)
            {
                static string CleanupIdentifier(string id) => id.ToLower().Replace("\n", " ").Replace("�", "ss");
                static bool CompareIdentifiers(string euCheck, string input) =>
                    euCheck != null && CleanupIdentifier(euCheck) == CleanupIdentifier(input) && euCheck != "---";

                if (CompareIdentifiers(valParam.name, clientName))
                {
                    if (messageOnSuccess)
                    {
                        userResponse = "\nCorrect company name: " + CleanupIdentifier(valParam.name);
                    }
                }
                else
                {
                    if (valParam.name == empty)
                    {
                        userResponse += $"\nCould not validate company name: {CleanupIdentifier(clientName)}";
                    }
                    else
                    {
                        userResponse += $"\nIncorrect company name: {CleanupIdentifier(clientName)} - expected (VIES): {CleanupIdentifier(valParam.name)}";
                    }

                    foundError |= true;
                }

                if (CompareIdentifiers(valParam.address, clientAddress))
                {
                    if (messageOnSuccess)
                    {
                        userResponse += "\nCorrect address: " + CleanupIdentifier(valParam.address);
                    }
                }
                else
                {
                    if (valParam.address == empty)
                    {
                        userResponse += $"\nCould not validate address: {CleanupIdentifier(clientAddress)}";
                    }
                    else
                    {
                        userResponse += $"\nIncorrect address: {CleanupIdentifier(clientAddress)} - expected (VIES): {CleanupIdentifier(valParam.address)}";
                    }

                    foundError |= true;
                }

                if (valParam.countryCode != null && valParam.countryCode != "---" && valParam.countryCode == countryCode)
                {
                    if (messageOnSuccess)
                    {
                        userResponse += "\nCorrect country code: " + valParam.countryCode;
                    }
                }
                else
                {
                    userResponse += $"\nIncorrect country code: {countryCode} - expected: {valParam.countryCode}";
                    foundError |= true;
                }

                // TODO review if/how this could even happen
                if (valParam.vatNumber != null && valParam.vatNumber != "---" && valParam.vatNumber == vatNumber)
                {
                    if (messageOnSuccess)
                    {
                        userResponse += "\nCorrect vat-number: " + valParam.vatNumber;
                    }
                }
                else
                {
                    userResponse += $"\nIncorrect vat-number: {vatNumber} - expected: {valParam.vatNumber}";
                    foundError |= true;
                }
            }
            else
            {
                userResponse = "\nNothing's valid/not found";
                foundError |= true;
            }

            if (foundError)
            {
                userResponse = $"VIES information does not match for {vatNumber}:\n{userResponse}";
            }

            return (userResponse, foundError);
        }

        private async Task<(string clientId, string contactId)> GetClientIdFromRequestBody(Stream body)
        {
            var requestBody = await new StreamReader(body).ReadToEndAsync();
            var invoiceObject = JsonSerializer.Deserialize<InvoiceObject>(requestBody);
            var clientId = invoiceObject?.invoice?.client_id;
            var contactId = invoiceObject?.invoice?.contact_id;
            return (clientId, contactId);
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
                    JsonSerializer.Serialize(new { channel = $"{slackChannel}", text = $"{slackUser} {var}" }), Encoding.UTF8, "application/json")
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

