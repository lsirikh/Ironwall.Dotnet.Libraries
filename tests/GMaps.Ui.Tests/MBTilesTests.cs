using System;
using System.IO;
using Microsoft.Data.Sqlite;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// MBTiles TMS 좌표 변환 및 SQLite 스키마 단위 테스트
///
/// 검증 대상:
///   TEST-01 — ToTmsRow(zoom, yXyz): XYZ → TMS tile_row 변환 정확도
///   TEST-02 — MBTiles SQLite 스키마 생성 + 타일 INSERT/SELECT 왕복
///
/// 전략:
///   MBTilesOverlayMapProvider는 GMapProvider 상속으로 WPF 의존성 존재.
///   ToTmsRow 공식(순수 산술)과 SQLite 스키마 로직은 WPF 없이 검증 가능.
///   ToTmsRow를 로컬 헬퍼로 복제하여 공식 동치를 확인한다.
/// </summary>
public class MBTilesTests
{
    // ── ToTmsRow 공식 — MBTilesOverlayMapProvider.ToTmsRow와 동일 ──────────
    private static long ToTmsRow(int zoom, long yXyz)
        => (long)(Math.Pow(2, zoom) - 1) - yXyz;

    // ─────────────────────────────────────────────────────────────────────────
    // TEST-01: ToTmsRow XYZ → TMS 변환 정확도
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0,  0, 0)]           // zoom=0: 2^0-1=0,     TMS=0-0=0
    [InlineData(1,  0, 1)]           // zoom=1: 2^1-1=1,     TMS=1-0=1
    [InlineData(1,  1, 0)]           // zoom=1:              TMS=1-1=0
    [InlineData(15, 0,     32767)]   // zoom=15 XYZ 최상단  → TMS 최하단
    [InlineData(15, 32767, 0)]       // zoom=15 XYZ 최하단  → TMS 최상단
    [InlineData(15, 1000,  31767)]   // PRD 지정 검증값
    [InlineData(10, 500,   523)]     // zoom=10: 2^10-1=1023, TMS=1023-500=523
    public void should_convert_xyz_y_to_tms_row_correctly(int zoom, long yXyz, long expectedTms)
    {
        var result = ToTmsRow(zoom, yXyz);

        Assert.Equal(expectedTms, result);
    }

    [Fact]
    public void should_be_self_inverse_for_arbitrary_coords()
    {
        // TMS 변환은 자기 역함수: ToTmsRow(zoom, ToTmsRow(zoom, y)) == y
        int zoom = 15;
        long originalY = 1000;

        var tmsRow = ToTmsRow(zoom, originalY);
        var backToXyz = ToTmsRow(zoom, tmsRow);

        Assert.Equal(originalY, backToXyz);
    }

    [Fact]
    public void should_cover_full_range_at_zoom15()
    {
        // zoom=15 전체 범위(0~32767)에서 TMS는 뒤집힌 순서
        int zoom = 15;
        long maxTileIndex = (long)Math.Pow(2, zoom) - 1; // 32767

        Assert.Equal(maxTileIndex, ToTmsRow(zoom, 0));        // XYZ 최상단 → TMS 최하단
        Assert.Equal(0L,           ToTmsRow(zoom, maxTileIndex)); // XYZ 최하단 → TMS 최상단
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TEST-02: MBTiles SQLite 스키마 + 타일 INSERT / SELECT 왕복
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void should_store_and_retrieve_tile_bytes_with_tms_conversion()
    {
        // Arrange: 임시 MBTiles 파일 생성
        var tempPath = Path.Combine(Path.GetTempPath(), $"mbtiles_test_{Guid.NewGuid():N}.mbtiles");
        // 최소 PNG (1×1 투명 PNG 헤더 + IDAT)
        var testTileData = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A // PNG 매직 바이트
        };

        const int zoom = 10;
        const long xyzY = 500;
        var expectedTmsRow = ToTmsRow(zoom, xyzY); // 523

        try
        {
            // ── Act 1: 스키마 생성 + TMS 변환 적용 후 INSERT ────────────────
            using (var conn = new SqliteConnection($"Data Source={tempPath}"))
            {
                conn.Open();

                using var createCmd = conn.CreateCommand();
                createCmd.CommandText = @"
                    CREATE TABLE tiles (
                        zoom_level  INTEGER NOT NULL,
                        tile_column INTEGER NOT NULL,
                        tile_row    INTEGER NOT NULL,
                        tile_data   BLOB NOT NULL,
                        PRIMARY KEY (zoom_level, tile_column, tile_row)
                    );
                    CREATE TABLE metadata (
                        name  TEXT NOT NULL,
                        value TEXT NOT NULL,
                        PRIMARY KEY (name)
                    );";
                createCmd.ExecuteNonQuery();

                using var metaCmd = conn.CreateCommand();
                metaCmd.CommandText = @"
                    INSERT INTO metadata VALUES ('name',    'test_overlay');
                    INSERT INTO metadata VALUES ('format',  'png');
                    INSERT INTO metadata VALUES ('minzoom', '10');
                    INSERT INTO metadata VALUES ('maxzoom', '10');";
                metaCmd.ExecuteNonQuery();

                using var insertCmd = conn.CreateCommand();
                insertCmd.CommandText =
                    "INSERT INTO tiles (zoom_level, tile_column, tile_row, tile_data) VALUES (@z, @x, @y, @data)";
                insertCmd.Parameters.AddWithValue("@z",    zoom);
                insertCmd.Parameters.AddWithValue("@x",    100L);
                insertCmd.Parameters.AddWithValue("@y",    expectedTmsRow);
                insertCmd.Parameters.AddWithValue("@data", testTileData);
                insertCmd.ExecuteNonQuery();
            }

            // ── Act 2: TMS 좌표로 SELECT ─────────────────────────────────────
            byte[]? retrieved = null;
            using (var conn = new SqliteConnection($"Data Source={tempPath}"))
            {
                conn.Open();

                using var queryCmd = conn.CreateCommand();
                queryCmd.CommandText =
                    "SELECT tile_data FROM tiles WHERE zoom_level=@z AND tile_column=@x AND tile_row=@y LIMIT 1";
                queryCmd.Parameters.AddWithValue("@z", zoom);
                queryCmd.Parameters.AddWithValue("@x", 100L);
                queryCmd.Parameters.AddWithValue("@y", expectedTmsRow);

                using var rd = queryCmd.ExecuteReader();
                if (rd.Read())
                    retrieved = (byte[])rd[0];
            }

            // ── Assert ────────────────────────────────────────────────────────
            Assert.NotNull(retrieved);
            Assert.Equal(testTileData, retrieved);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            SqliteConnection.ClearAllPools();
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void should_return_null_when_tile_not_found()
    {
        // 존재하지 않는 타일 조회 시 null 반환 확인
        var tempPath = Path.Combine(Path.GetTempPath(), $"mbtiles_empty_{Guid.NewGuid():N}.mbtiles");

        try
        {
            using (var conn = new SqliteConnection($"Data Source={tempPath}"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    CREATE TABLE tiles (
                        zoom_level INTEGER, tile_column INTEGER, tile_row INTEGER,
                        tile_data BLOB, PRIMARY KEY (zoom_level, tile_column, tile_row));";
                cmd.ExecuteNonQuery();
            }

            byte[]? result = null;
            using (var conn = new SqliteConnection($"Data Source={tempPath}"))
            {
                conn.Open();
                using var queryCmd = conn.CreateCommand();
                queryCmd.CommandText =
                    "SELECT tile_data FROM tiles WHERE zoom_level=10 AND tile_column=99 AND tile_row=99 LIMIT 1";
                using var rd = queryCmd.ExecuteReader();
                if (rd.Read()) result = (byte[])rd[0];
            }

            Assert.Null(result);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void should_store_multiple_zoom_levels_independently()
    {
        // 복수 줌 레벨 타일이 독립적으로 저장/조회됨을 확인
        var tempPath = Path.Combine(Path.GetTempPath(), $"mbtiles_multi_{Guid.NewGuid():N}.mbtiles");
        var tile10 = new byte[] { 0x10 };
        var tile15 = new byte[] { 0x15 };

        try
        {
            using (var conn = new SqliteConnection($"Data Source={tempPath}"))
            {
                conn.Open();
                using var createCmd = conn.CreateCommand();
                createCmd.CommandText = @"
                    CREATE TABLE tiles (
                        zoom_level INTEGER, tile_column INTEGER, tile_row INTEGER,
                        tile_data BLOB, PRIMARY KEY (zoom_level, tile_column, tile_row));";
                createCmd.ExecuteNonQuery();

                foreach (var (zoom, data) in new[] { (10, tile10), (15, tile15) })
                {
                    using var ins = conn.CreateCommand();
                    ins.CommandText = "INSERT INTO tiles VALUES (@z, 0, @row, @d)";
                    ins.Parameters.AddWithValue("@z",   zoom);
                    ins.Parameters.AddWithValue("@row", ToTmsRow(zoom, 0));
                    ins.Parameters.AddWithValue("@d",   data);
                    ins.ExecuteNonQuery();
                }
            }

            byte[]? r10 = null, r15 = null;
            using (var conn = new SqliteConnection($"Data Source={tempPath}"))
            {
                conn.Open();
                foreach (var (zoom, dest) in new (int, byte[]?)[] { (10, null), (15, null) })
                {
                    using var q = conn.CreateCommand();
                    q.CommandText = "SELECT tile_data FROM tiles WHERE zoom_level=@z AND tile_column=0 AND tile_row=@row LIMIT 1";
                    q.Parameters.AddWithValue("@z",   zoom);
                    q.Parameters.AddWithValue("@row", ToTmsRow(zoom, 0));
                    using var rd = q.ExecuteReader();
                    if (zoom == 10 && rd.Read()) r10 = (byte[])rd[0];
                    else if (zoom == 15 && rd.Read()) r15 = (byte[])rd[0];
                }
            }

            Assert.Equal(tile10, r10);
            Assert.Equal(tile15, r15);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TEST-03: 빈 타일 기본 타일(no-data tile) 반환 결정 로직
    //
    //   MBTilesMapProvider.GetTileImage(GMap.NET.Core)는 WPF/System.Data.SQLite 네이티브
    //   의존이라 net8.0 격리 테스트에서 직접 호출 불가 → ToTmsRow와 동일하게 분기 로직을
    //   로컬 복제하여 결정 테이블 동치를 검증한다.
    //
    //   실제 코드(미러 대상):
    //     if (source == null) return null;                      // (a)
    //     if (zoom < MinZoom || zoom > MaxZoom) return null;     // (b)
    //     var img = source.GetImage(pos, zoom);                 // (c)
    //     if (img == null && TileImageProxy != null && DefaultTileBytes != null)
    //         return GetTileImageFromArray(DefaultTileBytes);
    //     return img;
    // ─────────────────────────────────────────────────────────────────────────

    private enum TileOutcome { Null, DefaultTile, RealTile }

    /// <summary>MBTilesMapProvider.GetTileImage 기본 타일 분기(a/b/c)의 충실한 복제.</summary>
    private static TileOutcome ResolveTile(
        bool sourceOpen, int zoom, int minZoom, int maxZoom,
        bool tileExists, bool defaultBytesSet, bool tileImageProxyAvailable)
    {
        if (!sourceOpen) return TileOutcome.Null;                          // (a) source == null
        if (zoom < minZoom || zoom > maxZoom) return TileOutcome.Null;     // (b) 줌 범위 밖
        if (tileExists) return TileOutcome.RealTile;                       // (c) 실제 타일 우선
        if (defaultBytesSet && tileImageProxyAvailable)
            return TileOutcome.DefaultTile;                                // (c) 타일 없음 → 기본 타일
        return TileOutcome.Null;                                           // (c) 기본 없음 → 기존 동작 유지
    }

    [Fact]
    public void should_return_default_tile_when_tile_missing_and_default_set()
    {
        // (c) 정상 줌 + 타일 없음 + 기본 바이트 주입됨 → 기본 타일 (커버리지 밖 흰화면 해소)
        var outcome = ResolveTile(
            sourceOpen: true, zoom: 15, minZoom: 0, maxZoom: 18,
            tileExists: false, defaultBytesSet: true, tileImageProxyAvailable: true);

        Assert.Equal(TileOutcome.DefaultTile, outcome);
    }

    [Fact]
    public void should_prefer_real_tile_over_default()
    {
        // (c) 실제 타일이 있으면 기본 바이트가 설정돼 있어도 실제 타일 반환(우선순위)
        var outcome = ResolveTile(
            sourceOpen: true, zoom: 15, minZoom: 0, maxZoom: 18,
            tileExists: true, defaultBytesSet: true, tileImageProxyAvailable: true);

        Assert.Equal(TileOutcome.RealTile, outcome);
    }

    [Fact]
    public void should_return_null_when_source_not_loaded_even_with_default()
    {
        // (a) source 미오픈(맵 미로드)이면 기본 타일을 깔지 않음 — 로드 실패 은폐 방지
        var outcome = ResolveTile(
            sourceOpen: false, zoom: 15, minZoom: 0, maxZoom: 18,
            tileExists: false, defaultBytesSet: true, tileImageProxyAvailable: true);

        Assert.Equal(TileOutcome.Null, outcome);
    }

    [Theory]
    [InlineData(-1)]   // minZoom 미만
    [InlineData(19)]   // maxZoom 초과
    public void should_return_null_when_zoom_out_of_range_even_with_default(int zoom)
    {
        // (b) 줌 범위 밖이면 기본 타일 미반환 — FillEmptyTiles 부모타일 폴백 보존
        var outcome = ResolveTile(
            sourceOpen: true, zoom: zoom, minZoom: 0, maxZoom: 18,
            tileExists: false, defaultBytesSet: true, tileImageProxyAvailable: true);

        Assert.Equal(TileOutcome.Null, outcome);
    }

    [Fact]
    public void should_preserve_legacy_null_when_no_default_bytes()
    {
        // (c) 타일 없음 + 기본 바이트 미주입 → 기존 동작(null) 그대로 유지(하위호환)
        var outcome = ResolveTile(
            sourceOpen: true, zoom: 15, minZoom: 0, maxZoom: 18,
            tileExists: false, defaultBytesSet: false, tileImageProxyAvailable: true);

        Assert.Equal(TileOutcome.Null, outcome);
    }

    [Fact]
    public void should_return_null_when_tile_image_proxy_unavailable()
    {
        // (c) 타일 없음 + 기본 바이트 있으나 TileImageProxy 미설정 → null (NRE 가드)
        var outcome = ResolveTile(
            sourceOpen: true, zoom: 15, minZoom: 0, maxZoom: 18,
            tileExists: false, defaultBytesSet: true, tileImageProxyAvailable: false);

        Assert.Equal(TileOutcome.Null, outcome);
    }
}
