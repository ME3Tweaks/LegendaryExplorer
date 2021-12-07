param($outpath)

$doxygenUrl = ${env:DOXYGENURL}
if($null -eq $doxygenUrl)
{
    $doxygenUrl = "https://www.doxygen.nl/files/doxygen-1.9.2.windows.x64.bin.zip"
}
$outpath = $outpath.trim()

if(!(Test-Path $outpath))
{
      New-Item -ItemType Directory -Force -Path $outpath
}
$zipDownloadPath = $outpath + $(Split-Path -Path $doxygenUrl -Leaf)

Invoke-WebRequest -Uri $doxygenUrl -OutFile $zipDownloadPath
Expand-Archive -LiteralPath $zipDownloadPath -DestinationPath $outpath
Remove-Item $zipDownloadPath
