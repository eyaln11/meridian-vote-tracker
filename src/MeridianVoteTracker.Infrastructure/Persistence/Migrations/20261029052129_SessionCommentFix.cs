using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeridianVoteTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SessionCommentFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "session",
                table: "votes",
                type: "integer",
                nullable: false,
                comment: "Session number as printed by the assembly (README section 2). It is only a label: one session holds many votes on many dates, and the year cannot be derived from it. The 2015-2024 files fit 1945 + N (70 = 2015 ... 79 = 2024), but the scanned sheets do not (61 = 2007, 64 = 2009, 67 = 2013). The reliable date of a vote is vote_date.",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Number of the assembly's yearly session (README section 2), as published in the file: 79 = 2024, 78 = 2023, ... 70 = 2015. One session holds many votes on many different dates; it is not a day.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "session",
                table: "votes",
                type: "integer",
                nullable: false,
                comment: "Number of the assembly's yearly session (README section 2), as published in the file: 79 = 2024, 78 = 2023, ... 70 = 2015. One session holds many votes on many different dates; it is not a day.",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Session number as printed by the assembly (README section 2). It is only a label: one session holds many votes on many dates, and the year cannot be derived from it. The 2015-2024 files fit 1945 + N (70 = 2015 ... 79 = 2024), but the scanned sheets do not (61 = 2007, 64 = 2009, 67 = 2013). The reliable date of a vote is vote_date.");
        }
    }
}
