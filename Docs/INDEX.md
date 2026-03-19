# Project Index

> Last updated: 2026-03-13

## 현재 컨텍스트
| 파일 | 주제 | 날짜 |
|------|------|------|
| [2026-03-18_GMap_UI_Renewal_ROI_Broadcast_OfflineMap.md](context/2026-03-18_GMap_UI_Renewal_ROI_Broadcast_OfflineMap.md) | GMap UI 리뉴얼 + ROI + 방송패널 + 오프라인맵전략 (커밋 3건, PRD 4개) | 2026-03-18 |
| [2026-03-13_EventQueue_Symbol_Unification_Complete.md](context/2026-03-13_EventQueue_Symbol_Unification_Complete.md) | EventQueue 심볼 상태 일원화 완료 (13 Phases, 28/28, 31 tests) | 2026-03-13 |
| [2026-03-12_PIDS_Symbol_Status_Verification.md](context/2026-03-12_PIDS_Symbol_Status_Verification.md) | PIDS 심볼 상태 검증 (SYNC_DEVICE 3상태 매핑 확인, 타PC 오류 진단 로깅) | 2026-03-12 |
| [2026-03-08_Event_BatchUI_Performance_Complete.md](context/2026-03-08_Event_BatchUI_Performance_Complete.md) | 이벤트 배치 UI 성능 최적화 완료 (22/22, 32 tests) | 2026-03-08 |
| [2026-03-08_Event_Pipeline_Redesign_Complete.md](context/2026-03-08_Event_Pipeline_Redesign_Complete.md) | EventQueue 기반 이벤트 파이프라인 재설계 완료 (33/33, 23 tests) | 2026-03-08 |
| [2026-03-08_Pre_EventPipeline_Redesign_Snapshot.md](context/2026-03-08_Pre_EventPipeline_Redesign_Snapshot.md) | EventPipeline 재설계 전 코드베이스 스냅샷 (롤백 기준점) | 2026-03-08 |
| [2026-03-07_SymbolVisual_SyncDevice_Unification.md](context/2026-03-07_SymbolVisual_SyncDevice_Unification.md) | 심볼 비주얼 SyncFromDevice 단일 경로 일원화 | 2026-03-07 |
| [2026-03-05_ActionReport_Fix_And_SyncDevice_Handling.md](context/2026-03-05_ActionReport_Fix_And_SyncDevice_Handling.md) | 조치보고 HTTP 422/NPE 수정 + SYNC_DEVICE NATS 처리 구현 | 2026-03-05 |

## 진행중
| PRD | Plan | 설명 |
|-----|------|------|
| [PRD_EventCard_EntryId_Connection.md](prd/PRD_EventCard_EntryId_Connection.md) | [plan](prd/PRD_EventCard_EntryId_Connection.plan.md) | 이벤트 카드 ↔ EventQueue entryId 연결 (조치보고 심볼 복원 버그 수정) |
| [PRD_Pids_Symbol_Background.md](prd/PRD_Pids_Symbol_Background.md) | [plan](prd/PRD_Pids_Symbol_Background.plan.md) | PIDS 심볼 아이콘 반투명 배경 추가 (가시성 개선) |
| [PRD_DevicePanel_RealtimeStatus_NatsSync.md](prd/PRD_DevicePanel_RealtimeStatus_NatsSync.md) | [plan](prd/PRD_DevicePanel_RealtimeStatus_NatsSync.plan.md) | DevicePanel 실시간 상태 갱신 (NATS SYNC_DEVICE / SYNC_DEVICE_GROUP) |
| [PRD_DetectionEvent_Symbol_Visual_Restore.md](prd/PRD_DetectionEvent_Symbol_Visual_Restore.md) | [plan](prd/PRD_DetectionEvent_Symbol_Visual_Restore.plan.md) | 탐지 이벤트 NATS 수신 → 심볼 Detecting 비주얼 복원 |
| [PRD_SetupPanel_AppsettingsSync_Fix.md](prd/PRD_SetupPanel_AppsettingsSync_Fix.md) | [plan](prd/PRD_SetupPanel_AppsettingsSync_Fix.plan.md) | SetupPanel 경로 불일치 수정 + WebServer 설정 패널 추가 |
| [PRD_Gateway_DeviceGroup_Migration.md](prd/PRD_Gateway_DeviceGroup_Migration.md) | [plan](prd/PRD_Gateway_DeviceGroup_Migration.plan.md) | Gateway 이벤트 그룹 DeviceGroup 전환 (int → ComboBox) |
| [PRD_DeviceGroupSelection_ProgressCircle.md](prd/PRD_DeviceGroupSelection_ProgressCircle.md) | [plan](prd/PRD_DeviceGroupSelection_ProgressCircle.plan.md) | DeviceGroupSelectionView ProgressCircle 적용 |
| [PRD_ConductorControl_Rendering_Optimization.md](prd/PRD_ConductorControl_Rendering_Optimization.md) | - | ConductorControl 렌더링 성능 최적화 (α합성 감소, 애니메이션 단축) |
| [PRD_MapRoi_Management.md](prd/PRD_MapRoi_Management.md) | [plan](prd/PRD_MapRoi_Management.plan.md) | 관심지역(ROI) 관리 — Canvas 비모달 패널 + DB CRUD + xUnit |
| [PRD_GMap_UI_Design_Renewal.md](prd/PRD_GMap_UI_Design_Renewal.md) | [plan](prd/PRD_GMap_UI_Design_Renewal.plan.md) | GMap UI 디자인 통합 리뉴얼 — 컨셉1 블루헤더+화이트바디 + 툴바 그룹화 |
| [PRD_Broadcast_Panel_Embedded.md](prd/PRD_Broadcast_Panel_Embedded.md) | [plan](prd/PRD_Broadcast_Panel_Embedded.plan.md) | 방송 패널 임베디드 전환 — Window→Canvas CustomControl (음원+TTS) |
| [PRD_Layer_Panel_Tree_Redesign.md](prd/PRD_Layer_Panel_Tree_Redesign.md) | [plan](prd/PRD_Layer_Panel_Tree_Redesign.plan.md) | 레이어 패널 트리 재설계 — 카테고리 중심 3-Tier + Zoom AND + 이벤트 이중발생 수정 |
| [PRD_Layer_Management_System.md](prd/PRD_Layer_Management_System.md) | [plan](prd/PRD_Layer_Management_System.plan.md) | 레이어 관리 시스템 — 3-Tier 레이어 + DB 상태 저장 + 카테고리별 ON/OFF |
## 완료 (Report 있음) — 57건
> 상세 목록: [INDEX_ARCHIVE.md](INDEX_ARCHIVE.md)

| PRD | Plan | Report | 완료일 |
|-----|------|--------|-------|
| [PRD_CollectionChanged_BatchReset.md](prd/PRD_CollectionChanged_BatchReset.md) | [plan](prd/PRD_CollectionChanged_BatchReset.plan.md) | [report](reports/REPORT_CollectionChanged_BatchReset.md) | 2026-03-13 |
| [PRD_SharedTimer_Chunk_Dequeue.md](prd/PRD_SharedTimer_Chunk_Dequeue.md) | [plan](prd/PRD_SharedTimer_Chunk_Dequeue.plan.md) | [report](reports/REPORT_SharedTimer_Chunk_Dequeue.md) | 2026-03-13 |
| [PRD_EntryId_Nats_Uuid_DirectMatch.md](prd/PRD_EntryId_Nats_Uuid_DirectMatch.md) | [plan](prd/PRD_EntryId_Nats_Uuid_DirectMatch.plan.md) | [report](reports/REPORT_EntryId_Nats_Uuid_DirectMatch.md) | 2026-03-13 |
| [PRD_EventQueue_Symbol_Unification.md](prd/PRD_EventQueue_Symbol_Unification.md) | [plan](prd/PRD_EventQueue_Symbol_Unification.plan.md) | [report](reports/REPORT_EventQueue_Symbol_Unification.md) | 2026-03-13 |
| [PRD_Batch_Action_Report.md](prd/PRD_Batch_Action_Report.md) | [plan](prd/PRD_Batch_Action_Report.plan.md) | [report](reports/REPORT_Batch_Action_Report.md) | 2026-03-13 |
| [PRD_DevicePanel_ProgressCircle_Fix.md](prd/PRD_DevicePanel_ProgressCircle_Fix.md) | [plan](prd/PRD_DevicePanel_ProgressCircle_Fix.plan.md) | [report](reports/REPORT_DevicePanel_ProgressCircle_Fix.md) | 2026-03-12 |

## 완료 (Report 누락) — 조치 필요
| PRD | Plan | Report | 완료일 |
|-----|------|--------|-------|
| [PRD_Nats_Detection_Routing_Fix.md](prd/PRD_Nats_Detection_Routing_Fix.md) | [plan](prd/PRD_Nats_Detection_Routing_Fix.plan.md) | - (미생성) | 2026-03-05 |
| [PRD_SelectionView_CheckBox_To_ComboBox.md](prd/PRD_SelectionView_CheckBox_To_ComboBox.md) | [plan](prd/PRD_SelectionView_CheckBox_To_ComboBox.plan.md) | - (미생성) | 2026-02-26 |
| [PRD_DeviceTab_Header_Truncation.md](prd/PRD_DeviceTab_Header_Truncation.md) | [plan](prd/PRD_DeviceTab_Header_Truncation.plan.md) | - (미생성) | 2026-02-26 |
| [PRD_EventPanel_Cancel_Token.md](prd/PRD_EventPanel_Cancel_Token.md) | [plan](prd/PRD_EventPanel_Cancel_Token.plan.md) | - (미생성) | 2026-02-27 |
| [PRD_PanelView_Missing_Columns.md](prd/PRD_PanelView_Missing_Columns.md) | [plan](prd/PRD_PanelView_Missing_Columns.plan.md) | - (미생성) | 2026-02-27 |
| [PRD_EventPanel_Cache_Reuse.md](prd/PRD_EventPanel_Cache_Reuse.md) | [plan](prd/PRD_EventPanel_Cache_Reuse.plan.md) | - (미생성) | 2026-02-26 |
| [PRD_SelectionView_Layout_Compact.md](prd/PRD_SelectionView_Layout_Compact.md) | [plan](prd/PRD_SelectionView_Layout_Compact.plan.md) | - (미생성) | 2026-02-26 |
| [PRD_DeviceGroup_Assignment_Ui.md](prd/PRD_DeviceGroup_Assignment_Ui.md) | [plan](prd/PRD_DeviceGroup_Assignment_Ui.plan.md) | - (미생성) | 2026-02-25 |
| [PRD_Geolocation_AllDevices.md](prd/PRD_Geolocation_AllDevices.md) | [plan](prd/PRD_Geolocation_AllDevices.plan.md) | - (미생성) | 2026-02-25 |
| [PRD_DeviceView_ViewModel_Alignment.md](prd/PRD_DeviceView_ViewModel_Alignment.md) | [plan](prd/PRD_DeviceView_ViewModel_Alignment.plan.md) | - (미생성) | 2026-02-25 |

## Draft (미착수)
| PRD | 생성일 | 설명 |
|-----|-------|------|
| [PRD_EventStatistics_Api.md](prd/PRD_EventStatistics_Api.md) | 2026-02-27 | 이벤트 통계 API (클라이언트 완료, 서버 별도 저장소) |
| [PRD_Step3_Monitoring_Solution_NATS_Migration.md](migration_prd/PRD_Step3_Monitoring_Solution_NATS_Migration.md) | - | Monitoring Solution NATS 마이그레이션 |
| [PRD_Step4_GMaps_Integration.md](migration_prd/PRD_Step4_GMaps_Integration.md) | - | GMaps 통합 |
| [PRD_Step5_Infra_Services_Migration.md](migration_prd/PRD_Step5_Infra_Services_Migration.md) | - | 인프라 서비스 마이그레이션 |
| [PRD_Step6_GroupDevice_Legacy_Removal.md](migration_prd/PRD_Step6_GroupDevice_Legacy_Removal.md) | - | 그룹 디바이스 레거시 제거 |
| [PRD_DeviceApi_MigrationTest.md](migration_prd/PRD_DeviceApi_MigrationTest.md) | - | Device API 마이그레이션 테스트 |

## 기타 문서 (레거시)
| 문서 | 유형 | 설명 |
|------|------|------|
| [REPORT_NATS_Event_Integration.md](reports/REPORT_NATS_Event_Integration.md) | 리뷰 | NATS 이벤트 연동 설계 ↔ 구현 GAP 분석 (독립 문서, PRD 없음) |
| [PRD_Migration_Master_Guide.md](migration_prd/PRD_Migration_Master_Guide.md) | 가이드 | 마이그레이션 마스터 가이드 v1.1 |
| [GOP_Restful_Api_연동설계.md](GOP_Restful_Api_연동설계.md) | 설계 | REST API 연동 설계 v3.8 |
| [Gop_Message_Broker_연동설계.md](Gop_Message_Broker_연동설계.md) | 설계 | NATS 메시지 브로커 연동 설계 v1.0 |
