<!-- auto-section-start -->
# 프로젝트 문서 인덱스

- **마지막 갱신**: 2026-05-22
- **총 문서 수**: 122개

---

## 레퍼런스

| 파일 | 내용 | 날짜 |
|------|------|------|
| [EVENT_PIPELINE_LAYERS.md](EVENT_PIPELINE_LAYERS.md) | NATS 이벤트 파이프라인 8개 레이어 명명 체계 ([INGEST]/[QUEUE]/[SYMBOL]/[SOUND]/[CARD]/[ACTION]/[MAP]/[DB]) | 2026-05-21 |

## 분석 (docs/analysis/)

| 파일 | 분석 대상 | 날짜 |
|------|---------|------|
| [EVENT_PROCESS_VISUALIZATION.md](analysis/EVENT_PROCESS_VISUALIZATION.md) | 이벤트 파이프라인 전체 시각화 — 레이어별 ASCII 흐름도·Mermaid 시퀀스·상태 다이어그램·메서드-클래스 맵 | 2026-05-22 |
| [ANALYSIS_Skillset_Issues_And_Improvements.md](analysis/ANALYSIS_Skillset_Issues_And_Improvements.md) | ANALYSIS_Skillset_Issues_And_Improvements.md | 2026-05-19 |
| [ANALYSIS_View_Architecture.md](analysis/ANALYSIS_View_Architecture.md) | ANALYSIS_View_Architecture.md | 2026-05-18 |
| [ANALYSIS_Detection_Action_Process_Flow.md](analysis/ANALYSIS_Detection_Action_Process_Flow.md) | ANALYSIS_Detection_Action_Process_Flow.md | 2026-05-18 |
| [NATS_Detection_Redis_Flow.md](analysis/NATS_Detection_Redis_Flow.md) | NATS_Detection_Redis_Flow.md | 2026-05-15 |
| [ANALYSIS_GatewayEvent_Group_NtoN_Migration.md](analysis/ANALYSIS_GatewayEvent_Group_NtoN_Migration.md) | ANALYSIS_GatewayEvent_Group_NtoN_Migration.md | 2026-05-15 |

## 요구사항 정의서 (docs/prds/)

| 파일 | 내용 | 상태 | 날짜 |
|------|------|------|------|
| [OverlayMap_MBTiles_Provider-prd.md](prds/OverlayMap_MBTiles_Provider-prd.md) | OverlayMap PNG→MBTiles 전환 — 신규 Provider, TMS 좌표 변환, LRU 활성화, DB 스키마 마이그레이션 (시뮬레이션 S-01~S-15) | Draft | 2026-06-02 |
| [RedisDomainService_DoubleStop_Fix-prd.md](prds/RedisDomainService_DoubleStop_Fix-prd.md) | RedisBrokerService.StopAsync 이중 호출 NRE 수정 — _redisService.StopAsync 제거, NatsDomainService 패턴 통일 | Approved | 2026-06-01 |
| [NatsShutdown_SubscriptionHang_Fix-prd.md](prds/NatsShutdown_SubscriptionHang_Fix-prd.md) | NATS 종료 10초 블로킹 — startupToken/shutdownToken 수명주기 불일치, _subscriptionCts + Interlocked 재진입 가드 (시뮬레이션 2회) | Approved | 2026-06-01 |
| [AppShutdown_Blocking_Fix-prd.md](prds/AppShutdown_Blocking_Fix-prd.md) | 앱 종료 Task.Run 블로킹 — MessageService async void + Redis CloseAsync 토큰 미지원 수정 (시뮬레이션 2회, Fix A/B 검증) | Approved | 2026-05-28 |
| [AutoActionReport_DualPath_Fix-prd.md](prds/AutoActionReport_DualPath_Fix-prd.md) | 자동 조치보고 이중 경로(Path A 타이머 + Path B 공유 타이머) 버그 수정 — 46개 결함, 8단계 안전 구현 시퀀스, 4회 시뮬레이션 검증 | Draft | 2026-05-22 |
| [Event_Performance_Optimization-prd.md](prds/Event_Performance_Optimization-prd.md) | 이벤트 파이프라인 전체 성능 최적화 — EventCard(FR-01~10) + Symbol STA 위반(FR-11) + EQM lock(FR-12). 4회 시뮬레이션 검증, Blocker 1개 + Critical 12개 반영 | Draft | 2026-05-22 |
| [BatchReport_Sound_Stop_Fix-prd.md](prds/BatchReport_Sound_Stop_Fix-prd.md) | 일괄 조치보고 시 장애 사운드 즉시 중지 미동작 수정 (OnQueueCleared 이벤트 + _stopAll 추가) | Draft | 2026-05-21 |
| [PRD_SplashScreen_Startup_Gating.md](prds/PRD_SplashScreen_Startup_Gating.md) | SplashScreen 기동 시퀀스 + API/NATS 연결 게이팅 (워치독 타이머 제거 전제) | Draft | 2026-05-20 |
| [PRD_PidsSymbol_Transparency_Blink.md](prds/PRD_PidsSymbol_Transparency_Blink.md) | PIDS 심볼 투명도/깜빡임 버그 수정 (8개 버그, 6파일) | Draft | 2026-05-20 |
| [SoundTypeSwitch_ImmediateStop_Fix-prd.md](prds/SoundTypeSwitch_ImmediateStop_Fix-prd.md) | 이종 이벤트 사운드 즉시 전환 수정 (Detection↔Fault stop+play 원자화) | Completed | 2026-05-20 |
| [Device_CompositeState_SSOT_And_FaultAutoRecovery-prd.md](prds/Device_CompositeState_SSOT_And_FaultAutoRecovery-prd.md) | 개별 디바이스 복합 상태 SSOT 전환 + Fault 자동복구 | Completed | 2026-05-19 |
| [FenceGroup_Blink_And_Sound_DualPlay_Fix-prd.md](prds/FenceGroup_Blink_And_Sound_DualPlay_Fix-prd.md) | FenceGroup 깜빡임 재설계 + 사운드 이중재생 버그 수정 | Completed | 2026-05-19 |
| [Malfunction_CompositeState_And_FenceGroup_Visualization-prd.md](prds/Malfunction_CompositeState_And_FenceGroup_Visualization-prd.md) | Malfunction_CompositeState_And_FenceGroup_Visualization | Completed | 2026-05-19 |
| [BatchReport_DualInsert_And_MalfunctionRestore_Fix-prd.md](prds/BatchReport_DualInsert_And_MalfunctionRestore_Fix-prd.md) | BatchReport_DualInsert_And_MalfunctionRestore_Fix | Completed | 2026-05-19 |
| [GatewayEvent_Group_NtoN_Migration-prd.md](prds/GatewayEvent_Group_NtoN_Migration-prd.md) | GatewayEvent_Group_NtoN_Migration | Completed | 2026-05-19 |
| [Detection_Sound_And_DualPath_Fix-prd.md](prds/Detection_Sound_And_DualPath_Fix-prd.md) | Detection_Sound_And_DualPath_Fix | Completed | 2026-05-18 |
| [GMapCustomControl_ImageDrag_BugFix-prd.md](prds/GMapCustomControl_ImageDrag_BugFix-prd.md) | GMapCustomControl_ImageDrag_BugFix | Completed | 2026-05-15 |
| [LayerPanel_ContextMenu_Enhancement-prd.md](prds/LayerPanel_ContextMenu_Enhancement-prd.md) | LayerPanel_ContextMenu_Enhancement | Completed | 2026-05-14 |
| [PRD_ImageOverlay_FileCopy_On_Register.md](prds/PRD_ImageOverlay_FileCopy_On_Register.md) | PRD_ImageOverlay_FileCopy_On_Register.md | Completed | 2026-05-13 |

## 구현 플랜 (docs/plans/)

| 파일 | 연관 PRD | 진행률 | 날짜 |
|------|---------|--------|------|
| [Event_Performance_Optimization-prd-plan.md](plans/Event_Performance_Optimization-prd-plan.md) | [PRD](prds/Event_Performance_Optimization-prd.md) | 0/21 | 2026-05-22 |
| [SoundTypeSwitch_ImmediateStop_Fix-plan.md](plans/SoundTypeSwitch_ImmediateStop_Fix-plan.md) | [PRD](prds/SoundTypeSwitch_ImmediateStop_Fix-prd.md) | 10/10 | 2026-05-20 |
| [Device_CompositeState_SSOT_And_FaultAutoRecovery-plan.md](plans/Device_CompositeState_SSOT_And_FaultAutoRecovery-plan.md) | [PRD](prds/Device_CompositeState_SSOT_And_FaultAutoRecovery-prd.md) | 13/13 | 2026-05-19 |
| [Malfunction_CompositeState_And_FenceGroup_Visualization-plan.md](plans/Malfunction_CompositeState_And_FenceGroup_Visualization-plan.md) | [PRD](prds/Malfunction_CompositeState_And_FenceGroup_Visualization-prd.md) | 35/35 | 2026-05-19 |
| [BatchReport_DualInsert_And_MalfunctionRestore_Fix-plan.md](plans/BatchReport_DualInsert_And_MalfunctionRestore_Fix-plan.md) | [PRD](prds/BatchReport_DualInsert_And_MalfunctionRestore_Fix-prd.md) | 26/26 | 2026-05-19 |
| [Detection_Sound_And_DualPath_Fix-prd-plan.md](plans/Detection_Sound_And_DualPath_Fix-prd-plan.md) | [PRD](prds/Detection_Sound_And_DualPath_Fix-prd.md) | 21/21 | 2026-05-19 |
| [GatewayEvent_Group_NtoN_Migration-plan.md](plans/GatewayEvent_Group_NtoN_Migration-plan.md) | [PRD](prds/GatewayEvent_Group_NtoN_Migration-prd.md) | 43/43 | 2026-05-19 |
| [GMapCustomControl_ImageDrag_BugFix-plan.md](plans/GMapCustomControl_ImageDrag_BugFix-plan.md) | [PRD](prds/GMapCustomControl_ImageDrag_BugFix-prd.md) | 9/9 | 2026-05-15 |
| [LayerPanel_ContextMenu_Enhancement-prd-plan.md](plans/LayerPanel_ContextMenu_Enhancement-prd-plan.md) | [PRD](prds/LayerPanel_ContextMenu_Enhancement-prd.md) | 31/31 | 2026-05-14 |
| [PRD_ImageOverlay_FileCopy_On_Register-prd-plan.md](plans/PRD_ImageOverlay_FileCopy_On_Register-prd-plan.md) | [PRD](prds/PRD_ImageOverlay_FileCopy_On_Register-prd.md) | 9/9 | 2026-05-13 |

## 테스트 결과 (docs/tests/)

| 파일 | 통과율 | 커버리지 | 날짜 |
|------|--------|---------|------|
| [TEST_ImageOverlay_FileCopy_On_Register.md](tests/TEST_ImageOverlay_FileCopy_On_Register.md) | -% | -% | 2026-05-13 |

## 완료 리포트 (docs/reports/)

| 파일 | 문서 연결 체인 | 날짜 |
|------|------------|------|
| [Device_CompositeState_SSOT_And_FaultAutoRecovery-report.md](reports/Device_CompositeState_SSOT_And_FaultAutoRecovery-report.md) | [PRD](prds/Device_CompositeState_SSOT_And_FaultAutoRecovery-prd.md) → [Plan](plans/Device_CompositeState_SSOT_And_FaultAutoRecovery-plan.md) | 2026-05-19 |
| [Detection_Sound_And_DualPath_Fix-report.md](reports/Detection_Sound_And_DualPath_Fix-report.md) | [PRD](prds/Detection_Sound_And_DualPath_Fix-prd.md) → [Plan](plans/Detection_Sound_And_DualPath_Fix-prd-plan.md) | 2026-05-18 |
| [REPORT_GMapCustomControl_ImageDrag_BugFix.md](reports/REPORT_GMapCustomControl_ImageDrag_BugFix.md) | [PRD](prds/REPORT_GMapCustomControl_ImageDrag_BugFix.md-prd.md) → [Plan](plans/REPORT_GMapCustomControl_ImageDrag_BugFix.md-prd-plan.md) | 2026-05-15 |
| [REPORT_ImageOverlay_FileCopy_On_Register.md](reports/REPORT_ImageOverlay_FileCopy_On_Register.md) | [PRD](prds/REPORT_ImageOverlay_FileCopy_On_Register.md-prd.md) → [Plan](plans/REPORT_ImageOverlay_FileCopy_On_Register.md-prd-plan.md) | 2026-05-13 |
| [REPORT_DetectionEvent_Symbol_Visual_Restore.md](reports/REPORT_DetectionEvent_Symbol_Visual_Restore.md) | [PRD](prds/REPORT_DetectionEvent_Symbol_Visual_Restore.md-prd.md) → [Plan](plans/REPORT_DetectionEvent_Symbol_Visual_Restore.md-prd-plan.md) | 2026-05-12 |
| [REPORT_Broadcast_Panel_Embedded.md](reports/REPORT_Broadcast_Panel_Embedded.md) | [PRD](prds/REPORT_Broadcast_Panel_Embedded.md-prd.md) → [Plan](plans/REPORT_Broadcast_Panel_Embedded.md-prd-plan.md) | 2026-05-12 |
| [REPORT_Pids_Symbol_Background.md](reports/REPORT_Pids_Symbol_Background.md) | [PRD](prds/REPORT_Pids_Symbol_Background.md-prd.md) → [Plan](plans/REPORT_Pids_Symbol_Background.md-prd-plan.md) | 2026-05-12 |
| [REPORT_OverlayMap_Visibility_Activate.md](reports/REPORT_OverlayMap_Visibility_Activate.md) | [PRD](prds/REPORT_OverlayMap_Visibility_Activate.md-prd.md) → [Plan](plans/REPORT_OverlayMap_Visibility_Activate.md-prd-plan.md) | 2026-05-12 |
| [REPORT_EventCard_EntryId_Connection.md](reports/REPORT_EventCard_EntryId_Connection.md) | [PRD](prds/REPORT_EventCard_EntryId_Connection.md-prd.md) → [Plan](plans/REPORT_EventCard_EntryId_Connection.md-prd-plan.md) | 2026-05-12 |
| [REPORT_MBTiles_DefinedMap_Integration.md](reports/REPORT_MBTiles_DefinedMap_Integration.md) | [PRD](prds/REPORT_MBTiles_DefinedMap_Integration.md-prd.md) → [Plan](plans/REPORT_MBTiles_DefinedMap_Integration.md-prd-plan.md) | 2026-05-12 |
| [REPORT_DeviceGroupSelection_ProgressCircle.md](reports/REPORT_DeviceGroupSelection_ProgressCircle.md) | [PRD](prds/REPORT_DeviceGroupSelection_ProgressCircle.md-prd.md) → [Plan](plans/REPORT_DeviceGroupSelection_ProgressCircle.md-prd-plan.md) | 2026-05-12 |
| [REPORT_Gateway_DeviceGroup_Migration.md](reports/REPORT_Gateway_DeviceGroup_Migration.md) | [PRD](prds/REPORT_Gateway_DeviceGroup_Migration.md-prd.md) → [Plan](plans/REPORT_Gateway_DeviceGroup_Migration.md-prd-plan.md) | 2026-05-12 |
| [REPORT_Layer_Panel_Tree_Redesign.md](reports/REPORT_Layer_Panel_Tree_Redesign.md) | [PRD](prds/REPORT_Layer_Panel_Tree_Redesign.md-prd.md) → [Plan](plans/REPORT_Layer_Panel_Tree_Redesign.md-prd-plan.md) | 2026-05-12 |
| [REPORT_DeviceView_ViewModel_Alignment.md](reports/REPORT_DeviceView_ViewModel_Alignment.md) | [PRD](prds/REPORT_DeviceView_ViewModel_Alignment.md-prd.md) → [Plan](plans/REPORT_DeviceView_ViewModel_Alignment.md-prd-plan.md) | 2026-05-06 |
| [REPORT_Geolocation_AllDevices.md](reports/REPORT_Geolocation_AllDevices.md) | [PRD](prds/REPORT_Geolocation_AllDevices.md-prd.md) → [Plan](plans/REPORT_Geolocation_AllDevices.md-prd-plan.md) | 2026-05-06 |
| [REPORT_DeviceGroup_Assignment_Ui.md](reports/REPORT_DeviceGroup_Assignment_Ui.md) | [PRD](prds/REPORT_DeviceGroup_Assignment_Ui.md-prd.md) → [Plan](plans/REPORT_DeviceGroup_Assignment_Ui.md-prd-plan.md) | 2026-05-06 |
| [REPORT_SelectionView_Layout_Compact.md](reports/REPORT_SelectionView_Layout_Compact.md) | [PRD](prds/REPORT_SelectionView_Layout_Compact.md-prd.md) → [Plan](plans/REPORT_SelectionView_Layout_Compact.md-prd-plan.md) | 2026-05-06 |
| [REPORT_EventPanel_Cache_Reuse.md](reports/REPORT_EventPanel_Cache_Reuse.md) | [PRD](prds/REPORT_EventPanel_Cache_Reuse.md-prd.md) → [Plan](plans/REPORT_EventPanel_Cache_Reuse.md-prd-plan.md) | 2026-05-06 |
| [REPORT_PanelView_Missing_Columns.md](reports/REPORT_PanelView_Missing_Columns.md) | [PRD](prds/REPORT_PanelView_Missing_Columns.md-prd.md) → [Plan](plans/REPORT_PanelView_Missing_Columns.md-prd-plan.md) | 2026-05-06 |
| [REPORT_EventPanel_Cancel_Token.md](reports/REPORT_EventPanel_Cancel_Token.md) | [PRD](prds/REPORT_EventPanel_Cancel_Token.md-prd.md) → [Plan](plans/REPORT_EventPanel_Cancel_Token.md-prd-plan.md) | 2026-05-06 |
| [REPORT_DeviceTab_Header_Truncation.md](reports/REPORT_DeviceTab_Header_Truncation.md) | [PRD](prds/REPORT_DeviceTab_Header_Truncation.md-prd.md) → [Plan](plans/REPORT_DeviceTab_Header_Truncation.md-prd-plan.md) | 2026-05-06 |
| [REPORT_SelectionView_CheckBox_To_ComboBox.md](reports/REPORT_SelectionView_CheckBox_To_ComboBox.md) | [PRD](prds/REPORT_SelectionView_CheckBox_To_ComboBox.md-prd.md) → [Plan](plans/REPORT_SelectionView_CheckBox_To_ComboBox.md-prd-plan.md) | 2026-05-06 |
| [REPORT_Nats_Detection_Routing_Fix.md](reports/REPORT_Nats_Detection_Routing_Fix.md) | [PRD](prds/REPORT_Nats_Detection_Routing_Fix.md-prd.md) → [Plan](plans/REPORT_Nats_Detection_Routing_Fix.md-prd-plan.md) | 2026-05-06 |
| [REPORT_GetMarkerAtScreen_Priority_Fix.md](reports/REPORT_GetMarkerAtScreen_Priority_Fix.md) | [PRD](prds/REPORT_GetMarkerAtScreen_Priority_Fix.md-prd.md) → [Plan](plans/REPORT_GetMarkerAtScreen_Priority_Fix.md-prd-plan.md) | 2026-03-30 |
| [REPORT_Symbol_ZOrder_HitTest_Bug.md](reports/REPORT_Symbol_ZOrder_HitTest_Bug.md) | [PRD](prds/REPORT_Symbol_ZOrder_HitTest_Bug.md-prd.md) → [Plan](plans/REPORT_Symbol_ZOrder_HitTest_Bug.md-prd-plan.md) | 2026-03-30 |
| [REPORT_Symbol_ZOrder_Control.md](reports/REPORT_Symbol_ZOrder_Control.md) | [PRD](prds/REPORT_Symbol_ZOrder_Control.md-prd.md) → [Plan](plans/REPORT_Symbol_ZOrder_Control.md-prd-plan.md) | 2026-03-30 |
| [REPORT_Map_DragButton_LeftMouse.md](reports/REPORT_Map_DragButton_LeftMouse.md) | [PRD](prds/REPORT_Map_DragButton_LeftMouse.md-prd.md) → [Plan](plans/REPORT_Map_DragButton_LeftMouse.md-prd-plan.md) | 2026-03-30 |
| [REPORT_EditMode_HitTest_Passthrough.md](reports/REPORT_EditMode_HitTest_Passthrough.md) | [PRD](prds/REPORT_EditMode_HitTest_Passthrough.md-prd.md) → [Plan](plans/REPORT_EditMode_HitTest_Passthrough.md-prd-plan.md) | 2026-03-30 |
| [REPORT_OverlayImage_ZOrder_EditMode.md](reports/REPORT_OverlayImage_ZOrder_EditMode.md) | [PRD](prds/REPORT_OverlayImage_ZOrder_EditMode.md-prd.md) → [Plan](plans/REPORT_OverlayImage_ZOrder_EditMode.md-prd-plan.md) | 2026-03-27 |
| [REPORT_OverlayMap_ZOrder_Rendering.md](reports/REPORT_OverlayMap_ZOrder_Rendering.md) | [PRD](prds/REPORT_OverlayMap_ZOrder_Rendering.md-prd.md) → [Plan](plans/REPORT_OverlayMap_ZOrder_Rendering.md-prd-plan.md) | 2026-03-26 |
| [REPORT_Layer_Ordering_Investigation.md](reports/REPORT_Layer_Ordering_Investigation.md) | [PRD](prds/REPORT_Layer_Ordering_Investigation.md-prd.md) → [Plan](plans/REPORT_Layer_Ordering_Investigation.md-prd-plan.md) | 2026-03-26 |
| [REPORT_OverlayImage_Status_Analysis.md](reports/REPORT_OverlayImage_Status_Analysis.md) | [PRD](prds/REPORT_OverlayImage_Status_Analysis.md-prd.md) → [Plan](plans/REPORT_OverlayImage_Status_Analysis.md-prd-plan.md) | 2026-03-25 |
| [REPORT_MBTiles_ZoomLevel_Shadowing_Fix.md](reports/REPORT_MBTiles_ZoomLevel_Shadowing_Fix.md) | [PRD](prds/REPORT_MBTiles_ZoomLevel_Shadowing_Fix.md-prd.md) → [Plan](plans/REPORT_MBTiles_ZoomLevel_Shadowing_Fix.md-prd-plan.md) | 2026-03-24 |
| [REPORT_MapViewModel_Provider_Cleanup.md](reports/REPORT_MapViewModel_Provider_Cleanup.md) | [PRD](prds/REPORT_MapViewModel_Provider_Cleanup.md-prd.md) → [Plan](plans/REPORT_MapViewModel_Provider_Cleanup.md-prd-plan.md) | 2026-03-24 |
| [REPORT_EntryId_Nats_Uuid_DirectMatch.md](reports/REPORT_EntryId_Nats_Uuid_DirectMatch.md) | [PRD](prds/REPORT_EntryId_Nats_Uuid_DirectMatch.md-prd.md) → [Plan](plans/REPORT_EntryId_Nats_Uuid_DirectMatch.md-prd-plan.md) | 2026-03-13 |
| [REPORT_CollectionChanged_BatchReset.md](reports/REPORT_CollectionChanged_BatchReset.md) | [PRD](prds/REPORT_CollectionChanged_BatchReset.md-prd.md) → [Plan](plans/REPORT_CollectionChanged_BatchReset.md-prd-plan.md) | 2026-03-13 |
| [REPORT_SharedTimer_Chunk_Dequeue.md](reports/REPORT_SharedTimer_Chunk_Dequeue.md) | [PRD](prds/REPORT_SharedTimer_Chunk_Dequeue.md-prd.md) → [Plan](plans/REPORT_SharedTimer_Chunk_Dequeue.md-prd-plan.md) | 2026-03-13 |
| [REPORT_EventQueue_Logic_Analysis.md](reports/REPORT_EventQueue_Logic_Analysis.md) | [PRD](prds/REPORT_EventQueue_Logic_Analysis.md-prd.md) → [Plan](plans/REPORT_EventQueue_Logic_Analysis.md-prd-plan.md) | 2026-03-13 |
| [REPORT_EventQueue_Symbol_Unification.md](reports/REPORT_EventQueue_Symbol_Unification.md) | [PRD](prds/REPORT_EventQueue_Symbol_Unification.md-prd.md) → [Plan](plans/REPORT_EventQueue_Symbol_Unification.md-prd-plan.md) | 2026-03-13 |
| [REPORT_Batch_Action_Report.md](reports/REPORT_Batch_Action_Report.md) | [PRD](prds/REPORT_Batch_Action_Report.md-prd.md) → [Plan](plans/REPORT_Batch_Action_Report.md-prd-plan.md) | 2026-03-13 |
| [REPORT_DevicePanel_ProgressCircle_Fix.md](reports/REPORT_DevicePanel_ProgressCircle_Fix.md) | [PRD](prds/REPORT_DevicePanel_ProgressCircle_Fix.md-prd.md) → [Plan](plans/REPORT_DevicePanel_ProgressCircle_Fix.md-prd-plan.md) | 2026-03-12 |
| [ANALYSIS_DevicePanel_ProgressCircle_Visibility.md](reports/ANALYSIS_DevicePanel_ProgressCircle_Visibility.md) | [PRD](prds/ANALYSIS_DevicePanel_ProgressCircle_Visibility.md-prd.md) → [Plan](plans/ANALYSIS_DevicePanel_ProgressCircle_Visibility.md-prd-plan.md) | 2026-03-12 |
| [ANALYSIS_Redis_RTSP_Popup_Communication.md](reports/ANALYSIS_Redis_RTSP_Popup_Communication.md) | [PRD](prds/ANALYSIS_Redis_RTSP_Popup_Communication.md-prd.md) → [Plan](plans/ANALYSIS_Redis_RTSP_Popup_Communication.md-prd-plan.md) | 2026-03-12 |
| [REPORT_EventPanel_CentralizedDatePicker.md](reports/REPORT_EventPanel_CentralizedDatePicker.md) | [PRD](prds/REPORT_EventPanel_CentralizedDatePicker.md-prd.md) → [Plan](plans/REPORT_EventPanel_CentralizedDatePicker.md-prd-plan.md) | 2026-03-12 |
| [ANALYSIS_EventPanel_Chart_DataGrid_Mismatch.md](reports/ANALYSIS_EventPanel_Chart_DataGrid_Mismatch.md) | [PRD](prds/ANALYSIS_EventPanel_Chart_DataGrid_Mismatch.md-prd.md) → [Plan](plans/ANALYSIS_EventPanel_Chart_DataGrid_Mismatch.md-prd-plan.md) | 2026-03-12 |
| [REPORT_Event_BatchUI_Performance.md](reports/REPORT_Event_BatchUI_Performance.md) | [PRD](prds/REPORT_Event_BatchUI_Performance.md-prd.md) → [Plan](plans/REPORT_Event_BatchUI_Performance.md-prd-plan.md) | 2026-03-08 |
| [REPORT_Event_Pipeline_Redesign.md](reports/REPORT_Event_Pipeline_Redesign.md) | [PRD](prds/REPORT_Event_Pipeline_Redesign.md-prd.md) → [Plan](plans/REPORT_Event_Pipeline_Redesign.md-prd-plan.md) | 2026-03-08 |
| [ANALYSIS_Performance_Event_Processing_v2.md](reports/ANALYSIS_Performance_Event_Processing_v2.md) | [PRD](prds/ANALYSIS_Performance_Event_Processing_v2.md-prd.md) → [Plan](plans/ANALYSIS_Performance_Event_Processing_v2.md-prd-plan.md) | 2026-03-07 |
| [REPORT_DeviceGroup_AssignDialog_Wrapper.md](reports/REPORT_DeviceGroup_AssignDialog_Wrapper.md) | [PRD](prds/REPORT_DeviceGroup_AssignDialog_Wrapper.md-prd.md) → [Plan](plans/REPORT_DeviceGroup_AssignDialog_Wrapper.md-prd-plan.md) | 2026-03-07 |
| [REPORT_WindyMode_Nats_Integration.md](reports/REPORT_WindyMode_Nats_Integration.md) | [PRD](prds/REPORT_WindyMode_Nats_Integration.md-prd.md) → [Plan](plans/REPORT_WindyMode_Nats_Integration.md-prd-plan.md) | 2026-03-06 |
| [ANALYSIS_Performance_Event_Processing.md](reports/ANALYSIS_Performance_Event_Processing.md) | [PRD](prds/ANALYSIS_Performance_Event_Processing.md-prd.md) → [Plan](plans/ANALYSIS_Performance_Event_Processing.md-prd-plan.md) | 2026-03-06 |
| [REPORT_SymbolVisual_SyncDevice_Unification.md](reports/REPORT_SymbolVisual_SyncDevice_Unification.md) | [PRD](prds/REPORT_SymbolVisual_SyncDevice_Unification.md-prd.md) → [Plan](plans/REPORT_SymbolVisual_SyncDevice_Unification.md-prd-plan.md) | 2026-03-06 |
| [REPORT_PidsSymbol_Status_Visual_Fix.md](reports/REPORT_PidsSymbol_Status_Visual_Fix.md) | [PRD](prds/REPORT_PidsSymbol_Status_Visual_Fix.md-prd.md) → [Plan](plans/REPORT_PidsSymbol_Status_Visual_Fix.md-prd-plan.md) | 2026-03-06 |
| [REPORT_SyncDevice_SensorAllTypes_And_DeviceGroup.md](reports/REPORT_SyncDevice_SensorAllTypes_And_DeviceGroup.md) | [PRD](prds/REPORT_SyncDevice_SensorAllTypes_And_DeviceGroup.md-prd.md) → [Plan](plans/REPORT_SyncDevice_SensorAllTypes_And_DeviceGroup.md-prd-plan.md) | 2026-03-06 |
| [REPORT_NatsSync_PidsIndicator_Realtime_Fix.md](reports/REPORT_NatsSync_PidsIndicator_Realtime_Fix.md) | [PRD](prds/REPORT_NatsSync_PidsIndicator_Realtime_Fix.md-prd.md) → [Plan](plans/REPORT_NatsSync_PidsIndicator_Realtime_Fix.md-prd-plan.md) | 2026-03-06 |
| [REPORT_DeviceDetailUrl_SswSvms_Format.md](reports/REPORT_DeviceDetailUrl_SswSvms_Format.md) | [PRD](prds/REPORT_DeviceDetailUrl_SswSvms_Format.md-prd.md) → [Plan](plans/REPORT_DeviceDetailUrl_SswSvms_Format.md-prd-plan.md) | 2026-03-05 |
| [REPORT_Nats_SyncDevice_Handling.md](reports/REPORT_Nats_SyncDevice_Handling.md) | [PRD](prds/REPORT_Nats_SyncDevice_Handling.md-prd.md) → [Plan](plans/REPORT_Nats_SyncDevice_Handling.md-prd-plan.md) | 2026-03-05 |
| [REPORT_Speaker_Broadcast_ContextMenu.md](reports/REPORT_Speaker_Broadcast_ContextMenu.md) | [PRD](prds/REPORT_Speaker_Broadcast_ContextMenu.md-prd.md) → [Plan](plans/REPORT_Speaker_Broadcast_ContextMenu.md-prd-plan.md) | 2026-03-05 |
| [REPORT_PidsMarker_ContextMenu_DeviceDetail.md](reports/REPORT_PidsMarker_ContextMenu_DeviceDetail.md) | [PRD](prds/REPORT_PidsMarker_ContextMenu_DeviceDetail.md-prd.md) → [Plan](plans/REPORT_PidsMarker_ContextMenu_DeviceDetail.md-prd-plan.md) | 2026-03-05 |
| [REPORT_PidsMarker_FaultBlink_Animation.md](reports/REPORT_PidsMarker_FaultBlink_Animation.md) | [PRD](prds/REPORT_PidsMarker_FaultBlink_Animation.md-prd.md) → [Plan](plans/REPORT_PidsMarker_FaultBlink_Animation.md-prd-plan.md) | 2026-03-05 |
| [REPORT_Camera_PtzStatus_Nats_Service.md](reports/REPORT_Camera_PtzStatus_Nats_Service.md) | [PRD](prds/REPORT_Camera_PtzStatus_Nats_Service.md-prd.md) → [Plan](plans/REPORT_Camera_PtzStatus_Nats_Service.md-prd-plan.md) | 2026-03-05 |
| [REPORT_GMaps_Pids_SmartSensor_Symbol.md](reports/REPORT_GMaps_Pids_SmartSensor_Symbol.md) | [PRD](prds/REPORT_GMaps_Pids_SmartSensor_Symbol.md-prd.md) → [Plan](plans/REPORT_GMaps_Pids_SmartSensor_Symbol.md-prd-plan.md) | 2026-03-05 |
| [REPORT_FaultFence_GroupSymbol_Color_Fix.md](reports/REPORT_FaultFence_GroupSymbol_Color_Fix.md) | [PRD](prds/REPORT_FaultFence_GroupSymbol_Color_Fix.md-prd.md) → [Plan](plans/REPORT_FaultFence_GroupSymbol_Color_Fix.md-prd-plan.md) | 2026-03-05 |
| [REPORT_ActionReport_Flow_Analysis.md](reports/REPORT_ActionReport_Flow_Analysis.md) | [PRD](prds/REPORT_ActionReport_Flow_Analysis.md-prd.md) → [Plan](plans/REPORT_ActionReport_Flow_Analysis.md-prd-plan.md) | 2026-03-05 |
| [REPORT_GMaps_Pids_Speaker_Symbol.md](reports/REPORT_GMaps_Pids_Speaker_Symbol.md) | [PRD](prds/REPORT_GMaps_Pids_Speaker_Symbol.md-prd.md) → [Plan](plans/REPORT_GMaps_Pids_Speaker_Symbol.md-prd-plan.md) | 2026-03-05 |
| [REPORT_Nats_MessageService_Wiring_Fix.md](reports/REPORT_Nats_MessageService_Wiring_Fix.md) | [PRD](prds/REPORT_Nats_MessageService_Wiring_Fix.md-prd.md) → [Plan](plans/REPORT_Nats_MessageService_Wiring_Fix.md-prd-plan.md) | 2026-03-05 |
| [REPORT_Pids_NatsSync_OperationState_Realtime.md](reports/REPORT_Pids_NatsSync_OperationState_Realtime.md) | [PRD](prds/REPORT_Pids_NatsSync_OperationState_Realtime.md-prd.md) → [Plan](plans/REPORT_Pids_NatsSync_OperationState_Realtime.md-prd-plan.md) | 2026-03-05 |
| [REPORT_NATS_Event_Integration.md](reports/REPORT_NATS_Event_Integration.md) | [PRD](prds/REPORT_NATS_Event_Integration.md-prd.md) → [Plan](plans/REPORT_NATS_Event_Integration.md-prd-plan.md) | 2026-03-04 |
| [REPORT_Nats_Setup_Refactoring.md](reports/REPORT_Nats_Setup_Refactoring.md) | [PRD](prds/REPORT_Nats_Setup_Refactoring.md-prd.md) → [Plan](plans/REPORT_Nats_Setup_Refactoring.md-prd-plan.md) | 2026-03-04 |
| [REPORT_GMap_Pids_Indicator_OperationState_Policy.md](reports/REPORT_GMap_Pids_Indicator_OperationState_Policy.md) | [PRD](prds/REPORT_GMap_Pids_Indicator_OperationState_Policy.md-prd.md) → [Plan](plans/REPORT_GMap_Pids_Indicator_OperationState_Policy.md-prd-plan.md) | 2026-03-04 |
| [REPORT_EventPanel_Remove_Zone_Add_ActionReported.md](reports/REPORT_EventPanel_Remove_Zone_Add_ActionReported.md) | [PRD](prds/REPORT_EventPanel_Remove_Zone_Add_ActionReported.md-prd.md) → [Plan](plans/REPORT_EventPanel_Remove_Zone_Add_ActionReported.md-prd-plan.md) | 2026-03-04 |
| [REPORT_GMap_PidsGroup_DeviceGroup_Integration.md](reports/REPORT_GMap_PidsGroup_DeviceGroup_Integration.md) | [PRD](prds/REPORT_GMap_PidsGroup_DeviceGroup_Integration.md-prd.md) → [Plan](plans/REPORT_GMap_PidsGroup_DeviceGroup_Integration.md-prd-plan.md) | 2026-03-04 |
| [REPORT_DeviceAssignDialog_MultiSelect_And_RemoveConfirm.md](reports/REPORT_DeviceAssignDialog_MultiSelect_And_RemoveConfirm.md) | [PRD](prds/REPORT_DeviceAssignDialog_MultiSelect_And_RemoveConfirm.md-prd.md) → [Plan](plans/REPORT_DeviceAssignDialog_MultiSelect_And_RemoveConfirm.md-prd-plan.md) | 2026-03-04 |
| [REPORT_DevicePanel_CRUD_And_GroupAssignment.md](reports/REPORT_DevicePanel_CRUD_And_GroupAssignment.md) | [PRD](prds/REPORT_DevicePanel_CRUD_And_GroupAssignment.md-prd.md) → [Plan](plans/REPORT_DevicePanel_CRUD_And_GroupAssignment.md-prd-plan.md) | 2026-03-04 |
| [REPORT_GMaps_Db_Safe_Enum_Parse.md](reports/REPORT_GMaps_Db_Safe_Enum_Parse.md) | [PRD](prds/REPORT_GMaps_Db_Safe_Enum_Parse.md-prd.md) → [Plan](plans/REPORT_GMaps_Db_Safe_Enum_Parse.md-prd-plan.md) | 2026-03-04 |
| [REPORT_Dashboard_Loading_Progress.md](reports/REPORT_Dashboard_Loading_Progress.md) | [PRD](prds/REPORT_Dashboard_Loading_Progress.md-prd.md) → [Plan](plans/REPORT_Dashboard_Loading_Progress.md-prd-plan.md) | 2026-03-03 |
| [REPORT_EventDashboard_Statistics_Integration.md](reports/REPORT_EventDashboard_Statistics_Integration.md) | [PRD](prds/REPORT_EventDashboard_Statistics_Integration.md-prd.md) → [Plan](plans/REPORT_EventDashboard_Statistics_Integration.md-prd-plan.md) | 2026-03-03 |
| [REPORT_DevicePanel_InfiniteScroll_Pagination.md](reports/REPORT_DevicePanel_InfiniteScroll_Pagination.md) | [PRD](prds/REPORT_DevicePanel_InfiniteScroll_Pagination.md-prd.md) → [Plan](plans/REPORT_DevicePanel_InfiniteScroll_Pagination.md-prd-plan.md) | 2026-03-03 |
| [REPORT_Tab_Switch_Info_Refresh.md](reports/REPORT_Tab_Switch_Info_Refresh.md) | [PRD](prds/REPORT_Tab_Switch_Info_Refresh.md-prd.md) → [Plan](plans/REPORT_Tab_Switch_Info_Refresh.md-prd-plan.md) | 2026-03-03 |
| [REPORT_Chart_Empty_Data_Display.md](reports/REPORT_Chart_Empty_Data_Display.md) | [PRD](prds/REPORT_Chart_Empty_Data_Display.md-prd.md) → [Plan](plans/REPORT_Chart_Empty_Data_Display.md-prd-plan.md) | 2026-03-03 |
| [REPORT_ActionEvent_Loading_Fix.md](reports/REPORT_ActionEvent_Loading_Fix.md) | [PRD](prds/REPORT_ActionEvent_Loading_Fix.md-prd.md) → [Plan](plans/REPORT_ActionEvent_Loading_Fix.md-prd-plan.md) | 2026-02-27 |
| [REPORT_Event_UnitTest_Coverage.md](reports/REPORT_Event_UnitTest_Coverage.md) | [PRD](prds/REPORT_Event_UnitTest_Coverage.md-prd.md) → [Plan](plans/REPORT_Event_UnitTest_Coverage.md-prd-plan.md) | 2026-02-27 |
| [REPORT_EventPanel_Loading_State_Fix.md](reports/REPORT_EventPanel_Loading_State_Fix.md) | [PRD](prds/REPORT_EventPanel_Loading_State_Fix.md-prd.md) → [Plan](plans/REPORT_EventPanel_Loading_State_Fix.md-prd-plan.md) | 2026-02-27 |
| [REPORT_Event_InfiniteScroll_Pagination.md](reports/REPORT_Event_InfiniteScroll_Pagination.md) | [PRD](prds/REPORT_Event_InfiniteScroll_Pagination.md-prd.md) → [Plan](plans/REPORT_Event_InfiniteScroll_Pagination.md-prd-plan.md) | 2026-02-27 |
| [REPORT_Event_DtoModel_Matching.md](reports/REPORT_Event_DtoModel_Matching.md) | [PRD](prds/REPORT_Event_DtoModel_Matching.md-prd.md) → [Plan](plans/REPORT_Event_DtoModel_Matching.md-prd-plan.md) | 2026-02-26 |
| [REPORT_Dashboard_NewDevice_Fetch_Fix.md](reports/REPORT_Dashboard_NewDevice_Fetch_Fix.md) | [PRD](prds/REPORT_Dashboard_NewDevice_Fetch_Fix.md-prd.md) → [Plan](plans/REPORT_Dashboard_NewDevice_Fetch_Fix.md-prd-plan.md) | 2026-02-26 |
| [REPORT_AllDevices_Geo_Sync_Fix.md](reports/REPORT_AllDevices_Geo_Sync_Fix.md) | [PRD](prds/REPORT_AllDevices_Geo_Sync_Fix.md-prd.md) → [Plan](plans/REPORT_AllDevices_Geo_Sync_Fix.md-prd-plan.md) | 2026-02-26 |
| [REPORT_Sensor_UpdateProperties_Geo_Fix.md](reports/REPORT_Sensor_UpdateProperties_Geo_Fix.md) | [PRD](prds/REPORT_Sensor_UpdateProperties_Geo_Fix.md-prd.md) → [Plan](plans/REPORT_Sensor_UpdateProperties_Geo_Fix.md-prd-plan.md) | 2026-02-26 |
| [REPORT_Sensor_DeviceEquals_Fix.md](reports/REPORT_Sensor_DeviceEquals_Fix.md) | [PRD](prds/REPORT_Sensor_DeviceEquals_Fix.md-prd.md) → [Plan](plans/REPORT_Sensor_DeviceEquals_Fix.md-prd-plan.md) | 2026-02-26 |
| [REPORT_Camera_Dto_Mapping_Fix.md](reports/REPORT_Camera_Dto_Mapping_Fix.md) | [PRD](prds/REPORT_Camera_Dto_Mapping_Fix.md-prd.md) → [Plan](plans/REPORT_Camera_Dto_Mapping_Fix.md-prd-plan.md) | 2026-02-26 |
| [REPORT_Camera_IsRecord_Editable.md](reports/REPORT_Camera_IsRecord_Editable.md) | [PRD](prds/REPORT_Camera_IsRecord_Editable.md-prd.md) → [Plan](plans/REPORT_Camera_IsRecord_Editable.md-prd-plan.md) | 2026-02-26 |
| [REPORT_Camera_Model_Cleanup_And_DetailView.md](reports/REPORT_Camera_Model_Cleanup_And_DetailView.md) | [PRD](prds/REPORT_Camera_Model_Cleanup_And_DetailView.md-prd.md) → [Plan](plans/REPORT_Camera_Model_Cleanup_And_DetailView.md-prd-plan.md) | 2026-02-25 |
| [REPORT_DevicePanel_CRUD_Completion.md](reports/REPORT_DevicePanel_CRUD_Completion.md) | [PRD](prds/REPORT_DevicePanel_CRUD_Completion.md-prd.md) → [Plan](plans/REPORT_DevicePanel_CRUD_Completion.md-prd-plan.md) | 2026-02-25 |
| [REPORT_DeviceGroup_Ui.md](reports/REPORT_DeviceGroup_Ui.md) | [PRD](prds/REPORT_DeviceGroup_Ui.md-prd.md) → [Plan](plans/REPORT_DeviceGroup_Ui.md-prd-plan.md) | 2026-02-25 |
| [REPORT_EventUi_DeviceProperty_Binding.md](reports/REPORT_EventUi_DeviceProperty_Binding.md) | [PRD](prds/REPORT_EventUi_DeviceProperty_Binding.md-prd.md) → [Plan](plans/REPORT_EventUi_DeviceProperty_Binding.md-prd-plan.md) | 2026-02-25 |
| [REPORT_EventApi_IntegrationTest.md](reports/REPORT_EventApi_IntegrationTest.md) | [PRD](prds/REPORT_EventApi_IntegrationTest.md-prd.md) → [Plan](plans/REPORT_EventApi_IntegrationTest.md-prd-plan.md) | 2026-02-25 |
| [REPORT_CameraPreset_ROI_Point_Api.md](reports/REPORT_CameraPreset_ROI_Point_Api.md) | [PRD](prds/REPORT_CameraPreset_ROI_Point_Api.md-prd.md) → [Plan](plans/REPORT_CameraPreset_ROI_Point_Api.md-prd-plan.md) | 2026-02-24 |
| [REPORT_ServerApi_IntegrationTest.md](reports/REPORT_ServerApi_IntegrationTest.md) | [PRD](prds/REPORT_ServerApi_IntegrationTest.md-prd.md) → [Plan](plans/REPORT_ServerApi_IntegrationTest.md-prd-plan.md) | 2026-02-24 |
| [REPORT_DeviceApi_IntegrationTest.md](reports/REPORT_DeviceApi_IntegrationTest.md) | [PRD](prds/REPORT_DeviceApi_IntegrationTest.md-prd.md) → [Plan](plans/REPORT_DeviceApi_IntegrationTest.md-prd-plan.md) | 2026-02-24 |

<!-- auto-section-end -->
