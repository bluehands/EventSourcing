using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventSourcing.Persistence.EntityFramework.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class SwitchToWAL : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                PRAGMA journal_mode=wal;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
