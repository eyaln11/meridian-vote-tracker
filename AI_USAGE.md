# AI usage

## Tools

**Claude Code** (Anthropic's coding assistant, running in its desktop app) was the main tool. It read the assignment (README and Appendix B, including the scanned voting sheets) and the data files, proposed the design, wrote most of the code and tests, ran them, and explained each part to me. I made the decisions, asked for changes, and had to be able to defend every part, so I asked for explanations of each class and rule and questioned the design.

No AI service is used by the product itself.

## How I worked with it

- It analysed the real CSV files first: the problem rows in `q1` (invalid codes, wrong date formats, a column shift, a title that is inconsistent between rows, a row of a country that had already left) and the 8 differences in `rev2` were found by running the engine on the files, then locked in with tests (the README example, 7.2, is reproduced end to end).
- Code was written in small steps and verified by building, running the unit tests, running the integration tests on a real PostgreSQL, and exercising the Docker stack by hand (upload of all three files, a snapshot over the historic data, the screens in a browser).

## Where I rejected or corrected what the AI produced

- **Wrong diagnosis.** For an empty `X-Correlation-ID` response header it first blamed a stale server; the real bug was that the middleware wrote the id to the *request* headers. Fixed, and later changed to `OnStarting` because the exception handler clears response headers.
- **Event type as free text.** I questioned `vote_events.type` being a plain string. The review concluded that a lookup table would create two sources of truth, but that the database really was unprotected; it now has `CHECK` constraints generated from the enums, on every enum column.
- **Country code was not a foreign key.** My review of the other tables found `country_votes.country_code` accepted any text. A `countries` table with foreign keys was added, with a data-preserving migration.
- **Sessions.** I suggested a `sessions` table. Checking the files and the scanned sheets showed the session number carries no year or place and has no attributes, so the table was not added; the decision and the reason are documented, and the earlier claim "79 = 2024" was corrected to say the year cannot be derived.
- **Scoring edge case.** A first SQL prototype returned no score for a country with counted votes but no agreement (`SUM` of nothing is `NULL`). Corrected, and now covered by a unit test (zero is different from no score).
- **Integration tests caught two mistakes of the AI:** a conflict list query that could not be translated to SQL, and a test that depended on execution order. Both were fixed.
- **Tooling errors.** A scripted edit once duplicated lines in a configuration file and produced an empty migration; it was noticed because the database comment did not change, then removed and regenerated properly.
- **Open rules were decided deliberately, not left to the tool:** abstentions and absences do not count, ties share a rank, nothing is normalised silently. Vote 025 (inconsistent titles) is treated as a contradiction on the AI's recommendation; I questioned it (it can also be read as a conflict), and DECISIONS.md records both readings.

## What I asked for and decided

- I asked for step-by-step explanations in plain language of every class and rule, so that I can defend them, and for example data in the real tables (a demo copy of the database with the README example) instead of descriptions.
- I challenged the design where it looked wrong (event types as text, the missing country table, a possible sessions table, how a position's weight reaches the scores) and took the outcome of each check, including the cases where the check showed my suggestion was not needed.
- I decided the scope: the core first, the Azure PDF bonus not attempted.
