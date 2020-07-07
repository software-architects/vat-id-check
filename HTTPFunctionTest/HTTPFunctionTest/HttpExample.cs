using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RestSharp;
using System.Text;
using System.Net;
using System.Xml.Linq;
using System.IO;
using System.Xml.XPath;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using System.Xml;
using System.Net.Http;
using Microsoft.Azure.WebJobs.Host;

namespace HTTPFunctionTest
{
    public static class HttpExample
    {
        [FunctionName("HttpExample")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            XmlDocument doc = new XmlDocument();
            XmlElement child = doc.CreateElement("Child");
            child.InnerText = "child contents";
            XmlElement root = doc.CreateElement("Root");
            root.AppendChild(child);
            doc.AppendChild(root);

            //Billomat GET Request
            //URL: https://{BillomatID}.billomat.net/api/clients/{string}
            var webGetRequest = (HttpWebRequest)WebRequest.Create("https://softarchmelinatest.billomat.net/api/clients/3666450");
            webGetRequest.ContentType = "application/json;charset='utf-8'";
            webGetRequest.Accept = "application/json";
            webGetRequest.Timeout = 1000000000;
            webGetRequest.Method = "GET";

            var apiKey = System.Environment.GetEnvironmentVariable("BILLOMATID", EnvironmentVariableTarget.Process);
            //API_KEY
            webGetRequest.Headers.Add("X-BillomatApiKey", apiKey);

            var getResponse = webGetRequest.GetResponse();
            var readGetStream = new StreamReader(getResponse.GetResponseStream(), Encoding.UTF8);
            var getData = "";
            getData = readGetStream.ReadLine();

            ClientObject clientObject = Deserialize(getData);

            //From JSON to Object in ClientObject
            ClientObject Deserialize(string jsonString)
            {
                return System.Text.Json.JsonSerializer.Deserialize<ClientObject>(jsonString);
            }

            var countryCode = clientObject.client.country_code;
            var vatNumber = clientObject.client.vat_number.Substring(2).Replace(" ", string.Empty);
            var clientName = clientObject.client.name;
            var street = clientObject.client.street;
            var zip = clientObject.client.zip;
            var city = clientObject.client.city;
            var clientAddress = street + " " + countryCode + "-" + zip + " " + city;

            readGetStream.Close();
            getResponse.Close();

            //POST Request
            var webRequest = (HttpWebRequest)WebRequest.Create("http://ec.europa.eu/taxation_customs/vies/services/checkVatService");
            webRequest.ContentType = "text/xml;charset='utf-8'";
            webRequest.Accept = "text/xml";
            webRequest.Timeout = 1000000000;
            webRequest.Method = "POST";
            webRequest.Headers.Add("SOAPAction", "http://ec.europa.eu/taxation_customs/vies/services/checkVatService");

            //Send JSON 
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
            var readPostStream = new StreamReader(postResponse.GetResponseStream(), System.Text.Encoding.UTF8);
            var postData = readPostStream.ReadToEnd();

            readPostStream.Close();
            postResponse.Close();

            var nameTable = new NameTable();
            var nsManager = new XmlNamespaceManager(nameTable);
            nsManager.AddNamespace("x", "urn:ec.europa.eu:taxud:vies:services:checkVat:types");
            nsManager.AddNamespace("soap", "http://schemas.xmlsoap.org/soap/envelope/");

            var soapResponse = XDocument.Parse(postData);
            var ns = soapResponse.Root.Name.Namespace;

            var valid = soapResponse.XPathSelectElement("//soap:Body/x:checkVatResponse/x:valid", nsManager);
            var name = soapResponse.XPathSelectElement("//soap:Body/x:checkVatResponse/x:name", nsManager);
            var address = soapResponse.XPathSelectElement("//soap:Body/x:checkVatResponse/x:address", nsManager);
            var cCode = soapResponse.XPathSelectElement("//soap:Body/x:checkVatResponse/x:countryCode", nsManager);
            var vatNum = soapResponse.XPathSelectElement("//soap:Body/x:checkVatResponse/x:vatNumber", nsManager);

            //Validation
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
        public class ClientObject
        {
            public Client client { get; set; }
        }
        public class Client
        {
            public string id { get; set; }
            public DateTime created { get; set; }
            public DateTime updated { get; set; }
            public string archived { get; set; }
            public string dig_exclude { get; set; }
            public string client_number { get; set; }
            public string number { get; set; }
            public string number_pre { get; set; }
            public string number_length { get; set; }
            public string name { get; set; }
            public string salutation { get; set; }
            public string first_name { get; set; }
            public string last_name { get; set; }
            public string street { get; set; }
            public string zip { get; set; }
            public string city { get; set; }
            public string state { get; set; }
            public string country_code { get; set; }
            public string address { get; set; }
            public string phone { get; set; }
            public string fax { get; set; }
            public string mobile { get; set; }
            public string email { get; set; }
            public string www { get; set; }
            public string tax_number { get; set; }
            public string vat_number { get; set; }
            public string bank_account_owner { get; set; }
            public string bank_number { get; set; }
            public string bank_name { get; set; }
            public string bank_account_number { get; set; }
            public string bank_swift { get; set; }
            public string bank_iban { get; set; }
            public string currency_code { get; set; }
            public string enable_customerportal { get; set; }
            public string default_payment_types { get; set; }
            public string sepa_mandate { get; set; }
            public string sepa_mandate_date { get; set; }
            public string locale { get; set; }
            public string tax_rule { get; set; }
            public string net_gross { get; set; }
            public string price_group { get; set; }
            public string debitor_account_number { get; set; }
            public string reduction { get; set; }
            public string discount_rate_type { get; set; }
            public string discount_rate { get; set; }
            public string discount_days_type { get; set; }
            public string discount_days { get; set; }
            public string due_days_type { get; set; }
            public string due_days { get; set; }
            public string reminder_due_days_type { get; set; }
            public string reminder_due_days { get; set; }
            public string offer_validity_days_type { get; set; }
            public string offer_validity_days { get; set; }
            public string dunning_run { get; set; }
            public string note { get; set; }
            public string revenue_gross { get; set; }
            public string revenue_net { get; set; }
            public string customfield { get; set; }
            public string client_property_values { get; set; }
        }
    }
}

