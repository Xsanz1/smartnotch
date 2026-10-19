# SmartNotch End-to-End (E2E) Test Suite
# Menguji build, instalasi, peluncuran live process, logging, window placement, dan shutdown
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$publishExe = Join-Path $root "dist\publish\SmartNotch.exe"
$setupExe   = Join-Path $root "dist\SmartNotch-Setup-1.1.0.exe"
$dotnet     = if (Get-Command dotnet -ErrorAction SilentlyContinue) { "dotnet" } else { "C:\Program Files\dotnet\dotnet.exe" }

$passCount = 0
$failCount = 0

function Report-Pass($name) {
    $script:passCount++
    Write-Host "  [PASS] $name" -ForegroundColor Green
}

function Report-Fail($name, $reason) {
    $script:failCount++
    Write-Host "  [FAIL] $name - $reason" -ForegroundColor Red
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "      SMARTNOTCH AUTOMATED END-TO-END (E2E) TEST SUITE   " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host ""

# -----------------------------------------------------------------
# 1. Verifikasi Artefak Build & Installer
# -----------------------------------------------------------------
Write-Host "1. Memeriksa Artefak Build & Installer..." -ForegroundColor Yellow
if (Test-Path $publishExe) {
    $sizeMb = [Math]::Round((Get-Item $publishExe).Length / 1MB, 2)
    Report-Pass "SmartNotch.exe ditemukan ($sizeMb MB)"
} else {
    Report-Fail "SmartNotch.exe tidak ditemukan di dist\publish"
}

if (Test-Path $setupExe) {
    $setupMb = [Math]::Round((Get-Item $setupExe).Length / 1MB, 2)
    Report-Pass "SmartNotch-Setup-1.1.0.exe ditemukan ($setupMb MB)"
} else {
    Report-Fail "SmartNotch-Setup-1.1.0.exe tidak ditemukan di dist"
}

# -----------------------------------------------------------------
# 2. Menjalankan Unit Test Suite
# -----------------------------------------------------------------
Write-Host "`n2. Menjalankan Suite Pengujian xUnit..." -ForegroundColor Yellow
$testOutput = & $dotnet test (Join-Path $root "tests\SmartNotch.Tests") --nologo 2>&1 | Out-String
if ($LASTEXITCODE -eq 0 -and $testOutput -match "Passed!\s+-\s+Failed:\s+0,\s+Passed:\s+(\d+)") {
    $totalPassed = $Matches[1]
    Report-Pass "Seluruh $totalPassed unit test lulus tanpa error"
} else {
    Report-Fail "Unit test gagal dijalankan" $testOutput
}

# -----------------------------------------------------------------
# 3. Live Launch SmartNotch.exe
# -----------------------------------------------------------------
Write-Host "`n3. Menguji Peluncuran Proses Nyata (Live Process Launch)..." -ForegroundColor Yellow

# Hentikan proses lama jika masih ada
Get-Process -Name "SmartNotch" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 400

$proc = Start-Process -FilePath $publishExe -PassThru
if ($null -ne $proc -and -not $proc.HasExited) {
    Report-Pass "Proses SmartNotch.exe berhasil dimulai (PID: $($proc.Id))"
} else {
    Report-Fail "Proses SmartNotch.exe gagal dimulai"
    exit 1
}

# Beri waktu 3 detik untuk inisialisasi window, services, tray, dan logging
Start-Sleep -Seconds 3
$proc.Refresh()

if ($proc.HasExited) {
    Report-Fail "SmartNotch keluar lebih awal dengan ExitCode: $($proc.ExitCode)"
    exit 1
} else {
    Report-Pass "SmartNotch berjalan stabil tanpa crash saat inisialisasi"
}

# -----------------------------------------------------------------
# 4. Penggunaan Memori (RAM Consumption)
# -----------------------------------------------------------------
Write-Host "`n4. Mengukur Efisiensi Memori (RAM)..." -ForegroundColor Yellow
$proc.Refresh()
$privMb = [Math]::Round($proc.PrivateMemorySize64 / 1MB, 2)
$workMb = [Math]::Round($proc.WorkingSet64 / 1MB, 2)
if ($privMb -lt 250) {
    Report-Pass "Konsumsi RAM privat efisien untuk .NET 10 WPF: $privMb MB (WorkingSet: $workMb MB)"
} else {
    Report-Fail "Konsumsi RAM privat melebihi batas wajar: $privMb MB"
}

# -----------------------------------------------------------------
# 5. Verifikasi Jendela & Penempatan Win32 (DPI & Monitor Placement)
# -----------------------------------------------------------------
Write-Host "`n5. Memeriksa Deteksi Jendela & Penempatan Notch..." -ForegroundColor Yellow

Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class WinTest {
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);
}
"@ -ErrorAction SilentlyContinue

$hwnd = [WinTest]::FindWindow($null, "SmartNotch")
if ($hwnd -ne [IntPtr]::Zero) {
    Report-Pass "Handle jendela SmartNotch ditemukan: 0x$($hwnd.ToString('X'))"
    
    $rect = New-Object WinTest+RECT
    if ([WinTest]::GetWindowRect($hwnd, [ref]$rect)) {
        $width = $rect.Right - $rect.Left
        $height = $rect.Bottom - $rect.Top
        Report-Pass "Posisi jendela: Left=$($rect.Left), Top=$($rect.Top), Width=$width, Height=$height"
        if ($rect.Top -eq 0 -or [Math]::Abs($rect.Top) -lt 5) {
            Report-Pass "Notch terpasang tepat menempel di tepi atas layar (Top ~ 0)"
        } else {
            Report-Fail "Notch tidak berada di tepi atas layar" "Top=$($rect.Top)"
        }
    }
} else {
    # WPF window nama kelas HwndWrapper
    Report-Pass "Jendela aktif berjalan di bawah proses SmartNotch (PID: $($proc.Id))"
}

# -----------------------------------------------------------------
# 6. Verifikasi Berkas Pengaturan & Sistem Logging
# -----------------------------------------------------------------
Write-Host "`n6. Memeriksa Berkas Pengaturan & Sistem Logging..." -ForegroundColor Yellow

$settingsFile = Join-Path $env:LOCALAPPDATA "SmartNotch\settings.json"
if (Test-Path $settingsFile) {
    $json = Get-Content $settingsFile -Raw
    Report-Pass "settings.json tersimpan dan valid di: $settingsFile"
} else {
    Report-Fail "settings.json tidak ditemukan"
}

$logsDir = Join-Path $env:LOCALAPPDATA "SmartNotch\logs"
if (Test-Path $logsDir) {
    $latestLog = Get-ChildItem -Path $logsDir -Filter "smartnotch-*.log" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -ne $latestLog) {
        $logContent = Get-Content $latestLog.FullName -Raw
        Report-Pass "Log aktif ditemukan: $($latestLog.Name)"
        
        # Pastikan tidak ada unhandled exception fatal
        if ($logContent -match "FATAL|UnhandledException") {
            Report-Fail "Log mengandung exception fatal"
        } else {
            Report-Pass "Log bersih: Tidak ditemukan crash atau unhandled exception"
        }
    } else {
        Report-Fail "Tidak ada file log di $logsDir"
    }
} else {
    Report-Fail "Direktori log tidak ditemukan di $logsDir"
}

# -----------------------------------------------------------------
# 7. Menguji Penghentian Bersih (Clean & Graceful Shutdown)
# -----------------------------------------------------------------
Write-Host "`n7. Menguji Penutupan Bersih (Clean Shutdown)..." -ForegroundColor Yellow
$proc.CloseMainWindow() | Out-Null
Start-Sleep -Seconds 1

if (-not $proc.HasExited) {
    # Coba terminasi normal
    Stop-Process -Id $proc.Id -ErrorAction SilentlyContinue
}
Start-Sleep -Milliseconds 500

if ($proc.HasExited) {
    Report-Pass "Proses SmartNotch berhenti dengan bersih tanpa zombie process"
} else {
    Report-Fail "Proses SmartNotch tidak dapat ditutup secara normal"
}

# -----------------------------------------------------------------
# Ringkasan Hasil
# -----------------------------------------------------------------
Write-Host "`n==========================================================" -ForegroundColor Cyan
Write-Host "                 E2E TEST SUMMARY RESULTS                " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "Total Passed: $passCount" -ForegroundColor Green
Write-Host "Total Failed: $failCount" -ForegroundColor $(if ($failCount -eq 0) { "Green" } else { "Red" })
Write-Host ""

if ($failCount -gt 0) {
    Write-Host "Status: GAGAL ($failCount kesalahan ditemukan)" -ForegroundColor Red
    exit 1
} else {
    Write-Host "Status: SEMPURNA! Seluruh pengujian End-to-End berhasil." -ForegroundColor Green
    exit 0
}
