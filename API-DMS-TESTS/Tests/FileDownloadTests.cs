using API_DMS.Controllers.Workflow;
using API_DMS.Services;
using API_DMS.Storage;
using API_DMS_TESTS.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace API_DMS_TESTS.Tests
{
    public sealed class FileDownloadTests
    {
        [Fact]
        public async Task T13_Expired_token_returns_403()
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            var documentId = Guid.NewGuid();

            var clock =
                new FakeTimeProvider(
                    DateTimeOffset.UtcNow);

            var tokenService =
                new FileDownloadTokenService(
                    Options.Create(
                        new FileDownloadOptions
                        {
                            SigningKey =
                                new string('x', 32),
                            LifetimeMinutes = 15
                        }),
                    clock);

            var token =
                tokenService.Create(documentId)
                    .Token;

            clock.Advance(
                TimeSpan.FromMinutes(16));

            await using var db =
                TestDbContextFactory.CreateInMemory();

            var storage =
                new FileStorageService(
                    new TestHostEnvironment(),
                    Options.Create(
                        new FileStorageOptions()),
                    new FileContentDetector());

            var controller =
                new RegistryEntriesController(
                    db,
                    new RegistryNumberAllocator(db),
                    storage,
                    new RegistryEntryWorkflowService(db),
                    new RegistryTaskService(db),
                    tokenService);

            var result =
                await controller.DownloadDocument(
                    documentId,
                    token,
                    cancellationToken);

            var objectResult =
                Assert.IsType<ObjectResult>(result);

            Assert.Equal(
                StatusCodes.Status403Forbidden,
                objectResult.StatusCode);
        }

        private sealed class FakeTimeProvider
            : TimeProvider
        {
            private DateTimeOffset currentTime;

            public FakeTimeProvider(
                DateTimeOffset currentTime)
            {
                this.currentTime = currentTime;
            }

            public override DateTimeOffset GetUtcNow()
            {
                return currentTime;
            }

            public void Advance(TimeSpan duration)
            {
                currentTime = currentTime.Add(duration);
            }
        }

        private sealed class TestHostEnvironment
            : IHostEnvironment
        {
            public string EnvironmentName { get; set; }
                = "Testing";

            public string ApplicationName { get; set; }
                = "API-DMS.Tests";

            public string ContentRootPath { get; set; }
                = AppContext.BaseDirectory;

            public IFileProvider ContentRootFileProvider
            { get; set; }
                = new NullFileProvider();
        }
    }
}
