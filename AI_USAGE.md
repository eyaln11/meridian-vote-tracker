# AI usage

## Tool

I used **Claude Code** (Anthropic's coding assistant). It read the assignment and the data files, proposed the design, wrote most of the code and tests, ran them, and explained each part to me. No AI service is part of the product itself.

## How I worked

1. **Requirements first.** Before any code, I went through the assignment (README and Appendix B, including the scanned sheets and the data files) and defined the work from its requirements. I used the AI to check that I had not missed anything: it read the documents and the data, and we matched every requirement (ING, CON, HIS, POS, SNP) to a planned part of the system.
2. **Development in parts.** I built the system part by part: project setup, database, import rules, imports API, conflicts, positions, snapshots, web app, documents. For each part, we first agreed what would be built and why. I asked questions and challenged the design before moving on. Then the part was implemented, run and tested, and committed to Git on its own branch. The first parts were merged through GitHub pull requests, and later parts were merged locally because of the deadline.
3. **Review of results.** As the deadline came closer, the AI wrote code directly into the repository. I reviewed the result by running the system (Docker, the web app, the real data files), by reading the tests, and by asking for an explanation of each part until I could explain it myself.

## Where I questioned or corrected what the AI produced

- **Wrong diagnosis.** For an empty `X-Correlation-ID` response header it first blamed a stale server. The real bug was that the middleware wrote the id to the *request* headers. Fixed, and later changed to `OnStarting`, because the exception handler clears response headers.
- **Event type as free text.** I questioned `vote_events.type` being a plain string. The review concluded that a lookup table would create two sources of truth, but that the database really was unprotected. It now has `CHECK` constraints generated from the enums, on every enum column.
- **Country code was not a foreign key.** The review of the other tables found that `country_votes.country_code` accepted any text. A `countries` table with foreign keys was added, with a migration that keeps existing data.
- **Sessions.** I suggested a `sessions` table. Checking the files and the scanned sheets showed that the session number carries no year or place, so the table was not added, and the earlier claim "79 = 2024" was corrected.
- **Scoring edge case.** A first SQL prototype returned no score for a country with counted votes but no agreement (`SUM` of nothing is `NULL`). Fixed, and now covered by a unit test.
- **Mistakes found by tests.** The integration tests caught a conflict query that could not be translated to SQL and a test that depended on execution order. Both were fixed.
- **Tooling error.** A scripted edit once duplicated lines in a configuration file and produced an empty migration. It was noticed because the database comment did not change, then removed and regenerated.
- **Docker start-up risk.** While preparing the instructions for reviewers, we saw that publishing the database port (5432) could clash with a PostgreSQL already installed on their computer. The port was removed, because only the API needs the database.
- **Gaps against Appendix B.** Re-reading the appendix showed small missing details in the screens (votes without a position clearly marked, ballots changed by a decision marked, and the current state shown after a lost conflict race). They were added.
- **Open rules were decided on purpose:** abstentions and absences do not count, ties share a rank, nothing is changed silently. Vote 025 (inconsistent titles) is a contradiction on the AI's recommendation. I questioned it, since it can also be read as a conflict, and DECISIONS.md records both readings.

## Verification

Unit tests (rules), integration tests on a real PostgreSQL (the assignment example, two analysts at once, roles, snapshots), the real CSV files (including the 960,000-row historic file), and a test of the full start-up from a fresh clone with `docker compose up`.
