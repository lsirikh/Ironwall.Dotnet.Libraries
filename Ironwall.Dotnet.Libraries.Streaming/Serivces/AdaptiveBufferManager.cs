namespace Ironwall.Dotnet.Libraries.Streaming.Serivces;
/****************************************************************************
   Purpose      : 적응형 버퍼 관리자 - 네트워크 상태에 따라 캐싱 값을 동적 조절
   Created By   : GHLee
   Created On   : 12/03/2025
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 적응형 버퍼 관리자
/// 네트워크 상태에 따라 캐싱 값을 동적으로 조절
/// </summary>
public class AdaptiveBufferManager
{
    #region - Fields -

    private readonly NetworkMetrics _metrics;
    private readonly int _baseCaching;
    private readonly int _minCaching;
    private readonly int _maxCaching;
    private readonly double _jitterThreshold;
    private readonly double _adaptationFactor;
    private int _currentCaching;
    private int _previousCaching;

    #endregion

    #region - Ctors -

    /// <summary>
    /// 생성자
    /// </summary>
    /// <param name="baseCaching">기본 캐싱 값 (ms)</param>
    /// <param name="minCaching">최소 캐싱 값 (ms)</param>
    /// <param name="maxCaching">최대 캐싱 값 (ms)</param>
    /// <param name="jitterThreshold">지터 임계값 (ms)</param>
    /// <param name="adaptationFactor">적응 계수 (0-1)</param>
    public AdaptiveBufferManager(
        int baseCaching = 150,
        int minCaching = 100,
        int maxCaching = 500,
        double jitterThreshold = 50,
        double adaptationFactor = 0.3)
    {
        _baseCaching = baseCaching;
        _minCaching = minCaching;
        _maxCaching = maxCaching;
        _jitterThreshold = jitterThreshold;
        _adaptationFactor = Math.Clamp(adaptationFactor, 0.1, 1.0);
        _currentCaching = baseCaching;
        _previousCaching = baseCaching;
        _metrics = new NetworkMetrics();
    }

    #endregion

    #region - Properties -

    /// <summary>
    /// 현재 캐싱 값 (ms)
    /// </summary>
    public int CurrentCaching => _currentCaching;

    /// <summary>
    /// 기본 캐싱 값 (ms)
    /// </summary>
    public int BaseCaching => _baseCaching;

    /// <summary>
    /// 네트워크 메트릭
    /// </summary>
    public NetworkMetrics Metrics => _metrics;

    #endregion

    #region - Public Methods -

    /// <summary>
    /// 메트릭 업데이트
    /// </summary>
    public void UpdateMetrics(double jitterMs, double packetLossRate = 0)
    {
        _metrics.AddJitterSample(jitterMs);
        if (packetLossRate > 0)
        {
            _metrics.UpdatePacketLossRate(packetLossRate);
        }
    }

    /// <summary>
    /// 최적 캐싱 값 계산
    /// </summary>
    public int CalculateOptimalCaching()
    {
        _previousCaching = _currentCaching;

        var avgJitter = _metrics.AverageJitterMs;
        var maxJitter = _metrics.MaxJitterMs;
        var packetLoss = _metrics.PacketLossRate;

        // 지터 기반 캐싱 조절
        int targetCaching;

        if (avgJitter <= _jitterThreshold && packetLoss < 0.02)
        {
            // 네트워크가 안정적: 캐싱 감소 시도
            targetCaching = (int)(_baseCaching * 0.9);
        }
        else if (avgJitter > _jitterThreshold * 2 || packetLoss > 0.05)
        {
            // 네트워크 불안정: 캐싱 증가
            var jitterFactor = Math.Min(avgJitter / _jitterThreshold, 3.0);
            var lossFactor = 1 + (packetLoss * 5);
            targetCaching = (int)(_baseCaching * jitterFactor * lossFactor);
        }
        else
        {
            // 중간 상태: 점진적 조절
            var jitterRatio = avgJitter / _jitterThreshold;
            targetCaching = (int)(_baseCaching * (1 + (jitterRatio - 1) * _adaptationFactor));
        }

        // 현재 값과 목표 값 사이 부드러운 전환
        _currentCaching = (int)(_currentCaching + (_adaptationFactor * (targetCaching - _currentCaching)));

        // 범위 제한
        _currentCaching = Math.Clamp(_currentCaching, _minCaching, _maxCaching);

        return _currentCaching;
    }

    /// <summary>
    /// 적응이 필요한지 확인
    /// </summary>
    public bool IsAdaptationNeeded()
    {
        if (_metrics.SampleCount < 3)
            return false;

        var avgJitter = _metrics.AverageJitterMs;

        // 현재 캐싱이 기본값과 크게 다르거나 지터가 높으면 적응 필요
        var cachingDiff = Math.Abs(_currentCaching - _baseCaching);
        var cachingThreshold = _baseCaching * 0.2; // 20% 차이

        return avgJitter > _jitterThreshold || cachingDiff > cachingThreshold;
    }

    /// <summary>
    /// 캐싱 변화량
    /// </summary>
    public int GetCachingDelta()
    {
        return _currentCaching - _previousCaching;
    }

    /// <summary>
    /// 기본값으로 초기화
    /// </summary>
    public void Reset()
    {
        _currentCaching = _baseCaching;
        _previousCaching = _baseCaching;
        _metrics.Reset();
    }

    /// <summary>
    /// 현재 상태 요약
    /// </summary>
    public BufferStatus GetStatus()
    {
        return new BufferStatus
        {
            CurrentCaching = _currentCaching,
            BaseCaching = _baseCaching,
            MinCaching = _minCaching,
            MaxCaching = _maxCaching,
            AverageJitterMs = _metrics.AverageJitterMs,
            IsStable = _metrics.IsNetworkStable(_jitterThreshold),
            SampleCount = _metrics.SampleCount
        };
    }

    #endregion
}

/// <summary>
/// 버퍼 상태 정보
/// </summary>
public class BufferStatus
{
    public int CurrentCaching { get; set; }
    public int BaseCaching { get; set; }
    public int MinCaching { get; set; }
    public int MaxCaching { get; set; }
    public double AverageJitterMs { get; set; }
    public bool IsStable { get; set; }
    public int SampleCount { get; set; }
}
