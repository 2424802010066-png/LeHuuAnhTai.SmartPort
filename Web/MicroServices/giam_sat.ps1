<#
  GIAM SAT SMARTPORT (watchdog) - chay nen, moi 15 giay kiem 1 vong:
    DICH VU (dich-vu.json + gateway):
      - Khong co tep pid/<ten>.pid  -> dich vu chua chay / da tat CO CHU DICH -> khong dung toi.
      - Co tep pid ma tien trinh mat (2 vong lien tiep), hoac tien trinh con ma /suc-khoe KHONG tra loi 3 vong lien tiep
        (sau thoi gian an han khoi dong) -> chay lai DUNG dich vu do:
            chay_microservices.ps1 -KhongBuild -BoQuaHaTang -Chi <ten,...> [-ChoPhepLan neu he thong dang mo LAN]
      - /suc-khoe tra 503 (CSDL loi) -> chi ghi log: chay lai tien trinh khong chua duoc loi CSDL.
    RABBITMQ (chi khi he thong dang chay): Docker tat -> "docker desktop start"; container dung -> docker start
      (chua co container -> docker compose up -d); unhealthy 3 vong lien tiep -> docker restart.
    CHONG KHOI DONG LAI DON DAP: toi da 3 lan / 5 phut cho moi dich vu (va Docker, RabbitMQ). Vuot gioi han 2 dot ma
      dich vu chua lan nao chay on dinh qua 5 phut -> NGUNG tu chay lai, ghi "CAN NGUOI XU LY".
    AN TOAN: chi 1 ban giam sat chay cung luc; tam ngung khi chay_microservices.ps1 dang khoi dong; KHONG tu tat tien trinh
      nao (tat ban cu la viec cua dung_microservices.ps1 - co kiem dung tien trinh SmartPort theo pid + gio + dong lenh).
    NHAT KY: nhat-ky/giam-sat.log (ghi them; log cu doi ten theo gio vao nhat-ky/cu/, giu 10 ban).
  Dung:  pwsh MicroServices/giam_sat.ps1 [-ChuKyGiay 15] [-ToiDaLan 3] [-CuaSoPhut 5]
         pwsh MicroServices/giam_sat.ps1 -InLenhLich     (chi IN lenh Task Scheduler, khong tu dang ky)
  Tat:   dong cua so giam sat, hoac SMARTPORT.bat tat (tat ca he thong lan giam sat)
#>
param([int]$ChuKyGiay = 15, [int]$ToiDaLan = 3, [int]$CuaSoPhut = 5, [switch]$InLenhLich)
$ErrorActionPreference = "Continue"
$Goc = $PSScriptRoot
$Web = Split-Path -Parent $Goc
$ThuMucLog = Join-Path $Goc "nhat-ky"
$ThuMucPid = Join-Path $Goc "pid"
$TepLog = Join-Path $ThuMucLog "giam-sat.log"
$TepChay = Join-Path $Goc "chay_microservices.ps1"
$TepCompose = Join-Path $Goc "docker-compose.yml"
$TepKhoa = Join-Path $ThuMucPid "giam-sat.khoa"
$TepDung = Join-Path $ThuMucPid "giam-sat.dung"
$Pwsh = [Environment]::ProcessPath
if (-not $Pwsh -or $Pwsh -notmatch 'pwsh(\.exe)?$') { $Pwsh = (Get-Command pwsh -ErrorAction SilentlyContinue).Source }

# ---------------------------------------------------------------- chi in lenh Task Scheduler
if ($InLenhLich) {
    $pw = Join-Path $env:LOCALAPPDATA "Microsoft\WindowsApps\pwsh.exe"     # bi danh on dinh, khong doi theo phien ban pwsh
    if (-not (Test-Path $pw)) { $pw = $Pwsh }
    $kd = Join-Path $Goc "khoi_dong_tat_ca.ps1"
    Write-Host "LENH DANG KY TASK SCHEDULER (script KHONG tu dang ky - tu chay trong PowerShell neu muon):" -ForegroundColor Cyan
    Write-Host "# Tu chay SmartPort + giam sat moi khi dang nhap Windows (tai khoan hien tai, khong can quyen admin):"
    Write-Host "`$tacVu = New-ScheduledTaskAction -Execute '$pw' -Argument '-NoProfile -ExecutionPolicy Bypass -WindowStyle Minimized -File `"$kd`" -GiamSat'"
    Write-Host "`$kichHoat = New-ScheduledTaskTrigger -AtLogOn -User `"`$env:USERDOMAIN\`$env:USERNAME`""
    Write-Host "`$caiDat = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit ([TimeSpan]::Zero)"
    Write-Host "Register-ScheduledTask -TaskName 'SmartPort - Chay va giam sat' -Action `$tacVu -Trigger `$kichHoat -Settings `$caiDat"
    Write-Host "# Go bo: Unregister-ScheduledTask -TaskName 'SmartPort - Chay va giam sat' -Confirm:`$false"
    exit 0
}

New-Item -ItemType Directory -Force $ThuMucLog, $ThuMucPid | Out-Null
try { $Host.UI.RawUI.WindowTitle = "SmartPort - Giam sat" } catch { }

# ---------------------------------------------------------------- ham phu
function XoayLog([string]$ten) {
    $tep = Join-Path $ThuMucLog "$ten.log"
    if (-not (Test-Path $tep) -or (Get-Item $tep).Length -eq 0) { return }
    $thuMucCu = Join-Path $ThuMucLog "cu"
    New-Item -ItemType Directory -Force $thuMucCu | Out-Null
    $dich = Join-Path $thuMucCu ("{0}_{1}.log" -f $ten, (Get-Item $tep).LastWriteTime.ToString("yyyyMMdd_HHmmss"))
    try { Move-Item -LiteralPath $tep -Destination $dich -Force -ErrorAction Stop } catch { return }
    $mau = '^' + [regex]::Escape($ten) + '_\d{8}_\d{6}\.log$'
    Get-ChildItem $thuMucCu -File | Where-Object { $_.Name -match $mau } | Sort-Object Name -Descending |
        Select-Object -Skip 10 | Remove-Item -Force -ErrorAction SilentlyContinue
}
function Ghi([string]$s, [string]$mau = "Gray") {
    $dong = "{0} {1}" -f (Get-Date).ToString("yyyy-MM-dd HH:mm:ss"), $s
    Write-Host $dong -ForegroundColor $mau
    try {
        if ((Test-Path $TepLog) -and (Get-Item $TepLog).Length -gt 5MB) { XoayLog "giam-sat" }
        [System.IO.File]::AppendAllText($TepLog, $dong + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
    } catch { }
}
function GoiDocker([string[]]$ThamSo, [int]$GiayToiDa = 60) {
    $psi = [System.Diagnostics.ProcessStartInfo]::new("docker")
    foreach ($t in $ThamSo) { $psi.ArgumentList.Add($t) }
    $psi.UseShellExecute = $false; $psi.CreateNoWindow = $true
    $psi.RedirectStandardOutput = $true; $psi.RedirectStandardError = $true
    try { $p = [System.Diagnostics.Process]::Start($psi) }
    catch { return [pscustomobject]@{ Ma = -1; Ra = ""; Loi = $_.Exception.Message } }
    $ra = $p.StandardOutput.ReadToEndAsync(); $loi = $p.StandardError.ReadToEndAsync()
    if (-not $p.WaitForExit($GiayToiDa * 1000)) {
        try { $p.Kill($true) } catch { }       # chi tat chinh lenh docker vua goi (con cua giam sat)
        return [pscustomobject]@{ Ma = -2; Ra = ""; Loi = "docker $($ThamSo[0]) qua $GiayToiDa giay" }
    }
    $p.WaitForExit()
    return [pscustomobject]@{ Ma = $p.ExitCode; Ra = "$($ra.Result)".Trim(); Loi = "$($loi.Result)".Trim() }
}
function DongCuoi([string]$s) { (@("$s" -split "`r?`n" | Where-Object { $_.Trim() }) | Select-Object -Last 1) }

# Dau hieu dong lenh + kiem dung tien trinh (giong dung_microservices.ps1)
function DauHieu([string]$ten) {
    if ($ten -eq "gateway") { return "SmartPort.Gateway.dll" }
    if ($ten -eq "ai-thi-giac") { return "dich_vu_ai.py" }
    $P = ($ten -split '-' | ForEach-Object { $_.Substring(0, 1).ToUpper() + $_.Substring(1) }) -join ''
    return "SmartPort.DichVu.$P.dll"
}
function LaTienTrinhCuaTa($id, [datetime]$lucGhiPid, [string]$ten) {
    $p = Get-Process -Id $id -ErrorAction SilentlyContinue
    if (-not $p) { return $false }
    if ($p.ProcessName -ne "dotnet" -and $p.ProcessName -ne "python") { return $false }
    try { if ($p.StartTime -gt $lucGhiPid.AddSeconds(30)) { return $false } } catch { }
    $dongLenh = $null
    try { $dongLenh = (Get-CimInstance Win32_Process -Filter "ProcessId=$id" -ErrorAction Stop).CommandLine } catch { }
    if ($dongLenh -and $dongLenh -notlike "*$(DauHieu $ten)*") { return $false }
    return $true
}

# Trang thai tung doi tuong giam sat (dich vu, "docker", "rabbitmq")
# $script: TUONG MINH: PowerShell khong phan biet hoa thuong + pham vi dong -> bien cuc bo "$trangThai" cua ham goi
# se che mat bang nay neu chi viet $TrangThai (loi that: KiemRabbitMQ -> "String khong co ContainsKey")
$script:TrangThai = @{}
function LayTrangThai([string]$ten) {
    if (-not $script:TrangThai.ContainsKey($ten)) {
        $script:TrangThai[$ten] = @{ TinhTrang = ""; SoLanMat = 0; SoLanKhongTraLoi = 0; LanKhoiDong = @(); SoDotVuot = 0; DaBaoVuot = $false; BiKhoa = $false }
    }
    return $script:TrangThai[$ten]
}
function DoiTinhTrang([string]$ten, [string]$tt, [string]$mau = "Gray") {
    $st = LayTrangThai $ten
    if ($st.TinhTrang -ne $tt) { $st.TinhTrang = $tt; Ghi ("{0,-12} {1}" -f $ten, $tt) $mau }
}
# Gioi han: toi da $ToiDaLan lan / $CuaSoPhut phut. Tra ve $true (va ghi nhan 1 lan) neu duoc phep khoi dong lai.
function DuocChayLai([string]$ten) {
    $st = LayTrangThai $ten
    if ($st.BiKhoa) { return $false }
    $moc = (Get-Date).AddMinutes(-$CuaSoPhut)
    $st.LanKhoiDong = @($st.LanKhoiDong | Where-Object { $_ -gt $moc })
    if ($st.LanKhoiDong.Count -ge $ToiDaLan) {
        if (-not $st.DaBaoVuot) {
            $st.DaBaoVuot = $true
            $st.SoDotVuot++
            if ($st.SoDotVuot -ge 2) {
                $st.BiKhoa = $true
                Ghi "$ten : hong lien tuc ($($ToiDaLan * 2) lan chay lai khong on dinh) -> NGUNG tu chay lai. CAN NGUOI XU LY (xem nhat-ky/$ten.loi.log, nhat-ky/$ten.log)" "Red"
            } else {
                Ghi "$ten : da chay lai $ToiDaLan lan trong $CuaSoPhut phut -> tam dung cho het cua so $CuaSoPhut phut" "Red"
            }
        }
        return $false
    }
    $st.DaBaoVuot = $false
    $st.LanKhoiDong = @($st.LanKhoiDong) + @(Get-Date)
    return $true
}
# Chay on dinh -> xoa dem vuot gioi han (mo lai tu chay lai)
function DatLaiGioiHan([string]$ten) {
    $st = LayTrangThai $ten
    if ($st.BiKhoa -or $st.SoDotVuot -gt 0) { Ghi "$ten : da chay on dinh lai -> mo lai tu chay lai" "Green" }
    $st.BiKhoa = $false; $st.SoDotVuot = 0; $st.DaBaoVuot = $false
}

function DangKhoiDong {
    $tep = Join-Path $ThuMucPid "dang-khoi-dong.khoa"
    if (-not (Test-Path $tep)) { return $false }
    $f = Get-Item $tep
    if (((Get-Date) - $f.LastWriteTime).TotalMinutes -gt 15) { return $false }   # khoa sot lai (script bi tat giua chung)
    $id = 0
    [void][int]::TryParse((Get-Content $tep -Raw -ErrorAction SilentlyContinue), [ref]$id)
    $p = if ($id -gt 0) { Get-Process -Id $id -ErrorAction SilentlyContinue } else { $null }
    return [bool]($p -and $p.ProcessName -like "pwsh*")
}
function DocCheDo {
    $tep = Join-Path $ThuMucPid "che-do.json"
    if (-not (Test-Path $tep)) { return $null }
    try { return (Get-Content $tep -Raw | ConvertFrom-Json) } catch { return $null }
}
function DocDanhMuc {
    $dm = Get-Content (Join-Path $Web "dich-vu.json") -Raw | ConvertFrom-Json
    $ds = @([pscustomobject]@{ Ten = "gateway"; Cong = [int]$dm.gateway.cong; AnHan = 120 })
    foreach ($dv in $dm.dichVu) {
        # AI nap mo hinh lau hon (GPU / CPU) -> an han dai hon
        $ds += [pscustomobject]@{ Ten = $dv.ten; Cong = [int]$dv.cong; AnHan = $(if ($dv.nenTang -eq "python") { 300 } else { 120 }) }
    }
    return $ds
}
function KiemSucKhoe([int]$cong) {
    try { return [int](Invoke-WebRequest "http://127.0.0.1:$cong/suc-khoe" -TimeoutSec 8 -SkipHttpErrorCheck).StatusCode }
    catch { return 0 }
}

# Chay lai cac dich vu: tien trinh pwsh RIENG (ShellExecute, an cua so) - khong noi ong dan vao giam sat de cac dich vu
# moi chay khong giu ong ra cua giam sat. Ket qua cua chay_microservices ghi them vao giam-sat.log.
function ChayLai([string[]]$ds) {
    $cheDo = DocCheDo
    function Nhay([string]$s) { "'" + ($s -replace "'", "''") + "'" }
    $lenh = "& " + (Nhay $TepChay) + " -KhongBuild -BoQuaHaTang -Chi " + (Nhay ($ds -join ","))
    if ($cheDo -and $cheDo.choPhepLan) { $lenh += " -ChoPhepLan" }
    # dinh-danh dang chay o che do kiem thu (pid trong che-do.json = pid trong tep pid) -> chay lai van o che do do
    $kiemThu = $false
    if ($ds -contains "dinh-danh" -and $cheDo -and $cheDo.cheDoKiemThuPid) {
        $pidDd = "$(Get-Content (Join-Path $ThuMucPid 'dinh-danh.pid') -Raw -ErrorAction SilentlyContinue)".Trim()
        $kiemThu = ($pidDd -eq "$($cheDo.cheDoKiemThuPid)")
    }
    if ($kiemThu) { $lenh += " -CheDoKiemThu" }
    $lenh += " *>> " + (Nhay $TepLog)
    if ($cheDo -and "$($cheDo.moiTruong)".Trim()) { $lenh = "`$env:SMARTPORT_MOI_TRUONG = " + (Nhay "$($cheDo.moiTruong)") + "; " + $lenh }
    $maHoa = [Convert]::ToBase64String([System.Text.Encoding]::Unicode.GetBytes($lenh))
    Ghi "CHAY LAI: $($ds -join ', ')$(if ($cheDo -and $cheDo.choPhepLan) { ' (giu mang noi bo HTTPS)' })" "Yellow"
    try {
        $p = Start-Process -FilePath $Pwsh -ArgumentList @("-NoProfile", "-ExecutionPolicy", "Bypass", "-EncodedCommand", $maHoa) -WindowStyle Hidden -PassThru
        if ($p.WaitForExit(600 * 1000)) { Ghi "Chay lai xong (ma thoat $($p.ExitCode)): $($ds -join ', ')" }
        else { Ghi "Chay lai qua 10 phut chua xong - de no chay tiep, vong sau kiem lai" "Red" }
    } catch {
        Ghi "Khong chay lai duoc: $($_.Exception.Message)" "Red"
    }
    foreach ($ten in $ds) { $st = LayTrangThai $ten; $st.SoLanMat = 0; $st.SoLanKhongTraLoi = 0; $st.TinhTrang = "" }
}

function KiemRabbitMQ {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { DoiTinhTrang "rabbitmq" "khong co Docker -> he thong chay suy giam" "Yellow"; return }
    $r = GoiDocker @("info", "--format", "{{.ServerVersion}}") 30
    if ($r.Ma -ne 0) {
        DoiTinhTrang "rabbitmq" "Docker Desktop KHONG chay -> he thong chay suy giam (su kien cho trong Outbox)" "Yellow"
        if (DuocChayLai "docker") {
            Ghi "Bat Docker Desktop..." "Yellow"
            $r2 = GoiDocker @("desktop", "start") 180
            if ($r2.Ma -ne 0) {
                $exe = Join-Path $env:ProgramFiles "Docker\Docker\Docker Desktop.exe"
                if (Test-Path $exe) { Start-Process $exe } else { Ghi "  khong bat duoc Docker Desktop: $(DongCuoi ($r2.Loi + "`n" + $r2.Ra))" "Red" }
            }
        }
        return
    }
    DatLaiGioiHan "docker"
    $k = LayTrangThai "rabbitmq"
    $tt = GoiDocker @("inspect", "--format", "{{.State.Status}}|{{if .State.Health}}{{.State.Health.Status}}{{end}}", "smartport-rabbitmq") 20
    if ($tt.Ma -ne 0) {
        DoiTinhTrang "rabbitmq" "chua co container smartport-rabbitmq" "Yellow"
        if (DuocChayLai "rabbitmq") {
            $cheDo = DocCheDo
            if ($cheDo -and "$($cheDo.anhRabbitMQ)".Trim()) { $env:SMARTPORT_ANH_RABBITMQ = "$($cheDo.anhRabbitMQ)".Trim() }
            Ghi "docker compose up -d (RabbitMQ)" "Yellow"
            $r3 = GoiDocker @("compose", "-f", $TepCompose, "up", "-d") 240
            if ($r3.Ma -ne 0) { Ghi "  loi: $(DongCuoi ($r3.Loi + "`n" + $r3.Ra)) - thieu image? chay 'SMARTPORT.bat chay' de nap tu goi offline" "Red" }
        }
        return
    }
    $tach = "$($tt.Ra)" -split '\|'
    $ttContainer = $tach[0]; $suc = if ($tach.Count -gt 1) { $tach[1] } else { "" }
    if ($ttContainer -ne "running") {
        DoiTinhTrang "rabbitmq" "container dang '$ttContainer' -> he thong chay suy giam" "Yellow"
        if (DuocChayLai "rabbitmq") {
            Ghi "docker start smartport-rabbitmq" "Yellow"
            $r4 = GoiDocker @("start", "smartport-rabbitmq") 120
            if ($r4.Ma -ne 0) { Ghi "  loi: $(DongCuoi ($r4.Loi + "`n" + $r4.Ra))" "Red" }
        }
        return
    }
    if ($suc -eq "unhealthy") {
        $k.SoLanKhongTraLoi++
        DoiTinhTrang "rabbitmq" "container chay nhung UNHEALTHY" "Yellow"
        if ($k.SoLanKhongTraLoi -ge 3 -and (DuocChayLai "rabbitmq")) {
            Ghi "docker restart smartport-rabbitmq" "Yellow"
            $k.SoLanKhongTraLoi = 0
            $r5 = GoiDocker @("restart", "smartport-rabbitmq") 180
            if ($r5.Ma -ne 0) { Ghi "  loi: $(DongCuoi ($r5.Loi + "`n" + $r5.Ra))" "Red" }
        }
        return
    }
    $k.SoLanKhongTraLoi = 0
    if ($suc -eq "healthy") { DatLaiGioiHan "rabbitmq" }
    DoiTinhTrang "rabbitmq" "ok ($(if ($suc) { $suc } else { 'running' }))" "Green"
}

# Moi vong: kiem moi dich vu, chay lai cai chet (co gioi han), roi kiem RabbitMQ
$script:DaBaoTamNgung = $false
function Vong {
    if (DangKhoiDong) {
        if (-not $script:DaBaoTamNgung) { Ghi "chay_microservices.ps1 dang khoi dong / chay lai -> tam ngung kiem"; $script:DaBaoTamNgung = $true }
        return
    }
    $script:DaBaoTamNgung = $false
    $canChayLai = @()
    $coDichVuChay = $false
    foreach ($dv in DocDanhMuc) {
        $st = LayTrangThai $dv.Ten
        $tepPid = Join-Path $ThuMucPid "$($dv.Ten).pid"
        if (-not (Test-Path $tepPid)) {
            $st.SoLanMat = 0; $st.SoLanKhongTraLoi = 0
            DoiTinhTrang $dv.Ten "khong giam sat (chua chay / da tat co chu dich)"
            continue
        }
        $coDichVuChay = $true
        $f = Get-Item $tepPid
        $id = 0
        [void][int]::TryParse((Get-Content $tepPid -Raw -ErrorAction SilentlyContinue), [ref]$id)
        if (-not ($id -gt 0 -and (LaTienTrinhCuaTa $id $f.LastWriteTime $dv.Ten))) {
            $st.SoLanMat++; $st.SoLanKhongTraLoi = 0
            DoiTinhTrang $dv.Ten "tien trinh (pid $id) KHONG CON" "Yellow"
            if ($st.SoLanMat -ge 2) { $canChayLai += $dv.Ten }     # 2 vong lien tiep: tranh nham luc dang tat / chay lai
            continue
        }
        $st.SoLanMat = 0
        $tuoi = ((Get-Date) - $f.LastWriteTime).TotalSeconds
        $ma = KiemSucKhoe $dv.Cong
        if ($ma -gt 0) {
            $st.SoLanKhongTraLoi = 0
            if ($tuoi -gt $CuaSoPhut * 60) { DatLaiGioiHan $dv.Ten }     # da chay on dinh qua 1 cua so
            if ($ma -eq 200) { DoiTinhTrang $dv.Ten "ok" "Green" }
            elseif ($ma -eq 503) { DoiTinhTrang $dv.Ten "tra loi 503 (CSDL loi?) - KHONG chay lai, kiem SQL Server" "Yellow" }
            else { DoiTinhTrang $dv.Ten "tra loi HTTP $ma" "Yellow" }
            continue
        }
        if ($tuoi -lt $dv.AnHan) { DoiTinhTrang $dv.Ten "dang khoi dong"; continue }
        $st.SoLanKhongTraLoi++
        DoiTinhTrang $dv.Ten "tien trinh con nhung KHONG tra loi /suc-khoe" "Yellow"
        if ($st.SoLanKhongTraLoi -ge 3) { $canChayLai += $dv.Ten }
    }
    $duocChay = @($canChayLai | Where-Object { DuocChayLai $_ })
    if ($duocChay.Count -gt 0) { ChayLai $duocChay }
    if ($coDichVuChay) { KiemRabbitMQ }     # he thong da tat co chu dich -> khong bat lai RabbitMQ
}

# ---------------------------------------------------------------- chi 1 ban giam sat
$mutex = [System.Threading.Mutex]::new($false, "Local\SmartPort-GiamSat")
$coMutex = $false
try { $coMutex = $mutex.WaitOne(0) } catch [System.Threading.AbandonedMutexException] { $coMutex = $true }
if (-not $coMutex) { Write-Host "Da co mot ban giam sat SmartPort dang chay - thoat." -ForegroundColor Yellow; exit 0 }

XoayLog "giam-sat"
Remove-Item $TepDung -Force -ErrorAction SilentlyContinue      # yeu cau dung cu (cua lan tat truoc) khong tinh
Set-Content -Path $TepKhoa -Value "$PID"
$TepBiMat = Join-Path $Goc ".bimat\bi-mat.json"
if (Test-Path $TepBiMat) { try { $env:RABBITMQ_MAT_KHAU = (Get-Content $TepBiMat -Raw | ConvertFrom-Json).rabbitMatKhau } catch { } }
Ghi "=== BAT DAU GIAM SAT (pid $PID, moi $ChuKyGiay giay, toi da $ToiDaLan lan chay lai / $CuaSoPhut phut / dich vu) ===" "Cyan"
$demVong = 0
try {
    while ($true) {
        if (Test-Path $TepDung) { Ghi "Nhan yeu cau dung (SMARTPORT.bat tat) -> ket thuc giam sat" "Cyan"; break }
        try { Vong } catch { Ghi "Loi vong giam sat: $($_.Exception.Message)" "Red" }
        $demVong++
        if ($demVong % [Math]::Max(1, [int](600 / $ChuKyGiay)) -eq 0) {      # ~10 phut 1 dong tom tat
            $ok = @($script:TrangThai.Keys | Where-Object { $script:TrangThai[$_].TinhTrang -like "ok*" }).Count
            Ghi "Tom tat: $ok doi tuong dang ok; $(@($script:TrangThai.Keys | Where-Object { $script:TrangThai[$_].BiKhoa }).Count) bi ngung tu chay lai"
        }
        Start-Sleep -Seconds $ChuKyGiay
    }
} finally {
    try { if ((Get-Content $TepKhoa -Raw -ErrorAction Stop).Trim() -eq "$PID") { Remove-Item $TepKhoa -Force } } catch { }
    Remove-Item $TepDung -Force -ErrorAction SilentlyContinue
    if ($coMutex) { try { $mutex.ReleaseMutex() } catch { } }
    $mutex.Dispose()
}
