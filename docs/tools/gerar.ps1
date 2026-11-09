# Regenera as figuras e o artigo do TCC; opcionalmente exporta PDF pelo Word.
# Uso (a partir de docs/tools):  .\gerar.ps1          -> gera figuras + .docx
#                                .\gerar.ps1 -Pdf     -> também exporta docs/TCC_AntRoute_Artigo.pdf
param([switch]$Pdf)
$ErrorActionPreference = "Stop"
$here = $PSScriptRoot
$docx = Join-Path (Split-Path $here) "TCC_AntRoute_Artigo.docx"

if (-not (Test-Path (Join-Path $here "node_modules\docx"))) { Push-Location $here; npm install --no-audit --no-fund; Pop-Location }
python (Join-Path $here "figs.py")
python (Join-Path $here "fig_sens.py")   # mapa de calor beta x rho (precisa de datasets/vitoria-es/runs.csv)
python (Join-Path $here "fig_conv.py")   # convergencia do ACO (precisa de datasets/comparacao/convergence.csv)
node (Join-Path $here "build_tcc.js") $docx

if ($Pdf) {
    $pdfPath = [IO.Path]::ChangeExtension($docx, ".pdf")
    $word = New-Object -ComObject Word.Application
    try {
        $d = $word.Documents.Open($docx, $false, $true)
        $d.ExportAsFixedFormat($pdfPath, 17)
        $d.Close($false)
    } finally { $word.Quit() }
    Write-Host "PDF: $pdfPath"
}
