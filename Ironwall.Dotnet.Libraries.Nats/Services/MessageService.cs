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
        await Task.Run(() => UnregisterSubscribers(), token);
    }
    #endregion

    #region - Processes -
    /// <summary>
    /// Subject 구독을 등록합니다 (와일드카드 지원)
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

            _log?.Info($"[RegisterSubscribers] Starting subscription to: {_defaultSubject}");

            // NATS v2 API: SubscribeAsync로 직접 IAsyncEnumerable 반환
            // 백그라운드에서 메시지 수신 처리
            _subscriptionTask = Task.Run(async () =>
            {
                try
                {
                    _log?.Info("[SubscriptionTask] Starting subscription loop");
                    await foreach (var msg in Connection.SubscribeAsync<string>(_defaultSubject, cancellationToken: token))
                    {
                        try
                        {
                            var data = msg.Data ?? string.Empty;
                            _log?.Info($"[SubscriptionTask] Received message from '{msg.Subject}': {data}");

                            // 비동기 이벤트 핸들러 호출
                            await OnNatsSubscribeEventAsync(new MessageArgsModel(msg.Subject, _defaultSubject, data));
                        }
                        catch (Exception ex)
                        {
                            _log?.Error($"[SubscriptionTask] Error processing message: {ex.Message}");
                        }
                    }
                    _log?.Info("[SubscriptionTask] Subscription loop ended");
                }
                catch (OperationCanceledException)
                {
                    _log?.Info("[SubscriptionTask] Subscription cancelled");
                }
                catch (Exception ex)
                {
                    _log?.Error($"[SubscriptionTask] Subscription error: {ex.Message}");
                }
            }, token);

            // 구독이 준비될 때까지 잠시 대기
            await Task.Delay(100, token);

            _log?.Info($"[RegisterSubscribers] Subscription started for: {_defaultSubject}");
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
    protected virtual async void UnregisterSubscribers()
    {
        try
        {
            _log?.Info("[MessageService] Disposing NATS Connection...");

            // 구독 Task 취소 대기
            if (_subscriptionTask != null)
            {
                try
                {
                    await _subscriptionTask;
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
    protected ILogService? _log;
    protected Task? _subscriptionTask;
    #endregion
}
