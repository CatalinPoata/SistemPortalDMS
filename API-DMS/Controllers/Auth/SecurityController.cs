using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API_DMS.Controllers.Auth
{
    [ApiController]
    [Route("api/security")]
    public sealed class SecurityController : ControllerBase
    {
        private readonly IAntiforgery antiforgery;

        public SecurityController(IAntiforgery antiforgery)
        {
            this.antiforgery = antiforgery;
        }

        [AllowAnonymous]
        [HttpGet("csrf")]
        public IActionResult GetCsrfToken()
        {
            var tokens =
                antiforgery.GetAndStoreTokens(HttpContext);

            return Ok(new
            {
                token = tokens.RequestToken
            });
        }
    }
}
