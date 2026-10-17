using System;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Models;

/// <summary>
/// 타일 좌표 식별자 (zoom, x, y) — 오버레이 타일 캐시/렌더링 키
/// </summary>
public readonly struct TileKey : IEquatable<TileKey>
{
    public int Zoom { get; }
    public long X { get; }
    public long Y { get; }

    public TileKey(int zoom, long x, long y)
    {
        Zoom = zoom;
        X = x;
        Y = y;
    }

    public bool Equals(TileKey other)
        => Zoom == other.Zoom && X == other.X && Y == other.Y;

    public override bool Equals(object? obj)
        => obj is TileKey other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(Zoom, X, Y);

    public static bool operator ==(TileKey left, TileKey right) => left.Equals(right);
    public static bool operator !=(TileKey left, TileKey right) => !left.Equals(right);

    public override string ToString() => $"{Zoom}/{X}/{Y}";
}
