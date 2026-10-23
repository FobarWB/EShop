#!/bin/bash
# Creates the application role and database. Runs once, when the data volume is empty.
set -euo pipefail

psql -v ON_ERROR_STOP=1 -v eshop_password="$ESHOP_DB_PASSWORD" --username "$POSTGRES_USER" --dbname postgres <<'SQL'
CREATE ROLE eshop LOGIN CREATEDB PASSWORD :'eshop_password';
CREATE DATABASE eshop OWNER eshop ENCODING 'UTF8' TEMPLATE template0;
-- New databases are open to every role (CONNECT for PUBLIC); only the owner may connect to this one.
REVOKE CONNECT ON DATABASE eshop FROM PUBLIC;
SQL
