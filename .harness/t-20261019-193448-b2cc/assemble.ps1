# areas/<영역>.md 에서 절을 뽑아 PLAN.md 부록(시나리오 표) · HEADED.md · REPORT.md 의 결함 부록을 만든다(영역 보고서가 정본).
$h = '.harness/t-20261019-193448-b2cc'
$utf8 = New-Object Text.UTF8Encoding($true)
function Section([string[]]$lines, [string]$startPattern, [string]$endPattern) {
  $s = ($lines | Select-String $startPattern | Select-Object -First 1).LineNumber
  if (-not $s) { return @() }
  $rest = $lines[$s..($lines.Count - 1)]
  $e = ($rest | Select-String $endPattern | Select-Object -First 1).LineNumber
  if ($e) { return $lines[($s - 1)..($s + $e - 2)] } else { return $lines[($s - 1)..($lines.Count - 1)] }
}
$names = @{ A = 'A 3D 진입/이탈'; B = 'B 3D 영역'; C = 'C 3D 상호작용'; D = 'D 2D 편집'; E = 'E 오버레이 이미지(GIS)'; F = 'F map-3d(E · F 의 map-3d 쪽 · B/C 뷰어)' }
$scen = @(); $headed = @(); $defects = @()
foreach ($k in 'A', 'B', 'C', 'D', 'E', 'F') {
  $f = "$h/areas/$k.md"
  if (-not (Test-Path $f)) { continue }
  $c = Get-Content $f -Encoding UTF8
  $t = Section $c '^## 시나리오 표' '^## (?!시나리오 표)'
  $scen += "### $($names[$k])"; $scen += ($t | Select-Object -Skip 1); $scen += ''
  $hd = Section $c '^## (3\. )?헤디드' '^## (?!(3\. )?헤디드)'
  $headed += "## $($names[$k])"; $headed += ($hd | Select-Object -Skip 1); $headed += ''
  $d = Section $c '^## 고친 결함' '^## (?!고친 결함)'
  $defects += "## $($names[$k])"; $defects += ($d | Select-Object -Skip 1); $defects += ''
}
[IO.File]::WriteAllLines((Join-Path (Get-Location) "$h/SCENARIOS.md"), (@('# 시나리오 표 전체(영역 보고서 areas/*.md 에서 모음)', '', '상태: 통과(기존) = 이미 있던 시험 · 통과(새) = 이번에 쓴 시험 · 수정 후 통과 = 시험이 드러낸 결함을 고친 뒤 통과 · 헤디드 = HEADED.md · 결정 필요 = REPORT.md', '') + $scen), $utf8)
[IO.File]::WriteAllLines((Join-Path (Get-Location) "$h/HEADED.md"), ((Get-Content "$h/HEADED-preamble.md" -Encoding UTF8) + @('', '# 영역별 단계') + $headed), $utf8)
[IO.File]::WriteAllLines((Join-Path (Get-Location) "$h/DEFECTS.md"), (@('# 고친 결함 상세(영역 보고서에서 모음 — 원인 · 수정 · 수정 전 실패 / 수정 후 통과)', '') + $defects), $utf8)
