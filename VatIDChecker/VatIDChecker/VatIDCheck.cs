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
                var userResponse = string.Empty;
                var validStatus = Environment.GetEnvironmentVariable("VALIDSTATUS", EnvironmentVariableTarget.Process);

                var strStatus = "";
                var strPostToSlack = "";
                var count = 0;
                if (valParam.valid == null)
                {
                    userResponse = "Not found";
                    strStatus = "Not found";
                }
                else
                {
                    if (valParam.valid == "true")
                    {
                        if ((valParam.name != null) && (valParam.name.ToLower().Replace("\n", " ") == clientName.ToLower().Replace("\n", " ")) && (valParam.name != "---"))
                        {
                            userResponse = "\nCorrect company name: " + valParam.name.ToLower().Replace("\n", " ");
                            strStatus = "\nCorrect company name: " + valParam.name.ToLower().Replace("\n", " ");
                            count++;
                        }
                        else
                        {
                            userResponse += "\nCompany name not valid";
                            strStatus += "\nIncorrect company name: " + valParam.name.ToLower().Replace("\n", " ") + " != " + clientName.ToLower().Replace("\n", " ");
                        }
                        if ((valParam.address != null) && (valParam.address.ToLower().Replace("\n", " ") == clientAddress.ToLower().Replace("\n", " ")) && (valParam.address != "---"))
                        {
                            userResponse += "\nCorrect address: " + valParam.address.ToLower().Replace("\n", " ");
                            strStatus += "\nCorrect address: " + valParam.address.ToLower().Replace("\n", " ");
                            count++;
                        }
                        else
                        {
                            userResponse += "\nAddress not valid";
                            strStatus += "\nIncorrect Address: " + valParam.address.ToLower().Replace("\n", " ") + " != " + clientAddress.ToLower().Replace("\n", " ");
                        }
                        if ((valParam.cCode != null) && (valParam.cCode != "---") && (valParam.cCode == countryCode))
                        {
                            userResponse += "\nCorrect country code: " + valParam.cCode;
                            strStatus += "\nCorrect Correct country : " + valParam.cCode;
                            count++;
                        }
                        else
                        {
                            userResponse += "\nCountry Code not valid";
                            strStatus += "\nIncorrect Country Code: " + valParam.cCode + " != " + countryCode;
                        }
                        if ((valParam.vatNum != null) && (valParam.vatNum != "---") && (valParam.vatNum == vatNumber))
                        {
                            userResponse += "\nCorrect vat-number: " + valParam.vatNum;
                            strStatus += "\nCorrect vat-number: " + valParam.vatNum;
                            count++;
                        }
                        else
                        {
                            userResponse += "\nVatNumber not valid";
                            strStatus += "\nIncorrect vat-number: " + valParam.vatNum + " != " + vatNumber;
                        }
                    }
                    else
                    {
                        userResponse = "\nNot valid";
                        strStatus = "\nEverything's incorrect :(";
                    }
                    if (count == 4 && validStatus == "true")
                    {
                        strPostToSlack = await PostToSlack(strStatus);
                    }
                    else if (count < 4)
                    {
                        strPostToSlack = await PostToSlack(strStatus);
                    }
                    else if(count == 4 && validStatus == "false") { }
                }
                return new OkObjectResult(userResponse);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error while checking VAT ID");
                return new InternalServerErrorResult();
            }

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

