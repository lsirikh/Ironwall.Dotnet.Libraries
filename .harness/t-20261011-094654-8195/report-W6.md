# W6(CAMERA) 보고 — 본 세션이 작업자 보고를 옮겨 적음(작업자는 보고 파일 쓰기가 막혀 있음)

## 파일
- (새) Ironwall.Dotnet.Libraries.CameraPopup.Tests/Unit/MapCameraPopupScenarioTests.cs — 시나리오 시험 13건(BOM 확인)
- (새) Ironwall.Dotnet.Libraries.CameraPopup.Tests/Unit/MapCameraPopupScenarioSupport.cs — RealXaml(실제 MapView.xaml · CameraStreamPopupStyle.xaml 조각을 AutomationId 로 떼어 파싱, 바인딩 · 트리거 · id 원본 그대로) · PopupControlRig(실제 CameraStreamPopupControl + 같은 PART_ 이름 시험 템플릿)
- (고침) Unit/MapCameraPopupRig.cs(앞 작업이 만든 시험 장치): ① STA IoC 에 IEventAggregator 등록(지도 컨트롤 생성자가 꺼냄) ② NewVm 에 vm.IsNotifying = true(생성자 생략으로 알림 꺼져 있었음)
- 제품 코드 · 기존 시험 본문 무변경

## 결론
| id | 결론 | 시험 | 결과 |
|---|---|---|---|
| MAP-183 | 가 | MapCameraPopupScenarioTests::should_play_overlay_and_reopen_at_moved_spot_when_camera_symbol_double_clicked · ::should_bring_open_popup_to_front_without_second_stream_when_same_camera_double_clicked_again | 통과 |
| MAP-184 | 가 | ::should_keep_leader_line_from_symbol_to_popup_edge_inside_map_when_panned_and_remove_it_when_closed | 통과 |
| MAP-185 | 가 | ::should_send_one_relative_drag_move_when_ptz_overlay_video_dragged_and_none_when_clicked_or_cancelled · ::should_show_reason_and_send_no_ptz_when_fixed_or_unpermitted_camera_overlay_dragged | 통과 |
| MAP-186 | 가 | ::should_show_host_notice_with_restart_on_map_when_host_suspended_and_restart_host_from_it | 통과 |
| MAP-187 | 가 | ::should_show_broker_toast_and_open_no_overlay_when_camera_double_clicked_in_broker_mode · ::should_show_failure_toast_and_open_no_overlay_when_nvr_rejects_or_never_answers(2) · ::should_show_no_broker_toast_when_broker_mode_has_no_broker_service | 통과 |
| EVM-137 | 가 | ::should_fail_only_the_dead_stream_popup_and_retry_only_it_when_one_camera_stream_dies · ::should_keep_gis_alive_show_connecting_and_restore_every_popup_when_host_dies_and_comes_back · ::should_keep_restore_list_and_offer_restart_when_real_supervisor_has_no_host | 통과 |

## 단언 요지
- MAP-183: 실제 더블클릭 처리기 → 심볼 우상단 팝업 · 호스트 열기 요청(map-cam-7, RTSP 서브, 384×258) · VideoStatus "연결 중…" → 재생 알림 뒤 "재생" · 머리 끌기 뗌 → 위경도 DB 저장 · ✕ 닫힘/해제 · 다시 열면 옮긴 자리(±1.5px) · 같은 카메라 재더블클릭은 새 스트림 없이 맨 앞.
- MAP-184: 연결선 레이어 ClipToBounds(지도 안) · 끝점1=심볼 중점 · 끝점2=팝업 테두리 · 팬 · 팝업 이동에 따라감 · ✕ 뒤 연결선 제거 + 이동 정지/포커스 정지.
- MAP-185: 영상 끌기 → DragMove 1건(ViewX 0.25 / ViewY −0.1), 짧은 클릭 · 취소 0건 · 고정 카메라 "PTZ 지원안함" · 권한 없음 "PTZ 권한 없음" · 명령 0건.
- MAP-186: 일시 중지 → Map.CameraPopup.HostNotice "영상 기능 일시 중지" + [다시 시작] · cal:Message.Attach(RestartCameraPopupHost) 호출로 재시작 · 복구 뒤 안내 사라짐 · 호스트 없음은 토스트.
- MAP-187: 실제 브로커 서비스(요청 실행기만 가짜) · 대기 토스트 · CAMERA_POPUP_OPEN 1건(연타 합침) · "모니터 2 · 칸 5에 띄웠습니다" · 거부/무응답/창구 없음 문구 · 지도 팝업 0.
- EVM-137: 스트림 하나만 끊기면 그 팝업만 "연결 안 됨"+[다시 시도](그 스트림만 재개) · 호스트 사망에도 GIS 유지 · 전부 "연결 중…"+[다시 시작] · 복귀 뒤 전부 복구 · 실제 감시자(실행 파일 없이)의 복원 목록 · 재시작 시도.

## 한계(기록)
- 머리 끌기 이동량: 창 없이 캔버스 기준 마우스 위치를 못 읽어 CanvasLeft/Top 더하기만 직접 넣음(누름 · 뗌 · 저장 경로는 실제).
- EVM-137 타일 화면(TileView)은 호스트 프로세스 안이라 이 프로젝트 밖(Host.Tests 몫) — GIS 쪽만 단언.
- 실제 일시 중지(재시작 예산 소진)는 실제 호스트 반복 종료가 필요해(금지) 가짜 호스트 상태 변화로 대신.

## 결함
- 없음

## 실행
- 필터 `Category!=Survival&Category!=LibVlc&Category!=RealCamera`(실호스트 · LibVLC · 실카메라 제외, Unit 폴더엔 창 시험 없음)
- `통과!  - 실패: 0, 통과: 251, 건너뜀: 0, 전체: 251 - Ironwall.Dotnet.Libraries.CameraPopup.Tests.dll`
- 새 시험 3회: 13/13 ×3
