#!/bin/sh
set -eu
runtime_password="$(cat /run/secrets/postgres_app_password)"
psql --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" --set ON_ERROR_STOP=1 \
  --set runtime_user="$POSTGRES_APP_USER" --set runtime_password="$runtime_password" <<'SQL'
CREATE ROLE :"runtime_user" LOGIN PASSWORD :'runtime_password' NOSUPERUSER NOCREATEDB NOCREATEROLE;
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
CREATE SCHEMA IF NOT EXISTS erp;
GRANT USAGE ON SCHEMA erp TO :"runtime_user";
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA erp TO :"runtime_user";
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA erp TO :"runtime_user";
ALTER DEFAULT PRIVILEGES IN SCHEMA erp GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO :"runtime_user";
ALTER DEFAULT PRIVILEGES IN SCHEMA erp GRANT USAGE, SELECT ON SEQUENCES TO :"runtime_user";
SQL
