# TDD Plan: 전체 조치보고 (Batch Action Report)

- **PRD**: Docs/prd/PRD_Batch_Action_Report.md
- **Date**: 2026-03-13
- **Status**: Completed

## Phase 1: Structural — 금지 코드 제거 + 기존 핸들러 정리

- [x] **Tidy 1.1**: (Structural) `HandleAsync(CallAllEventReportMessageModel)` 내부의 금지 코드 제거 — `model.Status = EnumTrueFalse.True`, `_providerService.UpdateDetectionEventAsync`, `_providerService.UpdateMalfunctionEventAsync` 제거
  - File: `Ironwall.Dotnet.Libraries.Events.Ui/ViewModels/Panels/EventCardListPanelViewModel.cs`
  - Target: 같은 파일 (lines 250-347)
  - 빌드 확인 후 커밋

- [x] **Tidy 1.2**: (Structural) `IHandle<CallAllEventReportMessageModel>` 구독 및 `HandleAsync` 메서드 제거 — ConfirmPopup 메시지 타입 불일치로 사용 불가하므로 폐기
  - File: `Ironwall.Dotnet.Libraries.Events.Ui/ViewModels/Panels/EventCardListPanelViewModel.cs`
  - Target: 클래스 선언부에서 `IHandle<CallAllEventReportMessageModel>` 제거, `HandleAsync` 메서드 삭제
  - 빌드 확인 후 커밋

## Phase 2: Behavioral — 핵심 배치 처리 로직

- [x] **Test 2.1**: `BatchReport_ActionUser_IsUsername` — ActionUser가 `IAccountModel.Username` 값인지 검증
  - File: `Ironwall.Dotnet.Libraries.Events.Ui/Tests/BatchActionReportTests.cs`
  - Target: `EventCardListPanelViewModel.cs`
  - Red: `_userModel.Username`을 ActionUser로 사용하는 테스트 작성
  - Green: 배치 처리 메서드에서 `_userModel.Username` 사용

- [x] **Test 2.2**: `BatchReport_ActionDetails_IsBulkText` — ActionDetails가 `"일괄처리"` 고정 문구인지 검증
  - File: `Ironwall.Dotnet.Libraries.Events.Ui/Tests/BatchActionReportTests.cs`
  - Target: `EventCardListPanelViewModel.cs`
  - Red: Content가 "일괄처리"인 ActionEventCreateDto 생성 검증
  - Green: 최소 구현

- [x] **Test 2.3**: `BatchReport_SuccessAll_RemovesAllCards` — 전체 API 성공 시 ViewModelProvider가 비어있는지 검증
  - File: `Ironwall.Dotnet.Libraries.Events.Ui/Tests/BatchActionReportTests.cs`
  - Target: `EventCardListPanelViewModel.cs`
  - Red: Mock ApiService가 Success 반환 → ViewModelProvider.Count == 0 검증
  - Green: 순차 루프 + API 호출 + 카드 제거 구현

- [x] **Test 2.4**: `BatchReport_FailOnSecond_StopsAndKeepsRemaining` — 2번째 카드 API 실패 시 1번째만 제거, 나머지 유지
  - File: `Ironwall.Dotnet.Libraries.Events.Ui/Tests/BatchActionReportTests.cs`
  - Target: `EventCardListPanelViewModel.cs`
  - Red: Mock ApiService가 2번째에서 Success=false → ViewModelProvider.Count == 원래-1 검증
  - Green: API 실패 시 break 로직

- [x] **Test 2.5**: `BatchReport_EmptyList_NoApiCall` — 카드 0개일 때 API 호출 없이 정상 종료
  - File: `Ironwall.Dotnet.Libraries.Events.Ui/Tests/BatchActionReportTests.cs`
  - Target: `EventCardListPanelViewModel.cs`
  - Red: ViewModelProvider 비어있을 때 예외 없이 완료
  - Green: 빈 리스트 가드 조건

- [x] **Test 2.6**: `BatchReport_Success_PublishesSendActionRequestMessage` — API 성공 후 SendActionRequestMessage가 EventAggregator로 발행되는지 검증
  - File: `Ironwall.Dotnet.Libraries.Events.Ui/Tests/BatchActionReportTests.cs`
  - Target: `EventCardListPanelViewModel.cs`
  - Red: Mock EventAggregator에서 SendActionRequestMessage 수신 검증
  - Green: API 성공 → PublishOnBackgroundThreadAsync(SendActionRequestMessage) 호출

## Phase 3: Behavioral — ConfirmPopup → 배치 처리 연결

- [x] **Test 3.1**: `HandleAsync_CallAllEventReport_CallsExecuteBatchReport` — ConfirmPopup 확인 시 HandleAsync → ExecuteBatchReportAsync 호출 검증
  - File: `Ironwall.Dotnet.Libraries.Events.Ui/Tests/BatchActionReportTests.cs`
  - Target: `EventCardListPanelViewModel.cs`
  - Green: IHandle<CallAllEventReportMessageModel> 재등록 + HandleAsync에서 ExecuteBatchReportAsync 호출

- [x] **Impl 3.2**: ConfirmPopup 확인 시 배치 처리 실행 연결 — IHandle<CallAllEventReportMessageModel> 재등록, HandleAsync에서 ExecuteBatchReportAsync 호출
  - File: `Ironwall.Dotnet.Libraries.Events.Ui/ViewModels/Panels/EventCardListPanelViewModel.cs`
  - DeviceGroupPanelViewModel과 동일한 패턴: OpenConfirmPopupMessageModel.MessageModel → ConfirmPopup 릴레이 → IHandle 수신

## Phase 4: Behavioral — UI ProgressCircle 전환

- [x] **Test 4.1**: `BatchReport_IsVisible_FalseDuringProcess` — 처리 시작 시 IsVisible=false 확인
  - File: `Ironwall.Dotnet.Libraries.Events.Ui/Tests/BatchActionReportTests.cs`
  - Target: `EventCardListPanelViewModel.cs`
  - Green: ExecuteBatchReportAsync 시작 시 IsVisible = false, try/finally 패턴

- [x] **Test 4.2**: `BatchReport_IsVisible_TrueAfterComplete` + `BatchReport_IsVisible_TrueAfterFailure` — 처리 완료 후 IsVisible=true 확인 (성공/실패 모두)
  - File: `Ironwall.Dotnet.Libraries.Events.Ui/Tests/BatchActionReportTests.cs`
  - Target: `EventCardListPanelViewModel.cs`
  - Green: finally 블록에서 IsVisible = true

- [x] **Impl 4.3**: XAML ProgressCircle 추가 — EventCardListPanelView.xaml에 ProgressBar(IsIndeterminate) + IsVisible 바인딩 추가
  - File: `Ironwall.Dotnet.Libraries.Events.Ui/Views/Panels/EventCardListPanelView.xaml`
  - Button: BoolToVisibleConverter, ProgressBar: BoolToInverseVisibleConverter

## Phase 5: Behavioral — 에러 처리 (InformDialog)

- [x] **Test 5.1**: `BatchReport_Failure_PublishesInfoPopup` — API 실패 시 OpenInfoPopupMessageModel 발행 검증
  - File: `Ironwall.Dotnet.Libraries.Events.Ui/Tests/BatchActionReportTests.cs`
  - Target: `EventCardListPanelViewModel.cs`
  - Green: 실패 분기에서 OpenInfoPopupMessageModel 발행 (Title="전체 조치보고 오류")

- [x] **Impl 5.2**: InformDialog XAML TextTrimming 적용 — Explain 텍스트에 TextTrimming="CharacterEllipsis" 추가
  - File: `Dotnet.Monitoring.Solution/Views/PopupDialogs/Common/InfoPopupDialogView.xaml`
  - 별도 저장소 — 별도 커밋 필요

## Phase 6: 심볼 상태 복원 + 완료 처리

- [x] **Test 6.1**: `BatchReport_Success_CallsProcessEventReport` — 각 카드 성공 시 ProcessEventReport 호출 검증
  - File: `Ironwall.Dotnet.Libraries.Events.Ui/Tests/BatchActionReportTests.cs`
  - Target: `EventCardListPanelViewModel.cs`
  - Green: API 성공 후 _symbolEventManager.ProcessEventReport(deviceId, deviceType, deviceGroups) 호출

- [x] **Test 6.2**: `BatchReport_Complete_CallsDequeueAll` — 전체 완료 시 EventQueueManager.DequeueAll() 호출 검증
  - File: `Ironwall.Dotnet.Libraries.Events.Ui/Tests/BatchActionReportTests.cs`
  - Target: `EventCardListPanelViewModel.cs`
  - Green: 루프 완료 후 _eventQueueManager.DequeueAll() 호출

## Phase 7: 최종 검증

- [x] **Verify 7.1**: 전체 빌드 확인 — `dotnet build` 0 errors
- [x] **Verify 7.2**: 기존 테스트 회귀 없음 — 기존 36 tests 통과, BatchActionReport 13 tests 추가 (총 49 Green)
  - 사전 존재 실패 11건 (DeviceSymbolLookup/DataHelper/SymbolEventManager) — 본 변경과 무관
