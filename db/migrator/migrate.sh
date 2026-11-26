#!/bin/sh
# Applies the pending migrations of every service, one database at a time (#78). Each bundle connects as the owner of
# its database and keeps its history in <schema>.__EFMigrationsHistory, so a bundle with nothing to apply only checks it.
set -eu

migrate() {
    echo "Migrating database $1"
    "./$1" --connection "$2"
}

migrate catalog "${ConnectionStrings__Catalog:?ConnectionStrings__Catalog is not set}"
migrate cart "${ConnectionStrings__Cart:?ConnectionStrings__Cart is not set}"
migrate ordering "${ConnectionStrings__Ordering:?ConnectionStrings__Ordering is not set}"
migrate customer "${ConnectionStrings__Customer:?ConnectionStrings__Customer is not set}"
migrate notification "${ConnectionStrings__Notification:?ConnectionStrings__Notification is not set}"
