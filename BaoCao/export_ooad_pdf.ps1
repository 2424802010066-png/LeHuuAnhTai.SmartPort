$ErrorActionPreference = 'Stop'
$inDocx = "C:\Users\ASUS\Downloads\SmartPort\BaoCao\BaoCao_OOAD_SmartPort_LeHuuAnhTai_2424802010066.docx"
$outPdf = "C:\Users\ASUS\Downloads\SmartPort\BaoCao\BaoCao_OOAD_SmartPort_LeHuuAnhTai_2424802010066.pdf"

try {
    $word = New-Object -ComObject Word.Application
    $word.Visible = $false
    $word.DisplayAlerts = 0
    $doc = $word.Documents.Open($inDocx)
    # 17 corresponds to wdExportFormatPDF
    $doc.SaveAs([ref]$outPdf, [ref]17)
    $doc.Close([ref]0)
    $word.Quit()
    [System.Runtime.Interopservices.Marshal]::ReleaseComObject($word) | Out-Null
    Write-Host "SUCCESS: Exported PDF at $outPdf"
} catch {
    Write-Host "Word COM PDF export error: $_"
}
