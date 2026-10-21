namespace HealthcareRecruitment.Api.Migrations;

/// <summary>
/// MySQL's `ALTER TABLE ... ADD COLUMN IF NOT EXISTS ...` — despite matching the documented
/// grammar — was found to throw a real syntax error against this project's actual MySQL 8.0.46
/// install (confirmed live, not assumed). So idempotent "add this column only if it isn't already
/// there" is instead built by hand: look the column up in INFORMATION_SCHEMA, build the ALTER
/// TABLE text only if it's missing, and PREPARE/EXECUTE whichever statement results — the standard
/// portable idiom for conditional DDL in MySQL. Requires "AllowUserVariables=True" on the
/// connection string (see appsettings.json), since MySqlConnector otherwise treats "@name" tokens
/// as ADO.NET parameters rather than the session user variable this pattern relies on.
/// </summary>
internal static class MigrationSqlHelpers
{
    public static string AddColumnIfNotExists(string table, string column, string columnDefinition)
    {
        var addStatement = $"ALTER TABLE `{table}` ADD COLUMN `{column}` {columnDefinition}".Replace("'", "''");
        return $"""
            SET @preparedStatement = (SELECT IF(
              (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{table}' AND COLUMN_NAME = '{column}') > 0,
              'SELECT 1',
              '{addStatement}'
            ));
            PREPARE addColumnIfNotExists FROM @preparedStatement;
            EXECUTE addColumnIfNotExists;
            DEALLOCATE PREPARE addColumnIfNotExists;
            """;
    }

    public static string DropColumnIfExists(string table, string column)
    {
        var dropStatement = $"ALTER TABLE `{table}` DROP COLUMN `{column}`".Replace("'", "''");
        return $"""
            SET @preparedStatement = (SELECT IF(
              (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{table}' AND COLUMN_NAME = '{column}') > 0,
              '{dropStatement}',
              'SELECT 1'
            ));
            PREPARE dropColumnIfExists FROM @preparedStatement;
            EXECUTE dropColumnIfExists;
            DEALLOCATE PREPARE dropColumnIfExists;
            """;
    }
}
