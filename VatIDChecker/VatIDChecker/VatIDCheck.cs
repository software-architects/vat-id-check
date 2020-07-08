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
        public ValidationParams valParam = new ValidationParams();

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

            var xmlContent = await PostXMLToEU(countryCode, vatNumber);
            var soapResponse = XDocument.Parse(xmlContent.ToString());
            GetValidEUParam(soapResponse);

            // UST_ID Validation
            var userResponse = string.Empty;
            if (valParam.valid == null)
            {
                userResponse = "Not found";
            }
            else
            {
                if (valParam.valid == "true")
                {
                    if ((valParam.name != null) && (valParam.name.ToLower().Replace("\n", " ") == clientName.ToLower().Replace("\n", " ")) && (valParam.name != "---"))
                    {
                        userResponse = "Correct company name: " + valParam.name;
                    }
                    else
                    {
                        userResponse += "Company name not valid";
                    }
                    if ((valParam.address != null) && (valParam.address.ToLower().Replace("\n", " ") == clientAddress.ToLower().Replace("\n", " ")) && (valParam.address != "---"))
                    {
                        userResponse += "\nCorrect address: " + valParam.address.Replace("\n", " ");
                    }
                    else
                    {
                        userResponse += "\nAddress not valid";
                    }
                    if ((valParam.cCode != null) && (valParam.cCode != "---") && (valParam.cCode == countryCode))
                    {
                        userResponse += "\nCorrect country code: " + valParam.cCode;
                    }
                    else
                    {
                        userResponse += "\nCountry Code not valid";
                    }
                    if ((valParam.vatNum != null) && (valParam.vatNum != "---") && (valParam.vatNum == vatNumber))
                    {
                        userResponse += "\nCorrect vat-number: " + valParam.vatNum;
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
        private void GetValidEUParam(XDocument soapRes)
        {
            var nameTable = new NameTable();
            var nsManager = new XmlNamespaceManager(nameTable);
            nsManager.AddNamespace("x", "urn:ec.europa.eu:taxud:vies:services:checkVat:types");
            nsManager.AddNamespace("soap", "http://schemas.xmlsoap.org/soap/envelope/");

            valParam.valid = (string)soapRes.XPathSelectElement("//soap:Body/x:checkVatResponse/x:valid", nsManager);
            valParam.name = (string)soapRes.XPathSelectElement("//soap:Body/x:checkVatResponse/x:name", nsManager);
            valParam.address = (string)soapRes.XPathSelectElement("//soap:Body/x:checkVatResponse/x:address", nsManager);
            valParam.cCode = (string)soapRes.XPathSelectElement("//soap:Body/x:checkVatResponse/x:countryCode", nsManager);
            valParam.vatNum = (string)soapRes.XPathSelectElement("//soap:Body/x:checkVatResponse/x:vatNumber", nsManager);
        }
        private async Task<string> GetClientIdFromRequestBody(Stream body)
        {
            var requestBody = await new StreamReader(body).ReadToEndAsync();
            var invoiceObject = JsonSerializer.Deserialize<InvoiceObject>(requestBody);
            var clientId = invoiceObject?.invoice?.client_id;
            return clientId;
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
            var postContent = postResponse.Content;
            var postXmlContent = postContent.ReadAsStringAsync().Result;
            return postXmlContent;
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

