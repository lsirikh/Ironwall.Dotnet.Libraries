namespace Ironwall.Dotnet.Libraries.Streaming.Serivces;
/****************************************************************************
   Purpose      : 네트워크 메트릭 수집 및 분석 클래스
   Created By   : GHLee
   Created On   : 12/03/2025
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 네트워크 메트릭 수집 및 분석 클래스
/// 지터, 패킷 손실률 등을 추적하여 적응형 버퍼링에 활용
/// </summary>
public class NetworkMetrics
{
    #region - Fields -

    private readonly Queue<double> _jitterSamples;
    private readonly int _maxSamples;
    private readonly object _lock = new();

    #endregion

    #region - Ctors -

    /// <summary>
    /// 생성자
    /// </summary>
    /// <param name="maxSamples">최대 샘플 수</param>
    public NetworkMetrics(int maxSamples = 30)
    {
        _maxSamples = maxSamples;
        _jitterSamples = new Queue<double>(maxSamples);
    }

    #endregion

    #region - Properties -

    /// <summary>
    /// 평균 지터 (밀리초)
    /// </summary>
    public double AverageJitterMs
    {
        get
        {
            lock (_lock)
            {
                if (_jitterSamples.Count == 0)
                    return 0;
                return _jitterSamples.Average();
            }
        }
    }

    /// <summary>
    /// 최대 지터 (밀리초)
    /// </summary>
    public double MaxJitterMs
    {
        get
        {
            lock (_lock)
            {
                if (_jitterSamples.Count == 0)
                    return 0;
                return _jitterSamples.Max();
            }
        }
    }

    /// <summary>
    /// 최소 지터 (밀리초)
    /// </summary>
    public double MinJitterMs
    {
        get
        {
            lock (_lock)
            {
                if (_jitterSamples.Count == 0)
                    return 0;
                return _jitterSamples.Min();
            }
        }
    }

    /// <summary>
    /// 패킷 손실률 (0-1)
    /// </summary>
    public double PacketLossRate { get; private set; }

    /// <summary>
    /// 샘플 수
    /// </summary>
    public int SampleCount
    {
        get
        {
            lock (_lock)
            {
                return _jitterSamples.Count;
            }
        }
    }

    /// <summary>
    /// 마지막 업데이트 시간
    /// </summary>
    public DateTime LastUpdated { get; private set; }

    #endregion

    #region - Public Methods -

    /// <summary>
    /// 지터 샘플 추가
    /// </summary>
    public void AddJitterSample(double jitterMs)
    {
        lock (_lock)
        {
            // 최대 샘플 수 초과 시 가장 오래된 샘플 제거
            while (_jitterSamples.Count >= _maxSamples)
            {
                _jitterSamples.Dequeue();
            }

            _jitterSamples.Enqueue(jitterMs);
            LastUpdated = DateTime.Now;
        }
    }

    /// <summary>
    /// 패킷 손실률 업데이트
    /// </summary>
    public void UpdatePacketLossRate(double lossRate)
    {
        PacketLossRate = Math.Clamp(lossRate, 0, 1);
        LastUpdated = DateTime.Now;
    }

    /// <summary>
    /// 네트워크가 안정적인지 확인
    /// </summary>
    /// <param name="threshold">지터 임계값 (밀리초)</param>
    public bool IsNetworkStable(double threshold = 50)
    {
        return AverageJitterMs <= threshold && PacketLossRate < 0.05;
    }

    /// <summary>
    /// 지터 표준편차 계산
    /// </summary>
    public double GetJitterStandardDeviation()
    {
        lock (_lock)
        {
            if (_jitterSamples.Count < 2)
                return 0;

            var avg = _jitterSamples.Average();
            var sumOfSquares = _jitterSamples.Sum(x => Math.Pow(x - avg, 2));
            return Math.Sqrt(sumOfSquares / (_jitterSamples.Count - 1));
        }
    }

    /// <summary>
    /// 메트릭 초기화
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _jitterSamples.Clear();
            PacketLossRate = 0;
            LastUpdated = DateTime.MinValue;
        }
    }

    #endregion
}
