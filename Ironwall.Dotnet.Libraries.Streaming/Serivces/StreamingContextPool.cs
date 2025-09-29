using System;
using System.Collections.Concurrent;

namespace Ironwall.Dotnet.Libraries.Streaming.Serivces;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 9/24/2025 3:02:43 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// StreamingContext Object Pool
/// </summary>
public interface IStreamingContextPool
{
    ImprovedStreamingContext Get();
    void Return(ImprovedStreamingContext context);
    void Clear();
}

public class StreamingContextPool : IStreamingContextPool, IDisposable
{
    private readonly ConcurrentBag<ImprovedStreamingContext> _pool;
    private readonly int _maxSize;
    private bool _disposed;

    public StreamingContextPool(int maxSize = 32)
    {
        _maxSize = maxSize;
        _pool = new ConcurrentBag<ImprovedStreamingContext>();

        // 초기 생성
        for (int i = 0; i < Math.Min(8, maxSize); i++)
        {
            _pool.Add(new ImprovedStreamingContext());
        }
    }

    public ImprovedStreamingContext Get()
    {
        if (_pool.TryTake(out var context))
        {
            return context;
        }

        return new ImprovedStreamingContext();
    }

    public void Return(ImprovedStreamingContext context)
    {
        if (context == null || _pool.Count >= _maxSize || _disposed)
        {
            context?.Cleanup();
            return;
        }

        context.Reset();
        _pool.Add(context);
    }

    public void Clear()
    {
        while (_pool.TryTake(out var context))
        {
            context.Cleanup();
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Clear();
            _disposed = true;
        }
    }
}