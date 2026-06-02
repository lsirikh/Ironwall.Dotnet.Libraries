using System;
using System.IO;
using GMap.NET;
using GMap.NET.MapProviders;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services
{
    /// <summary>
    /// 오버레이 맵 전용 MBTiles Provider — 인스턴스별 독립 SQLiteConnection
    /// Base Map의 MBTilesMapProvider(싱글톤)와 달리 다중 오버레이를 동시 지원한다.
    /// </summary>
    public class MBTilesOverlayMapProvider : GMapProvider, IDisposable
    {
        private System.Data.SQLite.SQLiteConnection? _connection;
        private bool _disposed;
        private string _mbtilesPath = string.Empty;

        // GMapProvider 추상 멤버 구현 (오버레이는 GMap.NET 타일 파이프라인 미사용)
        public override Guid Id { get; } = Guid.NewGuid();
        public override string Name => $"MBTilesOverlay:{Path.GetFileNameWithoutExtension(_mbtilesPath)}";
        public override GMapProvider[] Overlays => Array.Empty<GMapProvider>();
        public override GMap.NET.Projections.MercatorProjection Projection => GMap.NET.Projections.MercatorProjection.Instance;
        public override PureImage GetTileImage(GPoint pos, int zoom) => GetTileImageInternal(pos, zoom)!;

        /// <summary>MBTiles 파일을 열고 연결한다. 실패 시 false 반환.</summary>
        public bool Open(string mbtilesPath)
        {
            if (string.IsNullOrEmpty(mbtilesPath) || !File.Exists(mbtilesPath))
                return false;

            try
            {
                _mbtilesPath = mbtilesPath;
                // Pooling=False: Dispose 후 즉시 파일 잠금 해제 (삭제 가능)
                var connStr = $"Data Source=\"{mbtilesPath}\";Pooling=False;Read Only=True;";
                _connection = new System.Data.SQLite.SQLiteConnection(connStr);
                _connection.Open();

                // 최소 검증: tiles 테이블에 데이터 존재 여부
                using var cmd = new System.Data.SQLite.SQLiteCommand(
                    "SELECT COUNT(*) FROM tiles LIMIT 1;", _connection);
                cmd.ExecuteScalar();
                return true;
            }
            catch
            {
                _connection?.Dispose();
                _connection = null;
                return false;
            }
        }

        /// <summary>파일 존재 + 연결 정상 여부</summary>
        public bool IsValid() => _connection != null && !_disposed;

        /// <summary>타일 이미지 조회. 없거나 오류 시 null 반환.</summary>
        private PureImage? GetTileImageInternal(GPoint pos, int zoom)
        {
            if (_connection == null || _disposed) return null;

            try
            {
                // MBTiles 표준: tile_row는 TMS (y축 아래→위)
                // GMap.NET pos.Y는 XYZ (y축 위→아래) → 변환 필요
                var tmsRow = ToTmsRow(zoom, pos.Y);

                using var cmd = new System.Data.SQLite.SQLiteCommand(
                    "SELECT tile_data FROM tiles WHERE zoom_level=@z AND tile_column=@x AND tile_row=@y LIMIT 1;",
                    _connection);
                cmd.Parameters.AddWithValue("@z", zoom);
                cmd.Parameters.AddWithValue("@x", pos.X);
                cmd.Parameters.AddWithValue("@y", tmsRow);

                using var rd = cmd.ExecuteReader(System.Data.CommandBehavior.SequentialAccess);
                if (!rd.Read()) return null;

                var length = rd.GetBytes(0, 0, null, 0, 0);
                var tile = new byte[length];
                rd.GetBytes(0, 0, tile, 0, tile.Length);

                return GetTileImageFromArray(tile);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>XYZ y좌표 → TMS tile_row 변환 (MBTiles 표준)</summary>
        public static long ToTmsRow(int zoom, long yXyz)
            => (long)(Math.Pow(2, zoom) - 1) - yXyz;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                _connection?.Close();
                _connection?.Dispose();
            }
            catch { /* 이미 닫힌 경우 무해 */ }
            _connection = null;
        }
    }
}
