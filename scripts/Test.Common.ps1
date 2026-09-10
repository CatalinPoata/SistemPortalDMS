Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Import-LocalEnvironment {
    param([string]$RepositoryRoot)

    $environmentFile = Join-Path $RepositoryRoot '.env'
    if (-not (Test-Path -LiteralPath $environmentFile)) {
        throw "Lipsește $environmentFile. Copiază .env.example în .env și completează valorile locale."
    }

    foreach ($line in Get-Content -LiteralPath $environmentFile) {
        $trimmedLine = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmedLine) -or $trimmedLine.StartsWith('#')) {
            continue
        }

        $parts = $trimmedLine -split '=', 2
        if ($parts.Count -ne 2) {
            throw "Linie .env invalidă: $line"
        }

        Set-Item -Path "Env:$($parts[0].Trim())" -Value $parts[1].Trim().Trim('"')
    }
}

function Assert-RequiredEnvironmentValue {
    param([string]$Name)

    $value = [Environment]::GetEnvironmentVariable($Name, 'Process')
    if ([string]::IsNullOrWhiteSpace($value) -or $value.StartsWith('CHANGE_ME_')) {
        throw "Variabila $Name trebuie configurată în .env."
    }
}

function Wait-ForPostgres {
    param([string]$RepositoryRoot)

    Push-Location $RepositoryRoot
    try {
        docker compose up -d postgres | Out-Host
        if ($LASTEXITCODE -ne 0) {
            throw 'Docker Desktop nu rulează sau comanda docker compose nu este disponibilă.'
        }

        foreach ($attempt in 1..30) {
            docker compose exec -T postgres pg_isready -U postgres -d portal_dms_db *> $null
            if ($LASTEXITCODE -eq 0) {
                return
            }

            Start-Sleep -Seconds 1
        }
    }
    finally {
        Pop-Location
    }

    throw 'PostgreSQL nu a devenit disponibil în 30 de secunde.'
}

function Initialize-TestDatabase {
    param(
        [string]$RepositoryRoot,
        [string]$Database,
        [string]$Owner,
        [switch]$ResetDatabase)

    Push-Location $RepositoryRoot
    try {
        if ($ResetDatabase) {
            docker compose exec -T postgres dropdb -U postgres --if-exists $Database | Out-Host
            if ($LASTEXITCODE -ne 0) {
                throw "Nu s-a putut recrea baza de test $Database."
            }
        }

        $databaseExists = $false
        docker compose exec -T postgres psql -U postgres -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname = '$Database'" |
            ForEach-Object { $databaseExists = $_.Trim() -eq '1' }
        if ($LASTEXITCODE -ne 0) {
            throw "Nu s-a putut verifica baza de test $Database."
        }

        if (-not $databaseExists) {
            docker compose exec -T postgres createdb -U postgres -O $Owner $Database | Out-Host
            if ($LASTEXITCODE -ne 0) {
                throw "Nu s-a putut crea baza de test $Database."
            }
        }

        docker compose exec -T postgres psql -U postgres -d $Database -v ON_ERROR_STOP=1 -c 'CREATE EXTENSION IF NOT EXISTS pg_trgm WITH SCHEMA public;' | Out-Host
        if ($LASTEXITCODE -ne 0) {
            throw "Nu s-a putut activa extensia pg_trgm în $Database."
        }
    }
    finally {
        Pop-Location
    }
}

function Invoke-ProjectTests {
    param(
        [string]$RepositoryRoot,
        [string]$ApiProject,
        [string]$TestProject,
        [string]$Context,
        [string]$ConnectionName,
        [string]$ConnectionString)

    Set-Item -Path "Env:$ConnectionName" -Value $ConnectionString
    $configurationName = if ($Context -eq 'DmsDbContext') {
        'ConnectionStrings__DmsDatabase'
    }
    else {
        'ConnectionStrings__PortalDatabase'
    }
    Set-Item -Path "Env:$configurationName" -Value $ConnectionString

    Push-Location $RepositoryRoot
    try {
        dotnet ef database update --project $ApiProject --startup-project $ApiProject --context $Context
        if ($LASTEXITCODE -ne 0) {
            throw "Aplicarea migrațiilor pentru $Context a eșuat."
        }

        dotnet test $TestProject
        if ($LASTEXITCODE -ne 0) {
            throw "Testele pentru $Context au eșuat."
        }
    }
    finally {
        Remove-Item -Path "Env:$ConnectionName" -ErrorAction SilentlyContinue
        Remove-Item -Path "Env:$configurationName" -ErrorAction SilentlyContinue
        Pop-Location
    }
}
