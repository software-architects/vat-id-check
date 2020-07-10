using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace VatIDChecker
{
    public class EuVatChecker
    {
        private readonly HttpClient client;

        public EuVatChecker(IHttpClientFactory clientFactory)
        {
            client = clientFactory.CreateClient();
        }

        public async Task<string> PostXMLToEU(string countryCode, string vatNumber)
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
        public ValidationParams GetValidEUParam(XDocument soapRes)
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
    }
}
