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
- **GMap 툴바 CPU/GPU/RAM 사용량 표시 — 우측 정렬 아이콘+% 칩** ([PRD](docs/prds/GMap_SystemResource_Indicator-prd.md) · [Plan](docs/plans/GMap_SystemResource_Indicator-prd-plan.md) · 태그 `before-sysres-indicator` · worktree `feature/sysres-indicator` · 신규 라이브러리 `Ironwall.Dotnet.Libraries.SystemResources` + GMaps.Ui)
  - **신규 라이브러리 `SystemResources`**(net8.0-windows, Base만 의존, 재사용 가능): OS 네이티브 PDH(pdh.dll)+kernel32로 CPU/GPU/RAM 사용률 취득. **보안**: LibreHardwareMonitor(WinRing0 커널드라이버=Defender 악성탐지 CVE-2020-14979) 배제 — 커널드라이버·관리자권한·서드파티 네이티브 바이너리 불요.
  - **로케일 독립**: `PdhAddEnglishCounterW`(Perflib 인덱스 기반)로 ko-KR Windows에서도 영문 카운터 해석. CPU=`% Processor Utility`(폴백 `% Processor Time`, Turbo>100% clamp), GPU=`\GPU Engine(*)` 와일드카드→(luid,phys,eng) 집계 busiest(멀티GPU 블렌딩 방지), RAM=`GlobalMemoryStatusEx.dwMemoryLoad`.
  - **설계**: `ISystemResourceMonitor : IDisposable`(IService 미구현=이중권위 회피). **WPF 비의존**(NFR-06) — 모니터는 타이머를 소유하지 않고 소비자(MapViewModel)의 UI-스레드 `DispatcherTimer`가 `Sample()` 구동 → 락/크로스스레드 마샬/재진입 원천 소멸(백그라운드 타이머+네이티브 핸들의 P0 UAF 크래시 회피). Fail-safe(PDH 실패=전체 N/A, UI 미전파, 1회 로깅). 히스테리시스 색 전환(정상 시안/경고 앰버/위험 빨강, 62-57/87-82 데드밴드).
  - **UI**: `MapView.xaml` DockPanel 우측 `Dock=Right` 3칩(아이콘 `Cpu32Bit`/`Gpu`/`Memory` + 고정폭 수치 + 절대값 툴팁), GPU 부재 시 Hidden(공간 예약), 전부 DynamicResource(테마 스왑). 모니터는 활성 시 Start(멱등)·비활성 시 타이머만 정지(핸들 유지=워밍업 보존, 모든 deactivate 경로 대칭).
  - **검증**: SystemResources 빌드 0 + **단위테스트 22/22**(GpuAggregator·Hysteresis·Monitor fail-safe/워밍업/생명주기) · GMaps.Ui 전체 빌드 0오류 · **실기 PDH 실측 정합**(CPU/GPU/RAM 실값). ⚠PackIcon 렌더·MonoFont·narrow창·통합 시각(VER-03/04/07·MANUAL-01)은 앱 재빌드 후 런타임 검증.
- **Line/Area 심볼 리사이즈 — 어도너 박스 리사이즈로 폴리라인·폴리곤 크기조절** ([PRD](docs/prds/LineArea_Symbol_Resize-prd.md) · [Plan](docs/plans/LineArea_Symbol_Resize-prd-plan.md) · 태그 `before-linearea-resize` · worktree `feature/linearea-resize` · GMaps.Ui)
  - **근본원인**: Line/Area(`IsClosedPath` 닫힌폴리곤)는 `LinePoints`(위경도) 기반이라 크기가 파생값(`UpdateLineGeometry`가 매 리드로우 W/H 재계산) → 어도너 W/H 리사이즈 무효 + line엔 핸들 미렌더. **해법(🅐)=박스 리사이즈로 점을 중심 기준 스케일**.
  - **FR-01**: `LineGeometryUtils.Scale`(순수·퇴화가드 ε/IsFinite/부호반전) + `ILineEditableMarker.ApplyGeometry` seam(GMapLineMarker/PidsGroup, SyncModelPoints 4단계 규약).
  - **FR-02/03**: 어도너 핸들 노출(코너=모든 line·변=닫힌폴리곤) + `GetHandleBounds`=ActualLineBounds 통일(짧은선 불일치 해소), `ProcessLineScale`(시작 bbox 대비 절대배율·줌 stale 방지, TransformToAncestor로 map 좌표, Position=새 bbox중심).
  - **FR-04 P0**: line 스케일은 W/H 불변→`HasChanges=false`→기존 Undo가 **미기록+즉시영속 파괴적**이던 결함을 신규 스냅샷 `LineGeometryCommand`(점+Position 복원, isImage=false)+`RecordLineGeometry`(HasChanges 우회)로 정합. **FR-05** ESC=스냅샷 점 복원. **FR-07** line 라벨 상한=절대 픽셀(파생 W/H 부작용 차단).
  - **시뮬레이션**: 승인 후 4도메인 40+시나리오 사이드이펙트 시뮬(§5-C) → PRD v2.0 보강(지오앵커·퇴화가드·Position정합·P0 undo 재기술). 정점편집(🅑)=후속.
  - **검증**: GMaps.Ui 빌드 0 · 단위 `LineScaleTests` 8/8 + `LineGeometryUndoTests` 4/4 + 회귀 `UndoRedoTests` 34/34. ⚠어도너 좌표 정합·핸들 표시(V-01/04/06/07)는 앱 재빌드 후 런타임 검증.
- **통합웹 접속 트리거 메시지 `CallWebApiProcessMessageModel`** ([PRD](docs/prds/LeftMenu_IntegratedWeb_Button-prd.md) · [Plan](docs/plans/LeftMenu_IntegratedWeb_Button-prd-plan.md) · ViewModel.Models/CommonMessages.cs) — LeftMenu "통합웹" 버튼의 확인 팝업 '확인' 시 발행되어, 크롬 앱 모드로 통합 웹 대시보드(`http://{웹서버IP}:{포트}`)를 여는 트리거(`IMessageModel` 마커 타입). 순수 추가(기존 타입 무변경). 소비측=메인 Monitoring `LeftMenuSectionViewModel`(권한 게이팅→웹설정 `IsWebServerEnabled` 게이팅으로 교체, DATABASE 메뉴→통합웹). ⚠앱 재빌드 후 반영.
- **맵 심볼 제어 단축키 — Delete(확인삭제) / Ctrl+C(복사) / Ctrl+V(붙여넣기)** ([PRD](docs/prds/MapSymbol_Shortcut_CopyPasteDelete-prd.md) · [Plan](docs/plans/MapSymbol_Shortcut_CopyPasteDelete-prd-plan.md) · 태그 `before-mapsymbol-shortcuts` · worktree `feature/mapsymbol-shortcuts` · GMaps.Ui)
  - **Delete(FR-05)**: 선택 심볼/이미지 삭제(단일·그룹)를 단일 진입점 `ExecuteDeleteSelected`(EventAggregator 표준 확인팝업)로 통일. **P0 갭 수정** — 단일 Delete 키가 원래 동작하지 않던 문제(어도너 `RequestMarkerDeletion`이 no-op 스텁인데 `e.Handled=true`로 키를 삼킴)를 `OnMapPreviewKeyDownForGroup` 단일 분기 추가 + 어도너 Delete case 제거로 해소(이중처리 차단).
  - **Ctrl+C/Ctrl+V(FR-02/03)**: 인메모리 클립보드 — 단일/멀티 선택 복사, 붙여넣기는 **마우스 커서 위치**(멀티는 앵커 기준 상대 간격 유지). 배치 Undo(`BeginBatch` 1매크로) + 트리 1회 리빌드 + 결과 자동선택. 반복 붙여넣기 가능. 오버레이 이미지는 v1 제외(삭제는 포함). 커서 추적=`GMapCustomControl.GetLastCursorLatLng`(맵 밖 폴백=뷰 중앙).
  - **복제 코어 통합 + P0 버그 수정(FR-01)**: `DuplicateSelectedMarker`의 300줄 타입별 switch를 `CreateSymbolCopyAsync`(스냅샷 딥클론 재사용) 코어로 추출 — Duplicate(오프셋)/Paste(커서) 공유. **PIDS 복제 버그 2건 수정**: ①`duplicatedSymbol = pidsSymbol`(Fetch Id 유실→Undo 누락) 제거, DB 발급 Id 사용 ②`LinkedDeviceId + 1000`(실장비 오참조) 제거 → PIDS·PidsGroup 붙여넣기는 미링크(0). 붙여넣기 제목은 `_Copy` 미부가(복제 버튼은 유지).
  - **검증**: GMaps.Ui 빌드 0오류. 신규 단위테스트 `SymbolCopyTransformTests` 7/7 통과(미링크·Id리셋·재배치·LinePoints 평행이동·제목정책) + 회귀 `UndoRedoTests` 34/34 통과. ⚠전체 복사/붙여넣기/삭제 플로우 및 키 후킹은 앱 재빌드 후 런타임 검증(V-01~V-06).

### Removed
- **`group_device`(deprecated 단일 그룹) 죽은 코드 정리** (태그 `before-remove-group-device` · Devices.Api/Ui) — `device_groups`(N:N EventMapping) 전환으로 deprecated된 `group_device`의 잔재 제거. `IDeviceApiService`/`DeviceApiService` 6개 조회 메서드의 **미사용 쿼리 필터 파라미터** `int? groupDevice` + XML doc + `MockDeviceApiService` 시그니처 정합. **DTO/Model엔 이미 프로퍼티 없음**(전환 완료·제거 대상 없음). 호출자 30곳 전부 named-arg/무인자라 무영향(빌드 검증: Devices.Api/Ui 0오류). 메인 솔루션 참조 0. (Messages/Tests 하위호환 JSON 픽스처는 유지 — 구서버 group_device 수신 시 무시됨을 검증.)

### Fixed
- **GIS NATS Stage 0 — PTZ_STATUS 수신 복구 + ACTION_REPORT device_groups/geolocation + DETECT frame 필드** ([PRD](docs/prds/GIS_Nats_Full_Integration-prd.md) · [검증](docs/analyses/GIS_Nats_Simulation_Verification.md) · 태그 `before-gis-nats-stage0` · Events.Ui/Messages)
  - **배경**: GIS.md v1.5(REST v4.6) 스펙 대비 26개 NATS 메시지 **225 시나리오 전수 시뮬 검증**(SIM 발행/이벤트/상태/SYNC/REQ-RSP) → 활성 21중 🔴11 결함. Stage 0=긴급·라이브러리 한정 4건(통합 PRD 6단계 중 1단계).
  - **FR-01 PTZ_STATUS subject 복구(🔴 실운용 전면 미수신)**: `CameraPtzNatsSyncService`가 구 subject `nvr_manager.ptz-status`만 필터 → 스펙 v1.5 subject `gis.ptz-status`(§3.6)로 오는 메시지를 **전량 드롭**했음(형제 `TrackingStatusNatsSyncService`가 `gis.tracking-status`로 정상 동작 → 브로커가 `gis.*` 전달함이 지상 증명). **두 subject 병행 수용**(`IsPtzStatusSubject` 추출)으로 서버 버전 무관 무회귀 복구.
  - **FR-02 ACTION_REPORT/DETECT device_groups(라우팅 키)**: `ConvertDeviceToDto`가 device_groups 미채움 → 수신자(NVR/방송/경광등/VMS) N:N EventMapping **라우팅 키 결손**. 모델 그룹 id(List<int>)로 `device_groups`(DeviceGroupDto) 채움. (name/description/device_count 이름 보강은 Stage 1 DeviceGroupProvider seam으로 분리 — 라우팅은 id로 즉시 동작.)
  - **FR-03 DETECT frame_width/frame_height**: `DetectionDetailDto`에 AI bbox 좌표 스케일 해석용 프레임 해상도 필드(optional) 추가.
  - **FR-04 geolocation 전필드**: `ConvertDeviceToDto`가 위경도만 채우던 것을 location/altitude/heading 포함 전필드로(`BuildGeolocationDto`).
  - **검증**: Messages 빌드 0오류·`DetectionDetailDtoTests` 4/4 · Events.Ui 빌드 0오류·`DtoToModelHelperTests`+`CameraPtzSubjectFilterTests` 15/15 통과(신규: device_groups+geolocation 왕복, PTZ subject Theory 5케이스, frame 왕복). ⚠앱 재빌드 후 런타임 반영. **D-4**: 서버가 실제 `gis.ptz-status`로 발행하는지는 배포 전 실 NATS 확인 권장(병행 수용으로 무회귀 보장).
- **이벤트 카드 "구역" 표시 — 그룹 Id 숫자 → 구역 이름(N:N)** (태그 `before-event-card-zone-name` · Events.Ui)
  - **결함**: 탐지/장애 이벤트 카드의 "구역" 칸이 구역 **이름** 대신 값이 이상하게 표시됨 — 장애 카드는 그룹 **DB Id 숫자**(`Device.DeviceGroupsText` = `string.Join(", ", List<int>)` 모델 구현)를 "1, 116"처럼 노출, 탐지 카드는 존재하지 않는 `Device.Name` 바인딩으로 **빈칸**. 근본원인=`DtoToModelHelper`가 서버 DTO의 그룹 `name`을 버리고 `id`만 매핑(`Select(g => g.Id)`) + 카드가 이름 변환 없는 모델 속성에 직접 바인딩.
  - **수정**: `EventCardViewModel<T>`에 이름 변환 `DeviceGroupsText` 속성 추가(`DeviceGroupProvider`로 Id→`DeviceGroupModel.Name` 조회, 미발견 시 Id fallback — 장비 관리 패널 `BaseDeviceViewModel` 패턴 재사용). 두 카드 뷰 바인딩을 VM `DeviceGroupsText`로 통일. N:N 다중 그룹="구역 1, 10", 단일="구역 1".
  - **검증**: Events.Ui 빌드 0오류. 카드 회귀 테스트 2종(`Detection/MalfunctionEventCardView_ZoneBindsToViewModelDeviceGroupsText`) 통과. ⚠앱 재빌드 후 반영.
- **이벤트 카드 제어기 필드 테스트 정정 — 낡은 `ControllerId` 기대값** (Events.Ui/Tests · 커밋 HEAD부터 red였던 사전 실패)
  - **원인**: Phase19 테스트(`Detection/MalfunctionEventCardView_ControllerId_BindsToViewModelProperty`)가 카드 XAML에 `"ControllerId"` 문자열을 기대했으나, 카드 설계가 진화 — **탐지 카드=`ControllerDeviceNumber`(제어기 번호)**, **장애 카드=`ControllerDisplay`/`SensorDisplay`(장애타입 인지 표시)**. 바인딩은 전부 유효한 VM 속성(깨진 `Device.Controller.*` 경로 없음) → **XAML 정상, 테스트만 낡음**.
  - **수정**: 두 테스트를 `..._ControllerField_BindsToViewModelProperty`로 정정 — 탐지=`ControllerDeviceNumber` 검증, 장애=`ControllerDisplay`+`SensorDisplay` 검증(+`Device.Controller.*` 부재 유지). 카드 관련 8/8 green.
  - **힌트 문구 정정**: 제어기/센서 필드가 실제로 **번호**(DeviceNumber)를 표시하는데 힌트가 "아이디"였음 → 탐지·장애 4곳 `"…아이디"`→`"제어기 번호"/"센서 번호"`로 실측 정합. 빌드 0오류.
  - **데이터 경로 확인(버그 없음)**: 이벤트 로딩(`EventProviderService`)이 `ToXxxEventModel(_deviceProvider)` 오버로드 사용 → provider의 실제 device 반환, `DeviceProviderService`가 센서를 `includeController:true`로 로드(단건/대량 모두) → 제어기 정상 채워짐. **갭 정리(완료)**: Insert/Update 반환 매핑 6곳(`EventProviderService`)을 `null`→`_deviceProvider` 오버로드로 통일 → 생성/수정 반환 모델도 provider 조회로 Controller 포함. Insert/Update 테스트 6/6 green.
- **ACTION_REPORT NATS 발행 계약 복구 — from_event/device 누락 + from 오류** ([PRD](docs/prds/Action_Report_Nats_FullDto_Contract-prd.md) · [Plan](docs/plans/Action_Report_Nats_FullDto_Contract-prd-plan.md) · 태그 `before-action-report-fulldto` · Monitoring.Models/Events.Ui + 메인 솔루션 NatsDomainService)
  - **결함**: 조치보고 NATS `ACTION_REPORT` body가 `{content,user}`만 → `id`·`type_event`·`from_event`(이벤트/장비 식별자) 통째 누락 + `from`=SystemUuid(`"gis-monitoring"`). 수신자(NVR 카메라홈복귀/방송종료/경광등해제/VMS)가 대상 장비 식별 불가 → 복귀동작 마비. 원인=FR-01 "transport adapter" 리팩터가 Full DTO 채우던 로직 제거(설계 §2.4 Pattern1·§6.4 위반).
  - **수정**: `SendActionRequestMessage`에 `OriginEvent`(IExEventModel)+`ActionId` 추가 → 발행 5지점(수동 탐지/장애·배치·자동조치·자동복구)에서 채움 → `NatsDomainService`가 `ActionEventModel.ToActionEventDto()`로 from_event(device.id 포함) 구성 + `from="GIS"`. OriginEvent null 시 기존 최소 body fallback(하위호환).
  - **§6.4 device 필드 확장**: `ConvertDeviceToDto`에 `status`·`version`·`geolocation`(위·경도)·`controller_id`(`BaseDeviceDto` 신규 필드) 매핑 추가. 헤드리스 테스트가 `from="GIS"`+`device.id/status/version/controller_id` 검증.
  - ⚠ **잔여 명세 차이(공용 DTO 구조 결정 대기)**: `device_groups`는 우리 `DeviceGroupDto`가 `name`(≠명세 `name_group`)+추가필드라 미포함(수신자 EventMapping 라우팅 키) · `group_device`는 의미/출처 미확정 · geolocation은 위·경도만(고도/설명 없음). ⚠ 메인 솔루션 재빌드(앱 종료 후) 필요.

### Added
- **보고서(Report) 기능 — 표준/템플릿 생성·목록·템플릿 CRUD·미리보기(WebView2)·PDF** (Messages·Reports.Api·Reports.Ui 신규 + 외부 Monitoring 배선 · 태그 `before-report-feature` · `c896a63`/`f58add4`)
  - **신규 라이브러리**: `Reports.Api`(IReportApiService 14메서드 — 카탈로그/상태/템플릿 CRUD/생성/이력/삭제/미리보기HTML/PDF, EventApiModule 패턴 Bearer 파이프라인) + `Reports.Ui`(콘솔 셸 + 목록/생성/템플릿/편집/미리보기 VM·View, LiveCharts). DTO=Messages/Dto/Reports/*.
  - **생성**: '표준 전체(STANDARD, 전 섹션)' 또는 '템플릿 기반(CUSTOM — 저장 템플릿 드롭다운 선택)' + 제목/기간 → 202 요청 → 1.5s 폴링(원형 프로그레스 + 상태 뱃지). (혼동되던 정형/비정형 용어 폐기.)
  - **CRUD**: 목록(상태 뱃지·검색·삭제·PDF다운로드·미리보기), 템플릿(추가 POST/수정 PATCH/삭제, 편집 다이얼로그=이름·설명·공개·기간·컴포넌트, 전체조회로 구성 로드), 생성이력 삭제.
  - **미리보기 = WebView2 embed**: 서버 자립형 HTML(`/api/reports/preview/{id}`, 인라인 Chart.js/CSS)을 콘솔 오버레이 WebView2에 NavigateToString → 실제 차트 **오프라인** 렌더 + 확대/축소(ZoomFactor)·스크롤. (Playwright=Chromium 동일 엔진 렌더 실증.)
  - **외부 Monitoring 배선**: LeftMenu **REPORTS** 버튼(reports:view 게이팅)→ConductorControl `IHandle<OpenReportPanelMessageModel>`→ReportConsoleViewModel · Bootstrapper `ReportUiModule` 등록 + `Microsoft.Web.WebView2` 패키지(네이티브 로더 복사).
  - **함정 기록**: ①WebView2=HwndHost 고유크기0→오버레이 확정높이 필요 · ②미리보기 단일 네비게이트(로딩HTML 레이스 제거)+15s 타임아웃 · ③DataGrid RowHeight 토큰=30 한글짤림→명시 38 · ④저장 후 목록새로고침=선택해제→id 재선택 · ⑤ReportUiModule ApiSetupModel 하드캐스트→복사생성자. **서버 verb-RBAC 집행 확인**(무권한 403·admin bypass·무토큰 401, 무인증 preview PII 봉합). ⚠앱 재빌드 후 E2E(생성→미리보기 차트→템플릿 CRUD→삭제) 필요.
- **권한 그룹 관리 — 그룹 CRUD·계정 배정 + 권한부여 계정 미표시 수정** ([PRD](docs/prds/GOP_Permission_Group_Management-prd.md) · 태그 `before-permission-group-management` · Accounts.Api/Ui·Messages · `59632ec`/`d1edcda`/`bda89b0`/`4502a61`)
  - **① 권한부여 계정 미표시(버그 수정)**: `GrantManagementPanel`이 `GetUsers(limit=200)` 요청 → 서버 `users` limit 상한(`le=100`) 초과 **422** → 계정 콤보 공백(에러가 로그로만 삼켜짐). `200→100` + 로드 실패 시 팝업 안내(무증상 재발 방지).
  - **② 권한 그룹 CRUD/계정 할당(OQ-PG-01 Option A→B 확장)**: 임의 권한그룹 **생성·이름/설명 수정·삭제** + 권한 **매트릭스 편집**(기존) + **계정→그룹 상시 배정**(user.group_id 추가/해제). 예약 5등급(ADMIN/MAINTAINER/OPERATOR/VIEWER/GUEST)은 삭제·개명 금지(매트릭스 편집만). 전 기능 ADMIN(콘솔 탭 + 서버 require_admin 최종방어).
  - **구현**: Messages `UserGroupCreateDto`/`UserGroupUpdateDto`/`UserGroupAssignDto` · Accounts.Api 그룹 CRUD 5메서드(Create/Update/Delete UserGroup·GetUserGroupUsers·AssignUserGroup) · Accounts.Ui `PermissionMatrixPanel` 3화면(목록+CRUD툴바/매트릭스/구성원 배정). **서버 무변경**(user-groups CRUD 완비). 빌드0 · Accounts.Api **93/93**(신규 10). ⚠**구성원 해제 서버 제약**: `update_user`가 `group_id:null`을 무시(`users.py:535` `if group_id is not None`) → 현재 no-op. 클라 정직성 가드(`bda89b0`, 미반영 시 안내)로 무증상 실패 방지, **서버 수정 대기**(PRD V-03, 서버세션 이관). 배정(add)·그룹 CRUD·매트릭스는 정상. ⚠앱 재빌드 후 E2E(그룹 생성→계정 배정→매트릭스 저장→삭제) 필요.
  - **v5.4 Role Simplification 정합(`4502a61`)**: 서버가 등급 role 5→2(ADMIN/USER) 축소 + ADMIN/GUEST 등급그룹 DROP + 나머지 `Preset - X` rename(편집 허용). 패널의 하드코딩 예약 5등급 로직 제거 — 전 그룹(팀/Preset) 편집·삭제 허용, `Preset` 접두 인식(팀→Preset 정렬). (원장 L151 A→B 요청 대응.)
- **심볼/이미지 잠금 + 심볼 이름변경 싱크** ([PRD](docs/prds/Symbol_Lock_And_RenameSync-prd.md) · 태그 `before-symbol-lock-rename` · Monitoring.Models/GMaps.Db/GMaps.Ui · `6156d10`)
  - **잠금(FR-01~03, DB 영속)**: 레이어 패널의 각 심볼·이미지 leaf 앞 **자물쇠 토글 아이콘**(열림 `LockOpenVariantOutline`/잠김 빨강 `Lock`) + **우클릭 잠금/잠금해제 메뉴**(상태별 헤더·아이콘 플립). 잠긴 심볼은 맵에서 **클릭 불가**(`GetMarkerAtScreen`에서 제외 → 좌·우클릭 + `OnMapMarkerClicked` 가드로 편집모드 ON 포함 차단). `LayerTreeNode.IsLocked` 단일소스 → VM이 마커 `IsLocked` 적용 + DB 영속. 패널·메뉴·맵·DB 4곳 싱크.
  - **모델·DB**: `ISymbolModel`/`IImageModel`(+구현·복사생성자)에 `IsLocked`. `Symbols`·`Images` 테이블 `IsLocked` 컬럼(CREATE inline + 멱등 `ALTER ADD COLUMN` 마이그레이션, 기존 행 `DEFAULT FALSE`) + 7종 심볼·이미지 INSERT/UPDATE/SELECT·DTO 매핑. 재시작 후 잠금 유지.
  - **이름변경 싱크(FR-04)**: 심볼 leaf rename 활성 + 컨텍스트 메뉴 '이름 바꾸기' + 인라인 편집. `symbol.Title` → `UpdateSymbolAsync`(공통 Symbols 행, 타입무관) 영속 + 마커/속성창 싱크(Overlay Image 패턴). 맵편집 권한(`CanEditMap`) 게이트 — 잠금 변경도 동일 게이트.
  - **초기화**: 심볼 leaf=`CreateSymbolLeaf(symbol.IsLocked)`, 이미지 leaf=`InitIsLocked`(마커 기준, `LockChanged` 미발화로 DB 재기록·피드백 루프 방지).
  - GMaps.Ui 빌드0 · LayerTree 단위 **33/33**(잠금/이름 4종 신규) · code-reviewer(opus) H3/M2/L2 반영(복사생성자 누락·이미지 초기화 갭·권한 게이트·이미지 CREATE 컬럼·rename UI 어포던스). ⚠앱 재빌드 후 E2E(잠금→클릭차단→**재시작 유지**, rename→**재시작 유지**) 필요.
- **레이어 패널 재설계 v1 — 카테고리별 개별 Overlay 심볼 트리노드 + 드래그 리사이즈** ([PRD](docs/prds/LayerPanel_SymbolNesting_Resize-prd.md) · [스토리보드](Docs/reports/LayerPanel_SymbolNesting_Resize_Storyboard_Wireframe.html) · 태그 `before-layerpanel-symbol-nesting` · GMaps.Ui)
  - **개별 심볼 노드화(FR-01~04)**: SYMBOLS 섹션의 카테고리(카메라/센서/군사 등)를 펼침 노드로 승격하고 그 아래에 `_symbolProvider`의 개별 심볼을 자식 노드로 노출(비균일 4단계, PIDS=Section›Group›Category›Symbol). 개별 심볼 체크박스로 마커 가시성 토글(`ShowShape`/`IsLayerEnabled`, 런타임), 우클릭 '중앙으로 이동'으로 맵 팬. tri-state 카테고리→그룹→섹션 4단 전파.
  - **모델 이음새 해소**: 개별 심볼 리프는 `IMapLayerModel`이 아닌 신규 `ISymbolModel? Symbol` 페이로드 + 신규 `NodeType.Category` + 심볼 전용 이벤트(`SymbolVisibilityChanged`/`SymbolNavigateRequested`). 기존 `CanDelete = Model?.LayerType != "Symbol"`가 Model=null에서 TRUE로 역전되던 잠재버그를 leaf-kind 게이팅으로 차단. 카테고리 일괄 cascade로 tri-state O(n²) 재계산 제거.
  - **카테고리 조인**: 비PIDS는 `EnumMarkerCategory` 직매핑(**VEHICLES 보강**), PIDS는 `DeviceType`로 6 하위카테고리 분기, 미매핑은 '기타' 폴백, Title 공백/중복은 `{카테고리} #{Id}` 폴백.
  - **드래그 리사이즈(FR-05/06)**: E/S/SE Thumb 그립, 250×420→375×630(각 +50%), 좌상단 앵커 고정, Canvas 경계 2차 클램프, 높이 초기 Auto+MaxHeight 캡(off-canvas 차단). 크기는 세션 내 기억(세션 간 영속=v2).
  - GMaps.Ui 빌드0 · 단위테스트 **148/148**(신규 19: DeviceType 분기·심볼 중첩·폴백·cascade·레거시 호환) · 설계검증 wf_d15d5365(4 렌즈+2 적대비평) 반영. **v2 보류**: 개별 심볼 삭제/이름변경·전면 가상화·검색바·세션간 크기 영속. ⚠앱 재빌드 후 E2E 런타임 검증 필요.

### Fixed
- **3rd Party(Gateway) 이벤트 그룹 쓰레기값("1, 116") 근본수정 — 레거시 `Group` 컬럼 부활 차단** ([PRD](docs/prds/GatewayEvent_Group_Resurrection_Fix-prd.md) · [Plan](docs/plans/GatewayEvent_Group_Resurrection_Fix-prd-plan.md) · Gateway lib · `0275776`)
  - 증상: 설정 > 3rd Party 이벤트 설정의 "그룹" 컬럼에 의도치 않은 그룹 ID가 섞여 표시(예: Event_A가 `116`이어야 하는데 `1, 116`). 원인은 **DataGrid가 아니라 DB 프로바이더 측**.
  - 근본 원인: `GatewayDbService.BuildSchemeAsync`의 레거시 이행 쿼리(`INSERT IGNORE SELECT Id,Group WHERE Group>0`)가 **매 앱 시작마다** 실행되어, N:N 전환 후에도 남은 단일 `GatewayEvents.Group` 값을 연결 테이블로 **부활(resurrection)**시킴. `UpdateGatewayEventAsync`가 레거시 컬럼을 비우지 않아 영구 반복. (선행 `GatewayEvent_Group_NtoN_Migration` PRD가 "다음 릴리스"로 연기한 컬럼 DROP의 부작용.)
  - 수정: 상시 이행 쿼리 제거 + `information_schema`로 레거시 컬럼 존재 시에만 **1회 실행되는 자기비활성화 마무리 블록**(`FinalizeLegacyGroupColumnAsync`) — ①최종 안전 이행 → ②좀비 정리(그룹 2개 이상 이벤트에서 `GroupId=레거시 Group` 행 삭제, count>1 가드로 유일값 보존) → ③`Group` 컬럼·`IX_Group` DROP. 신규 설치 DDL에서도 레거시 컬럼 제거. 외부 솔루션 무변경(`NatsDomainService` 이미 Intersect).
  - 검증: 통합 5종(좀비 제거·Group=0 무영향·유일값 보존·컬럼 DROP·이중실행 멱등) + 실 `monitor_DB` 덤프 사본 E2E(실 코드 경로 Event_A→`116`/Event_B→`117`·컬럼 제거·멱등) 통과. MariaDB 12.2 `DROP COLUMN`→`IX_Group` 동반삭제 실측. 빌드0.

### Added
- **강제 로그아웃 전파 — GIS(클라) Phase 1** ([PRD](docs/prds/GOP_Force_Logout_Propagation-prd.md) · [보고서](docs/reports/GOP_Force_Logout_Client_Phase1-report.md) · Accounts.Api/GMaps.Ui · `b15359b`)
  - 단일 멱등 진입점 `ISessionLifecycle.ForceLogoutOnce`(Interlocked once-guard — NATS/401/수동 수렴) + TokenStorage jti·세대가드(refresh 부활 차단, FR-FL-05) + `BearerAuthHandler` 401폴백 배선 + GMaps `ForceLogoutRequested` 구독→PTZ정지·팝업(스트림)해제(FR-FL-07).
  - 검증: 단위 신규6→Accounts.Api 73/73, GMaps.Ui 통합빌드0, **E2E(라이브) 강제로그아웃 후 access·refresh 401 무효화 확인**.
  - 후속(서버계약/메인솔루션): NATS 즉시푸시(FR-FL-02)·유휴 하트비트(06)·셸 가림막/로그인 전환(08)·서명(10)·session_id 정밀매칭.

### Fixed
- **PTZ 응답성(큐잉 제거) + 팬틸트/줌 속도 반영** ([PRD](docs/prds/CameraPopup_PTZ_Responsiveness_Speed-prd.md) · GMaps.Ui · 머지 `4ebb93a`)
  - **큐잉 지연 해소(A)**: 방향 패드·줌 버튼 핸들러가 `BeginPtzGesture` 취소토큰을 `ContinuousMoveAsync`에 미전달해 Gate 큐가 무한 누적되던 버그 수정 — 이제 새 제스처가 직전 대기명령을 LWW 취소(드래그/휠과 동일). `BeginPtzGesture`를 `ConcurrentDictionary.AddOrUpdate` 원자 교체로(경합 수정).
  - **속도 미반영("날아감") 해소(B)**: `ContinuousPanTilt/ZoomVelocitySpace`의 범위(XRange/YRange)를 캡처하지 않아 raw `[-1,1]×speed`를 보내 카메라 범위와 안 맞으면 속도 슬라이더가 무효화되던 버그 수정 — `PtzVelocityMath.ScaleToRange`로 정규화 속도를 카메라 연속속도 범위로 스케일(0=정지 보존, 부호별 풀스케일). 드래그·패드·줌버튼·휠 **전 경로**가 단일지점 통과 → PanTilt/Zoom 속도 실반영. `[-1,1]` 표준 카메라는 항등(회귀0).
  - Stop-before-fire는 미적용(ONVIF §5.3.2 ContinuousMove 자동대체, 추가 시 ~100ms 역효과). 진단로그에 `norm→scaled`+카메라 범위 노출(필드 진단). 단방향/비대칭 범위 1회 경고.
  - 설계검증 wf_7ad8437b(architect+code-reviewer) + opus 리뷰 MERGE. 빌드0·단위테스트 168(PtzVelocityMath 24 신규). 롤백 `before-ptz-latency-speed`.
  - ✅ **E2E 통과**(라이브 카메라, 사용자 승인): 범위 [-1,1] 표준 / v=0.2 vs 0.8 → ~6.7배 속도차 실증 → 카메라가 velocity magnitude 정상 존중 확인. 즉 증상의 진짜 원인은 큐잉(A)였고 LWW가 해소(B 스케일은 [-1,1] 카메라엔 항등·비표준엔 방어). 앱 런타임 체감은 사용자 최종 확인.

### Added
- **카메라 팝업 줌·포커스 Press-Hold 제어** ([PRD](docs/prds/CameraPopup_PressHold_PtzZoomFocus-prd.md) · [Plan](docs/plans/CameraPopup_PressHold_PtzZoomFocus-prd-plan.md) · GMaps.Ui · 머지 `0fbb261`)
  - 줌 ±·포커스 ± 버튼을 클릭=펄스 → **누르면 연속(ContinuousMove/ContinuousFocus)·떼면 정지** press-hold로 전환(방향 패드 패턴 통일). 통합 Tag 메커니즘(`PtzGestureTag`) + CaptureMouse 릴리즈 보장 안전망 + `_activeGesture` 타입별 정지 라우팅.
  - 🔴 포커스 정지는 ImagingClient 별도 경로(신규 `StartFocusAsync`/`StopFocusAsync`) — PTZ `StopAsync`(StopPTZ)로는 포커스 모터가 안 멈춤. 정상릴리즈/캡처유실(Alt+Tab)/팝업닫기/제스처전환 **4경로 모두** 올바른 모터로 라우팅(code-review 치명결함 해소).
  - **FR-PH-10**: 연속 포커스 속도를 카메라 `GetMoveOptions` ContinuousFocus 범위로 클램프(`PtzFocusMath`) — 0.7이 범위밖이라 무동작하던 F항목 해소. 포커스 게이팅을 외곽 `IsPtzCapable` 밖으로 분리(`IsImagingCapable` 독립) — 영상전용 고정카메라 수동포커스 도달가능(E항목 해소).
  - v2.6 PTZ 권한 게이팅(cam:control) 통합: ZoomHold/FocusHold=`CanControlCamera` 게이팅 · FocusStop=무게이팅(정지 항상 허용). 휠 줌 펄스 유지, 펄스 잔재(`MoveFocusAsync`·OnCameraPopupFocus·줌/포커스 Command) 제거.
  - 설계검증 wf_da23975b(architect SOUND_WITH_CHANGES + code-reviewer MERGE_WITH_FIXES). 빌드0·테스트 **144**(PtzGestureTag/PtzFocusMath 신규). 롤백태그 `before-presshold-ptz-zoomfocus`.
- **권한 실제집행 — 클라 게이팅 (GOP_Permission_Enforcement, 서버독립분 완료)** (라이브러리 GMaps.Ui/Devices.Ui/Events.Ui)
  - **FR-EN-06 PTZ**(`663c45e`): MapViewModel PTZ 제어 핸들러 10곳 `CanControl("cameras")` — 서버 ONVIF 미중계라 클라 단독 권위집행, 안전정지(Stop) 제외, EnsurePtzReady IsPtzCapable 이중방어.
  - **FR-EN-09 장비**(`a4e63f1`): 7패널 CRUD `CanEdit/CanDelete("devices")` + `DevicePermissionGate` 공유 헬퍼.
  - **FR-EN-10 이벤트**(`3282de8`): ACK=`CanControl`·CRUD=`CanEdit/CanDelete`("events") **독립 게이팅**(FR-PG-08), 배치 가드(_batchReportGate 이전), 자동경로 제외.
  - **FR-EN-11**(장비/이벤트): `PermissionsChanged` 구독 → `Execute.OnUIThread` 버튼/커맨드 재평가(역할강등 즉시 반영).
  - 주입=GMaps/Devices/Events.Ui→Accounts.Api ProjectReference + `IoC.Get<IPermissionService>()` lazy(미등록 전체허용 폴백, V-EN-11). 빌드0·GMaps.Ui 통합빌드0·Accounts.Api 62/62.
  - **FR-EN-05 모듈(`b1037f5`)·FR-EN-07 방송·FR-EN-08 맵편집(`ff4c0d7`) 완료**: 서버 enum이 map/broadcast 수용 확인 + §6-1 등급별 값 API 시드 후 게이팅. 방송 발행 2곳 + 맵 심볼/오버레이/ROI/레이어 13곳, FR-PG-11(로컬렌더 허용·DB영속만 게이트). GMaps PTZ 강등 재평가(`f353f28`)·사용자수 실수정(`c693ddb`)도 포함. → **클라 집행 PRD(FR-EN-05~11) 전체 완료.** ⏸ 남음=AUTH_MODE=token 전환 시 클라 Bearer 배선(FR-EN-03③)+GOP-07 가림막.
- **카메라 PTZ "특정 위치 확인" → NATS 좌표 발행** ([PRD](docs/prds/Camera_PTZ_AimLocation_Nats-prd.md) · [Plan](docs/plans/Camera_PTZ_AimLocation_Nats-prd-plan.md) · GMaps.Ui)
  - 맵에서 **PTZ 카메라 심볼 우클릭 → "특정 위치 확인" → 커서가 조준(Cross)으로 바뀌고 카메라 중심 반경 30m 원 표시 → 영역 안 클릭 → 해당 좌표를 NATS PUB로 발행**(카메라 회전 요청 + 좌표). 클라는 직접 회전하지 않고 좌표만 전달하며, 실제 PTZ 회전(지리 방위→pan/tilt)은 서버/NVRManager가 수행. 영역 밖 클릭·ESC·우클릭 = 취소.
  - PTZ 전용 노출: `ICameraDeviceModel.Category == EnumCameraType.PTZ`인 카메라에서만 메뉴 항목 표시(동기 판정, DB 스키마 변경 없음).
  - 신규 NATS 발행 서비스 `ICameraAimControlService`/`CameraAimControlService`(`BroadcastControlService` 패턴 + 경계검증·try/catch·`ConfigureAwait(false)`·`CancellationToken` 보강) — subject `{Domain}.{Group}.nvr_manager.camera-aim`, cmd `CAMERA_AIM_LOCATION`(신규 `EnumGopCommand`=14), body `CameraAimLocationBodyDto`(camera_id·타겟/카메라 lat·lng·distance_m·bearing_deg·requested_by).
  - 반경 판정은 지오 도메인(`CameraAimMath.IsWithinRadius`=`HaversineMeters ≤ R`, 줌 무관)으로 분리하고 화면 원은 `GMapCustomControl.OnRender`에서 지오 앵커로 그려 팬/줌/디지털줌에 자동 추종. 좌클릭 가로채기는 `OnMouseLeftButtonDown`의 라인드로잉 분기와 동급 위치(`base` 전 `e.Handled`)로 팬·마커선택·이미지편집·더블클릭 차단 + 모드 상호배제.
  - 반경 설정값 `ITrackingSetupModel.CameraAimRadiusMeters`(기본 30m, appsettings `Tracking` 섹션). 순수 로직(`CameraAimMath`/`CameraAimRequestBuilder`) xUnit 15종 신규. GMaps.Ui 빌드0·테스트 124/124. 분석=Explore×4+architect+code-reviewer 체인.
- **권한 모델 일원화: 역할(등급) = 권한 단위 (PRD-GOP-01 OQ-PG-01 = Option A)** (라이브러리 + 서버 `api-test-server`)
  - 기존엔 "구분(역할)"과 "권한 그룹(매트릭스)"이 따로 떠 있고(레벨/역할/그룹 3중), 실제 집행은 역할 하나뿐 + 그룹 매트릭스는 사용자 배정 경로조차 없는 고아 설정이었음. → **역할(등급)을 단일 권한 단위로 통합.**
  - **서버**: `initialize_database`에 `ensure_role_permission_groups` 추가 — 5개 역할명 등급그룹(ADMIN/MAINTAINER/OPERATOR/VIEWER/GUEST)을 idempotent 보장(PRD §6-1 기반 기본 8×4 매트릭스, 기존 팀그룹 비파괴). 로그인 권한 유도를 `group_id` → **`user.role` 명 등급그룹 매트릭스**로 변경(`auth.py`) — 역할이 권한을 결정. 도커 재빌드+재기동, 라이브 검증(admin이 빈 권한 대신 FULL 매트릭스 수신).
  - **클라**: `PermissionService` **ADMIN 무조건 통과**(서버가 ADMIN 권한 비워 보내 발생하던 잠복 차단 버그 수정). 권한 화면을 "권한 그룹"→**"권한 등급"**으로(5등급만 등급순 표시·한글 라벨, 임의 그룹 추가/삭제 제거). 계정의 "구분"이 곧 권한 등급. 빌드0·Accounts.Api 62/62.
- **권한 그룹 편집저장 (PRD-GOP-01 IMPL-06) — 서버 권한수정 API 신설 + 클라 매트릭스 편집** (라이브러리 + 서버 `api-test-server`)
  - **서버**: 신규 `POST /api/user-groups/{id}/permissions`(ADMIN 전용 `require_admin`). 일반 PUT이 권한상승 방지로 `permissions`를 차단(v4.8 Phase 12-7a)하던 것을 ADMIN 전용 경로로 재개. `PermissionsSchema` strict 검증(미정의 모듈/verb→422) + `PERMISSION_CHANGED` 감사(append-only). pytest 3종(200/404/422)·라이브 E2E(admin 저장·422·404·audit) 통과. swagger=route docstring(원천), 도커 이미지 재빌드+컨테이너 재기동. 안전점 `pre-perm-edit-endpoint`.
  - **클라**: `IAccountApiService.UpdateGroupPermissionsAsync`(기본 인터페이스 구현=테스트 스텁 무영향) + `AccountApiService` POST 호출. `PermissionMatrixPanel` 상세 매트릭스 편집가능화(체크박스 `OneWay→TwoWay`, `ModulePermRowViewModel`→`PropertyChangedBase`) + [저장] 버튼 활성·`OnClickSave`(Modules→`PermissionsDto`, device_groups 보존, 성공 시 재조회·목록복귀). 빌드0·Accounts.Api 62/62. 안전점 `before-perm-edit-save`.

### Fixed
- **카메라 팝업 3종 수정** (Track B · 커밋 `4900093`/`58b3fd7`/`5edbfb1`)
  - **66번 영상 프리즈**: Hub 플레이어(`CameraStreamEntry`)가 오디오 미차단 → 오디오 포함 스트림(`…/video1+audio1`)만 vmem 비디오 콜백 stall로 정지. `media.AddOption(":no-audio")`로 해결(비디오 전용 스트림 무영향). VLC `--no-audio` 캡처로 카메라 스트림 라이브 확인.
  - **팝업이 윈도우 타이틀바 침범**: `CameraStreamPopupViewModel.CanvasTop` 세터에 `MinCanvasTop`(=0) 하한 클램프 — 상단 카메라 팝업이 `ClipToBounds=False` 캔버스에서 위로 넘쳐 MahApps 타이틀바(최대/최소/닫기)를 덮던 문제(MahApps 아닌 앱 오버레이가 원인).
  - **PTZ 속도 슬라이더 부동소수 노출**: `PanTiltSpeed`/`ZoomSpeed`를 `Math.Round(.,1)`로 0.1 단위 반올림 + 슬라이더/표시 0.1 스냅(`0.2999…`→`0.3`).

### Changed
- **API·SVMS HTTP→HTTPS 전환** (라이브러리 `5edbfb1` · 메인 `162547f` · [영향분석](docs/analyses/Http_To_Https_Migration_Impact-analysis.md))
  - 데이터 수신 API 베이스 URL(appsettings `Url`) http→https. 모든 도메인 API가 공유 `ApiService`(단일 HttpClient) 경유. mkcert 사내 root CA 신뢰 등록으로 .NET 기본 검증 통과 — **인증서 코드 변경 0**. 끝단 검증 `https://localhost:8000/`→200·TLS OK.
  - SVMS 장비상세 링크 스킴 https(`DeviceDetailUrlService` `WebScheme` const, 테스트 25/25). ⚠ SVMS 서버도 https listen 필요.

### Added
- **Tracking Playback 데이터소스 토글 (로컬 DB ↔ 서버 API)** ([PRD](docs/prds/Tracking_Playback_DataSource_Toggle-prd.md) · [Plan](docs/plans/Tracking_Playback_DataSource_Toggle-prd-plan.md) · 커밋 `6024a71`/`5a662ad`/`0d0948a`, 브랜치 `feature/track-datasource-toggle`, 태그 `before-track-datasource-toggle`)
  - Playback reader를 설정에서 **로컬 DB / 서버 API** 선택(라이브 토글, 무재시작). `ITrackPointReader` seam만 — 라이브 오버레이·write(인제스트) 경로 무변경. 기본=Local(스테이션 #2까지).
  - 신규 `Tracking.Api`(Events.Api 미러: cursor loop `GET /api/tracking/points`) + `TrackPointApiReader`/`TrackDataSourceSelector`(FetchAsync마다 분기) + `EnumTrackDataSource`(Local/Api; Hybrid v2) + `TrackPointDto`/`TrackApiListResponse`/`TrackCursorDto`(cursor envelope) + `TrackPointDtoMapper`(KST `+09:00`→UTC, AssumeUniversal) + 설정 콤보 UI(`EnumDisplayNameConverter`) + DataSource 영속(MapSettingsHelper — 기존 누락 MaxPlaybackHours/RetentionDays도 보강).
  - 분석 체인(Explore×3→architect→**code-reviewer FLAWED 판정**) 적대검증 수정 전부 반영: `GetRequestAsync` ct無·첫페이지 cursor omit·`AsImplementedInterfaces`(ExecuteAsync 트리거)·`ITrackPointReader` 중복바인딩0·nullable 매퍼·webSetup Url부재→ApiSetupModel 명시. 빌드0·매퍼 7테스트·전체 회귀0(126/126).
  - 🔲 후속: 메인솔루션 Bootstrapper `GMapUiModule(..., trackingApiSetup: GOP ApiSetupModel)` 연동(**사전통지**)·v2.6 머지·앱 재빌드 런타임. **서버측(별도 repo `api-test-server`)** GET /api/tracking/{points·sessions·health} + `gis-ingest` 워커 = 배포·mock E2E 완료(차수 v4.11/v4.12).
- **Tracking GIS 시각화 + 트레일 + TTL + 설정 (P1~P3) — Foundation 착수** ([PRD](docs/prds/Tracking_GIS_Visualization_Playback-prd.md) · [Plan](docs/plans/Tracking_GIS_Visualization_Playback-prd-plan.md))
  - **계약 확정(V-CONTRACT-1)**: `Gop_Message_Broker_연동설계.md §8.3.7`(권위 SoT)로 TRACKING_STATUS 메시지 검증 — `targets[]`/`track_id`/`observed_at`/`threat_level`/`location` 전부 필수. 설계 보강 6건(복합키·lost/idle 제거·Unknown 클라폴백·소문자변환·ttl기본5·car·vehicle) PRD 반영.
  - **P1 Foundation(빌드0·테스트 183/183)**: ① `EnumThreatLevel`(NORMAL/CAUTION/THREAT+Unknown 클라폴백)·`EnumTargetType`·`TrackingEnumExtensions`(안전파싱+ToColorType, 토큰스캔으로 `armed_person`→Person 견고화) 신설(`.Enums`) ② `IClock`/`SystemClock` 신설(`.Base`, 규칙 I-02) ③ **DTO 전면 교체**(`.Messages`): `TrackingStatusBodyDto` 단수 `target`→다중 `targets[]`+`ttl_sec`/`frame_w·h`, 신규 `TrackingTargetDto`. Phase26 단위테스트 재작성(역직렬화/idle/enum폴백).
  - 롤백 태그 `before-tracking-gis`(@62dd557), worktree `v2.13.0`. 계약 인터페이스=Events.Ui 배치(비순환), Enum=`.Enums`·마커=`GMaps.Ui/GMapSymbols`(경로 정정).
- **UI Modern Dark/Light 테마 디자인 시스템 — Phase 1 완료** ([PRD](docs/prds/UI_ModernTheme_DesignSystem-prd.md) · [Plan](docs/plans/UI_ModernTheme_DesignSystem-prd-plan.md))
  - 신규 leaf 어셈블리 `Ironwall.Dotnet.Libraries.Theme`(net8, MD/Colors 5.2.1 + MahApps 2.4.10, GMap/*.Ui 무참조). 토큰 딕셔너리 5종 — `Tokens.Light`(현재 출고 byte-identical, AD-6) / `Tokens.Dark`(Modern Dark) / `Tokens.Shared`(radius·density·font) / `Converters` / `Theme.Current`(스왑 컨테이너).
  - `IThemeService`/`ThemeService`: Add-new→Remove-old 토큰 dict 원자 스왑 + PaletteHelper(MD) + ThemeManager(MahApps) 듀얼 엔진 단일 Dispatcher 패스 + `ThemeChanged`(비-WPF 경로 재색칠) + R-17 중복차단 + 영속화 seam(`IThemeSettingsStore`). 토큰 팩토리/MergedDictionaries 주입형으로 헤드리스 테스트 가능.
  - `ThemeKeyLinter`(RISK-03): 참조-vs-정의 키 검증 + Light≡Dark 파리티 게이트. **빌드 0에러 · 테스트 11/11**(ThemeService 7 + 린터 4). 롤백 태그 `before-modern-theme-migration`, worktree `v2.12.0`.
  - **Phase 2 라이브러리측 진행(IMPL-08·IMPL-09·TEST-12)**: `Accounts.Ui`/`Devices.Ui`/`Events.Ui`/`Sounds.Ui` csproj에 Theme 어셈블리 ProjectReference 배선(AD-1 순환 없음 — leaf 확인, 4개 전부 빌드 0에러). 토글 스모크 `ThemeToggleSmokeTests`(Toggle 라운드트립·ThemeChanged 시퀀스·dict 누수 가드) 3종 추가 → **테스트 14/14**. GMaps.Ui `Generic.xaml` 토큰 병합 설계안 문서화([docs/design/IMPL-09_GMaps_Generic_Merge_Design.md](docs/design/IMPL-09_GMaps_Generic_Merge_Design.md)) — 실제 편집은 Phase 6(동시 PTZ 세션 게이트). **외부 메인앱 배선(EXT-01/02/06)은 사전통지 후 진행 예정.**
  - **Phase 4 — Events.Ui SkiaSharp 차트 theme-aware(IMPL-21, FR-13)**: LiveChartsCore 페인트는 WPF 토큰 미도달 → 신규 `ChartThemeProvider`로 일원화. 축/범례/툴팁 텍스트 = theme-aware(`TextColor`: Light #1C1B1F·Dark #EDF1F6, 기존 흰축라벨↔어두운범례 모순 해소) · 세그먼트 위 라벨/스트로크 = 고정백(`OnSeriesFixed`) · 한글 타입페이스 일원화(`KoreanTypeface`, Malgun 보존; Noto 통일은 EXT-07). `EventInfoViewModel`에 **IThemeService guarded-optional 주입**(EXT-02 등록 전이면 null→Light 기본, graceful) + `ThemeChanged` 구독 시 열린 차트 rebuild + 구독해제. `DataChartPanelViewModel` 범례 중앙화. **V-07: 하드코딩 SKColor white·FromFamilyName 잔여 0**(provider 외), Events.Ui 빌드0. ⚠ 라이브 recolor 활성·V-08 시각검증은 EXT-02(IThemeService 등록)+앱 렌더 후.
  - **Phase 4 전파 — Events.Ui in-pattern 토큰화(IMPL-20 부분)**: 이벤트 카드/리포트의 확정 패턴 색 이관 — 카드 테두리·구분선 `#33000000`→`DividerBrush`·카드 배경 `White`→`SurfaceBrush`(byte-identical) · ColorZone 헤더 텍스트 `White`→`OnPrimaryFixedBrush`(byte-identical, on-primary 고정백) · 탐지/오류 severity `#FFDD2C00`·`Crimson`→`StatusCriticalBrush`(정규화). ② **신규 틴트/악센트 토큰화 완료**: KPI/severity 반투명 틴트 6색 → 신규 토큰 `TintInfo`/`TintSuccess`/`TintCritical`/`TintWarning`/`TintAccent`/`SurfaceTranslucent`(**Light=byte-identical 기존 hex**, Dark=다크표면 대비 별도값, storyboard/V-11 정밀화) · `DodgerBlue` 툴바→`PrimaryBrush` · 악센트 `#40C4FF`→`AccentBrush` · `WhiteSmoke`→`OnPrimaryFixedBrush` · muted `#88000000`→`TextSecondaryBrush`. EventInfo/CameraEventInfo/EventCardList/MalfunctionEventCard 등 6파일. **신규 토큰 Light≡Dark 파리티 린터 통과(테스트 18/18)**, 빌드0. 의미색·틴트 정규화는 V-11 시각 사인오프 대상.
  - **Phase 4 전파 — Sounds.Ui/Devices.Ui 토큰화(IMPL-18/19)**: Sounds.Ui 비활성 텍스트 `Gray`×8(SoundSettingView 7 + Resources.xaml 1)→`TextMutedBrush`. Devices.Ui AddSensorDialog 흰 테두리/제목 `White`×4→`OnPrimaryFixedBrush`(byte-identical #FFFFFF, white-on-colored 헤더). 둘 다 빌드0. (`SystemChrome*` 색 클론·Sounds Resources 폰트/converter 허브 통합은 Plan상 "부분"이라 이연. Devices `#FFE0B2` draft-row는 현 코드 부재.)
  - **Phase 4 파일럿 — Accounts.Ui 토큰화(IMPL-16/17)**: 7개 뷰(Login/Logout/MyPage/AccountManager/AccountSetup Panel + Editor/Register Dialog)의 하드코딩 색을 DynamicResource 토큰으로 이관. **byte-identical(AD-6)**: `#33000000` 구분선 12곳→`DividerBrush`(동일 hex) · `#88000000` 모달 스크림→`ScrimModalBrush`(동일 hex). **의도적 severity 정규화(FR-08, V-11 시각 사인오프 대상)**: 로그인 실패 테두리/결과 `Red`→`StatusCriticalBrush`(#C0392B) · 성공 `Green`→`StatusNormalBrush`(#2E9E5B) · 비활성 `Gray`→`TextMutedBrush`(#999999). 도입 토큰 5종 전부 Theme 정의 해소 확인(린터), 빌드0. ⚠ **런타임 해소는 앱이 Theme.Current 병합(EXT-01) 후 — 머지 시 EXT-01 선행 필수.** VER-11a 픽셀 diff는 앱 렌더 필요→이연. (다음: Sounds/Devices/Events 동일 패턴 전파 전 파일럿 패턴 확정)
  - **Phase 3 공용 컨트롤 스타일셋 완료(IMPL-12/13/14/15)**: Theme 어셈블리 내 keyed 스타일 3파일 신설 — `Styles.Controls.xaml`(Button Primary/Secondary/Danger/Text·TextBox·PasswordBox·ComboBox·Body/Mono TextBlock) · `Styles.Containers.xaml`(DataGrid 헤더/셀/행 hover·selected·Card·Dialog 패널/헤더) · `Styles.Nav.xaml`(툴바 버튼/토글·NavRail 탭·StatusBadge Critical/Warning/Normal/Info severity hue-lock). **implicit 금지(keyed only) → AD-6 Light byte-identical 보존**, MD BasedOn 유지+색/radius/font 만 DynamicResource 토큰. `Theme.Current.xaml`에 병합(소비자 단일 진입점). IMPL-15 OnPrimaryFixedBrush(양 테마 #FFFFFF white-on-blue 헤더)는 토큰 기존재+ModernDialogHeader로 실현. 검증: 스타일 참조 토큰 전수 정의 확인 테스트(`StyleTokenReferenceTests`) → **빌드 0 · 테스트 18/18**. (VER-06 시각 렌더는 파일럿 Accounts.Ui/Phase 4에서 재확인)
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

