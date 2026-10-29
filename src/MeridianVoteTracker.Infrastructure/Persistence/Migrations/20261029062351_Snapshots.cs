using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeridianVoteTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Snapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    filter_from = table.Column<DateOnly>(type: "date", nullable: true),
                    filter_to = table.Column<DateOnly>(type: "date", nullable: true),
                    filter_session = table.Column<int>(type: "integer", nullable: true),
                    filter_vote_ids = table.Column<List<string>>(type: "text[]", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    stage = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    total_rows = table.Column<long>(type: "bigint", nullable: false),
                    rows_processed = table.Column<long>(type: "bigint", nullable: false),
                    votes_selected = table.Column<int>(type: "integer", nullable: false),
                    votes_included = table.Column<int>(type: "integer", nullable: false),
                    votes_without_position = table.Column<int>(type: "integer", nullable: false),
                    countries_scored = table.Column<int>(type: "integer", nullable: false),
                    countries_unscored = table.Column<int>(type: "integer", nullable: false),
                    published_quarter = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    published_by_user_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_snapshots", x => x.id);
                    table.CheckConstraint("ck_snapshots_filter_dates", "filter_from IS NULL OR filter_to IS NULL OR filter_from <= filter_to");
                    table.CheckConstraint("ck_snapshots_published", "(published_quarter IS NULL AND published_at IS NULL AND published_by_user_id IS NULL) OR (published_quarter IS NOT NULL AND published_at IS NOT NULL AND published_by_user_id IS NOT NULL AND status = 'Completed')");
                    table.CheckConstraint("ck_snapshots_quarter", "published_quarter IS NULL OR published_quarter ~ '^[0-9]{4}-Q[1-4]$'");
                    table.CheckConstraint("ck_snapshots_status", "status IN ('Queued', 'Processing', 'Completed', 'Failed')");
                    table.ForeignKey(
                        name: "fk_snapshots_app_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_snapshots_app_users_published_by_user_id",
                        column: x => x.published_by_user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "snapshot_entries",
                columns: table => new
                {
                    snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    country_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    country_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    score = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    rank = table.Column<int>(type: "integer", nullable: true),
                    counted_weight = table.Column<long>(type: "bigint", nullable: false),
                    matching_weight = table.Column<long>(type: "bigint", nullable: false),
                    votes_counted = table.Column<int>(type: "integer", nullable: false),
                    votes_not_counted = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_snapshot_entries", x => new { x.snapshot_id, x.country_code });
                    table.CheckConstraint("ck_snapshot_entries_rank", "(score IS NULL) = (rank IS NULL) AND (rank IS NULL OR rank >= 1)");
                    table.CheckConstraint("ck_snapshot_entries_score", "score IS NULL OR score BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "fk_snapshot_entries_countries_country_code",
                        column: x => x.country_code,
                        principalTable: "countries",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_snapshot_entries_snapshots_snapshot_id",
                        column: x => x.snapshot_id,
                        principalTable: "snapshots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "snapshot_votes",
                columns: table => new
                {
                    snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vote_id = table.Column<int>(type: "integer", nullable: false),
                    assembly_vote_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    vote_date = table.Column<DateOnly>(type: "date", nullable: false),
                    stance = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    weight = table.Column<int>(type: "integer", nullable: true),
                    included = table.Column<bool>(type: "boolean", nullable: false),
                    excluded_reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_snapshot_votes", x => new { x.snapshot_id, x.vote_id });
                    table.CheckConstraint("ck_snapshot_votes_included", "(included AND stance IS NOT NULL AND weight BETWEEN 1 AND 5) OR (NOT included AND stance IS NULL AND weight IS NULL)");
                    table.CheckConstraint("ck_snapshot_votes_stance", "stance IN ('Yes', 'No')");
                    table.ForeignKey(
                        name: "fk_snapshot_votes_snapshots_snapshot_id",
                        column: x => x.snapshot_id,
                        principalTable: "snapshots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_snapshot_votes_votes_vote_id",
                        column: x => x.vote_id,
                        principalTable: "votes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "snapshot_contributions",
                columns: table => new
                {
                    snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    country_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    vote_id = table.Column<int>(type: "integer", nullable: false),
                    ballot = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_snapshot_contributions", x => new { x.snapshot_id, x.country_code, x.vote_id });
                    table.CheckConstraint("ck_snapshot_contributions_ballot", "ballot IN ('Y', 'N', 'A', 'X')");
                    table.CheckConstraint("ck_snapshot_contributions_outcome", "outcome IN ('Match', 'Against', 'NotCounted')");
                    table.ForeignKey(
                        name: "fk_snapshot_contributions_countries_country_code",
                        column: x => x.country_code,
                        principalTable: "countries",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_snapshot_contributions_snapshot_votes_snapshot_id_vote_id",
                        columns: x => new { x.snapshot_id, x.vote_id },
                        principalTable: "snapshot_votes",
                        principalColumns: new[] { "snapshot_id", "vote_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_snapshot_contributions_country_code",
                table: "snapshot_contributions",
                column: "country_code");

            migrationBuilder.CreateIndex(
                name: "ix_snapshot_contributions_snapshot_id_vote_id",
                table: "snapshot_contributions",
                columns: new[] { "snapshot_id", "vote_id" });

            migrationBuilder.CreateIndex(
                name: "ix_snapshot_entries_country_code",
                table: "snapshot_entries",
                column: "country_code");

            migrationBuilder.CreateIndex(
                name: "ix_snapshot_entries_snapshot_id_rank",
                table: "snapshot_entries",
                columns: new[] { "snapshot_id", "rank" });

            migrationBuilder.CreateIndex(
                name: "ix_snapshot_votes_vote_id",
                table: "snapshot_votes",
                column: "vote_id");

            migrationBuilder.CreateIndex(
                name: "ix_snapshots_created_at",
                table: "snapshots",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_snapshots_created_by_user_id",
                table: "snapshots",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_snapshots_published_by_user_id",
                table: "snapshots",
                column: "published_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_snapshots_published_quarter",
                table: "snapshots",
                column: "published_quarter",
                unique: true);

            // README SNP-04: a saved snapshot never changes. The database itself refuses to update or delete its rows,
            // so even a script or a bug in the code cannot rewrite what was published. New rows can still be added while a snapshot is being calculated.
            migrationBuilder.Sql(@"
CREATE FUNCTION prevent_snapshot_change() RETURNS trigger AS $$
BEGIN
    RAISE EXCEPTION 'Snapshot data is immutable (README SNP-04): % on % is not allowed', TG_OP, TG_TABLE_NAME;
END;
$$ LANGUAGE plpgsql;");
            foreach (var table in new[] { "snapshot_votes", "snapshot_entries", "snapshot_contributions" })
            {
                migrationBuilder.Sql($@"
CREATE TRIGGER {table}_immutable BEFORE UPDATE OR DELETE ON {table}
FOR EACH ROW EXECUTE FUNCTION prevent_snapshot_change();");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER snapshot_votes_immutable ON snapshot_votes;");
            migrationBuilder.Sql("DROP TRIGGER snapshot_entries_immutable ON snapshot_entries;");
            migrationBuilder.Sql("DROP TRIGGER snapshot_contributions_immutable ON snapshot_contributions;");
            migrationBuilder.Sql("DROP FUNCTION prevent_snapshot_change();");

            migrationBuilder.DropTable(
                name: "snapshot_contributions");

            migrationBuilder.DropTable(
                name: "snapshot_entries");

            migrationBuilder.DropTable(
                name: "snapshot_votes");

            migrationBuilder.DropTable(
                name: "snapshots");
        }
    }
}
