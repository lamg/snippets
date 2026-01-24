# Install script for Snippets Language Server (PowerShell)

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectDir = $ScriptDir

Write-Host "Installing Snippets Language Server..."

# Build the project in Release mode
Write-Host "Building project..."
dotnet publish -c Release -o "$ProjectDir\bin\release-publish" "$ProjectDir\Snippets.fsproj"

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# Install as dotnet tool
Write-Host "Installing as dotnet tool..."
dotnet tool install --global --add-source "$ProjectDir\bin\release-publish" Snippets.Tool

if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ Snippets Language Server installed successfully!" -ForegroundColor Green
    Write-Host "✓ You can now run: snippets"
    Write-Host ""
    Write-Host "Configuration:" -ForegroundColor Cyan
    Write-Host "  Default snippets file: ~/.config/helix/snippets.toml"
    Write-Host "  Debug mode: Set `$env:SNIPPETS_DEBUG=1"
} else {
    Write-Host "Installation failed!" -ForegroundColor Red
    exit 1
}
