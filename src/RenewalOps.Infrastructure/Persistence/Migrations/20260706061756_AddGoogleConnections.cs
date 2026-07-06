using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RenewalOps.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGoogleConnections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GoogleConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EncryptedRefreshToken = table.Column<string>(type: "text", nullable: false),
                    Scopes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false),
                    ConnectedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoogleConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoogleConnections_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GoogleConnections_UserId",
                table: "GoogleConnections",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GoogleConnections");
        }
    }
}
