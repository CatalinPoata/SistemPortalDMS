using Microsoft.EntityFrameworkCore.Migrations;


namespace API_DMS.Migrations
{
    public partial class ChangedEnumsOutboxMessage2 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_registry_entry_applicant_name",
                schema: "dms",
                table: "registry_entry");

            migrationBuilder.DropIndex(
                name: "IX_registry_entry_subject",
                schema: "dms",
                table: "registry_entry");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateIndex(
                name: "IX_registry_entry_applicant_name",
                schema: "dms",
                table: "registry_entry",
                column: "applicant_name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_registry_entry_subject",
                schema: "dms",
                table: "registry_entry",
                column: "subject")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_registry_entry_applicant_name",
                schema: "dms",
                table: "registry_entry");

            migrationBuilder.DropIndex(
                name: "IX_registry_entry_subject",
                schema: "dms",
                table: "registry_entry");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateIndex(
                name: "IX_registry_entry_applicant_name",
                schema: "dms",
                table: "registry_entry",
                column: "applicant_name");

            migrationBuilder.CreateIndex(
                name: "IX_registry_entry_subject",
                schema: "dms",
                table: "registry_entry",
                column: "subject");
        }
    }
}
