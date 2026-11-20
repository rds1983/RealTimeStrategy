# Exports every Myra .xmmp UI file in RealTimeStrategy/Presentation/Ui to C#.
# Namespace/class come from Project.ExportOptions inside each .xmmp; output goes next to the file.
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$uiRoot = Join-Path $root 'RealTimeStrategy/Presentation/Ui'

Push-Location $root
try {
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'dotnet tool restore failed.' }

    Get-ChildItem -Path $uiRoot -Filter '*.xmmp' -File -Recurse | ForEach-Object {
        dotnet tool run myra-export $_.FullName
        if ($LASTEXITCODE -ne 0) { throw "myra-export failed for $($_.Name)." }
    }
}
finally {
    Pop-Location
}
