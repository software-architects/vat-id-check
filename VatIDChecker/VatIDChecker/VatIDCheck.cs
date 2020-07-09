using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web.Http;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace VatIDChecker
{
    public class VatIDCheck
    {
        private readonly HttpClient client;

        public VatIDCheck(IHttpClientFactory clientFactory)
        {
            client = clientFactory.CreateClient();
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

                var billomatClient = await GetClientFromBillomat(clientId);
                var countryCode = billomatClient.country_code;
                var vatNumber = billomatClient.vat_number.Substring(2).Replace(" ", string.Empty);
                var clientName = billomatClient.name;
                var street = billomatClient.street;
                var zip = billomatClient.zip;
                var city = billomatClient.city;
                var clientAddress = street + " " + countryCode + "-" + zip + " " + city;

                var xmlContent = await PostXMLToEU(countryCode, vatNumber);
                var soapResponse = XDocument.Parse(xmlContent.ToString());

                var valParam = GetValidEUParam(soapResponse);

                // UST_ID Validation
                if (string.IsNullOrEmpty(valParam.valid))
                {
                    // Todo: Add meaningful message
                    log.LogError("Todo: Add meaningful message");

                    // Todo: Even if valParam is null, send a status message to Slack

                    return new NotFoundObjectResult("Not found");
                }

                (var userResponse, var foundError) = ValidateVatInformation(countryCode, vatNumber, clientName, clientAddress, valParam);

                var sendSlackMessageOnSuccess = Environment.GetEnvironmentVariable("VALIDSTATUS", EnvironmentVariableTarget.Process);
                if (foundError || sendSlackMessageOnSuccess == "true")
                {
                    await PostToSlack(userResponse);
                }

                return new OkObjectResult(userResponse);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error while checking VAT ID");
                return new InternalServerErrorResult();
            }

        }

        private (string userResponse, bool foundError) ValidateVatInformation(string countryCode, string vatNumber, string clientName, string clientAddress, ValidationParams valParam)
        {
            var userResponse = string.Empty;
            bool foundError = false;

            if (valParam.valid == "true")
            {
                static string CleanupIdentifier(string id) => id.ToLower().Replace("\n", " ");
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
                    // Todo: Interpolation
                    userResponse += "\nIncorrect Address: " + CleanupIdentifier(valParam.address) + " != " + CleanupIdentifier(clientAddress);
                    foundError |= true;
                }

                if (valParam.cCode != null && valParam.cCode != "---" && valParam.cCode == countryCode)
                {
                    userResponse += "\nCorrect country code: " + valParam.cCode;
                }
                else
                {
                    // Todo: Interpolation
                    userResponse += "\nIncorrect Country Code: " + valParam.cCode + " != " + countryCode;
                    foundError |= true;
                }

                if (valParam.vatNum != null && valParam.vatNum != "---" && valParam.vatNum == vatNumber)
                {
                    userResponse += "\nCorrect vat-number: " + valParam.vatNum;
                }
                else
                {
                    // Todo: Interpolation
                    userResponse += "\nIncorrect vat-number: " + valParam.vatNum + " != " + vatNumber;
                    foundError |= true;
                }
            }
            else
            {
                userResponse = "\nNot valid";
            }

            return (userResponse, foundError);
        }

        private ValidationParams GetValidEUParam(XDocument soapRes)
        {
            var nameTable = new NameTable();
            var nsManager = new XmlNamespaceManager(nameTable);
            nsManager.AddNamespace("x", "urn:ec.europa.eu:taxud:vies:services:checkVat:types");
            nsManager.AddNamespace("soap", "http://schemas.xmlsoap.org/soap/envelope/");

            var valParam = new ValidationParams()
            {
                valid = (string)soapRes.XPathSelectElement("//soap:Body/x:checkVatResponse/x:valid", nsManager),
                name = (string)soapRes.XPathSelectElement("//soap:Body/x:checkVatResponse/x:name", nsManager),
                address = (string)soapRes.XPathSelectElement("//soap:Body/x:checkVatResponse/x:address", nsManager),
                cCode = (string)soapRes.XPathSelectElement("//soap:Body/x:checkVatResponse/x:countryCode", nsManager),
                vatNum = (string)soapRes.XPathSelectElement("//soap:Body/x:checkVatResponse/x:vatNumber", nsManager)
            };

            return valParam;
        }

        private async Task<string> GetClientIdFromRequestBody(Stream body)
        {
            var requestBody = await new StreamReader(body).ReadToEndAsync();
            var invoiceObject = JsonSerializer.Deserialize<InvoiceObject>(requestBody);
            var clientId = invoiceObject?.invoice?.client_id;
            return clientId;
        }

        private async Task<string> PostToSlack(string var)
        {
            var urlSlack = @"https://slack.com/api/chat.postMessage";
            var slackAuthorization = Environment.GetEnvironmentVariable("SLACKAUTHORIZATIONKEY", EnvironmentVariableTarget.Process);
            var slackChannel = Environment.GetEnvironmentVariable("SLACKCHANNEL", EnvironmentVariableTarget.Process);
            var slackUser = Environment.GetEnvironmentVariable("SLACKUSER", EnvironmentVariableTarget.Process);

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
            postResponse.EnsureSuccessStatusCode();
            var postContent = postResponse.Content;
            var postXmlContent = postContent.ReadAsStringAsync().Result;
            return postXmlContent;
        }
        private async Task<string> PostXMLToEU(string countryCode, string vatNumber)
        {
            // POST Request
            const string urlEu = "http://ec.europa.eu/taxation_customs/vies/services/checkVatService";

            var webPostRequest = new HttpRequestMessage
            {
                RequestUri = new Uri(urlEu),
                Method = HttpMethod.Post,
                Headers = {
                    { "SOAPAction", urlEu},
                    { HttpRequestHeader.ContentType.ToString(), "text/xml;charset='utf-8'"},
                    { HttpRequestHeader.Accept.ToString(), "text/xml" },
                    { "Timeout", "1000000000"},
                },
                Content = new StringContent(@"<?xml version='1.0' encoding='utf-8'?>
                        <soap:Envelope xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xmlns:xsd='http://www.w3.org/2001/XMLSchema' xmlns:soap='http://schemas.xmlsoap.org/soap/envelope/'>
                            <soap:Body>
                                <checkVat xmlns='urn:ec.europa.eu:taxud:vies:services:checkVat:types'>
                                    <countryCode>" + countryCode + @"</countryCode>
                                    <vatNumber>" + vatNumber + @"</vatNumber>
                                </checkVat>
                            </soap:Body>
                        </soap:Envelope>")
            };

            var postResponse = await client.SendAsync(webPostRequest);
            postResponse.EnsureSuccessStatusCode();

            var postContent = postResponse.Content;
            var postXmlContent = postContent.ReadAsStringAsync().Result;
            return postXmlContent;
        }

        private async Task<Client> GetClientFromBillomat(string clientId)
        {
            // Billomat GET Request. For details see https://www.billomat.com/api/kunden/.
            // URL: https://{BillomatID}.billomat.net/api/clients/{string}

            var apiKey = Environment.GetEnvironmentVariable("APIKEY", EnvironmentVariableTarget.Process);
            var billomatID = Environment.GetEnvironmentVariable("BILLOMATID", EnvironmentVariableTarget.Process);
            var urlClient = $"https://{billomatID}.billomat.net/api/clients/{clientId}";


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
            getResponse.EnsureSuccessStatusCode();

            var getContent = getResponse.Content;
            var getJsonContent = getContent.ReadAsStringAsync().Result;

            return JsonSerializer.Deserialize<ClientObject>(getJsonContent).client;
        }
    }
}

