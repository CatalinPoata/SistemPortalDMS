using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;


namespace API_DMS.Migrations
{
    public partial class AddUserTotp : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_totp",
                schema: "dms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    protected_secret = table.Column<string>(type: "text", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    last_used_counter = table.Column<long>(type: "bigint", nullable: true),
                    recovery_code_hashes = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    enabled_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_totp", x => x.id);
                    table.CheckConstraint("CK_user_totp_secret_length", "length(protected_secret) <= 2048");
                    table.ForeignKey(
                        name: "FK_user_totp_user_user_id",
                        column: x => x.user_id,
                        principalSchema: "dms",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_totp_user_id",
                schema: "dms",
                table: "user_totp",
                column: "user_id",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_totp",
                schema: "dms");
        }
    }
}
