using API_DMS.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Reporting;

namespace API_DMS.Controllers.Diagnostics
{
    [ApiController]
    [Route("api/diagnostics")]
    [Authorize(Roles = "Admin")]
    public sealed class PdfDiagnosticsController : ControllerBase
    {
        private readonly IPdfRenderer renderer;
        private readonly IHostEnvironment environment;

        public PdfDiagnosticsController(
            IPdfRenderer renderer,
            IHostEnvironment environment)
        {
            this.renderer = renderer;
            this.environment = environment;
        }

        [HttpPost("pdf")]
        [ResponseCache(
            NoStore = true,
            Location = ResponseCacheLocation.None)]
        [ProducesResponseType(typeof(byte[]), 200, "application/pdf")]
        [ProducesResponseType(typeof(ProblemDetails), 401)]
        [ProducesResponseType(typeof(ProblemDetails), 403)]
        [ProducesResponseType(typeof(ProblemDetails), 404)]
        [ProducesResponseType(typeof(ProblemDetails), 503)]
        public async Task<IActionResult> Export(
            CancellationToken cancellationToken)
        {
            if (!environment.IsDevelopment() &&
                !environment.IsEnvironment("Testing"))
            {
                return NotFound();
            }

            try
            {
                var pdf = await renderer.RenderAsync(
                    PdfSmokeDocument.CreateHtml(),
                    cancellationToken);

                return File(
                    pdf,
                    "application/pdf",
                    "pdf-smoke.pdf");
            }
            catch (System.TimeoutException)
            {
                return Problem(
                    statusCode:
                        StatusCodes.Status503ServiceUnavailable,
                    title:
                        "Generarea PDF este temporar indisponibilă",
                    detail:
                        "Motorul este ocupat sau randarea a durat prea mult. Reîncearcă.");
            }
        }
    }
}
