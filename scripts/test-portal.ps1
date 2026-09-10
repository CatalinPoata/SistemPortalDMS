[CmdletBinding()]
param([switch]$ResetDatabase)

. "$PSScriptRoot\Test.Common.ps1"

$repositoryRoot = Split-Path $PSScriptRoot -Parent
Import-LocalEnvironment $repositoryRoot
Assert-RequiredEnvironmentValue 'POSTGRES_PASSWORD'
Assert-RequiredEnvironmentValue 'PORTAL_DB_PASSWORD'
Assert-RequiredEnvironmentValue 'DMS_DB_PASSWORD'

Wait-ForPostgres $repositoryRoot
Initialize-TestDatabase $repositoryRoot 'portal_portal_test' 'portal_app' -ResetDatabase:$ResetDatabase

$connectionString = "Host=localhost;Port=5432;Database=portal_portal_test;Username=portal_app;Password=$env:PORTAL_DB_PASSWORD;SearchPath=portal"
Invoke-ProjectTests $repositoryRoot 'API-PORTAL\API-PORTAL.csproj' 'API-PORTAL-TESTS\API-PORTAL-TESTS.csproj' 'PortalDbContext' 'PORTAL_TEST_CONNECTION' $connectionString
