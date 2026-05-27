[CmdletBinding()]
param(
    [switch]$SkipAndroid,
    [switch]$SkipDesktop
)

Set-StrictMode -Version Latest

$script:Results = New-Object System.Collections.Generic.List[object]

function Write-Status {
    param(
        [Parameter(Mandatory)][ValidateSet('OK', 'FAIL', 'WARN', 'SKIP')][string]$Status,
        [Parameter(Mandatory)][string]$Name,
        [string]$Detail = '',
        [string]$Hint = ''
    )

    $tag = switch ($Status) {
        'OK'   { '[ OK ]' }
        'FAIL' { '[FAIL]' }
        'WARN' { '[WARN]' }
        'SKIP' { '[SKIP]' }
    }
    $color = switch ($Status) {
        'OK'   { 'Green' }
        'FAIL' { 'Red' }
        'WARN' { 'Yellow' }
        'SKIP' { 'DarkGray' }
    }

    $line = ('{0}   {1,-16} {2}' -f $tag, $name, $Detail)
    Write-Host $line -ForegroundColor $color
    if ($Hint -ne '') {
        Write-Host ('         -> {0}' -f $Hint) -ForegroundColor $color
    }

    $script:Results.Add([pscustomobject]@{ Status = $Status; Name = $Name })
}

function Test-Command {
    param([Parameter(Mandatory)][string]$Name)
    $null -ne (Get-Command -Name $Name -ErrorAction SilentlyContinue)
}

Write-Host ''
Write-Host '== rAspCoreVueLauncher setup check (Windows) ==' -ForegroundColor Cyan
Write-Host ''

try {
    $ErrorActionPreference = 'Stop'
    if (-not (Test-Command 'dotnet')) {
        Write-Status -Status 'FAIL' -Name '.NET SDK' -Detail 'not found' `
            -Hint 'Install: https://dot.net   |   winget install Microsoft.DotNet.SDK.10'
    } else {
        $sdks = & dotnet --list-sdks 2>$null
        $match = $sdks | Where-Object { $_ -like '10.0*' } | Select-Object -First 1
        if ($match) {
            $version = ($match -split ' ')[0]
            Write-Status -Status 'OK' -Name '.NET SDK' -Detail $version
        } else {
            $have = if ($sdks) { ($sdks -join ', ') } else { 'none' }
            Write-Status -Status 'FAIL' -Name '.NET SDK' -Detail "10.x not found (have: $have)" `
                -Hint 'Install: https://dot.net   |   winget install Microsoft.DotNet.SDK.10'
        }
    }
} catch {
    Write-Status -Status 'FAIL' -Name '.NET SDK' -Detail $_.Exception.Message `
        -Hint 'Install: https://dot.net   |   winget install Microsoft.DotNet.SDK.10'
}

try {
    $ErrorActionPreference = 'Stop'
    if (-not (Test-Command 'node')) {
        Write-Status -Status 'FAIL' -Name 'Node.js' -Detail 'not found' `
            -Hint 'Install: https://nodejs.org   |   winget install OpenJS.NodeJS.LTS'
    } else {
        $nodeVer = (& node --version).Trim()
        $major = [int]($nodeVer.TrimStart('v').Split('.')[0])
        if ($major -ge 22) {
            Write-Status -Status 'OK' -Name 'Node.js' -Detail $nodeVer
        } else {
            Write-Status -Status 'FAIL' -Name 'Node.js' -Detail "$nodeVer (need >= 22)" `
                -Hint 'Install: https://nodejs.org   |   winget install OpenJS.NodeJS.LTS'
        }
    }
} catch {
    Write-Status -Status 'FAIL' -Name 'Node.js' -Detail $_.Exception.Message `
        -Hint 'Install: https://nodejs.org   |   winget install OpenJS.NodeJS.LTS'
}

try {
    $ErrorActionPreference = 'Stop'
    if (-not (Test-Command 'npm')) {
        Write-Status -Status 'FAIL' -Name 'npm' -Detail 'not found' `
            -Hint 'Ships with Node.js. Install: https://nodejs.org   |   winget install OpenJS.NodeJS.LTS'
    } else {
        $npmVer = (& npm --version).Trim()
        Write-Status -Status 'OK' -Name 'npm' -Detail $npmVer
    }
} catch {
    Write-Status -Status 'FAIL' -Name 'npm' -Detail $_.Exception.Message `
        -Hint 'Ships with Node.js. Install: https://nodejs.org   |   winget install OpenJS.NodeJS.LTS'
}

if ($SkipDesktop) {
    Write-Status -Status 'SKIP' -Name 'Rust' -Detail '(skipped: -SkipDesktop)'
    Write-Status -Status 'SKIP' -Name 'MSVC build tools' -Detail '(skipped: -SkipDesktop)'
    Write-Status -Status 'SKIP' -Name 'WebView2' -Detail '(skipped: -SkipDesktop)'
} else {
    try {
        $ErrorActionPreference = 'Stop'
        if ((Test-Command 'rustc') -and (Test-Command 'cargo')) {
            $rustcVer = ((& rustc --version) -split ' ')[1]
            Write-Status -Status 'OK' -Name 'Rust' -Detail $rustcVer
        } else {
            Write-Status -Status 'FAIL' -Name 'Rust' -Detail 'not found' `
                -Hint 'Install: https://rustup.rs   |   winget install Rustlang.Rustup'
        }
    } catch {
        Write-Status -Status 'FAIL' -Name 'Rust' -Detail $_.Exception.Message `
            -Hint 'Install: https://rustup.rs   |   winget install Rustlang.Rustup'
    }

    try {
        $ErrorActionPreference = 'Stop'
        $msvcDetail = $null
        if (Test-Command 'link.exe') {
            $msvcDetail = 'link.exe on PATH'
        } else {
            $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
            if (Test-Path -LiteralPath $vswhere) {
                $found = & $vswhere -products '*' -requires 'Microsoft.VisualStudio.Component.VC.Tools.x86.x64' -latest -property displayName 2>$null
                if ($found) { $msvcDetail = ($found | Select-Object -First 1).ToString() }
            }
            if (-not $msvcDetail) {
                $buildToolsDir = Join-Path $env:ProgramFiles 'Microsoft Visual Studio\2022\BuildTools'
                if (Test-Path -LiteralPath $buildToolsDir) {
                    $msvcDetail = 'Visual Studio Build Tools 2022'
                }
            }
        }
        if ($msvcDetail) {
            Write-Status -Status 'OK' -Name 'MSVC build tools' -Detail $msvcDetail
        } else {
            Write-Status -Status 'WARN' -Name 'MSVC build tools' -Detail 'not detected' `
                -Hint 'Install: https://visualstudio.microsoft.com/downloads/ (Build Tools 2022 + "Desktop development with C++")'
        }
    } catch {
        Write-Status -Status 'WARN' -Name 'MSVC build tools' -Detail $_.Exception.Message `
            -Hint 'Install: https://visualstudio.microsoft.com/downloads/ (Build Tools 2022 + "Desktop development with C++")'
    }

    try {
        $ErrorActionPreference = 'Stop'
        # WebView2 evergreen runtime registers under this GUID
        $wv2Keys = @(
            'HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}',
            'HKLM:\SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}'
        )
        $wv2Version = $null
        foreach ($k in $wv2Keys) {
            if (Test-Path -LiteralPath $k) {
                $props = Get-ItemProperty -LiteralPath $k -ErrorAction SilentlyContinue
                if ($props -and $props.PSObject.Properties.Name -contains 'pv') {
                    $wv2Version = $props.pv
                    break
                }
            }
        }
        if ($wv2Version) {
            Write-Status -Status 'OK' -Name 'WebView2' -Detail $wv2Version
        } else {
            Write-Status -Status 'WARN' -Name 'WebView2' -Detail 'not detected (preinstalled on Win11)' `
                -Hint 'Install: https://developer.microsoft.com/microsoft-edge/webview2/'
        }
    } catch {
        Write-Status -Status 'WARN' -Name 'WebView2' -Detail $_.Exception.Message `
            -Hint 'Install: https://developer.microsoft.com/microsoft-edge/webview2/'
    }
}

if ($SkipAndroid) {
    Write-Status -Status 'SKIP' -Name 'Java JDK' -Detail '(skipped: -SkipAndroid)'
    Write-Status -Status 'SKIP' -Name 'ANDROID_HOME' -Detail '(skipped: -SkipAndroid)'
    Write-Status -Status 'SKIP' -Name 'sdkmanager' -Detail '(skipped: -SkipAndroid)'
} else {
    $javaOk = $false
    try {
        $ErrorActionPreference = 'Stop'
        if (-not (Test-Command 'java')) {
            Write-Status -Status 'FAIL' -Name 'Java JDK' -Detail 'not found' `
                -Hint 'Install: https://learn.microsoft.com/java/openjdk/   |   winget install Microsoft.OpenJDK.17'
        } else {
            $javaOut = & java -version 2>&1 | Out-String
            $m = [regex]::Match($javaOut, 'version "([0-9._]+)"')
            if ($m.Success) {
                $verStr = $m.Groups[1].Value
                $majorPart = $verStr.Split('.')[0]
                $majorNum = [int]$majorPart
                # Legacy 1.x.y scheme means Java 8 or older
                if ($majorNum -eq 1) { $majorNum = [int]$verStr.Split('.')[1] }
                if ($majorNum -ge 17) {
                    Write-Status -Status 'OK' -Name 'Java JDK' -Detail $verStr
                    $javaOk = $true
                } else {
                    Write-Status -Status 'FAIL' -Name 'Java JDK' -Detail "$verStr (need >= 17)" `
                        -Hint 'Install: https://learn.microsoft.com/java/openjdk/   |   winget install Microsoft.OpenJDK.17'
                }
            } else {
                Write-Status -Status 'WARN' -Name 'Java JDK' -Detail 'version unparseable' `
                    -Hint 'Install: https://learn.microsoft.com/java/openjdk/   |   winget install Microsoft.OpenJDK.17'
            }
        }
    } catch {
        Write-Status -Status 'FAIL' -Name 'Java JDK' -Detail $_.Exception.Message `
            -Hint 'Install: https://learn.microsoft.com/java/openjdk/   |   winget install Microsoft.OpenJDK.17'
    }

    $androidHome = $env:ANDROID_HOME
    $androidOk = $false
    try {
        $ErrorActionPreference = 'Stop'
        if (-not $androidHome) {
            Write-Status -Status 'FAIL' -Name 'ANDROID_HOME' -Detail 'not set' `
                -Hint 'Install Android Studio, then set ANDROID_HOME to %LocalAppData%\Android\Sdk   |   winget install Google.AndroidStudio'
        } elseif (-not (Test-Path -LiteralPath $androidHome)) {
            Write-Status -Status 'FAIL' -Name 'ANDROID_HOME' -Detail "set but missing: $androidHome" `
                -Hint 'Install Android Studio, then set ANDROID_HOME to %LocalAppData%\Android\Sdk   |   winget install Google.AndroidStudio'
        } else {
            $adb = Join-Path $androidHome 'platform-tools\adb.exe'
            if (Test-Path -LiteralPath $adb) {
                Write-Status -Status 'OK' -Name 'ANDROID_HOME' -Detail $androidHome
                $androidOk = $true
            } else {
                Write-Status -Status 'FAIL' -Name 'ANDROID_HOME' -Detail 'platform-tools/adb.exe missing' `
                    -Hint 'Open Android Studio > SDK Manager and install "Android SDK Platform-Tools"'
            }
        }
    } catch {
        Write-Status -Status 'FAIL' -Name 'ANDROID_HOME' -Detail $_.Exception.Message `
            -Hint 'Install Android Studio, then set ANDROID_HOME to %LocalAppData%\Android\Sdk   |   winget install Google.AndroidStudio'
    }

    try {
        $ErrorActionPreference = 'Stop'
        if (-not $androidOk) {
            Write-Status -Status 'FAIL' -Name 'sdkmanager' -Detail 'not found (skipped: ANDROID_HOME missing)' `
                -Hint 'Install Android Studio cmdline-tools via SDK Manager   |   winget install Google.AndroidStudio'
        } else {
            $candidates = @(
                (Join-Path $androidHome 'cmdline-tools\latest\bin\sdkmanager.bat'),
                (Join-Path $androidHome 'cmdline-tools\bin\sdkmanager.bat')
            )
            $hit = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
            if ($hit) {
                Write-Status -Status 'OK' -Name 'sdkmanager' -Detail $hit
            } else {
                Write-Status -Status 'FAIL' -Name 'sdkmanager' -Detail 'not found in cmdline-tools' `
                    -Hint 'Open Android Studio > SDK Manager > SDK Tools and install "Android SDK Command-line Tools (latest)"'
            }
        }
    } catch {
        Write-Status -Status 'FAIL' -Name 'sdkmanager' -Detail $_.Exception.Message `
            -Hint 'Open Android Studio > SDK Manager > SDK Tools and install "Android SDK Command-line Tools (latest)"'
    }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$gradlew = Join-Path $repoRoot 'src\rAspCoreVueLauncher.Web\android\gradlew.bat'
if (Test-Path -LiteralPath $gradlew) {
    Write-Status -Status 'OK' -Name 'Gradle wrapper' -Detail $gradlew
}

$okCount = ($script:Results | Where-Object { $_.Status -eq 'OK' }).Count
$failCount = ($script:Results | Where-Object { $_.Status -eq 'FAIL' }).Count
$warnCount = ($script:Results | Where-Object { $_.Status -eq 'WARN' }).Count

Write-Host ''
$summaryColor = if ($failCount -gt 0) { 'Red' } elseif ($warnCount -gt 0) { 'Yellow' } else { 'Green' }
Write-Host ("Summary: {0} failed, {1} warning, {2} ok." -f $failCount, $warnCount, $okCount) -ForegroundColor $summaryColor

if ($failCount -gt 0) {
    Write-Host ''
    Write-Host 'Next steps:' -ForegroundColor Cyan
    Write-Host '  1. Install missing tools above.'
    Write-Host '  2. Restart your terminal so PATH picks up changes.'
    Write-Host '  3. Re-run: pwsh scripts/setup.ps1'
    Write-Host '  4. Then bootstrap deps: dotnet restore  ;  (cd src/rAspCoreVueLauncher.Web ; npm install)'
    exit 1
}

exit 0
