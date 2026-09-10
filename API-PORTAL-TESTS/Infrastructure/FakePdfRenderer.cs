using Shared.Reporting;

namespace API_PORTAL_TESTS.Infrastructure;

public sealed class FakePdfRenderer : IPdfRenderer
{
    public string? LastHtml { get; private set; }

    public Task<byte[]> RenderAsync(
        string html,
        CancellationToken cancellationToken = default) =>
        RenderAsync(html, new PdfRenderOptions(), cancellationToken);

    public Task<byte[]> RenderAsync(
        string html,
        PdfRenderOptions options,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastHtml = html;
        return Task.FromResult("%PDF-1.7\\n% test"u8.ToArray());
    }
}
