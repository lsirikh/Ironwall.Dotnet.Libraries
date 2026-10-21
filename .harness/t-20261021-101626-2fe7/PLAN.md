# PLAN — 3D 환경(해 · 날씨 · 차 · 사람) GIS 쪽 (t-20261021-101626-2fe7)

## 목표
첨부 env3d-contract.md 의 A(설정 모델 · 설정 묶음 · 3D 빠른 조절 판) · B(IWeatherProvider + 기상청 구현) · C(env.set 보내기 · env.state 받기).
3D 엔진(map-3d)은 손대지 않는다(다른 작업). 실제 기상청 호출은 시험에서 하지 않는다(가짜 HTTP 처리기).

## 할 일
- [x] 모델 `GMaps/Models/Map3DEnvironmentModel.cs` (AppSettings.Map3DEnvironment, 기본값 · Normalize · MaskKey)
- [x] 저장 · 불러오기 `MapSettingsHelper.LoadMap3DEnvironment / SaveMap3DEnvironmentAsync`
- [x] 날씨 `Services/Weather/WeatherProvider.cs`(IWeatherProvider · WeatherSnapshot · WeatherFailure) + `KmaWeatherProvider.cs`(격자 · 발표 시각 · 해석 · 실패 분류)
- [x] 메시지 `Utils/Map3DEnvironment.cs`(env.set · env.state · 쉬운 말 목록 · 요약)
- [x] Map3DMath: Map3DMessage.EnvState + ParseMessage 의 env.state 한 줄
- [x] VM `ViewModels/Maps/Map3DEnvironmentViewModel.cs`(두 화면 공용, 바로 3D + 0.8초 뒤 저장, 날씨 고리 — 3D 처음 그린 뒤부터, 연결 시험)
- [x] 뷰 `Views/Maps/Map3DEnvironmentSettingsBlock.xaml` · `Views/Maps/Map3DEnvironmentQuickPanel.xaml`
- [x] `MapViewModel.Map3DEnvironment.cs` partial + CreateMap3DHost 한 줄 구독 + MapView.xaml 왼쪽 위 3D 단추 아래 판 자리
- [x] DI(GMapUiModule) · "?" 도움말 2키(Map.Map3D.Environment · Map.Map3D.Environment.Settings)
- [x] 시험: GMaps.Ui.Tests/Map3DEnvironmentTests(순수 · 가짜 HTTP) · GMaps.MapAux.Tests/Map3DEnvironmentScenarioTests(VM · 화면 헤드리스 15)
- [x] dotnet test Ironwall.Dotnet.Libraries.sln(전부 통과) + tests/GMaps.* 7개 직접
  - MapEdit EditComboScenarioTests 2건이 전체 실행 1회에서 실패 → 같은 클래스 3회 다시 실행해 16/16 × 3 통과(묶음 회전 Undo · 시간 의존, 이 작업과 닿는 코드 없음).
    HEAD 사본(git archive)은 서명 키 등이 빠져 빌드되지 않아 HEAD 기준 대조는 못 했다.

## 앱 쪽 붙일 자리(사용자가 직접 — 최종 보고에 파일 · 줄)
- Dotnet.Monitoring.Solution/…/SetupPanels/Console/SettingsCatalog.cs:67 `Map3DSection(map3d)` · :336 Map3DSection 에 DraftBlock
- …/Console/ISettingsDraftBlock.cs:86 SensorModelsDraftBlock 과 같은 「초안 없는 블록」
- …/Views/Panels/SetupPanels/Console/SettingsConsoleView.xaml:535 근처 DataTemplate

## 발견
- Json.NET JObject.Parse 는 iso 글을 날짜로 바꿔 서식을 잃는다 — 시험은 DateParseHandling.None 으로 읽는다(보내는 글 자체는 올바름).
- 창 없는 시험에서 접힌 요소의 ActualHeight 는 예전 값이 남는다 — 보이기 판정은 Visibility 를 조상까지 본다.
