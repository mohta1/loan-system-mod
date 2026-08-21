using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace LoanSystem.Api.Infrastructure.Migrations;
[DbContext(typeof(PlatformDbContext))]
[Migration("20260821000000_InitialPlatform")]
public partial class InitialPlatform : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) { migrationBuilder.EnsureSchema("platform"); migrationBuilder.CreateTable(name: "PlatformMarkers", schema: "platform", columns: table => new { Id = table.Column<int>("int", nullable: false).Annotation("SqlServer:Identity", "1, 1"), Name = table.Column<string>("nvarchar(100)", maxLength: 100, nullable: false) }, constraints: table => table.PrimaryKey("PK_PlatformMarkers", x => x.Id)); }
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("PlatformMarkers", "platform");
}
