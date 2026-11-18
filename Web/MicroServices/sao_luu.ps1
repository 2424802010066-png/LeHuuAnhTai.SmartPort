<#
  SAO LUU SMARTPORT (chay duoc khi he thong DANG chay - BACKUP cua SQL Server la truc tuyen):
    1. BACKUP DATABASE ... WITH COMPRESSION, CHECKSUM cho MOI CSDL SmartPortMS_* trong dich-vu.json
       (KHONG dung toi SmartPortDB / SmartPortDB_BaoCao / SmartPortDB_KiemThu / CSDL somee: chi nhan ten khop ^SmartPortMS_)
    2. RESTORE VERIFYONLY ... WITH CHECKSUM tung tep .bak (sao luu doc lai duoc moi tinh la co)
    3. Nen SmartPort.Api/wwwroot/Uploads (anh cong, video su co...) + MicroServices/.bimat (khoa JWT, mat khau...) thanh .zip
    4. Ghi ket-qua.json; tat ca DAT moi xoa ban cu: giu $GiuLai ban moi nhat trong MicroServices/sao-luu/<yyyyMMdd_HHmmss>/
       (ban LOI doi ten <gio>_LOI, khong tinh vao so ban giu, khong lam mat ban tot cu)
    5. -ChepSang D:\SaoLuu : chep them ca thu muc sang o dia / USB khac (cung giu $GiuLai ban)
  Quyen: SQL Server ghi tep .bak bang tai khoan dich vu (NT Service\MSSQL$<instance>) -> script cap quyen Modify CHI cho
         thu muc csdl/ cua ban sao luu nay (icacls, thu muc cua chinh ban, khong can admin).
  Bao mat: bimat.zip chua bi mat he thong -> KHONG dua len git / chia se; chep ra USB thi giu kin.
  Dung:  pwsh MicroServices/sao_luu.ps1 [-ChepSang <thu muc>] [-GiuLai 7] [-InLenhLich]
  Phuc hoi: pwsh MicroServices/phuc_hoi.ps1 -Ban <yyyyMMdd_HHmmss>
#>
param([string]$ChepSang, [int]$GiuLai = 7, [string]$MayChuSql = ".\MSSQLSERVER01", [switch]$InLenhLich)
$ErrorActionPreference = "Stop"
$Goc = $PSScriptRoot
$Web = Split-Path -Parent $Goc
$ThuMucSaoLuu = Join-Path $Goc "sao-luu"
$MauBan = '^\d{8}_\d{6}$'

# ---------------------------------------------------------------- chi in lenh Task Scheduler
if ($InLenhLich) {
    $pw = Join-Path $env:LOCALAPPDATA "Microsoft\WindowsApps\pwsh.exe"
    if (-not (Test-Path $pw)) { $pw = (Get-Command pwsh -ErrorAction SilentlyContinue).Source }
    $tep = Join-Path $Goc "sao_luu.ps1"
    Write-Host "LENH DANG KY SAO LUU HANG NGAY (script KHONG tu dang ky - tu chay trong PowerShell neu muon):" -ForegroundColor Cyan
    Write-Host "`$tacVu = New-ScheduledTaskAction -Execute '$pw' -Argument '-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$tep`"'"
    Write-Host "`$kichHoat = New-ScheduledTaskTrigger -Daily -At 23:30"
    Write-Host "`$caiDat = New-ScheduledTaskSettingsSet -StartWhenAvailable -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries"
    Write-Host "Register-ScheduledTask -TaskName 'SmartPort - Sao luu hang ngay' -Action `$tacVu -Trigger `$kichHoat -Settings `$caiDat"
    Write-Host "# (StartWhenAvailable: may tat / ngu luc 23:30 thi sao luu ngay khi bat lai)"
    Write-Host "# Go bo: Unregister-ScheduledTask -TaskName 'SmartPort - Sao luu hang ngay' -Confirm:`$false"
    exit 0
}

# ---------------------------------------------------------------- ham phu
function MoKetNoi {
    $cn = [System.Data.SqlClient.SqlConnection]::new("Data Source=$MayChuSql;Initial Catalog=master;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=15")
    $cn.Open()
    return $cn
}
function ChaySql($cn, [string]$sql, [hashtable]$thamSo = @{}, [int]$giay = 3600) {
    $cmd = $cn.CreateCommand()
    $cmd.CommandText = $sql
    $cmd.CommandTimeout = $giay
    foreach ($k in $thamSo.Keys) { [void]$cmd.Parameters.AddWithValue($k, $thamSo[$k]) }
    try { return $cmd.ExecuteScalar() } finally { $cmd.Dispose() }
}
function TaiKhoanDichVuSql($cn) {
    try {
        $tk = ChaySql $cn "SELECT TOP 1 service_account FROM sys.dm_server_services WHERE servicename LIKE N'SQL Server (%'"
        if ("$tk".Trim()) { return "$tk".Trim() }
    } catch { }
    $inst = if ($MayChuSql -match '\\(.+)$') { $Matches[1] } else { "MSSQLSERVER" }
    if ($inst -eq "MSSQLSERVER") { return "NT Service\MSSQLSERVER" }
    return ('NT Service\MSSQL$' + $inst)
}
function NenThuMuc([string]$nguon, [string]$tepZip) {
    # Tung tep mot: tep dang bi khoa (dang ghi) thi bo qua va bao, khong lam hong ca ban sao luu
    Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
    $soTep = 0; $boQua = @()
    $zip = [System.IO.Compression.ZipFile]::Open($tepZip, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        if (Test-Path $nguon) {
            $goc = (Resolve-Path $nguon).Path.TrimEnd('\') + '\'
            foreach ($f in Get-ChildItem $nguon -Recurse -File -Force) {
                $ten = $f.FullName.Substring($goc.Length) -replace '\\', '/'
                try {
                    [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $f.FullName, $ten, [System.IO.Compression.CompressionLevel]::Optimal)
                    $soTep++
                } catch { $boQua += $ten }
            }
        }
    } finally { $zip.Dispose() }
    return [pscustomobject]@{ soTep = $soTep; boQua = $boQua }
}
function XoaBanCu([string]$thuMuc, [int]$giu) {
    if (-not (Test-Path $thuMuc)) { return }
    $ds = @(Get-ChildItem $thuMuc -Directory | Where-Object { $_.Name -match $MauBan } | Sort-Object Name -Descending)
    foreach ($d in ($ds | Select-Object -Skip $giu)) {
        Remove-Item $d.FullName -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "  Xoa ban cu: $($d.FullName)"
    }
}

# ---------------------------------------------------------------- danh sach CSDL (CHI SmartPortMS_*)
$DanhMuc = Get-Content (Join-Path $Web "dich-vu.json") -Raw | ConvertFrom-Json
$dsCsdl = @($DanhMuc.dichVu | Where-Object { $_.coSoDuLieu } | ForEach-Object { "$($_.coSoDuLieu)".Trim() } |
            Where-Object { $_ -match '^SmartPortMS_[A-Za-z0-9_]+$' } | Select-Object -Unique)
if ($dsCsdl.Count -eq 0) { Write-Host "dich-vu.json khong co CSDL SmartPortMS_* nao" -ForegroundColor Red; exit 1 }

$moc = Get-Date -Format "yyyyMMdd_HHmmss"
$ThuMucBan = Join-Path $ThuMucSaoLuu $moc
$ThuMucCsdl = Join-Path $ThuMucBan "csdl"
New-Item -ItemType Directory -Force $ThuMucCsdl | Out-Null
Write-Host "Sao luu SmartPort -> $ThuMucBan" -ForegroundColor Cyan
$ketQua = [ordered]@{ luc = (Get-Date).ToString("s"); may = $env:COMPUTERNAME; mayChuSql = $MayChuSql; csdl = @(); uploads = $null; bimat = $null; trangThai = "LOI"; loi = @() }

# ---------------------------------------------------------------- 1 + 2. CSDL
$cn = $null
try {
    $cn = MoKetNoi
    $tkSql = TaiKhoanDichVuSql $cn
    $ra = & icacls $ThuMucCsdl /grant "${tkSql}:(OI)(CI)M" 2>&1
    if ($LASTEXITCODE -ne 0) { Write-Host "  Canh bao: khong cap duoc quyen ghi cho $tkSql ($ra) - BACKUP co the bao 'Access is denied'" -ForegroundColor Yellow }
    foreach ($ten in $dsCsdl) {
        $muc = [ordered]@{ ten = $ten; tep = "csdl/$ten.bak"; kichThuocMB = 0; verifyOnly = $false; loi = $null }
        $tep = Join-Path $ThuMucCsdl "$ten.bak"
        try {
            $trangThaiDb = ChaySql $cn "SELECT state_desc FROM sys.databases WHERE name = @ten" @{ "@ten" = $ten }
            if (-not $trangThaiDb) { throw "CSDL khong ton tai" }
            if ("$trangThaiDb" -ne "ONLINE") { throw "CSDL dang $trangThaiDb" }
            Write-Host -NoNewline ("  {0,-24} BACKUP..." -f $ten)
            $tenBan = "SmartPort sao luu $moc"
            try {
                [void](ChaySql $cn "BACKUP DATABASE [$ten] TO DISK = @tep WITH COMPRESSION, CHECKSUM, INIT, NAME = @tenBan" @{ "@tep" = $tep; "@tenBan" = $tenBan })
            } catch {
                # ban SQL Server khong ho tro nen (Express) -> sao luu khong nen
                if ("$($_.Exception.Message)" -match 'COMPRESSION') {
                    [void](ChaySql $cn "BACKUP DATABASE [$ten] TO DISK = @tep WITH CHECKSUM, INIT, NAME = @tenBan" @{ "@tep" = $tep; "@tenBan" = $tenBan })
                } else { throw }
            }
            Write-Host -NoNewline " VERIFYONLY..."
            [void](ChaySql $cn "RESTORE VERIFYONLY FROM DISK = @tep WITH CHECKSUM" @{ "@tep" = $tep })
            $muc.verifyOnly = $true
            $muc.kichThuocMB = [math]::Round((Get-Item $tep).Length / 1MB, 2)
            Write-Host " DAT ($($muc.kichThuocMB) MB)" -ForegroundColor Green
        } catch {
            $muc.loi = "$($_.Exception.GetBaseException().Message)"
            $ketQua.loi += "$ten : $($muc.loi)"
            Write-Host ""
            Write-Host ("  {0,-24} LOI: {1}" -f $ten, $muc.loi) -ForegroundColor Red
        }
        $ketQua.csdl += [pscustomobject]$muc
    }
} catch {
    $ketQua.loi += "Khong ket noi duoc SQL Server ${MayChuSql}: $($_.Exception.GetBaseException().Message)"
    Write-Host $ketQua.loi[-1] -ForegroundColor Red
} finally { if ($cn) { $cn.Dispose() } }

# ---------------------------------------------------------------- 3. tep: Uploads + .bimat
try {
    Write-Host -NoNewline "  Uploads (anh / video)        nen..."
    $u = NenThuMuc (Join-Path $Web "SmartPort.Api\wwwroot\Uploads") (Join-Path $ThuMucBan "uploads.zip")
    $ketQua.uploads = [ordered]@{ tep = "uploads.zip"; soTep = $u.soTep; boQua = $u.boQua }
    Write-Host " $($u.soTep) tep$(if ($u.boQua.Count) { ", bo qua $($u.boQua.Count) tep dang bi khoa" })" -ForegroundColor Green
} catch { $ketQua.loi += "Nen Uploads loi: $($_.Exception.Message)"; Write-Host " LOI: $($_.Exception.Message)" -ForegroundColor Red }
try {
    Write-Host -NoNewline "  .bimat (bi mat he thong)     nen..."
    $b = NenThuMuc (Join-Path $Goc ".bimat") (Join-Path $ThuMucBan "bimat.zip")
    $ketQua.bimat = [ordered]@{ tep = "bimat.zip"; soTep = $b.soTep; boQua = $b.boQua }
    if ($b.boQua.Count) { $ketQua.loi += ".bimat: khong doc duoc $($b.boQua -join ', ')" }
    Write-Host " $($b.soTep) tep" -ForegroundColor Green
} catch { $ketQua.loi += "Nen .bimat loi: $($_.Exception.Message)"; Write-Host " LOI: $($_.Exception.Message)" -ForegroundColor Red }

# ---------------------------------------------------------------- 4. ket qua + giu $GiuLai ban
$datHet = ($ketQua.loi.Count -eq 0) -and (@($ketQua.csdl | Where-Object { $_.verifyOnly }).Count -eq $dsCsdl.Count)
$ketQua.trangThai = if ($datHet) { "DAT" } else { "LOI" }
$ketQua | ConvertTo-Json -Depth 5 | Set-Content -Encoding utf8 (Join-Path $ThuMucBan "ket-qua.json")
if (-not $datHet) {
    $tenLoi = Join-Path $ThuMucSaoLuu "${moc}_LOI"
    try { Rename-Item $ThuMucBan $tenLoi -ErrorAction Stop; $ThuMucBan = $tenLoi } catch { }
    Write-Host ""
    Write-Host "SAO LUU CHUA DAT ($($ketQua.loi.Count) loi) - giu nguyen moi ban cu. Chi tiet: $ThuMucBan\ket-qua.json" -ForegroundColor Red
    if ("$($ketQua.loi)" -match 'denied|5\(') {
        Write-Host "Goi y quyen: icacls `"$ThuMucSaoLuu`" /grant `"$(if ($tkSql) { $tkSql } else { 'NT Service\MSSQL$MSSQLSERVER01' }):(OI)(CI)M`"" -ForegroundColor Yellow
    }
    exit 1
}
XoaBanCu $ThuMucSaoLuu $GiuLai
$tong = [math]::Round(((Get-ChildItem $ThuMucBan -Recurse -File | Measure-Object Length -Sum).Sum) / 1MB, 1)
Write-Host ""
Write-Host "SAO LUU DAT: $($dsCsdl.Count) CSDL (VERIFYONLY dat) + Uploads + .bimat = $tong MB -> $ThuMucBan" -ForegroundColor Green

# ---------------------------------------------------------------- 5. chep sang o khac (tuy chon)
if ($ChepSang) {
    try {
        $dichGoc = Join-Path $ChepSang "SmartPort-sao-luu"
        New-Item -ItemType Directory -Force $dichGoc | Out-Null
        Copy-Item $ThuMucBan (Join-Path $dichGoc $moc) -Recurse -Force
        XoaBanCu $dichGoc $GiuLai
        Write-Host "Da chep sang: $(Join-Path $dichGoc $moc)" -ForegroundColor Green
    } catch {
        Write-Host "Chep sang $ChepSang LOI: $($_.Exception.Message) (ban sao luu tren may van DAT)" -ForegroundColor Red
        exit 1
    }
}
Write-Host "Luu y: bimat.zip chua khoa / mat khau he thong - khong dua len git, khong chia se."
Write-Host "Sao luu tu dong hang ngay: pwsh MicroServices/sao_luu.ps1 -InLenhLich (chi in lenh Task Scheduler)"
exit 0
