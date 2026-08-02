[CmdletBinding()]
param(
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repositoryRoot

try {
    $tracked = @(git ls-files --cached --others --exclude-standard)
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not enumerate tracked files.'
    }

    $forbiddenFiles = @($tracked | Where-Object {
        $_ -match '(^|/)(bin|obj|BepInEx|Managed|logs?)/' -or
        $_ -match '\.(dll|exe|pdb|zip|7z|rar|log)$'
    })
    if ($forbiddenFiles.Count -gt 0) {
        throw "Public release contains forbidden generated or binary files:`n$($forbiddenFiles -join "`n")"
    }

    $textFiles = @($tracked | Where-Object {
        $_ -match '\.(cs|csproj|props|sln|md|json|yml|yaml|ps1|txt)$'
    })
    if ($textFiles.Count -gt 0) {
        $privateMatches = @(
            Select-String -Path $textFiles -Pattern @(
                'E:\\zeep-fps',
                'C:\\Users\\Chris',
                '(?<!\d)7656119\d{10}(?!\d)',
                '-----BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY-----'
            ) -AllMatches
        )
        if ($privateMatches.Count -gt 0) {
            $locations = $privateMatches | ForEach-Object {
                "$($_.Path):$($_.LineNumber)"
            }
            throw "Public release hygiene scan found private-looking content:`n$($locations -join "`n")"
        }
    }

    git diff --check
    if ($LASTEXITCODE -ne 0) {
        throw 'git diff --check failed.'
    }

    if (-not $SkipBuild) {
        dotnet build miniZeep.sln -c Release
        if ($LASTEXITCODE -ne 0) {
            throw 'Release build failed.'
        }
    }

    Write-Host 'miniZeep release verification passed.' -ForegroundColor Green
}
finally {
    Pop-Location
}
