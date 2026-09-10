# Render trang PDF -> PNG bang Windows.Data.Pdf (WinRT)
param([string]$Pdf, [string]$OutDir, [int]$MaxPages = 1)
Add-Type -AssemblyName System.Runtime.WindowsRuntime
$null = [Windows.Storage.StorageFile, Windows.Storage, ContentType=WindowsRuntime]
$null = [Windows.Data.Pdf.PdfDocument, Windows.Data.Pdf, ContentType=WindowsRuntime]
$null = [Windows.Storage.Streams.InMemoryRandomAccessStream, Windows.Storage.Streams, ContentType=WindowsRuntime]

$rtExt = [System.WindowsRuntimeSystemExtensions].GetMethods()
function AwaitOp($op, $t) {
    $m = ($rtExt | Where-Object { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' })[0]
    $g = $m.MakeGenericMethod($t); $task = $g.Invoke($null, @($op)); $task.Wait(-1) | Out-Null; $task.Result
}
function AwaitAct($op) {
    $m = ($rtExt | Where-Object { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncAction' })[0]
    $task = $m.Invoke($null, @($op)); $task.Wait(-1) | Out-Null
}

$sf = AwaitOp ([Windows.Storage.StorageFile]::GetFileFromPathAsync($Pdf)) ([Windows.Storage.StorageFile])
$pdfDoc = AwaitOp ([Windows.Data.Pdf.PdfDocument]::LoadFromFileAsync($sf)) ([Windows.Data.Pdf.PdfDocument])
$n = [Math]::Min($MaxPages, $pdfDoc.PageCount)
for ($i = 0; $i -lt $n; $i++) {
    $page = $pdfDoc.GetPage($i)
    $stream = New-Object Windows.Storage.Streams.InMemoryRandomAccessStream
    $opt = New-Object Windows.Data.Pdf.PdfPageRenderOptions
    $opt.DestinationWidth = [uint32]1240
    AwaitAct ($page.RenderToStreamAsync($stream, $opt))
    $reader = New-Object Windows.Storage.Streams.DataReader($stream.GetInputStreamAt(0))
    $size = [uint32]$stream.Size
    AwaitOp ($reader.LoadAsync($size)) ([uint32]) | Out-Null
    $bytes = New-Object byte[] $size
    $reader.ReadBytes($bytes)
    [IO.File]::WriteAllBytes((Join-Path $OutDir ("pg{0}.png" -f ($i+1))), $bytes)
    $page.Dispose()
}
"Rendered $n page(s)"
