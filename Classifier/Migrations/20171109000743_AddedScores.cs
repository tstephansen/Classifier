using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace Classifier.Migrations
{
    public partial class AddedScores : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "AverageScore",
                table: "DocumentTypes",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "MaxScore",
                table: "DocumentTypes",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "MinScore",
                table: "DocumentTypes",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AverageScore",
                table: "DocumentTypes");

            migrationBuilder.DropColumn(
                name: "MaxScore",
                table: "DocumentTypes");

            migrationBuilder.DropColumn(
                name: "MinScore",
                table: "DocumentTypes");
        }
    }
}
