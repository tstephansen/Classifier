using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace Classifier.Migrations
{
    public partial class AddedDimensions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Height",
                table: "DocumentCriteria",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MatchThreshold",
                table: "DocumentCriteria",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PositionX",
                table: "DocumentCriteria",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PositionY",
                table: "DocumentCriteria",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Width",
                table: "DocumentCriteria",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Height",
                table: "DocumentCriteria");

            migrationBuilder.DropColumn(
                name: "MatchThreshold",
                table: "DocumentCriteria");

            migrationBuilder.DropColumn(
                name: "PositionX",
                table: "DocumentCriteria");

            migrationBuilder.DropColumn(
                name: "PositionY",
                table: "DocumentCriteria");

            migrationBuilder.DropColumn(
                name: "Width",
                table: "DocumentCriteria");
        }
    }
}
