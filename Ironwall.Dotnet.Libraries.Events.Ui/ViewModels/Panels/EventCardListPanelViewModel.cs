using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
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
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Events;
using System;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
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
                                            , IHandle<DetectionThumbnailSyncedMessage>
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
        // 조치보고 = 서버 events:edit — ActionReportRules 참조(종전 control 은 운영자를 403 으로 보냈다).
        private bool CanCtrlEvents() => ActionReportRules.CanReport(ResolvePermissionService());

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
        /// 새 이벤트 카드를 목록에 올리는 <b>유일한 입구</b> — 호스트(NATS DETECT · MALFUNCTION)가 부른다(WP-1 ①).
        /// lock-free ConcurrentQueue에 적재하고, 150ms 타이머(FlushPendingCardsAsync)가 Background BeginInvoke로 묶어 올린 뒤
        /// 표시 상한(<see cref="MAX_EVENT_CARDS"/>)을 지킨다. 종전 호스트는 ViewModelProvider.Add 로 곧장 넣어 상한 · 묶음이 죽어 있었다.
        /// 어느 스레드에서 불러도 된다 — UI 스레드를 기다리지 않는다.
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

        /// <summary>아직 목록에 오르지 않고 묶음 버퍼에서 기다리는 카드 수.</summary>
        public int PendingCardCount => _batchBuffer.PendingCount;

        /// <summary>묶음 버퍼를 지금 비운다(시험 · 타이머 없는 헤드리스용). 타이머 틱과 같은 경로다.</summary>
        internal Task FlushPendingCardsNowAsync() => FlushPendingCardsAsync();

        /// <summary>
        /// 묶음 카드를 UI 스레드로 넘기는 길(기본 <see cref="DispatcherService.BeginInvoke(Action, DispatcherPriority)"/>).
        /// 시험이 "Background 로 넘긴 삽입보다 Normal 우선순위의 원격 조치보고가 먼저 처리되는" 디스패처 순서를 재현하려고 바꾼다(WP-8 M3).
        /// </summary>
        internal Func<Action, DispatcherPriority, Task> DispatchToUi { get; set; } = DispatcherService.BeginInvoke;

        private void CollectionEntity_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
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
            if (KeyOf(card) is not { } key) return;

            // 종류 + 번호로 맞춘다 — 탐지 7번의 엔트리를 장애 7번 카드가 가져가지 않게(WP-1 ②).
            if (_pendingEntries.TryRemove(key, out var entryId))
            {
                card.EntryId = entryId;
                _cardByEntryId[entryId] = card;
                _log?.Info($"EntryId 지연 매칭: Card({key.Kind} {key.EventId}) → Entry({entryId})");
            }
        }

        /// <summary>카드의 열쇠(종류 + 서버 번호). 탐지 · 장애 카드가 아니거나 번호가 없으면 <c>null</c>.</summary>
        private static (string Kind, int EventId)? KeyOf(EventCardBaseViewModel? card)
        {
            var kind = EventCardKind.Of(card);
            var id = card?.Model?.Id ?? 0;
            return kind is null || id <= 0 ? null : (kind, id);
        }

        /// <summary>목록에서 종류 + 번호가 같은 카드. UI 스레드에서 부를 것.</summary>
        private EventCardBaseViewModel? FindCard(string kind, int eventId)
            => ViewModelProvider.FirstOrDefault(c => c.Model?.Id == eventId && EventCardKind.Of(c) == kind);

        /// <summary>
        /// 이미 조치된 이벤트를 기억한다 — 카드가 아직 묶음 버퍼(150 ms)에 있을 때 조치보고가 먼저 닿으면
        /// 버퍼를 비울 때 그 카드를 올리지 않는다(뒤늦게 나타난 유령 카드 · 멈추지 않는 알람 방지).
        /// </summary>
        private void RememberClosed(string kind, int eventId)
        {
            lock (_closedGate)
            {
                if (!_closedKeys.Add((kind, eventId))) return;
                _closedOrder.Enqueue((kind, eventId));
                while (_closedOrder.Count > CLOSED_MEMORY) _closedKeys.Remove(_closedOrder.Dequeue());
            }
            RaiseActionReported(kind, eventId);
        }

        /// <summary>
        /// 이벤트가 조치보고로 닫혔다(종류 · 서버 번호) — 개별(카드 · 창 · 트레이 · 이력) · 전체 · 자동 · 자동복구 · 원격 ACTION_REPORT
        /// 모든 길이 지나는 <see cref="RememberClosed"/> 에서 <b>처음 한 번만</b> 발화한다(종류를 아는 원격 조치 포함, 카드가 없어도).
        /// 카메라 팝업 이벤트 창 닫기(camera-popup-modes FR-15, T-06)가 듣는다. 구독자 예외는 삼키고 로그만(FR-27).
        /// </summary>
        public event Action<string, int>? ActionReported;

        private void RaiseActionReported(string kind, int eventId)
        {
            var handlers = ActionReported;
            if (handlers is null) return;
            foreach (Action<string, int> h in handlers.GetInvocationList())
            {
                try { h(kind, eventId); }
                catch (Exception ex) { _log?.Error($"[EventCardList] ActionReported 구독자 실패: {ex.GetType().Name} {ex.Message}"); }
            }
        }

        private bool WasClosed(EventCardBaseViewModel card)
        {
            if (KeyOf(card) is not { } key) return false;
            lock (_closedGate) return _closedKeys.Contains(key);
        }

        /// <summary>이미 조치됐거나(종류 + 번호 기억) 목록에서 빠진 카드 — 전체 조치보고가 다시 보고하지 않는다(WP-8 M6). UI 스레드에서 부를 것.</summary>
        private bool IsClosedOrGone(EventCardBaseViewModel card) => WasClosed(card) || !ViewModelProvider.Contains(card);

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
                // 새 카드는 맨 위(0)에 들어온다 — 가장 오래된 카드는 맨 아래(WP-1 ⑲).
                var oldest = ViewModelProvider[^1];
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
                var drained = _batchBuffer.DrainQueue();
                if (drained.Count == 0) return;

                // 버퍼에 있는 동안 이미 조치된 카드는 올리지 않는다(원격 · 임시 카드 조치보고가 먼저 닿은 경우).
                var batch = new List<EventCardBaseViewModel>(drained.Count);
                foreach (var card in drained)
                {
                    if (WasClosed(card))
                    {
                        _log?.Info($"[EnqueueCard] 이미 조치된 이벤트라 올리지 않음: {KeyOf(card)}");
                        card.Dispose();
                        continue;
                    }
                    batch.Add(card);
                }
                if (batch.Count == 0) return;

                // 적응형 간격 조정
                var newInterval = _batchBuffer.CalculateInterval(drained.Count);
                _batchTimer?.Change(newInterval, newInterval);

                await DispatchToUi(() =>
                {
                    // (WP-8 M3) UI 스레드에서 한 번 더 거른다 — 위 검사(타이머 스레드)와 이 Background 삽입 사이에 Normal 우선순위의
                    //   원격 ACTION_REPORT 가 먼저 처리되면, 카드가 없어 엔트리만 빼고 보류 EntryId 를 지운다. 여기서 거르지 않으면
                    //   카드가 뒤늦게 올라와 영영 남았다(EntryId 없음 → 자동 조치보고 · 원격 해제 모두 못 닿음). 원격 종결은 UI 스레드라 경합이 없다.
                    var inserted = new List<EventCardBaseViewModel>(batch.Count);
                    foreach (var card in batch)
                    {
                        if (WasClosed(card))
                        {
                            _log?.Info($"[EnqueueCard] 삽입 직전 이미 조치된 이벤트라 올리지 않음: {KeyOf(card)}");
                            card.Dispose();
                            continue;
                        }
                        // (GAP-C4) 같은 이벤트(종류 + 서버 번호)는 한 장 — 매니저가 봉투 id 만 바꿔 다시 보내면 호스트의 봉투 기억을 지나
                        //   두 번째 카드가 떴다(헤디드 r18-e1 EVT-E2E-070). 목록에 이미 있거나 이 묶음에 먼저 든 카드가 있으면 버린다.
                        if (KeyOf(card) is { } key
                            && (FindCard(key.Kind, key.EventId) is not null || inserted.Any(c => KeyOf(c) == key)))
                        {
                            _log?.Info($"[EnqueueCard] 같은 이벤트 카드가 이미 있어 올리지 않음(재전송): {key}");
                            card.Dispose();
                            continue;
                        }
                        inserted.Add(card);
                    }
                    if (inserted.Count == 0) return;

                    // CollectionChanged 서스펜드 → 배치 Add → 재등록 (핸들러 폭주 방지)
                    ViewModelProvider.CollectionChanged -= CollectionEntity_CollectionChanged;
                    try
                    {
                        // 최신이 맨 위 — 종전엔 맨 아래에 붙고 스크롤이 따라가지 않아 새 이벤트가 화면 밖에 떴다(WP-1 ⑲).
                        foreach (var card in inserted)
                            ViewModelProvider.Insert(0, card);
                    }
                    finally
                    {
                        ViewModelProvider.CollectionChanged += CollectionEntity_CollectionChanged;
                        // 배치 entryId 매칭
                        foreach (var card in inserted)
                            TryAssignEntryId(card);
                        // (EB2) 표시 카드 하드 캡 — 장시간 운용 시 무한 증가 방지 (핸들러 활성 상태에서 제거)
                        EnforceDisplayCap();
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
                    Explain = ActionReportRules.NO_PERMISSION_TEXT
                });
                return;
            }
            await _eventAggregator.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel() { Title = "전체 조치보고", Explain = "전체 조치보고를 수행하시겠습니까?", MessageModel = new CallAllEventReportMessageModel() });
        }

        /// <summary>
        /// 카드 더블클릭 — 그 카드를 고르고 장비를 지도에서 보인다(WP-1 ⑪). 카드 안의 단추(조치보고 · 뒤집기)를 두 번 누른 것은 무시한다.
        /// </summary>
        public async void OnClickEventCard(object sender, RoutedEventArgs e)
        {
            try
            {
                if (IsInsideButton(e?.OriginalSource as DependencyObject)) return;
                var card = (e?.OriginalSource as FrameworkElement)?.DataContext as EventCardBaseViewModel
                           ?? (sender as ListBox)?.SelectedItem as EventCardBaseViewModel;
                await LocateCardAsync(card);
            }
            catch (Exception ex)
            {
                _log?.Error($"[EventCardList] 카드 더블클릭 처리 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 카드를 고르고(<see cref="SelectedEventCardViewModel"/>) 그 장비를 지도에서 보이라고 요청한다 — 부대 콘솔 · 심볼 상세의
        /// [지도에서 보기] 와 같은 메시지(<see cref="MapLocateRequest"/>)라 지도가 강조 고리 · 맞춤을 한다.
        /// 장비가 없는 카드(지워진 장비)는 고르기만 한다.
        /// </summary>
        /// <returns>지도 요청을 보냈으면 <c>true</c>.</returns>
        public async Task<bool> LocateCardAsync(EventCardBaseViewModel? card)
        {
            if (card == null) return false;
            SelectedEventCardViewModel = card;

            var device = card.Model?.Device;
            var title = string.IsNullOrWhiteSpace(device?.DeviceName) ? $"이벤트 {card.Model?.Id}" : device!.DeviceName!;
            var request = MapLocateRequest.For(title, device is null ? null : new[] { device.Id });
            if (request is null)
            {
                _log?.Info($"[EventCardList] 지도에서 보기 생략 — 장비 없음: {KeyOf(card)}");
                return false;
            }
            if (_eventAggregator is null) return false;
            await _eventAggregator.PublishOnCurrentThreadAsync(request);
            return true;
        }

        private static bool IsInsideButton(DependencyObject? source)
        {
            for (var node = source; node is not null and not ListBoxItem; node = SafeParent(node))
                if (node is ButtonBase) return true;
            return false;

            static DependencyObject? SafeParent(DependencyObject node)
                => node is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);
        }


        public async void OnButtonAction(object sender, RoutedEventArgs e)
        {
            // FR-EN-10 ACK 개별 게이트 (CanControl)
            if (!CanCtrlEvents())
            {
                await _eventAggregator.PublishOnUIThreadAsync(new OpenInfoPopupMessageModel
                {
                    Title = "권한 없음",
                    Explain = ActionReportRules.NO_PERMISSION_TEXT
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
                _log?.Warning("[ExecuteBatchReportAsync] 권한 없음 — events:edit 미보유");
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
                        // (WP-8 M6) 목록은 시작할 때 한 번 베꼈다 — 앞 카드를 보고하는(await) 사이 자동 · 원격 · 개별 조치로 이미 닫힌 카드는
                        //   다시 보고하지 않는다(종전: 서버 조치 중복 생성 + ACTION_REPORT 중복 + 이중 정리). 가드를 잡은 뒤에 보므로
                        //   자동 경로가 끝내고 가드를 푼 카드도 여기서 걸린다.
                        if (IsClosedOrGone(card))
                        {
                            _log?.Info($"배치 조치보고: {KeyOf(card)} 이미 닫힘 — 건너뜀");
                            continue;
                        }
                        // ① 서버 API로 조치보고 생성 (보고자: Username(EmployeeNumber) 한 모양, Content: "일괄처리" 고정)
                        var actor = ActionReportRules.FormatActor(_userModel);
                        var dto = new ActionEventCreateDto
                        {
                            User = actor,
                            Content = "일괄처리",
                            FromEventId = eventId
                        };

                        var response = await _apiService.CreateActionEventAsync(dto);
                        if (!response.Success)
                            throw new Exception($"처리 중 장애가 발생했습니다.\n{response.Message}");

                    // (WP-8 M6) 보고를 기다리는 사이 다른 길(원격 ACTION_REPORT 등)이 이미 닫았으면 정리(②③)는 그쪽이 끝냈다 —
                    //   두 번 Dequeue · 두 번 치우지 않는다. 서버 조치는 이미 만들어졌으므로 ACTION_REPORT(④)는 사실대로 낸다.
                    if (IsClosedOrGone(card))
                    {
                        _log?.Info($"배치 조치보고: {KeyOf(card)} 보고 중 다른 경로로 닫힘 — 정리 생략");
                    }
                    else
                    {
                    // ② 심볼 복원: EntryId 폴백 체인(종류까지 본다) → 엔트리가 없으면 심볼 재계산 (FR-01 · FR-03)
                    ReleaseQueueAndSymbol(card);

                    // ③ 제거 + Dispose
                    if (KeyOf(card) is { } closedKey) RememberClosed(closedKey.Kind, closedKey.EventId);
                    RemoveCard(card);
                    }

                    // ④ NATS로 조치보고 발행 (NatsDomainService 경유)
                    await _eventAggregator.PublishOnBackgroundThreadAsync(ActionReportMessages.Create(
                        response, eventModel?.Id ?? 0, eventModel?.MessageType ?? EnumEventType.Intrusion,
                        "일괄처리", actor, eventModel as IExEventModel));

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
                // API 실패 시 즉시 중단하고 에러 팝업 표시 (남은 카드는 유지).
                // 예외 원문(스택 추적 · 서버 메시지)은 로그로만 — 팝업에는 무엇이 안 됐고 어떻게 하면 되는지만(완성도 수정 패스).
                _log?.Error($"[EventCardList] 전체 조치보고 중단: {ex}");
                await _eventAggregator.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                {
                    Title = "전체 조치보고 오류",
                    Explain = "조치보고를 보내는 중에 멈췄습니다. 보내지 못한 카드는 그대로 남아 있으니 잠시 뒤 다시 [전체 조치보고]를 누르세요."
                });
            }
            finally
            {
                _batchReportGate.Release();
                ViewModelProvider.CollectionChanged += CollectionEntity_CollectionChanged;
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
                    if (KeyOf(card) is { } key) RememberClosed(key.Kind, key.EventId);
                    _cardByEntryId.TryRemove(entryId, out _);
                    ViewModelProvider.Remove(card);
                    card.Dispose();
                });

                await _eventAggregator.PublishOnBackgroundThreadAsync(ActionReportMessages.Create(
                    response, eventId, entry.EventType, content, GetActorName(), card.Model as IExEventModel));

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
        /// 큐 엔트리의 카드 — 목록에 오른 카드(<see cref="_cardByEntryId"/>) 또는 아직 묶음 버퍼에서 기다리는 카드
        /// (보류 표 <see cref="_pendingEntries"/> 의 (종류, 번호) 로 버퍼 사본에서 찾는다). 못 찾으면 UI 스레드에 줄 선 일
        /// (Background 로 넘긴 EventEntryEnqueuedMessage · 묶음 삽입)을 한 번 보낸 뒤 다시 본다. 없으면 <c>null</c>.
        /// </summary>
        private async Task<EventCardBaseViewModel?> FindCardForEntryAsync(string entryId)
        {
            if (FindCardForEntry(entryId) is { } card) return card;
            await DispatcherService.BeginInvoke(() => { }, DispatcherPriority.Background);
            return FindCardForEntry(entryId);
        }

        private EventCardBaseViewModel? FindCardForEntry(string entryId)
        {
            if (_cardByEntryId.TryGetValue(entryId, out var shown)) return shown;
            foreach (var pending in _pendingEntries)
            {
                if (pending.Value != entryId) continue;
                return _batchBuffer.Snapshot().FirstOrDefault(c => KeyOf(c) == pending.Key);
            }
            return null;
        }

        /// <summary>
        /// Fault 자동복구 핸들러 — EventQueueManager.OnAutoRecovery 구독용.
        /// 서버 API 조치보고 → <b>성공했을 때만</b> UI 카드 제거 → NATS 발행.
        /// 종전엔 API 가 실패해도 카드를 지우고 ActionId=0 인 ACTION_REPORT 를 내보냈다 — 서버엔 조치가 없는데 다른 GIS 의 카드까지 닫혔다(WP-1 ⑤).
        /// 실패하면 카드는 남는다(큐 엔트리는 자동복구가 이미 뺐으므로 사람이 조치보고하면 심볼은 큐 실제 상태로 다시 계산된다).
        /// </summary>
        public async Task HandleAutoRecoveryAsync(string faultEntryId)
        {
            try
            {
                // 표시된 카드뿐 아니라 아직 묶음 버퍼(150 ms)에 있는 카드도 찾는다 — 장애 → 곧바로(30 ms) 같은 장비 탐지면
                //   카드가 아직 목록에 없어 "카드 없음 — API 스킵" 으로 조치보고가 영영 나가지 않았다(프로브 S28.30ms 회귀).
                var card = await FindCardForEntryAsync(faultEntryId);
                if (card is null)
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
                var actor = GetActorName();
                var dto = new ActionEventCreateDto
                {
                    User = actor,
                    Content = "etc 자동복구",
                    FromEventId = eventModel.Id
                };

                var response = await _apiService.CreateActionEventAsync(dto);
                if (!response.Success)
                {
                    // 서버에 조치가 없다 — 카드를 지우거나 ACTION_REPORT 를 내보내면 거짓이 된다.
                    _log?.Warning($"AutoRecovery API 실패 — 카드 유지 · NATS 미발행: Event({eventModel.Id}), {response.Message}");
                    return;
                }

                await DispatcherService.BeginInvoke(() =>
                {
                    if (KeyOf(card) is { } key)
                    {
                        RememberClosed(key.Kind, key.EventId);   // 아직 버퍼면 비울 때 올리지 않고 치운다(WP-1 ④ 와 같은 규칙)
                        _pendingEntries.TryRemove(new KeyValuePair<(string Kind, int EventId), string>(key, faultEntryId));
                    }
                    _cardByEntryId.TryRemove(faultEntryId, out _);
                    // 목록에 있었으면 여기서 치우고, 버퍼에 있으면 FlushPendingCardsAsync 가 WasClosed 로 걸러 치운다(두 번 Dispose 하지 않게).
                    if (ViewModelProvider.Remove(card)) card.Dispose();
                });

                await _eventAggregator.PublishOnBackgroundThreadAsync(ActionReportMessages.Create(
                    response, eventModel.Id, eventModel.MessageType, "etc 자동복구", actor, eventModel as IExEventModel));

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

        /// <summary>
        /// (C-2) 원격 ACTION_REPORT 수신 — 다른 GIS · 서브시스템이 조치한 이벤트를 이 GIS 에서도 푼다.
        /// <list type="number">
        ///   <item>목록에 <b>종류 + 번호</b>가 같은 카드가 있으면 닫는다(큐 엔트리 Dequeue → 심볼 복원). 종전엔 번호만 봐서
        ///     탐지 7번 조치보고가 장애 7번 카드를 닫을 수 있었다(WP-1 ②).</item>
        ///   <item>카드가 없어도(이미 다른 경로로 닫힘 · 표시 상한으로 밀려남) 큐에 엔트리가 남았으면 뺀다.</item>
        ///   <item>엔트리도 없으면 장비 · 그룹 심볼을 큐 실제 상태로 다시 계산한다 — 개별 조치보고의 복원과 같은 길(WP-1 ③).</item>
        ///   <item>열린 조치보고 창이 같은 이벤트면 [확인]을 끄라고 알린다(<see cref="RemoteActionReportedMessage"/>).</item>
        /// </list>
        /// 종류를 모르면(<paramref name="kind"/> = null) 번호가 하나의 카드로만 정해질 때 그 카드를 닫고, 둘 이상이면 아무것도 닫지 않는다.
        /// 자기 발행분의 메아리는 이미 닫혀 있어 <see cref="RemoteActionReportOutcome.Nothing"/> 이다(멱등).
        /// ⚠ ViewModelProvider를 변경하므로 UI 스레드에서 호출할 것(호출부 NatsDomainService가 DispatcherService.Invoke로 감쌈).
        /// </summary>
        /// <param name="kind"><see cref="ActionReportKind"/> 값 또는 <c>null</c>(모름).</param>
        /// <param name="eventId">원본 이벤트 서버 id(<c>from_event.id</c>).</param>
        /// <param name="eventType">원본 유형(<c>from_event.type_event</c>) — 큐 엔트리를 찾는 열쇠. 모르면 종류의 기본 유형.</param>
        /// <param name="device">원본 장비(DeviceProvider 에서 찾은 것) — 엔트리가 없을 때 심볼을 다시 계산할 대상. 없으면 생략.</param>
        public RemoteActionReportOutcome CloseByRemoteActionReport(string? kind, int eventId, EnumEventType? eventType = null, IBaseDeviceModel? device = null)
        {
            if (eventId <= 0) return RemoteActionReportOutcome.Invalid;

            if (kind is null)
            {
                var sameId = ViewModelProvider.Where(c => c.Model?.Id == eventId && EventCardKind.Of(c) is not null).ToList();
                if (sameId.Count > 1)
                {
                    _log?.Warning($"[ACTION_REPORT] 종류를 모르는데 번호 {eventId} 카드가 {sameId.Count}장 — 잘못 닫지 않으려고 건너뜀");
                    return RemoteActionReportOutcome.Ambiguous;
                }
                kind = sameId.Count == 1 ? EventCardKind.Of(sameId[0]) : null;
            }

            if (kind is not null)
            {
                RememberClosed(kind, eventId);
                _ = NotifyRemoteReportAsync(new RemoteActionReportedMessage(kind, eventId));
            }

            var card = kind is null ? null : FindCard(kind, eventId);
            if (card is not null)
            {
                ReleaseQueueAndSymbol(card, allowDeviceFallback: false);   // 번호로 못 찾으면 지우지 않고 재계산만(⑮)
                RemoveCard(card);
                _log?.Info($"[ACTION_REPORT] 원격 조치보고로 카드 종결: {kind} {eventId}");
                return RemoteActionReportOutcome.CardClosed;
            }

            // 카드가 없다 — 큐에 남은 엔트리를 빼거나, 그것도 없으면 심볼을 큐 실제 상태로 다시 계산한다.
            var type = eventType ?? EventCardKind.EventTypeOf(kind, null);
            var entry = type is EnumEventType t ? _eventQueueManager.FindEntryByEventId(eventId, t) : null;
            if (entry?.EntryId is { } entryId)
            {
                _eventQueueManager.Dequeue(entryId);
                _pendingEntries.TryRemove((kind ?? EventCardKind.Of(entry.EventType), eventId), out _);
                _log?.Info($"[ACTION_REPORT] 카드 없음 — 큐 엔트리 해제: {kind} {eventId} → Entry({entryId})");
                return RemoteActionReportOutcome.QueueEntryCleared;
            }

            if (device is not null)
            {
                RefreshSymbols(device);
                _log?.Info($"[ACTION_REPORT] 카드 · 엔트리 없음 — 심볼 재계산: {kind} {eventId}, Device({device.Id},{device.DeviceType})");
                return RemoteActionReportOutcome.SymbolRefreshed;
            }

            return RemoteActionReportOutcome.Nothing;   // 멱등: 이미 종결됐거나 이 GIS 가 모르는 이벤트
        }

        private async Task NotifyRemoteReportAsync(RemoteActionReportedMessage message)
        {
            try
            {
                if (_eventAggregator is not null) await _eventAggregator.PublishOnCurrentThreadAsync(message);
            }
            catch (Exception ex)
            {
                _log?.Warning($"[ACTION_REPORT] 열린 조치보고 창 알림 실패: {ex.Message}");
            }
        }

        /// <summary>카드를 목록에서 빼고 치운다. UI 스레드에서 부를 것.</summary>
        private void RemoveCard(EventCardBaseViewModel card)
        {
            if (card.EntryId != null) _cardByEntryId.TryRemove(card.EntryId, out _);
            ViewModelProvider.Remove(card);
            card.Dispose();
        }

        /// <summary>장비 · 소속 그룹 심볼을 큐(EQM) 실제 상태로 다시 계산한다(맹목 Normal 아님 — 남은 이벤트는 보존).</summary>
        private void RefreshSymbols(IBaseDeviceModel device)
        {
            _symbolEventManager.RefreshDeviceSymbol(device.Id, device.DeviceType);
            if (device.DeviceGroups != null)
                foreach (var g in device.DeviceGroups)
                    _symbolEventManager.RefreshGroupSymbol(g);
        }

        /// <summary>
        /// 조치보고 시 심볼/EQM 상태 정리 (FR-03). EntryId 폴백 체인: 카드 EntryId → 보류 표(종류 + 번호) → EQM 실엔트리(번호 + 유형)
        /// → 장비의 가장 오래된 엔트리(<b>같은 종류일 때만</b>). 엔트리를 찾으면 Dequeue(N→0 전이 → 그룹/개별 심볼 복원),
        /// 아예 없으면(그리드 일시 카드 / 부팅 전 장애) 심볼을 EQM 실제 상태로 **재계산 복원**. vm.EntryId를 확정 세팅.
        /// 개별 · 전체 · 원격 조치보고가 모두 이 한 길을 쓴다.
        /// </summary>
        /// <param name="allowDeviceFallback">
        /// 번호로 엔트리를 못 찾았을 때 '그 장비의 가장 오래된 같은 종류 엔트리'를 뺄지. 원격 조치보고는 <c>false</c> —
        /// 남이 조치한 이벤트 대신 이 GIS 의 다른 활성 이벤트를 지울 수 있다(WP-1 ⑮). 그때는 심볼만 큐 실제 상태로 다시 계산한다.
        /// </param>
        private void ReleaseQueueAndSymbol(EventCardBaseViewModel vm, bool allowDeviceFallback = true)
        {
            var model = vm.Model;
            var kind = EventCardKind.Of(vm);
            if (vm.EntryId == null && model != null)
            {
                if (kind is not null && _pendingEntries.TryRemove((kind, model.Id), out var pendingId))
                {
                    vm.EntryId = pendingId;
                    _log?.Info($"EntryId 개별 폴백 매칭: {kind} {model.Id} → Entry({pendingId})");
                }
                else
                {
                    // 그리드 조치보고=일시 카드 인스턴스라 EntryId 미할당 — 실제 EQM 엔트리 직접 조회(장애/탐지 독립 id → Type 판별).
                    var entry = _eventQueueManager.FindEntryByEventId(model.Id, model.MessageType);
                    if (entry == null && allowDeviceFallback && model.Device != null)
                    {
                        var byDevice = _eventQueueManager.FindEntryByDevice(model.Device.Id, model.Device.DeviceType);
                        // 장비 기준 폴백은 종류가 같을 때만 — 탐지 조치보고가 같은 장비의 장애 엔트리를 빼면 안 된다.
                        if (byDevice != null && (kind is null || EventCardKind.Of(byDevice.EventType) == kind)) entry = byDevice;
                    }
                    if (entry?.EntryId != null)
                    {
                        vm.EntryId = entry.EntryId;
                        _log?.Info($"조치보고 EQM 엔트리 직접 매칭(EntryId null 폴백): {kind} {model.Id} → Entry({entry.EntryId})");
                    }
                }
            }

            if (vm.EntryId != null)
            {
                _eventQueueManager.Dequeue(vm.EntryId);   // N→0 전이 시 그룹/개별 심볼 복원
            }
            else if (model?.Device != null)
            {
                // EQM 엔트리 자체가 없음(부팅 전 장애 등) — Dequeue 대상 부재. 심볼을 EQM 실제 상태로 재계산 복원.
                RefreshSymbols(model.Device);
                _log?.Warning($"EQM 엔트리 부재 — 심볼 재계산 복원(EntryId null): {kind} {model.Id}, Device({model.Device.Id})");
            }
            else
            {
                _log?.Warning($"EntryId·EQM 엔트리·Device 모두 부재 — 심볼 복원 스킵: {kind} {model?.Id}");
            }
        }

        public Task HandleAsync(DetectionReportedMessageModel message, CancellationToken cancellationToken)
            => HandleReportedAsync(message?.ViewModel);

        public Task HandleAsync(MalfunctionReportedMessageModel message, CancellationToken cancellationToken)
            => HandleReportedAsync(message?.ViewModel);

        /// <summary>
        /// 사람의 조치보고가 서버에 들어갔다 — 카드를 닫는다.
        /// 트레이 · 목록 우클릭 · 이력 창은 <b>임시 카드</b>로 보고한다(§8-3) — 그 임시 카드는 목록에 없으므로
        /// 목록에서 종류 + 번호가 같은 <b>진짜 카드</b>를 찾아 닫는다. 종전엔 임시 카드만 치워 진짜 카드가
        /// NATS 메아리가 올 때까지(안 오면 영영) 남고 그 알람도 계속 울렸다(WP-1 ④).
        /// </summary>
        private async Task HandleReportedAsync(EventCardBaseViewModel? vm)
        {
            try
            {
                if (vm == null) return;
                if (vm.Model == null) throw new NullReferenceException($"{vm.GetType().Name} 의 이벤트 모델을 찾을 수 없습니다.");

                await DispatcherService.BeginInvoke(() =>
                {
                    var key = KeyOf(vm);
                    var target = ViewModelProvider.Contains(vm) || key is null ? vm : FindCard(key.Value.Kind, key.Value.EventId) ?? vm;
                    if (key is { } k) RememberClosed(k.Kind, k.EventId);

                    // 심볼/EQM 상태 정리 — 진짜 카드의 EntryId 가 있으면 그것으로(가장 정확), 없으면 폴백 체인 (FR-03)
                    ReleaseQueueAndSymbol(target);

                    RemoveCard(target);
                    if (!ReferenceEquals(target, vm))
                    {
                        vm.Dispose();
                        _log?.Info($"임시 카드 조치보고 → 목록의 진짜 카드 종결: {key}");
                    }
                });
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
            // 종류 + 번호로 맞춘다 — 서버는 탐지 · 장애를 따로 번호 매긴다(WP-1 ②).
            var kind = EventCardKind.Of(message.EventType);
            var card = ViewModelProvider.FirstOrDefault(c => c.Model?.Id == message.EventId && EventCardKind.Of(c) == kind && c.EntryId == null);
            if (card != null)
            {
                card.EntryId = message.EntryId;
                _cardByEntryId[message.EntryId] = card;
                _log?.Info($"EntryId 직접 매칭: Card({kind} {message.EventId}) → Entry({message.EntryId})");
            }
            else
            {
                // 카드가 아직 추가되지 않음(묶음 버퍼에 있음) → 보류 큐에 저장
                _pendingEntries[(kind, message.EventId)] = message.EntryId;
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// SYNC_DETECTION{UPDATED} 재조회 결과 수신 → EventId로 활성 탐지 카드를 찾아 썸네일 in-place 갱신.
        /// (PTZ 회전 후 썸네일) 카드가 없으면(이미 조치/미표시) 멱등 no-op. DetectionSyncNatsService가 UI 스레드에서 발행.
        /// </summary>
        public Task HandleAsync(DetectionThumbnailSyncedMessage message, CancellationToken cancellationToken)
        {
            // 타입 우선 필터(OfType) 필수 — ViewModelProvider는 탐지·장애 카드 혼재 컬렉션이고
            // 탐지/장애는 독립 id 시퀀스라 숫자 Id 충돌 시 FirstOrDefault가 장애 카드를 먼저 잡아
            // as 캐스트 null → 실제 탐지 카드가 뒤에 있어도 조용히 유실됨(Undo id충돌 선례와 동일).
            var card = ViewModelProvider.OfType<DetectionEventCardViewModel>()
                                        .FirstOrDefault(c => c.Model?.Id == message.EventId);
            if (card == null)
                return Task.CompletedTask;   // 멱등: 이미 종결됐거나 본 패널에 없는 탐지

            card.ApplyThumbnailUpdate(message.Thumbnail, message.FrameWidth, message.FrameHeight);
            _log?.Info($"[SYNC_DETECTION] 탐지 카드 썸네일 갱신: Event({message.EventId}), frame={message.FrameWidth}x{message.FrameHeight}");
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

        public bool IsVisible
        {
            get { return _isVisible; }
            set { _isVisible = value; NotifyOfPropertyChange(() => IsVisible); }
        }
        #endregion
        #region - Attributes -
        private const int MAX_EVENT_CARDS = 500;   // (EB2) 표시 카드 하드 캡
        private const int CLOSED_MEMORY = 256;     // 이미 조치된 (종류, 번호) 기억 개수 — 묶음 버퍼 150 ms 창을 넉넉히 덮는다
        private const int BATCH_INTERVAL_MS = 150;
        private const string AUTO_REPORT_DETECTION   = "탐지 자동 조치보고";
        private const string AUTO_REPORT_MALFUNCTION = "이상 자동 조치보고";
        private const string AUTO_REPORT_DEFAULT     = "자동 조치보고";
        private const int BACKOFF_SECONDS = 30;

        /// <summary>자동 경로의 보고자 — 조치보고 창과 같은 <c>Username(EmployeeNumber)</c> 모양, 로그인한 사람이 없으면 SYSTEM(WP-1 ⑬).</summary>
        private string GetActorName() => ActionReportRules.FormatActor(_userModel);
        private EventProviderService _providerService;
        private IAccountModel _userModel;
        private IEventApiService _apiService;
        private ISymbolEventManager _symbolEventManager;
        private IEventQueueManager _eventQueueManager;
        private EventCardBatchBuffer<EventCardBaseViewModel> _batchBuffer;
        // 카드가 아직 없을 때 먼저 온 큐 엔트리 — 열쇠는 (종류, 서버 번호). 탐지 · 장애 번호는 서로 겹친다(WP-1 ②).
        private readonly ConcurrentDictionary<(string Kind, int EventId), string> _pendingEntries = new();
        // 이미 조치된 (종류, 번호) — 묶음 버퍼에 있던 카드가 조치 뒤에 떠오르지 않게(RememberClosed/WasClosed).
        private readonly object _closedGate = new();
        private readonly HashSet<(string Kind, int EventId)> _closedKeys = new();
        private readonly Queue<(string Kind, int EventId)> _closedOrder = new();
        private readonly ConcurrentDictionary<string, EventCardBaseViewModel> _cardByEntryId = new();
        private readonly SemaphoreSlim _batchReportGate = new(1, 1);
        // (EC2/EC5 + Phase3) 조치보고 멱등 가드(싱글톤 IActionReportGuard). Auto/AutoRecovery/Batch 3경로 +
        // 수동 조치보고(EventCard.SendAction)가 같은 인스턴스를 공유 → 동일 EventId에 CreateActionEventAsync
        // 동시 호출(서버 중복 조치보고/NATS 중복발행)을 차단. 수동×자동 교차 중복 해소.
        private readonly IActionReportGuard _reportGuard;
        private Timer? _batchTimer;
        private EventCardBaseViewModel _selectedEventCardViewModel;
        private bool _isVisible = true;
        #endregion
    }
}
