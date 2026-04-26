# Run this from the project root (where the .csproj is)
# It removes API/import service files and migrations added for ProviderId, then rebuilds.

$projRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $projRoot

# Files to remove (if they exist)
$filesToRemove = @(
    ".\ControleFutebolWeb\Services\ITeamImportService.cs",
    ".\ControleFutebolWeb\Services\TeamImportService.cs",
    ".\ControleFutebolWeb\Migrations\20260403_AddProviderIdToTime.cs",
    ".\ControleFutebolWeb\Migrations\20260403224239_AddProviderIdToTime.cs",
    ".\ControleFutebolWeb\Migrations\20260403224239_AddProviderIdToTime.Designer.cs"
)

foreach ($f in $filesToRemove) {
    if (Test-Path $f) {
        Write-Host "Removing $f"
        Remove-Item $f -Force
    }
    else {
        Write-Host "Not found: $f"
    }
}

# Rebuild solution
Write-Host "Cleaning and building project..."
dotnet clean
dotnet build

Write-Host "Done. If build succeeded, run the app (dotnet run) and test."