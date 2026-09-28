param([switch]$UseInstalled472References)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (!(Test-Path $vswhere)) { throw 'Install Visual Studio or Build Tools with the .NET desktop development workload.' }
$msbuild = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (!$msbuild) { throw 'MSBuild was not found.' }
$buildArgs = @((Join-Path $projectRoot 'CardFileConverter.sln'), '/p:Configuration=Release', '/verbosity:minimal', '/nologo')
if ($UseInstalled472References) {
    $references = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2'
    if (!(Test-Path $references)) { throw '.NET Framework 4.7.2 references were not found.' }
    $buildArgs += "/p:FrameworkPathOverride=$references"
    $buildArgs += '/p:BypassFrameworkInstallChecks=true'
}
& $msbuild @buildArgs
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
