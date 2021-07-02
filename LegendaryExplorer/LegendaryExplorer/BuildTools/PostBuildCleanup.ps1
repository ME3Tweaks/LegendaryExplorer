param($outpath)

$outpath = $outpath.trim()

# Cleanup 'runtimes'
Write-Host Cleaning up $outpath
$runtimes = Get-ChildItem (Join-Path -Path $outpath -ChildPath "runtimes") -Directory
foreach($runtime in $runtimes){
    if ($runtime.Name -ne "win-x64" -and $runtime.Name -ne "win"){
        Remove-Item $runtime.FullName -Recurse
    }
}
