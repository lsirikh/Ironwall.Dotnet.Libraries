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

## 발견
- 메인 앱 저장소는 이 세션에서 쓰기 거부 → 워크트리 안 복사본(라이브러리 참조를 이 워크트리로)에서 고쳐 시험하고 패치로 남김(DECISIONS 7). 복사본은 지움.
- 복사본 시험에서 실패 7건은 복사 위치 탓(Installer 폴더 · 라이브러리 XAML 상대 경로 없음) — 3D 변경과 무관. Map3DSettingsSectionTests 전부 통과.
- 설정 창에 "다른 칸 값에 따라 칸 끄기/숨기기" 관용구 없음 → 상태 주석(DECISIONS 4).

## 남은 일
- 메인 앱에 host.patch 적용(코디네이터). 실기 미확인.
