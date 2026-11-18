# -*- coding: utf-8 -*-
"""
TAO PROJECT CHO TUNG DICH VU (chay 1 lan khi tach; chay lai an toan — khong ghi de Program.cs da sua tay):
  Web/DichVu/SmartPort.DichVu.<Ten>/
      SmartPort.DichVu.<Ten>.csproj   (Web SDK, tham chieu NenTang + module can thiet)
      Program.cs                      (vai dong: ThemNenTang("<ten>") + dang ky module + DungNenTang)
      appsettings.json                (cong rieng, CSDL rieng SmartPortMS_<Ten>, RabbitMQ)
      Controllers/<Khu>/...           (CHUYEN tu SmartPort.Api/Controllers — moi controller thuoc DUNG MOT dich vu)
  SmartPort.Api (ban gop) bien dich lai moi controller do bang lien ket (Compile Include) nen giao dien khong doi.
"""
import json
import os
import shutil
import sys

WEB = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
API_CTRL = os.path.join(WEB, "SmartPort.Api", "Controllers")

# khu (thu muc) / file controller -> dich vu  (khop voi phan tich ghep noi khi lap dich-vu.json)
PHAN_CONG = {
    "dinh-danh": ["AuthController.cs", "QuenMatKhauController.cs", "HeThongController.cs", "TaiKhoanKhach", "HeThong"],
    "kinh-doanh": ["KinhDoanh", "DichVuKhach", "CongKhai", "TienIchCongKhai", "ThuTien/DoiSoatThanhToanController.cs"],
    "tau-ben": ["TauBen"],
    "bai": ["BaiContainer", "ContainerController.cs", "EdiDepot/DepotController.cs", "EdiDepot/DepotRongController.cs"],
    "cong": ["CongController.cs", "CongVaLenh", "TaiXeDoiSoat", "TichHopDoiTac"],
    "tai-chinh": ["ThuTien"],
    "an-ninh": ["AnNinh", "KiemDemDms", "ThiGiacAi"],
    "tich-hop": ["EdiDepot/EdiController.cs", "EdiDepot/EdiHangTauController.cs"],
    "van-hanh": ["AiVanHanh", "DieuHanh", "TongQuanController.cs", "TroLyController.cs", "AiLabController.cs"],
    "thong-bao": ["SuKienController.cs"],
    # dich vu MOI (cang so): controller viet thang trong project dich vu
    "lich-hen": [], "edo": [], "hai-quan": [],
}
MODULE = {
    "cong": ["Cong"], "bai": ["Bai"], "tai-chinh": ["TaiChinh"], "thong-bao": ["ThongBao"],
    "kinh-doanh": ["Cong"],          # bang tinh trang cong (ICongTruyVan) doc ban sao luot xe
    "dinh-danh": ["ThongBao"],       # trang van hanh he thong hien so nguoi nghe realtime
    "lich-hen": ["CangSo"], "edo": ["CangSo"], "hai-quan": ["CangSo"],
}
DANG_KY = {
    "Cong": ".AddModuleCong()", "Bai": ".AddModuleBai()", "TaiChinh": ".AddModuleTaiChinh()",
}


def pascal(ten):
    return "".join(p.capitalize() for p in ten.split("-"))


def ghi_neu_chua_co(duong, noi_dung, bom=False):
    if os.path.exists(duong):
        return False
    with open(duong, "w", encoding="utf-8-sig" if bom else "utf-8") as f:
        f.write(noi_dung)
    return True


def main():
    dm = json.load(open(os.path.join(WEB, "dich-vu.json"), encoding="utf-8"))
    for dv in dm["dichVu"]:
        if dv.get("nenTang", "dotnet") != "dotnet":
            continue
        ten, P = dv["ten"], pascal(dv["ten"])
        goc = os.path.join(WEB, "DichVu", "SmartPort.DichVu." + P)
        os.makedirs(os.path.join(goc, "Properties"), exist_ok=True)
        mods = MODULE.get(ten, [])

        ref = "\n".join('    <ProjectReference Include="..\\..\\SmartPort.Modules.%s\\SmartPort.Modules.%s.csproj" />' % (m, m) for m in mods)
        ghi_neu_chua_co(os.path.join(goc, "SmartPort.DichVu.%s.csproj" % P), """<Project Sdk="Microsoft.NET.Sdk.Web">

  <!--
    DICH VU %(hien)s — MICROSERVICE: tien trinh rieng (cong %(cong)d), CSDL rieng (%(csdl)s),
    chi ghi bang minh so huu; doc du lieu dich vu khac qua BAN SAO cap nhat bang su kien RabbitMQ.
    %(mota)s
  -->
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>disable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    <RootNamespace>SmartPort.Api</RootNamespace>
    <AssemblyName>SmartPort.DichVu.%(P)s</AssemblyName>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>$(NoWarn);CS1591;CS0618</NoWarn>
    <InvariantGlobalization>false</InvariantGlobalization>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\\..\\SmartPort.NenTang\\SmartPort.NenTang.csproj" />
    <ProjectReference Include="..\\..\\SmartPort.Contracts\\SmartPort.Contracts.csproj" />
%(ref)s
  </ItemGroup>

</Project>
""" % dict(hien=dv["tenHienThi"].upper(), cong=dv["cong"], csdl=dv["coSoDuLieu"], mota=dv.get("moTa", ""), P=P, ref=ref))

        usings = "".join("using SmartPort.Modules.%s;\n" % m for m in mods if m in DANG_KY)
        dang_ky = "".join("\n    " + DANG_KY[m] for m in mods if m in DANG_KY)
        ghi_neu_chua_co(os.path.join(goc, "Program.cs"), """using Microsoft.AspNetCore.Builder;
using SmartPort.Api.HaTang;
using SmartPort.BuildingBlocks;
using SmartPort.Contracts.Cong;
using SmartPort.Models;
%(usings)s
// =====================================================================
// DICH VU %(hien)s (microservice) — cong %(cong)d, CSDL %(csdl)s
// %(mota)s
// Nen tang chung (JWT, Swagger, /suc-khoe, bus RabbitMQ, kiem tra so huu bang) nam o SmartPort.NenTang.
// =====================================================================
var builder = WebApplication.CreateBuilder(args);
var mc = builder.ThemNenTang("%(ten)s");
builder.Services.AddSuKienTichHop<AppDb>(typeof(XeDaQuaCong).Assembly, bus: mc.Bus)%(dk)s;

var app = builder.Build();
app.DungNenTang(mc);
app.Run();
""" % dict(usings=usings, hien=dv["tenHienThi"].upper(), cong=dv["cong"], csdl=dv["coSoDuLieu"], mota=dv.get("moTa", ""),
           ten=ten, dk=dang_ky))

        ghi_neu_chua_co(os.path.join(goc, "appsettings.json"), json.dumps({
            "Logging": {"LogLevel": {"Default": "Information", "Microsoft.AspNetCore": "Warning", "Microsoft.EntityFrameworkCore": "Warning",
                                     "Yarp": "Warning"}},
            "AllowedHosts": "*",
            "Urls": "http://localhost:%d" % dv["cong"],
            "ConnectionStrings": {"AppDb": "Data Source=.\\MSSQLSERVER01;Initial Catalog=%s;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True" % dv["coSoDuLieu"]},
            "Bus": {"CheDo": "RabbitMQ"},
        }, ensure_ascii=False, indent=2))
        ghi_neu_chua_co(os.path.join(goc, "Properties", "launchSettings.json"), json.dumps({
            "profiles": {ten: {"commandName": "Project", "launchBrowser": False, "applicationUrl": "http://localhost:%d" % dv["cong"],
                               "environmentVariables": {"ASPNETCORE_ENVIRONMENT": "Development"}}}}, indent=2))

        # chuyen controller
        so = 0
        for muc in PHAN_CONG[ten]:
            nguon = os.path.join(API_CTRL, muc.replace("/", os.sep))
            dich = os.path.join(goc, "Controllers", muc.replace("/", os.sep))
            if not os.path.exists(nguon):
                continue
            os.makedirs(os.path.dirname(dich), exist_ok=True)
            if os.path.isdir(nguon):
                if os.path.exists(dich):
                    for f in os.listdir(nguon):
                        shutil.move(os.path.join(nguon, f), os.path.join(dich, f))
                    os.rmdir(nguon)
                else:
                    shutil.move(nguon, dich)
            else:
                shutil.move(nguon, dich)
            so += 1
        print("%-11s -> DichVu/SmartPort.DichVu.%-11s chuyen %d muc controller | module: %s" % (ten, P, so, ", ".join(mods) or "-"))

    con = [os.path.relpath(os.path.join(r, f), API_CTRL) for r, _, fs in os.walk(API_CTRL) for f in fs]
    print("Controller con lai trong SmartPort.Api:", con)
    for r, ds, fs in os.walk(API_CTRL, topdown=False):
        if not ds and not fs:
            os.rmdir(r)


if __name__ == "__main__":
    sys.exit(main())
