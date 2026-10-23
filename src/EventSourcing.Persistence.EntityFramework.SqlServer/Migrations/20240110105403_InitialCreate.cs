using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventSourcing.Persistence.EntityFramework.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TYPE dbo.EventBatch AS TABLE (
                    InputOrdinal bigint NOT NULL PRIMARY KEY,
                    StreamType nvarchar(128) NOT NULL,
                    StreamId nvarchar(256) NOT NULL,
                    EventType nvarchar(450) NOT NULL,
                    Payload nvarchar(max) NOT NULL,
                    Timestamp datetimeoffset NOT NULL
                );
                """);
            migrationBuilder.CreateTable(
                name: "Events",
                schema: "dbo",
                columns: table => new
                {
                    Position = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StreamType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    StreamId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.Position);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Events_StreamType_StreamId",
                schema: "dbo",
                table: "Events",
                columns: new[] { "StreamType", "StreamId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Events", schema: "dbo");
            migrationBuilder.Sql("DROP TYPE dbo.EventBatch;");
        }
    }
}
