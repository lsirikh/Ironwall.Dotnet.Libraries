# device-console-v8 — 영향 프로젝트를 "하나씩" 빌드한다.
#
# 왜 하나씩인가: 솔루션 단위 `dotnet build` 는 WPF 임시 프로젝트(_wpftmp)를 타지 않아
# 테스트 폴더의 페이크·목이 인터페이스 확장으로 깨져도(CS0535) 통과로 보인다.
# 인터페이스(IDeviceApiService · IBaseDeviceModel 등)를 넓힌 뒤에는 반드시 이 스크립트로 확인한다.
#
# 사용:  pwsh/powershell -File tools\build-device-console-projects.ps1 [-Test]
#   -Test  : 빌드 후 장비 콘솔 관련 테스트 프로젝트를 필터 없이 실행한다(Events.Ui 의 기존 실패 15건은 기준선).
param(
    [switch]$Test
)

# PS 5.1 은 네이티브 stderr 를 에러 레코드로 승격한다 — 종료 코드로만 판정한다.
$ErrorActionPreference = 'Continue'
$root = Split-Path -Parent $PSScriptRoot

$projects = @(
    'Ironwall.Dotnet.Libraries.Enums',
    'Ironwall.Dotnet.Framework',
    'Ironwall.Dotnet.Libraries.Messages',
    'Ironwall.Dotnet.Monitoring.Models',
    'Ironwall.Dotnet.Libraries.Utils',
    'Ironwall.Dotnet.Libraries.Api',
    'Ironwall.Dotnet.Libraries.ViewModel',
    'Ironwall.Dotnet.Libraries.Devices',
    'Ironwall.Dotnet.Libraries.Devices.Api',
    'Ironwall.Dotnet.Libraries.Devices.Db',
    'Ironwall.Dotnet.Libraries.Nats',
    'Ironwall.Dotnet.Libraries.Devices.Ui',
    'Ironwall.Dotnet.Libraries.Events.Ui',
    'Ironwall.Dotnet.Libraries.GMaps.Ui'
)

# ⚠ Devices.Db 는 여기 넣지 않는다 — 픽스처가 실제 DB 에 붙는 통합 테스트라 필터 없이 돌리면 안 된다.
#   순수 함수 테스트만: dotnet test ...Devices.Db.csproj --filter "FullyQualifiedName~DeviceTypeParseTests"
$testProjects = @(
    'Ironwall.Dotnet.Libraries.Messages',
    'Ironwall.Dotnet.Monitoring.Models',
    'Ironwall.Dotnet.Libraries.Devices.Ui'
)

$failed = @()
foreach ($name in $projects) {
    $csproj = Join-Path $root "$name\$name.csproj"
    if (-not (Test-Path $csproj)) { Write-Host "[SKIP] $name (csproj 없음)"; continue }

    $out = & dotnet build $csproj -nologo -v q 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "[ OK ] $name"
    } else {
        Write-Host "[FAIL] $name"
        $out | Select-String -Pattern ' error ' | Select-Object -Unique -First 8 | ForEach-Object { Write-Host "       $_" }
        $failed += $name
    }
}

if ($Test -and $failed.Count -eq 0) {
    foreach ($name in $testProjects) {
        $csproj = Join-Path $root "$name\$name.csproj"
        $out = & dotnet test $csproj --no-build -nologo -v q 2>&1
        $summary = $out | Select-String -Pattern '통과!|실패!|Passed!|Failed!' | Select-Object -First 1
        Write-Host "[TEST] $name — $summary"
        if ($LASTEXITCODE -ne 0) { $failed += "$name (test)" }
    }
}

if ($failed.Count -gt 0) {
    Write-Host ""
    Write-Host "실패: $($failed -join ', ')"
    exit 1
}
Write-Host ""
Write-Host "전부 통과 ($($projects.Count)개 프로젝트)"
exit 0
