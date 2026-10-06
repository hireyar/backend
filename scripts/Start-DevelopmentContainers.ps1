# Starts local services using the ignored appsettings file, without an .env file.
param([switch]$IncludeApi)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$settingsPath = Join-Path $projectRoot 'src/HireYar.Api/appsettings.Development.local.json'
if (-not (Test-Path -LiteralPath $settingsPath)) {
    throw 'Create src/HireYar.Api/appsettings.Development.local.json first.'
}
$settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
$connection = [System.Data.Common.DbConnectionStringBuilder]::new()
$connection.set_ConnectionString([string]$settings.ConnectionStrings.Default)
$password = [string]$connection['Password']
$signingKey = [string]$settings.Security.SigningKey
if ([string]::IsNullOrWhiteSpace($password) -or [Text.Encoding]::UTF8.GetByteCount($signingKey) -lt 32) {
    throw 'Local appsettings requires a database password and a JWT signing key of at least 32 bytes.'
}
$previousPassword = $env:POSTGRES_PASSWORD
$previousSigningKey = $env:JWT_SIGNING_KEY
try {
    $env:POSTGRES_PASSWORD = $password
    $env:JWT_SIGNING_KEY = $signingKey
    Push-Location $projectRoot
    try {
        if ($IncludeApi) {
            docker compose -f compose.dev.yaml up -d --build postgres api
        } else {
            docker compose -f compose.dev.yaml up -d postgres
        }
        if ($LASTEXITCODE -ne 0) { throw 'Docker Compose failed.' }
    } finally {
        Pop-Location
    }
} finally {
    $env:POSTGRES_PASSWORD = $previousPassword
    $env:JWT_SIGNING_KEY = $previousSigningKey
}
