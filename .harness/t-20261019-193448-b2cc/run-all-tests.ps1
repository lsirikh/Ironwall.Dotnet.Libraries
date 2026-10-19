# 전 프로젝트 시험(라이브러리 sln · .sln 밖 GMaps 7개 · 메인 앱 복제본) — 결과는 logs/<Tag>-*.txt 와 요약 logs/<Tag>-summary.txt
param([string]$Tag = 'final')
$log = '.harness/t-20261019-193448-b2cc/logs'
$sum = "$log/$Tag-summary.txt"
"== $Tag $(Get-Date -Format s)" | Out-File -Encoding utf8 $sum
dotnet test Ironwall.Dotnet.Libraries.sln --nologo -v q 2>&1 | Out-File -Encoding utf8 "$log/$Tag-sln.txt"
"sln EXIT=$LASTEXITCODE" | Out-File -Append -Encoding utf8 $sum
foreach ($p in 'GMaps.Ui.Tests','GMaps.MapEdit.Tests','GMaps.MapNav.Tests','GMaps.Housing.Tests','GMaps.MapAux.Tests','GMaps.PropertyPanel.Tests','GMaps.MapLayer.Tests') {
  dotnet test "tests/$p" --nologo -v q 2>&1 | Out-File -Encoding utf8 "$log/$Tag-$p.txt"
  "$p EXIT=$LASTEXITCODE" | Out-File -Append -Encoding utf8 $sum
}
powershell -NoProfile -File .harness/t-20261019-193448-b2cc/run-host-tests.ps1 -Log "$log/$Tag-host.txt"
Get-ChildItem $log -Filter "$Tag-*.txt" | Where-Object Name -ne "$Tag-summary.txt" | ForEach-Object {
  Get-Content $_.FullName | Select-String '^(통과|실패)!|EXIT=' | ForEach-Object { "$($_.Line.Trim())" }
} | Out-File -Append -Encoding utf8 $sum
