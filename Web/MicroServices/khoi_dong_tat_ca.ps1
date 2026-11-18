<#
  BAM 1 LAN CHAY TOAN BO SMARTPORT (goi tu CHAY_SMARTPORT.bat hoac loi tat "SmartPort" ngoai Desktop):
    1. Bat Docker Desktop neu chua chay (RabbitMQ chay trong Docker), doi san sang
       Docker khong len -> KHONG dung: bao vang "chay suy giam", van chay 13 dich vu (su kien cho trong Outbox)
    2. Kiem tra SQL Server (.\MSSQLSERVER01) - bat buoc (khong co CSDL thi khong nghiep vu nao chay duoc)
    3. chay_microservices.ps1: RabbitMQ + CSDL tung dich vu + 13 dich vu .NET + AI Python + API gateway
       (da build roi thi bo qua buoc build cho nhanh; them tham so -Build de build lai)
    4. Mo trinh duyet http://localhost:5100
  Tuy chon: -ChoPhepLan (may tram trong mang noi bo vao qua https://<ip>:5443)
            -GiamSat    (bat them giam_sat.ps1: tu chay lai dich vu / RabbitMQ bi chet)
            -CheDoKiemThu (CHI de chay bo kiem thu: dinh-danh tra maDemo OTP cho yeu cau loopback - SMARTPORT.bat chay-kiem-thu)
  Tat: TAT_SMARTPORT.bat
#>
param([switch]$Build, [switch]$ChoPhepLan, [switch]$GiamSat, [switch]$CheDoKiemThu)
$ErrorActionPreference = "Continue"
$Goc = $PSScriptRoot
$Web = Split-Path -Parent $Goc

function Buoc($s) { Write-Host ""; Write-Host "==> $s" -ForegroundColor Cyan }

# ---------------------------------------------------------------- 1. Docker
Buoc "Kiem tra Docker (RabbitMQ chay trong Docker)"
$dockerSan = $false
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Write-Host "Chua cai Docker Desktop -> chay SUY GIAM (khong RabbitMQ). Nghiep vu van chay, su kien cho trong Outbox." -ForegroundColor Yellow
} else {
    docker info *> $null
    if ($LASTEXITCODE -eq 0) { $dockerSan = $true }
    else {
        Write-Host "Docker chua chay -> dang bat Docker Desktop..."
        $daBat = $true
        docker desktop start *> $null
        if ($LASTEXITCODE -ne 0) {
            $exe = Join-Path $env:ProgramFiles "Docker\Docker\Docker Desktop.exe"
            if (Test-Path $exe) { Start-Process $exe } else { $daBat = $false; Write-Host "Khong tim thay Docker Desktop.exe" -ForegroundColor Yellow }
        }
        if ($daBat) {
            $het = (Get-Date).AddMinutes(4)
            do { Start-Sleep -Seconds 3; Write-Host -NoNewline "."; docker info *> $null } while ($LASTEXITCODE -ne 0 -and (Get-Date) -lt $het)
            Write-Host ""
            if ($LASTEXITCODE -eq 0) { $dockerSan = $true }
        }
        if (-not $dockerSan) {
            Write-Host "Docker chua san sang -> chay SUY GIAM (khong RabbitMQ): nghiep vu van chay, su kien cho trong Outbox." -ForegroundColor Yellow
            Write-Host "Khi Docker len: 'SMARTPORT.bat giam-sat' tu bat lai RabbitMQ (hoac chay lai 'SMARTPORT.bat chay')." -ForegroundColor Yellow
        }
    }
}
if ($dockerSan) { Write-Host "Docker: san sang" -ForegroundColor Green }

# ---------------------------------------------------------------- 2. SQL Server
Buoc "Kiem tra SQL Server (.\MSSQLSERVER01)"
$sql = Get-Service -Name 'MSSQL$MSSQLSERVER01' -ErrorAction SilentlyContinue
if ($sql -and $sql.Status -ne 'Running') {
    try { Start-Service $sql -ErrorAction Stop; Write-Host "Da bat SQL Server" -ForegroundColor Green }
    catch { Write-Host "SQL Server dang tat va can quyen quan tri de bat: chuot phai CHAY_SMARTPORT.bat -> Run as administrator." -ForegroundColor Red; exit 1 }
} else { Write-Host "SQL Server: dang chay" -ForegroundColor Green }

# ---------------------------------------------------------------- 3. he thong
Buoc "Khoi dong 13 dich vu .NET + AI Python + API gateway"
$daBuild = Test-Path (Join-Path $Web "SmartPort.Gateway\bin\Debug\net10.0\SmartPort.Gateway.dll")
# Splat bang HASHTABLE: splat mang thi "-KhongBuild" bi coi la GIA TRI cua tham so vi tri dau (-Chi) -> khong chay dich vu nao
$thamSo = @{}
if ($daBuild -and -not $Build) { $thamSo.KhongBuild = $true } else { Write-Host "Build lan dau / theo yeu cau (1-2 phut)..." }
if ($ChoPhepLan) { $thamSo.ChoPhepLan = $true }
if ($CheDoKiemThu) { $thamSo.CheDoKiemThu = $true }
& (Join-Path $Goc "chay_microservices.ps1") @thamSo
if (-not $?) { Write-Host "Khoi dong loi - xem MicroServices\nhat-ky\*.log" -ForegroundColor Red; exit 1 }

# ---------------------------------------------------------------- 3b. giam sat (tuy chon)
if ($GiamSat) {
    $pwsh = [Environment]::ProcessPath
    if (-not $pwsh -or -not (Test-Path $pwsh)) { $pwsh = "pwsh" }
    Start-Process -FilePath $pwsh -WindowStyle Minimized -ArgumentList @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "`"$(Join-Path $Goc 'giam_sat.ps1')`"")
    Write-Host "Da bat giam sat (cua so thu nho 'SmartPort - Giam sat', log: MicroServices\nhat-ky\giam-sat.log)" -ForegroundColor Green
}

# ---------------------------------------------------------------- 4. trinh duyet
Start-Process "http://localhost:5100"
Write-Host ""
Write-Host "SmartPort dang chay nen: http://localhost:5100" -ForegroundColor Green
Write-Host "Dong cua so nay KHONG tat he thong. Muon tat: bam dup TAT_SMARTPORT.bat"
