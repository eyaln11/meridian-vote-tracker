using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MeridianVoteTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PersistenceModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "app_users",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    username = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_app_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "imports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    stored_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    stage = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    uploaded_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    total_rows = table.Column<long>(type: "bigint", nullable: false),
                    rows_parsed = table.Column<long>(type: "bigint", nullable: false),
                    rows_applied = table.Column<long>(type: "bigint", nullable: false),
                    new_count = table.Column<long>(type: "bigint", nullable: false),
                    unchanged_count = table.Column<long>(type: "bigint", nullable: false),
                    duplicate_count = table.Column<long>(type: "bigint", nullable: false),
                    invalid_count = table.Column<long>(type: "bigint", nullable: false),
                    contradiction_count = table.Column<long>(type: "bigint", nullable: false),
                    conflict_count = table.Column<long>(type: "bigint", nullable: false),
                    conflicts_opened = table.Column<int>(type: "integer", nullable: false),
                    votes_created = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_imports", x => x.id);
                    table.ForeignKey(
                        name: "fk_imports_app_users_uploaded_by_user_id",
                        column: x => x.uploaded_by_user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "import_row_results",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    import_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    @class = table.Column<string>(name: "class", type: "character varying(20)", maxLength: 20, nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    assembly_vote_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    country_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    original_value = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_import_row_results", x => x.id);
                    table.ForeignKey(
                        name: "fk_import_row_results_imports_import_id",
                        column: x => x.import_id,
                        principalTable: "imports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "import_vote_summaries",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    import_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assembly_vote_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    vote_created = table.Column<bool>(type: "boolean", nullable: false),
                    new_count = table.Column<int>(type: "integer", nullable: false),
                    unchanged_count = table.Column<int>(type: "integer", nullable: false),
                    duplicate_count = table.Column<int>(type: "integer", nullable: false),
                    contradiction_count = table.Column<int>(type: "integer", nullable: false),
                    conflict_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_import_vote_summaries", x => x.id);
                    table.ForeignKey(
                        name: "fk_import_vote_summaries_imports_import_id",
                        column: x => x.import_id,
                        principalTable: "imports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "votes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    assembly_vote_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    session = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    vote_date = table.Column<DateOnly>(type: "date", nullable: false),
                    details_import_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_votes", x => x.id);
                    table.ForeignKey(
                        name: "fk_votes_imports_details_import_id",
                        column: x => x.details_import_id,
                        principalTable: "imports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "conflicts",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    vote_id = table.Column<int>(type: "integer", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    country_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    approved_value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    proposed_value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    approved_import_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposed_import_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposed_line = table.Column<int>(type: "integer", nullable: false),
                    opened_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    resolved_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolution_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conflicts", x => x.id);
                    table.CheckConstraint("ck_conflicts_country", "(kind = 'Ballot' AND country_code IS NOT NULL) OR (kind <> 'Ballot' AND country_code IS NULL)");
                    table.CheckConstraint("ck_conflicts_resolution", "(status = 'Open' AND resolved_by_user_id IS NULL AND resolved_at IS NULL) OR (status <> 'Open' AND resolved_by_user_id IS NOT NULL AND resolved_at IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_conflicts_app_users_resolved_by_user_id",
                        column: x => x.resolved_by_user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_conflicts_imports_approved_import_id",
                        column: x => x.approved_import_id,
                        principalTable: "imports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_conflicts_imports_proposed_import_id",
                        column: x => x.proposed_import_id,
                        principalTable: "imports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_conflicts_votes_vote_id",
                        column: x => x.vote_id,
                        principalTable: "votes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "country_votes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    vote_id = table.Column<int>(type: "integer", nullable: false),
                    country_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ballot = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    source_import_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_line = table.Column<int>(type: "integer", nullable: false),
                    approved_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_country_votes", x => x.id);
                    table.CheckConstraint("ck_country_votes_ballot", "ballot IN ('Y', 'N', 'A', 'X')");
                    table.ForeignKey(
                        name: "fk_country_votes_app_users_approved_by_user_id",
                        column: x => x.approved_by_user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_country_votes_imports_source_import_id",
                        column: x => x.source_import_id,
                        principalTable: "imports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_country_votes_votes_vote_id",
                        column: x => x.vote_id,
                        principalTable: "votes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "positions",
                columns: table => new
                {
                    vote_id = table.Column<int>(type: "integer", nullable: false),
                    stance = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    weight = table.Column<int>(type: "integer", nullable: false),
                    updated_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_positions", x => x.vote_id);
                    table.CheckConstraint("ck_positions_weight", "weight BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "fk_positions_app_users_updated_by_user_id",
                        column: x => x.updated_by_user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_positions_votes_vote_id",
                        column: x => x.vote_id,
                        principalTable: "votes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vote_events",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    vote_id = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor_user_id = table.Column<int>(type: "integer", nullable: true),
                    import_id = table.Column<Guid>(type: "uuid", nullable: true),
                    conflict_id = table.Column<long>(type: "bigint", nullable: true),
                    country_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    old_value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    new_value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vote_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_vote_events_app_users_actor_user_id",
                        column: x => x.actor_user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vote_events_conflicts_conflict_id",
                        column: x => x.conflict_id,
                        principalTable: "conflicts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vote_events_imports_import_id",
                        column: x => x.import_id,
                        principalTable: "imports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vote_events_votes_vote_id",
                        column: x => x.vote_id,
                        principalTable: "votes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_app_users_username",
                table: "app_users",
                column: "username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_conflicts_approved_import_id",
                table: "conflicts",
                column: "approved_import_id");

            migrationBuilder.CreateIndex(
                name: "ix_conflicts_proposed_import_id",
                table: "conflicts",
                column: "proposed_import_id");

            migrationBuilder.CreateIndex(
                name: "ix_conflicts_resolved_by_user_id",
                table: "conflicts",
                column: "resolved_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_conflicts_status_vote_id",
                table: "conflicts",
                columns: new[] { "status", "vote_id" });

            migrationBuilder.CreateIndex(
                name: "ix_conflicts_vote_id_kind_country_code_proposed_value",
                table: "conflicts",
                columns: new[] { "vote_id", "kind", "country_code", "proposed_value" },
                unique: true,
                filter: "country_code IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_conflicts_vote_id_kind_proposed_value",
                table: "conflicts",
                columns: new[] { "vote_id", "kind", "proposed_value" },
                unique: true,
                filter: "country_code IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_country_votes_approved_by_user_id",
                table: "country_votes",
                column: "approved_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_country_votes_source_import_id",
                table: "country_votes",
                column: "source_import_id");

            migrationBuilder.CreateIndex(
                name: "ix_country_votes_vote_id_country_code",
                table: "country_votes",
                columns: new[] { "vote_id", "country_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_import_row_results_import_id_class_line_number",
                table: "import_row_results",
                columns: new[] { "import_id", "class", "line_number" });

            migrationBuilder.CreateIndex(
                name: "ix_import_vote_summaries_import_id_assembly_vote_id",
                table: "import_vote_summaries",
                columns: new[] { "import_id", "assembly_vote_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_imports_uploaded_at",
                table: "imports",
                column: "uploaded_at");

            migrationBuilder.CreateIndex(
                name: "ix_imports_uploaded_by_user_id",
                table: "imports",
                column: "uploaded_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_positions_updated_by_user_id",
                table: "positions",
                column: "updated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_vote_events_actor_user_id",
                table: "vote_events",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_vote_events_conflict_id",
                table: "vote_events",
                column: "conflict_id");

            migrationBuilder.CreateIndex(
                name: "ix_vote_events_import_id",
                table: "vote_events",
                column: "import_id");

            migrationBuilder.CreateIndex(
                name: "ix_vote_events_vote_id_occurred_at_id",
                table: "vote_events",
                columns: new[] { "vote_id", "occurred_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_votes_assembly_vote_id",
                table: "votes",
                column: "assembly_vote_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_votes_details_import_id",
                table: "votes",
                column: "details_import_id");

            migrationBuilder.CreateIndex(
                name: "ix_votes_session",
                table: "votes",
                column: "session");

            migrationBuilder.CreateIndex(
                name: "ix_votes_vote_date",
                table: "votes",
                column: "vote_date");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "country_votes");

            migrationBuilder.DropTable(
                name: "import_row_results");

            migrationBuilder.DropTable(
                name: "import_vote_summaries");

            migrationBuilder.DropTable(
                name: "positions");

            migrationBuilder.DropTable(
                name: "vote_events");

            migrationBuilder.DropTable(
                name: "conflicts");

            migrationBuilder.DropTable(
                name: "votes");

            migrationBuilder.DropTable(
                name: "imports");

            migrationBuilder.DropTable(
                name: "app_users");
        }
    }
}
