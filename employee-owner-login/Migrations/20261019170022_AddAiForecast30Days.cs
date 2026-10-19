using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoultryOS.Migrations
{
    /// <inheritdoc />
    public partial class AddAiForecast30Days : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AiForecast30DaysJson",
                table: "Forecasts",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiForecast30DaysJson",
                table: "Forecasts");
        }
    }
}
