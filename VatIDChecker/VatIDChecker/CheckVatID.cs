using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Web;
using System.Xml.Linq;

namespace VatIDChecker
{
    public class CheckVatID
    {
        private readonly EuVatChecker euVatChecker;

        public CheckVatID(EuVatChecker euVatChecker)
        {
            this.euVatChecker = euVatChecker;
        }

        [FunctionName("CheckVatID")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            var parsed = HttpUtility.ParseQueryString(requestBody);
            var countryCode = parsed["text"].Substring(0, 2);
            var vatNumber = parsed["text"].Substring(2).Replace(" ", string.Empty);

            var xmlContent = await euVatChecker.PostXMLToEU(countryCode, vatNumber);
            var soapResponse = XDocument.Parse(xmlContent.ToString());

            var valParam = euVatChecker.GetValidEUParam(soapResponse);
            var responseToSlack = $"{valParam.cCode}{valParam.vatNum}\n{valParam.name}\n{valParam.address}";

            return new OkObjectResult(responseToSlack);
        }
    }
}
