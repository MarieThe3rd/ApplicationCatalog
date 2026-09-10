using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApplicationCatalog.Modules.Applications.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "applications");

            migrationBuilder.CreateTable(
                name: "Applications",
                schema: "applications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AliasNames = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BusinessOwner = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ItOwner = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Applications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationLink",
                schema: "applications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationLink", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationLink_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalSchema: "applications",
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationProject",
                schema: "applications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FrameworkVersion = table.Column<int>(type: "int", nullable: false),
                    ProjectType = table.Column<int>(type: "int", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationProject", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationProject_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalSchema: "applications",
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationLink_ApplicationId",
                schema: "applications",
                table: "ApplicationLink",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationProject_ApplicationId",
                schema: "applications",
                table: "ApplicationProject",
                column: "ApplicationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationLink",
                schema: "applications");

            migrationBuilder.DropTable(
                name: "ApplicationProject",
                schema: "applications");

            migrationBuilder.DropTable(
                name: "Applications",
                schema: "applications");
        }
    }
}
