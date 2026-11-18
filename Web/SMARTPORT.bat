@echo off
rem ============================================================================================
rem  SMARTPORT.bat - MOT FILE DIEU KHIEN TOAN BO HE THONG
rem    Bam dup: hien menu.   Hoac goi kem lenh:  SMARTPORT.bat chay ^| build ^| tat ^| tinh-trang ^|
rem                                               mo ^| an-toan ^| kiem-thu ^| kiem-thu-nhanh ^| danh-gia-ai ^|
rem                                               chay-lan ^| giam-sat ^| sao-luu [thu-muc-chep-them] ^|
rem                                               phuc-hoi [ban] ^| tao-chung-chi ^| chay-kiem-thu
rem  Ben duoi goi cac script cua he thong: MicroServices\khoi_dong_tat_ca.ps1 (Docker/RabbitMQ + SQL Server +
rem  13 dich vu .NET + AI Python + API gateway), dung_microservices.ps1, giam_sat.ps1, sao_luu.ps1, phuc_hoi.ps1,
rem  tao_chung_chi.ps1, bo kiem thu tests\SmartPort.KiemThu va bo kiem thu AI trong SmartPort.AI.
rem  Can PowerShell 7 (pwsh), .NET 10 SDK, SQL Server. Docker Desktop: thieu / tat thi he thong CHAY SUY GIAM (khong RabbitMQ).
rem  Ma thoat kiem-thu / kiem-thu-nhanh: 0 = dat het, 1 = co kiem thu LOI, 3 = CHUA DU (co phan bi BO QUA).
rem  Ma thoat danh-gia-ai: 0 = da ghi JSON moi, 1 = danh gia loi (JSON cu giu nguyen).
rem  Ma thoat sao-luu / phuc-hoi: 0 = dat, 1 = loi, 2 = nguoi dung huy.
rem  chay-kiem-thu = chay he thong CHO BO KIEM THU (dinh-danh tra maDemo OTP cho yeu cau tu chinh may chu); chay / build /
rem  chay-lan luon TAT maDemo. kiem-thu canh bao neu he thong dang chay khong o che do kiem thu.
rem ============================================================================================
setlocal EnableExtensions
chcp 65001 >nul
title SmartPort - Bang dieu khien
cd /d "%~dp0"
set "GOC=%~dp0"
set "PS=pwsh -NoProfile -ExecutionPolicy Bypass"
set "AI=%GOC%SmartPort.AI"
set "PY=%GOC%SmartPort.AI\.venv\Scripts\python.exe"
set "KT=%GOC%tests\SmartPort.KiemThu\SmartPort.KiemThu.csproj"
set "NHATKY=%GOC%MicroServices\nhat-ky"
set "PYTHONIOENCODING=utf-8"
rem AI chay hoan toan cuc bo: cam ultralytics / Hugging Face tu do mang (mat Internet van chay)
set "YOLO_OFFLINE=1"
set "HF_HUB_OFFLINE=1"
set "TRANSFORMERS_OFFLINE=1"
if not exist "%NHATKY%" mkdir "%NHATKY%"

where pwsh >nul 2>&1
if errorlevel 1 goto thieu_pwsh

rem ---------------------------------------------------------------- goi kem lenh: chay 1 lenh roi thoat
if "%~1"=="" goto menu
call :lenh "%~1" "%~2"
rem tra RO ma thoat cua lenh: "exit /b" tran thi "cmd /c SMARTPORT.bat kiem-thu" luon ra 0
exit /b %errorlevel%

:thieu_pwsh
echo Can PowerShell 7 (pwsh): winget install Microsoft.PowerShell
pause
exit /b 1

:menu
cls
echo ============================================================
echo   SMARTPORT - HE THONG DIEU HANH CANG / DEPOT THONG MINH
echo ============================================================
echo   1. Chay toan bo he thong
echo   2. Build lai ma nguon roi chay
echo   3. Tat he thong
echo   4. Xem tinh trang tung dich vu
echo   5. Mo giao dien            http://localhost:5100
echo   6. Mo trang Canh bao nguy hiem AI   /admin/an-toan-ai
echo   7. Chay TOAN BO kiem thu (9 nhom C# + AI Python + giao dien + video dau-cuoi) - chon 15 truoc
echo   8. Kiem thu nhanh (khong can he thong dang chay)
echo   9. Danh gia lai AI do bao ho tren VAL/TEST (GPU ~10 phut; khong co GPU thi chay CPU, rat lau)
echo  ---- van hanh khi mat Wi-Fi / Internet ----
echo  10. Chay cho MANG NOI BO (may tram vao https://IP-may-chu:5443) - can tao chung chi truoc (14)
echo  11. Bat giam sat: tu chay lai dich vu / RabbitMQ bi chet (cua so rieng)
echo  12. Sao luu CSDL SmartPortMS_* + anh tai len + bi mat (giu 7 ban)
echo  13. Phuc hoi tu ban sao luu (hoi xac nhan; can tat he thong truoc)
echo  14. Tao chung chi HTTPS noi bo (CA + may chu; chi in lenh cai dat / firewall)
echo  15. Chay he thong CHE DO KIEM THU (cho muc 7: OTP demo tra ma cho chinh may chu; chay that dung muc 1)
echo   0. Thoat
echo ------------------------------------------------------------
set "chon="
set /p "chon=Chon (0-15): "
if "%chon%"=="0" exit /b 0
if "%chon%"=="1" call :lenh chay
if "%chon%"=="2" call :lenh build
if "%chon%"=="3" call :lenh tat
if "%chon%"=="4" call :lenh tinh-trang
if "%chon%"=="5" call :lenh mo
if "%chon%"=="6" call :lenh an-toan
if "%chon%"=="7" call :lenh kiem-thu
if "%chon%"=="8" call :lenh kiem-thu-nhanh
if "%chon%"=="9" call :lenh danh-gia-ai
if "%chon%"=="10" call :lenh chay-lan
if "%chon%"=="11" call :lenh giam-sat
if "%chon%"=="12" call :lenh sao-luu
if "%chon%"=="13" call :lenh phuc-hoi
if "%chon%"=="14" call :lenh tao-chung-chi
if "%chon%"=="15" call :lenh chay-kiem-thu
echo.
pause
goto menu

rem ================================================================ cac lenh
:lenh
if /i "%~1"=="chay"           goto chay
if /i "%~1"=="build"          goto build
if /i "%~1"=="tat"            goto tat
if /i "%~1"=="tinh-trang"     goto tinh_trang
if /i "%~1"=="mo"             goto mo
if /i "%~1"=="an-toan"        goto an_toan
if /i "%~1"=="kiem-thu"       goto kiem_thu
if /i "%~1"=="kiem-thu-nhanh" goto kiem_thu_nhanh
if /i "%~1"=="danh-gia-ai"    goto danh_gia_ai
if /i "%~1"=="chay-lan"       goto chay_lan
if /i "%~1"=="giam-sat"       goto giam_sat
if /i "%~1"=="sao-luu"        goto sao_luu
if /i "%~1"=="phuc-hoi"       goto phuc_hoi
if /i "%~1"=="tao-chung-chi"  goto tao_chung_chi
if /i "%~1"=="chay-kiem-thu"  goto chay_kiem_thu
echo Lenh khong hop le: %~1
echo Dung: SMARTPORT.bat [chay^|build^|tat^|tinh-trang^|mo^|an-toan^|kiem-thu^|kiem-thu-nhanh^|danh-gia-ai^|chay-lan^|giam-sat^|sao-luu^|phuc-hoi^|tao-chung-chi^|chay-kiem-thu]
exit /b 2

:chay
echo [Chay] Docker/RabbitMQ + SQL Server + 13 dich vu .NET + AI Python + API gateway...
%PS% -File "%GOC%MicroServices\khoi_dong_tat_ca.ps1"
exit /b

:build
echo [Build lai ma nguon + chay]
%PS% -File "%GOC%MicroServices\khoi_dong_tat_ca.ps1" -Build
exit /b

:chay_kiem_thu
echo [Chay CHE DO KIEM THU] nhu "chay" + dinh-danh tra maDemo OTP cho yeu cau tu chinh may chu (loopback) - de chay kiem-thu.
echo   Xong kiem thu: SMARTPORT.bat chay (khoi dong lai, maDemo TAT).
%PS% -File "%GOC%MicroServices\khoi_dong_tat_ca.ps1" -CheDoKiemThu
exit /b

:chay_lan
echo [Chay cho mang noi bo] gateway nghe them HTTPS 5443 bang chung chi MicroServices\.bimat\chung-chi (localhost:5100 giu nguyen)...
%PS% -File "%GOC%MicroServices\khoi_dong_tat_ca.ps1" -ChoPhepLan
exit /b

:tat
echo [Tat] giam sat + 13 dich vu .NET + AI Python + API gateway + RabbitMQ...
%PS% -File "%GOC%MicroServices\dung_microservices.ps1" -TatRabbitMQ
echo Da tat SmartPort. Docker Desktop va SQL Server van chay - tat rieng neu muon.
exit /b 0

:giam_sat
echo [Giam sat] mo cua so rieng "SmartPort - Giam sat" - moi 15 giay kiem /suc-khoe, chay lai dich vu chet (toi da 3 lan / 5 phut).
echo   Log: %NHATKY%\giam-sat.log   Tat: dong cua so do, hoac SMARTPORT.bat tat
start "SmartPort - Giam sat" %PS% -File "%GOC%MicroServices\giam_sat.ps1"
echo   Tu chay khi dang nhap Windows: %PS% -File "%GOC%MicroServices\giam_sat.ps1" -InLenhLich  (chi in lenh)
exit /b 0

:sao_luu
echo [Sao luu] CSDL SmartPortMS_* (BACKUP + VERIFYONLY) + Uploads + .bimat -^> MicroServices\sao-luu\
rem tham so 2 (tuy chon) = thu muc chep them (o dia / USB khac). Re nhanh bang nhan (khong dung khoi ngoac): duong dan co dau ngoac dong khong lam vo lenh
if not "%~2"=="" goto sao_luu_chep
%PS% -File "%GOC%MicroServices\sao_luu.ps1"
exit /b
:sao_luu_chep
%PS% -File "%GOC%MicroServices\sao_luu.ps1" -ChepSang "%~2"
exit /b

:phuc_hoi
echo [Phuc hoi] chi CSDL SmartPortMS_* - he thong phai dang tat (SMARTPORT.bat tat)
rem tham so 2 (tuy chon) = ban sao luu (vd 20261014_233000); bo trong thi liet ke de chon
if not "%~2"=="" goto phuc_hoi_ban
%PS% -File "%GOC%MicroServices\phuc_hoi.ps1"
exit /b
:phuc_hoi_ban
%PS% -File "%GOC%MicroServices\phuc_hoi.ps1" -Ban "%~2"
exit /b

:tao_chung_chi
echo [Tao chung chi HTTPS noi bo] chi ghi tep vao MicroServices\.bimat\chung-chi - KHONG cai vao Windows, KHONG mo firewall
%PS% -File "%GOC%MicroServices\tao_chung_chi.ps1"
exit /b

:tinh_trang
%PS% -Command "$d = Get-Content -Raw '%GOC%dich-vu.json' | ConvertFrom-Json; $ds = @([pscustomobject]@{ ten = 'gateway'; cong = $d.gateway.cong; tenHienThi = 'Cong API gateway (YARP)' }) + @($d.dichVu); $n = 0; foreach ($v in $ds) { try { $r = Invoke-RestMethod ('http://127.0.0.1:' + $v.cong + '/suc-khoe') -TimeoutSec 4; $t = 'chay'; if ($r.tinhTrang) { $t = $r.tinhTrang }; $n++ } catch { $t = 'TAT / LOI' }; '{0,-13} {1,5}  {2,-10} {3}' -f $v.ten, $v.cong, $t, $v.tenHienThi }; ''; 'Dang chay: ' + $n + '/' + $ds.Count; $k = '%GOC%MicroServices\pid\giam-sat.khoa'; $g = 'KHONG chay'; if (Test-Path $k) { $i = 0; if ([int]::TryParse((Get-Content $k -Raw), [ref]$i) -and (Get-Process -Id $i -ErrorAction SilentlyContinue)) { $g = 'dang chay (pid ' + $i + ')' } }; 'Giam sat: ' + $g"
exit /b 0

:mo
start "" http://localhost:5100
exit /b 0

:an_toan
start "" http://localhost:5100/admin/an-toan-ai
exit /b 0

:kiem_thu_nhanh
call :xay_kiem_thu
if errorlevel 1 exit /b 1
set "LOI=0"
set "BOQUA=0"
for %%g in (kien-truc ai mat-khau giay-to dang-nhap-ngoai) do call :mot_nhom %%g
call :python_an_toan
call :node_ngoai_tuyen
goto tong_ket

:kiem_thu
call :xay_kiem_thu
if errorlevel 1 exit /b 1
set "LOI=0"
set "BOQUA=0"
call :kiem_che_do_kiem_thu
for %%g in (kien-truc ai mat-khau giay-to dang-nhap-ngoai microservices ngoai-tuyen do-ben tien-bao-mat) do call :mot_nhom %%g
call :python_an_toan
call :node_ngoai_tuyen
call :video_dau_cuoi
echo.
echo   Nhom tich-hop KHONG chay o day: can CSDL rieng ten duoi _KiemThu - bien SMARTPORT_KIEMTHU_DB - va se ghi du lieu thu.
goto tong_ket

:tong_ket
rem co phan bi BO QUA (he thong chua chay, thieu du lieu) thi KHONG duoc bao dat het: in CHUA DU + ma thoat 3
echo.
echo ============================================================
if not "%LOI%"=="0" echo   CO KIEM THU LOI - xem chi tiet: %NHATKY%\kiem-thu-*.txt
if not "%BOQUA%"=="0" echo   CHUA DU: %BOQUA% phan bi BO QUA - xem cac dong BO QUA o tren; chon 15 chay he thong che do kiem thu roi chay lai
if "%LOI%%BOQUA%"=="00" echo   TAT CA KIEM THU DAT
echo ============================================================
if not "%LOI%"=="0" exit /b 1
if not "%BOQUA%"=="0" exit /b 3
exit /b 0

:kiem_che_do_kiem_thu
rem He thong dang chay ma dinh-danh KHONG o che do kiem thu -> cac kiem tra OTP (maDemo) se LOI: bao ro truoc, khong tu khoi dong lai.
%PS% -Command "try { Invoke-RestMethod http://127.0.0.1:5100/suc-khoe -TimeoutSec 4 | Out-Null } catch { exit 0 }; $c = $null; try { $c = Get-Content -Raw -ErrorAction Stop '%GOC%MicroServices\pid\che-do.json' | ConvertFrom-Json } catch { }; $p = ''; try { $p = (Get-Content -Raw -ErrorAction Stop '%GOC%MicroServices\pid\dinh-danh.pid').Trim() } catch { }; if ($c -and $c.cheDoKiemThuPid -and ('' + $c.cheDoKiemThuPid) -eq $p) { '  He thong dang o CHE DO KIEM THU (dinh-danh pid ' + $p + '). Xong thi chay that: SMARTPORT.bat chay'; exit 0 }; '  CANH BAO: he thong dang chay KHONG o che do kiem thu -> kiem tra OTP (maDemo) nhom microservices se LOI.'; '  Chay lai: SMARTPORT.bat chay-kiem-thu (menu 15) roi kiem-thu lai.'; exit 0"
exit /b 0

:xay_kiem_thu
echo [Build bo kiem thu]
dotnet build "%KT%" -c Debug -v q -nologo
exit /b

:mot_nhom
echo.
echo ===== Nhom %1 =====
dotnet run --no-build --project "%KT%" -- %1 > "%NHATKY%\kiem-thu-%1.txt" 2>&1
if errorlevel 1 set "LOI=1"
findstr /c:"KET QUA" /c:"  LOI " "%NHATKY%\kiem-thu-%1.txt"
call :dem_bo_qua "%NHATKY%\kiem-thu-%1.txt"
exit /b 0

:dem_bo_qua
rem dong bat dau bang "BO QUA" co dau (bo.Ghi cua bo kiem thu C#) = 1 phan KHONG chay: in ra va tang BOQUA.
rem Mau regex viet \u1ECE de file .bat giu thuan ASCII; -CaseSensitive: ten kiem thu chua chu "bo qua" thuong khong tinh.
set "TEP_BQ=%~1"
%PS% -Command "$m = @(Select-String -LiteralPath $env:TEP_BQ -Pattern '^\s+B\u1ECE QUA' -CaseSensitive -ErrorAction SilentlyContinue); foreach ($x in $m) { '  ' + $x.Line.Trim() }; if ($m.Count -gt 0) { exit 3 }; exit 0"
if errorlevel 3 set /a BOQUA+=1
exit /b 0

:python_an_toan
echo.
echo ===== AI canh bao nguy hiem: cac kich ban tren camera ao - khong can GPU =====
"%PY%" "%AI%\kiem_thu_an_toan.py" > "%NHATKY%\kiem-thu-an-toan.txt" 2>&1
if errorlevel 1 set "LOI=1"
findstr /c:"KET QUA" /c:"  LOI " "%NHATKY%\kiem-thu-an-toan.txt"
echo.
echo ===== AI do bao ho: vung xet cua bo danh gia khop may canh bao that - khong can GPU =====
"%PY%" "%AI%\danh_gia_bao_ho_nguoi.py" --tu-kiem > "%NHATKY%\kiem-thu-vung-bao-ho.txt" 2>&1
if errorlevel 1 set "LOI=1"
findstr /c:"KET QUA" /c:"  LOI " "%NHATKY%\kiem-thu-vung-bao-ho.txt"
echo.
echo ===== AI dich vu canh bao: phien, gioi han anh / cau hinh, thu hoi token - bo phat hien gia, khong can GPU =====
pushd "%AI%"
"%PY%" kiem_thu_dich_vu_an_toan.py > "%NHATKY%\kiem-thu-dich-vu-an-toan.txt" 2>&1
if errorlevel 1 set "LOI=1"
popd
findstr /c:"KET QUA" /c:"  LOI " "%NHATKY%\kiem-thu-dich-vu-an-toan.txt"
echo.
echo ===== Giao dien canh bao nguy hiem: logic gui khung, chong spam, don dep - can Node.js =====
where node >nul 2>&1
if errorlevel 1 goto giao_dien_bo_qua
node "%GOC%smartport-web\kiem-thu\canh-bao-nguy-hiem.mjs" > "%NHATKY%\kiem-thu-giao-dien-an-toan.txt" 2>&1
if errorlevel 1 set "LOI=1"
findstr /c:"DAT" /c:"TRUOT" "%NHATKY%\kiem-thu-giao-dien-an-toan.txt"
exit /b 0
:giao_dien_bo_qua
echo   BO QUA: chua cai Node.js
set /a BOQUA+=1
exit /b 0

:node_ngoai_tuyen
echo.
echo ===== Tram ngoai tuyen: hang doi gui sau, dong bo 1 luong, chi xep hang khi loi mang - can Node.js =====
where node >nul 2>&1
if errorlevel 1 goto ngoai_tuyen_bo_qua_node
if not exist "%GOC%smartport-web\kiem-thu\ngoai-tuyen.mjs" goto ngoai_tuyen_bo_qua_tep
node "%GOC%smartport-web\kiem-thu\ngoai-tuyen.mjs" > "%NHATKY%\kiem-thu-ngoai-tuyen.txt" 2>&1
if errorlevel 1 set "LOI=1"
findstr /c:"KET QUA" /c:"DAT" /c:"TRUOT" /c:"  LOI " "%NHATKY%\kiem-thu-ngoai-tuyen.txt"
exit /b 0
:ngoai_tuyen_bo_qua_node
echo   BO QUA: chua cai Node.js
set /a BOQUA+=1
exit /b 0
:ngoai_tuyen_bo_qua_tep
echo   BO QUA: chua co smartport-web\kiem-thu\ngoai-tuyen.mjs
set /a BOQUA+=1
exit /b 0

:video_dau_cuoi
echo.
echo ===== AI canh bao nguy hiem: video dau-cuoi qua gateway, so voi dap an =====
%PS% -Command "try { Invoke-RestMethod http://127.0.0.1:5100/suc-khoe -TimeoutSec 4 | Out-Null; exit 0 } catch { exit 1 }"
if errorlevel 1 goto video_bo_qua
"%PY%" "%AI%\kiem_thu_an_toan_video.py" > "%NHATKY%\kiem-thu-an-toan-video.txt" 2>&1
if errorlevel 1 set "LOI=1"
findstr /c:"KET QUA" /c:"  LOI " /c:"khung " /c:"Video ket qua" "%NHATKY%\kiem-thu-an-toan-video.txt"
exit /b 0
:video_bo_qua
echo   BO QUA: video dau-cuoi - he thong chua chay, chon 15 truoc.
set /a BOQUA+=1
exit /b 0

:danh_gia_ai
echo [Danh gia do bao ho o muc tung nguoi: ca khung vs cat nguoi, nguoi xa / gan, khung nen JPEG]
set "MH=bao_ho"
if exist "%AI%\runs\bao_ho_ben\weights\best.pt" set "MH=bao_ho bao_ho_ben"
pushd "%AI%"
"%PY%" danh_gia_bao_ho_khoang_cach.py %MH%
set "MA=%errorlevel%"
popd
if not "%MA%"=="0" echo LOI: danh gia that bai - ma %MA%. JSON cu KHONG duoc cap nhat, trang /admin/an-toan-ai van hien so cu.
if not "%MA%"=="0" exit /b 1
echo Ket qua: SmartPort.AI\runs\danh_gia\bao_ho_theo_khoang_cach.json
exit /b 0
