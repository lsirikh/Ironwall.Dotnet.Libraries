# PLAN — 3D 들어가는 방식 설정(자동(줌) / 수동(단추)) · 설정 화면 정리

## 목표
- Map3DModel.EntryMode("auto" 기본 | "manual") + 읽기/쓰기 · Normalize.
- 수동: 줌으로 자동 진입 없음(미리 준비는 함). 들어가는 길 = 「이 영역 3D 로 보기」(직접 진입) · 지도 왼쪽 위 「3D 로 보기」 단추.
- 설정(메인 앱): "2D 로 돌아가는 줌" 칸 제거, 「지도」 절 "3D 최소 줌" → 「3D 지도」 절 "3D 로 들어가는 줌"(들어가는 방식 바로 아래), 쉬운 이름 · "?" 설명.

## 설계
- 순수 함수(Map3DMath): `IsZoomEntry(entryMode)` · `IsEnterButtonVisible(enabled, is3DOff, centerInsideScene)` · `KeepsPendingEntry(...)`.
- 직접 진입(MapViewModel.EnterMap3DDirect): 돌아올 자리 저장 → _map3DAreaViewJob = 그 영역 → Begin3D. 게이트는 직접 진입한 Pending 을 줌으로 취소하지 않는다.
- 「3D 로 보기」 단추: 「나가기」와 같은 자리 줄(왼쪽 위, 좌표 칩 왼쪽), 같은 스타일 키
  — `MapFloatingPanelStyle`(테두리) + `PanelPrimaryButtonStyle`(단추) + `help:HelpTip`(HelpKey `Map.Map3D.Enter`), 토큰만.
  AutomationId: 테두리 `GMaps.Map3D.Enter`, 단추 `Map.Map3D.EnterButton`(명세 그대로).
- ExitZoom: 화면에서 빼고 내부 값으로만 — 메인 앱 창구가 EnterZoom 을 쓸 때 ExitZoom = EnterZoom − 0.5 로 함께 맞춘다.
  줌 순서 규칙 = "미리 준비하는 줌 ≤ 3D 로 들어가는 줌 − 0.5"(라이브러리 Normalize 의 미리 준비 ≤ 이탈 과 같은 뜻).
- 수동일 때 "3D 로 들어가는 줌" 칸: 설정 화면에 칸 비활성 관용구가 없어 상태 주석(NoteSource)으로 "수동(단추)이라 지금은 쓰지 않습니다".

## 진행
- [x] 라이브러리 모델(Map3DModel.EntryMode · Normalize · ToString) · MapSettingsHelper.LoadMap3D · Map3DMath(IsZoomEntry · IsEnterButtonVisible · DecideEntry)
- [x] MapViewModel: EnterMap3DDirect · 게이트(DecideEntry) · IsMap3DEnterButtonVisible/EnterMap3DCommand · 칩은 자동일 때만 · 굽기 끝 문구
- [x] MapView.xaml 「3D 로 보기」 단추 · MapsHelp "Map.Map3D.Enter" · EnterZoom 도움말 문구
- [x] 라이브러리 시험 tests/GMaps.Ui.Tests/Map3DEntryModeTests.cs(+ Map3DSettingsTests 키 목록에 EntryMode)
- [x] 메인 앱(복사본에서) SettingsCatalog · SettingsPorts · Map3DSettingsViewModel · SettingsValidation · HostHelp · Map3DSettingsSectionTests → host.patch(`git apply --check` 통과)
- [x] dotnet test Ironwall.Dotnet.Libraries.sln — 실패 0 · tests/GMaps.Ui.Tests 1401 통과

## 추가 지시(2026-10-19 "시나리오 빡세게")
- [x] tests/GMaps.MapEdit.Tests/Map3DEntryScenarioTests.cs — 실제 MapViewModel(WiredMapRig) 게이트 · 표시 · 단추 · 영역 메뉴 · 나가기 · ESC 경로, 284건
  - 표: 방식 2 × 줌 8(진입 줌 −1/−0.01/0/+0.01/+0.5 · 미리 준비 줌 ±0.01) × 위치 4(안 · 밖 · 겹침 · 경계 위) × 장면 4(있음 · 없음 · 만드는 중 · 만들기 실패) = 256
  - 흐름 · 막힘(편집/묶음 회전 · 영역 그리기 · 심볼 배치) · 단추 보임 회귀 · 반복 누름 · 안내 중 나가기/ESC · 준비 중 줌 변경 · 영역 밖 이동 · flyTo 중 · XAML 디자인/바인딩
- [x] tests/GMaps.MapEdit.Tests/Map3DSettingsReadTests.cs — ReadMap3D 옛 파일 · 이상한 값 12종 · 왕복(16건)
- [x] MapsHelpTests 에 Map.Map3D.Enter · EnterZoom 문구 6줄
- [x] 메인 앱 Map3DSettingsSectionTests 에 칸 순서 · 수동 주석(저장 전/뒤) · 수동에서도 진입 줌 저장 · 되돌리기 · 줌 순서 경계 9 · "?" 에 모든 칸 설명 → 58건 통과(host.patch 다시 만듦)
- [x] 시험이 찾은 결함: 만드는 중 · flyTo 중 단추 → 준비 시작 후 즉시 취소(안내 깜빡임) → EnterMap3DDirect 에서 처음부터 막음(DECISIONS 8)
- [x] 수정 전 실패 증명: 게이트를 옛 `Map3DMath.Decide` 로, 만드는 중/flyTo 막음을 빼고 돌리면 284 중 16 실패 → 되돌리면 284 통과

- [x] 다시 확인: sln 전체 통과(첫 회 CameraPopup.Tests 의 시간 예산 시험 1건이 부하로 실패 → 단독 · 재실행 통과, 3D 변경과 무관). tests/ 아래 GMaps.* 시험은 sln 밖이라 따로 돌림.

- [x] 하네스 검사 1회 "exit 1 · 실패 0(통과 2582)" — 정상 실행은 10개 시험 묶음 합계 2888건이라 한 묶음 일부가 집계되지 않은 일시 중단으로 보임. 같은 명령을 연달아 2번 다시 돌려 둘 다 exit 0 · 실패 0(코드 변경 없음).

## 발견
- 메인 앱 저장소는 이 세션에서 쓰기 거부 → 워크트리 안 복사본(라이브러리 참조를 이 워크트리로)에서 고쳐 시험하고 패치로 남김(DECISIONS 7). 복사본은 지움.
- 복사본 시험에서 실패 7건은 복사 위치 탓(Installer 폴더 · 라이브러리 XAML 상대 경로 없음) — 3D 변경과 무관. Map3DSettingsSectionTests 전부 통과.
- 설정 창에 "다른 칸 값에 따라 칸 끄기/숨기기" 관용구 없음 → 상태 주석(DECISIONS 4).

## 남은 일
- 메인 앱에 host.patch 적용(코디네이터). 실기 미확인.
