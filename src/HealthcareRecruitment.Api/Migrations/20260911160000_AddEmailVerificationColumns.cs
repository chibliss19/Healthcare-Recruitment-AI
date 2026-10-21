using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareRecruitment.Api.Migrations
{
    /// <summary>
    /// The earlier "AddEmailVerification" migration (20260907195413) is a no-op — its snapshot was
    /// generated before IsEmailVerified/VerificationToken/VerificationTokenExpiresAt existed on the
    /// User model, so it never actually added these columns to any real database. This migration
    /// adds them for real, as idempotent SQL (see MigrationSqlHelpers for why a plain
    /// "IF NOT EXISTS" clause doesn't work on real MySQL) rather than the standard AddColumn API —
    /// it's possible a database already has some of these columns from an earlier manual attempt,
    /// and this makes it safe either way instead of hard-failing app startup with
    /// "Duplicate column name".
    /// </summary>
    public partial class AddEmailVerificationColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(MigrationSqlHelpers.AddColumnIfNotExists("Users", "IsEmailVerified", "tinyint(1) NOT NULL DEFAULT 0"));
            migrationBuilder.Sql(MigrationSqlHelpers.AddColumnIfNotExists("Users", "VerificationToken", "longtext NULL"));
            migrationBuilder.Sql(MigrationSqlHelpers.AddColumnIfNotExists("Users", "VerificationTokenExpiresAt", "datetime(6) NULL"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(MigrationSqlHelpers.DropColumnIfExists("Users", "VerificationTokenExpiresAt"));
            migrationBuilder.Sql(MigrationSqlHelpers.DropColumnIfExists("Users", "VerificationToken"));
            migrationBuilder.Sql(MigrationSqlHelpers.DropColumnIfExists("Users", "IsEmailVerified"));
        }
    }
}
