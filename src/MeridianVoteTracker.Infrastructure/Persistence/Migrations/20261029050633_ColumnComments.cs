using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeridianVoteTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ColumnComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "session",
                table: "votes",
                type: "integer",
                nullable: false,
                comment: "Number of the assembly's yearly session (README section 2), as published in the file: 79 = 2024, 78 = 2023, ... 70 = 2015. One session holds many votes on many different dates; it is not a day.",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<Guid>(
                name: "details_import_id",
                table: "votes",
                type: "uuid",
                nullable: false,
                comment: "The import that supplied the current title and date.",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "assembly_vote_id",
                table: "votes",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                comment: "The assembly's own permanent vote id, e.g. MA/2024/003.",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "old_value",
                table: "vote_events",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                comment: "Display text of the value before the change, e.g. Y, a title, or 'Yes / weight 4'.",
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "new_value",
                table: "vote_events",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                comment: "Display text of the value after the change.",
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "source_line",
                table: "country_votes",
                type: "integer",
                nullable: false,
                comment: "Physical line in the uploaded file; the header is line 1.",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "approved_by_user_id",
                table: "country_votes",
                type: "integer",
                nullable: true,
                comment: "Set only when a person approved this value by accepting a conflict.",
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "session",
                table: "votes",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Number of the assembly's yearly session (README section 2), as published in the file: 79 = 2024, 78 = 2023, ... 70 = 2015. One session holds many votes on many different dates; it is not a day.");

            migrationBuilder.AlterColumn<Guid>(
                name: "details_import_id",
                table: "votes",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "The import that supplied the current title and date.");

            migrationBuilder.AlterColumn<string>(
                name: "assembly_vote_id",
                table: "votes",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldComment: "The assembly's own permanent vote id, e.g. MA/2024/003.");

            migrationBuilder.AlterColumn<string>(
                name: "old_value",
                table: "vote_events",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true,
                oldComment: "Display text of the value before the change, e.g. Y, a title, or 'Yes / weight 4'.");

            migrationBuilder.AlterColumn<string>(
                name: "new_value",
                table: "vote_events",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true,
                oldComment: "Display text of the value after the change.");

            migrationBuilder.AlterColumn<int>(
                name: "source_line",
                table: "country_votes",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Physical line in the uploaded file; the header is line 1.");

            migrationBuilder.AlterColumn<int>(
                name: "approved_by_user_id",
                table: "country_votes",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true,
                oldComment: "Set only when a person approved this value by accepting a conflict.");
        }
    }
}
