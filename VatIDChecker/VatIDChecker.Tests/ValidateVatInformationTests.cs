using Moq;
using System;
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
            var checker = new VatIDCheck(factory);

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
            var checker = new VatIDCheck(factory);

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
            var checker = new VatIDCheck(factory);

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
        public void SuccessfulValidationCountryCode()
        {
            // Prepare
            var factory = Mock.Of<IHttpClientFactory>();
            var checker = new VatIDCheck(factory);

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
            var checker = new VatIDCheck(factory);

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
            var checker = new VatIDCheck(factory);

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
            var checker = new VatIDCheck(factory);

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
    }
}
