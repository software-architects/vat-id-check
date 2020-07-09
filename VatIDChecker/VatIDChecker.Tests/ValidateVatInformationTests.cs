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
            // Pepare
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
            // Pepare
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
            // Pepare
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
    }
}
