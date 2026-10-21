using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareRecruitment.Api.Migrations
{
    /// <summary>
    /// Adds the forgot-password columns. Same idempotent SQL pattern as
    /// AddEmailVerificationColumns (see MigrationSqlHelpers) rather than the standard AddColumn
    /// API — safe to apply even if a database already has some of these columns from an earlier
    /// attempt.
    /// </summary>
    public partial class AddPasswordResetToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(MigrationSqlHelpers.AddColumnIfNotExists("Users", "PasswordResetToken", "longtext NULL"));
            migrationBuilder.Sql(MigrationSqlHelpers.AddColumnIfNotExists("Users", "PasswordResetTokenExpiresAt", "datetime(6) NULL"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(MigrationSqlHelpers.DropColumnIfExists("Users", "PasswordResetTokenExpiresAt"));
            migrationBuilder.Sql(MigrationSqlHelpers.DropColumnIfExists("Users", "PasswordResetToken"));
        }
    }
}
