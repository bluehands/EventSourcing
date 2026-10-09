using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventSourcing.Persistence.EntityFramework.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.CreateSequence<long>(name: "EventPosition", schema: "public");
            migrationBuilder.Sql("ALTER SEQUENCE public.\"EventPosition\" CACHE 1 NO CYCLE;");

            migrationBuilder.CreateTable(
                name: "Events",
                schema: "public",
                columns: table => new
                {
                    Position = table.Column<long>(type: "bigint", nullable: false),
                    StreamType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    StreamId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EventType = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.Position);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Events_StreamType_StreamId",
                schema: "public",
                table: "Events",
                columns: new[] { "StreamType", "StreamId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropSequence(name: "EventPosition", schema: "public");

            migrationBuilder.DropTable(
                name: "Events",
                schema: "public");
        }
    }
}
