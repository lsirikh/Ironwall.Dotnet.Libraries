using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using GMap.NET.Projections;
using GMap.NET.Properties;

namespace GMap.NET.MapProviders.Custom
{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 7/28/2025 7:00:58 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    /// <summary>
    /// 파일 기반 커스텀 맵 Provider - 가장 간단한 버전
    /// 
    /// 핵심 로직:
    /// 1. 고유 GUID 생성으로 GMap.NET Provider 중복 방지
    /// 2. 타일 파일 경로: {TilesDirectory}/{zoom}/{x}/{y}.png
    /// 3. 타일 요청시 파일 존재 여부 확인 후 이미지 반환
    /// 4. 경계 좌표 설정으로 표시 영역 제한
    /// </summary>
    public class FileBasedCustomMapProvider : GMapProvider
    {
        #region Private Fields
        private readonly int _minZoom;              // 최소 줌 레벨
        private readonly int _maxZoom;              // 최대 줌 레벨
        private readonly RectLatLng? _bounds;       // 지리적 경계 (선택사항)
        private bool _disposed = false;
        private string _tilesDirectory;
        private readonly Guid _instanceId = Guid.NewGuid();  // 필드 초기화 — base() 호출 전에 할당
        private readonly byte[]? _defaultImageBytes; // 기본 이미지 바이트 캐시 (생성자에서 한 번만 로드)
        #endregion

        #region Constructor
        /// <summary>
        /// FileBasedCustomMapProvider 생성자
        /// </summary>
        /// <param name="name">Provider 이름 (예: "내 위성지도")</param>
        /// <param name="tilesDirectory">타일이 저장된 디렉토리 경로 (예: "C:\Tiles\MyMap")</param>
        /// <param name="minZoom">최소 줌 레벨 (기본값: 0)</param>
        /// <param name="maxZoom">최대 줌 레벨 (기본값: 18)</param>
        /// <param name="bounds">지리적 경계 [minLat, minLng, maxLat, maxLng] (선택사항)</param>
        public FileBasedCustomMapProvider(
            string tilesDirectory,
            int minZoom = 0,
            int maxZoom = 18,
            double[] bounds = null)
        {
            // 필드 초기화 (_instanceId는 필드 초기화에서 Guid.NewGuid()로 생성됨)
            _tilesDirectory = tilesDirectory;
            _minZoom = minZoom;
            _maxZoom = maxZoom;

            // GMap.NET Provider 설정
            MinZoom = minZoom;
            MaxZoom = maxZoom;

            // 지리적 경계 설정 (있는 경우)
            if (bounds != null && bounds.Length >= 4)
            {
                // bounds 배열: [minLat, minLng, maxLat, maxLng]
                double minLat = bounds[0];
                double minLng = bounds[1];
                double maxLat = bounds[2];
                double maxLng = bounds[3];

                // RectLatLng 생성: (북쪽위도, 서쪽경도, 경도폭, 위도폭)
                _bounds = new RectLatLng(
                    maxLat,                 // top (가장 북쪽 위도)
                    minLng,                 // left (가장 서쪽 경도)
                    maxLng - minLng,        // width (경도 폭)
                    maxLat - minLat         // height (위도 폭)
                );

                Area = _bounds; // GMap.NET에서 사용하는 영역 설정
            }

            var defaultImagePath = Path.Combine(tilesDirectory, "default_image.png");
            if (File.Exists(defaultImagePath))
                _defaultImageBytes = File.ReadAllBytes(defaultImagePath);

            System.Diagnostics.Debug.WriteLine($"[FileBasedCustomMapProvider] 생성 완료: {Name}");
            System.Diagnostics.Debug.WriteLine($"[FileBasedCustomMapProvider] GUID: {Id}");
            System.Diagnostics.Debug.WriteLine($"[FileBasedCustomMapProvider] 타일 디렉토리: {_tilesDirectory}");
        }
        #endregion

        

        #region GMapProvider Implementation

        public override Guid Id => _instanceId;

        

        public override string Name => "CustomMap";

        /// <summary>
        /// 지도 투영법 (웹 메르카토르 투영 사용)
        /// </summary>
        public override PureProjection Projection => MercatorProjection.Instance;

        /// <summary>
        /// 오버레이 Provider 배열 (자기 자신만 포함)
        /// </summary>
        private GMapProvider[] _overlays;
        public override GMapProvider[] Overlays
        {
            get
            {
                if (_overlays == null)
                    _overlays = new GMapProvider[] { this };
                return _overlays;
            }
        }


        /// <summary>
        /// 타일 이미지 가져오기 - 핵심 메서드!
        /// 
        /// 로직 설명:
        /// 1. 요청된 줌 레벨이 유효 범위인지 확인
        /// 2. 타일 파일 경로 생성: {TilesDirectory}/{zoom}/{x}/{y}.png
        /// 3. 파일 존재 여부 확인
        /// 4. 파일이 있으면 이미지로 로드하여 반환
        /// 5. 파일이 없으면 null 반환 (투명 타일 처리됨)
        /// </summary>
        /// <param name="pos">타일 좌표 (X, Y)</param>
        /// <param name="zoom">줌 레벨</param>
        /// <returns>타일 이미지 또는 null</returns>
        public override PureImage GetTileImage(GPoint pos, int zoom)
        {
            try
            {
                // 1. 줌 레벨 범위 확인
                if (zoom < _minZoom || zoom > _maxZoom)
                {
                    System.Diagnostics.Debug.WriteLine($"[타일 요청] 줌 레벨 범위 초과: {zoom} (범위: {_minZoom}-{_maxZoom})");
                    return null;
                }

                // 2. 타일 파일 경로 생성
                // 표준 타일 구조: /{zoom}/{x}/{y}.png
                var tilePath = Path.Combine(
                    _tilesDirectory,     // 예: C:\Tiles\MyMap
                    zoom.ToString(),     // 예: 15
                    pos.X.ToString(),    // 예: 26240
                    $"{pos.Y}.png"       // 예: 16383.png
                );
                // 최종 경로: C:\Tiles\MyMap\15\26240\16383.png

                // 3. 파일 존재 여부 확인
                if (!File.Exists(tilePath))
                {
                    if (_defaultImageBytes != null)
                        return GetTileImageFromArray(_defaultImageBytes);

                    tilePath = Path.Combine(_tilesDirectory, "default_image.png");
                    return GetTileImageFromFile(tilePath);
                }

                // 4. 파일에서 이미지 로드
                var tileImage = GetTileImageFromFile(tilePath);

                if (tileImage != null)
                {
                    // 파일이 없으면 기본 이미지 반환
                    //System.Diagnostics.Debug.WriteLine($"[타일 로드] 성공: zoom={zoom}, x={pos.X}, y={pos.Y}");
                    return tileImage;
                }
                else
                {
                    //System.Diagnostics.Debug.WriteLine($"[타일 로드] 이미지 로드 실패: {tilePath}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                // 오류 발생시 로그 출력 후 null 반환
                System.Diagnostics.Debug.WriteLine($"[타일 로드 오류] zoom={zoom}, x={pos.X}, y={pos.Y}: {ex.Message}");
                return null;
            }
        }
        #endregion
        #region GUID Generation
        /// <summary>
        /// 고유 GUID 생성
        /// 
        /// 로직 설명:
        /// 1. 현재 시간(틱) + Provider 이름 + 타일 경로를 결합
        /// 2. SHA256 해시로 16바이트 생성
        /// 3. 16바이트를 GUID로 변환
        /// 4. Empty GUID인 경우 완전히 새로운 GUID 생성
        /// </summary>
        private static Guid GenerateUniqueGuid(string name, string tilesDirectory)
        {
            try
            {
                // 고유성을 위한 입력 문자열 생성
                var timestamp = DateTime.UtcNow.Ticks;
                var uniqueInput = $"FileBasedCustomMapProvider_{name}_{tilesDirectory}_{timestamp}";

                System.Diagnostics.Debug.WriteLine($"[GUID 생성] 입력: {uniqueInput}");

                // SHA256 해시 생성 (32바이트)
                using (var sha256 = SHA256.Create())
                {
                    var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(uniqueInput));

                    // 처음 16바이트를 GUID로 사용
                    var guidBytes = new byte[16];
                    Array.Copy(hashBytes, guidBytes, 16);
                    var resultGuid = new Guid(guidBytes);

                    // Empty GUID 방지
                    if (resultGuid == Guid.Empty)
                    {
                        System.Diagnostics.Debug.WriteLine("[GUID 생성] Empty GUID 감지, 새 GUID 생성");
                        resultGuid = Guid.NewGuid();
                    }
                    System.Diagnostics.Debug.WriteLine($"[GUID 생성] 결과: {resultGuid}");
                    return resultGuid;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GUID 생성] 오류: {ex.Message}");
                return Guid.NewGuid(); // 오류 시 완전히 새로운 GUID
            }
        }
        #endregion

        #region Helper Methods

        /// <summary>
        /// 지리적 경계 정보 반환
        /// </summary>
        public RectLatLng? GeographicBounds => _bounds;

        /// <summary>
        /// default_image.png 바이트 (비동기 타일 로딩용 — 생성자에서 한 번만 로드됨)
        /// </summary>
        public byte[]? DefaultImageBytes => _defaultImageBytes;

        /// <summary>
        /// 타일 디렉토리 경로 반환
        /// </summary>
        public string TilesDirectory => _tilesDirectory;

        /// <summary>
        /// Provider 유효성 검사
        /// 
        /// 검사 항목:
        /// 1. 타일 디렉토리 존재 여부
        /// 2. 줌 레벨 범위 유효성
        /// 3. 최소한 하나의 타일 파일 존재 여부
        /// </summary>
        /// <summary>
        /// 지정된 줌 레벨의 타일 개수 반환
        /// </summary>
        public int GetTileCount(int zoom)
        {
            if (_disposed || zoom < _minZoom || zoom > _maxZoom)
                return 0;

            try
            {
                var zoomDir = Path.Combine(_tilesDirectory, zoom.ToString());
                if (!Directory.Exists(zoomDir)) return 0;

                int count = 0;
                foreach (var xDir in Directory.GetDirectories(zoomDir))
                {
                    count += Directory.GetFiles(xDir, "*.png").Length;
                }
                return count;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 전체 타일 개수 반환
        /// </summary>
        public int GetTotalTileCount()
        {
            if (_disposed) return 0;

            int total = 0;
            for (int zoom = _minZoom; zoom <= _maxZoom; zoom++)
            {
                total += GetTileCount(zoom);
            }
            return total;
        }

        /// <summary>
        /// 타일 디렉토리 크기 계산
        /// </summary>
        public long GetTileDirectorySize()
        {
            if (_disposed) return 0;

            try
            {
                var dirInfo = new DirectoryInfo(_tilesDirectory);
                return dirInfo.EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Provider 유효성 검사
        /// </summary>
        public bool IsValid()
        {
            if (_disposed) return false;

            try
            {
                if (!Directory.Exists(_tilesDirectory)) return false;
                if (_minZoom > _maxZoom) return false;

                // 최소한 하나의 타일이 존재하는지 확인
                for (int zoom = _minZoom; zoom <= _maxZoom; zoom++)
                {
                    if (GetTileCount(zoom) > 0) return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Provider 정보 문자열 반환
        /// </summary>
        public override string ToString()
        {
            return $"FileBasedCustomMapProvider: {Name} (줌: {_minZoom}-{_maxZoom}, 타일: {GetTotalTileCount()}, GUID: {_instanceId.ToString("N").Substring(0, 8)})";
        }


        /// <summary>
        /// 특정 타일 존재 여부 확인
        /// </summary>
        public bool HasTile(GPoint pos, int zoom)
        {
            if (zoom < _minZoom || zoom > _maxZoom)
                return false;

            var tilePath = Path.Combine(_tilesDirectory, zoom.ToString(), pos.X.ToString(), $"{pos.Y}.png");
            return File.Exists(tilePath);
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// 리소스 해제 및 GUID 등록 해제
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // 관리되는 리소스 해제
                    _overlays = null;
                }

                _disposed = true;

                System.Diagnostics.Debug.WriteLine($"[FileBasedCustomMapProvider] 해제됨: {Name}");
            }
        }

        // 소멸자
        ~FileBasedCustomMapProvider()
        {
            Dispose(false);
        }

        #endregion
        
    }
}
