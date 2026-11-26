<#
.SYNOPSIS
Converts the Northwind data in archive/Northwind4SQLServer.sql into PostgreSQL data-only scripts, one per schema.

.DESCRIPTION
The schemas are created by the EF Core migrations of the services; this script produces only the rows (#77):
db/northwind-catalog.sql (schema catalog) and db/northwind-ordering.sql (schema ordering).
It reads the INSERT statements of the SQL Server dump and rewrites them for PostgreSQL:
schema-qualified snake_case names, bytea and ISO date literals, booleans, parent tables before child tables,
the columns the dump does not have, and identity sequences moved past the inserted ids.
No foreign key crosses the schemas, so the files load independently, each into its empty schema.

.EXAMPLE
powershell -File db/convert-northwind.ps1
#>
param(
    [string]$Source = (Join-Path $PSScriptRoot '..\archive\Northwind4SQLServer.sql'),
    [string]$OutputDirectory = $PSScriptRoot
)

$ErrorActionPreference = 'Stop'

# Load order (parents first). Schema: the service's schema; one output file each.
# Columns: used when the dump INSERT has no column list.
# Identity: generated key whose sequence must be reset after explicit ids are inserted.
$tables = [ordered]@{
    'Categories'          = @{ Schema = 'catalog'; Name = 'categories'; Identity = 'category_id' }
    'Suppliers'           = @{ Schema = 'catalog'; Name = 'suppliers'; Identity = 'supplier_id' }
    'Products'            = @{ Schema = 'catalog'; Name = 'products'; Identity = 'product_id' }
    'Shippers'            = @{ Schema = 'ordering'; Name = 'shippers'; Identity = 'shipper_id' }
    'Customers'           = @{
        Schema  = 'ordering'
        Name    = 'customers'
        Columns = 'CustomerId', 'CompanyName', 'ContactName', 'ContactTitle', 'Address', 'City',
                  'Region', 'PostalCode', 'Country', 'Phone', 'Fax'
    }
    'Employees'           = @{ Schema = 'ordering'; Name = 'employees'; Identity = 'employee_id' }
    'Region'              = @{ Schema = 'ordering'; Name = 'regions'; Columns = 'RegionId', 'RegionDescription' }
    'Territories'         = @{ Schema = 'ordering'; Name = 'territories'; Columns = 'TerritoryId', 'TerritoryDescription', 'RegionId' }
    'EmployeeTerritories' = @{ Schema = 'ordering'; Name = 'employee_territories'; Columns = 'EmployeeId', 'TerritoryId' }
    'Orders'              = @{ Schema = 'ordering'; Name = 'orders'; Identity = 'order_id' }
    'Order Details'       = @{ Schema = 'ordering'; Name = 'order_details'; Columns = 'OrderId', 'ProductId', 'UnitPrice', 'Quantity', 'Discount' }
}

$dateColumns = 'order_date', 'required_date', 'shipped_date', 'birth_date', 'hire_date'
$booleanColumns = 'discontinued'

# INSERT [INTO] "Table" | Table [(columns)] VALUES (values); string literals may contain parentheses and newlines.
$statementPattern = '(?is)\bINSERT\s+(?:INTO\s+)?(?:"(?<table>[^"]+)"|(?<table>\w+))\s*(?:\((?<columns>[^)]*)\))?\s*VALUES\s*\((?<values>(?:N?''(?:[^'']|'''')*''|[^''()])*)\)'
$valuePattern = 'N?''(?:[^'']|'''')*''|0x[0-9A-Fa-f]*|NULL|-?\d+(?:\.\d+)?(?:[eE][-+]?\d+)?'

function ConvertTo-SnakeCase([string]$name) {
    # CategoryId -> category_id, CustomerTypeID -> customer_type_id
    ($name.Trim().Trim('"') -creplace '(?<=[a-z0-9])(?=[A-Z])', '_').ToLowerInvariant()
}

function ConvertTo-PostgresValue([string]$value, [string]$column) {
    if ($value -eq 'NULL') { return $value }
    if ($value.StartsWith("N'")) { $value = $value.Substring(1) }  # N'...' is a SQL Server Unicode literal

    if ($value.StartsWith('0x')) { return "'\x" + $value.Substring(2) + "'" }  # bytea hex format

    if ($dateColumns -contains $column) {
        # The dump uses US dates (M/d/yyyy); ISO dates do not depend on the server DateStyle.
        $text = $value.Trim("'")
        $date = [datetime]::ParseExact($text, [string[]]@('M/d/yyyy', 'MM/dd/yyyy'),
            [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::None)
        return "'" + $date.ToString('yyyy-MM-dd') + "'"
    }

    if ($booleanColumns -contains $column) {
        switch ($value) {
            '0' { return 'false' }
            '1' { return 'true' }
            default { throw "Unexpected boolean value $value in column $column" }
        }
    }

    $value
}

$dump = [IO.File]::ReadAllText((Resolve-Path $Source), [Text.Encoding]::Unicode)

$rows = @{}
foreach ($key in $tables.Keys) { $rows[$key] = New-Object System.Collections.Generic.List[object] }
$columnsByTable = @{}

foreach ($match in [regex]::Matches($dump, $statementPattern)) {
    $table = $match.Groups['table'].Value
    if (-not $tables.Contains($table)) { throw "Unexpected table in dump: $table" }

    $columns = if ($match.Groups['columns'].Success) {
        $match.Groups['columns'].Value -split ','
    } else {
        $tables[$table].Columns
    }
    $columns = @($columns | ForEach-Object { ConvertTo-SnakeCase $_ })

    $values = @([regex]::Matches($match.Groups['values'].Value, $valuePattern) | ForEach-Object { $_.Value })
    if ($values.Count -ne $columns.Count) {
        throw "Column/value count mismatch in $table`: $($match.Value.Substring(0, 80))"
    }

    $converted = for ($i = 0; $i -lt $columns.Count; $i++) { ConvertTo-PostgresValue $values[$i] $columns[$i] }

    $columnsByTable[$table] = $columns
    $rows[$table].Add(@($converted))
}

# employees.reports_to references employees: insert every manager before their reports.
$employeeColumns = $columnsByTable['Employees']
$idIndex = [array]::IndexOf($employeeColumns, 'employee_id')
$managerIndex = [array]::IndexOf($employeeColumns, 'reports_to')
$employees = $rows['Employees']
$ordered = New-Object System.Collections.Generic.List[object]
$inserted = @{}
while ($ordered.Count -lt $employees.Count) {
    $added = 0
    foreach ($row in $employees) {
        $id = $row[$idIndex]
        $manager = $row[$managerIndex]
        if (-not $inserted.ContainsKey($id) -and ($manager -eq 'NULL' -or $inserted.ContainsKey($manager))) {
            $ordered.Add($row)
            $inserted[$id] = $true
            $added++
        }
    }
    if ($added -eq 0) { throw 'Cyclic reports_to references in Employees' }
}
$rows['Employees'] = $ordered

# Columns the model requires and the dump does not have. Their values are those the shared database's migrations
# gave the loaded rows before #77 (AddProductRevision, AddOrderLifecycle, AddGuestOrders): no product change has been
# published yet, the Northwind orders are finished orders placed by Northwind's customers, and each order line keeps
# the product name it was sold under, here the current one.
$columnsByTable['Products'] += 'revision'
$rows['Products'] = @($rows['Products'] | ForEach-Object { , ($_ + @('0')) })

$columnsByTable['Orders'] += 'status', 'placed_as_guest'
$rows['Orders'] = @($rows['Orders'] | ForEach-Object { , ($_ + @("'Delivered'", 'false')) })

$productColumns = $columnsByTable['Products']
$productIdIndex = [array]::IndexOf($productColumns, 'product_id')
$productNameIndex = [array]::IndexOf($productColumns, 'product_name')
$productNames = @{}
foreach ($row in $rows['Products']) { $productNames[$row[$productIdIndex]] = $row[$productNameIndex] }

$detailProductIndex = [array]::IndexOf($columnsByTable['Order Details'], 'product_id')
$columnsByTable['Order Details'] += 'product_name'
$rows['Order Details'] = @($rows['Order Details'] | ForEach-Object {
    $name = $productNames[$_[$detailProductIndex]]
    if (-not $name) { throw "Order Details references the unknown product $($_[$detailProductIndex])" }
    , ($_ + @($name))
})

foreach ($schema in @($tables.Values | ForEach-Object { $_.Schema } | Select-Object -Unique)) {
    $file = "northwind-$schema.sql"
    $sql = New-Object System.Text.StringBuilder
    [void]$sql.AppendLine("-- Northwind sample data of the schema $schema.")
    [void]$sql.AppendLine('-- Generated by db/convert-northwind.ps1 from archive/Northwind4SQLServer.sql; do not edit by hand.')
    [void]$sql.AppendLine('-- Load into the empty schema after the migrations (the migrator), e.g. with the container''s psql:')
    [void]$sql.AppendLine("--   docker compose cp db/$file postgres:/tmp/$file")
    [void]$sql.AppendLine("--   docker compose exec postgres psql -U $schema -d $schema -v ON_ERROR_STOP=1 -f /tmp/$file")
    [void]$sql.AppendLine()
    [void]$sql.AppendLine('\set ON_ERROR_STOP on')
    [void]$sql.AppendLine("SET client_encoding = 'UTF8';")
    [void]$sql.AppendLine()
    [void]$sql.AppendLine('BEGIN;')

    $keys = @($tables.Keys | Where-Object { $tables[$_].Schema -eq $schema })
    foreach ($key in $keys) {
        $table = $tables[$key]
        if ($rows[$key].Count -eq 0) { throw "No rows found for $key" }

        [void]$sql.AppendLine()
        [void]$sql.AppendLine("INSERT INTO $schema.$($table.Name) ($($columnsByTable[$key] -join ', ')) VALUES")
        $lines = $rows[$key] | ForEach-Object { '    (' + ($_ -join ', ') + ')' }
        [void]$sql.Append($lines -join ",`n")
        [void]$sql.AppendLine(';')
        Write-Host ('{0,-31} {1,5} rows' -f "$schema.$($table.Name)", $rows[$key].Count)
    }

    [void]$sql.AppendLine()
    [void]$sql.AppendLine('-- Explicit ids do not advance identity sequences; move each sequence past the loaded rows.')
    foreach ($key in $keys) {
        $table = $tables[$key]
        if ($table.Identity) {
            $name = "$schema.$($table.Name)"
            [void]$sql.AppendLine("SELECT setval(pg_get_serial_sequence('$name', '$($table.Identity)'), max($($table.Identity))) FROM $name;")
        }
    }

    [void]$sql.AppendLine()
    [void]$sql.AppendLine('COMMIT;')

    $output = Join-Path $OutputDirectory $file
    [IO.File]::WriteAllText($output, $sql.ToString().Replace("`r`n", "`n"), (New-Object Text.UTF8Encoding $false))
    Write-Host "Written $output"
}
