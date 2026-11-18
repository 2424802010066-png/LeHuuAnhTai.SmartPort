<#
  TAT cac tien trinh dich vu + gateway da chay bang chay_microservices.ps1 (theo tep pid/*.pid).
  -Im : khong in gi (dung ben trong script chay)
  -TatRabbitMQ : tat ca container RabbitMQ (du lieu hang doi van giu trong volume)
  -Chi cong,ai-thi-giac : chi tat cac dich vu nay (cac dich vu khac van chay)
  Tat TOAN BO (khong -Chi, khong -Im - vd SMARTPORT.bat tat) thi tat ca tien trinh giam sat (giam_sat.ps1) TRUOC,
  de no khong chay lai cac dich vu vua tat.
  CHI tat dung tien trinh cua SmartPort: ten dotnet/python + bat dau truoc luc ghi tep pid (pid khong bi tai su dung)
  + dong lenh co ten dll / dich_vu_ai.py cua dich vu do.
#>
param([switch]$Im, [switch]$TatRabbitMQ, [string[]]$Chi)
# "pwsh -File ... -Chi cong,bai" truyen ca "cong,bai" thanh 1 chuoi (khong thanh mang) -> tach theo dau phay
if ($Chi) { $Chi = @($Chi | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ }) }
$ThuMucPid = Join-Path $PSScriptRoot "pid"

# Dau hieu tren dong lenh cua tien trinh dich vu <ten> (khop cach chay_microservices.ps1 khoi dong)
function DauHieu([string]$ten) {
    if ($ten -eq "gateway") { return "SmartPort.Gateway.dll" }
    if ($ten -eq "ai-thi-giac") { return "dich_vu_ai.py" }
    $P = ($ten -split '-' | ForEach-Object { $_.Substring(0, 1).ToUpper() + $_.Substring(1) }) -join ''
    return "SmartPort.DichVu.$P.dll"
}
# Tien trinh $id co dung la tien trinh SmartPort da ghi vao tep pid khong
function LaTienTrinhCuaTa($id, [datetime]$lucGhiPid, [string]$ten) {
    $p = Get-Process -Id $id -ErrorAction SilentlyContinue
    if (-not $p) { return $false }
    if ($p.ProcessName -ne "dotnet" -and $p.ProcessName -ne "python") { return $false }
    # pid bi Windows cap lai cho tien trinh khac: tien trinh do bat dau SAU luc ghi tep pid
    try { if ($p.StartTime -gt $lucGhiPid.AddSeconds(30)) { return $false } } catch { }
    $dongLenh = $null
    try { $dongLenh = (Get-CimInstance Win32_Process -Filter "ProcessId=$id" -ErrorAction Stop).CommandLine } catch { }
    if ($dongLenh -and $dongLenh -notlike "*$(DauHieu $ten)*") { return $false }
    return $true
}

# ---------------------------------------------------------------- tat giam sat truoc (chi khi tat toan bo)
if (-not $Chi -and -not $Im) {
    $tepKhoaGs = Join-Path $ThuMucPid "giam-sat.khoa"
    if (Test-Path $tepKhoaGs) {
        New-Item -ItemType File -Force (Join-Path $ThuMucPid "giam-sat.dung") | Out-Null     # yeu cau dung em
        $idGs = 0
        if ([int]::TryParse((Get-Content $tepKhoaGs -Raw -ErrorAction SilentlyContinue), [ref]$idGs)) {
            $pGs = Get-Process -Id $idGs -ErrorAction SilentlyContinue
            $dlGs = $null
            if ($pGs) { try { $dlGs = (Get-CimInstance Win32_Process -Filter "ProcessId=$idGs" -ErrorAction Stop).CommandLine } catch { } }
            if ($pGs -and $pGs.ProcessName -like "pwsh*" -and $dlGs -like "*giam_sat.ps1*") {
                # KHONG dung /T: cac dich vu ma giam sat da chay lai la tien trinh con cua no, se duoc tat rieng theo tep pid
                Stop-Process -Id $idGs -Force -ErrorAction SilentlyContinue
                if (-not $Im) { Write-Host "Da tat giam sat (pid $idGs)" }
            }
        }
        Remove-Item $tepKhoaGs -Force -ErrorAction SilentlyContinue
    }
}

if (Test-Path $ThuMucPid) {
    foreach ($f in Get-ChildItem $ThuMucPid -Filter *.pid) {
        if ($Chi -and ($Chi -notcontains $f.BaseName)) { continue }      # -Chi: chi tat cac dich vu nay
        $id = 0
        [void][int]::TryParse((Get-Content $f.FullName -Raw), [ref]$id)
        if ($id -gt 0 -and (LaTienTrinhCuaTa $id $f.LastWriteTime $f.BaseName)) {
            # /T = tat CA CAY tien trinh: python.exe trong .venv chi la trinh khoi chay, may chu that la tien trinh con
            # (chi tat cha thi con van giu cong 8100)
            taskkill /PID $id /T /F 2>$null | Out-Null
            if (-not $Im) { Write-Host "Da tat $($f.BaseName) (pid $id)" }
        }
        Remove-Item $f.FullName -Force
    }
}
if ($TatRabbitMQ) {
    # docker-compose.yml bat buoc co RABBITMQ_MAT_KHAU (ke ca khi stop) -> doc tu .bimat nhu chay_microservices.ps1
    $TepBiMat = Join-Path $PSScriptRoot ".bimat\bi-mat.json"
    if (-not $env:RABBITMQ_MAT_KHAU -and (Test-Path $TepBiMat)) {
        $env:RABBITMQ_MAT_KHAU = (Get-Content $TepBiMat -Raw | ConvertFrom-Json).rabbitMatKhau
    }
    $coDocker = [bool](Get-Command docker -ErrorAction SilentlyContinue)
    if ($coDocker) { docker info *> $null }
    if (-not $coDocker -or $LASTEXITCODE -ne 0) {
        if (-not $Im) { Write-Host "RabbitMQ da tat (Docker Desktop khong chay)" }
    } else {
        docker compose -f (Join-Path $PSScriptRoot "docker-compose.yml") stop
        if ($LASTEXITCODE -ne 0) { Write-Host "Khong tat duoc RabbitMQ (docker compose stop loi $LASTEXITCODE)" -ForegroundColor Red }
    }
}
