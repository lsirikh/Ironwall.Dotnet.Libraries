# 메인 앱 시험 실행기: 원본 대조 시험(..\Ironwall.Dotnet.Libraries\ 소스를 읽음)을 위해 실행하는 동안만 junction 을 만들고 끝나면 링크만 지운다.
# (junction 을 계속 두면 라이브러리 쪽 소스 스캔 시험이 무한 경로를 돈다)
param([string]$Log = '.harness/t-20261019-193448-b2cc/logs/host.txt')
$wt = (Get-Location).Path
$j = Join-Path $wt '.harness-worktrees\host\Ironwall.Dotnet.Libraries'
dotnet build .harness-worktrees/host/Dotnet.Monitoring.Solution/Dotnet.Monitoring.Solution.Tests --nologo -v q 2>&1 | Out-File -Encoding utf8 $Log
New-Item -ItemType Junction -Path $j -Target $wt | Out-Null
try {
  dotnet test .harness-worktrees/host/Dotnet.Monitoring.Solution/Dotnet.Monitoring.Solution.Tests --no-build --nologo -v q 2>&1 | Out-File -Append -Encoding utf8 $Log
  "EXIT=$LASTEXITCODE" | Out-File -Append -Encoding utf8 $Log
} finally { [System.IO.Directory]::Delete($j, $false) }
