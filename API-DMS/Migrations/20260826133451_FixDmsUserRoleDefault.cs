using Microsoft.EntityFrameworkCore.Migrations;


namespace API_DMS.Migrations
{
    public partial class FixDmsUserRoleDefault : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "role",
                schema: "dms",
                table: "user",
                type: "varchar",
                nullable: false,
                defaultValueSql: "'Clerk'",
                oldClrType: typeof(string),
                oldType: "varchar",
                oldDefaultValueSql: "'Citizen'");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "role",
                schema: "dms",
                table: "user",
                type: "varchar",
                nullable: false,
                defaultValueSql: "'Citizen'",
                oldClrType: typeof(string),
                oldType: "varchar",
                oldDefaultValueSql: "'Clerk'");
        }
    }
}
