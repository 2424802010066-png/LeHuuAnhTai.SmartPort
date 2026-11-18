<#
  CHAY HE THONG SMARTPORT THEO KIEN TRUC MICROSERVICES (may phat trien):
    1. Sinh bi mat cuc bo lan dau (khoa JWT CHUNG cho moi dich vu, mat khau RabbitMQ) -> .bimat/bi-mat.json
    2. Bat RabbitMQ bang Docker (docker-compose.yml), doi san sang
       KHONG len duoc (Docker tat, thieu image, khong healthy) -> KHONG dung he thong: bao vang "CHAY SUY GIAM" va
       chay tiep (nghiep vu van chay, su kien nam cho trong Outbox, tu gui khi RabbitMQ len lai).
       Khong bao gio pull image tu mang: thieu image thi "docker load" tu MicroServices/goi/*.tar.
    3. Dung CSDL rieng tung dich vu neu chua co (dung_csdl_dich_vu.py, nguon SmartPortDB_BaoCao chi doc)
    4. Build solution, chay tung dich vu (moi dich vu 1 tien trinh, cong rieng) + cong API gateway (5100)
       Log cu KHONG bi ghi de: doi ten theo gio vao nhat-ky/cu/, giu 10 ban moi dich vu.
    5. Doi /suc-khoe cua tat ca, in bang tinh trang
  Dung:  pwsh MicroServices/chay_microservices.ps1 [-KhongBuild] [-LamLaiCsdl] [-Chi cong,bai] [-ChoPhepLan] [-BoQuaHaTang] [-CheDoKiemThu]
    -ChoPhepLan  : cho may tram trong mang noi bo vao qua HTTPS: gateway nghe THEM https://0.0.0.0:5443 bang chung chi
                   .bimat/chung-chi/may-chu.pfx (tao bang tao_chung_chi.ps1). Khong co tham so nay: nhu cu (chi localhost).
    -BoQuaHaTang : bo qua buoc 2 + 3 (RabbitMQ, CSDL) - giam_sat.ps1 dung khi chay lai 1 dich vu da chet.
    -CheDoKiemThu: CHI cho bo kiem thu (SMARTPORT.bat chay-kiem-thu): dich vu dinh-danh duoc dat AppSettings__SmsDemoTraMa=true
                   -> tra maDemo cua OTP SMS demo, va CHI cho yeu cau tu chinh may chu (loopback). Khong co tham so nay thi
                   script dat ro =false (bien moi truong de len moi appsettings) -> chay that KHONG BAO GIO lo ma OTP.
  Bien moi truong tuy chon: SMARTPORT_MOI_TRUONG (mac dinh Development), SMARTPORT_CONG_HTTPS (mac dinh 5443).
  Tat:   pwsh MicroServices/dung_microservices.ps1
#>
param([switch]$KhongBuild, [switch]$LamLaiCsdl, [string[]]$Chi, [switch]$ChoPhepLan, [switch]$BoQuaHaTang, [switch]$CheDoKiemThu)
$ErrorActionPreference = "Stop"
# "pwsh -File ... -Chi cong,bai" truyen ca "cong,bai" thanh 1 chuoi (khong thanh mang) -> tach theo dau phay
if ($Chi) { $Chi = @($Chi | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ }) }
$Goc = $PSScriptRoot
$Web = Split-Path -Parent $Goc
$ThuMucBiMat = Join-Path $Goc ".bimat"
$ThuMucLog = Join-Path $Goc "nhat-ky"
$ThuMucPid = Join-Path $Goc "pid"
$ThuMucGoi = Join-Path $Goc "goi"
$TepCompose = Join-Path $Goc "docker-compose.yml"
New-Item -ItemType Directory -Force $ThuMucBiMat, $ThuMucLog, $ThuMucPid | Out-Null

# ---------------------------------------------------------------- ham phu
# Goi docker co GIOI HAN THOI GIAN (docker CLI co luc treo khi Docker Desktop dang khoi dong) - khong nem loi.
function GoiDocker([string[]]$ThamSo, [int]$GiayToiDa = 60) {
    $psi = [System.Diagnostics.ProcessStartInfo]::new("docker")
    foreach ($t in $ThamSo) { $psi.ArgumentList.Add($t) }
    $psi.UseShellExecute = $false; $psi.CreateNoWindow = $true
    $psi.RedirectStandardOutput = $true; $psi.RedirectStandardError = $true
    $psi.StandardOutputEncoding = [System.Text.Encoding]::UTF8; $psi.StandardErrorEncoding = [System.Text.Encoding]::UTF8
    try { $p = [System.Diagnostics.Process]::Start($psi) }
    catch { return [pscustomobject]@{ Ma = -1; Ra = ""; Loi = $_.Exception.Message } }
    $ra = $p.StandardOutput.ReadToEndAsync(); $loi = $p.StandardError.ReadToEndAsync()
    if (-not $p.WaitForExit($GiayToiDa * 1000)) {
        try { $p.Kill($true) } catch { }
        return [pscustomobject]@{ Ma = -2; Ra = ""; Loi = "docker $($ThamSo[0]) qua $GiayToiDa giay" }
    }
    $p.WaitForExit()
    return [pscustomobject]@{ Ma = $p.ExitCode; Ra = "$($ra.Result)".Trim(); Loi = "$($loi.Result)".Trim() }
}
function DongCuoi([string]$s) { (@("$s" -split "`r?`n" | Where-Object { $_.Trim() }) | Select-Object -Last 1) }
function CoAnh([string]$anh) { (GoiDocker @("image", "inspect", "--format", "{{.Id}}", $anh) 30).Ma -eq 0 }
function TepGoi([string]$anh) { Join-Path $ThuMucGoi (($anh -replace '[:/\\]', '_') + ".tar") }   # rabbitmq:4.3.6-management -> rabbitmq_4.3.6-management.tar
function PhienBanRabbit([string]$anh) {
    $r = GoiDocker @("image", "inspect", "--format", "{{range .Config.Env}}{{println .}}{{end}}", $anh) 30
    if ($r.Ma -ne 0) { return $null }
    $dong = @($r.Ra -split "`r?`n" | Where-Object { $_ -like "RABBITMQ_VERSION=*" }) | Select-Object -First 1
    if ($dong) { return ($dong -replace '^RABBITMQ_VERSION=', '').Trim() }
    return $null
}

# Image RabbitMQ dung duoc NGAY tren may (khong pull): ban ghim trong docker-compose.yml -> goi offline -> image 4.x cu.
# Tra ve ten image, hoac $null neu khong co. Dung image cu thi dat $env:SMARTPORT_ANH_RABBITMQ cho compose.
function ChonAnhRabbitMQ {
    $m = [regex]::Match((Get-Content $TepCompose -Raw), 'SMARTPORT_ANH_RABBITMQ:-([^}\s]+)\}')
    $ghim = if ($m.Success) { $m.Groups[1].Value } else { "rabbitmq:4.3.6-management" }
    $muon = if ("$env:SMARTPORT_ANH_RABBITMQ".Trim()) { "$env:SMARTPORT_ANH_RABBITMQ".Trim() } else { $ghim }
    if (CoAnh $muon) { return $muon }
    # 1) goi offline cua dung image can dung, roi cac goi rabbitmq khac (vd ban luu cua image cu)
    $dsGoi = @()
    if (Test-Path (TepGoi $muon)) { $dsGoi += Get-Item (TepGoi $muon) }
    $dsGoi += @(Get-ChildItem $ThuMucGoi -Filter "rabbitmq_*.tar" -File -ErrorAction SilentlyContinue | Where-Object { $_.FullName -ne (TepGoi $muon) })
    foreach ($t in $dsGoi) {
        Write-Host "Nap image tu goi offline (khong can Internet): $($t.FullName)"
        $r = GoiDocker @("load", "-i", $t.FullName) 900
        if ($r.Ma -ne 0) { Write-Host "  docker load loi: $(DongCuoi $r.Loi)" -ForegroundColor Yellow }
        if (CoAnh $muon) { return $muon }
    }
    # 2) image RabbitMQ 4.x da co san tren may (vd the troi rabbitmq:4-management cua ban truoc). Uu tien image cua
    #    container dang co: volume du lieu hang doi do chinh phien ban do tao ra (RabbitMQ khong cho ha phien ban).
    $anhContainer = (GoiDocker @("inspect", "--format", "{{.Config.Image}}", "smartport-rabbitmq") 20).Ra
    $dsCu = @((GoiDocker @("image", "ls", "rabbitmq", "--format", "{{.Repository}}:{{.Tag}}") 30).Ra -split "`r?`n" |
              Where-Object { $_ -match '^rabbitmq:4[.\d]*-management$' } |
              Sort-Object { if ($_ -eq $anhContainer) { 0 } elseif ($_ -eq "rabbitmq:4-management") { 1 } else { 2 } })
    foreach ($cu in $dsCu) {
        $pb = PhienBanRabbit $cu
        if ($pb -and $muon -eq "rabbitmq:$pb-management") {
            GoiDocker @("tag", $cu, $muon) 30 | Out-Null          # cung phien ban -> gan the ghim (cuc bo, khong tai)
            if (CoAnh $muon) { return $muon }
        }
    }
    if ($dsCu.Count -gt 0) {
        $cu = $dsCu[0]; $pb = PhienBanRabbit $cu
        $env:SMARTPORT_ANH_RABBITMQ = $cu
        Write-Host "Chua co image ghim $muon -> dung tam image da co tren may: $cu (RabbitMQ $pb), KHONG tai tu mang." -ForegroundColor Yellow
        if ($pb) { Write-Host "  Muon ghim dung ban nay: sua docker-compose.yml thanh rabbitmq:$pb-management" -ForegroundColor Yellow }
        return $cu
    }
    return $null
}

# Bat RabbitMQ: tra ve $null neu san sang, nguoc lai tra ve LY DO (he thong van chay tiep o che do suy giam)
function BatRabbitMQ {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { return "chua cai Docker Desktop" }
    $r = GoiDocker @("info", "--format", "{{.ServerVersion}}") 45
    if ($r.Ma -ne 0) { return "Docker Desktop chua chay ($(DongCuoi ($r.Loi + "`n" + $r.Ra)))" }
    $script:AnhRabbit = ChonAnhRabbitMQ
    if (-not $script:AnhRabbit) {
        return "may chua co image RabbitMQ va khong co goi offline trong $ThuMucGoi (can 1 lan co Internet: docker pull rabbitmq:4.3.6-management)"
    }
    $r = GoiDocker @("compose", "-f", $TepCompose, "up", "-d") 240
    if ($r.Ma -ne 0) { return "docker compose up loi: $(DongCuoi ($r.Loi + "`n" + $r.Ra))" }
    Write-Host -NoNewline "Doi RabbitMQ san sang"
    $tt = ""
    for ($i = 0; $i -lt 90; $i++) {
        $tt = (GoiDocker @("inspect", "--format", "{{.State.Health.Status}}", "smartport-rabbitmq") 20).Ra
        if ($tt -eq "healthy") { break }
        Write-Host -NoNewline "."; Start-Sleep -Seconds 2
    }
    Write-Host " -> $tt"
    if ($tt -ne "healthy") { return "RabbitMQ chua healthy sau 180 giay (docker logs smartport-rabbitmq)" }
    # Luu image ra goi offline 1 LAN (lan sau may mat image + mat mang van "docker load" duoc)
    $tep = TepGoi $script:AnhRabbit
    if (-not (Test-Path $tep)) {
        New-Item -ItemType Directory -Force $ThuMucGoi | Out-Null
        Write-Host "Luu image $($script:AnhRabbit) vao goi offline (1 lan, ~30 giay): $tep"
        $tam = "$tep.dang-ghi"
        $r = GoiDocker @("save", "-o", $tam, $script:AnhRabbit) 900
        if ($r.Ma -eq 0 -and (Test-Path $tam)) { Move-Item $tam $tep -Force }
        else { Remove-Item $tam -Force -ErrorAction SilentlyContinue; Write-Host "  Khong luu duoc goi offline: $(DongCuoi $r.Loi)" -ForegroundColor Yellow }
    }
    return $null
}

# Log cu: doi ten theo gio (lan ghi cuoi) vao nhat-ky/cu/<ten>_yyyyMMdd_HHmmss.log, giu 10 ban moi loai.
# Tep rong khong can giu (vd .loi.log khong co loi).
function XoayLog([string]$ten) {
    $thuMucCu = Join-Path $ThuMucLog "cu"
    foreach ($duoi in @("", ".loi")) {
        $tep = Join-Path $ThuMucLog "$ten$duoi.log"
        if (-not (Test-Path $tep)) { continue }
        $f = Get-Item $tep
        if ($f.Length -eq 0) { continue }
        New-Item -ItemType Directory -Force $thuMucCu | Out-Null
        $dich = Join-Path $thuMucCu ("{0}{1}_{2}.log" -f $ten, $duoi, $f.LastWriteTime.ToString("yyyyMMdd_HHmmss"))
        for ($i = 0; $i -lt 10; $i++) {      # tien trinh cu vua bi tat co the chua nha tep
            try { Move-Item -LiteralPath $tep -Destination $dich -Force -ErrorAction Stop; break } catch { Start-Sleep -Milliseconds 300 }
        }
        $mau = '^' + [regex]::Escape("$ten$duoi") + '_\d{8}_\d{6}\.log$'
        Get-ChildItem $thuMucCu -File | Where-Object { $_.Name -match $mau } | Sort-Object Name -Descending |
            Select-Object -Skip 10 | Remove-Item -Force -ErrorAction SilentlyContinue
    }
}

# Mo .pfx KHONG ghi khoa vao kho cua Windows (EphemeralKeySet)
function MoPfx([string]$tep, [string]$matKhau) {
    $co = [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::EphemeralKeySet
    $loader = "System.Security.Cryptography.X509Certificates.X509CertificateLoader" -as [type]
    if ($loader) { return $loader::LoadPkcs12FromFile($tep, $matKhau, $co) }
    return [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($tep, $matKhau, $co)
}
function KiemChungChi([string]$tep, [string]$matKhau) {
    try { $c = MoPfx $tep $matKhau } catch { return "khong mo duoc $tep (sai mat khau?): $($_.Exception.Message)" }
    try {
        if (-not $c.HasPrivateKey) { return "$tep khong co khoa rieng" }
        if ($c.NotAfter -lt (Get-Date)) { return "chung chi het han tu $($c.NotAfter.ToString('dd/MM/yyyy'))" }
        return $null
    } finally { $c.Dispose() }
}
function DiaChiLan {
    foreach ($ni in [System.Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces()) {
        if ($ni.OperationalStatus -ne "Up" -or $ni.NetworkInterfaceType -eq "Loopback") { continue }
        foreach ($u in $ni.GetIPProperties().UnicastAddresses) {
            if ($u.Address.AddressFamily -ne "InterNetwork") { continue }
            $b = $u.Address.GetAddressBytes()
            if ($b[0] -eq 10 -or ($b[0] -eq 172 -and $b[1] -ge 16 -and $b[1] -le 31) -or ($b[0] -eq 192 -and $b[1] -eq 168) -or ($b[0] -eq 169 -and $b[1] -eq 254)) {
                $u.Address.ToString()
            }
        }
    }
}

# Khoa "dang khoi dong": giam_sat.ps1 tam ngung trong luc script nay dang tat / bat dich vu (tranh 2 ben cung chay lai)
$TepKhoaKhoiDong = Join-Path $ThuMucPid "dang-khoi-dong.khoa"
Set-Content -Path $TepKhoaKhoiDong -Value "$PID"
$AnhRabbit = $null
$SuyGiam = $null
$PidDinhDanh = $null
try {

# ---------------------------------------------------------------- 1. bi mat cuc bo
$TepBiMat = Join-Path $ThuMucBiMat "bi-mat.json"
if (-not (Test-Path $TepBiMat)) {
    function NgauNhien([int]$n) {
        $b = [byte[]]::new($n); [System.Security.Cryptography.RandomNumberGenerator]::Fill($b)
        return ([Convert]::ToBase64String($b) -replace '[+/=]', 'x')
    }
    @{ jwtKey = (NgauNhien 48); rabbitMatKhau = (NgauNhien 18) } | ConvertTo-Json | Set-Content -Encoding utf8 $TepBiMat
    Write-Host "Da sinh bi mat cuc bo: $TepBiMat (khong dua len git)"
}
$BiMat = Get-Content $TepBiMat -Raw | ConvertFrom-Json

# ---------------------------------------------------------------- 2. RabbitMQ (khong len -> chay suy giam)
$env:RABBITMQ_MAT_KHAU = $BiMat.rabbitMatKhau
if ($BoQuaHaTang) {
    Write-Host "Bo qua buoc RabbitMQ + CSDL (-BoQuaHaTang: chi chay lai dich vu)"
} else {
    $SuyGiam = BatRabbitMQ
    if ($SuyGiam) {
        Write-Host ""
        Write-Host "!! CHAY SUY GIAM: RabbitMQ khong san sang - $SuyGiam" -ForegroundColor Yellow
        Write-Host "   He thong VAN CHAY: nghiep vu ghi CSDL binh thuong, su kien nam cho trong Outbox va tu gui khi RabbitMQ len lai." -ForegroundColor Yellow
        Write-Host "   Bat lai RabbitMQ: mo Docker Desktop roi chay 'SMARTPORT.bat giam-sat' (tu bat lai) hoac 'SMARTPORT.bat chay'." -ForegroundColor Yellow
        Write-Host ""
    }

# ---------------------------------------------------------------- 3. CSDL tung dich vu
    $thamSo = @((Join-Path $Goc "dung_csdl_dich_vu.py"))
    if ($LamLaiCsdl) { $thamSo += "--lam-lai" }
    $env:PYTHONIOENCODING = "utf-8"
    python @thamSo
    if ($LASTEXITCODE -ne 0) { throw "Dung CSDL dich vu loi (thieu CSDL -> phuc hoi tu ban sao luu: SMARTPORT.bat phuc-hoi)" }
}

# ---------------------------------------------------------------- 4. build + chay
if (-not $KhongBuild) {
    dotnet build (Join-Path $Web "SmartPort.Net10.slnx") -v q -nologo
    if ($LASTEXITCODE -ne 0) { throw "Build loi" }
}
# Chay lai: tat ban cu truoc (co -Chi thi chi tat cac dich vu duoc chay lai, gateway giu nguyen)
if ($Chi) { & (Join-Path $Goc "dung_microservices.ps1") -Im -Chi $Chi | Out-Null } else { & (Join-Path $Goc "dung_microservices.ps1") -Im | Out-Null }

$DanhMuc = Get-Content (Join-Path $Web "dich-vu.json") -Raw | ConvertFrom-Json
$env:Jwt__Key = $BiMat.jwtKey
$env:Bus__ChuoiKetNoi = "amqp://smartport:$($BiMat.rabbitMatKhau)@127.0.0.1:5672/"   # 127.0.0.1: RabbitMQ chi publish IPv4, "localhost" thu ::1 truoc rat cham
$env:DichVu__DanhMuc = Join-Path $Web "dich-vu.json"
$env:DuongDan__WebRoot = Join-Path $Web "SmartPort.Api\wwwroot"
$env:DichVu__CauHinhChung = Join-Path $Goc "appsettings.chung.json"
# Giu Development (xem ly do trong bao cao van hanh); muon thu Production: dat SMARTPORT_MOI_TRUONG=Production truoc khi chay
$env:ASPNETCORE_ENVIRONMENT = if ("$env:SMARTPORT_MOI_TRUONG".Trim()) { "$env:SMARTPORT_MOI_TRUONG".Trim() } else { "Development" }

# Tuy chon (dien vao .bimat/bi-mat.json — KHONG dua len git): dang nhap Google, gui email that qua Gmail, SMS that qua Twilio.
# Bo trong thi he thong chay CHE DO DEMO (email -> App_Data/Outbox/*.eml, SMS -> App_Data/OutboxSms/*.txt, an nut Google).
function DatBien($ten, $giaTri) { if ("$giaTri".Trim()) { Set-Item -Path "Env:$ten" -Value "$giaTri".Trim() } }
if ($BiMat.google -and "$($BiMat.google.clientId)".Trim() -and "$($BiMat.google.clientSecret)".Trim()) {
    DatBien "Google__ClientId" $BiMat.google.clientId
    DatBien "Google__ClientSecret" $BiMat.google.clientSecret
    DatBien "Google__DiaChiGoiLai" $BiMat.google.diaChiGoiLai
    Write-Host "Dang nhap Google: BAT"
}
if ($BiMat.smtp -and "$($BiMat.smtp.host)".Trim() -and "$($BiMat.smtp.pass)".Trim()) {
    DatBien "AppSettings__SmtpHost" $BiMat.smtp.host
    DatBien "AppSettings__SmtpPort" $BiMat.smtp.port
    DatBien "AppSettings__SmtpSsl" $BiMat.smtp.ssl
    DatBien "AppSettings__SmtpUser" $BiMat.smtp.user
    DatBien "AppSettings__SmtpPass" $BiMat.smtp.pass
    DatBien "AppSettings__SmtpFrom" $BiMat.smtp.from
    Write-Host "Email that qua $($BiMat.smtp.host): BAT"
}
if ($BiMat.sms -and "$($BiMat.sms.nhaCungCap)" -eq "twilio" -and "$($BiMat.sms.twilioAccountSid)".Trim() -and "$($BiMat.sms.twilioAuthToken)".Trim() -and "$($BiMat.sms.twilioFrom)".Trim()) {
    DatBien "AppSettings__SmsNhaCungCap" "twilio"
    DatBien "AppSettings__TwilioAccountSid" $BiMat.sms.twilioAccountSid
    DatBien "AppSettings__TwilioAuthToken" $BiMat.sms.twilioAuthToken
    DatBien "AppSettings__TwilioFrom" $BiMat.sms.twilioFrom
    Write-Host "SMS that qua Twilio: BAT"
}

# Mang noi bo (-ChoPhepLan): gateway doc 3 bien nay -> nghe THEM https://0.0.0.0:<cong>; http://localhost:5100 giu nguyen.
# 13 dich vu, AI va RabbitMQ van chi nghe loopback.
$LanBat = $false
if ($ChoPhepLan) {
    $tepPfx = Join-Path $ThuMucBiMat "chung-chi\may-chu.pfx"
    $mkPfx = "$($BiMat.chungChiMatKhau)"
    $loiCc = if (-not (Test-Path $tepPfx)) { "chua co $tepPfx" }
             elseif (-not $mkPfx.Trim()) { ".bimat/bi-mat.json chua co khoa chungChiMatKhau" }
             else { KiemChungChi $tepPfx $mkPfx }
    if ($loiCc) {
        Write-Host "Mang noi bo HTTPS: CHUA BAT - $loiCc" -ForegroundColor Yellow
        Write-Host "  Tao chung chi: SMARTPORT.bat tao-chung-chi (roi chay lai chay-lan). He thong van chay o localhost." -ForegroundColor Yellow
    } else {
        $env:SMARTPORT_CHUNG_CHI = $tepPfx
        $env:SMARTPORT_CHUNG_CHI_MAT_KHAU = $mkPfx
        if (-not "$env:SMARTPORT_CONG_HTTPS".Trim()) { $env:SMARTPORT_CONG_HTTPS = "5443" }
        $LanBat = $true
        Write-Host "Mang noi bo HTTPS: BAT (cong $($env:SMARTPORT_CONG_HTTPS), chung chi $tepPfx)"
    }
}

function ChayTienTrinh($ten, $thuMucDuAn, $dll, $cong) {
    $env:ASPNETCORE_URLS = "http://localhost:$cong"
    XoayLog $ten
    $p = Start-Process -FilePath "dotnet" -ArgumentList "`"$dll`"" -WorkingDirectory $thuMucDuAn -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $ThuMucLog "$ten.log") -RedirectStandardError (Join-Path $ThuMucLog "$ten.loi.log")
    Set-Content -Path (Join-Path $ThuMucPid "$ten.pid") -Value $p.Id
    return $p
}

$dangChay = @()
# Dich vu AI thi giac (Python): ma nguon + mo hinh o Web\SmartPort.AI (dat SMARTPORT_AI_DIR neu de cho khac)
$ThuMucAi = if ($env:SMARTPORT_AI_DIR) { $env:SMARTPORT_AI_DIR }
            elseif (Test-Path (Join-Path $Web "SmartPort.AI")) { Join-Path $Web "SmartPort.AI" }
            else { Join-Path $HOME "Downloads\SmartPort_AI" }
foreach ($dv in $DanhMuc.dichVu | Where-Object { $_.nenTang -eq "python" -and $_.ten -eq "ai-thi-giac" }) {
    $py = Join-Path $ThuMucAi ".venv\Scripts\python.exe"
    if ((Test-Path $py) -and (-not $Chi -or $Chi -contains $dv.ten)) {
        $env:AI_CONG = "$($dv.cong)"
        # Mo hinh da co san cuc bo: cam ultralytics / Hugging Face tu do mang (DNS treo khi mat WAN, telemetry, tai trong so)
        $env:YOLO_OFFLINE = "1"; $env:HF_HUB_OFFLINE = "1"; $env:TRANSFORMERS_OFFLINE = "1"
        XoayLog $dv.ten
        $p = Start-Process -FilePath $py -ArgumentList "dich_vu_ai.py" -WorkingDirectory $ThuMucAi -WindowStyle Hidden -PassThru `
            -RedirectStandardOutput (Join-Path $ThuMucLog "$($dv.ten).log") -RedirectStandardError (Join-Path $ThuMucLog "$($dv.ten).loi.log")
        Set-Content -Path (Join-Path $ThuMucPid "$($dv.ten).pid") -Value $p.Id
        $dangChay += [pscustomobject]@{ Ten = $dv.ten; Cong = $dv.cong; Pid = $p.Id }
    }
}
foreach ($dv in $DanhMuc.dichVu) {
    if ($dv.nenTang -and $dv.nenTang -ne "dotnet") { continue }
    if ($Chi -and ($Chi -notcontains $dv.ten)) { continue }
    $P = ($dv.ten -split '-' | ForEach-Object { $_.Substring(0, 1).ToUpper() + $_.Substring(1) }) -join ''
    $duAn = Join-Path $Web "DichVu\SmartPort.DichVu.$P"
    $dll = Join-Path $duAn "bin\Debug\net10.0\SmartPort.DichVu.$P.dll"
    if ($dv.ten -eq "dinh-danh") {
        # Ma OTP demo trong phan hoi: CHI che do kiem thu; chay that dat ro false (khong de appsettings nao bat duoc)
        $env:AppSettings__SmsDemoTraMa = $(if ($CheDoKiemThu) { "true" } else { "false" })
        $p = ChayTienTrinh $dv.ten $duAn $dll $dv.cong
        Remove-Item Env:AppSettings__SmsDemoTraMa -ErrorAction SilentlyContinue     # chi dinh-danh nhan bien nay
        $PidDinhDanh = $p.Id
    } else {
        $p = ChayTienTrinh $dv.ten $duAn $dll $dv.cong
    }
    $dangChay += [pscustomobject]@{ Ten = $dv.ten; Cong = $dv.cong; Pid = $p.Id }
}
$coGateway = (-not $Chi -or $Chi -contains "gateway")
if ($coGateway) {
$gw = ChayTienTrinh "gateway" (Join-Path $Web "SmartPort.Gateway") (Join-Path $Web "SmartPort.Gateway\bin\Debug\net10.0\SmartPort.Gateway.dll") $DanhMuc.gateway.cong
$dangChay += [pscustomobject]@{ Ten = "gateway"; Cong = $DanhMuc.gateway.cong; Pid = $gw.Id }
}

# Che do dang chay (giam_sat.ps1 doc de chay lai gateway / RabbitMQ y nhu cu)
$TepCheDo = Join-Path $ThuMucPid "che-do.json"
$cheDo = [ordered]@{ choPhepLan = $false; moiTruong = $env:ASPNETCORE_ENVIRONMENT; anhRabbitMQ = $null; cheDoKiemThuPid = $null; capNhatLuc = $null }
if (Test-Path $TepCheDo) {
    try { $cu = Get-Content $TepCheDo -Raw | ConvertFrom-Json; foreach ($k in @($cheDo.Keys)) { if ($null -ne $cu.$k) { $cheDo[$k] = $cu.$k } } } catch { }
}
if ($coGateway) { $cheDo.choPhepLan = [bool]$ChoPhepLan; $cheDo.moiTruong = $env:ASPNETCORE_ENVIRONMENT }
if (-not $BoQuaHaTang -and $AnhRabbit) { $cheDo.anhRabbitMQ = $AnhRabbit }
# pid cua dinh-danh dang chay o che do kiem thu (SMARTPORT.bat kiem-thu doi chieu voi pid/dinh-danh.pid); chay thuong -> null
if ($PidDinhDanh) { $cheDo.cheDoKiemThuPid = $(if ($CheDoKiemThu) { $PidDinhDanh } else { $null }) }
$cheDo.capNhatLuc = (Get-Date).ToString("s")
$cheDo | ConvertTo-Json | Set-Content -Encoding utf8 $TepCheDo

# ---------------------------------------------------------------- 5. doi suc khoe
# Suy giam (hoac chi chay lai 1 dich vu): /suc-khoe ban cu tra 503 khi mat RabbitMQ -> co tra loi la da khoi dong xong
$chap503 = [bool]$SuyGiam -or $BoQuaHaTang
Write-Host -NoNewline "Doi cac dich vu khoi dong"
$het = (Get-Date).AddSeconds(150)
do {
    Start-Sleep -Seconds 2; Write-Host -NoNewline "."
    $chuaXong = $dangChay | Where-Object {
        try {
            $ma = (Invoke-WebRequest "http://127.0.0.1:$($_.Cong)/suc-khoe" -TimeoutSec 3 -SkipHttpErrorCheck).StatusCode
            -not ($ma -eq 200 -or ($chap503 -and $ma -eq 503))
        } catch { $true }
    }
} while ($chuaXong -and (Get-Date) -lt $het)
Write-Host ""
$bang = foreach ($d in $dangChay) {
    try {
        $tl = Invoke-WebRequest "http://127.0.0.1:$($d.Cong)/suc-khoe" -TimeoutSec 5 -SkipHttpErrorCheck
        $r = $tl.Content | ConvertFrom-Json
        [pscustomobject]@{ DichVu = $d.Ten; Cong = $d.Cong; TinhTrang = $r.tinhTrang; CSDL = $r.coSoDuLieu; RabbitMQ = $r.rabbitMQ;
                           SoHuu = $(if ($r.bangSoHuu) { @($r.bangSoHuu).Count } else { 0 }); BanSao = $(if ($r.bangBanSao) { @($r.bangBanSao).Count } else { 0 }) }
    } catch {
        [pscustomobject]@{ DichVu = $d.Ten; Cong = $d.Cong; TinhTrang = "KHONG PHAN HOI (xem nhat-ky/$($d.Ten).log)" }
    }
}
$bang | Format-Table -AutoSize
Write-Host "Giao dien:      http://localhost:$($DanhMuc.gateway.cong)"
if ($LanBat -and $coGateway) {
    foreach ($ip in @(DiaChiLan | Select-Object -Unique)) { Write-Host "Mang noi bo:    https://${ip}:$($env:SMARTPORT_CONG_HTTPS)   (may tram: cai ca.cer + mo firewall - xem SMARTPORT.bat tao-chung-chi)" }
}
Write-Host "RabbitMQ admin: http://localhost:15672  (tai khoan smartport, mat khau trong .bimat/bi-mat.json)"
Write-Host "Swagger tung dich vu: http://localhost:$($DanhMuc.gateway.cong)/dich-vu/<ten>/swagger"
if ($SuyGiam) {
    Write-Host "!! DANG CHAY SUY GIAM (khong co RabbitMQ): $SuyGiam" -ForegroundColor Yellow
}
if ($CheDoKiemThu -and $PidDinhDanh) {
    Write-Host "CHE DO KIEM THU: dinh-danh tra maDemo OTP cho yeu cau tu chinh may chu. Chay that (tat maDemo): SMARTPORT.bat chay" -ForegroundColor Yellow
}

} finally {
    # chi xoa khoa cua chinh minh (giam_sat.ps1 co the dang chay lai 1 dich vu bang tien trinh khac)
    try { if ((Get-Content $TepKhoaKhoiDong -Raw -ErrorAction Stop).Trim() -eq "$PID") { Remove-Item $TepKhoaKhoiDong -Force } } catch { }
}
