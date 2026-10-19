# PLAN — t-20261019-221052-8a7a (전수 시험 「결정 필요」 추천 적용)

## 목표
REPORT(t-20261019-193448-b2cc) 「결정 필요」 1~9 를 사용자 추천대로 적용(Goal 1~9).

## 진행
- [x] 5. 묶음 회전 저장 실패 → 실패 멤버만 원위치 · Undo 는 저장된 멤버만 · "n개 저장 실패 — 원위치" (MapViewModel.cs OnGroupRotateCompleted) + 시험 GroupRotationScenarioTests(0·1·3 실패)
- [x] 6. Ctrl+I 편집 모드에서만 — 실행 첫머리 가드 + 안내(CanExecute 는 기존 시험이 고정해 그대로) + 시험 MapRegistrationCommandScenarioTests
- [x] 1. "shown" 안내에서 ESC 제거(hwnd 일 때만 Esc) + Map3DHudTests 기대값 변경
- [x] 2. ShouldLeave3D(3) · ExitCamera(bool,…) · ShouldLeave3DForPanel 삭제 + 그것만 시험하던 시험 삭제(메인 앱 grep 0건 확인됨)
- [x] 4. 자동 모드 입력 모드 뒤 재진입 보류(SyncMap3DWithInputModes 에서 _map3DEntrySuppressed=true) + 시험
- [x] 7. 3D 빈 땅 클릭 → map-3d 가 {type:"building.none"} 전송, GIS 가 판 닫기(취소) + 시험 · map3d.patch
- [x] 수정 전 실패 → 수정 후 통과 증명(4 · 5 · 6 · 7)
- [x] 완료 판정: sln + GMaps.Ui/MapEdit/MapNav/PropertyPanel 테스트(+MapLayer)

## 발견
- 3D 건물 클릭도 view.js 가 먼저 빈 pick(kind null)을 보낸다 → 빈 pick 으로 판을 닫으면 건물 클릭도 닫힌다. 그래서 map-3d 에 새 메시지 필요.
- map-3d 작업 사본: artifacts/map3d (git 무시 폴더, C:\workspace_python\map-3d clone)

## 결과(2026-10-19)
- dotnet test Ironwall.Dotnet.Libraries.sln 통과 · GMaps.Ui 1595 · MapEdit 676 · MapNav 67 · PropertyPanel 436 · MapLayer 98 · MapAux 80 전부 통과.
- 수정 전 실패 증명: prove_before.py / prove_before.log. 결정: DECISIONS.md. 요약: REPORT.md. map-3d: map3d.patch(pytest 286 · node 통과, 원본 apply --check 통과).
- 정리: 시험 로그 · artifacts/map3d 작업 사본 지움.

## 남은 일
- 없음. map3d.patch 적용 · 실기 확인(빈 땅 클릭 · 운영 모드 Ctrl+I 안내)은 사람 몫.
