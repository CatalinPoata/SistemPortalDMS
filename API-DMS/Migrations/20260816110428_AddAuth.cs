using Microsoft.EntityFrameworkCore.Migrations;


namespace API_DMS.Migrations
{
    public partial class AddAuth : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_refresh_token_token_hash",
                schema: "dms",
                table: "refresh_token",
                column: "token_hash",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_refresh_token_token_hash",
                schema: "dms",
                table: "refresh_token");
        }
    }
}
