using Microsoft.EntityFrameworkCore.Migrations;


namespace API_PORTAL.Migrations
{
    public partial class VariousFixes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_user_role",
                schema: "portal",
                table: "user");

            migrationBuilder.AddCheckConstraint(
                name: "CK_user_role",
                schema: "portal",
                table: "user",
                sql: "role IN ('Citizen', 'Admin')");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_user_role",
                schema: "portal",
                table: "user");

            migrationBuilder.AddCheckConstraint(
                name: "CK_user_role",
                schema: "portal",
                table: "user",
                sql: "role IN ('Citizen', 'Clerk', 'Admin')");
        }
    }
}
