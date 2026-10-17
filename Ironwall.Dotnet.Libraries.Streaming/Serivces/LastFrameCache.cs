namespace Ironwall.Dotnet.Libraries.Streaming.Serivces;
/****************************************************************************
   Purpose      : 마지막 프레임 캐시 - 버퍼링 시 마지막 프레임 유지
   Created By   : GHLee
   Created On   : 12/03/2025
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 마지막 프레임 캐시
/// 버퍼링 상태에서 마지막 정상 프레임을 유지하여 표시
/// </summary>
public class LastFrameCache : IDisposable
{
    #region - Fields -

    private byte[]? _frameData;
    private readonly object _lock = new();
    private bool _disposed;

    #endregion

    #region - Properties -

    /// <summary>
    /// 캐시된 프레임이 있는지 여부
    /// </summary>
    public bool HasFrame
    {
        get
        {
            lock (_lock)
            {
                return _frameData != null && _frameData.Length > 0;
            }
        }
    }

    /// <summary>
    /// 프레임 너비
    /// </summary>
    public int Width { get; private set; }

    /// <summary>
    /// 프레임 높이
    /// </summary>
    public int Height { get; private set; }

    /// <summary>
    /// 마지막 업데이트 시간
    /// </summary>
    public DateTime LastUpdated { get; private set; }

    /// <summary>
    /// 총 캐시된 프레임 수
    /// </summary>
    public int FrameCount { get; private set; }

    /// <summary>
    /// 프레임 나이 (마지막 업데이트 이후 경과 시간)
    /// </summary>
    public TimeSpan FrameAge
    {
        get
        {
            if (LastUpdated == DateTime.MinValue)
                return TimeSpan.MaxValue;
            return DateTime.Now - LastUpdated;
        }
    }

    #endregion

    #region - Public Methods -

    /// <summary>
    /// 프레임 캐싱
    /// </summary>
    /// <param name="frameData">프레임 데이터</param>
    /// <param name="width">너비</param>
    /// <param name="height">높이</param>
    public void CacheFrame(byte[] frameData, int width, int height)
    {
        if (frameData == null || frameData.Length == 0)
            return;

        lock (_lock)
        {
            // 기존 데이터 재사용 또는 새로 할당
            if (_frameData == null || _frameData.Length != frameData.Length)
            {
                _frameData = new byte[frameData.Length];
            }

            Buffer.BlockCopy(frameData, 0, _frameData, 0, frameData.Length);
            Width = width;
            Height = height;
            LastUpdated = DateTime.Now;
            FrameCount++;
        }
    }

    /// <summary>
    /// 마지막 프레임 가져오기
    /// </summary>
    /// <returns>프레임 데이터 복사본</returns>
    public byte[]? GetLastFrame()
    {
        lock (_lock)
        {
            if (_frameData == null)
                return null;

            var copy = new byte[_frameData.Length];
            Buffer.BlockCopy(_frameData, 0, copy, 0, _frameData.Length);
            return copy;
        }
    }

    /// <summary>
    /// 프레임 만료 여부 확인
    /// </summary>
    /// <param name="timeout">만료 시간</param>
    public bool IsExpired(TimeSpan timeout)
    {
        if (!HasFrame)
            return true;

        return FrameAge > timeout;
    }

    /// <summary>
    /// 캐시 초기화
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _frameData = null;
            Width = 0;
            Height = 0;
            LastUpdated = DateTime.MinValue;
        }
    }

    /// <summary>
    /// 현재 상태 가져오기
    /// </summary>
    public FrameCacheStatus GetStatus()
    {
        lock (_lock)
        {
            return new FrameCacheStatus
            {
                HasFrame = HasFrame,
                Width = Width,
                Height = Height,
                FrameSizeBytes = _frameData?.Length ?? 0,
                FrameCount = FrameCount,
                LastUpdated = LastUpdated,
                FrameAge = FrameAge
            };
        }
    }

    #endregion

    #region - IDisposable -

    public void Dispose()
    {
        if (_disposed)
            return;

        Clear();
        _disposed = true;
    }

    #endregion
}

/// <summary>
/// 프레임 캐시 상태 정보
/// </summary>
public class FrameCacheStatus
{
    public bool HasFrame { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int FrameSizeBytes { get; set; }
    public int FrameCount { get; set; }
    public DateTime LastUpdated { get; set; }
    public TimeSpan FrameAge { get; set; }
}
