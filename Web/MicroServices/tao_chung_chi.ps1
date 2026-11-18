<#
  TAO CHUNG CHI HTTPS CHO MANG NOI BO (khong can Internet, khong can mkcert - dung API .NET CertificateRequest):
    1. CA noi bo "SmartPort CA noi bo (<ten may>)" RSA 3072, 10 nam. Da co ca.pfx thi DUNG LAI (may tram khong phai cai lai);
       -LamMoiCA de tao CA moi.
    2. Chung chi may chu RSA 2048, toi da 825 ngay (gioi han iOS/macOS), CA ky, EKU serverAuth,
       SAN: localhost, 127.0.0.1, ::1, ten may, <ten may>.local, MOI IPv4 noi bo (10.x, 172.16-31.x, 192.168.x, 169.254.x) + -TenThem/-IpThem.
       Hieu luc lui 2 ngay (may tram mat Internet hay lech gio).
    3. CHI XUAT TEP vao -ThuMucRa (mac dinh MicroServices/.bimat/chung-chi):
         ca.cer        - CA (cong khai): cai len MAY TRAM
         ca.pfx        - CA + khoa rieng (GIU KIN): de cap lai chung chi may chu khi doi IP ma khong phai cai lai may tram
         may-chu.pfx   - gateway dung (SMARTPORT_CHUNG_CHI), may-chu.cer (cong khai, de xem)
       Mat khau .pfx ghi vao bi-mat.json khoa "chungChiMatKhau" (giu nguyen moi khoa cu; da co mat khau thi dung lai).
    4. KHONG ghi kho chung chi Windows, KHONG mo firewall: chi IN lenh de nguoi dung (quyen admin) tu chay.
    5. Tu kiem: doc lai may-chu.pfx bang mat khau, dung chuoi tin cay voi CA (trong bo nho), kiem SAN + EKU.
  Dung:  pwsh MicroServices/tao_chung_chi.ps1 [-ThuMucRa <thu muc>] [-TepBiMat <tep json>] [-TenThem cang.local] [-IpThem 192.168.1.10] [-LamMoiCA]
         Co -ThuMucRa ma khong co -TepBiMat -> mat khau ghi vao <ThuMucRa>\bi-mat.json (de chay thu, khong dung .bimat that).
#>
param([string]$ThuMucRa, [string]$TepBiMat, [string[]]$TenThem, [string[]]$IpThem, [switch]$LamMoiCA, [int]$CongHttps = 5443)
$ErrorActionPreference = "Stop"
$Goc = $PSScriptRoot
$TepBiMatThat = Join-Path $Goc ".bimat\bi-mat.json"
if (-not $ThuMucRa) {
    $ThuMucRa = Join-Path $Goc ".bimat\chung-chi"
    if (-not $TepBiMat) { $TepBiMat = $TepBiMatThat }
} elseif (-not $TepBiMat) {
    $TepBiMat = Join-Path $ThuMucRa "bi-mat.json"
}
if ($TenThem) { $TenThem = @($TenThem | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ }) }
if ($IpThem) { $IpThem = @($IpThem | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ }) }
New-Item -ItemType Directory -Force $ThuMucRa | Out-Null
$ThuMucRa = (Resolve-Path $ThuMucRa).Path

$X509 = "System.Security.Cryptography.X509Certificates"
$Rsa = [System.Security.Cryptography.RSA]
$Sha256 = [System.Security.Cryptography.HashAlgorithmName]::SHA256
$Pkcs1 = [System.Security.Cryptography.RSASignaturePadding]::Pkcs1
$OidServerAuth = "1.3.6.1.5.5.7.3.1"

function NgauNhien([int]$n) {
    $b = [byte[]]::new($n); [System.Security.Cryptography.RandomNumberGenerator]::Fill($b)
    return ([Convert]::ToBase64String($b) -replace '[+/=]', 'x')
}
# Mo .pfx KHONG luu khoa vao kho khoa cua Windows (EphemeralKeySet)
function MoPfx([string]$tep, [string]$matKhau) {
    $co = [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::EphemeralKeySet
    $loader = "$X509.X509CertificateLoader" -as [type]
    if ($loader) { return $loader::LoadPkcs12FromFile($tep, $matKhau, $co) }
    return [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($tep, $matKhau, $co)
}
function MoCer([string]$tep) {
    $loader = "$X509.X509CertificateLoader" -as [type]
    if ($loader) { return $loader::LoadCertificateFromFile($tep) }
    return [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($tep)
}
function GhiTepAnToan([string]$tep, [byte[]]$du) {      # ghi tep tam roi doi ten: khong de tep do dang
    $tam = "$tep.dang-ghi"
    [System.IO.File]::WriteAllBytes($tam, $du)
    Move-Item -LiteralPath $tam -Destination $tep -Force
}
function LaIPv4NoiBo([System.Net.IPAddress]$ip) {
    if ($ip.AddressFamily -ne [System.Net.Sockets.AddressFamily]::InterNetwork) { return $false }
    $b = $ip.GetAddressBytes()
    return ($b[0] -eq 10 -or ($b[0] -eq 172 -and $b[1] -ge 16 -and $b[1] -le 31) -or ($b[0] -eq 192 -and $b[1] -eq 168) -or ($b[0] -eq 169 -and $b[1] -eq 254))
}

# ---------------------------------------------------------------- 1. mat khau (.bimat/bi-mat.json, giu khoa cu)
$biMat = $null
if (Test-Path $TepBiMat) { $biMat = Get-Content $TepBiMat -Raw | ConvertFrom-Json }
$canGhiBiMat = $false
if (-not $biMat) {
    $biMat = [pscustomobject]@{}
    if ([System.IO.Path]::GetFullPath($TepBiMat) -eq [System.IO.Path]::GetFullPath($TepBiMatThat)) {
        # .bimat that chua co: sinh luon khoa JWT + mat khau RabbitMQ giong chay_microservices.ps1 (neu khong, chay se khong sinh nua)
        $biMat | Add-Member -NotePropertyName jwtKey -NotePropertyValue (NgauNhien 48)
        $biMat | Add-Member -NotePropertyName rabbitMatKhau -NotePropertyValue (NgauNhien 18)
    }
    $canGhiBiMat = $true
}
$matKhau = "$($biMat.chungChiMatKhau)".Trim()
if (-not $matKhau) {
    $matKhau = NgauNhien 24
    $biMat | Add-Member -NotePropertyName chungChiMatKhau -NotePropertyValue $matKhau -Force
    $canGhiBiMat = $true
}

# ---------------------------------------------------------------- 2. CA noi bo (dung lai neu co)
$tepCaPfx = Join-Path $ThuMucRa "ca.pfx"
$tepCaCer = Join-Path $ThuMucRa "ca.cer"
$ca = $null; $caMoi = $false
if (-not $LamMoiCA -and (Test-Path $tepCaPfx)) {
    try {
        $ca = MoPfx $tepCaPfx $matKhau
        if (-not $ca.HasPrivateKey) { Write-Host "ca.pfx khong co khoa rieng -> tao CA moi" -ForegroundColor Yellow; $ca = $null }
        elseif ($ca.NotAfter -lt (Get-Date).AddDays(60)) { Write-Host "CA cu sap het han ($($ca.NotAfter)) -> tao CA moi" -ForegroundColor Yellow; $ca = $null }
    } catch {
        Write-Host "Khong mo duoc ca.pfx bang mat khau hien tai -> tao CA moi (may tram phai cai lai ca.cer)" -ForegroundColor Yellow
        $ca = $null
    }
}
if (-not $ca) {
    $khoaCa = $Rsa::Create(3072)
    $tenCa = [System.Security.Cryptography.X509Certificates.X500DistinguishedName]::new("CN=SmartPort CA noi bo ($($env:COMPUTERNAME)), O=SmartPort, OU=Van hanh")
    $ycCa = [System.Security.Cryptography.X509Certificates.CertificateRequest]::new($tenCa, $khoaCa, $Sha256, $Pkcs1)
    $ycCa.CertificateExtensions.Add([System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]::new($true, $true, 0, $true))
    $ycCa.CertificateExtensions.Add([System.Security.Cryptography.X509Certificates.X509KeyUsageExtension]::new(
        [System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]"KeyCertSign, CrlSign, DigitalSignature", $true))
    $ycCa.CertificateExtensions.Add([System.Security.Cryptography.X509Certificates.X509SubjectKeyIdentifierExtension]::new($ycCa.PublicKey, $false))
    $ca = $ycCa.CreateSelfSigned([DateTimeOffset]::Now.AddDays(-2), [DateTimeOffset]::Now.AddYears(10))
    $caMoi = $true
}

# ---------------------------------------------------------------- 3. chung chi may chu
$dsTen = [System.Collections.Generic.List[string]]::new()
foreach ($t in @("localhost", $env:COMPUTERNAME, "$($env:COMPUTERNAME).local")) { if ($t -and -not $dsTen.Contains($t.ToLower())) { $dsTen.Add($t.ToLower()) } }
try {
    $gp = [System.Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties()
    if ($gp.DomainName) { $fq = "$($gp.HostName).$($gp.DomainName)".ToLower(); if (-not $dsTen.Contains($fq)) { $dsTen.Add($fq) } }
} catch { }
foreach ($t in @($TenThem)) { if ($t -and -not $dsTen.Contains($t.ToLower())) { $dsTen.Add($t.ToLower()) } }

$dsIp = [System.Collections.Generic.List[string]]::new()
$dsIp.Add("127.0.0.1"); $dsIp.Add("::1")
foreach ($ni in [System.Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces()) {
    foreach ($u in $ni.GetIPProperties().UnicastAddresses) {
        if ((LaIPv4NoiBo $u.Address) -and -not $dsIp.Contains($u.Address.ToString())) { $dsIp.Add($u.Address.ToString()) }
    }
}
foreach ($t in @($IpThem)) {
    if (-not $t) { continue }
    $ip = $null
    if (-not [System.Net.IPAddress]::TryParse($t, [ref]$ip)) { throw "-IpThem '$t' khong phai dia chi IP" }
    if (-not $dsIp.Contains($ip.ToString())) { $dsIp.Add($ip.ToString()) }
}

$khoaMc = $Rsa::Create(2048)
$ycMc = [System.Security.Cryptography.X509Certificates.CertificateRequest]::new(
    [System.Security.Cryptography.X509Certificates.X500DistinguishedName]::new("CN=$($env:COMPUTERNAME.ToLower()), O=SmartPort"), $khoaMc, $Sha256, $Pkcs1)
$ycMc.CertificateExtensions.Add([System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]::new($false, $false, 0, $true))
$ycMc.CertificateExtensions.Add([System.Security.Cryptography.X509Certificates.X509KeyUsageExtension]::new(
    [System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]"DigitalSignature, KeyEncipherment", $true))
$eku = [System.Security.Cryptography.OidCollection]::new()
[void]$eku.Add([System.Security.Cryptography.Oid]::new($OidServerAuth))
$ycMc.CertificateExtensions.Add([System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]::new($eku, $false))
$san = [System.Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder]::new()
foreach ($t in $dsTen) { $san.AddDnsName($t) }
foreach ($t in $dsIp) { $san.AddIpAddress([System.Net.IPAddress]::Parse($t)) }
$ycMc.CertificateExtensions.Add($san.Build($false))
$ycMc.CertificateExtensions.Add([System.Security.Cryptography.X509Certificates.X509SubjectKeyIdentifierExtension]::new($ycMc.PublicKey, $false))
$akiType = "$X509.X509AuthorityKeyIdentifierExtension" -as [type]
if ($akiType) { $ycMc.CertificateExtensions.Add($akiType::CreateFromCertificate($ca, $true, $false)) }
$soSerial = [byte[]]::new(16); [System.Security.Cryptography.RandomNumberGenerator]::Fill($soSerial); $soSerial[0] = $soSerial[0] -band 0x7F
$tu = [DateTimeOffset]::Now.AddDays(-2)
$den = [DateTimeOffset]::Now.AddDays(825)
$caHet = [DateTimeOffset]::new($ca.NotAfter).AddDays(-1)
if ($den -gt $caHet) { $den = $caHet }
$mc = $ycMc.Create($ca, $tu, $den, $soSerial)
$mcCoKhoa = [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::CopyWithPrivateKey($mc, $khoaMc)

# ---------------------------------------------------------------- 4. xuat tep (KHONG cai vao kho Windows)
$Pfx = [System.Security.Cryptography.X509Certificates.X509ContentType]::Pfx
$Cer = [System.Security.Cryptography.X509Certificates.X509ContentType]::Cert
$tepMcPfx = Join-Path $ThuMucRa "may-chu.pfx"
$tepMcCer = Join-Path $ThuMucRa "may-chu.cer"
GhiTepAnToan $tepCaCer $ca.Export($Cer)
if ($caMoi) { GhiTepAnToan $tepCaPfx $ca.Export($Pfx, $matKhau) }
GhiTepAnToan $tepMcPfx $mcCoKhoa.Export($Pfx, $matKhau)
GhiTepAnToan $tepMcCer $mc.Export($Cer)
if ($canGhiBiMat) {
    New-Item -ItemType Directory -Force (Split-Path $TepBiMat -Parent) | Out-Null
    $tam = "$TepBiMat.dang-ghi"
    $biMat | ConvertTo-Json -Depth 20 | Set-Content -Encoding utf8 $tam
    Move-Item -LiteralPath $tam -Destination $TepBiMat -Force
}

# ---------------------------------------------------------------- 5. tu kiem (trong bo nho, khong dung kho Windows)
$loiKiem = @()
$mcDoc = $null; $caDoc = $null; $chuoi = $null
try {
    $mcDoc = MoPfx $tepMcPfx $matKhau
    $caDoc = MoCer $tepCaCer
    if (-not $mcDoc.HasPrivateKey) { $loiKiem += "may-chu.pfx khong co khoa rieng" }
    if ($mcDoc.Issuer -ne $caDoc.Subject) { $loiKiem += "nguoi cap ($($mcDoc.Issuer)) khac CA ($($caDoc.Subject))" }
    $chuoi = [System.Security.Cryptography.X509Certificates.X509Chain]::new()
    $chuoi.ChainPolicy.TrustMode = [System.Security.Cryptography.X509Certificates.X509ChainTrustMode]::CustomRootTrust
    [void]$chuoi.ChainPolicy.CustomTrustStore.Add($caDoc)
    $chuoi.ChainPolicy.RevocationMode = [System.Security.Cryptography.X509Certificates.X509RevocationMode]::NoCheck
    [void]$chuoi.ChainPolicy.ApplicationPolicy.Add([System.Security.Cryptography.Oid]::new($OidServerAuth))
    if (-not $chuoi.Build($mcDoc)) {
        $loiKiem += "chuoi tin cay voi CA KHONG hop le: " + (($chuoi.ChainStatus | ForEach-Object { "$($_.Status) $($_.StatusInformation)".Trim() }) -join "; ")
    }
    $sanExt = $mcDoc.Extensions | Where-Object { $_.Oid.Value -eq "2.5.29.17" } | Select-Object -First 1
    $sanType = "$X509.X509SubjectAlternativeNameExtension" -as [type]
    if (-not $sanExt) { $loiKiem += "thieu SAN" }
    elseif ($sanType) {
        $sanDoc = $sanType::new($sanExt.RawData, $false)
        $tenDoc = @($sanDoc.EnumerateDnsNames()); $ipDoc = @($sanDoc.EnumerateIPAddresses() | ForEach-Object { $_.ToString() })
        foreach ($t in $dsTen) { if ($tenDoc -notcontains $t) { $loiKiem += "SAN thieu ten $t" } }
        foreach ($t in $dsIp) { if ($ipDoc -notcontains $t) { $loiKiem += "SAN thieu IP $t" } }
    }
} catch {
    $loiKiem += "Loi tu kiem: $($_.Exception.Message)"
} finally {
    if ($chuoi) { $chuoi.Dispose() }
    if ($mcDoc) { $mcDoc.Dispose() }
}

# ---------------------------------------------------------------- 6. bao cao + LENH cho nguoi dung (chi in)
Write-Host ""
Write-Host "=== DA TAO CHUNG CHI HTTPS (CHI GHI TEP - chua cai vao Windows, chua mo firewall) ===" -ForegroundColor Cyan
Write-Host ("  CA noi bo  : {0}  ({1}, het han {2:dd/MM/yyyy})" -f $tepCaCer, $(if ($caMoi) { "MOI tao" } else { "dung lai CA cu - may tram KHONG phai cai lai" }), $ca.NotAfter)
Write-Host "               $($ca.Subject)  SHA1 $($ca.Thumbprint)"
Write-Host ("  May chu    : {0}  (het han {1:dd/MM/yyyy})" -f $tepMcPfx, $mc.NotAfter)
Write-Host "  SAN ten    : $($dsTen -join ', ')"
Write-Host "  SAN IP     : $($dsIp -join ', ')"
Write-Host "  Mat khau   : $TepBiMat  (khoa chungChiMatKhau$(if (-not $canGhiBiMat) { ', dung lai' }))"
if ($loiKiem.Count -eq 0) { Write-Host "  TU KIEM    : DAT (doc lai .pfx bang mat khau, chuoi tin cay voi CA, EKU serverAuth, SAN du)" -ForegroundColor Green }
else { Write-Host "  TU KIEM    : LOI" -ForegroundColor Red; $loiKiem | ForEach-Object { Write-Host "    - $_" -ForegroundColor Red } }
$ipLan = @($dsIp | Where-Object { $_ -ne "127.0.0.1" -and $_ -ne "::1" })
$ipVd = if ($ipLan.Count -gt 0) { $ipLan[0] } else { "<ip-may-chu>" }
Write-Host ""
Write-Host "LENH NGUOI DUNG TU CHAY - PowerShell 'Run as administrator' (script KHONG tu chay):" -ForegroundColor Yellow
Write-Host "  1) May chu - mo cong HTTPS $CongHttps chi cho mang Private:"
Write-Host "       New-NetFirewallRule -DisplayName 'SmartPort HTTPS $CongHttps' -Direction Inbound -Protocol TCP -LocalPort $CongHttps -Action Allow -Profile Private"
Write-Host "     Mang Wi-Fi / LAN cua cang phai la Private:  Get-NetConnectionProfile   (doi: Set-NetConnectionProfile -InterfaceAlias '<ten>' -NetworkCategory Private)"
Write-Host "  2) May chu (tuy chon, de chinh may chu mo https://${ipVd}:$CongHttps khong bao loi):"
Write-Host "       certutil -addstore -f Root `"$tepCaCer`""
Write-Host "  3) MOI MAY TRAM: chep ca.cer sang roi cai vao muc CA goc tin cay:"
Write-Host "       Windows (admin): certutil -addstore -f Root ca.cer"
Write-Host "       Android: Cai dat > Bao mat > Ma hoa va thong tin xac thuc > Cai dat chung chi > Chung chi CA > chon ca.cer"
Write-Host "       iPhone/iPad: mo ca.cer > Cai dat > Da tai ve ho so > Cai dat; roi Cai dat chung > Gioi thieu > Cai dat tin cay chung chi > bat 'SmartPort CA noi bo'"
Write-Host "  4) Chay he thong cho mang noi bo: SMARTPORT.bat chay-lan  -> may tram mo https://${ipVd}:$CongHttps"
Write-Host "  Doi IP may chu: chay lai tao-chung-chi (dung lai CA -> may tram khong phai cai lai). Nen dat IP tinh / DHCP co dinh cho may chu."
Write-Host "  GIU KIN ca.pfx + bi-mat.json: ai co khoa CA co the gia mao trang web voi cac may da tin CA nay." -ForegroundColor Yellow
if ($loiKiem.Count -gt 0) { exit 1 }
exit 0
