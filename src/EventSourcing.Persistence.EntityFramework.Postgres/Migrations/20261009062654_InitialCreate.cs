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

            migrationBuilder.CreateTable(
                name: "EventPositionCounter",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    LastPosition = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventPositionCounter", x => x.Id);
                    table.CheckConstraint("CK_EventPositionCounter_SingleRow", "\"Id\" = 1 AND \"LastPosition\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "Events",
                schema: "public",
                columns: table => new
                {
                    Position = table.Column<long>(type: "bigint", nullable: false),
                    StreamType = table.Column<string>(type: "text", nullable: false),
                    StreamId = table.Column<string>(type: "text", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.Position);
                });

            migrationBuilder.InsertData(
                schema: "public",
                table: "EventPositionCounter",
                columns: new[] { "Id", "LastPosition" },
                values: new object[] { 1, 0L });

            migrationBuilder.CreateIndex(
                name: "IX_Events_StreamType_StreamId",
                schema: "public",
                table: "Events",
                columns: new[] { "StreamType", "StreamId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventPositionCounter",
                schema: "public");

            migrationBuilder.DropTable(
                name: "Events",
                schema: "public");
        }
    }
}
