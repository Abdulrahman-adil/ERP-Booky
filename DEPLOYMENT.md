# Production deployment

This runbook is for a **new, isolated production deployment**, not an instruction to reset the existing development database. Never run `down -v`, `EnsureCreated`, development reset, or old migrations blindly against an existing installation. The application does not migrate at startup.

## 1. Prerequisites and release

Use a maintained 64-bit Linux host, Docker Engine and the Docker Compose v2 plugin installed from Docker's official repository. Start with 2 CPU cores, 4 GB RAM and monitored SSD storage; size from load testing. Angular builds need Node 22 if built outside Docker. The backend targets .NET 8. Reserve TCP 80/443 for Nginx; firewall all other public inbound ports except restricted SSH. Docker publishes ports independently of some host firewall frontends: verify the cloud firewall too.

Deploy a reviewed, versioned repository checkout to `/opt/rabt-erp`. Do not copy `.env`, overrides, local user-secrets or development volumes. Record the commit/release and image digests. Container tags are currently maintained major/stable tags: resolve and retain tested digests for a repeatable release and rollback.

```sh
cd /opt/rabt-erp
cp .env.production.example .env.production
chmod 600 .env.production
sudo install -d -m 700 secrets backups
```

Edit `.env.production` with the real hostname, database names, secret **file paths**, currency, issuer and audience. All example `REPLACE_...` values must be replaced. Production is a standalone Compose file; always use `-f docker-compose.prod.yml`. Never combine it with the development Compose or its ignored override.

## 2. Secrets and configuration

Required configuration keys:

| Key | Purpose |
| --- | --- |
| `ERP_DOMAIN` | One real DNS hostname, without scheme or path; controls HTTPS CORS, host filtering and Nginx |
| `TLS_CERT_PATH`, `TLS_KEY_PATH` | Absolute full-chain certificate/private-key paths |
| `POSTGRES_DB`, `POSTGRES_OWNER`, `POSTGRES_APP_USER` | Database, migration owner and separate non-superuser runtime role |
| `POSTGRES_OWNER_PASSWORD_FILE`, `POSTGRES_APP_PASSWORD_FILE` | Separate random PostgreSQL password files |
| `API_CONNECTION_STRING_FILE`, `MIGRATION_CONNECTION_STRING_FILE` | Npgsql connection-string files for the respective roles |
| `JWT_SIGNING_KEY_FILE` | Independently generated signing secret, at least 64 characters |
| `JWT_ISSUER`, `JWT_AUDIENCE` | Stable issuer/audience, not development defaults |
| `GOOGLE_AUTHENTICATION_ENABLED`, `GOOGLE_CLIENT_ID` | Disabled/empty until configured; Client ID is public, not a secret |
| `EXTERNAL_WORKSPACE_BASE_CURRENCY` | Approved ISO currency for newly provisioned workspaces |

Generate separate secrets, for example `openssl rand -hex 48` for each password and `openssl rand -hex 64` for JWT. Redirect outputs directly into protected files; never paste into tracked files or shell command arguments. Connection-string file templates (substitute the corresponding secret locally):

```text
Host=postgres;Port=5432;Database=<database>;Username=<runtime-role>;Password=<runtime-password>;Include Error Detail=false
Host=postgres;Port=5432;Database=<database>;Username=<migration-owner>;Password=<owner-password>;Include Error Detail=false
```

Compose secrets here are local read-only mounts, not an encrypted secret manager. On Linux set the API connection and JWT files to root group **1654**, mode **0440** (the .NET `app` user). Set the PostgreSQL application password file to root group **70**, mode **0440** (postgres:16-alpine's postgres user). Set owner connection/password and TLS private key to root-only **0400**. Confirm image user IDs with `docker run --rm --entrypoint id IMAGE USER` when pinning images. Keep the directory root-only. For a remote/managed PostgreSQL server additionally require and verify TLS; the provided compose database is isolated on the same Docker host.

No development administrator is seeded in Production. Migrate a reviewed existing identity database, or use the approved Google new-workspace flow. There is no production bootstrap password. Do not temporarily run a production database in Development to create an administrator.

## 3. DNS, HTTPS and proxy

Create A/AAAA DNS records for the host. Obtain a publicly trusted certificate covering `ERP_DOMAIN`, for example with an ACME DNS-01 client, and provide full-chain/key files before starting Nginx. Schedule certificate renewal with your ACME client; after successful renewal run `docker compose --env-file .env.production -f docker-compose.prod.yml exec proxy nginx -s reload`.

The proxy alone exposes 80 and 443. Port 80 redirects to HTTPS. Angular supports deep-link refresh through its internal static Nginx. `/api/` is proxied unchanged to the internal API. PostgreSQL and API have **no published host ports**. The API runs as non-root and has an outbound network for Google signature-key retrieval.

Forwarded headers run before redirects, authentication and IP rate limiting. Only the known proxy `172.30.89.10` (plus framework loopback defaults) is trusted, with a one-hop limit. Nginx overwrites forwarded headers rather than trusting browser-supplied ones. If the subnet conflicts on your host, change the compose subnet, proxy IP and `ReverseProxy__KnownProxies__0` together. Never clear trusted proxy/network lists to trust everyone.

Optional Cloudflare: use **Full (strict)** TLS to the origin, not Flexible. Keep origin TLS validation and restrict origin ingress to Cloudflare if appropriate. By default the rate limiter will see the Cloudflare edge IP; before enabling Cloudflare proxying, configure Nginx `real_ip_header CF-Connecting-IP` and `set_real_ip_from` for **only current official Cloudflare CIDRs**, test spoofed headers, and maintain that allowlist. Never trust that header from arbitrary clients. This provider-specific configuration is deliberately not pre-enabled.

HSTS is production-only, without preload/includeSubDomains assumptions. CSP allows Angular's generated inline styles but **not arbitrary inline scripts/eval**. Google GIS script/frame/connect/style origins are allowed. Other external images/fonts/scripts require deliberate policy changes. HTML is not cached; API responses use `no-store`. Confirm browser CSP and Google popup behavior on the final HTTPS domain.

## 4. Build and controlled migrations

Define a convenience function in the shell:

```sh
cd /opt/rabt-erp
dc() { docker compose --env-file .env.production -f docker-compose.prod.yml "$@"; }
dc config --quiet
dc --profile tools build --pull api frontend migrate
dc up -d postgres
dc ps
```

On a **brand-new empty volume only**, PostgreSQL initializes its configured database and a least-privilege runtime role using `deploy/init-runtime-role.sh`. That script is not a general migration and does not run again for existing volumes. For an imported database, review role ownership/default privileges and grant the runtime role schema usage, table CRUD and sequence usage without superuser/schema-creation rights.

### Existing databases: stop and reconcile first

Runtime and design-time EF now both use `erp."__EFMigrationsHistory"`. Older project versions used the provider's default `public` history while manual reconciliations populated `erp`. A populated schema and missing/incomplete history must **not** trigger rerunning old migrations.

1. Back up the complete database and attachment volume as in `BACKUP_RESTORE.md`.
2. Inspect both history locations and schema without making changes:

```sh
dc exec -T postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "SELECT schemaname, tablename FROM pg_tables WHERE tablename = '\''__EFMigrationsHistory'\'';"'
dc exec -T postgres sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" --schema-only' > backups/schema-before.sql
dc run --rm migrate migrations list --project src/Erp.Infrastructure --startup-project src/Erp.Api --configuration Release --no-build
```

3. Compare every repository migration's tables, columns, keys, indexes, constraints and data/sequence changes against the live schema and release history. Export each existing history table separately. Do not copy all history rows or mark migrations applied merely because their tables exist.
4. If canonical history is incomplete, obtain database-owner approval for a reviewed, transaction-protected baseline containing only independently verified migration IDs/ProductVersions. No generic baseline SQL is supplied because it could corrupt a different installation. Leave the old history table intact for evidence.
5. Generate/review the SQL for the specific last-applied to target range and test it on a restored copy first:

```sh
dc run --rm migrate migrations script LAST_VERIFIED_MIGRATION TARGET_MIGRATION --project src/Erp.Infrastructure --startup-project src/Erp.Api --configuration Release --no-build > backups/reviewed-migration.sql
dc run --rm migrate database update TARGET_MIGRATION --project src/Erp.Infrastructure --startup-project src/Erp.Api --configuration Release --no-build
```

Replace the two migration identifiers with actual reviewed IDs. On a confirmed empty database `LAST_VERIFIED_MIGRATION` is `0`. To migrate to the checked-out release head on that empty database use `dc run --rm migrate`. Only the isolated migration container receives the owner connection. Never run two deployment migrations concurrently.

## 5. Start and verify

```sh
dc up -d api frontend proxy
dc ps
dc exec api curl --fail http://localhost:8080/health/ready
dc exec proxy nginx -t
curl --fail --head "https://YOUR_REAL_DOMAIN/login"
curl --fail "https://YOUR_REAL_DOMAIN/api/v1/system/ping"
curl --head "https://YOUR_REAL_DOMAIN/api/auth/me"
```

Replace `YOUR_REAL_DOMAIN`. The final request must be **401** without credentials. `/health` is process liveness; `/health/ready` tests DB connectivity but does not certify schema/correct financial balances. Do a scoped login, refresh, logout, deep-link refresh, report/export, company isolation and attachment rehearsal in a separate staging database. Never use posted production journals as test fixtures.

## 6. Google Sign-In

Use a **Web application** OAuth client. In Google Cloud configure Authorized JavaScript origins to `https://YOUR_REAL_DOMAIN`, without paths. This implementation uses GIS JavaScript callback/ID token exchange; it has no OAuth redirect callback URL to register. Configure consent branding, publishing status and any test-user restrictions. Use the same public Client ID in `GOOGLE_CLIENT_ID`; set `GOOGLE_AUTHENTICATION_ENABLED=true`; rebuild the frontend and restart API via `dc up -d --build api frontend`.

Backend verifies signature, issuer, audience, expiry and verified email using Google.Apis.Auth. Provider+subject is authoritative. Matching an old account email does not auto-link. New identities get an independent workspace; returning identities reuse it. There is **no Client Secret dependency**. A genuine Google login on the final domain is still a deployment acceptance step; fake-verifier tests do not prove Google Cloud configuration.

## 7. Authentication and operational limits

Password hashes use ASP.NET's PBKDF2 PasswordHasher; never plaintext. JWT lifetime is 30 minutes in production, no refresh tokens. Password changes, inactive users and removed roles/company access are checked server-side on every authenticated request. Signed privileges can only be reduced, not silently expanded; new grants require a new login. Company administrators cannot change global credentials/profile/status of users shared with another company; scoped roles can still be updated.

Logout clears browser sessionStorage; there is no per-token server revocation list. A copied JWT can remain usable until expiry unless account/password/access is revoked. sessionStorage remains XSS-sensitive, which is why framework security updates and CSP are deployment gates. Login/password and Google endpoints share **10 attempts/minute/IP** with 429/Retry-After; normal ERP operations are not put under that limiter. Limits are per API process: a multi-replica deployment needs a coordinated gateway policy.

Uploads are bounded at 12 MiB request/10 MiB file and downloaded as attachments. No malware scanner is configured. Treat untrusted files as a deployment risk and add scanning/quarantine before enabling uploads for untrusted tenants. Google signup currently provisions a workspace for any verified new Google identity; decide whether that open-registration product policy is appropriate before publishing publicly.

## 8. Upgrade and rollback

Back up DB plus attachments before migrations. Record images/digests and migration history. Build and test the next release against a restored staging copy. Stop writes during the backup/migration window, run only approved new migrations, deploy immutable release images, then verify health and business acceptance. Monitor 401/403/429/5xx, DB capacity, file storage, backup completion and certificate expiry; logging must never include tokens or secret values.

Roll back containers to retained previous images only if their code is compatible with the migrated schema. Do not blindly run EF `database update` to an earlier migration. For incompatible changes restore the verified backup to a **new database/volume** and corresponding attachment snapshot, verify in isolation, then approve cutover of secret connection files. Preserve the failed database for investigation. Restoring loses changes after the backup; get explicit approval for the recovery point.

No production domain, certificates, scheduled backups, monitoring or hosting have been provisioned by adding these files. Complete the production acceptance checklist in `docs/PRODUCTION_READINESS.md` before public deployment.
