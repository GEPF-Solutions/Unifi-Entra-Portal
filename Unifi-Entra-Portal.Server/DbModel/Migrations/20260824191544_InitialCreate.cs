using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifi_Entra_Portal.Server.DbModel.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuthorizedGuests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MacAddress = table.Column<string>(type: "TEXT", nullable: false),
                    UserObjectId = table.Column<string>(type: "TEXT", nullable: false),
                    UserPrincipalName = table.Column<string>(type: "TEXT", nullable: true),
                    AuthorizedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastValidatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthorizedGuests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuthorizedGuests_MacAddress",
                table: "AuthorizedGuests",
                column: "MacAddress",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuthorizedGuests");
        }
    }
}
