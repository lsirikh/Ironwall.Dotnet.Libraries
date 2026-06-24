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
- **UI Modern Dark/Light 테마 디자인 시스템 — Phase 1 완료** ([PRD](docs/prds/UI_ModernTheme_DesignSystem-prd.md) · [Plan](docs/plans/UI_ModernTheme_DesignSystem-prd-plan.md))
  - 신규 leaf 어셈블리 `Ironwall.Dotnet.Libraries.Theme`(net8, MD/Colors 5.2.1 + MahApps 2.4.10, GMap/*.Ui 무참조). 토큰 딕셔너리 5종 — `Tokens.Light`(현재 출고 byte-identical, AD-6) / `Tokens.Dark`(Modern Dark) / `Tokens.Shared`(radius·density·font) / `Converters` / `Theme.Current`(스왑 컨테이너).
  - `IThemeService`/`ThemeService`: Add-new→Remove-old 토큰 dict 원자 스왑 + PaletteHelper(MD) + ThemeManager(MahApps) 듀얼 엔진 단일 Dispatcher 패스 + `ThemeChanged`(비-WPF 경로 재색칠) + R-17 중복차단 + 영속화 seam(`IThemeSettingsStore`). 토큰 팩토리/MergedDictionaries 주입형으로 헤드리스 테스트 가능.
  - `ThemeKeyLinter`(RISK-03): 참조-vs-정의 키 검증 + Light≡Dark 파리티 게이트. **빌드 0에러 · 테스트 11/11**(ThemeService 7 + 린터 4). 롤백 태그 `before-modern-theme-migration`, worktree `v2.12.0`.
- **Accounts.Ui 라이브러리 추출 — Phase 0 착수** ([PRD](docs/prds/Accounts_Ui_Library_Extraction-prd.md) R3 · [Plan](docs/plans/Accounts_Ui_Library_Extraction-prd-plan.md))
  - 외부 Monitoring 솔루션에 산재한 계정 UI(VM 11 + View 8 = **27파일**)를 신규 WPF 라이브러리 `Ironwall.Dotnet.Libraries.Accounts.Ui`로 이관 준비. **Gateway seam**(`IAuthGateway`/`IUserDirectoryGateway`/`IProfileGateway` + `DbAccountGateway` 어댑터)으로 데이터 접근 역전 → 후속 GOP API 연동(GOP-00) 시 VM 재편집 0.
  - 동반 결함 수정 예정: C-1 공유싱글톤 Clear, H-4 중복확인 경쟁, async void/ct 미전달, DeleteAccount 2중루프 버그, L115 토큰 평문 로깅·`"12345678"` 하드코딩 제거. 롤백 태그 `before-accounts-ui-extraction`, worktree `v2.9.22`.
  - **Phase 1 완료(빌드 0, 테스트 10/10)**: 신규 프로젝트 `Ironwall.Dotnet.Libraries.Accounts.Ui` + **Gateway seam**(`IAuthGateway`/`IUserDirectoryGateway`/`IProfileGateway` + `AuthResult` in `Accounts/Gateways`, `DbAccountGateway` 어댑터) + `SessionConfigService`/`ProfileImageService`(+`ProfileImageHelper`) + `AccountUiModule`(`useDbAuth` 플래그). 별도 `.Accounts.Ui.Tests`(10 테스트). P-CHK-2(Framework 충돌) 무해 확인.
- **맵 위 이동식 RTSP 스트리밍 팝업** ([PRD](docs/prds/Rtsp_Map_Popup-prd.md) v1.3 · [Plan](docs/plans/Rtsp_Map_Popup-prd-plan.md))
  - 맵 카메라 심볼 **더블클릭** → 카메라 우상단(중점 +오른쪽100/위100)에 **이동식 RTSP 영상 팝업**. Geo 앵커로 팬/줌 추종, 드래그 이동 위치를 **카메라별 DB 영속**(`CameraPopupPositions`, 다중 클라 공유)해 재오픈 시 복원. 멀티 팝업 + 중복=기존 포커스, **심볼 제거 시 자동 닫기(FR-13)**, 크게보기 384↔640.
  - 참조 솔루션 `Dotnet.Rtsp.Viewer.Ui`의 `Streaming(.Base)`(LibVLCSharp 3.9.4, **Hub 공유 디코더**) 이식. 맵 오버레이 airspace hole 회피 위해 **Hub WriteableBitmap(IsHubMode)** 경로. `CameraStreamPopupControl`/Style(관심지역·레이어 창 답습)+VM, `CameraConnectionAdapter`(Urls.RtspSub→RtspMain→Ip), 더블클릭 배선(GMapCustomControl 편집/일반 공통), `CameraPopupPositionStore`(DB+인메모리폴백).
  - NFR-07 패키지 정합(Caliburn.Micro 5.0.258→4.0.230, Autofac 8.4.0→8.3.0). 네이티브 libvlc/plugins 배포. code-review(opus). ※메인솔루션 Bootstrapper에 `StreamingModule` 등록 필요(미등록 시 팝업 비활성).
- **MBTiles 베이스맵 빈 영역 기본 타일(no-data tile)** ([PRD](docs/prds/BaseMap_NoData_DefaultTile-prd.md) · [Plan](docs/plans/BaseMap_NoData_DefaultTile-prd-plan.md))
  - MBTiles 베이스맵 커버리지 밖으로 팬/줌 시 흰 화면 대신 **깔끔/모던 기본 타일**(라이트 뉴트럴 #EEF1F5 + 우/하 1px #DFE3E8 hairline)을 맵 격자에 정렬해 타일링 표시.
  - `MBTilesMapProvider.DefaultTileBytes`(byte[]) 추가 + `GetTileImage`가 **정상 줌 & 타일 없음일 때만** 기본 타일 반환(맵 미로드·줌 범위 밖은 null 유지 → 로드실패 은폐/부모타일 폴백 보존). 공유 인스턴스 금지(`GetTileImageFromArray` 매 요청 새 PureImage → use-after-dispose 회피).
  - 신규 WPF 헬퍼 `DefaultTileImageFactory`(256×256, 96DPI/Pbgra32, Freeze, Dispatcher 마샬링, 1회 캐시). `MapViewModel` Init/Switch에서 UI 스레드 주입. Core(WPF 비의존)는 byte[]만 보관.
  - architect+code-reviewer(opus) 검증, xUnit 회귀 6케이스(결정 테이블 a/b/c). ※GMap.NET.Core는 미추적 벤더(고아 서브모듈)라 해당 1파일은 git 외 — 수동 백업 `MBTilesMapProvider.cs.bak-before-basemap-nodata-tile`.
  - **각 타일 정중앙에 센서웨이 로고** 배치(가로·세로 가운데 정렬, 타일 반복 → 빈 영역 워터마크 패턴). `sensorway.png`(150×50)를 GMaps.Ui Resources에 임베드(pack URI), `DefaultTileImageFactory`가 로드 실패 시 격자만 표시(graceful). 로고 크기/투명도/여백 상수화.
- **함체 임계값(Threshold) 설정 다이얼로그 — P1 라이브러리** ([PRD](docs/prds/EnclosureThresholdDialog-prd.md))
  - 함체 임계값(온/습도 상하한·진동) 편집 다이얼로그 신설 — 카메라 상세(Conductor.Collection.OneActive) 패턴 복제: `EnclosureThresholdDialogViewModel`+`EnclosureThresholdSettingViewModel`/View, `OpenEnclosureThresholdDialogMessageModel`, 함체 SelectionView '임계값' 버튼(단일선택).
  - **매핑 보강(BLOCKER급)**: `DtoToModelHelper`가 threshold_config(JObject)↔`EnclosureThresholdConfigModel` 양방향 매핑(이전 드롭). `EnclosureDeviceDto.ThresholdConfig` NullValueHandling.Ignore. `DeviceEquals`에 임계값 비교(null=빈객체 동등), `UpdateDeviceProperties` 임계값 복사. xUnit 7. (P2 메인솔루션 wrapper/핸들러/등록 별도)
- **스피커 방송서버(server_id) 배정** ([PRD](docs/prds/SpeakerServerAssignment-prd.md) v1.1 · [Plan](docs/plans/SpeakerServerAssignment-prd-plan.md))
  - 스피커 속성패널에 방송서버 드롭다운(ServerProvider) — 선택/변경 + 신규 추가 시 첫 서버 자동배정(서버 0개면 Inform). 12-Agent opus 시뮬레이션(5블로커/1High) 반영.
  - 매핑 비대칭 정합: 쓰기=`server_id`(int?, ShouldSerialize 차단으로 nested 미전송) ↔ 읽기=nested `server`. DeviceEquals server_id 비교 추가, UpdateDeviceProperties Server 복사(유령서버 차단), ServerProvider 신설(startup 적재+새로고침).
- **장비 속성패널 레이아웃 재설계 — Phase 1 (레이아웃+스크롤)** ([PRD](docs/prds/DevicePropertyPanel_Layout_Redesign-prd.md) v1.1 · [Plan](docs/plans/DevicePropertyPanel_Layout_Redesign-prd-plan.md))
  - 6 SelectionView(제어기/센서/카메라/스피커/함체/경광등)를 **4구역(장비공통·장비별·위치·그룹)** 으로 통일 + 헤더·**적용버튼 고정**, **속성영역만 세로 스크롤**(GroupBox 내부 2행 Grid: ScrollViewer/footer).
  - 신규 `Utils/Behaviors/BubbleMouseWheelBehavior` — 내부 ListBox(Groups) 스크롤 한계 시에만 부모로 휠 재전파(Groups 기존 세로스크롤 보존 + 중첩 휠 갇힘 해소).
  - 6차원 Agent 시뮬레이션(머지블로커 4 식별) 반영한 PRD v1.1. 바인딩/PasswordBox/ItemsSource/BindingProxy 1:1 보존(code-review opus: Critical/High 0).
- **장비 속성패널 — Phase 2 (Bearing/Altitude 왕복)**
  - 6패널 위치구역에 **Bearing(방위각 0~360°)·Alt(고도)** 편집 추가. `BaseDeviceViewModel.Bearing`(→Heading, set mod360 정규화)/`Altitude`, 6 SelectionViewModel(`CommonOrNullNullable` 공통값 + RefreshAll + ApplyButton HasValue 가드), 6 DeviceEquals(Heading/Altitude 비교).
  - **매핑 핫픽스(BLOCKER-1)**: `DtoToModelHelper.MapGeolocationToDto`가 Heading·Altitude를 실제 API 전송(이전 누락 → 방위각 저장 무효). `GeolocationDto.Altitude`→`double?`. `BaseDeviceModel` Altitude + 복사생성자 Heading/Altitude 복사.
  - **심볼 FOV 갱신**: `DeviceProviderService.UpdateDeviceProperties`에 Heading/Altitude 복사 + `SymbolEventManager.RegisterDeviceSymbol`에 `SetUpdate()` → Bearing 저장 시 지도 심볼 부채꼴 재렌더링.
  - **버그수정**: `CameraSelectionViewModel` ctor `RefreshAll()` 누락 복구. xUnit 7케이스(왕복/null보존/가드/직렬화) — 전체 81 통과.
- **Client/Server API v4.6 정합 — Phase 0 (S1~S5)** ([PRD](docs/prds/Client_API_v46_Conformance-prd.md) v3.1 · [Plan](docs/plans/Client_API_v46_Conformance-prd-plan.md))
  - **FR-0**: `IApiService.DeleteRequestAsync<T>(endpoint, body)` body-DELETE 오버로드 신설(벌크해제 공통 인프라, `PatchRequestAsync` 패턴)
  - **FR-1**: ActionEvent 1:N — `GetDetection/MalfunctionActionsAsync`가 `/{id}/actions`(복수) + `ApiListResponse<ActionEventDto>` 배열 반환(기존 단수 `/action`+단건 → 404/역직렬화 위험 제거)
  - **FR-5**: `DeviceGroupBulkRemoveResultDto` + `IDeviceApiService.RemoveDevicesFromGroupAsync`(body-DELETE 벌크 제거) — VM 1콜 교체는 후속(WIP 정리 후)
  - **FR-6**: `EventMappingCameraDto`/`SpeakerDto`에 `is_enable` 추가(Speaker PUT 422 해소)
  - **FR-7**: `GeolocationDto.heading`(0~360 FOV 방위) + `CameraPresetDto.is_restricted_zone`(감시금지구역) 추가 (restricted_actions는 v4.6 폐기로 미추가)
  - 검증: 5개 프로젝트 빌드 0에러, Messages 단위테스트 174/174 통과, code-review 무차단. PARK(후속): FR-1(b) RowDetails·FR-2·FR-3·FR-8·FR-10~17(NATS/외부의존) — Plan 참조
- **디지털 줌 (Digital Zoom)** ([PRD](docs/prds/DigitalZoom_RenderTransform-prd.md) · [Plan](docs/plans/DigitalZoom_RenderTransform-prd-plan.md))
  - MaxZoom 초과 시 `GMapCustomControl.RenderTransform = ScaleTransform`으로 타일+마커+오버레이 균일 소프트 확대(1.5x/2.0x). ScaleMode/Zoom/_core 불변, 히트테스트 무보정(WPF 자동 역변환)
  - 휠/슬라이더/버튼 라우팅(`DigitalZoomLevel`/`SliderValue` DP), 축척바 거리 숫자 ÷배율, 맵 전환 시 리셋
  - UX: 슬라이더 라벨 "18+"/"18++"(오렌지 구분, 고정 너비), AdornerDecorator ClipToBounds로 윈도우 컨트롤 침범 차단
- **OverlayImage 회전 편집** ([PRD](docs/prds/OverlayImage_Rotation_Editing-prd.md) · [Plan](docs/plans/OverlayImage_Rotation_Editing-prd-plan.md))
  - `GMapCustomImage` 회전 핸들(초록 원) 드래그 편집 + 중심 이동 핸들. `UserRotation`/`MapCorrectionRotation`/`EffectiveRotation` 분리(맵 회전이 사용자 편집값 파괴 방지)
  - 좌표계 단일화: 렌더·히트·드래그 델타가 동일 `RotateTransform(EffectiveRotation)` 공유(`InverseRotateMouse`)
  - 마커 회전 속성 UI: TextBox → Slider + 직접입력 TextBox + ° (MarkerBearing 동기화)
- **줌 동작 개선** ([PRD](docs/prds/GMap_Zoom_Improvements-prd.md))
  - 슬라이더 눈금/Max 동기화(ZoomMax/ZoomMin INPC 래퍼 + 정수 스냅), MaxZoom 경계 휠 점프 차단
  - 편집모드 오브젝트 위 휠줌/드래그팬 통과(`IgnoreMarkerOnMouseWheel`, MarkerEditAdorner `HitTestCore` 핸들 한정)

### Fixed
- **디지털 줌 활성 시 카메라 RTSP 팝업/연결선 좌표 어긋남 수정** ([PRD](docs/prds/CameraPopup_DigitalZoom_Alignment-prd.md) v1.0 · [Plan](docs/plans/CameraPopup_DigitalZoom_Alignment-prd-plan.md))
  - 증상: 디지털 줌(1.5/2.0x) 시 팝업·빨간 연결선이 카메라 심볼에서 떨어짐(화면 중심에서 멀수록·줌 클수록 선형↑). 디지털 줌만 인/아웃 시 팝업 미추종(제자리 고정).
  - 근본원인: 디지털 줌 = `GMapCustomControl.RenderTransform=ScaleTransform(s,s,W/2,H/2)`. 마커는 transform '안'(WPF가 `RenderTransform.Inverse` 자동 적용), 팝업은 형제 `PropertyPanelCanvas`(transform '밖')에서 `FromLatLngToLocal` raw inner 좌표를 그대로 사용 → **좌표 도메인 비대칭(RC-1)**. 디지털 줌은 `_core.Zoom` 불변이라 `OnMapZoomChanged` 미발화 → `RefreshCameraPopupPositions` 미호출(RC-2).
  - 수정: `GMapCustomControl`에 **팝업 전용** `InnerToOuter`/`OuterToInner`(중심 W/2,H/2 기준 ScaleTransform 정/역, `ActualWidth<=0`·`scale=1` 항등 가드) 신설 → 팝업 최초/추종 위치·연결선 끝점(`OpenCameraStreamPopupAsync`/`RefreshCameraPopupPositions`)을 outer 보정, 드래그 저장은 `OuterToInner`로 역보정 후 `FromLocalToLatLng`. `OnMapDigitalZoomLevelChanged`·`MainMap_SizeChanged`에 `RefreshCameraPopupPositions` 연결(RC-2). **scale=1이면 항등 → 디지털 줌 미사용 회귀 0**, 마커/격자/스냅 무손상(이중보정 회피, 불변식 준수).
  - 검증: 회전 독립 합성 확인(V-01: `ApplyMapRotation`은 `Bearing`만 변경), GMaps.Ui 빌드 0에러, 격리 단위테스트 61통과(DigitalZoom 7 신규: 항등/왕복/중심불변/보정정확/단조성/배율테이블), code-review(opus) **MERGE**(Critical/High/Medium 0). ※메인솔루션 재빌드 후 런타임 검증 대기.
- **장비 패널 CRUD Temp-state 통일 (Phase 1, PR-A/B/C)** ([PRD](docs/prds/DevicePanel_TempState_Unification-prd.md) v1.1 · [Plan](docs/plans/DevicePanel_TempState_Unification-prd-plan.md))
  - 설계 전환(사용자 결정): 직전 DeviceGroup B모델(추가 즉시 Create)을 **Temp-state로 통일 환원** — 서버가 미완성 placeholder 거부(422)·Sensor는 controller_id FK 필요로 즉시등록 불가 → 7패널(Controller/Camera/Sensor/Speaker/Enclosure/Lamp + DeviceGroup) 일관. 5-Agent PRD 검토 + opus 코드리뷰(머지차단 2 + High 3) 반영.
  - **Temp-state 베이스 템플릿**(`BaseDataGridMultiPanelViewModel`): `ExecuteCreateAsync`(필수필드 사전검증→보류)/`ExecuteSaveUpdatesAsync`/`NotifySaveResultAsync`(sanitize 통지)/`SanitizeDetails`(민감정보 마스킹)/`ShouldProjectToProvider`. 추가=로컬 Draft(Id≤0) Add(서버 미호출), Save=일괄 Create+Update+재조회+생존자(실패/보류) 복원.
  - **Draft 격리**(근본해결): CollectionChanged.Add Id≤0 미투영 → 공유/타입드 provider 오염·이중행 동시 차단. Insert는 `_processGate`로 직렬화(Save 진행 중 경합 방지).
  - **Id≤0 게이팅**(Critical 버그 차단 — 미저장 그룹 `POST groups/0/devices`·낙관적 desync): 그룹 장비추가 버튼 비활성(`CanAddAssign`)+메서드 가드, 배정 다이얼로그 groupId/후보/newId 가드, verify-after-success(응답 `AssignedDeviceIds` 기준), 센서 제어기 드롭다운·PIDS 픽커 Temp(Id≤0) 제외.
  - **UX/보안**: 미저장 행 시각마커(`IsDraft` 배경/툴팁) + 화면전환 시 미저장 손실 비차단 알림. `Uninitialize` 빈 catch 로깅화, Controller 전체 DTO 로깅 제거, 생존자 행 Index 재부여.
  - 검증: Devices.Ui/GMaps.Ui/Events.Ui 빌드 0에러, 불변식 grep 7/7. 후속(Phase 2/3): Speaker `server_id` FK, Enclosure ThresholdConfig UI, Camera RTSP.
- **제어기 추가 HTTP 422 수정 + API 422 응답 본문 보존/진단 로깅** ([Report](docs/reports/Controller_422_Fix-report.md)) — 머지 `881ac5a`
  - 근본원인(4각도 Workflow 조사): `ControllerDeviceModel` ctor가 Camera/Speaker/Enclosure/Lamp와 달리 `DeviceType` 미설정 → `ToControllerDeviceDto`가 `type_device="NONE"` 전송 → 서버 enum 검증 422. **제어기에서만** 발생한 이유.
  - **수정**: `ControllerDeviceModel` ctor에 `DeviceType = EnumDeviceType.Controller`(peer 4모델과 동일 패턴 누락 보완).
  - **진단 인프라**: `ApiMessageHelper`의 3개 비성공 분기가 FastAPI `{"detail":[...]}`를 `MissingMemberHandling.Ignore`로 빈 객체 역직렬화→**본문 폐기**하던 결함 수정 → 표준 envelope면 그대로, 아니면 본문 전문을 `Error.Details`에 보존(FR-8 보완) + `ApiResponse.StatusCode` 추가. Controller Insert에 요청 DTO/실패 422본문 로깅.
  - 검증: Devices.Ui·Events.Ui 빌드 0에러, Messages 182/182(신규 `ApiMessageHelperErrorTests` 3건 포함) 통과, code-review(opus) 머지차단 0.
  - **차기 422(머지 `a916d59`)**: 로그가 다음 거부 필드 표출 — `{"field":"ip_port","message":"Input should be greater than or equal to 1"}`. 즉시-POST가 `ip_port=0` 전송. → Controller/Camera/Lamp Insert에 유효 placeholder 포트 **502**(통합테스트 검증값) 부여(`ip_address=""`는 서버 허용). Camera/Lamp/Speaker/Enclosure Insert에 실패 `Error.Details` 로깅 추가(Camera/Lamp 비번 보유→요청본문 미로깅). 후속: Sensor ctor DeviceType 미설정(다중 서브타입→별도 설계).
- **DataGridPanel CRUD 라이프사이클 베이스 통일 — Phase 1 (베이스 + Event 4패널)** ([PRD](docs/prds/DataGridPanel_Delete_Centralization-prd.md) v2.0 · [Plan](docs/plans/DataGridPanel_Delete_Centralization-prd-plan.md))
  - 2개 Workflow 전수 감사(삭제+저장)로 확정: Event 4패널이 **삭제 시 Id≤0(Draft) 무가드 → Id=0 DELETE(404/유령행)**, **Save가 Create 후 Id write-back 없이 Insert 루프 → 재Save 유령중복**. code-review(opus) 반영(H1/H2).
  - **베이스 `ExecuteDeleteAsync` 공통 헬퍼**(`BaseDataGridMultiPanelViewModel<T>`): Id≤0 로컬만 제거(API 미호출) / Id>0 DELETE + 성공 시에만 제거(verify-after-success) / 취소(OCE) 재던지기 / 부분실패 누적 후 **InfoPopup 통지(중앙화)**. abstract·virtual 계약 변경 없음 → device 6패널 무영향(증분 안전).
  - **Event 4패널**(Detection/Malfunction/Connection/Action): 무가드 삭제 루프 → `ExecuteDeleteAsync`. Save Insert 루프에서 `created.Id`를 model에 **write-back**(재Save 재Insert·유령중복 차단).
  - 검증: ViewModel/Events.Ui/Devices.Ui 빌드 0에러, Events.Ui 테스트 300통과/11실패=baseline(3069ba1) 동일(**회귀 0**).
- **DataGridPanel CRUD 통일 — Phase 2 (Device 6패널 B모델)** ([PRD](docs/prds/DataGridPanel_Delete_Centralization-prd.md) v2.0 · [Report](docs/reports/DataGridPanel_CRUD_Phase2_3-report.md)) — 머지 `a84dab6`
  - architect(opus) 정밀 맵: 6패널 구조 동일(A모델). `CreateXxxAsync` 래퍼가 `Task<bool>`로 서버 `ApiResponse<Dto>.Data`(Id) 폐기 = 유령중복(#13) 직접원인.
  - **Insert(5패널 Camera/Speaker/Enclosure/Lamp/Controller)**: Draft(Id=0) → `_processGate` 안 즉시 `CreateXxxAsync` → **FetchAllDevicesAsync+DataInitialize로만 반영**(코드리뷰 C1: 타입드 provider=공유 단방향 투영이라 수동 Add 시 FetchAll 투영과 이중행 → 수동 Add 제거).
  - **Save(6패널)**: insertList(Id≤0 Create) 루프 제거 → `FetchXxxAsync` complete 가드 + `Where(Id>0)` Update 전용(유령중복 차단). 5패널 Fetch 전페이지+complete 튜플화.
  - **Delete(6패널)**: 베이스 `ExecuteDeleteAsync`(Id≤0 로컬/Id>0 verify-after-success/부분실패 통지) + `_processGate` 직렬화.
  - **Sensor 특수**: controller_id FK라 즉시 Create 불가 → Insert는 Draft 유지, Save에서 `Controller.Id>0` Draft만 커밋 후 로컬 Draft 제거(코드리뷰 H1: 이중행 방지)+FetchAll 서버본 반영, 미선택은 보류+안내(Draft 보존).
  - code-review(opus) 2라운드: C1(이중행)/H1(Sensor 커밋 이중행)/M1 수정 후 머지차단 0 검증. 빌드 0에러. ⚠ 런타임 검증 필요(즉시 POST 동작).
- **DataGridPanel CRUD 통일 — Phase 3 (조치보고 멱등)** ([PRD](docs/prds/DataGridPanel_Delete_Centralization-prd.md) v2.0 · [Report](docs/reports/DataGridPanel_CRUD_Phase2_3-report.md)) — 머지 `bc53164`
  - 수동 조치보고(ReportDialog→EventCard.SendAction)가 자동/자동복구/배치의 in-flight 가드(`_inFlightEventIds`)에 미참여 → 수동×자동 동일 EventId 중복 ActionEvent 위험.
  - **싱글톤 `IActionReportGuard` 추출**: EventCardListPanel 3경로 + 수동 2경로(Detection/Malfunction SendAction)가 동일 인스턴스 공유. 진행 중이면 수동 스킵(중복 차단). 가드 의미론 `eventId>0` 기준 통일(AutoRecovery Id≤0 키 점유 제거 — 의도).
  - code-review(opus): Critical/High 0, 3경로 동치 확인. 빌드 0에러, 테스트 300통과/11실패=baseline(회귀 0).
- **장비 패널 CRUD↔API 연동 정합 — DeviceGroup B모델 + Controller 페이지네이션 (Phase 1)** ([PRD](docs/prds/DevicePanel_CRUD_API_Sync-prd.md) v1.1)
  - 16-Agent 분석 + 2-Round 시뮬로 식별: 그룹 추가/삭제/수정이 API와 desync(추가후 사라짐·삭제후 잔존·수정 시 무관 그룹 중복생성). 원인=클라이언트 pending(Id=0)+Save 일괄 diff 모델(서버/API는 정상). 10-Agent 코드리뷰 반영.
  - **DeviceGroup B모델 전환**: 추가=즉시 `CreateDeviceGroupAsync`(반환 서버 Id 반영, pending 미발생) / 삭제=API 성공 시에만 provider 제거(verify-after-success) / Save=Update 전용(Insert·Delete diff 루프 제거 → 유령 중복생성 차단) / Reload=서버 재조회 swap-on-success(유령 청소)
  - **Controller**: Save 비교 fetch `limit:20` 단건 → 전 페이지 루프(20건 초과 편집 소실 해소)
  - **견고화**: 그룹/제어기 fetch 페이지네이션 + 완전성 신호(>100 누락·중간페이지 실패 시 Save 보류), OnActivate/삭제를 `_processGate`로 직렬화(초기로딩 경합·공유 CTS 조기취소 방지), 실패 시 InformDialog 통지
  - 멤버십(`AssignDevicesToGroupAsync`)은 정상이라 미변경. 후속(Phase 2): 나머지 6개 장비 패널 일관 적용.
- **탐지/장애 이벤트 처리 오염 수정 — Phase 1** ([PRD](Docs/prds/EventProcess_ContaminationFix-prd.md) v1.1 · [Plan](docs/plans/EventProcess_ContaminationFix-prd-plan.md))
  - 구현 전 13-Agent 재대조 검증(§11): EB7 false positive 제외, 선행 DeviceApi C1(NATS DELETED) 미구현 확인 → EB3 메서드만(트리거 dormant). 10-Agent 적대 코드리뷰 반영(머지차단 결함 0).
  - **EA2(CRITICAL)**: `DetectionEventPanelViewModel.DataInitialize` 가 fetch 전 기존 이벤트를 Remove/Clear + 실패 시 빈 결과(예외 미전파)로 화면 전체 공백·복구불가되던 것을 **swap-on-success**로 수정(`PagedResult.Success` 플래그로 API실패 vs 빈결과 구분, 실패 시 기존 보존 + 팝업)
  - **EC2/EC5**: 조치보고 Auto/AutoRecovery/Batch 3경로가 동일 EventId 동시 호출로 서버 중복 조치보고/NATS 중복발행 → `_inFlightEventIds` 멱등 가드(수동 다이얼로그 경로는 후속)
  - **EA3**: 조치보고 API 실패해도 다이얼로그가 닫혀 성공 오인식 → `SendAction`→`Task<bool>`, `ClickOk` 결과 검사(실패 시 다이얼로그 유지 + 오류 팝업)
  - **EA7**: 저장 배치 한 건 실패로 전체 중단·무응답 → per-item 부분실패 수집 + 실패 시 재로드 생략(편집 보존) + 팝업
  - **EB1**: `NatsSyncService`(Detection/Malfunction) `IService` 미등록으로 종료 시 NATS 구독 미해제 → `As<IService>()` + 멱등 구독(`-=` 후 `+=`)
  - **EB2**: 표시 카드 무한 증가 → `MAX_EVENT_CARDS=500` 하드캡(오래된 카드 제거)
  - **EB3**: `EventQueueManager.RemoveByDevice` 추가(장비 삭제 시 고아 이벤트 정리, Dequeue 재사용) — 트리거는 선행 DeviceApi PRD C1 소관(현재 dormant)
  - 후속(별도): 수동 조치보고 멱등 통합, Malfunction swap-on-success, DataInitialize 구독토글 동시성, Phase 2(EC1/EC7/EB6/EA1)·Phase 3
- **격자 스냅 정확도 수정 — 픽셀 도메인 + 라인/교점 차등 가중치** ([PRD](docs/prds/GridSnap_System-prd.md) v1.2 · [Plan](docs/plans/GridSnap_System-prd-plan.md))
  - **RC-1**: 시각 격자(화면 픽셀 원점)와 스냅 수학(지리 0° 원점) 불일치로 보이는 선/교점에서 최대 `gridPx-1 px` 어긋나던 "중점 이탈 스냅" 버그 수정 → `SnapGridOverlayService.ComputeOrigin`/`Snap` 단일 원점으로 통일(시각 격자 = 스냅 격자)
  - **RC-2**: `MarkerEditAdorner._grabOffset`(그랩지점−중심) 도입 → 클릭 지점이 아닌 마커 **중심**이 격자에 스냅(12px 점프 제거)
  - **RC-3**: 교점 가중치(`rCross=gridPx×0.25`) > 라인 가중치(`rLine=max(gridPx×0.15,3px)`) — 교점이 라인보다 넓게/강하게 흡착(데드존 없는 합집합)
  - **RC-4**: `GMapCustomImage` Move 시 `SnapBoundsCenter`로 AABB 중심 픽셀 스냅(기존 미구현)
  - 맵 회전(`MapRotation`≠0) 시 스냅 비활성 가드. DigitalZoom 역보정 불필요(`TransformToAncestor`가 컨트롤 RenderTransform 제외 → 이중보정 방지)
  - **RC-5(v1.3)**: 스냅 대상을 **Adorner 중앙 이동핸들**(`RenderSize/2`)로 교정. 이동핸들은 라벨 포함 bbox 중앙이고 Position 앵커는 아이콘 중앙(`Offset=-_model/2`)이라 라벨 시 둘이 달라 핸들이 격자에서 이탈하던 것을, 핸들 화면위치를 스냅한 뒤 `Position += (handleTarget−handleNow)` 델타 보정으로 핸들이 정확히 격자선/교점에 안착하도록 수정
  - **RC-6(v1.4)**: 우측 영역 **세로줄 누락** 버그 수정. `MAX_GRID_LINES=100`이 폭 > `100×gridPx`(예 gridPx=16→1600px)에서 세로선을 우측에서 잘라내던 것을, 축별 동적 가드(`min(ceil(dim/gridPx)+2, 2000)`)로 교체해 화면 전체 커버
  - **RC-7(v1.4)**: 격자를 **맵에 고정**(geo-anchored). 화면 고정 원점(`gridPx-(w%gridPx)`)을 고정 지오 앵커 `FromLatLngToLocal` 화면좌표 위상정렬(`ComputeOrigin(ctrl,gridPx)`)로 교체 → 패닝 시 격자가 맵과 1:1 이동, 줌은 픽셀크기 유지. DrawGrid·마커스냅·이미지스냅 3경로 동일 원점(시각=스냅 단일원천 유지)
- **마커/이미지 히트테스트 AABB 통일** ([PRD](docs/prds/MarkerHitTest_AABB_Fix-prd.md))
  - `GetMarkerAtScreen` 원형 반경(`Math.Max(W,H)/2+8`) → Width×Height AABB + `-Bearing` 역회전 보정 + screenWidth 캐시
  - `GetImageAtScreen` Opacity≤0 클릭 차단 + 회전 보정
- **GMapImageMarker 이중 회전 제거** (줌→회전 누적 버그) — `GMapMarkerImageControl.OnRender`의 `PushTransform(Bearing)`이 base RenderTransform과 중복되어 2배 과회전 + 줌 위치 드리프트 발생하던 것 제거

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

