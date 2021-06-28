# Find Text Transform
# Will need updated for 64-bit visual studio!

$rootDir = "C:\Program Files (x86)\Microsoft Visual Studio"
$transformPath = $null

$yearlyEditions = Get-ChildItem $rootDir -Directory
foreach($yedition in $yearlyEditions){
    if ($null -eq $transformPath) {
        [int]$yearver = 0
        if  ([int]::TryParse($yedition.Name, [ref]$yearver)){
            $tiers = Get-ChildItem $yedition.FullName -Directory
            foreach($tier in $tiers){
                $testTextTransform = [IO.Path]::Combine($tier.FullName, "Common7", "IDE", "TextTransform.exe")
                if (Test-Path $testTextTransform){
                    $transformPath = $testTextTransform
                    break
                }
            }
        }
    }
}

if ($null -ne $transformPath) {
    $files = Get-ChildItem -Path "$PSScriptRoot\.." -Filter *.tt -Recurse -File
    foreach($file in $files) {
        Write-Host "Running text transform on $($file.FullName)"
        Start-Process $transformPath -ArgumentList $file.FullName -NoNewWindow -Wait
    }
}
