# Meridian Vote Tracker

A system for the Northwind Policy Institute. It reads the voting CSV files of the Meridian Assembly, keeps approved data safe,
lets analysts decide on conflicts, lets the admin set a position and a weight for each vote, and calculates saved rankings (snapshots).

Built with ASP.NET Core (.NET 10), PostgreSQL 17, Angular 20 and Docker Compose. Data access is Entity Framework Core.

## 1. What you need

- **Git**
- **Docker** with Compose v2 (Docker Desktop on Windows or macOS). Check with `docker compose version`.
- An internet connection for the first build, and about 4 GB of free memory.
- Free ports **4200** (web) and **8080** (API). If they are taken, see "If something goes wrong" below.

You do **not** need .NET, Node.js or PostgreSQL on your computer. Everything is built and run inside Docker.

## 2. Download and run (step by step)

**Step 1. Download the project from Git.**

```bash
git clone https://github.com/eyaln11/meridian-vote-tracker.git
cd meridian-vote-tracker
```

**Step 2. Make sure Docker is running** (on Windows or macOS, open Docker Desktop and wait until it says it is running).

**Step 3. Build and start everything.**

```bash
docker compose up --build
```

The first run takes a few minutes, because Docker downloads images and builds the API and the web app.
Later runs start in about 20 seconds. The system is ready when `docker compose ps` (in a second terminal) shows three running containers
(`db` healthy, `api`, `web`), and the log of `api` says `Application started`.

**Step 4. Open the web application:** http://localhost:4200

On start, the API creates the database tables and loads the base data (the 150 countries from `member_states.csv` and the three users).
You do not need to load anything by hand.

**Stop and start again**

```bash
docker compose down          # stop (your data is kept)
docker compose up            # start again
docker compose down -v       # stop AND delete all data, to start from a clean system
```

You can also start it in the background with `docker compose up --build -d`.

## 3. If something goes wrong

| Problem | What to do |
|---|---|
| `Cannot connect to the Docker daemon` | Docker is not running. Start Docker Desktop and try again. |
| `port is already allocated` (4200 or 8080) | Use other ports. Bash: `WEB_PORT=4300 API_PORT=8090 docker compose up --build`. PowerShell: `$env:WEB_PORT=4300; $env:API_PORT=8090; docker compose up --build`. Then open http://localhost:4300. |
| The page shows an error just after start | The API is still starting (it runs the database migrations). Wait 20 to 30 seconds and refresh. |
| `git clone` or the build fails on Windows with "filename too long" | Clone into a short folder, for example `C:\src`. |
| You changed `POSTGRES_PASSWORD` after the first run | Run `docker compose down -v` first, because the old database keeps the old password. |

The database has no published port, so it cannot clash with a PostgreSQL that is already installed on your computer.

## 4. Addresses

| What | Address |
|---|---|
| The web application | http://localhost:4200 |
| The API | http://localhost:8080 |
| API contract (OpenAPI, JSON) | http://localhost:8080/openapi/v1.json |
| Health check | http://localhost:8080/health |
| Database viewer (optional) | `docker compose --profile tools up -d adminer`, then http://localhost:8081 (system PostgreSQL, server `db`, user `meridian`, password `meridian_local`, database `meridian`) |

## 5. Users

The assignment allows a simple login, so there is a menu at the top of the screen ("Acting as").
**This is not real security**: the web app sends a header like `X-User: dana`, and the API trusts it. There is no password and no token.
What is real is the roles: the API checks them on every request, so an analyst who calls an admin endpoint gets `403`.

| User | Role | Can do |
|---|---|---|
| Dana | Analyst | upload files, decide conflicts, create snapshots |
| Omer | Analyst | same as Dana |
| Lena | Admin | the same, plus set positions and weights, and publish snapshots |

**Why there is a user menu, and one user per tab.** In real use, each person would work at their own computer and sign in with their own account and role. To make the system easy to check, this version lets one person switch between the three users with the "Acting as" menu, and each browser tab keeps its own user. So you can open two tabs on one computer, one as Dana and one as Omer, and try the "two analysts at once" rule. The roles are still checked by the API.

## 6. A short walkthrough

The data files are in the `candidate_data` folder you received with the assignment.

1. **Imports:** upload `assembly_export_2024_q1.csv`. Watch the progress, then open the report (rejected rows with line, reason and original value).
2. Upload `assembly_export_2024_q1_rev2.csv`. It opens 8 conflicts. Upload it again: there are no new rows and no new conflicts.
3. **Review:** decide a conflict. To see the "two analysts at once" rule, open the same conflict in two tabs (Dana and Omer), decide in one, then decide in the other.
4. **Votes:** switch to Lena, press "Set position" on a vote, and save a stance and a weight. Try weight 7 to see the error next to the field. The timeline shows every change.
5. **Snapshots:** create one, open a country to see what each vote added to its score, then publish it as Lena.
6. Optional: upload `assembly_historic.csv` (about 960,000 rows, about 2 minutes) and create a snapshot over everything (about 30 seconds).

## 7. Tests

The tests need the **.NET 10 SDK** and a running Docker (the integration tests start their own PostgreSQL in a container).

```bash
dotnet test
```

- `tests/MeridianVoteTracker.Core.Tests`: unit tests for the rules (CSV parsing, row checks, classification, scoring). No database needed.
- `tests/MeridianVoteTracker.Api.IntegrationTests`: start the real API on a real PostgreSQL. They cover the example from the assignment, conflict decisions (including two at the same moment), roles, and snapshots.

## 8. Project layout

```
src/MeridianVoteTracker.Core            the rules, with no database: checks, classification, scoring
src/MeridianVoteTracker.Infrastructure  database model and migrations, the code that runs imports and snapshots
src/MeridianVoteTracker.Api             controllers, simple login, correlation id, background workers
web/                                    the Angular app (nginx serves it and forwards /api to the API)
tests/                                  unit and integration tests
scripts/demo-readme-example.sql         only for learning: the example from the assignment, written as table rows
```

## 9. Working on the web app without Docker (optional)

```bash
cd web
npm install
npm start                # http://localhost:4200, sends /api to http://localhost:8080
npm run api:generate     # rebuilds src/app/api/schema.d.ts from the running API
```

The API types in the web app are generated from the API contract. They are not written by hand.

## 10. Good to know

- **Secrets:** there are no keys or connection strings in the code. The API reads them from environment variables.
  `docker-compose.yml` has a default database password (`meridian_local`) for local use only. Set `POSTGRES_PASSWORD` to use another one.
- **Correlation id:** the web app sends an `X-Correlation-ID` with every request. The API puts it in every log line and every error, so one click can be followed in the logs (`docker compose logs api`).
- **Not done:** the bonus (reading PDF sheets with Azure AI), and no Azure resources are used. [DECISIONS.md](DECISIONS.md) explains why, and what I planned.
