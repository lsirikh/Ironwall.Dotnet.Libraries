# PLAN — Map3DExitInput 2건 · GroupRotationEdge 3건 흔들림 원인 규명 · 수정

## 목표
- `tests/GMaps.MapEdit.Tests` `Map3DExitInputScenarioTests.should_restore_2d_input_when_exited_with_panel_or_window_opened_in_3d`
  (during: Nothing · BuildingPanel) 2026-10-21 저녁 868/2 실패의 원인을 증거로 밝히고 고친다.
- 같은 날 흔들린 `GroupRotationEdgeScenarioTests` 3개가 같은 원인인지 본다.
- 끝나면 tests/GMaps.* 7개(.sln 밖)를 직접 돌려 보고.

## 진행
- [x] 현재 HEAD(199fc849)에서 재현 시도 — 클래스 단독 24/24, MapEdit 전체 870/870 통과(재현 안 됨)
- [x] 저녁 실패 원문 찾기 — 메인 세션 기록(`~/.claude/projects/C--workspace-app-Ironwall-Dotnet-Libraries/d36b76f2-….jsonl`)
- [x] CPU 부하 반복 실행 — GroupRotationEdge 는 대부분 `STA timeout`(45초) — 저녁 실패와 모양이 다름(아래)
- [x] 공유 static(`RotationFeature.IsEnabled`) 가설 — 켜고 6건 돌려 6/6 통과 → 기각
- [x] 보조키(Shift · Ctrl) 가설 — 시험 스레드 한정 `SetKeyboardState` 로 재현 성공(아래)
- [x] 실제 키보드 상태가 창 없는 시험 스레드로 새는지 · 숨은 데스크톱이 막는지 실측(아래 발견 7)
- [x] 수정 — `Scenario.StaOnHiddenDesktop` · `StaAsyncOnHiddenDesktop`(ScenarioHarness.cs) + 두 클래스 호출부
- [x] 실제 Shift 로 수정 전 실패 → 수정 후 통과 증명(사용자 유휴 4분 넘을 때만 Shift 0.1초 — `$TEMP/keyprobe2/wait-and-prove.sh`, 임시 시험 `ZzProbeRealShift` 는 끝나고 지움)
  - 기본 데스크톱(`Map3DEntryScenarioTests.Run`, 고치기 전 방식): `Modifiers=Shift → Assert.Equal() Failure: Expected: 17.5 Actual: 17`
  - 숨은 데스크톱(`Scenario.StaOnHiddenDesktop`, 고친 방식): `AssertMap2DInputAlive` 전부 통과, `Modifiers=None`
- [x] tests/GMaps.* 7개 실행(아래 검증 표)

## 이분 탐색을 하지 않은 이유
현재 HEAD 에서도 그 시험은 단독 · 전체 모두 통과한다(재현 안 되는 흔들림) — 커밋별로 돌려도 전부 통과라 어느 커밋부터인지 가를 수 없다.
원인이 커밋이 아니라 "시험 도중 실제로 눌린 보조키"이므로 커밋 대신 키 상태로 재현 · 증명했다(발견 3 · 4 · 7). QuietWindow(d06b52bd)는
MapEdit 에서 쓰이지 않고(창 없음), 3D 환경(199fc849)은 MapEdit 시험을 바꾸지 않았다.

## 발견(증거)
1. 저녁 실패의 모양: `[xUnit.net 00:01:10.25] …(during: Nothing) [FAIL]` · `[00:01:10.45] …(during: BuildingPanel) [FAIL]` — 0.2초 간격.
   이론 행은 한 클래스 안에서 차례로 돌므로 45초 `STA timeout` 이면 둘 사이가 45초 이상 벌어진다 → 빠른 단정 실패.
   같은 날 GroupRotationEdge 3개도 `00:00:39.04 · 39.33 · 39.56` 0.5초 안에 몰렸다 → "잠깐 동안의 전역 조건".
   (실패 메시지는 원래 명령이 `[FAIL]` 줄만 걸러 남지 않았다.)
2. 그 시각 메인 세션 기록: 시험 명령 09:00:58Z 시작 → 09:05:06Z 끝(xUnit 3m45s → 시작 ≈09:01:21Z, 실패 ≈09:02:31Z).
   사용자가 09:02:55Z 에 메시지("하네스 업데이트 할거야 잠시 작업중단해줘")를 입력해 보냈다 — 실패 직후 키보드를 쓰던 때.
3. 메커니즘: `GMapCustomControl.OnMouseWheel` 은 `Keyboard.Modifiers == Shift`(또는 Ctrl+Shift)면 휠을 회전으로 소비한다(설계 — Shift+휠 회전).
   WPF `Keyboard.Modifiers` 는 Win32 `GetKeyState` 를 읽는다 = 시험 스레드의 키 상태. 시험 스레드에 Shift 를 얹으면
   (`SetKeyboardState`, 다른 스레드 · 사용자 입력에 안 닿음) `AssertMap2DInputAlive` 86번 줄
   `Assert.Equal(zoom + 0.5, map.EffectiveZoom)` → `Expected: 17.5 Actual: 17` 로 깨진다(Ctrl 만으로는 안 깨짐).
4. 같은 방법(공용 Sta 에 임시 훅 → 확인 뒤 원복)으로 GroupRotationEdge 를 돌리면 Ctrl 눌림에서 그날 흔들린 3개
   (`…two_undo_units_when_arrow_key…` · `…group_move_drag_starts…` · `…locked_from_layer_panel`)가 모두 실패 목록에 든다
   → 흔들림 원인 종류가 같다(시험이 실제 보조키 상태를 읽음).
5. 제품 결함 아님: 3D 나가기 뒤 2D 입력 복구는 정상 — Shift 를 누른 채 휠을 돌리면 회전하는 것은 의도된 동작.
7. 실측(`$TEMP/keyprobe2` — 사용자 입력이 5분 넘게 없을 때만 실제 Shift 를 0.3초 눌렀다 뗌, 글자 입력 없음):
   `default desktop thread: before=None shiftDown=Shift after=Shift` · `hidden desktop thread: before=None shiftDown=None after=None`.
   별도 프로세스 수동 탐침(`$TEMP/keyprobe`)도 같은 순간 `19:51:07.212 default: wpf=Shift asyncShift=True` → `.521 None` 을 남겼고 숨은 스레드 줄은 없었다.
   → 창 없는 시험 STA 스레드의 `Keyboard.Modifiers` 에 사용자의 실제 Shift 가 보이고(뗀 직후에도 잠시 남음), 숨은 데스크톱 스레드에는 안 보인다.
6. CPU 부하(32코어 바쁜 루프)에서는 GroupRotationEdge 가 `STA timeout`(45초)으로 깨진다 — 저녁 실패와는 다른 모양이라 이 작업 범위 밖(보고만).

## 결정
- DECISIONS.md 참고.

## 검증 명령 · 결과
- `dotnet test tests/GMaps.MapEdit.Tests --no-build --filter FullyQualifiedName~Map3DExitInputScenarioTests` → 24/24 통과
- `dotnet test tests/GMaps.MapEdit.Tests --no-build` → 870/870 통과(5m8s)
- 부하 반복(`$TEMP/burn.ps1` 코어 수만큼 바쁜 루프 + 두 클래스 6회) → 1·2회 45/45, 3·4회 GroupRotationEdge 5·4건 `STA timeout`
- 임시 시험 `ZzProbeShift`(삭제함): shift · ctrl+shift 실패(86번 줄 17.5≠17), ctrl · none 통과

- 수정 뒤 두 클래스: `dotnet test tests/GMaps.MapEdit.Tests --no-build --filter "…GroupRotationEdgeScenarioTests|…Map3DExitInputScenarioTests"` → 45/45 통과(16s)
- 수정 뒤 7개 프로젝트(다른 작업으로 CPU 60%대 부하 중):
  | 프로젝트 | 결과 |
  |---|---|
  | GMaps.Ui.Tests | 1841/1841 통과 |
  | GMaps.MapEdit.Tests | 865/870 — 실패 5건 모두 시간 상한(STA timeout 45s ×4 · Map3DComposite `Until` 6s ×1), 23m10s(평소 5분) |
  | GMaps.MapAux.Tests | 118/118 통과 |
  | GMaps.MapLayer.Tests | 102/102 통과 |
  | GMaps.MapNav.Tests | 70/70 통과 |
  | GMaps.PropertyPanel.Tests | 436/436 통과 |
  | GMaps.Housing.Tests | 362/362 통과 |
- 위 5건 클래스(Map3DCompositeScenarioTests · EditComboScenarioTests) 단독 재실행 → 49/49 통과(12s)
- `Ironwall.Dotnet.Libraries.sln` 에는 tests/GMaps.* 가 없다 — 이번 변경은 sln 밖 시험 프로젝트만 건드렸다.

## 남은 일(범위 밖 · 보고)
- 부하 때 STA timeout(45초) — 머신이 바쁘면 지도 시나리오 시험이 시간 상한에 걸린다. 키 상태와 무관한 별개 현상.
- 다른 시험 클래스도 보조키를 읽는 처리기를 거치면 같은 흔들림이 날 수 있다(DECISIONS 3) — 관찰되면 `Scenario.StaOnHiddenDesktop` 로 바꾼다.

## 정리
- 임시 시험 `ZzProbeRotation.cs` · `ZzProbeShift.cs` · `ZzProbeRealShift.cs` 삭제, `ScenarioHarness.cs` 임시 훅(ZZ_MODS) 원복 후 정식 변경만 남김,
  탐침 `keyprobe.exe` 종료, 부하 발생 작업은 12분 뒤 스스로 끝남. 탐침 소스 · 로그는 `$TEMP`(하네스가 지움).
