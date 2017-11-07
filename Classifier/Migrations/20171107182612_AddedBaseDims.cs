using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace Classifier.Migrations
{
    public partial class AddedBaseDims : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BaseHeight",
                table: "DocumentCriteria",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BaseWidth",
                table: "DocumentCriteria",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BaseHeight",
                table: "DocumentCriteria");

            migrationBuilder.DropColumn(
                name: "BaseWidth",
                table: "DocumentCriteria");
        }
    }
}
