using Moq;
using System.Net.Http;
using Xunit;

namespace VatIDChecker.Tests
{
    public class ValidateVatInformationTests
    {
        [Fact]
        public void ValidationFailed()
        {
            // Prepare
            var factory = Mock.Of<IHttpClientFactory>();
            var checker = new VatIDCheck(factory, null);

            // Execute
            var (userResponse, foundError) = checker.ValidateVatInformation(
                string.Empty, string.Empty, string.Empty, string.Empty,
                new ValidationParams { valid = "false" });
            
            // Assert
            Assert.True(foundError);
            Assert.NotEmpty(userResponse);
        }

        [Fact]
        public void SuccessfulValidationCompanyName()
        {
            // Prepare
            var factory = Mock.Of<IHttpClientFactory>();
            var checker = new VatIDCheck(factory, null);

            // Execute
            var (userResponse, foundError) = checker.ValidateVatInformation(
                string.Empty, string.Empty, "software\narchitects", string.Empty,
                new ValidationParams { 
                    valid = "true", 
                    name = "software architects", 
                    address = string.Empty, 
                    vatNum = string.Empty, 
                    cCode = string.Empty 
                });

            // Assert
            Assert.False(foundError);
            Assert.Contains("Correct company name", userResponse);
        }

        [Fact]
        public void UnsuccessfulValidationCompanyName()
        {
            // Prepare
            var factory = Mock.Of<IHttpClientFactory>();
            var checker = new VatIDCheck(factory, null);

            // Execute
            var (userResponse, foundError) = checker.ValidateVatInformation(
                string.Empty, string.Empty, "dummy", string.Empty,
                new ValidationParams
                {
                    valid = "true",
                    name = "software architects",
                    address = string.Empty,
                    vatNum = string.Empty,
                    cCode = string.Empty
                });

            // Assert
            Assert.True(foundError);
            Assert.Contains("Incorrect company name", userResponse);
        }
        [Fact]
        public void SuccessfulValidationCompanyAddress()
        {
            // Prepare
            var factory = Mock.Of<IHttpClientFactory>();
            var checker = new VatIDCheck(factory, null);

            // Execute
            var (userResponse, foundError) = checker.ValidateVatInformation(
                string.Empty, string.Empty, string.Empty, "Musterstrasse 1 AT-4210 Gallneukirchen",
                new ValidationParams
                {
                    valid = "true",
                    name = string.Empty,
                    address = "Musterstrasse 1 AT-4210 Gallneukirchen",
                    vatNum = string.Empty,
                    cCode = string.Empty
                });

            // Assert
            Assert.False(foundError);
            Assert.Contains("Correct address", userResponse);
        }

        [Fact]
        public void UnsuccessfulValidationCompanyAddress()
        {
            // Prepare
            var factory = Mock.Of<IHttpClientFactory>();
            var checker = new VatIDCheck(factory, null);

            // Execute
            var (userResponse, foundError) = checker.ValidateVatInformation(
                string.Empty, string.Empty, string.Empty, "dummy",
                new ValidationParams
                {
                    valid = "true",
                    name = string.Empty,
                    address = "Musterstrasse 1 AT-4210 Gallneukirchen",
                    vatNum = string.Empty,
                    cCode = string.Empty
                });

            // Assert
            Assert.True(foundError);
            Assert.Contains("Incorrect address", userResponse);
        }

        [Fact]
        public void SuccessfulValidationCountryCode()
        {
            // Prepare
            var factory = Mock.Of<IHttpClientFactory>();
            var checker = new VatIDCheck(factory, null);

            // Execute
            var (userResponse, foundError) = checker.ValidateVatInformation(
                "AT", string.Empty, string.Empty, string.Empty,
                new ValidationParams
                {
                    valid = "true",
                    name = string.Empty,
                    address = string.Empty,
                    vatNum = string.Empty,
                    cCode = "AT"
                });

            // Assert
            Assert.False(foundError);
            Assert.Contains("Correct country code", userResponse);
        }

        [Fact]
        public void UnsuccessfulValidationCountryCode()
        {
            // Prepare
            var factory = Mock.Of<IHttpClientFactory>();
            var checker = new VatIDCheck(factory, null);

            // Execute
            var (userResponse, foundError) = checker.ValidateVatInformation(
                "---", string.Empty, string.Empty, string.Empty,
                new ValidationParams
                {
                    valid = "true",
                    name = string.Empty,
                    address = string.Empty,
                    vatNum = string.Empty,
                    cCode = "---"
                });

            // Assert
            Assert.True(foundError);
            Assert.Contains("Incorrect country code", userResponse);
        }
        [Fact]
        public void SuccessfulValidationVatNumber()
        {
            // Prepare
            var factory = Mock.Of<IHttpClientFactory>();
            var checker = new VatIDCheck(factory, null);

            // Execute
            var (userResponse, foundError) = checker.ValidateVatInformation(
                string.Empty, "U12345678", string.Empty, string.Empty,
                new ValidationParams
                {
                    valid = "true",
                    name = string.Empty,
                    address = string.Empty,
                    vatNum = "U12345678",
                    cCode = string.Empty
                });

            // Assert
            Assert.False(foundError);
            Assert.Contains("Correct vat-number", userResponse);
        }

        [Fact]
        public void UnsuccessfulValidationVatNumber()
        {
            // Prepare
            var factory = Mock.Of<IHttpClientFactory>();
            var checker = new VatIDCheck(factory, null);

            // Execute
            var (userResponse, foundError) = checker.ValidateVatInformation(
                string.Empty, "dummy", string.Empty, string.Empty,
                new ValidationParams
                {
                    valid = "true",
                    name = string.Empty,
                    address = string.Empty,
                    vatNum = "U12345678",
                    cCode = string.Empty
                });

            // Assert
            Assert.True(foundError);
            Assert.Contains("Incorrect vat-number:", userResponse);
        }

        [Fact]
        public void SuccessfulValidationCompanyAddressWithS()
        {
            // Prepare
            var factory = Mock.Of<IHttpClientFactory>();
            var checker = new VatIDCheck(factory, null);

            // Execute
            var (userResponse, foundError) = checker.ValidateVatInformation(
                string.Empty, string.Empty, string.Empty, "Musterstraﬂe 1 AT-4210 Gallneukirchen",
                new ValidationParams
                {
                    valid = "true",
                    name = string.Empty,
                    address = "Musterstrasse 1 AT-4210 Gallneukirchen",
                    vatNum = string.Empty,
                    cCode = string.Empty
                });

            // Assert
            Assert.False(foundError);
            Assert.Contains("Correct address", userResponse);
        }
    }
}
