using System.Buffers.Binary;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

/// <summary>
/// 파이프 프레이밍: <c>[int32 LE 길이][UTF-8 JSON 봉투]</c>. 길이는 1 ~ <see cref="MaxPayloadBytes"/>.
/// 범위를 벗어난 길이는 상대가 깨졌다는 뜻 — 연결을 끊는다(<see cref="InvalidDataException"/>).
/// </summary>
public static class FrameCodec
{
    public const int HeaderBytes = 4;

    /// <summary>한 메시지 상한(1 MiB). 영상 프레임은 파이프가 아니라 공유 메모리로 간다.</summary>
    public const int MaxPayloadBytes = 1024 * 1024;

    /// <summary>머리말이 가리키는 길이가 유효한지(순수).</summary>
    public static bool IsValidLength(int length) => length > 0 && length <= MaxPayloadBytes;

    /// <summary>머리말 + 본문을 한 배열로(순수). 한 번의 Write 로 보내 부분 쓰기 끼어들기를 막는다.</summary>
    public static byte[] Encode(ReadOnlySpan<byte> payload)
    {
        if (!IsValidLength(payload.Length))
            throw new ArgumentOutOfRangeException(nameof(payload), payload.Length, "IPC 메시지 길이 범위 밖");
        var frame = new byte[HeaderBytes + payload.Length];
        BinaryPrimitives.WriteInt32LittleEndian(frame, payload.Length);
        payload.CopyTo(frame.AsSpan(HeaderBytes));
        return frame;
    }

    /// <summary>머리말 4바이트에서 길이를 읽는다(순수).</summary>
    public static bool TryReadLength(ReadOnlySpan<byte> header, out int length)
    {
        length = 0;
        if (header.Length < HeaderBytes) return false;
        length = BinaryPrimitives.ReadInt32LittleEndian(header);
        return IsValidLength(length);
    }

    /// <summary>프레임 하나를 쓴다.</summary>
    public static async Task WriteFrameAsync(Stream stream, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        var frame = Encode(payload.Span);
        await stream.WriteAsync(frame, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// 프레임 하나를 읽는다. 머리말 전에 깨끗이 끝나면 null(상대가 닫음).
    /// 중간에 끊기면 <see cref="EndOfStreamException"/>, 길이가 범위 밖이면 <see cref="InvalidDataException"/>.
    /// </summary>
    public static async Task<byte[]?> ReadFrameAsync(Stream stream, CancellationToken ct)
    {
        var header = new byte[HeaderBytes];
        int read = await ReadFullyAsync(stream, header, ct).ConfigureAwait(false);
        if (read == 0) return null;
        if (read < HeaderBytes) throw new EndOfStreamException("IPC 머리말 도중 연결 종료");
        if (!TryReadLength(header, out var length))
            throw new InvalidDataException($"IPC 길이 범위 밖: {BinaryPrimitives.ReadInt32LittleEndian(header)}");
        var payload = new byte[length];
        read = await ReadFullyAsync(stream, payload, ct).ConfigureAwait(false);
        if (read < length) throw new EndOfStreamException("IPC 본문 도중 연결 종료");
        return payload;
    }

    private static async Task<int> ReadFullyAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        int total = 0;
        while (total < buffer.Length)
        {
            int n = await stream.ReadAsync(buffer.AsMemory(total), ct).ConfigureAwait(false);
            if (n == 0) break;
            total += n;
        }
        return total;
    }
}
