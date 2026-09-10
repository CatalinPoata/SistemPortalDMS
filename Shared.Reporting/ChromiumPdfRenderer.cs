using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Reporting
{
    public interface IPdfRenderer
    {
        Task<byte[]> RenderAsync(
            string html,
            CancellationToken cancellationToken = default);

        Task<byte[]> RenderAsync(
            string html,
            PdfRenderOptions options,
            CancellationToken cancellationToken = default);
    }

    public sealed record PdfRenderOptions(
        bool ShowPageNumbers = true);

    public sealed class ChromiumPdfRenderer : IPdfRenderer, IDisposable
    {
        private readonly SemaphoreSlim gate = new(1, 1);

        public async Task<byte[]> RenderAsync(
            string html,
            CancellationToken cancellationToken = default)
        {
            return await RenderAsync(
                html,
                new PdfRenderOptions(),
                cancellationToken);
        }

        public async Task<byte[]> RenderAsync(
            string html,
            PdfRenderOptions options,
            CancellationToken cancellationToken = default)
        {
            if (!await gate.WaitAsync(0, cancellationToken))
            {
                throw new System.TimeoutException(
                    "Motorul PDF este ocupat.");
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var playwright =
                    await Playwright.CreateAsync();

                await using var browser =
                    await playwright.Chromium.LaunchAsync(
                        new BrowserTypeLaunchOptions
                        {
                            Headless = true,
                            Timeout = 15_000
                        });

                await using var context =
                    await browser.NewContextAsync(
                        new BrowserNewContextOptions
                        {
                            JavaScriptEnabled = false,
                            Offline = true,
                            ServiceWorkers = ServiceWorkerPolicy.Block,
                            AcceptDownloads = false
                        });

                await context.RouteAsync(
                    "**/*",
                    route => route.AbortAsync());

                var page = await context.NewPageAsync();

                await page.SetContentAsync(
                    html,
                    new PageSetContentOptions
                    {
                        WaitUntil = WaitUntilState.Load,
                        Timeout = 10_000
                    })
                    .WaitAsync(cancellationToken);

                await page.EvaluateAsync(
                    "() => document.fonts.ready.then(() => true)")
                    .WaitAsync(
                        TimeSpan.FromSeconds(10),
                        cancellationToken);

                return await page.PdfAsync(
                    new PagePdfOptions
                    {
                        Format = "A4",
                        PreferCSSPageSize = true,
                        PrintBackground = true,
                        DisplayHeaderFooter = options.ShowPageNumbers,
                        HeaderTemplate = options.ShowPageNumbers
                            ? "<span></span>"
                            : null,
                        FooterTemplate = options.ShowPageNumbers
                            ? """
                        <div style="font-family:'DejaVu Sans',sans-serif;
                                    font-size:9px;
                                    width:100%;
                                    text-align:center;">
                            Pagina <span class="pageNumber"></span>
                            din <span class="totalPages"></span>
                        </div>
                        """
                            : null
                    })
                    .WaitAsync(
                        TimeSpan.FromSeconds(30),
                        cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        }

        public void Dispose() => gate.Dispose();
    }
}
