[CmdletBinding()]
param([switch]$ResetDatabase)

. "$PSScriptRoot\Test.Common.ps1"

$repositoryRoot = Split-Path $PSScriptRoot -Parent
Import-LocalEnvironment $repositoryRoot
Assert-RequiredEnvironmentValue 'POSTGRES_PASSWORD'
Assert-RequiredEnvironmentValue 'DMS_DB_PASSWORD'
Assert-RequiredEnvironmentValue 'PORTAL_DB_PASSWORD'

Wait-ForPostgres $repositoryRoot
Initialize-TestDatabase $repositoryRoot 'portal_dms_test' 'dms_app' -ResetDatabase:$ResetDatabase

$connectionString = "Host=localhost;Port=5432;Database=portal_dms_test;Username=dms_app;Password=$env:DMS_DB_PASSWORD;SearchPath=dms,public"
Invoke-ProjectTests $repositoryRoot 'API-DMS\API-DMS.csproj' 'API-DMS-TESTS\API-DMS-TESTS.csproj' 'DmsDbContext' 'DMS_TEST_CONNECTION' $connectionString
