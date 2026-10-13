# PLAN — 센서 설치 위치 프리셋 후속(t-20261013-115417-837d)

## 목표
앞 작업(t-20261013-111040-db54 · v2.6 7f3dc968)이 남긴 PRD §10 "남은 일"을 끝낸다.
명세: `docs/prds/sensor-mount-preset-prd.md`(첨부 — 저장소에 없어 이번에 v1.1 로 더함) · storyboard · wireframe · 스케치 1 · 2.

## 진행
- [x] 1. 탐지 범위 = 속성 프리셋 탐지 거리(`FenceSensor.RangeM` · `FenceWorld.RangeOf(FenceSensor)`) · 권장 간격 넘음 삼각형 표지(`FenceWorld.UseSpacingAlerts` · `FenceScene.SpacingAlertMarks`)
- [x] 2. 펜스 보기 끌기 = 그 칸 붙는 점(`WiringViewModel.PresetDrag.cs` · `FencePresetDrag` · `FenceCanvas` BeginPresetDrag/MovePresetDrag/FinishDrag) · Esc · Alt · Tab · Ctrl+←/→ · Alt+↑/↓
- [x] 3. 입구: 장비 창 도구줄 [센서 모델](Extra → 좁으면 [⋯]) · 설정 창 블록 `SensorModelsSettingsBlockView(Model)` + 진입 메시지 `OpenSensorModelsMessage` · `ISensorPresetLauncher`(WiringLauncher 구현)
- [x] 4. `SensorPresetSettings`(appsettings 읽기) · `SensorPresetRepositoryFactory` · `ReadOnlySensorPresetRepository` · GMapDbModule 등록 교체
- [x] 5. 시험 MNT-200~233(Devices.Ui) · MNT-240~245(Monitoring.Models · sln) · PRD §10 갱신
- [ ] 6. 솔루션 전체 시험 확인(하네스 검사)

## 결정
- 따로 고친 값(`SensorMountOverride`)엔 붙는 곳 갈래가 없다 → 펜스 보기 끌기는 가로 · 높이(지중 점은 펜스와 거리도)만 바꾼다. 그래서 지중 프리셋은 바닥 점만, 그 밖은 바닥 점 빼고 붙는다(`WiringPresetPlan.SnapChoices`). 갈래를 바꾸려면 다른 프리셋을 고른다.
- 칸을 넘어 끌면 자리 번호(Panel)를 `BayIndexOf` 의 거꿀로 고른다(`WiringPresetPlan.PanelForBay`) — 9자리 갈래(Spot)는 그대로.
- 끄는 동안 붙는 점은 "포인터 아래 칸" 것만 보인다(망 200칸에서도 가볍게). 반경 없이 늘 가장 가까운 허용 점에 붙는다(W2 와 같다).
- 키보드: 끄는 중 Tab/Shift+Tab = 이 칸 다음/이전 붙는 점(끄는 중에만 소비 — 칩 사이 Tab 이동을 깨지 않게). 끌지 않을 때는 기존 "다음 격자 점" 키 Ctrl+←/→ 가 프리셋 센서에선 다음 붙는 점(칸 끝이면 옆 칸). Alt+↑/↓ = 높이 10 cm(Shift 1 cm) 따로 고친 값.
- 여러 대를 함께 끌면 옛 9점 격자 끌기 그대로(프리셋 끌기는 한 대만).
- 권장 간격 넘음 표지는 탐지 범위를 꺼도 그린다(쓰임 = 알림).
- GopApi 는 자리만(NotSupported)이라 `ReadOnlySensorPresetRepository(GopApi, 읽기 대체 = 회사 기본 7종 메모리)` — 표지 "GOP API(읽기 전용)" · 편집 잠금. 설정 키가 없거나 모르는 값이면 GisDb. 라이브러리 선례(Symbol3D)대로 앱 폴더 appsettings.json 을 직접 읽는다(루트 · AppSettings 둘 다).
- 설정 창 레일은 호스트(`Dotnet.Monitoring.Solution` `SettingsCatalog.Build`)가 정한다 → 라이브러리는 블록 · 메시지까지만. 호스트 배선은 PRD §10 에 적었다(이 작업 범위 밖 저장소).

## 발견
- 펜스 보기에는 이미 "9점 격자 끌기"(FenceCanvas SnapMove · 빨강 점)가 있다. 프리셋으로 서는 센서는 이 대신 프리셋 붙는 점 끌기로 간다.
- 시험 환경엔 Application 이 없어 `SelectedPhotoCaption`(pack URI)을 부르면 UriFormatException — 시험에서 부르지 않는다.
- 펜스센서 기본 속성(권장 2.5 m)은 결선 기본 배치(3 m)에서 늘 "간격 넘음"을 낸다 — 사실대로 보이는 것.

## 남은 일
- 헤디드(실창) 확인 목록은 PRD §10 끝.
- 호스트 배선(설정 창 레일 "센서 모델" 절 · appsettings 키 추가)은 호스트 저장소 작업.
