using Ironwall.Dotnet.Libraries.Streaming.Base.Models;
using System.Collections.Concurrent;

namespace Ironwall.Dotnet.Libraries.Streaming.Serivces;
/****************************************************************************
   Purpose      : 스트림 프리워밍 서비스 구현
   Created By   : GHLee
   Created On   : 12/03/2025
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 스트림 프리워밍 서비스 구현
/// 이벤트 수신 시 미리 연결 컨텍스트를 준비하여 빠른 스트림 연결 지원
/// </summary>
public class StreamPrewarmer : IStreamPrewarmer, IDisposable
{
    #region - Fields -

    private readonly ConcurrentDictionary<string, PrewarmedContext> _prewarmedContexts;
    private readonly int _maxPrewarmedContexts;
    private readonly TimeSpan _staleTimeout;
    private readonly object _cleanupLock = new();
    private bool _disposed;

    #endregion

    #region - Ctors -

    /// <summary>
    /// 생성자
    /// </summary>
    /// <param name="maxPrewarmedContexts">최대 프리워밍 컨텍스트 수</param>
    /// <param name="staleTimeoutSeconds">만료 시간 (초)</param>
    public StreamPrewarmer(int maxPrewarmedContexts = 8, int staleTimeoutSeconds = 30)
    {
        _prewarmedContexts = new ConcurrentDictionary<string, PrewarmedContext>();
        _maxPrewarmedContexts = maxPrewarmedContexts;
        _staleTimeout = TimeSpan.FromSeconds(staleTimeoutSeconds);
    }

    #endregion

    #region - Properties -

    /// <inheritdoc/>
    public int PrewarmedCount => _prewarmedContexts.Count;

    #endregion

    #region - IStreamPrewarmer Implementation -

    /// <inheritdoc/>
    public async Task PrewarmAsync(
        string contextId,
        RtspConnectionInfo connectionInfo,
        StreamingOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(contextId))
            throw new ArgumentNullException(nameof(contextId));

        if (connectionInfo == null)
            throw new ArgumentNullException(nameof(connectionInfo));

        // 최대 개수 초과 시 가장 오래된 항목 제거
        EnsureCapacity();

        // 기존 컨텍스트가 있으면 업데이트
        var context = new PrewarmedContext(contextId)
        {
            ConnectionInfo = connectionInfo.Clone(),
            Options = options?.Clone(),
            State = PrewarmState.Prewarming
        };

        // 기존 항목이 있으면 먼저 정리
        if (_prewarmedContexts.TryRemove(contextId, out var existing))
        {
            existing.Dispose();
        }

        try
        {
            // 프리워밍 로직 (현재는 연결 정보만 캐싱)
            // 향후 Media 객체 미리 생성 등 추가 가능
            await Task.CompletedTask; // Placeholder for actual prewarming logic

            context.State = PrewarmState.Ready;
            _prewarmedContexts[contextId] = context;
        }
        catch (Exception)
        {
            context.State = PrewarmState.Failed;
            context.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public bool TryGetPrewarmedContext(string contextId, out PrewarmedContext? context)
    {
        context = null;

        if (string.IsNullOrEmpty(contextId))
            return false;

        if (_prewarmedContexts.TryRemove(contextId, out var prewarmed))
        {
            if (prewarmed.IsExpired(_staleTimeout))
            {
                // 만료된 컨텍스트는 정리
                prewarmed.Dispose();
                return false;
            }

            prewarmed.State = PrewarmState.Used;
            prewarmed.Touch();
            context = prewarmed;
            return true;
        }

        return false;
    }

    /// <inheritdoc/>
    public int CleanupStalePrewarms()
    {
        lock (_cleanupLock)
        {
            var removedCount = 0;
            var keysToRemove = new List<string>();

            foreach (var kvp in _prewarmedContexts)
            {
                if (kvp.Value.IsExpired(_staleTimeout))
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                if (_prewarmedContexts.TryRemove(key, out var context))
                {
                    context.Dispose();
                    removedCount++;
                }
            }

            return removedCount;
        }
    }

    /// <inheritdoc/>
    public void ClearAll()
    {
        lock (_cleanupLock)
        {
            foreach (var context in _prewarmedContexts.Values)
            {
                context.Dispose();
            }
            _prewarmedContexts.Clear();
        }
    }

    #endregion

    #region - Helper Methods -

    private void EnsureCapacity()
    {
        while (_prewarmedContexts.Count >= _maxPrewarmedContexts)
        {
            // 가장 오래된 항목 찾기
            var oldest = _prewarmedContexts
                .OrderBy(x => x.Value.CreatedAt)
                .FirstOrDefault();

            if (!string.IsNullOrEmpty(oldest.Key))
            {
                if (_prewarmedContexts.TryRemove(oldest.Key, out var context))
                {
                    context.Dispose();
                }
            }
            else
            {
                break;
            }
        }
    }

    #endregion

    #region - IDisposable -

    public void Dispose()
    {
        if (_disposed)
            return;

        ClearAll();
        _disposed = true;
    }

    #endregion
}
