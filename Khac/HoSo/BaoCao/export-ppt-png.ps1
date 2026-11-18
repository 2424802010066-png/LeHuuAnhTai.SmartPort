$ErrorActionPreference = 'Stop'
$outPptx = 'C:\Users\ASUS\Downloads\SmartPort\BaoCao\Slide_ThuyetTrinh_SmartPort.pptx'
$outPng  = 'C:\Users\ASUS\Downloads\SmartPort\BaoCao\_qa_ppt'
if (Test-Path $outPng) { Remove-Item $outPng -Recurse -Force }
New-Item -ItemType Directory -Force $outPng | Out-Null
$pp = New-Object -ComObject PowerPoint.Application
$pres = $pp.Presentations.Open($outPptx, 0, 0, 0)
$pres.Export($outPng, 'PNG', 1280, 720)
$n = $pres.Slides.Count
$pres.Close()
$pp.Quit()
"Exported $n slides"
