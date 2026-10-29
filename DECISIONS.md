# Decisions and trade-offs

## 1. The main choices

- **Entity Framework Core with PostgreSQL.** It gives migrations and simple queries. For the one heavy job (copying about a million ballots into a snapshot) I use Npgsql's bulk copy directly.
- **Three layers.** `Core` has the rules and no database (fast to test), `Infrastructure` talks to the database, `Api` handles HTTP.
- **Long jobs run in the background.** An upload or a snapshot returns `202` at once. A background worker takes jobs one at a time from a queue. The progress is saved in the database every half second, so refreshing the page does not lose it.
  The limit: the queue is in memory, so it works with one API instance only.
- **Angular, kept small.** Four screens. The API types are generated from the OpenAPI contract, so a change in the API breaks the build instead of breaking production.
- **Login is simple on purpose.** A header names one of three users. There is no password or token. The roles are checked in the API.

## 2. Importing files

- **I do not fix values silently.** `deu`, `Yes` or `21/02/2024` is rejected and reported with the line, the reason and the original value. A wrong guess in voting data could change a ranking.
- **Duplicate, contradiction, conflict.** A duplicate is the same row twice. A contradiction is a file that disagrees with itself (same country with two ballots, or two titles for one vote): I store nothing from those rows.
  A conflict is a new file that disagrees with data that is already approved.
- **Vote 025.** Its titles are inconsistent inside `q1`, so I treat it as a contradiction (there was no approved data to disagree with). In `rev2` the title is consistent, so it is imported as new.
  You can also read it as a conflict. I chose the reading that does not store a guess.
- **Membership** is checked on the date of the vote, and the last day of membership counts. A country with two periods (Iceland) is checked against both.
- **Uploading the same file again** creates nothing new. A conflict is identified by vote, kind, country and proposed value, and the database does not allow the same one twice.
- **What the report stores.** Rows that need an explanation (invalid, duplicate, contradiction, conflict) are stored with line, reason and original value. New rows can be traced through `country_votes` (file and line). Unchanged rows are only counted.
  The assignment says "the result of every row". I did not store every row, because the historic file has about 960,000 rows and it would double the writing without adding information. This is my main trade-off here.

## 3. Conflicts and history

- Deciding a conflict is one database statement: `UPDATE ... WHERE id = ? AND status = 'Open'`. The database locks the row, so if two analysts decide together, the second one changes zero rows and gets a `409` with the current state. A test sends two requests at the same time.
- Accepting a conflict updates the approved value in place and saves its new source (file, line, who, when). The old value stays in the conflict and on the vote's timeline.
- The timeline is one table (`vote_events`): type, user, old value, new value. The values are plain text (for example `Yes / weight 4`), which is simple but not typed.

## 4. Scoring

Score = 100 × (matching weight) ÷ (counted weight), for each country.

- **Only Yes and No count.** An abstention or an absence is not agreement and not disagreement, so it counts for nothing. It is still shown in the breakdown.
- **If nothing counted for a country, it has no score and no rank.** That is different from zero (votes counted, none matched).
- **A vote without a position is left out of the ranking and listed with the reason.**
- **Equal scores share a rank** (1, 2, 2, 4).
- **Choosing votes:** dates and session work together; listed vote ids are added to that choice; choosing nothing means all votes.
- **A saved snapshot does not change.** It keeps its own copy of every position and contribution it used. Database triggers also refuse any `UPDATE` or `DELETE` on snapshot data. The calculation is one transaction, so a snapshot is complete or does not exist.
- **Publishing:** one published snapshot per quarter, enforced by a unique index. To publish another one for the same quarter, the admin must unpublish the first.

## 5. The database

- **Rules live in the database too.** Foreign keys (so a country code must exist, because countries are a table), `CHECK` rules for every enum column, weight between 1 and 5, and unique indexes (one ballot per country per vote, the same conflict only once).
- **There is no `sessions` table.** The session is only a number in the files. It has no other data, and the year cannot be taken from it (the scanned sheets show session 61 in 2007 and 67 in 2013, while 70 to 79 are 2015 to 2024). A table with one column would check nothing. Column comments in the database explain it.
- **Migrations run when the API starts.** That is easy for `docker compose up`, but with several instances they could collide. In production they should be a separate step.

## 6. Azure

I did not use Azure. The core does not need an AI service. The database runs in Docker, uploads go to a Docker volume, and I did not do the PDF bonus, because the core (importing, conflicts, history, snapshots) came first.
In production I would use Azure Database for PostgreSQL, Blob Storage for uploads, Key Vault for secrets, and Application Insights for the logs, which already carry the correlation id.

## 7. Known limits, and what I would change before production

- The code that recovers imports after a server restart exists, but I did not test it by stopping the server in the middle of an import.
- Importing the historic file takes about 2 minutes. Bulk copy would make it faster (I did that for snapshots).
- For production: real login (for example Entra ID with tokens and roles), migrations as a release step, a durable queue with cancel, uploads to Blob Storage, rate limits, backups, and load and security tests.

## 8. The next phase with four developers

1. Importing and data quality (durable queue, Blob Storage, PDF reading with a human check).
2. Review and history (conflict workflow, notifications, reports).
3. Ranking (compare snapshots, export, versions of the scoring rules).
4. Platform and web app (login, CI/CD, monitoring, accessibility).

The standards I would set first: the OpenAPI contract is the source of truth and clients are generated; every rule starts as a small tested function; every change to approved data writes its history in the same transaction; integration tests run on a real PostgreSQL in CI; migrations are reviewed like code; no secrets in the repository.
