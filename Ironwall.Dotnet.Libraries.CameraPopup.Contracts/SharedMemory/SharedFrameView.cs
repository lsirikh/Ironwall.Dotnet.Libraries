using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.SharedMemory;

/// <summary>
/// 공유 메모리 프레임 버퍼 한 개(양쪽 공용 구현). GIS 는 <see cref="CreateNew"/> 로 만들고 머리말을 채우며,
/// 호스트는 <see cref="OpenExisting"/> 으로 열어 <see cref="WriteFrame(IntPtr, int)"/> 만 한다.
/// 복사 · 해제는 한 잠금 아래에서 한다 — 해제된 매핑을 건드리면 그 프로세스가 접근 위반으로 죽기 때문이다
/// (GIS 쪽에서 그런 일은 절대 없어야 한다).
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed unsafe class SharedFrameView : IDisposable
{
    private readonly object _gate = new();
    private readonly MemoryMappedFile _file;
    private readonly MemoryMappedViewAccessor _accessor;
    private byte* _base;
    private bool _disposed;

    public string Name { get; }
    public SharedFrameLayout Layout { get; }

    private SharedFrameView(string name, MemoryMappedFile file, MemoryMappedViewAccessor accessor, SharedFrameLayout layout)
    {
        Name = name;
        _file = file;
        _accessor = accessor;
        Layout = layout;
        byte* p = null;
        _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref p);
        _base = p + _accessor.PointerOffset;
    }

    /// <summary>GIS: 새 공유 메모리를 만들고 머리말을 쓴다.</summary>
    public static SharedFrameView CreateNew(string name, int width, int height)
    {
        var layout = SharedFrameLayout.Create(width, height);
        var file = MemoryMappedFile.CreateNew(name, layout.TotalBytes, MemoryMappedFileAccess.ReadWrite);
        try
        {
            var accessor = file.CreateViewAccessor(0, layout.TotalBytes, MemoryMappedFileAccess.ReadWrite);
            var view = new SharedFrameView(name, file, accessor, layout);
            view.InitializeHeader();
            return view;
        }
        catch
        {
            file.Dispose();
            throw;
        }
    }

    /// <summary>호스트: 기존 공유 메모리를 연다. 머리말이 기대 크기와 다르면 <see cref="InvalidDataException"/>.</summary>
    public static SharedFrameView OpenExisting(string name, int expectedWidth, int expectedHeight)
    {
        var expected = SharedFrameLayout.Create(expectedWidth, expectedHeight);
        var file = MemoryMappedFile.OpenExisting(name, MemoryMappedFileRights.ReadWrite);
        try
        {
            var accessor = file.CreateViewAccessor(0, expected.TotalBytes, MemoryMappedFileAccess.ReadWrite);
            var view = new SharedFrameView(name, file, accessor, expected);
            if (!view.HeaderMatches())
            {
                view.Dispose();
                throw new InvalidDataException($"공유 메모리 머리말 불일치: {name}");
            }
            return view;
        }
        catch
        {
            file.Dispose();
            throw;
        }
    }

    private ref int IntAt(int offset) => ref Unsafe.AsRef<int>(_base + offset);
    private ref long LongAt(int offset) => ref Unsafe.AsRef<long>(_base + offset);

    private void InitializeHeader()
    {
        IntAt(SharedFrameLayout.OffsetLayoutVersion) = SharedFrameLayout.LayoutVersion;
        IntAt(SharedFrameLayout.OffsetWidth) = Layout.Width;
        IntAt(SharedFrameLayout.OffsetHeight) = Layout.Height;
        IntAt(SharedFrameLayout.OffsetStride) = Layout.Stride;
        IntAt(SharedFrameLayout.OffsetSlotCount) = SharedFrameLayout.SlotCount;
        Volatile.Write(ref LongAt(SharedFrameLayout.OffsetWriting), 0L);
        Volatile.Write(ref LongAt(SharedFrameLayout.OffsetPublished), 0L);
        Volatile.Write(ref IntAt(SharedFrameLayout.OffsetMagic), SharedFrameLayout.Magic);
    }

    private bool HeaderMatches()
        => Volatile.Read(ref IntAt(SharedFrameLayout.OffsetMagic)) == SharedFrameLayout.Magic
           && IntAt(SharedFrameLayout.OffsetLayoutVersion) == SharedFrameLayout.LayoutVersion
           && IntAt(SharedFrameLayout.OffsetWidth) == Layout.Width
           && IntAt(SharedFrameLayout.OffsetHeight) == Layout.Height
           && IntAt(SharedFrameLayout.OffsetStride) == Layout.Stride
           && IntAt(SharedFrameLayout.OffsetSlotCount) == SharedFrameLayout.SlotCount;

    /// <summary>마지막으로 다 쓴 프레임 순번(0 = 없음). 해제 뒤에는 -1.</summary>
    public long PublishedSequence
    {
        get
        {
            lock (_gate)
            {
                return _disposed ? -1 : Volatile.Read(ref LongAt(SharedFrameLayout.OffsetPublished));
            }
        }
    }

    /// <summary>마지막 쓰기 시각(유닉스 ms). 멈춤 판단용.</summary>
    public long LastWriteUnixMs
    {
        get
        {
            lock (_gate)
            {
                return _disposed ? 0 : Volatile.Read(ref LongAt(SharedFrameLayout.OffsetLastWriteUnixMs));
            }
        }
    }

    /// <summary>
    /// 프레임 하나를 쓴다(호스트). <paramref name="source"/> 는 Width×Height BGRA, 행 간격 <paramref name="sourceStride"/>.
    /// 반환: 쓴 순번(해제됐으면 -1).
    /// </summary>
    public long WriteFrame(IntPtr source, int sourceStride)
    {
        if (source == IntPtr.Zero) throw new ArgumentNullException(nameof(source));
        if (sourceStride < Layout.Stride) throw new ArgumentOutOfRangeException(nameof(sourceStride));
        lock (_gate)
        {
            if (_disposed) return -1;
            long n = Volatile.Read(ref LongAt(SharedFrameLayout.OffsetPublished)) + 1;
            Volatile.Write(ref LongAt(SharedFrameLayout.OffsetWriting), n);
            Interlocked.MemoryBarrier();
            byte* dst = _base + Layout.SlotOffset(n);
            byte* src = (byte*)source;
            int rowBytes = Layout.Stride;
            if (sourceStride == rowBytes)
            {
                Buffer.MemoryCopy(src, dst, Layout.SlotBytes, Layout.SlotBytes);
            }
            else
            {
                for (int y = 0; y < Layout.Height; y++)
                    Buffer.MemoryCopy(src + (long)y * sourceStride, dst + (long)y * rowBytes, rowBytes, rowBytes);
            }
            Volatile.Write(ref LongAt(SharedFrameLayout.OffsetLastWriteUnixMs), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            Volatile.Write(ref LongAt(SharedFrameLayout.OffsetPublished), n);
            return n;
        }
    }

    /// <summary>관리 배열에서 쓰기(시험 무늬 생산자 · 시험용).</summary>
    public long WriteFrame(ReadOnlySpan<byte> source, int sourceStride)
    {
        if (source.Length < (long)sourceStride * (Layout.Height - 1) + Layout.Stride)
            throw new ArgumentException("원본 버퍼가 작다", nameof(source));
        fixed (byte* p = source)
        {
            return WriteFrame((IntPtr)p, sourceStride);
        }
    }

    /// <summary>
    /// 최신 프레임을 <paramref name="destination"/>(행 간격 <paramref name="destinationStride"/>, 전체 <paramref name="destinationBytes"/>)으로 복사.
    /// 쓰는 쪽과 겹쳐 온전하지 않으면 몇 번 다시 시도하고, 그래도 안 되면 false(다음 기회에).
    /// </summary>
    public bool TryCopyLatest(IntPtr destination, int destinationStride, long destinationBytes, out long sequence)
    {
        sequence = 0;
        if (destination == IntPtr.Zero || destinationStride < Layout.Stride) return false;
        if (destinationBytes < (long)destinationStride * (Layout.Height - 1) + Layout.Stride) return false;
        lock (_gate)
        {
            if (_disposed) return false;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                long published = Volatile.Read(ref LongAt(SharedFrameLayout.OffsetPublished));
                if (published <= 0) return false;
                Interlocked.MemoryBarrier();
                byte* src = _base + Layout.SlotOffset(published);
                byte* dst = (byte*)destination;
                int rowBytes = Layout.Stride;
                if (destinationStride == rowBytes)
                {
                    Buffer.MemoryCopy(src, dst, destinationBytes, Layout.SlotBytes);
                }
                else
                {
                    for (int y = 0; y < Layout.Height; y++)
                        Buffer.MemoryCopy(src + (long)y * rowBytes, dst + (long)y * destinationStride, rowBytes, rowBytes);
                }
                Interlocked.MemoryBarrier();
                long writing = Volatile.Read(ref LongAt(SharedFrameLayout.OffsetWriting));
                if (SharedFrameLayout.IsCopyConsistent(published, writing))
                {
                    sequence = published;
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>관리 배열로 복사(시험 · 진단용).</summary>
    public bool TryCopyLatest(Span<byte> destination, out long sequence)
    {
        fixed (byte* p = destination)
        {
            return TryCopyLatest((IntPtr)p, Layout.Stride, destination.Length, out sequence);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            if (_base != null)
            {
                _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
                _base = null;
            }
            _accessor.Dispose();
            _file.Dispose();
        }
    }
}
