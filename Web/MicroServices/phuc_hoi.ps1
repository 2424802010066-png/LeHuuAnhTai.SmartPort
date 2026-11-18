<#
  PHUC HOI SMARTPORT TU BAN SAO LUU (tao boi sao_luu.ps1):
    - CHI phuc hoi CSDL SmartPortMS_* (co trong dich-vu.json VA co tep csdl/<ten>.bak trong ban sao luu).
      KHONG dung toi SmartPortDB / SmartPortDB_BaoCao / CSDL khac.
    - Truoc khi ghi de, kiem tung tep: RESTORE VERIFYONLY WITH CHECKSUM + ten CSDL ghi trong tep (RESTORE HEADERONLY)
      phai DUNG ten CSDL dich -> khong the phuc hoi nham tep cua CSDL khac.
    - HOI XAC NHAN (go dung chu PHUC HOI) truoc khi ghi de.
    - Sao luu AN TOAN ban hien tai vao sao-luu/truoc-phuc-hoi_<gio>/ (COPY_ONLY) truoc khi ghi de -> chon nham ban van lay lai duoc.
    - He thong phai dang TAT (SMARTPORT.bat tat): khong dich vu / giam sat nao dang ghi CSDL.
    - Tep Uploads / .bimat: KHONG tu ghi de, chi in lenh giai nen de lam bang tay neu can.
  Dung:  pwsh MicroServices/phuc_hoi.ps1                                 (liet ke cac ban sao luu, chon so)
         pwsh MicroServices/phuc_hoi.ps1 -Ban 20261014_233000            (ten thu muc trong sao-luu/ hoac duong dan day du)
         pwsh MicroServices/phuc_hoi.ps1 -Ban 20261014_233000 -Chi cong,tai-chinh   (chi vai dich vu - ban sao giua dich vu co the lech)
#>
param([string]$Ban, [string[]]$Chi, [string]$MayChuSql = ".\MSSQLSERVER01")
$ErrorActionPreference = "Stop"
if ($Chi) { $Chi = @($Chi | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ }) }
$Goc = $PSScriptRoot
$Web = Split-Path -Parent $Goc
$ThuMucSaoLuu = Join-Path $Goc "sao-luu"
$ThuMucPid = Join-Path $Goc "pid"

# ---------------------------------------------------------------- ham phu
function MoKetNoi {
    $cn = [System.Data.SqlClient.SqlConnection]::new("Data Source=$MayChuSql;Initial Catalog=master;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=15")
    $cn.Open()
    return $cn
}
function TaoLenh($cn, [string]$sql, [hashtable]$thamSo, [int]$giay) {
    $cmd = $cn.CreateCommand()
    $cmd.CommandText = $sql
    $cmd.CommandTimeout = $giay
    foreach ($k in $thamSo.Keys) { [void]$cmd.Parameters.AddWithValue($k, $thamSo[$k]) }
    return $cmd
}
function ChaySql($cn, [string]$sql, [hashtable]$thamSo = @{}, [int]$giay = 3600) {
    $cmd = TaoLenh $cn $sql $thamSo $giay
    try { return $cmd.ExecuteScalar() } finally { $cmd.Dispose() }
}
function DocBang($cn, [string]$sql, [hashtable]$thamSo = @{}) {
    $cmd = TaoLenh $cn $sql $thamSo 600
    $ds = [System.Collections.Generic.List[object]]::new()
    $r = $cmd.ExecuteReader()
    try {
        while ($r.Read()) {
            $h = [ordered]@{}
            for ($i = 0; $i -lt $r.FieldCount; $i++) { $h[$r.GetName($i)] = $r.GetValue($i) }
            $ds.Add([pscustomobject]$h)
        }
    } finally { $r.Dispose(); $cmd.Dispose() }
    return , $ds.ToArray()
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
function CapQuyen([string]$thuMuc, [string]$tk) {
    $ra = & icacls $thuMuc /grant "${tk}:(OI)(CI)M" 2>&1
    if ($LASTEXITCODE -ne 0) { Write-Host "  Canh bao: khong cap duoc quyen cho $tk tren $thuMuc ($ra)" -ForegroundColor Yellow }
}
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
function SaoLuuAnToan($cn, [string]$ten, [string]$tep) {
    try { [void](ChaySql $cn "BACKUP DATABASE [$ten] TO DISK = @tep WITH COPY_ONLY, COMPRESSION, CHECKSUM, INIT" @{ "@tep" = $tep }) }
    catch {
        if ("$($_.Exception.Message)" -match 'COMPRESSION') { [void](ChaySql $cn "BACKUP DATABASE [$ten] TO DISK = @tep WITH COPY_ONLY, CHECKSUM, INIT" @{ "@tep" = $tep }) }
        else { throw }
    }
}

# ---------------------------------------------------------------- 1. CSDL duoc phep phuc hoi (CHI SmartPortMS_*)
$DanhMuc = Get-Content (Join-Path $Web "dich-vu.json") -Raw | ConvertFrom-Json
$CsdlTheoDv = [ordered]@{}
foreach ($dv in $DanhMuc.dichVu) {
    $ten = "$($dv.coSoDuLieu)".Trim()
    if ($ten -match '^SmartPortMS_[A-Za-z0-9_]+$') { $CsdlTheoDv[$dv.ten] = $ten }
}
if ($Chi) {
    $loc = [ordered]@{}
    foreach ($c in $Chi) {
        $k = @($CsdlTheoDv.Keys | Where-Object { $_ -eq $c -or $CsdlTheoDv[$_] -eq $c }) | Select-Object -First 1
        if (-not $k) { Write-Host "Khong co dich vu / CSDL SmartPortMS_* ten '$c' trong dich-vu.json" -ForegroundColor Red; exit 1 }
        $loc[$k] = $CsdlTheoDv[$k]
    }
    $CsdlTheoDv = $loc
}

# ---------------------------------------------------------------- 2. chon ban sao luu
if (-not $Ban) {
    # Ban THUONG (yyyyMMdd_HHmmss) moi nhat len dau; ban an toan "truoc-phuc-hoi_*" va ban LOI "*_LOI" xuong cuoi.
    # Truoc day sap theo ten giam dan: "truoc-phuc-hoi_..." ('t' > '2') va "_LOI" luon dung dau -> de chon nham ban cu / thieu.
    $ds = @(Get-ChildItem $ThuMucSaoLuu -Directory -ErrorAction SilentlyContinue | Where-Object { Test-Path (Join-Path $_.FullName "csdl") } |
            Sort-Object @{ Expression = { if ($_.Name -match '^\d{8}_\d{6}$') { 0 } elseif ($_.Name -like 'truoc-phuc-hoi_*') { 1 } else { 2 } } },
                        @{ Expression = { ($_.Name -replace '^truoc-phuc-hoi_', '') }; Descending = $true })
    if ($ds.Count -eq 0) { Write-Host "Chua co ban sao luu nao trong $ThuMucSaoLuu (tao bang: SMARTPORT.bat sao-luu)" -ForegroundColor Yellow; exit 1 }
    Write-Host "CAC BAN SAO LUU (ban thuong moi nhat truoc; ban an toan truoc-phuc-hoi va ban LOI o cuoi):" -ForegroundColor Cyan
    for ($i = 0; $i -lt $ds.Count; $i++) {
        $tt = "?"
        $kq = Join-Path $ds[$i].FullName "ket-qua.json"
        if (Test-Path $kq) { try { $tt = (Get-Content $kq -Raw | ConvertFrom-Json).trangThai } catch { } }
        $soBak = @(Get-ChildItem (Join-Path $ds[$i].FullName "csdl") -Filter *.bak -File).Count
        $nhan = if ($ds[$i].Name -like 'truoc-phuc-hoi_*') { "(ban an toan truoc lan phuc hoi)" } elseif ($ds[$i].Name -like '*_LOI') { "(LOI - co the thieu CSDL)" } else { "" }
        Write-Host ("  {0,2}. {1,-34} {2,-4} {3,2} tep .bak {4}" -f ($i + 1), $ds[$i].Name, $tt, $soBak, $nhan)
    }
    $chon = Read-Host "Chon so ban muon phuc hoi (Enter = thoat)"
    $so = 0
    if (-not [int]::TryParse("$chon", [ref]$so) -or $so -lt 1 -or $so -gt $ds.Count) { Write-Host "Khong phuc hoi gi."; exit 0 }
    $ThuMucBan = $ds[$so - 1].FullName
} elseif (Test-Path $Ban -PathType Container) {
    $ThuMucBan = (Resolve-Path $Ban).Path
} else {
    $ThuMucBan = Join-Path $ThuMucSaoLuu $Ban
}
$ThuMucCsdl = Join-Path $ThuMucBan "csdl"
if (-not (Test-Path $ThuMucCsdl)) { Write-Host "Khong thay thu muc csdl\ trong ban sao luu: $ThuMucBan" -ForegroundColor Red; exit 1 }
$kqBan = $null
if (Test-Path (Join-Path $ThuMucBan "ket-qua.json")) { try { $kqBan = Get-Content (Join-Path $ThuMucBan "ket-qua.json") -Raw | ConvertFrom-Json } catch { } }

$canPhucHoi = @()
foreach ($dv in $CsdlTheoDv.Keys) {
    $tep = Join-Path $ThuMucCsdl "$($CsdlTheoDv[$dv]).bak"
    if (Test-Path $tep) { $canPhucHoi += [pscustomobject]@{ DichVu = $dv; Csdl = $CsdlTheoDv[$dv]; Tep = $tep } }
    else { Write-Host "  Canh bao: ban sao luu KHONG co $($CsdlTheoDv[$dv]).bak -> CSDL nay giu nguyen" -ForegroundColor Yellow }
}
if ($canPhucHoi.Count -eq 0) { Write-Host "Khong co tep .bak nao hop le de phuc hoi" -ForegroundColor Red; exit 1 }

# ---------------------------------------------------------------- 3. he thong phai dang tat
$dangChay = @()
if (Test-Path $ThuMucPid) {
    foreach ($f in Get-ChildItem $ThuMucPid -Filter *.pid) {
        $id = 0
        [void][int]::TryParse((Get-Content $f.FullName -Raw), [ref]$id)
        if ($id -gt 0 -and (LaTienTrinhCuaTa $id $f.LastWriteTime $f.BaseName)) { $dangChay += $f.BaseName }
    }
    $khoaGs = Join-Path $ThuMucPid "giam-sat.khoa"
    if (Test-Path $khoaGs) {
        $idGs = 0
        [void][int]::TryParse((Get-Content $khoaGs -Raw), [ref]$idGs)
        $pGs = if ($idGs -gt 0) { Get-Process -Id $idGs -ErrorAction SilentlyContinue } else { $null }
        if ($pGs -and $pGs.ProcessName -like "pwsh*") { $dangChay += "giam-sat" }
    }
}
if ($dangChay.Count -gt 0) {
    Write-Host "He thong DANG CHAY ($($dangChay -join ', ')) -> tat truoc: SMARTPORT.bat tat   roi chay lai phuc-hoi." -ForegroundColor Red
    exit 1
}

# ---------------------------------------------------------------- 4. kiem tep + xac nhan
$cn = MoKetNoi
try {
    $tkSql = TaiKhoanDichVuSql $cn
    CapQuyen $ThuMucCsdl $tkSql          # SQL Server doc tep .bak bang tai khoan dich vu
    Write-Host "Kiem tra ban sao luu: $ThuMucBan" -ForegroundColor Cyan
    foreach ($m in $canPhucHoi) {
        $dau = DocBang $cn "RESTORE HEADERONLY FROM DISK = @tep" @{ "@tep" = $m.Tep }
        if ($dau.Count -lt 1) { throw "$($m.Tep): khong doc duoc header" }
        if ("$($dau[0].DatabaseName)" -ne $m.Csdl) { throw "$($m.Tep) la sao luu cua CSDL '$($dau[0].DatabaseName)', KHONG phai $($m.Csdl) -> dung lai" }
        [void](ChaySql $cn "RESTORE VERIFYONLY FROM DISK = @tep WITH CHECKSUM" @{ "@tep" = $m.Tep })
        $m | Add-Member -NotePropertyName LucSaoLuu -NotePropertyValue $dau[0].BackupFinishDate
        $m | Add-Member -NotePropertyName DangCo -NotePropertyValue ([int](ChaySql $cn "SELECT CASE WHEN DB_ID(@ten) IS NULL THEN 0 ELSE 1 END" @{ "@ten" = $m.Csdl }) -eq 1)
        Write-Host ("  {0,-24} sao luu luc {1}  VERIFYONLY DAT  (hien {2})" -f $m.Csdl, $m.LucSaoLuu, $(if ($m.DangCo) { "dang co -> se GHI DE" } else { "chua co -> tao moi" }))
    }
    Write-Host ""
    Write-Host "SE PHUC HOI $($canPhucHoi.Count) CSDL tu ban $(Split-Path $ThuMucBan -Leaf)$(if ($kqBan) { " (trang thai luc sao luu: $($kqBan.trangThai))" })." -ForegroundColor Yellow
    Write-Host "DU LIEU HIEN TAI cua cac CSDL tren se bi THAY bang du lieu luc sao luu (truoc do se tu sao luu an toan ban hien tai)." -ForegroundColor Yellow
    if ($Chi) { Write-Host "Chi phuc hoi mot phan (-Chi): bang BAN SAO o dich vu khac co the lech - nen phuc hoi TOAN BO tu cung 1 ban." -ForegroundColor Yellow }
    $xn = Read-Host "Go dung chu PHUC HOI de tiep tuc (khac = huy)"
    if ("$xn".Trim() -ne "PHUC HOI") { Write-Host "Da huy, khong thay doi gi."; exit 2 }

    # ------------------------------------------------------------ 5. sao luu an toan ban hien tai
    # Cung cau truc ban sao luu (csdl\<ten>.bak) -> chon nham ban thi phuc hoi lai bang: -Ban truoc-phuc-hoi_<gio>
    $moc = Get-Date -Format "yyyyMMdd_HHmmss"
    $ThuMucAnToan = Join-Path $ThuMucSaoLuu "truoc-phuc-hoi_$moc"
    $ThuMucAnToanCsdl = Join-Path $ThuMucAnToan "csdl"
    $coDangCo = @($canPhucHoi | Where-Object { $_.DangCo })
    if ($coDangCo.Count -gt 0) {
        New-Item -ItemType Directory -Force $ThuMucAnToanCsdl | Out-Null
        CapQuyen $ThuMucAnToanCsdl $tkSql
        Write-Host "Sao luu an toan ban hien tai -> $ThuMucAnToan"
        foreach ($m in $coDangCo) {
            try { SaoLuuAnToan $cn $m.Csdl (Join-Path $ThuMucAnToanCsdl "$($m.Csdl).bak"); Write-Host "  $($m.Csdl) DAT" }
            catch { throw "Khong sao luu an toan duoc $($m.Csdl): $($_.Exception.GetBaseException().Message) -> DUNG, chua ghi de gi" }
        }
        [ordered]@{ luc = (Get-Date).ToString("s"); trangThai = "AN-TOAN"; ghiChu = "Ban hien tai truoc khi phuc hoi tu $(Split-Path $ThuMucBan -Leaf)" } |
            ConvertTo-Json | Set-Content -Encoding utf8 (Join-Path $ThuMucAnToan "ket-qua.json")
    }

    # ------------------------------------------------------------ 6. phuc hoi
    $duongDuLieu = "$(ChaySql $cn "SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(400))")"
    $duongLog = "$(ChaySql $cn "SELECT CAST(SERVERPROPERTY('InstanceDefaultLogPath') AS nvarchar(400))")"
    $soDat = 0; $loi = @()
    foreach ($m in $canPhucHoi) {
        Write-Host -NoNewline ("  {0,-24} RESTORE..." -f $m.Csdl)
        $daDonNguoi = $false
        try {
            # Vi tri tep: CSDL dang co -> dung cho cu theo ten logic; chua co -> thu muc mac dinh cua SQL Server
            $tepCu = @{}
            if ($m.DangCo) {
                foreach ($x in (DocBang $cn "SELECT name, physical_name FROM sys.master_files WHERE database_id = DB_ID(@ten)" @{ "@ten" = $m.Csdl })) { $tepCu["$($x.name)"] = "$($x.physical_name)" }
            }
            $thamSo = @{ "@tep" = $m.Tep }
            $move = @()
            $i = 0
            foreach ($t in (DocBang $cn "RESTORE FILELISTONLY FROM DISK = @tep" @{ "@tep" = $m.Tep })) {
                $logic = "$($t.LogicalName)"
                $dich = if ($tepCu.ContainsKey($logic)) { $tepCu[$logic] }
                        elseif ("$($t.Type)" -eq "L") { Join-Path $duongLog "$($m.Csdl)_$logic.ldf" }
                        else { Join-Path $duongDuLieu "$($m.Csdl)_$logic$(if ($i -eq 0) { '.mdf' } else { '.ndf' })" }
                $thamSo["@l$i"] = $logic; $thamSo["@p$i"] = $dich
                $move += "MOVE @l$i TO @p$i"
                $i++
            }
            if ($m.DangCo) {
                [void](ChaySql $cn "ALTER DATABASE [$($m.Csdl)] SET SINGLE_USER WITH ROLLBACK IMMEDIATE")
                $daDonNguoi = $true
            }
            [void](ChaySql $cn ("RESTORE DATABASE [$($m.Csdl)] FROM DISK = @tep WITH REPLACE, CHECKSUM, RECOVERY, " + ($move -join ", ")) $thamSo)
            $soDat++
            Write-Host " DAT" -ForegroundColor Green
        } catch {
            $loi += "$($m.Csdl): $($_.Exception.GetBaseException().Message)"
            Write-Host " LOI: $($_.Exception.GetBaseException().Message)" -ForegroundColor Red
        } finally {
            if ($daDonNguoi -or -not $m.DangCo) {
                try { [void](ChaySql $cn "IF DB_ID(@ten) IS NOT NULL ALTER DATABASE [$($m.Csdl)] SET MULTI_USER" @{ "@ten" = $m.Csdl }) } catch { }
            }
        }
    }
} catch {
    Write-Host ""
    Write-Host "PHUC HOI DUNG LAI: $($_.Exception.GetBaseException().Message)" -ForegroundColor Red
    exit 1
} finally { $cn.Dispose() }

Write-Host ""
if ($loi.Count -gt 0) {
    Write-Host "PHUC HOI CHUA DAT: $soDat/$($canPhucHoi.Count) CSDL. Loi:" -ForegroundColor Red
    $loi | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    if ($coDangCo.Count -gt 0) { Write-Host "Ban truoc khi phuc hoi van con: $ThuMucAnToan (quay lai: phuc_hoi.ps1 -Ban $(Split-Path $ThuMucAnToan -Leaf))" -ForegroundColor Yellow }
    exit 1
}
Write-Host "PHUC HOI DAT: $soDat/$($canPhucHoi.Count) CSDL tu ban $(Split-Path $ThuMucBan -Leaf)." -ForegroundColor Green
if ($coDangCo.Count -gt 0) { Write-Host "Ban hien tai truoc khi phuc hoi da duoc giu o: $ThuMucAnToan" }
Write-Host "Tep (KHONG tu ghi de - lam bang tay neu can):"
Write-Host "  Anh / video: Expand-Archive -Path '$ThuMucBan\uploads.zip' -DestinationPath '$Web\SmartPort.Api\wwwroot\Uploads'   (them -Force de ghi de tep trung ten)"
Write-Host "  Bi mat (chi khi mat .bimat): Expand-Archive -Path '$ThuMucBan\bimat.zip' -DestinationPath '$Goc\.bimat'"
Write-Host "Chay lai he thong: SMARTPORT.bat chay"
exit 0
