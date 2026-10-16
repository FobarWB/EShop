<#
.SYNOPSIS
Converts the Northwind data in archive/Northwind4SQLServer.sql into a PostgreSQL data-only script.

.DESCRIPTION
The schema is created by EF Core migrations; this script produces only the rows.
It reads the INSERT statements of the SQL Server dump and rewrites them for PostgreSQL:
snake_case names, bytea and ISO date literals, booleans, parent tables before child tables,
and identity sequences moved past the inserted ids.

.EXAMPLE
powershell -File db/convert-northwind.ps1
#>
param(
    [string]$Source = (Join-Path $PSScriptRoot '..\archive\Northwind4SQLServer.sql'),
    [string]$Output = (Join-Path $PSScriptRoot 'northwind-data.sql')
)

$ErrorActionPreference = 'Stop'

# Load order (parents first). Columns: used when the dump INSERT has no column list.
# Identity: generated key whose sequence must be reset after explicit ids are inserted.
$tables = [ordered]@{
    'Categories'          = @{ Name = 'categories'; Identity = 'category_id' }
    'Suppliers'           = @{ Name = 'suppliers'; Identity = 'supplier_id' }
    'Products'            = @{ Name = 'products'; Identity = 'product_id' }
    'Shippers'            = @{ Name = 'shippers'; Identity = 'shipper_id' }
    'Customers'           = @{
        Name    = 'customers'
        Columns = 'CustomerId', 'CompanyName', 'ContactName', 'ContactTitle', 'Address', 'City',
                  'Region', 'PostalCode', 'Country', 'Phone', 'Fax'
    }
    'Employees'           = @{ Name = 'employees'; Identity = 'employee_id' }
    'Region'              = @{ Name = 'regions'; Columns = 'RegionId', 'RegionDescription' }
    'Territories'         = @{ Name = 'territories'; Columns = 'TerritoryId', 'TerritoryDescription', 'RegionId' }
    'EmployeeTerritories' = @{ Name = 'employee_territories'; Columns = 'EmployeeId', 'TerritoryId' }
    'Orders'              = @{ Name = 'orders'; Identity = 'order_id' }
    'Order Details'       = @{ Name = 'order_details'; Columns = 'OrderId', 'ProductId', 'UnitPrice', 'Quantity', 'Discount' }
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

$sql = New-Object System.Text.StringBuilder
[void]$sql.AppendLine('-- Northwind sample data for the EShop database.')
[void]$sql.AppendLine('-- Generated by db/convert-northwind.ps1 from archive/Northwind4SQLServer.sql; do not edit by hand.')
[void]$sql.AppendLine('-- Load into an empty database after `dotnet ef database update`:')
[void]$sql.AppendLine('--   psql -h localhost -U eshop -d eshop -f db/northwind-data.sql')
[void]$sql.AppendLine()
[void]$sql.AppendLine('\set ON_ERROR_STOP on')
[void]$sql.AppendLine("SET client_encoding = 'UTF8';")
[void]$sql.AppendLine()
[void]$sql.AppendLine('BEGIN;')

foreach ($key in $tables.Keys) {
    $table = $tables[$key]
    if ($rows[$key].Count -eq 0) { throw "No rows found for $key" }

    [void]$sql.AppendLine()
    [void]$sql.AppendLine("INSERT INTO $($table.Name) ($($columnsByTable[$key] -join ', ')) VALUES")
    $lines = $rows[$key] | ForEach-Object { '    (' + ($_ -join ', ') + ')' }
    [void]$sql.Append($lines -join ",`n")
    [void]$sql.AppendLine(';')
    Write-Host ('{0,-22} {1,5} rows' -f $table.Name, $rows[$key].Count)
}

[void]$sql.AppendLine()
[void]$sql.AppendLine('-- Explicit ids do not advance identity sequences; move each sequence past the loaded rows.')
foreach ($key in $tables.Keys) {
    $table = $tables[$key]
    if ($table.Identity) {
        [void]$sql.AppendLine("SELECT setval(pg_get_serial_sequence('$($table.Name)', '$($table.Identity)'), max($($table.Identity))) FROM $($table.Name);")
    }
}

[void]$sql.AppendLine()
[void]$sql.AppendLine('COMMIT;')

[IO.File]::WriteAllText($Output, $sql.ToString().Replace("`r`n", "`n"), (New-Object Text.UTF8Encoding $false))
Write-Host "Written $Output"
