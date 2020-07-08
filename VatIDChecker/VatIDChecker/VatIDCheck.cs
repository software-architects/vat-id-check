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
            var postContent = postResponse.Content;
            var postXmlContent = postContent.ReadAsStringAsync().Result;

            var nameTable = new NameTable();
            var nsManager = new XmlNamespaceManager(nameTable);
            nsManager.AddNamespace("x", "urn:ec.europa.eu:taxud:vies:services:checkVat:types");
            nsManager.AddNamespace("soap", "http://schemas.xmlsoap.org/soap/envelope/");

            //var soapResponse = XDocument.Parse(postData);
            var soapResponse = XDocument.Parse(postXmlContent);

            var valid = soapResponse.XPathSelectElement("//soap:Body/x:checkVatResponse/x:valid", nsManager);
            var name = soapResponse.XPathSelectElement("//soap:Body/x:checkVatResponse/x:name", nsManager);
            var address = soapResponse.XPathSelectElement("//soap:Body/x:checkVatResponse/x:address", nsManager);
            var cCode = soapResponse.XPathSelectElement("//soap:Body/x:checkVatResponse/x:countryCode", nsManager);
            var vatNum = soapResponse.XPathSelectElement("//soap:Body/x:checkVatResponse/x:vatNumber", nsManager);

            // UST_ID Validation
            var userResponse = string.Empty;
            if (valid == null)
            {
                userResponse = "Not found";
            }
            else
            {
                if (valid.Value == "true")
                {
                    if ((name != null) && (name.Value.ToLower().Replace("\n", " ") == clientName.ToLower().Replace("\n", " ")) && (name.Value != "---"))
                    {
                        userResponse = "Correct company name: " + name.Value;
                    }
                    else
                    {
                        userResponse += "Company name not valid";
                    }
                    if ((address != null) && (address.Value.ToLower().Replace("\n", " ") == clientAddress.ToLower().Replace("\n", " ")) && (address.Value != "---"))
                    {
                        userResponse += "\nCorrect address: " + address.Value.Replace("\n", " ");
                    }
                    else
                    {
                        userResponse += "\nAddress not valid";
                    }
                    if ((cCode != null) && (cCode.Value != "---"))
                    {
                        userResponse += "\nCorrect country code: " + cCode.Value;
                    }
                    else
                    {
                        userResponse += "\nCountry Code not valid";
                    }
                    if ((vatNum != null) && (vatNum.Value != "---"))
                    {
                        userResponse += "\nCorrect vat-number: " + vatNum.Value;
                    }
                    else
                    {
                        userResponse += "\nVatNumber not valid";
                    }
                }
                else
                {
                    userResponse = "Not valid";
                }
            }
            return new OkObjectResult(userResponse);
        }
        private async Task<string> GetClientIdFromRequestBody(Stream body)
        {
            var requestBody = await new StreamReader(body).ReadToEndAsync();
            var invoiceObject = JsonSerializer.Deserialize<InvoiceObject>(requestBody);
            var clientId = invoiceObject?.invoice?.client_id;
            return clientId;
        }

        private async Task<Client> GetClientFromBillomat(string clientId)
        {
            // Billomat GET Request. For details see https://www.billomat.com/api/kunden/.
            // URL: https://{BillomatID}.billomat.net/api/clients/{string}
            var urlClient = $"https://softarchmelinatest.billomat.net/api/clients/{clientId}";
            var apiKey = Environment.GetEnvironmentVariable("BILLOMATID", EnvironmentVariableTarget.Process);

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
            var getContent = getResponse.Content;
            var getJsonContent = getContent.ReadAsStringAsync().Result;

            return JsonSerializer.Deserialize<ClientObject>(getJsonContent).client;
        }
    }
}

