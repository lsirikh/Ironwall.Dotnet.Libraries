# EVM 남은 36개 재검토 결과 (2026-10-11, t-20261011-081052-b135)

앞 작업(t-20261011-073820-4f76) 결과표에서 「범위 밖」 28개 · 「헤디드 남김」 8개를 하나씩 다시 봤다. 모든 새 시험은 창 없음(Window.Show · ShowDialog · HwndSource 없음), 가짜 API 기록 → 같은 번호 NATS 봉투, 탐지/장애 조치보고 전 같은 대상 중복 송신 없음.

약호: ELS = `Ironwall.Dotnet.Libraries.Events.Ui/Tests/EventLeftoverScenarioTests` · ELH = `tests/GMaps.Housing.Tests/EventLeftoverHousingScenarioTests` · EPC = `Ironwall.Dotnet.Libraries.CameraPopup.Tests/Unit/EventWindowPopupCoexistenceTests` · NUS = `Ironwall.Dotnet.Libraries.Nats/Tests/NatsUnitSubjectScenarioTests` · DCS = `Ironwall.Dotnet.Libraries.Devices.Ui/Tests/DeviceConsoleSyncSelectionTests`

## 1. 결과표

| id | 새 상태 | 시험 메서드 또는 남긴 이유 |
|---|---|---|
| EVM-003 | 통과(새) | ELS.should_write_group_dash_type_and_number_on_the_card_and_drop_the_dash_without_group_evm003_018 — 「GOP-1구역 - 센서 3」 · 두 그룹 「GOP-1구역, GOP-2구역 - 센서 3」 + 카드 XAML 머리 「탐지 이벤트」 · ZoneDeviceText 바인딩 |
| EVM-010 | 통과(새, 부분: 배지 원천만) | ELS.should_raise_update_action_with_two_cards_listed_when_two_detections_arrive_evm010 — UpdateAction 순간 카드 2장. 배지 글자(ShellViewModel.TotalEvents)는 호스트 |
| EVM-018 | 통과(새, 부분: 구분자 없음) | ELS.…_evm003_018(그룹 없음 사례 「센서 3」). 구역선 무변화는 그룹 없는 센서라 대상 구역선이 없어 따로 단언하지 않음 |
| EVM-022 | 범위 밖 | 같은 센서 ×20 = 「탐지/장애는 조치보고 전 같은 대상 중복 송신 없음」 사용자 규칙 위반 입력 |
| EVM-023 | 헤디드 남김 | 편집 모드 토글 = MapViewModel.IsEditModeEnabled → MainMap(GMap 지도 컨트롤).SetEditMode 마커 재구성 — 지도 컨트롤 · 실창 필요 |
| EVM-026 | 통과(새) | ELS.should_not_ask_the_map_to_move_when_events_arrive_and_only_when_the_card_is_double_clicked_evm026 — 탐지 · 장애 도착에 MapLocateRequest 0건, 카드 더블클릭(LocateCardAsync)에만 1건 |
| EVM-027 | 통과(새, 부분: 뷰모델 수준) | ELS.should_keep_listing_cards_by_the_panel_timer_alone_while_the_view_is_not_looked_at_evm027_028 — 활성 패널 타이머만으로 5장 · 모두 EntryId. 실제 셸 최소화는 헤디드 |
| EVM-028 | 통과(새, 부분: 뷰모델 수준) | 같은 시험. 실제 모달 열림 · 모달 유지는 헤디드 |
| EVM-029 | 통과(새, 부분: 뷰모델 수준) | ELS.should_show_every_card_that_arrived_before_the_panel_was_opened_once_it_is_opened_evm029 — 열기 전 도착 2장이 활성화 때 모두 오르고 조치 가능. 실제 패널 접기/펴기는 헤디드 |
| EVM-030 | 통과(새, 부분: 관리자 수준) | EPC.should_never_close_the_double_click_camera_popup_while_event_windows_open_evict_and_close_evm030 — 더블클릭 팝업(오버레이) 연 채 이벤트 창 열기 · 정리 · 조치 닫기에 CloseOverlay 0건, 닫은 것은 이벤트 키뿐. 실창 팝업 픽셀은 헤디드 |
| EVM-031 | 범위 밖 | grep: EVENT_CALL 은 Messages DTO · RtspMessage 모델 · BrokerMessageHelper 뿐, Events.Ui 등 탐지 경로에 송신 코드 0 — Redis EVENT_CALL 은 호스트 |
| EVM-035 | 통과(새, 부분: 라이브러리 몫) | ELS.should_split_array_envelope_take_only_detect_item_and_leave_no_card_once_its_report_is_routed_evm035 — 배열 봉투를 나눠 DETECT 만 적재(AR 항목 오인 · 오류 로그 0), 라우팅 결과(카드 버퍼 → 원격 종결) 뒤 카드 0. AR 항목을 패널로 옮기는 라우터 자체는 호스트 |
| EVM-037 | 범위 밖 | 같은 장비 반복 버스트 — 사용자 규칙 위반 입력 |
| EVM-040 | 통과(새, 부분: 상태 유지) | ELS.should_keep_the_typed_memo_and_choice_of_an_open_report_dialog_when_a_burst_arrives_and_send_that_memo_evm040 — 다른 센서 5대 버스트 뒤 메모 · '기타' 선택 · 대상 · [확인] · 안내 유지, 그 메모로 조치 1건. 실창 키 입력 지연은 헤디드 |
| EVM-044 | 범위 밖 | grep: 옛 cmd AI_DETECTION 정규화는 호스트 NatsBrokerService 뿐, 라이브러리는 무시(기존 EMS 무표지 시험이 확인) |
| EVM-048 | 통과(새, 부분: 최종 자세) | ELH.should_swing_3d_gate_leaves_on_contact_without_touching_the_status_color_evm048 — ContactOn/Off → 3D 통문 문짝 스토리보드(0.4 s, To 1/0) → DoorOpen 1 · 각도 (−80, 80) → (0, 0), 큐 0 · 색 축 불변. 중간 프레임 픽셀은 헤디드. 「빨강 그대로」(탐지 중 접점)는 같은 대상 중복 송신 규칙 때문에 색 축 불변으로 대신 |
| EVM-063 | 통과(새) | ELS.should_list_controller_fault_card_with_korean_reason_controller_number_and_section_one_to_three_evm063 — FAULT_CONTROLLER body → DTO → 카드: 「제어기 장애」 · 제어기 칸 3 · 센서 칸 비움 · 구간 1~3 · EntryId · 신호등 (0,1) → 조치로 정상 |
| EVM-068 | 범위 밖 | 구현 없음(GAP-N) — 1차 구간을 지도에 그리는 코드가 없다(카탈로그 정적 판독 1). 단언할 대상 없음 |
| EVM-069 | 범위 밖 | 구현 없음(GAP-N) — 2차 구간, 위와 같음 |
| EVM-080 | 범위 밖 | grep: 라이브러리에 CONNECTION 수신 서비스 없음 — body.device.status 반영은 호스트 ProcessConnection |
| EVM-081 | 통과(새) | ELS.should_never_produce_the_cyan_connection_state_on_symbol_or_zone_evm081 — 큐 우선순위 8 조합 모두 Connection 아님 + CONNECTION · SYNC_DEVICE 3상태 · 탐지 · 장애 · 조치 전 과정에서 심볼 · 구역선 Connection 0 |
| EVM-092 | 통과(새) | ELS.should_keep_card_symbol_and_alarm_and_write_nothing_when_report_dialog_is_cancelled_evm092 — [취소] → 창 닫기 알림만, 카드 · 심볼 · 구역선 · 소리 유지, 조치 쓰기 0 |
| EVM-097 | 통과(새, 부분: 구독 계산) | NUS.should_subscribe_only_own_unit_and_global_so_other_unit_action_report_never_arrives_evm097(6사례) — 구독 subject 가 자기 부대 · global 뿐이라 unit002 subject 와 와일드카드로도 겹치지 않음. 실제 배달 거름은 NATS 서버 |
| EVM-098 | 통과(새) | ELS.should_close_the_map_card_and_restore_the_symbol_when_the_console_tray_is_applied_evm098 — 콘솔 트레이 [적용] → 임시 카드 조치 → 호스트 EA 대역으로 지도 카드 바로 닫힘 · 심볼 정상 · 소리 멈춤 · 콘솔 행 '조치됨' |
| EVM-099 | 통과(새) | ELS.should_close_the_map_card_when_a_console_row_is_reported_through_its_report_dialog_evm099 — 행 [조치보고] 명령 → 조치 창 열기 요청 → [확인] → 지도 카드 닫힘 |
| EVM-102 | 통과(새) | ELS.should_close_every_card_restore_every_symbol_and_write_one_action_each_when_report_all_is_confirmed_evm102 — 실제 큐 · 심볼로 탐지 2 + 장애 1 전체 조치: 카드 0 · 조치 3 · 심볼/구역선 정상 · 신호등 (0,0) · ActionReported 3 |
| EVM-108 | 통과(새, 부분: 최종 자세) | ELH.should_swing_3d_housing_door_open_in_04s_and_back_when_operation_event_opens_and_closes_evm108(통문 · 함체) — OPERATION_EVENT → 3D 하우징 문짝 스토리보드 0.4 s → DoorOpen 1/0 · 관절 각도, 색 축 불변. 프레임 픽셀은 헤디드 |
| EVM-112 | 통과(새, 부분: 지도 아이콘) | ELH.should_show_fault_then_degraded_badge_and_plate_and_clear_them_when_ptz_component_status_changes_evm112_113 — PTZ 고장: 배지 · 이름표(「… 고장」) · 자동화 이름 「부품 고장 1」. 띠 카드 · 상세 부품 탭은 이 시험 밖 |
| EVM-113 | 통과(새) | 같은 시험 — SYNC_DEVICE 재조회로 저하(이름표 「… 저하」 · 「저하 1」) → 정상에서 배지 · 이름표 사라짐 |
| EVM-118 | 범위 밖 | grep: 라이브러리에 SYNC_DEVICE 처리 코드 없음 — CREATED 캐시 갱신은 호스트 ProcessSyncDeviceAsync |
| EVM-121 | 통과(기존+표지) | DCS.should_keep_the_unapplied_edit_and_say_so_when_the_selected_controller_is_updated_by_sync — SYNC_DEVICE 로 고른 장비가 바뀌어도 고치던 글 유지 + 알림(표지 한 줄만 추가, `git diff --numstat` 1 0) |
| EVM-122 | 범위 밖 | grep: 깨진 SYNC_DEVICE body 처리는 호스트 라우터 — 라이브러리 코드 없음 |
| EVM-127 | 범위 밖 | 조치 뒤 SYNC_DETECTION 발행 = 서버 몫 |
| EVM-128 | 범위 밖 | 조치 삭제 · action_reported 재계산 = 서버 몫 |
| EVM-129 | 범위 밖 | 억제 = 서버 REST 게이트(202, 발행 없음) — GIS 쪽 코드 없음(클라이언트 게이트 없음은 EVM-130 이 확인) |
| EVM-137 | 헤디드 남김 | 카메라 팝업 호스트 프로세스 죽임 · 멈춤 = 외부 프로세스(CameraPopup.Tests Survival 은 프로세스 · 창을 띄움) |

집계(36): 통과(새) 9 · 통과(새, 부분) 12 · 통과(기존+표지) 1 · 범위 밖 12 · 헤디드 남김 2 · 결함 0.

## 2. 찾은 결함

- 제품 결함 없음(빨강→초록 없음).
- 시험 쪽 고친 것(제품 무변경):
  - ELS 1차 실행 12/15 — EVM-092 · 040 · 099 가 `ArgumentNullException: Value cannot be null. (Parameter 'source')`(DetectionSelectionViewModel.CommonOrNullReference). 조치 창이 IoC 로 DeviceProvider 를 찾는데 시험 장치 IoC 에 없었다(호스트는 늘 등록). 장치 `Serve` 에 DeviceProvider(rig.Devices) 를 덧대 15/15.
  - ELH 1차 1/4 — 창 없는 렌더 시계에서 새 스토리보드는 첫 틱 전 `Stopped` 로 보여 상태 판정이 틀렸다. `SeekAlignedToLastTick` 으로 끝 시각을 맞추고 DoorOpen · 관절 각도로 판정. 2차 3/4 — 함체 기대값을 (−75, 0) 으로 잘못 잡음(오른쪽 관절 각은 거울값 75, 그 관절에 부품이 없을 뿐) → (−75, 75) 로 고쳐 4/4.
- 관찰(결함 아님 · 보고만): `EventCardListPanelViewModel.OnDeactivateAsync` 는 표시 카드 · 버퍼를 모두 Dispose · Clear 하지만 큐 엔트리는 남는다. 호스트는 카드 패널을 활성화만 하고(EventSectionShellViewModel) 끄지 않으므로 지금 경로로는 닿지 않는다 — 패널을 끄는 경로가 생기면 카드 없이 경보 · 심볼만 남는 위험.

## 3. 파일 · 명령 · 결과

새로 만든/고친 파일(새 .cs 4개 모두 UTF-8 BOM `ef bb bf` 확인):
- `Ironwall.Dotnet.Libraries.Events.Ui/Tests/EventLeftoverScenarioTests.cs` (새, 15 사례)
- `tests/GMaps.Housing.Tests/EventLeftoverHousingScenarioTests.cs` (새, 4 사례)
- `Ironwall.Dotnet.Libraries.CameraPopup.Tests/Unit/EventWindowPopupCoexistenceTests.cs` (새, 1 사례)
- `Ironwall.Dotnet.Libraries.Nats/Tests/NatsUnitSubjectScenarioTests.cs` (새, 6 사례)
- `Ironwall.Dotnet.Libraries.Devices.Ui/Tests/DeviceConsoleSyncSelectionTests.cs` (기존 시험에 `[Trait("Scenario", "EVM-121")]` 한 줄, 삭제 0줄)
- 제품 코드 무변경. 기존 시험 본문 무변경. 다른 에이전트의 `Events.Ui/Tests/ReportPhraseTrayScenarioTests.cs` 는 손대지 않음(컴파일 오류 없었음).

실행한 명령과 결과:
- `dotnet build Ironwall.Dotnet.Libraries.Events.Ui/…csproj --no-dependencies -v q -nologo -clp:ErrorsOnly` — 첫 시도 CS0103(SendActionRequestMessage 이름공간) 1건 → 고친 뒤 오류 0
- `dotnet test Events.Ui --no-build --filter "FullyQualifiedName~EventLeftoverScenarioTests"` — 1차 12/15 → 최종 **15/15 통과**
- `dotnet test Events.Ui --no-build --filter "FullyQualifiedName~ReportPhraseTrayScenarioTests"` — **3/3 통과**(RPT-025, 다른 에이전트 파일 — 시험 · 제품 결함 없음)
- `dotnet test Events.Ui --no-build --filter "Scenario~EVM"` — **123/123 통과**(앞 작업 108 + 이번 15)
- **Events.Ui 회귀**(창 띄우는 6 클래스 제외 필터) — **1970/1970 통과, 실패 0**(앞 작업 1952 + 이번 15 + ReportPhraseTray 3)
- `dotnet build tests/GMaps.Housing.Tests/…csproj`(의존 포함 1회, 오류 0) 뒤 `--no-dependencies` 재빌드 · `dotnet test … --filter "FullyQualifiedName~EventLeftoverHousingScenarioTests|FullyQualifiedName~EventMarkerPaintScenarioTests"` — 1차 1/4 · 2차 3/4 → 최종 **20/20 통과**(새 4 + 기존 16). HwndSource 를 쓰는 다른 Housing 시험 클래스는 돌리지 않음
- `dotnet build Ironwall.Dotnet.Libraries.CameraPopup.Tests/…csproj --no-dependencies` · `dotnet test … --filter "FullyQualifiedName~EventWindowPopupCoexistenceTests|FullyQualifiedName~Unit.EventWindowManagerTests"` — **24/24 통과**(Survival 클래스는 돌리지 않음)
- `dotnet build Ironwall.Dotnet.Libraries.Nats/…csproj --no-dependencies` · `dotnet test … --filter "FullyQualifiedName~NatsUnitSubjectScenarioTests"` — **6/6 통과**. (첫 실행은 기존 NatsServiceGlobalSubjectTests 5개를 함께 돌려 11/11 — 그 가운데 1개가 ConnectAsync 로 127.0.0.1:4222 에 핑을 시도하는 기존 시험이라 이후엔 빼고 돌림. 운영 서버 · 실제 NATS 접속 아님)
- `dotnet build Ironwall.Dotnet.Libraries.Devices.Ui/…csproj --no-dependencies` · `dotnet test … --filter "FullyQualifiedName~DeviceConsoleSyncSelectionTests"` — **8/8 통과**
