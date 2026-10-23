#!/bin/bash
# Creates the Keycloak role and database. Runs once, when the data volume is empty;
# on an existing volume run it by hand (see AGENTS.md).
set -euo pipefail

psql -v ON_ERROR_STOP=1 -v keycloak_password="$KEYCLOAK_DB_PASSWORD" --username "${POSTGRES_USER:-postgres}" --dbname postgres <<'SQL'
CREATE ROLE keycloak LOGIN PASSWORD :'keycloak_password';
CREATE DATABASE keycloak OWNER keycloak ENCODING 'UTF8' TEMPLATE template0;
-- New databases are open to every role (CONNECT for PUBLIC); only the owner may connect to this one.
REVOKE CONNECT ON DATABASE keycloak FROM PUBLIC;
SQL
