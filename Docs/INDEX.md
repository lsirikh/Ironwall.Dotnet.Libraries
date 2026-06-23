<!-- auto-section-start -->
# 프로젝트 문서 인덱스

- **마지막 갱신**: 2026-06-21 (Accounts_Ui_Library_Extraction PRD 추가 — 1,050라인, 24시나리오, 2회시뮬레이션)
- **총 문서 수**: 216개

---

## 스토리보드 (Docs/storyboards/)

| 파일 | 내용 | 날짜 |
|------|------|------|
| [Account_GOP_Integration_Storyboard.html](storyboards/Account_GOP_Integration_Storyboard.html) | GOP Account 연동 인터랙티브 스토리보드 v2.0 — WPF ShellView 기반 와이어프레임 (LoginGateOverlay·NATS Gate·MaterialDesignDataGrid·1차 인터랙션, 7화면) | 2026-06-21 |
| [DevicePanel_EventPanel_Storyboard.html](storyboards/DevicePanel_EventPanel_Storyboard.html) | Device/Event 패널 스토리보드 | 2026-06-19 |

---

## 분석 (docs/analysis/)

| 파일 | 분석 대상 | 날짜 |
|------|---------|------|
| [Client_API_Conformance_Audit-analysis.md](analysis/Client_API_Conformance_Audit-analysis.md) | Client_API_Conformance_Audit | 2026-06-19 |
| [GOP_API_v4_Changes_ClientImpact-analysis.md](analysis/GOP_API_v4_Changes_ClientImpact-analysis.md) | GOP_API_v4_Changes_ClientImpact | 2026-06-19 |
| [OverlayImage_Rotation_Zoom_AABB_RootCause-analysis.md](analysis/OverlayImage_Rotation_Zoom_AABB_RootCause-analysis.md) | OverlayImage_Rotation_Zoom_AABB_RootCause | 2026-06-12 |
| [RapidEventBurst_Stutter_Analysis.md](analysis/RapidEventBurst_Stutter_Analysis.md) | RapidEventBurst_Stutter_Analysis.md | 2026-06-04 |
| [Multisensor_Symbol_Bug_Analysis.md](analysis/Multisensor_Symbol_Bug_Analysis.md) | Multisensor_Symbol_Bug_Analysis.md | 2026-06-04 |
| [EventStateSyncArchitecture_Analysis.md](analysis/EventStateSyncArchitecture_Analysis.md) | EventStateSyncArchitecture_Analysis.md | 2026-06-04 |
| [BatchReport_SymbolLeak_Analysis.md](analysis/BatchReport_SymbolLeak_Analysis.md) | BatchReport_SymbolLeak_Analysis.md | 2026-06-04 |
| [OverlayMap-Performance-Analysis.md](analysis/OverlayMap-Performance-Analysis.md) | OverlayMap-Performance-Analysis.md | 2026-05-27 |
| [EVENT_PROCESS_VISUALIZATION.md](analysis/EVENT_PROCESS_VISUALIZATION.md) | EVENT_PROCESS_VISUALIZATION.md | 2026-05-22 |
| [ANALYSIS_Skillset_Issues_And_Improvements.md](analysis/ANALYSIS_Skillset_Issues_And_Improvements.md) | ANALYSIS_Skillset_Issues_And_Improvements.md | 2026-05-19 |
| [ANALYSIS_View_Architecture.md](analysis/ANALYSIS_View_Architecture.md) | ANALYSIS_View_Architecture.md | 2026-05-18 |
| [ANALYSIS_Detection_Action_Process_Flow.md](analysis/ANALYSIS_Detection_Action_Process_Flow.md) | ANALYSIS_Detection_Action_Process_Flow.md | 2026-05-18 |
| [NATS_Detection_Redis_Flow.md](analysis/NATS_Detection_Redis_Flow.md) | NATS_Detection_Redis_Flow.md | 2026-05-15 |
| [ANALYSIS_GatewayEvent_Group_NtoN_Migration.md](analysis/ANALYSIS_GatewayEvent_Group_NtoN_Migration.md) | ANALYSIS_GatewayEvent_Group_NtoN_Migration.md | 2026-05-15 |

## 요구사항 정의서 (docs/prds/)

| 파일 | 내용 | 상태 | 날짜 |
|------|------|------|------|
| [CameraPopup_Snapshot_UX-prd.md](prds/CameraPopup_Snapshot_UX-prd.md) | 카메라 팝업 스냅샷 UX · 저장폴더 설정화(EventSetupView 옵션+찾아보기, appsettings/SetupModel/StreamingSetupModel SnapshotPath) + 폴더 자동생성(기존엔 폴더없으면 저장실패) · 플래시 효과(흰 번쩍) · OSD "스냅샷 저장" 우상단 1초 · **구현·커밋(lib 7b3e984, 메인 fe276ee) 빌드0** ※앱 닫고 재빌드 검증 (Track C) | 구현완료 | 2026-06-23 |
| [CameraPopup_Streaming_Settings-prd.md](prds/CameraPopup_Streaming_Settings-prd.md) | 카메라 팝업 설정 연동(EventSetupView↔맵) · SetupModel:IStreamingSetupModel 인터페이스 주입(IGMapSetupModel 패턴) · 더블클릭 게이팅(IsCameraPopupUsed) · 자동해제 타이머(IsAutoDiscard/TimeoutSeconds·상호작용 리셋) · 카메라심볼↔팝업 연결선(Leader Line·팬/줌/드래그 추종) · **구현·커밋(lib 04005a2/b57de1a/fb6ea46, 메인 3dcc6fe) code-review H-1/H-2/M-3 반영·빌드0** ※앱 닫고 재빌드 후 런타임 검증 (Track C) | 구현완료 | 2026-06-23 |
| [Rtsp_Map_Popup-prd.md](prds/Rtsp_Map_Popup-prd.md) | 맵 카메라 더블클릭→Geo앵커 이동식 RTSP 팝업(위치기억) · 참조 Dotnet.Rtsp.Viewer.Ui LibVLCSharp Streaming 이식 · 관심지역/레이어 창 답습(384×300, Hub WriteableBitmap airspace 회피) · **위치영속=DB(다중클라 공유)** · 스토리보드+와이어프레임 · FR12/리스크9 · v1.1 미결4건 확정(DB/카메라Id/포커스/Hub) · 5영역 워크플로우+architect(opus) · **구현·머지 완료(lib `c9fcd8d` v2.6 / 메인솔루션 `1ee7ae8` v0.5) 8/8단계, 빌드0·48테스트·code-review H-1수정·네이티브배포** ※런타임 검증 대기 (Track C) | 구현완료 | 2026-06-23 |
| [EnclosureThresholdDialog-prd.md](prds/EnclosureThresholdDialog-prd.md) | 함체 임계값(온/습도·진동) 설정 다이얼로그 · 카메라 상세 패턴 복제(양 repo) · threshold_config 매핑 보강(양방향 드롭 해소)+M1 가드 · **구현·머지(5616dd3/ea4eb68/bd612bd, review C/H 0)** | 구현완료 | 2026-06-22 |
| [BaseMap_NoData_DefaultTile-prd.md](prds/BaseMap_NoData_DefaultTile-prd.md) | MBTiles 베이스맵 커버리지 밖 흰 화면에 "깔끔/모던" 기본 타일(격자 타일링) · DefaultTileBytes+GetTileImage 분기(c)+DefaultTileImageFactory · xUnit 41통과 · **구현·머지(a8d968b)** + v1.1 각 타일 중앙 센서웨이 로고(`cead507`) ※GMap.NET 고아 서브모듈=Core 1파일 git외+수동백업 (Track C) | 구현완료 | 2026-06-22 |
| [SpeakerServerAssignment-prd.md](prds/SpeakerServerAssignment-prd.md) | 스피커 방송서버(server_id) 배정 · 12-Agent opus 시뮬레이션(5블로커/1High) · 매핑 비대칭(write server_id↔read nested) · ServerProvider 신설 · 해제없음+첫서버 자동배정 (Track C) · **구현·머지 완료(0913360)** | 구현완료 | 2026-06-22 |
| [DevicePropertyPanel_Layout_Redesign-prd.md](prds/DevicePropertyPanel_Layout_Redesign-prd.md) | 6패널 속성 4구역 레이아웃+스크롤+Bearing/Alt 왕복 · 6차원 시뮬레이션 · **구현·머지 완료(c92344a)** | 구현완료 | 2026-06-22 |
| [GOP_Account_Auth_Integration-prd.md](prds/GOP_Account_Auth_Integration-prd.md) | GOP REST API v4.6 Account/Auth JWT 연동 · PRD-GOP-00 (보완: SUP-C1~L1 12건 추가) | Draft | 2026-06-20 |
| [GOP_Permission_Gate_Feature-prd.md](prds/GOP_Permission_Gate_Feature-prd.md) | PRD-GOP-01: IPermissionService + MinRoleConverter + ConductorControlViewModel 3중 방어선 (P1, 18 STEP) | Draft | 2026-06-20 |
| [GOP_AccountManager_UI-prd.md](prds/GOP_AccountManager_UI-prd.md) | PRD-GOP-02: AccountManager/Register/Editor/Delete→GOP API 전환 · 하드코딩 비밀번호 CRITICAL 해소 (P1, 26 STEP) | Draft | 2026-06-20 |
| [GOP_MyPage_UI-prd.md](prds/GOP_MyPage_UI-prd.md) | PRD-GOP-03: MyPage 자기정보 GOP 전환 · role덮어쓰기 방지 · 세션관리 섹션 신규 (P1, 18 STEP) | Draft | 2026-06-20 |
| [GOP_Menu_Role_Visibility-prd.md](prds/GOP_Menu_Role_Visibility-prd.md) | PRD-GOP-04: LeftMenu 5단계 role 가시성 · Label 오타 3건 · Tag 우회 차단 (P2, 12 STEP) | Draft | 2026-06-20 |
| [GOP_UserSession_AuditLog_UI-prd.md](prds/GOP_UserSession_AuditLog_UI-prd.md) | PRD-GOP-05: 세션모니터/그룹관리/감사로그/설정변경이력 신규 UI (P2, 28 STEP) | Draft | 2026-06-20 |
| [Accounts_Ui_Library_Extraction-prd.md](prds/Accounts_Ui_Library_Extraction-prd.md) | Accounts.Ui 신규 라이브러리 구축 + VM/View 이관 전략 (Track C, 1,050라인, 24시나리오, 2회시뮬레이션, 2 CRITICAL / 6 HIGH 이슈 확정) | Draft | 2026-06-21 |
| [GOP_PreAuth_Overlay_NatsGate-prd.md](prds/GOP_PreAuth_Overlay_NatsGate-prd.md) | PRD-GOP-07: 미인증 LoginGateOverlay + EventCardPanel 숨김 + NATS IsLogin 게이팅 (P1, 14 STEP) | Draft | 2026-06-21 |
| [GOP_Session_Resilience_Lifecycle-prd.md](prds/GOP_Session_Resilience_Lifecycle-prd.md) | PRD-GOP-06: 앱재시작복원·선제Refresh·강제로그아웃·지수백오프·OnExit통보 (P2, 20 STEP) | Draft | 2026-06-20 |
| [Client_API_v46_Conformance-prd.md](prds/Client_API_v46_Conformance-prd.md) | Client_API_v46_Conformance | Approved | 2026-06-19 |
| [NATS-Tracking-Geolocation-메시지정리.md](prds/NATS-Tracking-Geolocation-메시지정리.md) | NATS-Tracking-Geolocation-메시지정리.md | Draft | 2026-06-19 |
| [DevicePanel_CRUD_API_Sync-prd.md](prds/DevicePanel_CRUD_API_Sync-prd.md) | DevicePanel_CRUD_API_Sync | Draft | 2026-06-17 |
| [EventProcess_ContaminationFix-prd.md](prds/EventProcess_ContaminationFix-prd.md) | EventProcess_ContaminationFix | Draft | 2026-06-15 |
| [GridSnap_System-prd.md](prds/GridSnap_System-prd.md) | GridSnap_System | Approved | 2026-06-15 |
| [DigitalZoom_RenderTransform-prd.md](prds/DigitalZoom_RenderTransform-prd.md) | DigitalZoom_RenderTransform | Draft | 2026-06-15 |
| [GMap_Zoom_Improvements-prd.md](prds/GMap_Zoom_Improvements-prd.md) | GMap_Zoom_Improvements | Approved | 2026-06-12 |
| [MarkerHitTest_AABB_Fix-prd.md](prds/MarkerHitTest_AABB_Fix-prd.md) | MarkerHitTest_AABB_Fix | Completed | 2026-06-10 |
| [OverlayImage_Rotation_Editing-prd.md](prds/OverlayImage_Rotation_Editing-prd.md) | OverlayImage_Rotation_Editing | Approved | 2026-06-10 |
| [ContextMenu_DisplayRules-prd.md](prds/ContextMenu_DisplayRules-prd.md) | ContextMenu_DisplayRules | Approved | 2026-06-10 |
| [OverlayImage_ZOrder_Independence-prd.md](prds/OverlayImage_ZOrder_Independence-prd.md) | OverlayImage_ZOrder_Independence | Approved | 2026-06-10 |
| [ZOrder_PropertyPanel_Integration-prd.md](prds/ZOrder_PropertyPanel_Integration-prd.md) | ZOrder_PropertyPanel_Integration | Draft | 2026-06-10 |
| [LayerVisibility_Persistence_Fix-prd.md](prds/LayerVisibility_Persistence_Fix-prd.md) | LayerVisibility_Persistence_Fix | Approved | 2026-06-08 |
| [DeviceApi_ProviderPropagation_Fix-prd.md](prds/DeviceApi_ProviderPropagation_Fix-prd.md) | DeviceApi_ProviderPropagation_Fix | Draft | 2026-06-08 |
| [SplashScreen_MonitoringSolution-prd.md](prds/SplashScreen_MonitoringSolution-prd.md) | SplashScreen_MonitoringSolution | Draft | 2026-06-08 |
| [SplashScreen_LibraryComponent-prd.md](prds/SplashScreen_LibraryComponent-prd.md) | SplashScreen_LibraryComponent | Draft | 2026-06-08 |
| [SymbolTextSeparation_LabelPositioning-prd.md](prds/SymbolTextSeparation_LabelPositioning-prd.md) | SymbolTextSeparation_LabelPositioning | Approved | 2026-06-05 |
| [WebServer_Enable_Feature-prd.md](prds/WebServer_Enable_Feature-prd.md) | WebServer_Enable_Feature | Completed | 2026-06-05 |
| [MapSetup_Panel_Refactor-prd.md](prds/MapSetup_Panel_Refactor-prd.md) | MapSetup_Panel_Refactor | Approved | 2026-06-05 |
| [SettingPanel_BrokerLabel_Rename-prd.md](prds/SettingPanel_BrokerLabel_Rename-prd.md) | SettingPanel_BrokerLabel_Rename | Approved | 2026-06-05 |
| [RemoteDesktop_PanFollowBug_Fix-prd.md](prds/RemoteDesktop_PanFollowBug_Fix-prd.md) | RemoteDesktop_PanFollowBug_Fix | Approved | 2026-06-04 |
| [MapSymbol_DispatcherFreeze_And_LogNoise_Fix-prd.md](prds/MapSymbol_DispatcherFreeze_And_LogNoise_Fix-prd.md) | MapSymbol_DispatcherFreeze_And_LogNoise_Fix | Superseded | 2026-06-04 |
| [SymbolUpdate_DispatcherFreeze_Fix-prd.md](prds/SymbolUpdate_DispatcherFreeze_Fix-prd.md) | SymbolUpdate_DispatcherFreeze_Fix | Completed | 2026-06-04 |
| [Multisensor_Symbol_Fix-prd.md](prds/Multisensor_Symbol_Fix-prd.md) | Multisensor_Symbol_Fix | Completed | 2026-06-04 |
| [BatchReport_SymbolRestore_Fix-prd.md](prds/BatchReport_SymbolRestore_Fix-prd.md) | BatchReport_SymbolRestore_Fix | Approved | 2026-06-04 |
| [EventCardPerformance-prd.md](prds/EventCardPerformance-prd.md) | EventCardPerformance | Draft | 2026-06-04 |
| [OverlayMap_MBTiles_Provider-prd.md](prds/OverlayMap_MBTiles_Provider-prd.md) | OverlayMap_MBTiles_Provider | Approved | 2026-06-02 |
| [RedisDomainService_DoubleStop_Fix-prd.md](prds/RedisDomainService_DoubleStop_Fix-prd.md) | RedisDomainService_DoubleStop_Fix | Approved | 2026-06-01 |
| [NatsShutdown_SubscriptionHang_Fix-prd.md](prds/NatsShutdown_SubscriptionHang_Fix-prd.md) | NatsShutdown_SubscriptionHang_Fix | Approved | 2026-06-01 |
| [AppShutdown_Blocking_Fix-prd.md](prds/AppShutdown_Blocking_Fix-prd.md) | AppShutdown_Blocking_Fix | Approved | 2026-06-01 |
| [DetectionPulse_Ripple_Enlargement-prd.md](prds/DetectionPulse_Ripple_Enlargement-prd.md) | DetectionPulse_Ripple_Enlargement | Approved | 2026-05-28 |
| [PRD_SplashScreen_Startup_Gating.md](prds/PRD_SplashScreen_Startup_Gating.md) | PRD_SplashScreen_Startup_Gating.md | Draft | 2026-05-27 |
| [SymbolUpdate_Threading_And_LeakFix-prd.md](prds/SymbolUpdate_Threading_And_LeakFix-prd.md) | SymbolUpdate_Threading_And_LeakFix | Draft | 2026-05-27 |
| [OverlayMap_Performance_Optimization-prd.md](prds/OverlayMap_Performance_Optimization-prd.md) | OverlayMap_Performance_Optimization | Draft | 2026-05-27 |
| [MalfunctionCard_ControllerNumber_BindingFix-prd.md](prds/MalfunctionCard_ControllerNumber_BindingFix-prd.md) | MalfunctionCard_ControllerNumber_BindingFix | Approved | 2026-05-27 |
| [MapSymbol_PulseAnimation_Performance_Fix-prd.md](prds/MapSymbol_PulseAnimation_Performance_Fix-prd.md) | MapSymbol_PulseAnimation_Performance_Fix | Approved | 2026-05-26 |
| [Event_Performance_Optimization-prd.md](prds/Event_Performance_Optimization-prd.md) | Event_Performance_Optimization | Draft | 2026-05-22 |
| [AutoActionReport_DualPath_Fix-prd.md](prds/AutoActionReport_DualPath_Fix-prd.md) | AutoActionReport_DualPath_Fix | Draft | 2026-05-22 |
| [BatchReport_Sound_Stop_Fix-prd.md](prds/BatchReport_Sound_Stop_Fix-prd.md) | BatchReport_Sound_Stop_Fix | Draft | 2026-05-21 |
| [PRD_PidsSymbol_Transparency_Blink.md](prds/PRD_PidsSymbol_Transparency_Blink.md) | PRD_PidsSymbol_Transparency_Blink.md | Draft | 2026-05-20 |
| [SoundTypeSwitch_ImmediateStop_Fix-prd.md](prds/SoundTypeSwitch_ImmediateStop_Fix-prd.md) | SoundTypeSwitch_ImmediateStop_Fix | Draft | 2026-05-20 |
| [Device_CompositeState_SSOT_And_FaultAutoRecovery-prd.md](prds/Device_CompositeState_SSOT_And_FaultAutoRecovery-prd.md) | Device_CompositeState_SSOT_And_FaultAutoRecovery | Draft | 2026-05-19 |
| [FenceGroup_Blink_And_Sound_DualPlay_Fix-prd.md](prds/FenceGroup_Blink_And_Sound_DualPlay_Fix-prd.md) | FenceGroup_Blink_And_Sound_DualPlay_Fix | Draft | 2026-05-19 |
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
| [Client_API_v46_Conformance-prd-plan.md](plans/Client_API_v46_Conformance-prd-plan.md) | [PRD](prds/Client_API_v46_Conformance-prd.md) | 0/0 | 2026-06-19 |
| [EventProcess_ContaminationFix-prd-plan.md](plans/EventProcess_ContaminationFix-prd-plan.md) | [PRD](prds/EventProcess_ContaminationFix-prd.md) | 0/14 | 2026-06-15 |
| [GridSnap_System-prd-plan.md](plans/GridSnap_System-prd-plan.md) | [PRD](prds/GridSnap_System-prd.md) | 11/32 | 2026-06-15 |
| [DigitalZoom_RenderTransform-prd-plan.md](plans/DigitalZoom_RenderTransform-prd-plan.md) | [PRD](prds/DigitalZoom_RenderTransform-prd.md) | 0/12 | 2026-06-15 |
| [GMap_Zoom_Improvements-prd-plan.md](plans/GMap_Zoom_Improvements-prd-plan.md) | [PRD](prds/GMap_Zoom_Improvements-prd.md) | 0/5 | 2026-06-12 |
| [OverlayImage_Rotation_Editing-prd-plan.md](plans/OverlayImage_Rotation_Editing-prd-plan.md) | [PRD](prds/OverlayImage_Rotation_Editing-prd.md) | 0/33 | 2026-06-10 |
| [OverlayImage_ZOrder_Independence-plan.md](plans/OverlayImage_ZOrder_Independence-plan.md) | [PRD](prds/OverlayImage_ZOrder_Independence-prd.md) | 0/25 | 2026-06-10 |
| [ZOrder_PropertyPanel_Integration-prd-plan.md](plans/ZOrder_PropertyPanel_Integration-prd-plan.md) | [PRD](prds/ZOrder_PropertyPanel_Integration-prd.md) | 0/0 | 2026-06-10 |
| [LayerVisibility_Persistence_Fix-prd-plan.md](plans/LayerVisibility_Persistence_Fix-prd-plan.md) | [PRD](prds/LayerVisibility_Persistence_Fix-prd.md) | 3/14 | 2026-06-08 |
| [SplashScreen_MonitoringSolution-prd-plan.md](plans/SplashScreen_MonitoringSolution-prd-plan.md) | [PRD](prds/SplashScreen_MonitoringSolution-prd.md) | 0/0 | 2026-06-08 |
| [SymbolTextSeparation_LabelPositioning-prd-plan.md](plans/SymbolTextSeparation_LabelPositioning-prd-plan.md) | [PRD](prds/SymbolTextSeparation_LabelPositioning-prd.md) | 20/38 | 2026-06-05 |
| [RemoteDesktop_PanFollowBug_Fix-prd-plan.md](plans/RemoteDesktop_PanFollowBug_Fix-prd-plan.md) | [PRD](prds/RemoteDesktop_PanFollowBug_Fix-prd.md) | 8/16 | 2026-06-04 |
| [MapSymbol_DispatcherFreeze_And_LogNoise_Fix-prd-plan.md](plans/MapSymbol_DispatcherFreeze_And_LogNoise_Fix-prd-plan.md) | [PRD](prds/MapSymbol_DispatcherFreeze_And_LogNoise_Fix-prd.md) | 7/15 | 2026-06-04 |
| [SymbolUpdate_DispatcherFreeze_Fix-prd-plan.md](plans/SymbolUpdate_DispatcherFreeze_Fix-prd-plan.md) | [PRD](prds/SymbolUpdate_DispatcherFreeze_Fix-prd.md) | 20/20 | 2026-06-04 |
| [Multisensor_Symbol_Fix-prd-plan.md](plans/Multisensor_Symbol_Fix-prd-plan.md) | [PRD](prds/Multisensor_Symbol_Fix-prd.md) | 20/20 | 2026-06-04 |
| [EventCardPerformance-prd-plan.md](plans/EventCardPerformance-prd-plan.md) | [PRD](prds/EventCardPerformance-prd.md) | 16/29 | 2026-06-04 |
| [OverlayMap_MBTiles_Provider-prd-plan.md](plans/OverlayMap_MBTiles_Provider-prd-plan.md) | [PRD](prds/OverlayMap_MBTiles_Provider-prd.md) | 22/23 | 2026-06-02 |
| [DetectionPulse_Ripple_Enlargement-prd-plan.md](plans/DetectionPulse_Ripple_Enlargement-prd-plan.md) | [PRD](prds/DetectionPulse_Ripple_Enlargement-prd.md) | 9/9 | 2026-06-02 |
| [RedisDomainService_DoubleStop_Fix-prd-plan.md](plans/RedisDomainService_DoubleStop_Fix-prd-plan.md) | [PRD](prds/RedisDomainService_DoubleStop_Fix-prd.md) | 2/3 | 2026-06-01 |
| [NatsShutdown_SubscriptionHang_Fix-prd-plan.md](plans/NatsShutdown_SubscriptionHang_Fix-prd-plan.md) | [PRD](prds/NatsShutdown_SubscriptionHang_Fix-prd.md) | 0/8 | 2026-06-01 |
| [AppShutdown_Blocking_Fix-prd-plan.md](plans/AppShutdown_Blocking_Fix-prd-plan.md) | [PRD](prds/AppShutdown_Blocking_Fix-prd.md) | 4/7 | 2026-06-01 |
| [OverlayMap_Performance_Optimization-prd-plan.md](plans/OverlayMap_Performance_Optimization-prd-plan.md) | [PRD](prds/OverlayMap_Performance_Optimization-prd.md) | 56/67 | 2026-05-27 |
| [PRD_SplashScreen_Startup_Gating-prd-plan.md](plans/PRD_SplashScreen_Startup_Gating-prd-plan.md) | [PRD](prds/PRD_SplashScreen_Startup_Gating-prd.md) | 22/36 | 2026-05-27 |
| [MalfunctionCard_ControllerNumber_BindingFix-prd-plan.md](plans/MalfunctionCard_ControllerNumber_BindingFix-prd-plan.md) | [PRD](prds/MalfunctionCard_ControllerNumber_BindingFix-prd.md) | 4/8 | 2026-05-27 |
| [MapSymbol_PulseAnimation_Performance_Fix-prd-plan.md](plans/MapSymbol_PulseAnimation_Performance_Fix-prd-plan.md) | [PRD](prds/MapSymbol_PulseAnimation_Performance_Fix-prd.md) | 20/24 | 2026-05-26 |
| [Event_Performance_Optimization-prd-plan.md](plans/Event_Performance_Optimization-prd-plan.md) | [PRD](prds/Event_Performance_Optimization-prd.md) | 49/60 | 2026-05-22 |
| [SoundTypeSwitch_ImmediateStop_Fix-plan.md](plans/SoundTypeSwitch_ImmediateStop_Fix-plan.md) | [PRD](prds/SoundTypeSwitch_ImmediateStop_Fix-prd.md) | 0/0 | 2026-05-20 |
| [Device_CompositeState_SSOT_And_FaultAutoRecovery-plan.md](plans/Device_CompositeState_SSOT_And_FaultAutoRecovery-plan.md) | [PRD](prds/Device_CompositeState_SSOT_And_FaultAutoRecovery-prd.md) | 0/0 | 2026-05-19 |
| [FenceGroup_Blink_And_Sound_DualPlay_Fix-plan.md](plans/FenceGroup_Blink_And_Sound_DualPlay_Fix-plan.md) | [PRD](prds/FenceGroup_Blink_And_Sound_DualPlay_Fix-prd.md) | 0/9 | 2026-05-19 |
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
| [Client_API_v46_Conformance_Phase0-report.md](reports/Client_API_v46_Conformance_Phase0-report.md) | [PRD](prds/Client_API_v46_Conformance_Phase0-prd.md) → [Plan](plans/Client_API_v46_Conformance_Phase0-prd-plan.md) | 2026-06-19 |
| [DigitalZoom_RenderTransform-report.md](reports/DigitalZoom_RenderTransform-report.md) | [PRD](prds/DigitalZoom_RenderTransform-prd.md) → [Plan](plans/DigitalZoom_RenderTransform-prd-plan.md) | 2026-06-15 |
| [WebServer_Enable_Feature-report.md](reports/WebServer_Enable_Feature-report.md) | [PRD](prds/WebServer_Enable_Feature-prd.md) → [Plan](plans/WebServer_Enable_Feature-prd-plan.md) | 2026-06-05 |
| [SymbolUpdate_DispatcherFreeze_Fix-report.md](reports/SymbolUpdate_DispatcherFreeze_Fix-report.md) | [PRD](prds/SymbolUpdate_DispatcherFreeze_Fix-prd.md) → [Plan](plans/SymbolUpdate_DispatcherFreeze_Fix-prd-plan.md) | 2026-06-04 |
| [OverlayMap_MBTiles_Provider-report.md](reports/OverlayMap_MBTiles_Provider-report.md) | [PRD](prds/OverlayMap_MBTiles_Provider-prd.md) → [Plan](plans/OverlayMap_MBTiles_Provider-prd-plan.md) | 2026-06-02 |
| [Device_CompositeState_SSOT_And_FaultAutoRecovery-report.md](reports/Device_CompositeState_SSOT_And_FaultAutoRecovery-report.md) | [PRD](prds/Device_CompositeState_SSOT_And_FaultAutoRecovery-prd.md) → [Plan](plans/Device_CompositeState_SSOT_And_FaultAutoRecovery-prd-plan.md) | 2026-05-20 |
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
