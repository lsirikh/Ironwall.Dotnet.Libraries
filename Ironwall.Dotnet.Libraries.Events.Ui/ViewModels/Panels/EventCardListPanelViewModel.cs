using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Providers;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Ironwall.Dotnet.Monitoring.Models.Comms;
using Ironwall.Dotnet.Monitoring.Models.Events;
using System;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Threading;
using Action = System.Action;

namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels{
    /****************************************************************************
       Purpose      : 이벤트 카드 목록 패널 — 개별/전체 조치보고 처리
                       [전체 조치보고] ConfirmPopup 확인 → 순차 API 호출
                       → 성공 시 카드 제거 + 심볼 복원 + NATS 발행
                       → 실패 시 즉시 중단 + InformDialog 표시
                       (PRD: Docs/prd/PRD_Batch_Action_Report.md)
       Created By   : GHLee
       Created On   : 7/1/2025 7:13:26 PM
       Department   : SW Team
       Company      : Sensorway Co., Ltd.
       Email        : lsirikh@naver.com
    ****************************************************************************/
    public class EventCardListPanelViewModel: BaseEventPanelViewModel<EventCardBaseViewModel>
                                            , IHandle<DetectionReportedMessageModel>
                                            , IHandle<MalfunctionReportedMessageModel>
                                            , IHandle<CallAllEventReportMessageModel>
                                            , IHandle<EventEntryEnqueuedMessage>
    {
        #region - Ctors -
        public EventCardListPanelViewModel(IEventAggregator ea
                                          , ILogService log
                                          , EventProviderService providerService
                                          , IAccountModel userModel
                                          , IEventApiService apiService
                                          , ISymbolEventManager symbolEventManager
                                          , IEventQueueManager eventQueueManager
                                          , IActionReportGuard reportGuard)
                                        : base(ea, log)
        {
            _providerService = providerService;
            _userModel = userModel;
            _apiService = apiService;
            _symbolEventManager = symbolEventManager;
            _eventQueueManager = eventQueueManager;
            _reportGuard = reportGuard;
            _batchBuffer = new EventCardBatchBuffer<EventCardBaseViewModel>();
        }
        #endregion
        #region - FR-EN-10/11 권한 게이팅 (events 도메인) -
        // IoC.Get lazy — 미등록/오프라인/테스트 시 null → 전체허용 폴백
        private IPermissionService? _permissionService;
        private bool _permissionResolved;
        private IPermissionService? ResolvePermissionService()
        {
            if (_permissionResolved) return _permissionService;
            try { _permissionService = IoC.Get<IPermissionService>(); _permissionResolved = _permissionService != null; }   // 성공 시에만 캐시(영구 fail-open 방지)
            catch (Exception ex)
            {
                _log?.Warning($"[{nameof(EventCardListPanelViewModel)}] PermissionService 미해석(전체허용 폴백): {ex.Message}");
                _permissionService = null;
            }
            return _permissionService;
        }
        private bool CanCtrlEvents() => ResolvePermissionService()?.CanControl("events") ?? true;

        // FR-EN-11: 역할강등 시 ACK 버튼 CanExecute 재평가
        private void OnPermissionsChanged()
        {
            Execute.OnUIThread(() =>
            {
                NotifyOfPropertyChange(nameof(CanReportAction));
                NotifyOfPropertyChange(nameof(CanReportAll));
            });
        }
        public bool CanReportAction => CanCtrlEvents();
        public bool CanReportAll    => CanCtrlEvents();
        #endregion
        #region - Implementation of Interface -
        #endregion
        #region - Overrides -
        protected override Task OnActivateAsync(CancellationToken cancellationToken)
        {
            // FR-EN-11: 역할강등 재평가 구독
            var perm = ResolvePermissionService();
            if (perm != null) perm.PermissionsChanged += OnPermissionsChanged;

            ViewModelProvider.CollectionChanged += CollectionEntity_CollectionChanged;
            _batchTimer = new Timer(FlushPendingCards, null, BATCH_INTERVAL_MS, BATCH_INTERVAL_MS);
            return base.OnActivateAsync(cancellationToken);
        }

        protected override Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
        {
            // FR-EN-11: 역할강등 재평가 구독 해제
            var perm = ResolvePermissionService();
            if (perm != null) perm.PermissionsChanged -= OnPermissionsChanged;

            ViewModelProvider.CollectionChanged -= CollectionEntity_CollectionChanged;

            // 타이머 안전 정지: Infinite로 먼저 중지 후 Dispose (진행 중 콜백 race 방지)
            _batchTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            _batchTimer?.Dispose();
            _batchTimer = null;
            _pendingEntries.Clear();
            _cardByEntryId.Clear();

            // 잔여 배치 큐 카드 Dispose
            var remaining = _batchBuffer.DrainQueue();
            foreach (var card in remaining)
                card.Dispose();

            // 현재 표시 중인 카드 전체 Dispose
            foreach (var card in ViewModelProvider.ToList())
                card.Dispose();
            ViewModelProvider.Clear();

            return base.OnDeactivateAsync(close, cancellationToken);
        }
        #endregion
        #region - Binding Methods -
        #endregion
        #region - Processes -
        /// <summary>
        /// NATS 수신 스레드에서 직접 호출 — lock-free ConcurrentQueue에 적재.
        /// 150ms 타이머(FlushPendingCardsAsync)가 Background BeginInvoke로 배치 처리.
        /// DispatcherService.Invoke() 직접 호출 금지 — NATS 스레드를 블로킹하지 않는다.
        /// </summary>
        public void EnqueueCard(EventCardBaseViewModel card)
        {
            if (card == null) return;
            var accepted = _batchBuffer.Enqueue(card);
            if (!accepted)
            {
                _log?.Warning($"[EnqueueCard] 배치 버퍼 포화 — 카드 폐기: {card.GetType().Name}");
                card.Dispose();
            }
        }
        private void CollectionEntity_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            IsAnimationEnabled = ViewModelProvider.Count <= ANIMATION_THRESHOLD;

            // Reset은 배치 서스펜드 패턴에서 수동 발행 — entryId 매칭은 배치에서 이미 처리
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                UpdateAction?.Invoke();
                return;
            }

            // 카드 추가 시 미연결 entryId 자동 매칭
            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
            {
                foreach (var item in e.NewItems)
                {
                    if (item is EventCardBaseViewModel card)
                        TryAssignEntryId(card);
                }
            }

            UpdateAction?.Invoke();
        }

        /// <summary>
        /// 카드 추가 시 _pendingEntries에서 eventId 기반 1:1 매칭 (폴백)
        /// (PRD_EntryId_Nats_Uuid_DirectMatch FR-05, FR-06)
        /// </summary>
        private void TryAssignEntryId(EventCardBaseViewModel card)
        {
            if (card.EntryId != null) return;
            if (card.Model == null) return;

            if (_pendingEntries.TryGetValue(card.Model.Id, out var entryId))
            {
                card.EntryId = entryId;
                _pendingEntries.TryRemove(card.Model.Id, out _);
                _cardByEntryId[entryId] = card;
                _log?.Info($"EntryId 지연 매칭: Card({card.Model.Id}) → Entry({entryId})");
            }
        }

        // Timer 콜백: 동기 래퍼 — async void 금지(Timer 콜백에서 예외 시 프로세스 크래시)
        /// <summary>
        /// (EB2) 표시 카드 수가 MAX_EVENT_CARDS 를 초과하면 가장 오래된 카드부터 제거한다.
        /// 제거 방식은 검증된 경로(HandleAutoReportAsync)와 동일: _cardByEntryId 정리 + Dispose.
        /// 이 패널은 EventProvider(EP) 백킹 컬렉션을 쓰지 않고 ViewModelProvider(VP) 에서만 카드를 제거한다
        /// (CollectionChanged 핸들러에 Remove 분기가 없어 핸들러 활성/비활성이 캡 정확성에 영향 없음).
        /// ⚠ 제거되는 카드의 EQM 엔트리는 남는다 — 표시 캡은 UI 메모리 보호 목적이며, EQM 엔트리/심볼 시각상태는
        ///   EQM 수명주기(자동정리/조치보고)로 정리된다(그 전까지 desync 가능). 추적용으로 경고 로그를 남긴다.
        /// </summary>
        private void EnforceDisplayCap()
        {
            int removed = 0;
            int orphanedEntries = 0;
            while (ViewModelProvider.Count > MAX_EVENT_CARDS)
            {
                var oldest = ViewModelProvider[0];
                if (oldest.EntryId != null)
                {
                    _cardByEntryId.TryRemove(oldest.EntryId, out _);
                    orphanedEntries++;   // EQM 엔트리는 남음 (자동정리까지 잔류)
                }
                ViewModelProvider.Remove(oldest);
                oldest.Dispose();
                removed++;
            }
            if (removed > 0)
                _log?.Warning($"[EB2] 표시 카드 하드캡({MAX_EVENT_CARDS}) 초과 — 오래된 카드 {removed}개 제거 " +
                              $"(EQM 엔트리 {orphanedEntries}개는 자동정리까지 잔류)");
        }

        private void FlushPendingCards(object? state) => _ = FlushPendingCardsAsync();

        private async Task FlushPendingCardsAsync()
        {
            try
            {
                var batch = _batchBuffer.DrainQueue();
                if (batch.Count == 0) return;

                // 적응형 간격 조정
                var newInterval = _batchBuffer.CalculateInterval(batch.Count);
                _batchTimer?.Change(newInterval, newInterval);

                await DispatcherService.BeginInvoke(() =>
                {
                    // CollectionChanged 서스펜드 → 배치 Add → 재등록 (핸들러 폭주 방지)
                    ViewModelProvider.CollectionChanged -= CollectionEntity_CollectionChanged;
                    try
                    {
                        foreach (var card in batch)
                            ViewModelProvider.Add(card);
                    }
                    finally
                    {
                        ViewModelProvider.CollectionChanged += CollectionEntity_CollectionChanged;
                        // 배치 entryId 매칭
                        foreach (var card in batch)
                            TryAssignEntryId(card);
                        // (EB2) 표시 카드 하드 캡 — 장시간 운용 시 무한 증가 방지 (핸들러 활성 상태에서 제거)
                        EnforceDisplayCap();
                        IsAnimationEnabled = ViewModelProvider.Count <= ANIMATION_THRESHOLD;
                        UpdateAction?.Invoke();
                    }
                }, DispatcherPriority.Background);
            }
            catch (ObjectDisposedException)
            {
                // Deactivate 이후 타이머가 한 번 더 실행된 경우 — 정상 종료 패턴, 무시
            }
            catch (Exception ex)
            {
                _log?.Error($"[FlushPendingCards] 배치 처리 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 전체 조치보고 버튼 클릭 → ConfirmPopup 표시
        /// 확인 시 ConfirmPopupDialogViewModel이 CallAllEventReportMessageModel을 재발행하여
        /// HandleAsync(CallAllEventReportMessageModel)에서 ExecuteBatchReportAsync() 실행
        /// </summary>
        public async void OnClickButtonActionAll(object sender, RoutedEventArgs e)
        {
            // FR-EN-10 ACK 일괄 게이트 (CanControl)
            if (!CanCtrlEvents())
            {
                await _eventAggregator.PublishOnUIThreadAsync(new OpenInfoPopupMessageModel
                {
                    Title = "권한 없음",
                    Explain = "조치보고 권한이 없습니다."
                });
                return;
            }
            await _eventAggregator.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel() { Title = "전체 조치보고", Explain = "전체 조치보고를 수행하시겠습니까?", MessageModel = new CallAllEventReportMessageModel() });
        }

        public void OnClickEventCard(object sender, RoutedEventArgs e)
        {
            //if (!((sender as ListBox).SelectedItem is EventCardViewModel preEventViewModel))
            //    return;

            //if (SetupModel.EventCardMapChange)
            //{
            //    await _eventAggregator.PublishOnCurrentThreadAsync(new OpenCanvasMessageModel() { MapNumber = preEventViewModel.Map });
            //}
        }


        public async void OnButtonAction(object sender, RoutedEventArgs e)
        {
            // FR-EN-10 ACK 개별 게이트 (CanControl)
            if (!CanCtrlEvents())
            {
                await _eventAggregator.PublishOnUIThreadAsync(new OpenInfoPopupMessageModel
                {
                    Title = "권한 없음",
                    Explain = "조치보고 권한이 없습니다."
                });
                return;
            }

            var source = e.OriginalSource as FrameworkElement;
            var dataContext = source?.DataContext as EventCardBaseViewModel;
            if(dataContext?.GetType() == typeof(DetectionEventCardViewModel)) 
            {
                var report = IoC.Get<DetectionReportDialogViewModel>();
                var user = IoC.Get<IAccountModel>();
                report.UpdateData(dataContext, user);
                await _eventAggregator.PublishOnCurrentThreadAsync(new OpenEventReportDialogMessageModel() { EventType = "DETECTION" });
            }
            else if(dataContext?.GetType() == typeof(MalfunctionEventCardViewModel))
            {
                var report = IoC.Get<MalfunctionReportDialogViewModel>();
                var user = IoC.Get<IAccountModel>();
                report.UpdateData(dataContext, user);
                await _eventAggregator.PublishOnCurrentThreadAsync(new OpenEventReportDialogMessageModel() { EventType = "MALFUNCTION" });
            }
            else
            {
                var report = IoC.Get<DetectionReportDialogViewModel>();
                var user = IoC.Get<IAccountModel>();
                report.UpdateData(dataContext, user);
                await _eventAggregator.PublishOnCurrentThreadAsync(new OpenEventReportDialogMessageModel() { EventType = "DETECTION" });
            }

        }

        /// <summary>
        /// 전체 조치보고 배치 처리 (PRD_Batch_Action_Report)
        /// 흐름: IsVisible=false(ProgressCircle 표시) → 카드 순차 순회
        ///   → ① API 조치보고(CreateActionEventAsync) — 실패 시 즉시 중단 + InformDialog
        ///   → ② 심볼 상태 복원(ProcessEventReport)
        ///   → ③ 카드 UI 제거
        ///   → ④ NATS 발행(SendActionRequestMessage)
        ///   → 전체 완료 후 DequeueAll()로 큐 정리
        /// 주의: 클라이언트에서 status/action_reported 직접 변경 금지 — 서버가 Action 생성 시 자동 처리
        /// </summary>
        public async Task ExecuteBatchReportAsync()
        {
            // FR-EN-10 ACK 배치 게이트 (CanControl) — _batchReportGate 이전에 검사
            if (!CanCtrlEvents())
            {
                _log?.Warning("[ExecuteBatchReportAsync] 권한 없음 — events:control 미보유");
                return;
            }

            // FR-03: 인플라이트 가드 — 더블클릭/중복 실행 방지
            if (!await _batchReportGate.WaitAsync(0))
            {
                _log?.Warning("ExecuteBatchReportAsync 이미 실행 중 — 중복 요청 무시");
                return;
            }

            IsVisible = false;
            // CollectionChanged 서스펜드 — Remove × N의 UpdateAction 폭주 방지
            ViewModelProvider.CollectionChanged -= CollectionEntity_CollectionChanged;
            try
            {
                await _eventAggregator.PublishOnCurrentThreadAsync(new ClosePopupMessageModel());

                var cards = ViewModelProvider.ToList();

                foreach (var card in cards)
                {
                    var eventModel = card.Model;
                    var eventId = eventModel?.Id ?? 0;

                    // (EC5) 동일 이벤트가 Auto/AutoRecovery 등 다른 경로에서 조치 진행 중이면 중복 스킵
                    if (!_reportGuard.TryEnter(eventId))
                    {
                        _log?.Info($"배치 조치보고: Event({eventId}) 진행 중 — 중복 스킵");
                        continue;
                    }

                    try
                    {
                        // ① 서버 API로 조치보고 생성 (ActionUser: Username만, Content: "일괄처리" 고정)
                        var dto = new ActionEventCreateDto
                        {
                            User = _userModel.Name,
                            Content = "일괄처리",
                            FromEventId = eventId
                        };

                        var response = await _apiService.CreateActionEventAsync(dto);
                        if (!response.Success)
                            throw new Exception($"처리 중 장애가 발생했습니다.\n{response.Message}");

                        // ② 심볼 복원: EntryId null 폴백 체인 (FR-01)
                    // 1차: _pendingEntries 폴백 — entryId가 아직 카드에 할당 안 된 경우
                    if (card.EntryId == null && eventModel?.Id != null)
                    {
                        if (_pendingEntries.TryRemove(eventModel.Id, out var fallbackEntryId))
                        {
                            card.EntryId = fallbackEntryId;
                            _cardByEntryId[fallbackEntryId] = card;
                            _log?.Info($"EntryId 배치 폴백 매칭: Event({eventModel.Id}) → Entry({fallbackEntryId})");
                        }
                    }

                    if (card.EntryId != null)
                    {
                        _eventQueueManager.Dequeue(card.EntryId);
                    }
                    else if (eventModel?.Device != null)
                    {
                        // 2차: FindEntryByDevice 안전망 — _pendingEntries에도 없는 경우
                        var entry = _eventQueueManager.FindEntryByDevice(eventModel.Device.Id, eventModel.Device.DeviceType);
                        if (entry != null)
                        {
                            _eventQueueManager.Dequeue(entry.EntryId);
                            _log?.Warning($"EntryId null 안전망 — FindEntryByDevice 복원: Device({eventModel.Device.Id})");
                        }
                        else
                        {
                            _log?.Error($"EntryId null 복원 불가: Event({eventModel?.Id}) — EQM 잔류 가능성");
                        }
                    }

                    // ③ 제거 + Dispose
                    ViewModelProvider.Remove(card);
                    if (card.EntryId != null) _cardByEntryId.TryRemove(card.EntryId, out _);
                    card.Dispose();

                    // ④ NATS로 조치보고 발행 (NatsDomainService 경유)
                    await _eventAggregator.PublishOnBackgroundThreadAsync(new SendActionRequestMessage
                    {
                        EventId = eventModel?.Id ?? 0,
                        EventType = eventModel?.MessageType ?? EnumEventType.Intrusion,
                        ActionDetails = "일괄처리",
                        ActionUser = _userModel.Name,
                        ActionTime = DateTime.Now,
                        OriginEvent = eventModel as IExEventModel,   // NATS from_event(device) 원천
                        ActionId = response.Data?.Id ?? 0            // 생성된 Action DB ID
                    });

                        await Task.Yield(); // Dispatcher 렌더 기회 보장 (Task.Delay(20) 대체)
                    }
                    finally
                    {
                        _reportGuard.Exit(eventId);
                    }
                }
            }
            catch (Exception ex)
            {
                // API 실패 시 즉시 중단하고 에러 팝업 표시 (남은 카드는 유지)
                await _eventAggregator.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                {
                    Title = "전체 조치보고 오류",
                    Explain = $"{ex}"
                });
            }
            finally
            {
                _batchReportGate.Release();
                ViewModelProvider.CollectionChanged += CollectionEntity_CollectionChanged;
                IsAnimationEnabled = ViewModelProvider.Count <= ANIMATION_THRESHOLD;
                UpdateAction?.Invoke();
                IsVisible = true;
            }
        }

        public void OnButtonCameraPopup(object sender, RoutedEventArgs e)
        {
            //if (!SetupModel.IsServer)
            //    return;

            //lock (_locker)
            //{

            //    var source = e.OriginalSource as FrameworkElement;
            //    var dataContext = source.DataContext as EventCardViewModel;

            //    var domainService = IoC.Get<DomainService>();


            //    if (Cts.IsCancellationRequested)
            //        Cts = new CancellationTokenSource();
            //    else
            //    {
            //        Cts.Cancel();
            //        Cts = new CancellationTokenSource();
            //    }

            //    _ = domainService.CameraPopup(dataContext.IdController, dataContext.IdSensor, Cts.Token);
            //}
        }

        /// <summary>
        /// 자동 조치보고 핸들러 — EventQueueManager.OnAutoReport 구독용.
        /// 타임아웃 만료 시 API 조치보고 → Dequeue → UI 카드 제거 → NATS 발행
        /// AutoReportInFlight 리셋은 EventUiModule ContinueWith에서 처리 (IMPL-07)
        /// </summary>
        public async Task HandleAutoReportAsync(EventEntry entry)
        {
            var entryId = entry.EntryId!;
            try
            {
                if (!_cardByEntryId.TryGetValue(entryId, out var card))
                {
                    _log?.Warning($"AutoReport: entryId({entryId}) 카드 없음 → Dequeue 후 스킵");
                    _eventQueueManager.Dequeue(entryId);
                    return;
                }

                var eventId = card.Model?.Id ?? entry.EventId;
                if (eventId <= 0)
                {
                    _log?.Warning($"AutoReport: entryId({entryId}) eventId 확인 불가 — Dequeue 후 스킵");
                    _eventQueueManager.Dequeue(entryId);
                    return;
                }

                // (EC5) 동일 이벤트가 Batch/AutoRecovery 등 다른 경로에서 조치 진행 중이면 중복 스킵
                if (!_reportGuard.TryEnter(eventId))
                {
                    _log?.Info($"AutoReport: Event({eventId}) 조치보고 진행 중 — 중복 스킵");
                    entry.NextRetryAfter = DateTime.Now.AddSeconds(BACKOFF_SECONDS); // 타이트 재발화 루프 방지
                    return;
                }

                try
                {
                var content = entry.EventType switch
                {
                    EnumEventType.Intrusion => AUTO_REPORT_DETECTION,
                    EnumEventType.Fault     => AUTO_REPORT_MALFUNCTION,
                    _                       => AUTO_REPORT_DEFAULT
                };

                var dto = new ActionEventCreateDto
                {
                    User = GetActorName(),
                    Content = content,
                    FromEventId = eventId
                };

                var response = await _apiService.CreateActionEventAsync(dto);
                if (!response.Success)
                {
                    _log?.Warning($"AutoReport API 실패: {response.Message}");
                    entry.NextRetryAfter = DateTime.Now.AddSeconds(BACKOFF_SECONDS);
                    return;
                }

                _eventQueueManager.Dequeue(entryId);

                await DispatcherService.BeginInvoke(() =>
                {
                    _cardByEntryId.TryRemove(entryId, out _);
                    ViewModelProvider.Remove(card);
                    card.Dispose();
                });

                await _eventAggregator.PublishOnBackgroundThreadAsync(new SendActionRequestMessage
                {
                    EventId = eventId,
                    EventType = entry.EventType,
                    ActionDetails = content,
                    ActionUser = GetActorName(),
                    ActionTime = DateTime.Now,
                    OriginEvent = card.Model as IExEventModel,   // NATS from_event(device) 원천
                    ActionId = response.Data?.Id ?? 0            // 생성된 Action DB ID
                });

                _log?.Info($"AutoReport 완료: EventId({eventId}), EntryId({entryId})");
                }
                finally
                {
                    _reportGuard.Exit(eventId);
                }
            }
            catch (Exception ex)
            {
                _log?.Error($"AutoReport 예외: {ex.Message}");
                entry.NextRetryAfter = DateTime.Now.AddSeconds(BACKOFF_SECONDS);
            }
        }

        /// <summary>
        /// Fault 자동복구 핸들러 — EventQueueManager.OnAutoRecovery 구독용.
        /// 서버 API 조치보고 → UI 카드 제거 → NATS 발행
        /// </summary>
        public async Task HandleAutoRecoveryAsync(string faultEntryId)
        {
            try
            {
                if (!_cardByEntryId.TryGetValue(faultEntryId, out var card))
                {
                    _log?.Warning($"AutoRecovery: entryId({faultEntryId}) 카드 없음 — API 스킵");
                    return;
                }

                var eventModel = card.Model;
                if (eventModel == null) return;

                // (EC5) 동일 이벤트가 Batch/Auto 등 다른 경로에서 조치 진행 중이면 중복 스킵
                if (!_reportGuard.TryEnter(eventModel.Id))
                {
                    _log?.Info($"AutoRecovery: Event({eventModel.Id}) 조치보고 진행 중 — 중복 스킵");
                    return;
                }

                try
                {
                var dto = new ActionEventCreateDto
                {
                    User = _userModel.Name,
                    Content = "etc 자동복구",
                    FromEventId = eventModel.Id
                };

                var response = await _apiService.CreateActionEventAsync(dto);
                if (!response.Success)
                    _log?.Warning($"AutoRecovery API 실패: {response.Message}");

                await DispatcherService.BeginInvoke(() =>
                {
                    _cardByEntryId.TryRemove(faultEntryId, out _);
                    ViewModelProvider.Remove(card);
                    card.Dispose();
                });

                await _eventAggregator.PublishOnBackgroundThreadAsync(new SendActionRequestMessage
                {
                    EventId = eventModel.Id,
                    EventType = eventModel.MessageType,
                    ActionDetails = "etc 자동복구",
                    ActionUser = _userModel.Name,
                    ActionTime = DateTime.Now,
                    OriginEvent = eventModel as IExEventModel,   // NATS from_event(device) 원천
                    ActionId = response.Data?.Id ?? 0            // 생성된 Action DB ID
                });

                _log?.Info($"AutoRecovery 완료: Event({eventModel.Id}), Entry({faultEntryId})");
                }
                finally
                {
                    _reportGuard.Exit(eventModel.Id);
                }
            }
            catch (Exception ex)
            {
                _log?.Error($"AutoRecovery 실패: {ex.Message}");
            }
        }

        public async Task HandleAsync(DetectionReportedMessageModel message, CancellationToken cancellationToken)
        {
            try
            {
                if (message != null && message.ViewModel != null)
                {
                    var vm = message.ViewModel;
                    var model = vm.Model;
                    if (model == null) throw new NullReferenceException("DetectionEventModel을 찾을 수 없습니다.");

                    // 심볼 복원: _pendingEntries 폴백 후 Dequeue (FR-02)
                    if (vm.EntryId == null && vm.Model?.Id != null)
                    {
                        if (_pendingEntries.TryRemove(vm.Model.Id, out var fallbackEntryId))
                        {
                            vm.EntryId = fallbackEntryId;
                            _log?.Info($"EntryId 개별 탐지 폴백 매칭: Event({vm.Model.Id}) → Entry({fallbackEntryId})");
                        }
                    }
                    if (vm.EntryId != null)
                        _eventQueueManager.Dequeue(vm.EntryId);
                    else
                        _log?.Warning($"EntryId null — Dequeue 스킵: Event({vm.Model?.Id}), Device({vm.Model?.Device?.Id})");

                    await DispatcherService.BeginInvoke(() =>
                    {
                        if (vm.EntryId != null) _cardByEntryId.TryRemove(vm.EntryId, out _);
                        ViewModelProvider.Remove(vm);
                        vm.Dispose();
                    });
                }
            }
            catch (Exception ex)
            {
                _log?.Error(ex.Message);
            }
        }

        public async Task HandleAsync(MalfunctionReportedMessageModel message, CancellationToken cancellationToken)
        {
            try
            {
                if (message != null && message.ViewModel != null)
                {
                    var vm = message.ViewModel;
                    var model = vm.Model;
                    if (model == null) throw new NullReferenceException("MalfunctionEventModel을 찾을 수 없습니다.");

                    // 심볼 복원: _pendingEntries 폴백 후 Dequeue (FR-02)
                    if (vm.EntryId == null && vm.Model?.Id != null)
                    {
                        if (_pendingEntries.TryRemove(vm.Model.Id, out var fallbackEntryId))
                        {
                            vm.EntryId = fallbackEntryId;
                            _log?.Info($"EntryId 개별 장애 폴백 매칭: Event({vm.Model.Id}) → Entry({fallbackEntryId})");
                        }
                    }
                    if (vm.EntryId != null)
                        _eventQueueManager.Dequeue(vm.EntryId);
                    else
                        _log?.Warning($"EntryId null — Dequeue 스킵: Event({vm.Model?.Id}), Device({vm.Model?.Device?.Id})");

                    await DispatcherService.BeginInvoke(() =>
                    {
                        if (vm.EntryId != null) _cardByEntryId.TryRemove(vm.EntryId, out _);
                        ViewModelProvider.Remove(vm);
                        vm.Dispose();
                    });
                }
            }
            catch (Exception ex)
            {
                _log?.Error(ex.Message);
            }
        }

        #endregion
        #region - IHanldes -
        /// <summary>
        /// ConfirmPopup 확인 시 CallAllEventReportMessageModel을 수신하여 배치 처리 실행
        /// (ConfirmPopupDialogViewModel.ClickOk() → PublishOnCurrentThread(MessageModel) → 여기로 도달)
        /// </summary>
        public async Task HandleAsync(CallAllEventReportMessageModel message, CancellationToken cancellationToken)
        {
            // FR-PG-13 하위가드: ConfirmPopup 도달 후에도 CanControl 재확인 (역할 강등 경합 방어)
            if (!CanCtrlEvents())
            {
                _log?.Warning("[HandleAsync(CallAllEventReport)] 권한 없음 — 배치 조치보고 차단 (FR-PG-13)");
                return;
            }
            await ExecuteBatchReportAsync();
        }

        /// <summary>
        /// EventEntryEnqueuedMessage 수신 → eventId로 카드 검색하여 entryId 1:1 직접 매칭
        /// (PRD_EntryId_Nats_Uuid_DirectMatch FR-04)
        /// </summary>
        public Task HandleAsync(EventEntryEnqueuedMessage message, CancellationToken cancellationToken)
        {
            var card = ViewModelProvider.FirstOrDefault(c => c.Model?.Id == message.EventId && c.EntryId == null);
            if (card != null)
            {
                card.EntryId = message.EntryId;
                _cardByEntryId[message.EntryId] = card;
                _log?.Info($"EntryId 직접 매칭: Card({message.EventId}) → Entry({message.EntryId})");
            }
            else
            {
                // 카드가 아직 추가되지 않음 → 보류 큐에 저장
                _pendingEntries[message.EventId] = message.EntryId;
            }
            return Task.CompletedTask;
        }
        #endregion
        #region - Properties -
        public EventCardBaseViewModel SelectedEventCardViewModel
        {
            get { return _selectedEventCardViewModel; }
            set { _selectedEventCardViewModel = value; NotifyOfPropertyChange(() => SelectedEventCardViewModel); }
        }
        public event Action? UpdateAction;

        public bool IsAnimationEnabled
        {
            get { return _isAnimationEnabled; }
            set { _isAnimationEnabled = value; NotifyOfPropertyChange(() => IsAnimationEnabled); }
        }

        public bool IsVisible
        {
            get { return _isVisible; }
            set { _isVisible = value; NotifyOfPropertyChange(() => IsVisible); }
        }
        #endregion
        #region - Attributes -
        private const int ANIMATION_THRESHOLD = 20;
        private const int MAX_EVENT_CARDS = 500;   // (EB2) 표시 카드 하드 캡
        private const int BATCH_INTERVAL_MS = 150;
        private const string AUTO_REPORT_DETECTION   = "탐지 자동 조치보고";
        private const string AUTO_REPORT_MALFUNCTION = "이상 자동 조치보고";
        private const string AUTO_REPORT_DEFAULT     = "자동 조치보고";
        private const int BACKOFF_SECONDS = 30;

        private string GetActorName() =>
            string.IsNullOrWhiteSpace(_userModel?.Name) ? "SYSTEM" : _userModel.Name;
        private EventProviderService _providerService;
        private IAccountModel _userModel;
        private IEventApiService _apiService;
        private ISymbolEventManager _symbolEventManager;
        private IEventQueueManager _eventQueueManager;
        private EventCardBatchBuffer<EventCardBaseViewModel> _batchBuffer;
        private readonly ConcurrentDictionary<int, string> _pendingEntries = new();
        private readonly ConcurrentDictionary<string, EventCardBaseViewModel> _cardByEntryId = new();
        private readonly SemaphoreSlim _batchReportGate = new(1, 1);
        // (EC2/EC5 + Phase3) 조치보고 멱등 가드(싱글톤 IActionReportGuard). Auto/AutoRecovery/Batch 3경로 +
        // 수동 조치보고(EventCard.SendAction)가 같은 인스턴스를 공유 → 동일 EventId에 CreateActionEventAsync
        // 동시 호출(서버 중복 조치보고/NATS 중복발행)을 차단. 수동×자동 교차 중복 해소.
        private readonly IActionReportGuard _reportGuard;
        private Timer? _batchTimer;
        private EventCardBaseViewModel _selectedEventCardViewModel;
        private bool _isAnimationEnabled = true;
        private bool _isVisible = true;
        #endregion
    }
}