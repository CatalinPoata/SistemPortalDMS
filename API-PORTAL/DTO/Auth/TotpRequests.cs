using System.ComponentModel.DataAnnotations;

namespace API_PORTAL.DTO.Auth;

public sealed class EnableTotpRequest
{
    [Required]
    public string Code { get; set; } = null!;
}

public sealed class VerifyTotpLoginRequest
{
    [Required]
    public string Challenge { get; set; } = null!;

    [Required]
    public string Code { get; set; } = null!;
}

public sealed class DisableTotpRequest
{
    [Required]
    public string CurrentPassword { get; set; } = null!;

    [Required]
    public string Code { get; set; } = null!;
}
