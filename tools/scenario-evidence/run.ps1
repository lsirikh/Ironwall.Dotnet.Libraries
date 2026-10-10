<#
  시나리오 검증 실행기 — 시험을 돌리고 그 기록(TRX + meta.json)을 증거 폴더에 남긴 뒤 상태표를 다시 만든다.

  예) 헤드리스(창 없음) 묶음:
      .\run.ps1 -Label headless-map -Projects tests\GMaps.PropertyPanel.Tests\GMaps.PropertyPanel.Tests.csproj -Filter "FullyQualifiedName~MapSymbolReflectionTests"
  예) 헤디드(실창) 묶음 — 사용자가 PC 를 비워 줄 때만:
      .\run.ps1 -Label headed-r28 -Headed -Projects C:\workspace_app\Dotnet.Monitoring.Solution\Dotnet.Monitoring.Solution.UiTests\Dotnet.Monitoring.Solution.UiTests.csproj -Filter "Category=UiFunctional"

  증거 폴더 기본값: <라이브러리>\docs\tests\evidence\<시각>-<Label>\  (docs/ 는 git 밖)
  meta.json: 실행 시각 · 두 저장소 HEAD 커밋 · 미커밋 여부 · 명령 · 컴퓨터 · 헤디드 여부
#>
param(
    [Parameter(Mandatory)] [string] $Label,
    [Parameter(Mandatory)] [string[]] $Projects,
    [string] $Filter = "",
    [switch] $Headed,
    [string] $Evidence = (Join-Path $PSScriptRoot "..\..\docs\tests\evidence"),
    [string] $Catalog = (Join-Path $PSScriptRoot "..\..\docs\tests\gis-map-event-scenario-catalog.md"),
    [string] $LibRepo = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path,
    [string] $HostRepo = "C:\workspace_app\Dotnet.Monitoring.Solution"
)
$ErrorActionPreference = 'Continue'
$runId = (Get-Date -Format "yyyyMMdd-HHmmss") + "-" + $Label
$runDir = Join-Path $Evidence $runId
New-Item -ItemType Directory -Force $runDir | Out-Null

function Head($repo) { (git -C $repo rev-parse HEAD 2>$null) }
function Dirty($repo) { [bool](git -C $repo status --porcelain --untracked-files=no 2>$null) }

$meta = [ordered]@{
    run_id   = $runId
    started  = (Get-Date).ToString("o")
    computer = $env:COMPUTERNAME
    headed   = [bool]$Headed
    filter   = $Filter
    projects = $Projects
    shas     = [ordered]@{ (Split-Path $LibRepo -Leaf) = (Head $LibRepo); (Split-Path $HostRepo -Leaf) = (Head $HostRepo) }
    dirty    = [ordered]@{ (Split-Path $LibRepo -Leaf) = (Dirty $LibRepo); (Split-Path $HostRepo -Leaf) = (Dirty $HostRepo) }
}

$exit = 0
foreach ($p in $Projects) {
    $name = [IO.Path]::GetFileNameWithoutExtension($p)
    $testArgs = @("test", $p, "-nologo", "-v", "q", "--blame-hang-timeout", "5m",
              "--logger", "trx;LogFileName=$name.trx", "--results-directory", $runDir)
    if ($Filter) { $testArgs += @("--filter", $Filter) }
    & dotnet @testArgs 2>&1 | Select-String -Pattern '통과!|실패!|Passed!|Failed!|error CS' | ForEach-Object { $_.Line }
    if ($LASTEXITCODE -ne 0) { $exit = $LASTEXITCODE }
}
$meta.finished = (Get-Date).ToString("o")
$meta | ConvertTo-Json -Depth 4 | Set-Content -Path (Join-Path $runDir "meta.json") -Encoding UTF8

python (Join-Path $PSScriptRoot "collect.py") --catalog $Catalog --repo $LibRepo --repo $HostRepo --evidence $Evidence --out (Join-Path $Evidence "..")
exit $exit
