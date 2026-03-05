using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Nats.Models;
using NATS.Client.Core;
using System.Threading.Channels;

namespace Ironwall.Dotnet.Libraries.Nats.Services;

/****************************************************************************
    Purpose      : NATS 메시지 서비스 추상 클래스 (공통 로직 포함)
    Created By   : GHLee
    Created On   : 10/28/2025
    Department   : SW Team
    Company      : Sensorway Co., Ltd.
    Email        : lsirikh@naver.com
 ****************************************************************************/

/// <summary>
/// NATS 메시지 서비스의 공통 로직을 제공하는 추상 클래스
/// </summary>
/// <typeparam name="T">구체적인 서비스 타입</typeparam>
public abstract class MessageService<T> : IMessageService<T>
{
    #region - Ctors -
    protected MessageService()
    {
    }

    protected MessageService(ILogService log) => _log = log;
    #endregion

    #region - Implementation of Interface -
    public async Task ExecuteAsync(CancellationToken token = default)
    {
        await RegisterSubscribers(token);
    }

    public async Task StopAsync(CancellationToken token = default)
    {
        await UnregisterSubscribersAsync();
    }
    #endregion

    #region - Processes -
    /// <summary>
    /// Subject 구독을 등록합니다 (와일드카드 지원, 복수 Subject 병렬 구독)
    /// </summary>
    protected virtual async Task RegisterSubscribers(CancellationToken token = default)
    {
        try
        {
            _log?.Info($"[RegisterSubscribers] Called - Connection: {Connection != null}, Subject: {_defaultSubject}");

            if (Connection == null || string.IsNullOrEmpty(_defaultSubject))
            {
                _log?.Warning("[RegisterSubscribers] Connection or Subject is null - skipping subscription.");
                return;
            }

            // 구독할 전체 subject 목록 (primary + additional)
            var subjects = new List<string> { _defaultSubject };
            if (_additionalSubjects.Count > 0)
                subjects.AddRange(_additionalSubjects);

            _log?.Info($"[RegisterSubscribers] Starting subscription to {subjects.Count} subject(s): {string.Join(", ", subjects)}");

            foreach (var subject in subjects)
            {
                var capturedSubject = subject;
                var task = Task.Run(async () =>
                {
                    try
                    {
                        _log?.Info($"[SubscriptionTask] Starting loop for '{capturedSubject}'");
                        await foreach (var msg in Connection.SubscribeAsync<string>(capturedSubject, cancellationToken: token))
                        {
                            try
                            {
                                var data = msg.Data ?? string.Empty;
                                _log?.Info($"[SubscriptionTask] Received from '{msg.Subject}': {data}");
                                await OnNatsSubscribeEventAsync(new MessageArgsModel(msg.Subject, capturedSubject, data));
                            }
                            catch (Exception ex)
                            {
                                _log?.Error($"[SubscriptionTask] Error processing message: {ex.Message}");
                            }
                        }
                        _log?.Info($"[SubscriptionTask] Loop ended for '{capturedSubject}'");
                    }
                    catch (OperationCanceledException)
                    {
                        _log?.Info($"[SubscriptionTask] Subscription cancelled for '{capturedSubject}'");
                    }
                    catch (Exception ex)
                    {
                        _log?.Error($"[SubscriptionTask] Subscription error for '{capturedSubject}': {ex.Message}");
                    }
                }, token);

                _subscriptionTasks.Add(task);
            }

            // 구독이 준비될 때까지 잠시 대기
            await Task.Delay(100, token);

            _log?.Info($"[RegisterSubscribers] All subscriptions started.");
        }
        catch (Exception ex)
        {
            _log?.Error($"[RegisterSubscribers] Exception: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 비동기 이벤트 핸들러 실행
    /// </summary>
    protected virtual async Task OnNatsSubscribeEventAsync(MessageArgsModel e)
    {
        _log?.Info($"[OnNatsSubscribeEventAsync] Invoking event handlers for subject: {e.Subject}");

        // 동기 이벤트 핸들러 실행
        NatsSubscribeEvent?.Invoke(this, e);

        // 비동기 이벤트 핸들러 순차 실행
        if (NatsSubscribeEventAsync != null)
        {
            var handlers = NatsSubscribeEventAsync.GetInvocationList();
            _log?.Info($"[OnNatsSubscribeEventAsync] Found {handlers.Length} async handlers");

            foreach (var handler in handlers)
            {
                await ((Func<MessageArgsModel, Task>)handler)(e);
            }
        }
        else
        {
            _log?.Warning("[OnNatsSubscribeEventAsync] No async event handlers registered");
        }
    }

    /// <summary>
    /// 구독 해제 및 연결 종료
    /// </summary>
    protected virtual async Task UnregisterSubscribersAsync()
    {
        try
        {
            _log?.Info("[MessageService] Disposing NATS Connection...");

            // 모든 구독 Task 완료 대기
            foreach (var task in _subscriptionTasks)
            {
                try
                {
                    await task;
                }
                catch (OperationCanceledException)
                {
                    // 정상적인 취소
                }
                catch (Exception ex)
                {
                    _log?.Error($"[MessageService] Subscription task error: {ex.Message}");
                }
            }
            _subscriptionTasks.Clear();

            // Connection 종료
            if (Connection != null)
            {
                await Connection.DisposeAsync();
                Connection = null;
            }

            _log?.Info("[MessageService] NATS Connection disposed successfully.");
        }
        catch (Exception ex)
        {
            _log?.Error($"[MessageService] Dispose error: {ex.Message}");
        }
    }

    /// <summary>
    /// 연결 로직 (자식 클래스에서 구현)
    /// </summary>
    public abstract T? Connect(INatsSetupModel setupModel);

    /// <summary>
    /// 비동기 연결 로직 (자식 클래스에서 구현)
    /// </summary>
    public abstract Task<T?> ConnectAsync(INatsSetupModel setupModel);

    /// <summary>
    /// 메시지 발행 (자식 클래스에서 구현)
    /// </summary>
    public abstract Task PublishAsync(string subject, string data);
    #endregion

    #region - Properties -
    protected NatsConnection? Connection { get; set; }
    #endregion

    #region - Attributes -
    public event EventHandler<MessageArgsModel>? NatsSubscribeEvent;
    public event Func<MessageArgsModel, Task>? NatsSubscribeEventAsync;

    protected string _defaultSubject = string.Empty;
    protected List<string> _additionalSubjects = new();
    protected ILogService? _log;
    protected List<Task> _subscriptionTasks = new();
    #endregion
}
