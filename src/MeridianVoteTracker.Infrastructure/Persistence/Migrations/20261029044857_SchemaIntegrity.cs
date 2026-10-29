using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeridianVoteTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SchemaIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "country_code",
                table: "vote_events",
                type: "character(3)",
                fixedLength: true,
                maxLength: 3,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(3)",
                oldMaxLength: 3,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "country_code",
                table: "member_states",
                type: "character(3)",
                fixedLength: true,
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(3)",
                oldMaxLength: 3);

            migrationBuilder.AlterColumn<string>(
                name: "country_code",
                table: "country_votes",
                type: "character(3)",
                fixedLength: true,
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(3)",
                oldMaxLength: 3);

            migrationBuilder.AlterColumn<string>(
                name: "country_code",
                table: "conflicts",
                type: "character(3)",
                fixedLength: true,
                maxLength: 3,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(3)",
                oldMaxLength: 3,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "countries",
                columns: table => new
                {
                    code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_countries", x => x.code);
                    table.CheckConstraint("ck_countries_code", "code ~ '^[A-Z]{3}$'");
                });

            // Data migration: the country name moves from member_states (one copy per period) to countries (one row per country).
            // The names are copied before the column is dropped, so a database that is already seeded loses nothing.
            migrationBuilder.Sql(
                "INSERT INTO countries (code, name) " +
                "SELECT DISTINCT ON (country_code) country_code, country_name FROM member_states ORDER BY country_code, member_from DESC;");

            migrationBuilder.DropColumn(
                name: "country_name",
                table: "member_states");

            migrationBuilder.AddCheckConstraint(
                name: "ck_votes_session",
                table: "votes",
                sql: "session > 0");

            migrationBuilder.CreateIndex(
                name: "ix_vote_events_country_code",
                table: "vote_events",
                column: "country_code");

            migrationBuilder.AddCheckConstraint(
                name: "ck_vote_events_conflict",
                table: "vote_events",
                sql: "type NOT IN ('ConflictOpened', 'ConflictAccepted', 'ConflictRejected') OR conflict_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_vote_events_type",
                table: "vote_events",
                sql: "type IN ('VoteCreated', 'CountryVotesAdded', 'ConflictOpened', 'ConflictAccepted', 'ConflictRejected', 'PositionSet', 'PositionChanged')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_positions_stance",
                table: "positions",
                sql: "stance IN ('Yes', 'No')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_member_states_period",
                table: "member_states",
                sql: "member_to IS NULL OR member_to >= member_from");

            migrationBuilder.AddCheckConstraint(
                name: "ck_imports_status",
                table: "imports",
                sql: "status IN ('Queued', 'Processing', 'Completed', 'Failed')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_import_row_results_class",
                table: "import_row_results",
                sql: "class IN ('New', 'Unchanged', 'Duplicate', 'Invalid', 'Contradiction', 'Conflict')");

            migrationBuilder.CreateIndex(
                name: "ix_country_votes_country_code",
                table: "country_votes",
                column: "country_code");

            migrationBuilder.CreateIndex(
                name: "ix_conflicts_country_code",
                table: "conflicts",
                column: "country_code");

            migrationBuilder.AddCheckConstraint(
                name: "ck_conflicts_ballot_values",
                table: "conflicts",
                sql: "kind <> 'Ballot' OR (approved_value IN ('Y', 'N', 'A', 'X') AND proposed_value IN ('Y', 'N', 'A', 'X'))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_conflicts_kind",
                table: "conflicts",
                sql: "kind IN ('Ballot', 'Title', 'Date')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_conflicts_status",
                table: "conflicts",
                sql: "status IN ('Open', 'Accepted', 'Rejected')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_conflicts_values_differ",
                table: "conflicts",
                sql: "approved_value <> proposed_value");

            migrationBuilder.AddCheckConstraint(
                name: "ck_app_users_role",
                table: "app_users",
                sql: "role IN ('Analyst', 'Admin')");

            migrationBuilder.AddForeignKey(
                name: "fk_conflicts_countries_country_code",
                table: "conflicts",
                column: "country_code",
                principalTable: "countries",
                principalColumn: "code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_country_votes_countries_country_code",
                table: "country_votes",
                column: "country_code",
                principalTable: "countries",
                principalColumn: "code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_member_states_countries_country_code",
                table: "member_states",
                column: "country_code",
                principalTable: "countries",
                principalColumn: "code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_vote_events_countries_country_code",
                table: "vote_events",
                column: "country_code",
                principalTable: "countries",
                principalColumn: "code",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_conflicts_countries_country_code",
                table: "conflicts");

            migrationBuilder.DropForeignKey(
                name: "fk_country_votes_countries_country_code",
                table: "country_votes");

            migrationBuilder.DropForeignKey(
                name: "fk_member_states_countries_country_code",
                table: "member_states");

            migrationBuilder.DropForeignKey(
                name: "fk_vote_events_countries_country_code",
                table: "vote_events");

            migrationBuilder.DropTable(
                name: "countries");

            migrationBuilder.DropCheckConstraint(
                name: "ck_votes_session",
                table: "votes");

            migrationBuilder.DropIndex(
                name: "ix_vote_events_country_code",
                table: "vote_events");

            migrationBuilder.DropCheckConstraint(
                name: "ck_vote_events_conflict",
                table: "vote_events");

            migrationBuilder.DropCheckConstraint(
                name: "ck_vote_events_type",
                table: "vote_events");

            migrationBuilder.DropCheckConstraint(
                name: "ck_positions_stance",
                table: "positions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_member_states_period",
                table: "member_states");

            migrationBuilder.DropCheckConstraint(
                name: "ck_imports_status",
                table: "imports");

            migrationBuilder.DropCheckConstraint(
                name: "ck_import_row_results_class",
                table: "import_row_results");

            migrationBuilder.DropIndex(
                name: "ix_country_votes_country_code",
                table: "country_votes");

            migrationBuilder.DropIndex(
                name: "ix_conflicts_country_code",
                table: "conflicts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_conflicts_ballot_values",
                table: "conflicts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_conflicts_kind",
                table: "conflicts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_conflicts_status",
                table: "conflicts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_conflicts_values_differ",
                table: "conflicts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_app_users_role",
                table: "app_users");

            migrationBuilder.AlterColumn<string>(
                name: "country_code",
                table: "vote_events",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character(3)",
                oldFixedLength: true,
                oldMaxLength: 3,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "country_code",
                table: "member_states",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character(3)",
                oldFixedLength: true,
                oldMaxLength: 3);

            migrationBuilder.AddColumn<string>(
                name: "country_name",
                table: "member_states",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "country_code",
                table: "country_votes",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character(3)",
                oldFixedLength: true,
                oldMaxLength: 3);

            migrationBuilder.AlterColumn<string>(
                name: "country_code",
                table: "conflicts",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character(3)",
                oldFixedLength: true,
                oldMaxLength: 3,
                oldNullable: true);
        }
    }
}
