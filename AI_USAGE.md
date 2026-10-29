# AI usage

## Tool

I used **Claude Code** (Anthropic's coding assistant) as a development partner for the whole project. I owned the requirements, the plan and the decisions. Claude Code generated the implementation (code and tests) in parts, under my direction, and explained it to me. No AI service is part of the product itself.

## How I worked

1. **Requirements first.** Before any code, I went through the assignment (README and Appendix B, including the scanned sheets and the data files) and defined the work from its requirements. I used the AI to check that I had not missed anything: it read the documents and the data, and we matched every requirement (ING, CON, HIS, POS, SNP) to a planned part of the system.
2. **Part by part, with my own reasoning.** The system was built in parts: project setup, database, import rules, imports API, conflicts, positions, snapshots, web app, documents. For each part we first agreed what to build and why, and I questioned the design before any code was accepted (see the list below). Then the part was implemented, built and tested, and committed to Git on its own branch. The first parts were merged through GitHub pull requests, and later parts were merged locally because of the deadline.
3. **My own checks.** In the early parts I ran the builds, tests and checks on my own machine. Later I used the running system like a user: Docker, the web app as Dana, Omer and Lena, and the real data files. This found things the AI had not seen: the user switch looked as if it did nothing, the Review filter was unclear, I could not find where to set a position, and one screen differed from Appendix B. All of them were fixed. I also asked to see real table data (a demo copy of the database) to understand the model before accepting it, and I asked for an explanation of each part until I could explain it myself.
4. **Tests.** The AI wrote the tests, and they run on a real PostgreSQL. They reproduce the example of the assignment (section 7.2), and the real files give the results we expected from the analysis (for example 8 conflicts in `rev2`).

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
