namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.SharedMemory;

/// <summary>
/// 공유 메모리 프레임 배치(순수). BGRA 32bpp(알파 무시 — WPF <c>Bgr32</c>) 이중 버퍼 + 순번.
/// <code>
/// 0  int  Magic 'CPF1'         24 long Writing   (쓰기 시작한 순번)
/// 4  int  LayoutVersion        32 long Published (마지막으로 다 쓴 순번, 0 = 아직 없음)
/// 8  int  Width                40 int  ProducerState
/// 12 int  Height               44 int  (예약)
/// 16 int  Stride               48 long LastWriteUnixMs
/// 20 int  SlotCount(=2)        56..63 (예약)
/// 64 슬롯0 · 64+SlotBytes 슬롯1 — 순번 n 의 프레임은 슬롯 n % 2
/// </code>
/// 읽기 규칙(seqlock): p = Published → 슬롯 p%2 복사 → w = Writing. <c>w &lt; p + 2</c> 이면 복사가 온전하다
/// (쓰는 쪽이 같은 슬롯을 다시 쓰는 프레임 p+2 를 아직 시작하지 않았다).
/// </summary>
public readonly record struct SharedFrameLayout(int Width, int Height)
{
    public const int Magic = 0x31465043; // "CPF1" little-endian
    public const int LayoutVersion = 1;
    public const int HeaderBytes = 64;
    public const int SlotCount = 2;
    public const int BytesPerPixel = 4;
    public const int MaxWidth = 3840;
    public const int MaxHeight = 2160;

    public const int OffsetMagic = 0;
    public const int OffsetLayoutVersion = 4;
    public const int OffsetWidth = 8;
    public const int OffsetHeight = 12;
    public const int OffsetStride = 16;
    public const int OffsetSlotCount = 20;
    public const int OffsetWriting = 24;
    public const int OffsetPublished = 32;
    public const int OffsetProducerState = 40;
    public const int OffsetLastWriteUnixMs = 48;

    public int Stride => Width * BytesPerPixel;
    public long SlotBytes => (long)Stride * Height;
    public long TotalBytes => HeaderBytes + SlotBytes * SlotCount;

    public static bool IsValidSize(int width, int height)
        => width is >= 1 and <= MaxWidth && height is >= 1 and <= MaxHeight;

    public static SharedFrameLayout Create(int width, int height)
    {
        if (!IsValidSize(width, height))
            throw new ArgumentOutOfRangeException(nameof(width), $"{width}x{height} — 1..{MaxWidth} x 1..{MaxHeight}");
        return new SharedFrameLayout(width, height);
    }

    public static int SlotIndex(long sequence) => (int)(sequence % SlotCount);

    public long SlotOffset(long sequence) => HeaderBytes + SlotIndex(sequence) * SlotBytes;

    /// <summary>복사가 온전한지(seqlock 판정, 순수).</summary>
    public static bool IsCopyConsistent(long publishedBeforeCopy, long writingAfterCopy)
        => publishedBeforeCopy > 0 && writingAfterCopy < publishedBeforeCopy + SlotCount;

    /// <summary>GIS 쪽 공유 메모리 이름(순수). <c>Local\</c> — 같은 로그온 세션 안에서만 보인다.</summary>
    public static string BuildName(int gisProcessId, string uniqueSuffix)
        => $@"Local\ironwall-camfrm-{gisProcessId}-{uniqueSuffix}";
}
