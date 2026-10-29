# Decisions and trade-offs

## 1. Assumptions I made

- The CSV files follow Appendix A: UTF-8, one row is one country vote, and the first line is the exact header. A different header fails the whole import before anything is saved.
- The country **code** is the identity. Names in the files are ignored, and `member_states.csv` is the master list.
- Membership dates are inclusive: the last day of membership still counts.
- "Approved data" is data that was imported without conflict, or that a person accepted. Only approved data is used in snapshots.
- A vote has one current position (stance and weight). Its history is kept on the timeline.
- The assignment does not say how a snapshot belongs to a quarter, so the admin types the quarter (for example `2024-Q1`) when publishing.

## 2. Main choices

- **Entity Framework Core with PostgreSQL.** Migrations and simple queries. For the one heavy job (about a million ballots into a snapshot) I use Npgsql bulk copy.
- **Three layers.** `Core` has the rules and no database (fast to test), `Infrastructure` talks to the database, `Api` handles HTTP.
- **Long jobs run in the background.** An upload or a snapshot returns `202` at once, and a worker takes jobs one at a time from a queue. Progress is saved in the database every half second, so refreshing the page loses nothing.
- **Angular, kept small.** Four screens, with API types generated from the OpenAPI contract, so an API change breaks the build instead of production.
- **The login is simple on purpose** (allowed by the assignment): a header names one of three users, with no password or token. The roles are checked in the API.

## 3. Importing and conflicts

- **No silent fixes (ING-13).** `deu`, `Yes` or `21/02/2024` is rejected and reported with line, reason and original value. A wrong guess could change a ranking.
- **Duplicate, contradiction, conflict.** A duplicate is the same row twice. A contradiction is a file that disagrees with itself (one country with two ballots, or two titles for one vote): nothing from those rows is stored. A conflict is a new file that disagrees with approved data.
- **Vote 025** has inconsistent titles inside `q1`, so I treat it as a contradiction (there was no approved data to disagree with). It can also be read as a conflict. I chose the reading that does not store a guess.
- **Uploading the same file again** creates nothing new. Unique indexes forbid the same conflict (vote, kind, country, proposed value) twice.
- **Deciding a conflict** is one statement: `UPDATE ... WHERE id = ? AND status = 'Open'`. The database locks the row, so if two analysts decide together, the second changes zero rows and gets `409` with the current state. A test does this with two simultaneous requests.
- **Accepting** updates the approved value in place with its new source (file, line, who, when). The old value stays in the conflict and on the vote timeline.
- **Report storage.** Rows that need an explanation (invalid, duplicate, contradiction, conflict) are stored with line, reason and original value. New rows can be traced through `country_votes`. Unchanged rows are only counted. The assignment says "the result of every row", but storing all rows of a 960,000-row file would double the writing without adding information. This is my main trade-off here.

## 4. Scoring

Score = 100 × matching weight ÷ counted weight, per country.

- **Only Yes and No count.** An abstention or an absence is neither agreement nor disagreement. It is still shown in the breakdown.
- **No counted vote means no score and no rank**, which is different from zero (votes counted, none matched).
- **A vote without a position is left out of the ranking and listed with the reason.**
- **Equal scores share a rank** (1, 2, 2, 4).
- **Choosing votes:** dates and session work together, listed vote ids are added, and choosing nothing means all votes.
- **A saved snapshot never changes.** It keeps its own copy of every position and contribution, and database triggers refuse any `UPDATE` or `DELETE` on snapshot data. The calculation is one transaction, so a snapshot is complete or does not exist.
- **Publishing:** one snapshot per quarter, enforced by a unique index. The admin unpublishes the old one before publishing another.

## 5. Database

- **Rules also live in the database:** foreign keys (countries are a table), `CHECK` rules for every enum column, weight between 1 and 5, and unique indexes (one ballot per country per vote).
- **No `sessions` table.** The session is only a number with no other data, and the year cannot be taken from it (the scanned sheets show session 61 in 2007 and 67 in 2013, while 70 to 79 are 2015 to 2024). Column comments explain it.
- **Migrations run when the API starts.** This is easy for `docker compose up`, but with several instances they could collide. In production they should be a separate release step.

## 6. What I did not finish, and what I planned

**The bonus: reading scanned PDF sheets.** Not done: I made the core correct first. My plan:
- Use **Azure AI Document Intelligence** to read each sheet's table, and a language model (Azure OpenAI or Claude in Azure AI Foundry) only for unclear cells.
- Treat the output as untrusted: check the structure, unknown country codes and countries that appear twice, and compare my totals with the totals printed on the sheet.
- Show the result on the Review screen with low-confidence cells marked. Nothing becomes approved without a person, and an approved extraction goes through the same import rules as a CSV.
- Show failures to the user, with time limits and retries, and pass the correlation id to every Azure call.
- Measure quality on sheets checked by hand: correct cells, cells that needed a correction, and totals that did not match.

**Azure for production.** Not used now (PostgreSQL runs in Docker, uploads go to a Docker volume). I would use Azure Database for PostgreSQL, Blob Storage for uploaded files, Key Vault with managed identity for secrets, Application Insights for the logs (they already carry the correlation id), Container Apps to host the API and the web app, and Service Bus for a durable job queue. Not Cosmos DB, because this data is relational.

**Other things not done:**
- The code that recovers imports after a server restart exists, but I did not test it by stopping the server in the middle of an import.
- No automated tests for the web app (only for the rules, the API and the database).
- Real login (for example Microsoft Entra ID) instead of the simple header.
- A faster historic import (it takes about 2 minutes; bulk copy would help), cancelling an import, and a durable queue (today it is in memory, so one API instance only).

## 7. The next phase with four developers

1. Importing and data quality: durable queue, Blob Storage, PDF reading with human review.
2. Review and history: conflict workflow, notifications, reports.
3. Ranking: compare snapshots, export, versions of the scoring rules.
4. Platform and web app: login, CI/CD, monitoring, accessibility.

Standards first: the OpenAPI contract is the source of truth and clients are generated; every rule starts as a small tested function; every change to approved data writes its history in the same transaction; integration tests run on a real PostgreSQL in CI; migrations are reviewed like code; no secrets in the repository.
