# Build SmartNotch menjadi installer Setup.exe
# Jalankan dari PowerShell:  .\build-installer.ps1
$ErrorActionPreference = "Stop"
$root    = $PSScriptRoot
$dotnet  = if (Get-Command dotnet -ErrorAction SilentlyContinue) { "dotnet" } else { "C:\Program Files\dotnet\dotnet.exe" }
$project = Join-Path $root "src\SmartNotch.App\SmartNotch.App.csproj"
$publish = Join-Path $root "dist\publish"

Write-Host "==> Publish (self-contained, win-x64)..." -ForegroundColor Cyan
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
& $dotnet publish $project -c Release -o $publish
if ($LASTEXITCODE -ne 0) { throw "Publish gagal" }
Write-Host "    -> $publish\SmartNotch.exe" -ForegroundColor Green

# Cari Inno Setup
$iscc = @(
  "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
  "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
  "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
  Write-Host "Inno Setup belum terpasang. Install dulu:  winget install JRSoftware.InnoSetup" -ForegroundColor Yellow
  Write-Host "SmartNotch.exe portable tetap tersedia di: $publish" -ForegroundColor Yellow
  exit 0
}

Write-Host "==> Membuat installer..." -ForegroundColor Cyan
& $iscc (Join-Path $root "installer\SmartNotch.iss")
if ($LASTEXITCODE -ne 0) { throw "Inno Setup gagal" }
Write-Host "Selesai. Installer ada di folder: $(Join-Path $root 'dist')" -ForegroundColor Green
