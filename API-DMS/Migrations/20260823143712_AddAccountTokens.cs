using System;
using Microsoft.EntityFrameworkCore.Migrations;


namespace API_DMS.Migrations
{
    public partial class AddAccountTokens : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "account_token",
                schema: "dms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "text", nullable: false),
                    purpose = table.Column<string>(type: "varchar", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_token", x => x.id);
                    table.CheckConstraint("CK_account_token_hash_length", "length(token_hash) <= 128");
                    table.CheckConstraint("CK_account_token_purpose", "purpose IN ('EmailConfirmation', 'PasswordReset')");
                    table.ForeignKey(
                        name: "FK_account_token_user_user_id",
                        column: x => x.user_id,
                        principalSchema: "dms",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_account_token_token_hash",
                schema: "dms",
                table: "account_token",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_account_token_user_id_purpose",
                schema: "dms",
                table: "account_token",
                columns: new[] { "user_id", "purpose" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_token",
                schema: "dms");
        }
    }
}
