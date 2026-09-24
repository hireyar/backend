# Uses the same development User Secrets as dotnet run. Does not print credentials.
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
$previousConnection = $env:ConnectionStrings__Default
try {
    [xml]$project = Get-Content src/HireYar.Api/HireYar.Api.csproj
    $secretId = [string]$project.Project.PropertyGroup.UserSecretsId
    $secretRoot = if ($IsWindows) { Join-Path $env:APPDATA 'Microsoft/UserSecrets' } else { Join-Path ([Environment]::GetFolderPath('UserProfile')) '.microsoft/usersecrets' }
    $settings = Get-Content (Join-Path $secretRoot "$secretId/secrets.json") -Raw | ConvertFrom-Json -AsHashtable
    $connection = [string]$settings['ConnectionStrings:Default']
    # This helper intentionally accepts only the project's local Docker development DB.
    if ($connection -notmatch '^Host=localhost;Port=5433;Database=hireyar_development;Username=hireyar;') {
        throw 'This script requires the local Docker development connection in User Secrets.'
    }
    $env:ConnectionStrings__Default = $connection
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'Tool restore failed.' }
    dotnet ef database update --project src/HireYar.Infrastructure --startup-project src/HireYar.Api
    if ($LASTEXITCODE -ne 0) { throw 'Development migration failed. Verify Docker PostgreSQL is running.' }
} finally {
    $env:ConnectionStrings__Default = $previousConnection
    Pop-Location
}
