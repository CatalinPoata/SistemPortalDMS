using Microsoft.EntityFrameworkCore.Migrations;


namespace API_DMS.Migrations
{
    public partial class VariousFixes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_user_role",
                schema: "dms",
                table: "user");

            migrationBuilder.AddCheckConstraint(
                name: "CK_user_role",
                schema: "dms",
                table: "user",
                sql: "role IN ('Clerk', 'Admin')");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_user_role",
                schema: "dms",
                table: "user");

            migrationBuilder.AddCheckConstraint(
                name: "CK_user_role",
                schema: "dms",
                table: "user",
                sql: "role IN ('Citizen', 'Clerk', 'Admin')");
        }
    }
}
