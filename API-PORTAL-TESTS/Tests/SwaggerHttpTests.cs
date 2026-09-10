using API_PORTAL_TESTS.Infrastructure;
using System.Net;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace API_PORTAL_TESTS.Tests
{
    public sealed class SwaggerHttpTests : IClassFixture<PortalApiFactory>
    {
        private readonly PortalApiFactory factory;

        public SwaggerHttpTests(PortalApiFactory factory)
        {
            this.factory = factory;
        }

        [Fact]
        public async Task Swagger_document_is_generated_successfully()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            using var client = factory.CreateClient();
            using var response = await client.GetAsync(
                "/swagger/v1/swagger.json",
                cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            Assert.True(response.StatusCode == HttpStatusCode.OK,
                $"Swagger a răspuns {(int)response.StatusCode}: {body}");
        }
    }
}
