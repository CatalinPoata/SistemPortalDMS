using API_PORTAL.Entities.Base;
using System.Text.Json;

namespace API_PORTAL.Entities;

public sealed class UserTotp : BaseEntity
{
    public Guid id { get; set; }

    public Guid user_id { get; set; }

    public User user { get; set; } = null!;

    public string protected_secret { get; set; } = null!;

    public bool is_enabled { get; set; }

    public long? last_used_counter { get; set; }

    public JsonDocument recovery_code_hashes { get; set; } =
        JsonDocument.Parse("[]");

    public DateTimeOffset? enabled_at { get; set; }
}
