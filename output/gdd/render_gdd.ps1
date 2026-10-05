$ErrorActionPreference = 'Stop'
$gddWord = $null
$gddDocument = $null
try {
  $gddWord = New-Object -ComObject Word.Application
  $gddWord.Visible = $false
  $gddWord.DisplayAlerts = 0
  $gddWord.AutomationSecurity = 3
  $gddDocument = $gddWord.Documents.Open('C:\Users\elian\Mismo\output\gdd\Mismo_GDD_v0.9.docx', $false, $true)
  $gddDocument.ExportAsFixedFormat('C:\Users\elian\Mismo\output\gdd\Mismo_GDD_v0.9-qa.pdf', 17)
  Write-Output ('Updated pages: ' + $gddDocument.ComputeStatistics(2))
} finally {
  if ($null -ne $gddDocument) { $gddDocument.Close(0); [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($gddDocument) }
  if ($null -ne $gddWord) { $gddWord.Quit(); [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($gddWord) }
}
