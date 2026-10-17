# PLAN — 지도 3D 설정 빼기 + 메인 앱 세팅 「3D 지도」 절

## 목표
고정된 지도 3D 값(진입 17 · 이탈 16.5 · 미리 준비 15 · 영역 표시 13 · 영역 2 km · 안내 0.6 s)을 AppSettings.Map3D 설정으로 빼고,
메인 앱 세팅 화면(SettingsCatalog)에 「3D 지도」 절을 만든다. 메인 앱 변경은 `.harness/t-20261017-232402-ca69/host.patch` 로 남긴다.

## 진행
- [x] 라이브러리: Map3DModel 새 설정(EnterZoom · ExitZoom · PreloadZoom · AreaShowZoom · MaxAreaSideM · GuideMinSeconds) + Normalize
- [x] 라이브러리: Map3DMath · Map3DGuide 인자 받는 오버로드(기존 상수 = 기본값, 기존 시험 그대로 통과)
- [x] 라이브러리: MapViewModel.Map3D · Map3DAreas 가 설정값 사용, MapSettingsHelper.Map3DSaved → LoadMap3DFromSettings 다시 실행
- [x] 라이브러리: LoadMap3D 새 키 읽기 · SaveMap3DAsync(이미 있음) 저장 뒤 Map3DSaved 알림
- [x] 라이브러리 시험: tests/GMaps.Ui.Tests/Map3DSettingsTests.cs (Normalize · 게이트/안내가 설정값을 따름 · 직렬화 왕복) — 1107 통과
- [x] 메인 앱: IMap3DSettingsPort · Map3DSettingsViewModel · SettingsCatalog.Map3DSection · 칸 사이 규칙(Check) · HostHelp.SettingsMap3D · Bootstrapper 등록 · 시험 → host.patch
- [x] 검증: dotnet test Ironwall.Dotnet.Libraries.sln 통과 · GMaps.Ui.Tests 통과 · 메인 앱 시험(필터) 805 통과 / 1 실패(이번 변경과 무관, 아래)

## 발견
- 메인 앱 `MapToolbarScenarioTests.should_offer_switch_map_coordinate_systems_zoom_ranges_and_exit_in_the_map_menu` 실패 —
  앞선 작업이 라이브러리 MapView.xaml 지도 메뉴에 「3D 영역 목록」을 더해 메뉴가 5개가 됐는데 시험은 4개를 기대한다. 이번 작업은 XAML 을 고치지 않았다(후속 과제).
- 라이브러리 솔루션 시험의 `CameraPopup.Tests EventWindowManagerTests`(50 ms 예산) · `Utils.Tests HelpTipTests(Space)` 는 기계가 바쁠 때 가끔 실패하고 단독 · 재실행에선 통과한다.
- GMaps.Ui.Tests 는 소스 링크 방식이라 MapSettingsHelper(GMap.NET 의존)를 직접 시험하지 못한다 — 직렬화 왕복은 JToken.FromObject/ToObject 와 LoadMap3D 가 읽는 키 목록으로 확인했다.

## 남은 일(코디네이터)
- 메인 앱 저장소 루트에서 `git apply <이 worktree>/.harness/t-20261017-232402-ca69/host.patch` → 빌드 · 시험.
