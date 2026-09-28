param([string]$SampleRoot = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
$results = Join-Path $PSScriptRoot 'TestResults'
New-Item -ItemType Directory -Path $results -Force | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
$vb = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\Microsoft.VisualBasic.dll'
& $compiler /nologo /target:exe "/out:$results\ConversionTests.exe" "/reference:$vb" (Join-Path $PSScriptRoot 'CardFileConverter\ErrorHandling.cs') (Join-Path $PSScriptRoot 'CardFileConverter\ConversionEngine.cs') (Join-Path $PSScriptRoot 'CardFileConverter\BankProfiles.cs') (Join-Path $PSScriptRoot 'Tests\BankProfileTests.cs') (Join-Path $PSScriptRoot 'Tests\ConversionTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
& (Join-Path $results 'ConversionTests.exe') $SampleRoot
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
$appSources = Get-ChildItem (Join-Path $PSScriptRoot 'CardFileConverter') -Filter '*.cs' | Select-Object -ExpandProperty FullName
& $compiler /nologo /target:exe /main:FormSmokeTest "/out:$results\FormSmokeTest.exe" "/win32icon:$PSScriptRoot\CardFileConverter\Assets\AppIcon.ico" "/resource:$PSScriptRoot\CardFileConverter\Assets\AppIcon.ico,CardFileConverter.AppIcon.ico" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Xml.dll "/reference:$vb" @appSources (Join-Path $PSScriptRoot 'Tests\FormSmokeTest.cs')
if ($LASTEXITCODE -ne 0) { throw 'UI test build failed.' }
& (Join-Path $results 'FormSmokeTest.exe') $results
if ($LASTEXITCODE -ne 0) { throw 'UI tests failed.' }
