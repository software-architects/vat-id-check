using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Net;
using System.Xml.Linq;
using System.Xml.XPath;
using System.Xml;
using System.Text.Json;
using System.Net.Http;

namespace HttpFunctionTest
{
    public class HttpExample
    {
        private readonly HttpClient client;

        public HttpExample(IHttpClientFactory clientFactory)
        {
            client = clientFactory.CreateClient();
        }

        [FunctionName("HttpExample")]
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

            // Billomat GET Request. For details see https://www.billomat.com/api/kunden/.
            // URL: https://{BillomatID}.billomat.net/api/clients/{string}
            var url = $"https://softarchmelinatest.billomat.net/api/clients/{clientId}";
            var apiKey = Environment.GetEnvironmentVariable("BILLOMATID", EnvironmentVariableTarget.Process);

            var webGetRequest = new HttpRequestMessage
            {
                RequestUri = new Uri(url),
                Method = HttpMethod.Get,
                Headers = {
                    { "X-BillomatApiKey", apiKey },
                    { HttpRequestHeader.ContentType.ToString(), "application/json;charset='utf-8'"},
                    { HttpRequestHeader.Accept.ToString(), "application/json" },
                    { "Timeout", "1000000000"},
                },
            };

            /*
            var webGetRequest = (HttpWebRequest)WebRequest.Create(url);
            webGetRequest.ContentType = "application/json;charset='utf-8'";
            webGetRequest.Accept = "application/json";
            webGetRequest.Timeout = 1000000000;
            webGetRequest.Method = "GET";

            API_KEY
            webGetRequest.Headers.Add("X-BillomatApiKey", apiKey);
            */

            var getResponse = await client.SendAsync(webGetRequest);
            var readGetStream = new StreamReader(getResponse.ToString(), Encoding.UTF8);
            var getData = readGetStream.ReadLine();

            var clientObject = JsonSerializer.Deserialize<DTO.ClientObject>(getData);

            var countryCode = clientObject.client.country_code;
            var vatNumber = clientObject.client.vat_number.Substring(2).Replace(" ", string.Empty);
            var clientName = clientObject.client.name;
            var street = clientObject.client.street;
            var zip = clientObject.client.zip;
            var city = clientObject.client.city;
            var clientAddress = street + " " + countryCode + "-" + zip + " " + city;

            readGetStream.Close();
            getResponse.Dispose();

            // POST Request
            var webRequest = (HttpWebRequest)WebRequest.Create("http://ec.europa.eu/taxation_customs/vies/services/checkVatService");
            webRequest.ContentType = "text/xml;charset='utf-8'";
            webRequest.Accept = "text/xml";
            webRequest.Timeout = 1000000000;
            webRequest.Method = "POST";
            webRequest.Headers.Add("SOAPAction", "http://ec.europa.eu/taxation_customs/vies/services/checkVatService");

            // Send XML
            var requestContent = Encoding.UTF8.GetBytes(@"<?xml version='1.0' encoding='utf-8'?>
            <soap:Envelope xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xmlns:xsd='http://www.w3.org/2001/XMLSchema' xmlns:soap='http://schemas.xmlsoap.org/soap/envelope/'>
                <soap:Body>
                    <checkVat xmlns='urn:ec.europa.eu:taxud:vies:services:checkVat:types'>
                        <countryCode>" + countryCode + @"</countryCode>
                        <vatNumber>" + vatNumber + @"</vatNumber>
                    </checkVat>
                </soap:Body>
            </soap:Envelope>");

            var request = webRequest.GetRequestStream();
            request.Write(requestContent, 0, requestContent.Length);
            var postResponse = webRequest.GetResponse();
            var readPostStream = new StreamReader(postResponse.GetResponseStream(), Encoding.UTF8);
            var postData = readPostStream.ReadToEnd();

            readPostStream.Close();
            postResponse.Close();

            var nameTable = new NameTable();
            var nsManager = new XmlNamespaceManager(nameTable);
            nsManager.AddNamespace("x", "urn:ec.europa.eu:taxud:vies:services:checkVat:types");
            nsManager.AddNamespace("soap", "http://schemas.xmlsoap.org/soap/envelope/");

            var soapResponse = XDocument.Parse(postData);

            var valid = soapResponse.XPathSelectElement("//soap:Body/x:checkVatResponse/x:valid", nsManager);
            var name = soapResponse.XPathSelectElement("//soap:Body/x:checkVatResponse/x:name", nsManager);
            var address = soapResponse.XPathSelectElement("//soap:Body/x:checkVatResponse/x:address", nsManager);
            var cCode = soapResponse.XPathSelectElement("//soap:Body/x:checkVatResponse/x:countryCode", nsManager);
            var vatNum = soapResponse.XPathSelectElement("//soap:Body/x:checkVatResponse/x:vatNumber", nsManager);

            // Validation
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

        private static async Task<string> GetClientIdFromRequestBody(Stream body)
        {
            var requestBody = await new StreamReader(body).ReadToEndAsync();
            var invoiceObject = JsonSerializer.Deserialize<DTO.InvoiceObject>(requestBody);
            var clientId = invoiceObject?.invoice?.client_id;
            return clientId;
        }

    }
}

