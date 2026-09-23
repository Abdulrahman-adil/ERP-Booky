# ERP Phase 4

This workspace contains the technical foundation, approved domain model, PostgreSQL persistence schema, JWT authentication, and the initial ERP application shell. Business workflows and feature modules are not implemented yet.

## Run with Docker

```bash
cd erp
cp .env.example .env
# Edit .env with a random JWT signing key and your development administrator credentials.
docker compose up --build
```

The frontend runs at `http://localhost:4200`, the API runs at `http://localhost:5080`, and Swagger is available at `http://localhost:5080/swagger`.

## Run locally

Start PostgreSQL with `docker compose up postgres` from `erp`. Supply the connection string through your shell or a local secrets mechanism, then run the API:

```bash
export ConnectionStrings__PostgreSQL='Host=localhost;Port=5432;Database=<database>;Username=<user>;Password=<password>'
export Authentication__Jwt__Issuer='erp-api'
export Authentication__Jwt__Audience='erp-web'
export Authentication__Jwt__SigningKey='<random-value-with-at-least-32-characters>'
export DevelopmentAdmin__Email='<your-development-admin-email>'
export DevelopmentAdmin__Password='<your-development-admin-password>'
dotnet run --project backend/src/Erp.Api
```

In another terminal, run the Angular app:

```bash
cd frontend
npm start
```

## Verify

```bash
dotnet test backend/Erp.sln
cd frontend && npm run build
npm test -- --watch=false --browsers=ChromeHeadless
```

## Database migrations

The EF Core CLI is version-pinned as a local tool. After setting `ConnectionStrings__PostgreSQL`, run:

```bash
cd backend
dotnet tool restore
dotnet tool run dotnet-ef database update --project src/Erp.Infrastructure --startup-project src/Erp.Infrastructure
```

Production deployments must provide `ConnectionStrings__PostgreSQL` and an explicit `Cors__AllowedOrigins__0` value through environment-specific configuration. Do not commit credentials to application settings or source control.

## Development administrator

The API provisions a development-only `Administrator` role and the listed baseline permissions on startup when both `DevelopmentAdmin__Email` and `DevelopmentAdmin__Password` are configured. It also creates a `DEV` organization and company if they are absent.

The supplied email and password are used only when the user has no credential record; restarting the API does not reset an existing password. This seeding mechanism never runs outside the Development environment.

Sign in at `http://localhost:4200/login` with the credentials configured in `.env` or your local environment. The session uses a short-lived JWT stored in browser session storage; closing the browser session or selecting **Log out** clears it.
