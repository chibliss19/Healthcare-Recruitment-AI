using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareRecruitment.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddVacancyEmbeddingColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Embedding",
                table: "Vacancies",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Embedding",
                table: "Vacancies");
        }
    }
}
