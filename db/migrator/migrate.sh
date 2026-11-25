#!/bin/sh
# Applies the pending migrations of every service, one schema at a time (#77). Each bundle keeps its history in
# <schema>.__EFMigrationsHistory, so a bundle that has nothing to apply only checks it.
set -eu

: "${ConnectionStrings__EShop:?ConnectionStrings__EShop is not set}"

for bundle in catalog cart ordering customer notification; do
    echo "Migrating schema $bundle"
    "./$bundle" --connection "$ConnectionStrings__EShop"
done
