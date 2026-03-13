# TDD Plan: 전체 조치보고 (Batch Action Report)

- **PRD**: Docs/prd/PRD_Batch_Action_Report.md
- **Date**: 2026-03-13
- **Status**: In Progress

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

- [ ] **Test 3.1**: `OnClickButtonActionAll_PublishesConfirmPopup` — 버튼 클릭 시 OpenConfirmPopupMessageModel 발행 검증
  - File: `Ironwall.Dotnet.Libraries.Events.Ui/Tests/BatchActionReportTests.cs`
  - Target: `EventCardListPanelViewModel.cs`
  - Red: OnClickButtonActionAll 호출 시 ConfirmPopup 메시지 발행 확인
  - Green: 기존 코드 유지 (이미 동작)

- [ ] **Impl 3.2**: ConfirmPopup 확인 시 배치 처리 실행 연결 — `OnClickButtonActionAll`을 수정하여 ConfirmPopup 대신 직접 확인 후 `ExecuteBatchReportAsync` 호출 (또는 ConfirmPopup 콜백 방식)
  - File: `Ironwall.Dotnet.Libraries.Events.Ui/ViewModels/Panels/EventCardListPanelViewModel.cs`
  - Target: `OnClickButtonActionAll` 메서드
  - 기존 다른 Panel(예: DeviceGroupPanelViewModel)의 ConfirmPopup→실행 패턴 참조

## Phase 4: Behavioral — UI ProgressCircle 전환

- [ ] **Test 4.1**: `BatchReport_IsVisible_FalseDuringProcess` — 처리 시작 시 IsVisible=false 확인
  - File: `Ironwall.Dotnet.Libraries.Events.Ui/Tests/BatchActionReportTests.cs`
  - Target: `EventCardListPanelViewModel.cs`
  - Red: 배치 처리 시작 직후 IsVisible == false 검증
  - Green: ExecuteBatchReportAsync 시작 시 IsVisible = false 설정

- [ ] **Test 4.2**: `BatchReport_IsVisible_TrueAfterComplete` — 처리 완료 후 IsVisible=true 확인 (성공/실패 모두)
  - File: `Ironwall.Dotnet.Libraries.Events.Ui/Tests/BatchActionReportTests.cs`
  - Target: `EventCardListPanelViewModel.cs`
  - Red: 배치 처리 완료 후 IsVisible == true 검증 (finally 보장)
  - Green: finally 블록에서 IsVisible = true

- [ ] **Impl 4.3**: XAML ProgressCircle 추가 — EventCardListPanelView.xaml에 ProgressBar(IsIndeterminate) + IsVisible 바인딩 추가
  - File: `Ironwall.Dotnet.Libraries.Events.Ui/Views/Panels/EventCardListPanelView.xaml`
  - Target: ButtonActionAll 영역에 ProgressBar + BoolToInverseVisibleConverter 패턴 적용
  - 패턴 참조: `ControllerDevicePanelView.xaml` (IsVisible + BoolToInverseVisibleConverter)

## Phase 5: Behavioral — 에러 처리 (InformDialog)

- [ ] **Test 5.1**: `BatchReport_Failure_PublishesInfoPopup` — API 실패 시 OpenInfoPopupMessageModel 발행 검증
  - File: `Ironwall.Dotnet.Libraries.Events.Ui/Tests/BatchActionReportTests.cs`
  - Target: `EventCardListPanelViewModel.cs`
  - Red: API 실패 → OpenInfoPopupMessageModel 발행 + Title/Explain 검증
  - Green: catch/실패 분기에서 InfoPopup 메시지 발행

- [ ] **Impl 5.2**: InformDialog XAML TextTrimming 적용 — Explain 텍스트에 TextTrimming="CharacterEllipsis" 추가
  - File: `Dotnet.Monitoring.Solution/Views/PopupDialogs/Common/InfoPopupDialogView.xaml` (Monitoring Solution 측)
  - Target: Explain 바인딩 TextBlock에 TextTrimming 적용
  - ※ 이 파일이 다른 저장소에 있을 경우, 별도 커밋으로 처리

## Phase 6: 심볼 상태 복원 + 완료 처리

- [ ] **Test 6.1**: `BatchReport_Success_CallsProcessEventReport` — 각 카드 성공 시 ProcessEventReport 호출 검증
  - File: `Ironwall.Dotnet.Libraries.Events.Ui/Tests/BatchActionReportTests.cs`
  - Target: `EventCardListPanelViewModel.cs`
  - Red: 카드별 ProcessEventReport(deviceId, deviceType, deviceGroups) 호출 검증
  - Green: API 성공 후 _symbolEventManager.ProcessEventReport 호출

- [ ] **Test 6.2**: `BatchReport_Complete_CallsDequeueAll` — 전체 완료 시 EventQueueManager.DequeueAll() 호출 검증
  - File: `Ironwall.Dotnet.Libraries.Events.Ui/Tests/BatchActionReportTests.cs`
  - Target: `EventCardListPanelViewModel.cs`
  - Red: 모든 카드 처리 완료 후 DequeueAll 호출 검증
  - Green: finally 또는 완료 분기에서 _eventQueueManager.DequeueAll() 호출

## Phase 7: 최종 검증

- [ ] **Verify 7.1**: 전체 빌드 확인 — `dotnet build` 0 errors, 0 warnings
- [ ] **Verify 7.2**: 기존 테스트 회귀 없음 — `dotnet test` 전체 통과
