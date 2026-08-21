# Loan System

TASK-00 establishes the runnable modular-monolith baseline: ASP.NET Core 10 REST API, EF Core/SQL Server, React/TypeScript/Vite SPA, automated boundary and runtime tests, and Compose deployment.

## Prerequisites

- .NET SDK 10
- Node.js 20 and npm
- Docker Engine with Compose v2

## Full runtime

```bash
cp .env.example .env # replace the example development password
docker compose up --build -d
docker compose ps
```

Open the frontend at <http://localhost:5173>, API at <http://localhost:8080/api/v1/system/info>, OpenAPI at <http://localhost:8080/swagger>, liveness at <http://localhost:8080/health/live>, and SQL-backed readiness at <http://localhost:8080/health/ready>. Stop without deleting persistent data using `docker compose down`.

## Development

```bash
docker compose up -d sqlserver
dotnet watch --project src/LoanSystem.Api
npm ci --prefix frontend/loan-system-web
npm run dev --prefix frontend/loan-system-web
```

## Quality gates

```bash
dotnet restore
dotnet build --no-restore
dotnet test --no-build --collect:"XPlat Code Coverage" --results-directory TestResults
python3 scripts/check_backend_coverage.py "TestResults/**/coverage.cobertura.xml"
dotnet format --verify-no-changes
npm ci --prefix frontend/loan-system-web
npm run typecheck --prefix frontend/loan-system-web
npm run lint --prefix frontend/loan-system-web
npm run test:coverage --prefix frontend/loan-system-web
npm run build --prefix frontend/loan-system-web
# With the runtime running:
npm run e2e --prefix frontend/loan-system-web
```

Integration tests start disposable Microsoft SQL Server through Testcontainers whenever Docker is available; GitHub CI always executes this real-database path. The `LoanSystem.ArchitectureTests` project enforces module implementation and Clean Architecture boundaries.

## Required main branch ruleset

Repository administrators must configure a ruleset targeting `main`: require a pull request before merging (one approval), block force pushes/deletions, require branches to be up to date, and require the `backend`, `frontend`, and `docker-smoke` status checks. Do not allow bypass except emergency administrators.
