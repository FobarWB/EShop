#!/bin/bash
# Creates a role and a database per bounded context (#78), and the role of the test databases.
# Runs once, when the data volume is empty.
set -euo pipefail

# The role owns its database (and its schema public, owned by pg_database_owner since PostgreSQL 15), so its migrations
# can create the service's schema. New databases are open to every role (CONNECT for PUBLIC): revoked, so only the
# owner connects and no service reads another's data.
create_database() {
    psql -v ON_ERROR_STOP=1 -v name="$1" -v password="$2" --username "$POSTGRES_USER" --dbname postgres <<'SQL'
CREATE ROLE :"name" LOGIN PASSWORD :'password';
CREATE DATABASE :"name" OWNER :"name" ENCODING 'UTF8' TEMPLATE template0;
REVOKE CONNECT ON DATABASE :"name" FROM PUBLIC;
SQL
}

create_database catalog "$CATALOG_DB_PASSWORD"
create_database cart "$CART_DB_PASSWORD"
create_database ordering "$ORDERING_DB_PASSWORD"
create_database customer "$CUSTOMER_DB_PASSWORD"
create_database notification "$NOTIFICATION_DB_PASSWORD"

# Integration tests pointed at this server (ConnectionStrings__EShopTests) create and drop their own eshop_test_* databases.
psql -v ON_ERROR_STOP=1 -v password="$ESHOP_TEST_DB_PASSWORD" --username "$POSTGRES_USER" --dbname postgres <<'SQL'
CREATE ROLE eshop_test LOGIN CREATEDB PASSWORD :'password';
SQL
