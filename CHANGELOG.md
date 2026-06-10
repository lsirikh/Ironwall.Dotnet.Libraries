# Changelog

<!-- changelog-entries-start -->

## [2.7.1] - 2026-06-04

### Added
- **SymbolUpdate_DispatcherFreeze_Fix PRD** ([PRD](docs/prds/SymbolUpdate_DispatcherFreeze_Fix-prd.md) · [Plan](docs/plans/SymbolUpdate_DispatcherFreeze_Fix-prd-plan.md))

## [2.7.0] - 2026-06-04

### Added
- **Multisensor_Symbol_Fix PRD** ([PRD](docs/prds/Multisensor_Symbol_Fix-prd.md) · [Plan](docs/plans/Multisensor_Symbol_Fix-prd-plan.md))

## [Unreleased]

### Added
- **OverlayImage ZOrder 독립 영속화** ([PRD](docs/prds/OverlayImage_ZOrder_Independence-prd.md) · [Plan](docs/plans/OverlayImage_ZOrder_Independence-plan.md))
  - `IImageModel` / `ImageModel`: `ZOrder` 런타임 프로퍼티 추가 (DB 컬럼 없음, `MapLayers.ZOrder` SSOT)
  - `GMapImageMarker`: 생성자 `ZIndex=5` 하드코딩 제거, `IEditableMarker.ZOrder` setter `_imageModel` 동기화
  - `RestoreLayerVisibility`: `Panel.SetZIndex` 동기화 (`IEditableMarker.ZOrder` setter 경유)
  - `GMapDbSymbolService.BuildSchemeAsync`: 심볼 ZOrder band 시프트 마이그레이션 (`UPDATE Symbols SET ZOrder = ZOrder + 1000 WHERE ZOrder < 1000`)
  - `MapViewModel.SaveMarkerZOrderAsync`: 이미지→`UpdateMapLayerAsync`, 심볼→`UpdateSymbolZOrderAsync` 타입 분기
  - `EnsureUniqueZOrder` / `NormalizeAllZOrder`: band-aware 분리 (이미지 0~999 / 심볼 1000+)
  - `MoveMarkerUp/Down/ToTop/ToBottom`: 동일 band 내에서만 스왑/이동
  - `RefreshPropertyPanelZOrder`: 선택 마커 band 기준 순위 표시

### Refactored
- **ZIndex → ZOrder 네이밍 전사 통일** ([PRD](docs/prds/ZOrder_Naming_Unification-prd.md))
  - `ISymbolModel`, `SymbolModel`: `ZIndex` → `ZOrder` 프로퍼티 변경
  - `IEditableMarker`, `GMapBaseMarker`: 인터페이스 멤버 및 명시적 구현 `ZOrder`로 변경
  - `GMapImageMarker`: `IEditableMarker.ZOrder` 명시적 구현 추가 (GMap.NET `ZIndex`에 위임)
  - `IGMapDbSymbolService` / `GMapDbSymbolService`: `UpdateSymbolZOrderAsync`, `BatchUpdateZOrderAsync` 메서드명 변경 + DB 마이그레이션(`ZOrder` 컬럼 추가, `ZIndex` 이관)
  - `MapViewModel`: 내부 메서드명(`EnsureUniqueZOrder`, `NormalizeAllZOrder` 등) 전체 변경
  - `GMapPropertyBaseControl` / `BasePropertyStyle.xaml`: `MarkerZOrderDisplay` DP 및 바인딩 경로 변경



### Fixed
- **RDP/원격 환경 패닝 시 심볼 위치 점프 버그 수정** ([PRD](docs/prds/RemoteDesktop_PanFollowBug_Fix-prd.md) · [Plan](docs/plans/RemoteDesktop_PanFollowBug_Fix-prd-plan.md))
  - `GMapControl.cs`: `PositionChanged()` — 드래그 중 `ForceUpdateOverlays()` 무조건 호출 제거 (`!_core.IsDragging` 가드 추가). RDP 마우스 이벤트 압축 환경에서 심볼/타일 불일치 근본 원인 해소
  - `MapViewModel.cs`: `MainMap_OnCurrentPositionChanged()` — `MainMap.Position = point` 불필요 재설정 제거. `PositionChangedCallBack` 재진입으로 인한 `ForceUpdateOverlays()` + `RefreshVisibleTiles()` 이중 호출 차단
  - `GMapCustomControl.cs` / `MGRSGridOverlayService.cs`: `FormattedText` 생성 시 `pixelsPerDip=96` 하드코딩 제거 → `PixelsPerDip` 프로퍼티 기반 실시간 DPI 조회. 125%/150% 스케일 환경에서 라벨 크기 정상화
  - `GMapCustomControl.cs`: `OnInitialized`에 `RenderCapability.Tier` 진단 로그 추가 — RDP/소프트웨어 렌더링 환경 자동 감지

### Changed
- **OverlayMap MBTiles Provider 전환** ([PRD](docs/prds/OverlayMap_MBTiles_Provider-prd.md) · [Plan](docs/plans/OverlayMap_MBTiles_Provider-prd-plan.md))
  - `MBTilesOverlayMapProvider` 신규 — 인스턴스별 독립 SQLiteConnection, TMS 좌표 변환, 파일 검증
  - `TileGenerationService`: PNG 폴더 → MBTiles(SQLite) 단일 파일 쓰기 전환, 트랜잭션 배치(1000개)
  - `LruTileCache`: `BitmapImage` → `ImageSource`, thread-safe lock 추가, LoadTileImageSource에 실제 결선
  - `CustomMapService` / `CustomMapOverlayService`: StorageType 분기(MBTiles/PngDirectory), LRU 캐시 활성화
  - `GMapDbService`: `MbtilesPath`, `StorageType` 컬럼 idempotent ALTER 마이그레이션 추가
  - MBTiles 저장 경로: `C:\ProgramData\Sensorway\PIDS\maps\` → `{exe}\maps\` (상대 경로 통일)
  - `TileDirectory` 설정 항목 완전 제거 (`IGMapSetupModel`, `GMapSetupModel`, `MapViewModel`, 소비측 포함)

### Fixed
- **버스트 이벤트 시 지도 패닝 스턱 제거** ([PRD](docs/prds/SymbolUpdate_DispatcherFreeze_Fix-prd.md) · [Plan](docs/plans/SymbolUpdate_DispatcherFreeze_Fix-prd-plan.md))
  - `DetectionNatsSyncService.cs` / `MalfunctionNatsSyncService.cs`: `PublishOnUIThreadAsync(Normal=9)` → `Dispatcher.InvokeAsync(Background=4) + PublishOnCurrentThreadAsync` — 탐지/장애 이벤트마다 Normal 콜백이 Input 기아 유발하는 근본 원인 제거. NFR-01 검증 완료
  - `DeviceSymbolLookupModel.cs:60` `MarshalUpdate()` `DispatcherPriority.Normal(9)` → `Background(4)` — `_isFlushPending` 코얼레싱으로 35이벤트 버스트에서도 최대 1콜백만 큐잉됨
  - `GMapMarkerPidsControl.cs:447` `UpdateFOVPath` `BeginInvoke` Background 우선순위 명시 — Input(5) 마우스 기아 제거
  - `EventUiModule.cs:117-118` `OnDeviceFirstEvent += SetDeviceDetecting` / `OnDeviceEmpty += RestoreDeviceSymbol` 이중 경로 제거
  - `DeviceSymbolLookupModel.cs:109-113` `ProcessEvent(Fault)` `FaultedDetecting` 보존 가드 추가
  - `RedisBrokerService.cs` `ParseMessageItems` 이식 — JObject 형식 Redis 메시지 무음 소실 수정
- **PulseRing Canvas 중앙 정렬 수정** ([PRD](docs/prds/SymbolUpdate_DispatcherFreeze_Fix-prd.md))
  - `PidsMarkerStyle.xaml` Canvas Zero-size anchor(`Width=0/Height=0 + HA/VA=Center`) 패턴 적용
  - Canvas.Left/Top `-25` → `-40` (80px 링 정확한 아이콘 중심 정렬, 마커 크기 무관)
  - StrokeThickness `2` → `6` (가시성 개선)
- **Multisensor(Multi/SmartMultisensor2) 심볼 미동작 + 레이어 Hide 불량** ([PRD](docs/prds/Multisensor_Symbol_Fix-prd.md))
  - `MapViewModel.MatchMarkerToCategory("PidsSensor")`: `Multi`, `SmartMultisensor2` 추가 — 레이어 숨김/표시·카운트·줌 재적용 3곳 동시 수정 (BUG-1)
  - `DetectionNatsSyncService` / `MalfunctionNatsSyncService`: DeviceType 파싱 실패 시 `Fence` 오분류 fallback 제거 → Error 로그 + 이벤트 드롭 (BUG-2)
  - `NatsBrokerService.GetDevice(BrkDectection/BrkMalfunction)`: `SmartMultisensor2` switch case 추가 — 이벤트 무음 손실 방지 (BUG-3)
  - `DeviceFilterHelper`: `SmartMultisensor2` 독립 필터 case 추가
  - `GMapMarkerPidsControl.GetSizeForDeviceType`: `Multi`, `SmartMultisensor2` 32px 명시
  - `SymbolEventManager`: FenceGroup 전용 센서 `미등록 Device` WARN 제거 (로그 노이즈 감소)
  - `MapViewModel.InitializeDeviceSymbolIntegration`: 시작 시 심볼 등록 집계 Info 로그 추가
- **전체 조치보고 후 심볼 Detecting 고착 수정** ([PRD](docs/prds/BatchReport_SymbolRestore_Fix-prd.md))
  - `EventCardListPanelViewModel.ExecuteBatchReportAsync`: EntryId null 시 `_pendingEntries` 폴백 → `FindEntryByDevice` 2차 안전망 — EQM.Dequeue 보장
  - `HandleAsync(Detection/MalfunctionReportedMessageModel)`: 동일 폴백 패턴 적용
  - `SemaphoreSlim _batchReportGate` 추가 — 더블클릭 중복 실행 방지
- **이벤트 카드 10 events/sec 성능 개선** ([PRD](docs/prds/EventCardPerformance-prd.md) · [Plan](docs/plans/EventCardPerformance-prd-plan.md))
  - `EventCardListPanelView.xaml`: `<ListBox.ItemsPanel>` StackPanel 블록 제거 → WPF 기본 VirtualizingStackPanel 복원. `ScrollUnit=Pixel` 추가. Add당 378ms O(n) → 3ms 고정
  - `EventCardListPanelViewModel.cs`: `EnqueueCard()` 공개 메서드 추가 — NATS 스레드 Dispatcher 동기 블로킹 제거, 배치버퍼 경유
  - `EventCardBaseViewModel.cs`: `Dispose()`에 `Cts?.Cancel(); Cts?.Dispose(); Cts = null;` 추가 — Win32 WaitHandle 600개/분 누수 제거
  - `EventCardListPanelViewModel.cs`: `FlushPendingCards` `async void` → `void` 래퍼 + `async Task FlushPendingCardsAsync` 분리 — 타이머 콜백 예외 크래시 방지
  - `EventCardListPanelViewModel.cs`: `_cardByEntryId` `Dictionary` → `ConcurrentDictionary`, `.Remove()` 5곳 → `.TryRemove()` — 멀티스레드 경쟁 조건 제거
  - `DataGridSelectedItemsBehavior.cs`: `OnSelectionChanged` 전체 재생성 → 델타 HashSet 교체 — 1000개 선택 시 O(N) List 생성 제거
  - `DataGridScrollEndBehavior.cs`: `OnDataGridLoaded` unsubscribe-before-subscribe — 탭 전환 시 ScrollChanged 이중구독 방지
- **앱 종료 로그 노이즈 제거** (`a7d0762`)
  - `CustomMapOverlayService.Dispose()`: `OperationCanceledException` 분리 — Dispatcher 셧다운 취소 무시
  - `GMapDbService` / `GMapDbSymbolService` / `GatewayDbService`: `IOException` 분리 — MySQL close 패킷 소켓 실패 무시
- `TileGenerationService.GenerateTilesFromTifAsync`: `using var insertCmd` 스코프 오류 → `File.Move` IOException 수정
- `MapRegistrationStyle.xaml`: 진행률 표시 `{0:F0}` → `{0:F0}%` (% 기호 누락 수정)
- **이벤트 카드 제어기 번호 미표시** (`b69dc5b`, `72865ab`)
  - `MalfunctionEventCardView` / `DetectionEventCardView`: `ControllerId`(DB PK) → `ControllerDeviceNumber`(논리 번호) 바인딩 교체
  - `DeviceProviderService.FetchSingleSensorAsync`: `includeController:false` → `includeController:true` — SYNC_DEVICE 수신 후 Controller 링크 파괴 수정
  - `MalfunctionEventCardViewModel`: `ControllerDisplay` / `SensorDisplay` 프로퍼티 추가 — 장애 타입별 제어기/센서 표시 로직 명시화
- **Pulse 소나 애니메이션 미동작** (`fe17f0a`)
  - ScaleTransform(Freezable) `TargetName` 타겟팅 → FrameworkElement PropertyPath 방식으로 변경
  - `(UIElement.RenderTransform).(ScaleTransform.ScaleX/Y)` — WPF ControlTemplate 이름 스코프 불안정 문제 해결

### Performance
- **탐지 펄스 소나 패턴 확대** ([PRD](docs/prds/DetectionPulse_Ripple_Enlargement-prd.md) · [Plan](docs/plans/DetectionPulse_Ripple_Enlargement-prd-plan.md))
  - `PidsMarkerStyle.xaml`: `PulseEllipse`(30px) 제거 → `PulseRing1~3`(80px, `Stroke="#CCFF0000"`, `Fill="Transparent"`) 교체
  - Storyboard: 3개 링 0.4s 스태거(`BeginTime` 0s/0.4s/0.8s), 각 1.2s `CubicEase EaseOut` 팽창 → 3-링 소나 패턴

## [2.6.3] - 2026-05-27

### Fixed
- **SplashModule 아키텍처 결함 수정** ([PRD](docs/prds/PRD_SplashScreen_Startup_Gating.md)) — Option A: 기동 인프라 항상 등록 보장
  - `BootstrapCoordinator`, `ConnectionWatchdog`, 기동 Job 3개를 `SplashModule`에서 `ParentBootstrapper.RegisterBaseType()`으로 이전
  - `SplashModule` 삭제 — splash UI 유무와 무관하게 기동 인프라 항상 등록됨
  - `ISplashViewModelBase` 미등록 시에도 NATS 연결 게이팅(`IBootstrapCoordinator`) 정상 동작
  - 소비 프로젝트: `builder.RegisterModule(new SplashModule())` 제거 필요

---

### Added
- **이벤트 파이프라인 전체 성능 최적화** ([PRD](docs/prds/Event_Performance_Optimization-prd.md) · [Plan](docs/plans/Event_Performance_Optimization-prd-plan.md)) — IMPL-01~09 완료, 18/21

### Fixed
- **장애/탐지 이벤트 카드 제어기 번호 바인딩 수정** ([PRD](docs/prds/MalfunctionCard_ControllerNumber_BindingFix-prd.md) · [Plan](docs/plans/MalfunctionCard_ControllerNumber_BindingFix-prd-plan.md))
  - `MalfunctionEventCardView.xaml` L269 / `DetectionEventCardView.xaml` L294: `ControllerId`(DB PK) → `ControllerDeviceNumber`(논리 번호) 바인딩 교체
  - `EventCardViewModel.ControllerDeviceNumber`: DeviceNumber=0 → null 반환 방어 코드 추가 (Fallback 모델 "0" 표시 방지)
  - 테스트 2개 갱신 (`ControllerId` → `ControllerDeviceNumber` 검증으로 업데이트)
- **UI 동결(30초 드레인) 근본 수정** ([PRD](docs/prds/MapSymbol_DispatcherFreeze_And_LogNoise_Fix-prd.md) · [Plan](docs/plans/MapSymbol_DispatcherFreeze_And_LogNoise_Fix-prd-plan.md))
  - `GlobalSymbolUpdateManager` 신규 도입 — `DispatcherTimer(Background, 80ms)` dirty-set flush로 `InvokeAsync(Normal=9)` per-symbol 콜백 제거 → DataBind/Render/Input 기아 해소
  - `DeviceSymbolLookupModel.MarshalUpdate()` → `GlobalSymbolUpdateManager.MarkDirty()` 위임, `_isFlushPending` Interlocked 제거
  - `SymbolEventManager`: 그룹 전용 Fence 장비 WARN → INFO 강등 3개소 (false alarm 제거)
  - `RedisBrokerService.MessageSelector`: `JToken` 타입 체크로 `JsonReaderException` 폭탄 제거 (JSON Object → 조기 리턴)
- `ApplyCompositeStatus` — NATS 스레드→WPF STA 위반 (`MarshalUpdate` 코얼레싱 적용)
- `DetectionNatsSyncService` / `MalfunctionNatsSyncService` — EA BackgroundThread ObservableCollection 접근 Blocker (`PublishOnUIThreadAsync` 전환)
- `SoundAlarmController.OnQueueCleared` — `State`/`_lastEventTime` 미리셋으로 인한 Playing 고착
- `HandleAutoReport` / `HandleAutoRecovery` — `async void` → `async Task` + ContinueWith (미처리 예외 크래시, `AutoReportInFlight` 미리셋)
- `HandleAutoReport` API 실패 시 `NextRetryAfter` 미설정으로 인한 무한 재시도 (backoff 30s 추가)

### Performance
- **MapSymbol Pulse 애니메이션 성능 최적화** ([PRD](docs/prds/MapSymbol_PulseAnimation_Performance_Fix-prd.md) · [Plan](docs/plans/MapSymbol_PulseAnimation_Performance_Fix-prd-plan.md))
  - `PidsMarkerStyle.xaml`: `PART_EventStatusIndicator` DropShadowEffect 제거 (GPU off-screen pass 차단 해소)
  - `PidsMarkerStyle.xaml`: `PulseEllipse` BitmapCache 제거, 크기 반전(5→30px, Scale 1→0.17), Storyboard `HandoffBehavior=SnapshotAndReplace`
  - `PidsMarkerStyle.xaml`: ErrorBlink 6개 DoubleAnimation → `PART_IconContainer` 단일 애니메이션 (Animation Clock 6N→N 감소)
  - `GMapPidsMarker.PidsModel_Update`: FOV PropChanged 조건부 발화 (값 비교 캐시 + NaN sentinel, `CompositionTarget.Rendering` 누적 방지)
  - `GMapMarkerPidsControl.OnEventStatusChanged`: `UpdateMarkerAppearance()` 역방향 setter 제거, `EventStatus` 바인딩 OneWay 전환
  - `GMapMarkerPidsControl.OnFOVParameterChanged`: 0.5° 미만 변동 임계값 가드 추가
  - `GMapMarkerPidsControl.UpdateFOVPath`: 6× `GetTemplateChild` → `OnApplyTemplate` 캐시 필드 직접 참조
  - `DeviceSymbolLookupModel`: 핫패스 Info 로그 제거 (GC 압박 해소)
- `EventCardListPanelViewModel._cardByEntryId` — O(1) 인덱스 (기존 O(N) `ViewModelProvider.FirstOrDefault` 대체)
- `SymbolEventManager._deviceLookupById` — O(1) 보조 인덱스 + `TryResolveDevice` (O(N) fallback 3곳 제거)
- `EventQueueManager.Dequeue` — `_scratchPrevGroupStates.Clear()` 재사용 (매회 `new Dictionary<>()` 할당 제거)
- `FindEntryByDevice` — LINQ `OrderBy().FirstOrDefault()` → foreach min 단일 패스

---

## [2.6.2] - 2026-05-22

### Added
- **자동조치보고 이중경로 통합** ([PRD](docs/prds/AutoActionReport_DualPath_Fix-prd.md)) — Path A 타이머 제거, Path B 단일화

### Fixed
- `EventCardBaseViewModel` per-card `System.Timers.Timer` 완전 제거 — 이중 API 호출 / 20초 무한 재시도 / `GC.Collect()` 안티패턴
- `OnAutoReport` 구독자 없음 → UI 카드 영구 좀비화 (와이어링 완성)
- `EventEntry.EventId` 필드 추가 — 서버 이벤트 ID 수신 시점 보관

---

## [2.6.1] - 2026-05-20

### Added
- **사운드 타입 즉시 전환** ([PRD](docs/prds/SoundTypeSwitch_ImmediateStop_Fix-prd.md))

### Fixed
- 탐지↔장애 타입 전환 시 이전 사운드 미중지 (`StopAndPlayAsync` + `_switchSemaphore`)
- `SoundAlarmController` 3-Action → 1-Action `stopAndPlay` 리팩터

---

## [2.6.0] - 2026-05-19

### Added
- **Malfunction 복합 상태 및 FenceGroup 시각화 아키텍처 PRD** ([PRD](docs/prds/Malfunction_CompositeState_And_FenceGroup_Visualization-prd.md))

## [2.6.1] - 2026-05-19

### Added
- **배치 조치보고 이중 INSERT 및 Malfunction 심볼 복원 불가 수정 PRD** ([PRD](docs/prds/BatchReport_DualInsert_And_MalfunctionRestore_Fix-prd.md))

## [2.6.0] - 2026-05-19

### Added
- **배치 조치보고 이중 INSERT 및 Malfunction 심볼 복원 불가 수정 PRD** ([PRD](docs/prds/BatchReport_DualInsert_And_MalfunctionRestore_Fix-prd.md))

## [2.5.1] - 2026-05-15

### Added
- **GMapCustomControl 이미지 드래그/리사이즈 버그 수정 PRD** ([PRD](docs/prds/GMapCustomControl_ImageDrag_BugFix-prd.md))

## [2.5.0] - 2026-05-14

### Added
- **LayerPanel ContextMenu Enhancement PRD** ([PRD](docs/prds/LayerPanel_ContextMenu_Enhancement-prd.md) · [Plan](docs/plans/LayerPanel_ContextMenu_Enhancement-prd-plan.md))

## [2.4.0] - 2026-05-13

### Added
- **PRD_ImageOverlay_FileCopy_On_Register.md**

