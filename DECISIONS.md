# Decisions and trade-offs

## 1. Stack and shape

- **Data access: EF Core with PostgreSQL.** Migrations, LINQ and change tracking cover most work; the few hot paths (bulk copy of one million ballots into a snapshot) use Npgsql directly. A micro-ORM would be faster but would mean writing and testing every query by hand.
- **Layers.** `Core` has no dependencies (pure rules, tested without a database), `Infrastructure` holds EF and the runners, `Api` holds HTTP. The import engine reaches the database through two small interfaces.
- **Background work: a `Channel` plus a `BackgroundService`, one job at a time.** Progress is written to the database every 500 ms, so a page refresh loses nothing. Interrupted jobs are marked failed on restart (re-uploading is safe). Trade-off: one API instance. For several instances use a durable queue.
- **Angular, minimal.** Four screens, signals, one typed client generated from the OpenAPI contract (`openapi-typescript` + `openapi-fetch`). Validation errors are shown next to fields; a lost race on a conflict is a clear message, not a generic error.
- **Sign-in is simulated** (`X-User` header, three predefined users). Roles are enforced on the server.

## 2. Ingestion rules (open points decided)

- **No silent fixes (ING-13).** `deu`, `Yes`, `21/02/2024` are rejected and reported with line, reason and original value; nothing is normalised. A wrong guess in voting data can change a ranking.
- **Contradiction vs conflict.** A contradiction is the file disagreeing with itself (same country, two ballots; or two titles/dates for one vote): nothing from those rows is stored. A conflict is a file disagreeing with *approved* data. Vote 025 has inconsistent titles inside `q1`, so it is a contradiction there (no approved data existed); in `rev2` the title is consistent and the vote is imported as new. The opposite reading (CON-03) is defensible; I chose the one that does not store a guess.
- **Membership** is checked on the vote's date; the last day of membership counts. Countries with two periods (Iceland) are checked against both.
- **Line numbers** are physical lines of the file, header = line 1, so an analyst can find the row in an editor.
- **Re-upload (ING-12).** A conflict is identified by (vote, kind, country, proposed value) and never recorded twice, enforced by unique indexes. A new proposal that differs from an already decided one is a new conflict.
- **Report storage.** Rows that need explaining (invalid, duplicate, contradiction, conflict) are stored with line, reason and original value; *new* rows are traceable through `country_votes.source_import_id + source_line`; *unchanged* rows are counted per vote. Storing every row of a 960,000-row file would double the write volume without adding information. HIS-01 asks for the result of every row: this is the one place where I traded literal completeness for volume.

## 3. Conflicts and history

- Deciding is one statement: `UPDATE ... WHERE id = ? AND status = 'Open'`. The row lock makes a concurrent second decision change zero rows, and it receives `409` with the current state (CON-05). Tested with two simultaneous requests.
- Accepting updates the approved ballot in place with its new source (file, line, who, when). The previous value is kept in the conflict row and on the vote's timeline (`vote_events`: type, actor, old value, new value). One timeline table serves HIS-02..04; `old_value`/`new_value` are display text (for positions, `Yes / weight 4`), a deliberate simplification.
- A vote missing from a new file is never touched (ING-09).

## 4. Scoring (open point decided)

Score = 100 x matching weight / counted weight, per country, over the chosen votes.

- **Only Yes and No count.** An abstention or absence is neither agreement nor disagreement, so it adds nothing to either part. It is still listed in the country's breakdown.
- **Non-members:** a vote where the country was not a member cannot exist (rejected at import, ING-04), so it cannot be counted.
- **No counted vote = no score and no rank** (not zero). Zero means votes counted and none agreed.
- **Votes without a position are left out and listed** in the snapshot with the reason (POS-04).
- **Ties share a rank** (1, 2, 2, 4).
- **Selection (SNP-01):** dates and session narrow together; listed vote ids are added to that selection; nothing chosen means all votes.
- **Reproducibility (SNP-04):** a snapshot stores its own copy of every position it used and every ballot's contribution. Database triggers refuse any `UPDATE` or `DELETE` on snapshot data, so even a script cannot rewrite it. The calculation is one transaction: a snapshot is complete or absent.
- **Publishing (SNP-05):** one published snapshot per quarter (unique index). Publishing a second one is refused with the name of the current one; the admin unpublishes first.

## 5. Database

- Integrity lives in the database: foreign keys (countries are a table, so an unknown code cannot be stored), `CHECK` constraints for every enum column (generated from the C# enums so they cannot drift), weight 1-5, conflict resolved-by/at rules, unique indexes for "one ballot per country per vote" and for conflicts.
- **No `sessions` table.** The session is only a number in the files; there are no session attributes, and the year cannot be derived from it (the scanned sheets show 61 = 2007, 67 = 2013, while 2015-2024 are 70-79). A table with one column filled from the same files would validate nothing. Column comments document it in the database.
- **Migrations run at API start-up.** Convenient for `docker compose up`, but with several instances they would race; in production migrations run as a separate release step.

## 6. Azure

Not used. The core needs no AI service; PostgreSQL runs in Docker, uploads go to a Docker volume, and the PDF bonus was not attempted: the correctness of ingestion, conflicts, history and snapshots came first. In production: Azure Database for PostgreSQL, Blob Storage for uploads, Key Vault for secrets, Application Insights for the JSON logs that already carry the correlation id.

## 7. Before production

Real authentication (Entra ID) with roles from claims; migrations as a release step; a durable job queue with cancellation; streaming uploads to Blob Storage; faster bulk loading of imports (960,000 rows take about 2 minutes today); indexes reviewed against real volumes; rate limits, backups, load and security tests.

## 8. The next phase with four developers

1. Ingestion and data quality (durable queue, Blob storage, PDF extraction with validation and human review). 2. Review and history (conflict workflows, notifications, reports). 3. Ranking (snapshot comparison, export, scoring rule versions). 4. Platform and front end (authentication, CI/CD, observability, accessibility).

Standards first: the OpenAPI contract is the source of truth and clients are generated; every rule starts as a pure, unit-tested function; every change to approved data writes its history in the same transaction; integration tests run on real PostgreSQL in CI; migrations are reviewed like code; no secrets in the repository.
