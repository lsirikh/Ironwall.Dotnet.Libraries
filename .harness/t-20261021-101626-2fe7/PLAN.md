# PLAN — 3D 환경(해 · 날씨 · 차 · 사람) GIS 쪽 (t-20261021-101626-2fe7)

## 목표
첨부 env3d-contract.md 의 A(설정 모델 · 설정 묶음 · 3D 빠른 조절 판) · B(IWeatherProvider + 기상청 구현) · C(env.set 보내기 · env.state 받기).
3D 엔진(map-3d)은 손대지 않는다(다른 작업, 05361ed 로 구현 끝). 실제 기상청 호출은 시험에서 하지 않는다(가짜 HTTP 처리기).

## 할 일
- [x] 모델 `GMaps/Models/Map3DEnvironmentModel.cs` (AppSettings.Map3DEnvironment, 기본값 · Normalize · MaskKey)
- [x] 저장 · 불러오기 `MapSettingsHelper.LoadMap3DEnvironment / SaveMap3DEnvironmentAsync`
- [x] 날씨 `Services/Weather/WeatherProvider.cs` + `KmaWeatherProvider.cs`(격자 · 발표 시각 · 해석 · 실패 분류)
- [x] 메시지 `Utils/Map3DEnvironment.cs`(env.set · env.state · 쉬운 말 목록 · 요약)
- [x] Map3DMath: Map3DMessage.EnvState + ParseMessage 의 env.state 한 줄
- [x] VM `ViewModels/Maps/Map3DEnvironmentViewModel.cs`
- [x] 뷰 `Views/Maps/Map3DEnvironmentSettingsBlock.xaml` · `Views/Maps/Map3DEnvironmentQuickPanel.xaml`
- [x] `MapViewModel.Map3DEnvironment.cs` partial + CreateMap3DHost 한 줄 구독 + MapView.xaml 판 자리
- [x] DI(GMapUiModule) · "?" 도움말 2키
- [x] 시험: GMaps.Ui.Tests/Map3DEnvironmentTests · GMaps.MapAux.Tests/Map3DEnvironmentScenarioTests(15)
- [x] (추가 지시) 뷰어 주소 `&sky=1` — Map3DMath.ViewUrl(sky) · LoadMap3DScene 에서 환경 VM 이 있으면
- [x] (추가 지시) env.state `time`("3D 시각 HH:mm") · 극지방 sunrise/sunset null — Map3DEnvState.Time · SunText, 시험 2개
- [x] (추가 지시) accept 때 sln 1개 실패 원인 조사 — 아래

## accept 실패 조사(2026-10-21)
- 하네스 기록: accept 검사 pass 2887 · fail 1(전체 2888 = sln 시험 10개 프로젝트 합과 같음), 이름은 남지 않음.
- v2.6 에서 합쳐진 것: AGENTS.md(8ef0ab50) 한 개뿐 — facade · ColorsWith 커밋(280ef66a · 81689de1)은 이 작업 시작 전에 이미 기반에 있었다.
  sln 시험 프로젝트 중 GMaps.Ui · AGENTS.md 를 읽는 것은 Utils.Tests/SliderPrecisionContractTests(XAML 슬라이더 검사)뿐 — 새 슬라이더는 모두 IsSnapToTickEnabled=True, 매번 통과.
- 같은 코드로 sln 4회: 1회차(이전 턴)·sln1 통과 / sln2 실패 2(SupervisorSurvivalTests K1 external-kill 3408 ms · native-access-violation 5588 ms, 상한 3초) /
  sln3 실패 9(전부 CameraPopup.Tests.Survival — 실제 호스트 프로세스 · LibVLC · 시간 상한 시험).
- SupervisorSurvivalTests 단독 3회: 실패 2 · 3 · 1 — 매번 다른 충돌 종류(dispatcher-exception · fail-fast · native-access-violation · unhandled-exception)가 상한을 넘김, 재시작 3.5~6.7초 · 감지 0.7~3.4초로 흩어짐.
- 그때 PC: dotnet.exe 약 1000개 · testhost.exe 약 10개(다른 하네스 작업 동시 실행), Get-Counter 가 120초 안에 끝나지 않음.
- 이어서(중단 뒤 재개): dotnet.exe 약 1580개(부하 그대로). MapNav 70 · MapAux 95 · MapLayer 102 모두 통과(sky=1 배선 뒤).
  sln 다시 1회: 실패 1 — 같은 시험 native-access-violation running=3272 ms(상한 3000). `git diff --name-only 1e12f873 HEAD` 에 CameraPopup 파일 없음 = 시험 대상 코드는 작업 전과 같다.
- 결론: 이 작업 · v2.6 변경이 닿지 않는 CameraPopup 감독자(Supervisor) 시간 시험이 PC 부하로 흔들린 것. 시험은 고치지 않았다(약화 금지). 부하 없는 때 다시 돌려 확인 필요.

## 앱 쪽 붙일 자리(사용자가 직접)
- Dotnet.Monitoring.Solution/…/SetupPanels/Console/SettingsCatalog.cs:67 `Map3DSection(map3d)` · :336 Map3DSection 에 DraftBlock
- …/Console/ISettingsDraftBlock.cs:86 SensorModelsDraftBlock 과 같은 「초안 없는 블록」
- …/Views/Panels/SetupPanels/Console/SettingsConsoleView.xaml:535 근처 DataTemplate

## 발견
- Json.NET JObject.Parse 는 iso 글을 날짜로 바꿔 서식을 잃는다 — 시험은 DateParseHandling.None 으로 읽는다.
- 창 없는 시험에서 접힌 요소의 ActualHeight 는 예전 값이 남는다 — 보이기 판정은 Visibility 를 조상까지 본다.
- MapEdit EditComboScenarioTests 2건도 전체 실행 1회에서 실패 · 단독 3회 통과(시간 의존).
