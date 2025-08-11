using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Maps;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Monitoring.Models.Maps;
#region - 공통 Map 확장 메서드 -
/// <summary>
/// 모든 지도 모델에 공통 적용되는 확장 메서드
/// </summary>
public static class MapExtensions
{
    /// <summary>
    /// 지도 사용 가능 여부 확인
    /// </summary>
    public static bool IsAvailable(this IMapModel map)
    {
        return map?.Status == EnumMapStatus.Active;
    }

    /// <summary>
    /// 유효한 경계 좌표 확인
    /// </summary>
    public static bool HasValidBounds(this IMapModel map)
    {
        return map != null &&
               map.MinLatitude.HasValue && map.MaxLatitude.HasValue &&
               map.MinLongitude.HasValue && map.MaxLongitude.HasValue &&
               map.MinLatitude < map.MaxLatitude &&
               map.MinLongitude < map.MaxLongitude;
    }

    /// <summary>
    /// 표시용 이름 생성
    /// </summary>
    public static string GetDisplayName(this IMapModel map)
    {
        if (map == null) return "Unknown Map";
        return $"{map.Name} ({map.ProviderType} - {map.Category})";
    }

    /// <summary>
    /// 경계 영역 계산 (제곱도)
    /// </summary>
    public static double? GetBoundsArea(this IMapModel map)
    {
        if (!map.HasValidBounds()) return null;

        var latDiff = map.MaxLatitude!.Value - map.MinLatitude!.Value;
        var lngDiff = map.MaxLongitude!.Value - map.MinLongitude!.Value;
        return latDiff * lngDiff;
    }

    /// <summary>
    /// 지도 나이 계산 (생성 후 경과 일수)
    /// </summary>
    public static int GetAgeInDays(this IMapModel map)
    {
        if (map == null) return 0;
        return (DateTime.Now - map.CreatedAt).Days;
    }

    /// <summary>
    /// 커스텀 지도인지 확인
    /// </summary>
    public static bool IsCustomMap(this IMapModel map)
    {
        return map?.ProviderType == EnumMapProvider.Custom;
    }

    /// <summary>
    /// 상태별 색상 반환
    /// </summary>
    public static string GetStatusColor(this IMapModel map)
    {
        if (map == null) return "#000000";

        return map.Status switch
        {
            EnumMapStatus.Active => "#4CAF50",      // 녹색
            EnumMapStatus.Processing => "#FF9800",  // 주황색
            EnumMapStatus.Inactive => "#9E9E9E",    // 회색
            EnumMapStatus.Error => "#F44336",       // 빨간색
            EnumMapStatus.Deleted => "#424242",     // 진한 회색
            _ => "#000000"
        };
    }

    /// <summary>
    /// 상태별 아이콘 반환
    /// </summary>
    public static string GetStatusIcon(this IMapModel map)
    {
        if (map == null) return "❓";

        return map.Status switch
        {
            EnumMapStatus.Active => "✅",
            EnumMapStatus.Processing => "⏳",
            EnumMapStatus.Inactive => "⏸️",
            EnumMapStatus.Error => "❌",
            EnumMapStatus.Deleted => "🗑️",
            _ => "❓"
        };
    }
}
#endregion
#region - 공통 유효성 검사 확장 메서드 -
/// <summary>
/// 모든 지도 모델의 유효성 검사 확장 메서드
/// </summary>
public static class MapValidationExtensions
{
    /// <summary>
    /// 기본 지도 유효성 검사
    /// </summary>
    public static List<string> Validate(this IMapModel map)
    {
        var errors = new List<string>();

        if (map == null)
        {
            errors.Add("지도 객체가 null입니다.");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(map.Name))
            errors.Add("지도 이름은 필수입니다.");

        if (map.Name?.Length > 100)
            errors.Add("지도 이름은 100자를 초과할 수 없습니다.");

        if (map.MinZoomLevel < 0 || map.MinZoomLevel > 20)
            errors.Add("최소 줌 레벨은 0-20 범위여야 합니다.");

        if (map.MaxZoomLevel < 0 || map.MaxZoomLevel > 20)
            errors.Add("최대 줌 레벨은 0-20 범위여야 합니다.");

        if (map.MinZoomLevel > map.MaxZoomLevel)
            errors.Add("최소 줌 레벨은 최대 줌 레벨보다 작아야 합니다.");

        if (map.TileSize <= 0 || map.TileSize > 1024)
            errors.Add("타일 크기는 1-1024 범위여야 합니다.");

        // 좌표 검증
        if (map.HasValidBounds())
        {
            if (map.MinLatitude < -90 || map.MinLatitude > 90)
                errors.Add("최소 위도는 -90도에서 90도 범위여야 합니다.");

            if (map.MaxLatitude < -90 || map.MaxLatitude > 90)
                errors.Add("최대 위도는 -90도에서 90도 범위여야 합니다.");

            if (map.MinLongitude < -180 || map.MinLongitude > 180)
                errors.Add("최소 경도는 -180도에서 180도 범위여야 합니다.");

            if (map.MaxLongitude < -180 || map.MaxLongitude > 180)
                errors.Add("최대 경도는 -180도에서 180도 범위여야 합니다.");
        }

        return errors;
    }

    /// <summary>
    /// 유효성 검사 통과 여부
    /// </summary>
    public static bool IsValid(this IMapModel map)
    {
        return !map.Validate().Any();
    }
}
#endregion

#region - 커스텀 지도 전용 확장 메서드 -
/// <summary>
/// 커스텀 지도 전용 확장 메서드
/// </summary>
public static class CustomMapExtensions
{
    /// <summary>
    /// 처리 완료 여부 확인
    /// </summary>
    public static bool IsProcessed(this ICustomMapModel map)
    {
        return map?.ProcessedAt.HasValue == true &&
               map.Status == EnumMapStatus.Active &&
               map.TotalTileCount > 0;
    }

    /// <summary>
    /// 타일 크기를 사람이 읽기 쉬운 형태로 변환
    /// </summary>
    public static string GetFormattedSize(this ICustomMapModel map)
    {
        if (map == null) return "0 B";
        return FormatBytes(map.TilesDirectorySize);
    }

    /// <summary>
    /// 원본 파일 크기를 사람이 읽기 쉬운 형태로 변환
    /// </summary>
    public static string GetFormattedOriginalSize(this ICustomMapModel map)
    {
        if (map == null) return "0 B";
        return FormatBytes(map.OriginalFileSize);
    }

    /// <summary>
    /// 처리 상태 텍스트 반환
    /// </summary>
    public static string GetProcessingStatus(this ICustomMapModel map)
    {
        if (map == null) return "알 수 없음";

        return map.Status switch
        {
            EnumMapStatus.Processing => $"처리 중 ({map.CalculateProcessingProgress():F1}%)",
            EnumMapStatus.Active when map.IsProcessed() => map.GetQualityStatusText(),
            EnumMapStatus.Active => "처리 완료",
            EnumMapStatus.Error => "처리 실패",
            EnumMapStatus.Inactive => "비활성화",
            EnumMapStatus.Deleted => "삭제됨",
            _ => "알 수 없음"
        };
    }

    /// <summary>
    /// 이미지 크기 정보
    /// </summary>
    public static string GetImageDimensions(this ICustomMapModel map)
    {
        if (map == null) return "정보 없음";
        return $"{map.OriginalWidth:N0} x {map.OriginalHeight:N0}";
    }

    /// <summary>
    /// 해상도 정보
    /// </summary>
    public static string GetResolution(this ICustomMapModel map)
    {
        if (map == null || !map.PixelResolutionX.HasValue || !map.PixelResolutionY.HasValue)
            return "정보 없음";

        return $"{map.PixelResolutionX:F6} x {map.PixelResolutionY:F6} {map.ResolutionUnit}";
    }

    /// <summary>
    /// 기준점 추가
    /// </summary>
    public static ICustomMapModel AddControlPoint(this ICustomMapModel map, IGeoControlPointModel point)
    {
        if (map == null || point == null) return map;

        map.ControlPoints.Add(point);
        map.ControlPointCount = map.ControlPoints.Count;
        map.UpdatedAt = DateTime.Now;
        return map;
    }

    /// <summary>
    /// 기준점 제거
    /// </summary>
    public static ICustomMapModel RemoveControlPoint(this ICustomMapModel map, int pointId)
    {
        if (map == null) return map;

        var point = map.ControlPoints.FirstOrDefault(p => p.Id == pointId);
        if (point != null)
        {
            map.ControlPoints.Remove(point);
            map.ControlPointCount = map.ControlPoints.Count;
            map.UpdatedAt = DateTime.Now;
        }
        return map;
    }

    /// <summary>
    /// 모든 기준점 제거
    /// </summary>
    public static ICustomMapModel ClearControlPoints(this ICustomMapModel map)
    {
        if (map == null) return map;

        map.ControlPoints.Clear();
        map.ControlPointCount = 0;
        map.UpdatedAt = DateTime.Now;
        return map;
    }

    /// <summary>
    /// 원본 이미지 파일 유효성 확인
    /// </summary>
    public static bool ValidateSourceImage(this ICustomMapModel map)
    {
        return map != null &&
               !string.IsNullOrEmpty(map.SourceImagePath) &&
               File.Exists(map.SourceImagePath) &&
               new FileInfo(map.SourceImagePath).Length > 0;
    }

    /// <summary>
    /// 타일 디렉토리 유효성 확인
    /// </summary>
    public static bool ValidateTilesDirectory(this ICustomMapModel map)
    {
        return map != null &&
               !string.IsNullOrEmpty(map.TilesDirectoryPath) &&
               Directory.Exists(map.TilesDirectoryPath);
    }

    /// <summary>
    /// 처리 진행률 계산
    /// </summary>
    public static double CalculateProcessingProgress(this ICustomMapModel map)
    {
        if (map == null) return 0.0;

        return map.Status switch
        {
            EnumMapStatus.Processing => map.TotalTileCount > 0 ? Math.Min(95.0, map.TotalTileCount * 0.01) : 10.0,
            EnumMapStatus.Active => 100.0,
            EnumMapStatus.Error => 0.0,
            _ => 0.0
        };
    }

    /// <summary>
    /// 품질 상태 텍스트 반환
    /// </summary>
    public static string GetQualityStatusText(this ICustomMapModel map)
    {
        if (map?.QualityScore == null) return "처리완료";

        return map.QualityScore.Value switch
        {
            >= 0.9 => "우수",
            >= 0.8 => "양호",
            >= 0.6 => "보통",
            >= 0.4 => "주의",
            _ => "불량"
        };
    }

    /// <summary>
    /// 커스텀 지도 유효성 검사 (기본 검사 + 커스텀 특화 검사)
    /// </summary>
    public static List<string> ValidateCustomMap(this ICustomMapModel map)
    {
        var errors = ((IMapModel)map).Validate(); // 기본 유효성 검사 먼저

        if (map == null) return errors;

        if (string.IsNullOrWhiteSpace(map.SourceImagePath))
            errors.Add("원본 이미지 경로는 필수입니다.");

        if (string.IsNullOrWhiteSpace(map.TilesDirectoryPath))
            errors.Add("타일 디렉토리 경로는 필수입니다.");

        if (!map.ValidateSourceImage())
            errors.Add("원본 이미지 파일이 존재하지 않거나 비어있습니다.");

        if (map.OriginalWidth <= 0)
            errors.Add("원본 이미지 너비는 0보다 커야 합니다.");

        if (map.OriginalHeight <= 0)
            errors.Add("원본 이미지 높이는 0보다 커야 합니다.");

        if (map.QualityScore.HasValue && (map.QualityScore < 0 || map.QualityScore > 1))
            errors.Add("품질 점수는 0-1 범위여야 합니다.");

        if (map.GeoReferenceMethod == EnumGeoReference.ManualControlPoints && map.ControlPoints.Count < 4)
            errors.Add("수동 기준점 방식은 최소 4개의 기준점이 필요합니다.");

        return errors;
    }

    /// <summary>
    /// 파일 크기 포맷팅 헬퍼
    /// </summary>
    private static string FormatBytes(long bytes)
    {
        if (bytes == 0) return "0 B";
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }
        return $"{number:n1} {suffixes[counter]}";
    }
}
#endregion

#region - 기존 제공자 지도 전용 확장 메서드 -
/// <summary>
/// 기존 제공자 지도 전용 확장 메서드
/// </summary>
public static class DefinedMapExtensions
{
    /// <summary>
    /// API 키 보유 여부 확인
    /// </summary>
    public static bool HasApiKey(this IDefinedMapModel map)
    {
        return map != null &&
               (!map.RequiresApiKey || !string.IsNullOrEmpty(map.ApiKey));
    }

    /// <summary>
    /// 일일 사용량 제한 확인
    /// </summary>
    public static bool IsWithinDailyLimit(this IDefinedMapModel map)
    {
        if (map?.DailyRequestLimit == null) return true;
        return (map.TodayUsageCount ?? 0) < map.DailyRequestLimit.Value;
    }

    /// <summary>
    /// 벤더 표시명 반환
    /// </summary>
    public static string GetVendorDisplayName(this IDefinedMapModel map)
    {
        if (map == null) return "Unknown";
        return $"{map.Vendor} {map.Style}";
    }

    /// <summary>
    /// API 키 유효성 검사
    /// </summary>
    public static bool ValidateApiKey(this IDefinedMapModel map)
    {
        if (map == null || !map.RequiresApiKey) return true;
        if (string.IsNullOrEmpty(map.ApiKey)) return false;

        return map.Vendor switch
        {
            EnumMapVendor.Google => map.ApiKey.StartsWith("AIza") && map.ApiKey.Length == 39,
            EnumMapVendor.Microsoft => map.ApiKey.Length == 64,
            EnumMapVendor.OpenStreetMap => true, // API 키 불필요
            _ => !string.IsNullOrEmpty(map.ApiKey)
        };
    }

    /// <summary>
    /// 사용량 기록
    /// </summary>
    public static IDefinedMapModel RecordUsage(this IDefinedMapModel map)
    {
        if (map == null) return map;

        map.LastAccessedAt = DateTime.Now;
        map.TodayUsageCount = (map.TodayUsageCount ?? 0) + 1;

        // 날짜가 바뀌었으면 사용량 리셋
        if (map.LastAccessedAt.Value.Date != DateTime.Today)
        {
            map.ResetDailyUsage();
        }

        return map;
    }

    /// <summary>
    /// 일일 사용량 리셋
    /// </summary>
    public static IDefinedMapModel ResetDailyUsage(this IDefinedMapModel map)
    {
        if (map != null)
        {
            map.TodayUsageCount = 0;
        }
        return map;
    }

    /// <summary>
    /// 서비스 사용 가능 여부 확인
    /// </summary>
    public static bool IsServiceAvailable(this IDefinedMapModel map)
    {
        if (map == null) return false;

        // 기본적인 가용성 체크
        if (!map.HasApiKey()) return false;
        if (!map.IsWithinDailyLimit()) return false;

        // 벤더별 특수 조건
        return map.Vendor switch
        {
            EnumMapVendor.OpenStreetMap => true, // 항상 사용 가능
            EnumMapVendor.Google or EnumMapVendor.Microsoft => map.HasApiKey(),
            _ => true
        };
    }

    /// <summary>
    /// GMap.NET Provider 인스턴스 이름 생성
    /// </summary>
    public static string GetProviderInstanceName(this IDefinedMapModel map)
    {
        if (map == null) return "UnknownProvider";

        return (map.Vendor, map.Style) switch
        {
            (EnumMapVendor.Google, EnumMapStyle.Normal) => "GoogleMapProvider",
            (EnumMapVendor.Google, EnumMapStyle.Satellite) => "GoogleSatelliteMapProvider",
            (EnumMapVendor.Google, EnumMapStyle.Hybrid) => "GoogleHybridMapProvider",
            (EnumMapVendor.Google, EnumMapStyle.Terrain) => "GoogleTerrainMapProvider",
            (EnumMapVendor.Microsoft, EnumMapStyle.Roads) => "BingMapProvider",
            (EnumMapVendor.Microsoft, EnumMapStyle.Satellite) => "BingSatelliteMapProvider",
            (EnumMapVendor.Microsoft, EnumMapStyle.Hybrid) => "BingHybridMapProvider",
            (EnumMapVendor.OpenStreetMap, EnumMapStyle.Normal) => "OpenStreetMapProvider",
            _ => $"{map.Vendor}{map.Style}MapProvider"
        };
    }

    /// <summary>
    /// Provider 설정 정보 반환
    /// </summary>
    public static Dictionary<string, object> GetProviderSettings(this IDefinedMapModel map)
    {
        var settings = new Dictionary<string, object>();

        if (map == null) return settings;

        settings["ProviderName"] = map.GetProviderInstanceName();
        settings["Vendor"] = map.Vendor.ToString();
        settings["Style"] = map.Style.ToString();
        settings["RequiresApiKey"] = map.RequiresApiKey;

        if (!string.IsNullOrEmpty(map.ApiKey))
            settings["ApiKey"] = map.ApiKey;

        if (!string.IsNullOrEmpty(map.ServiceUrl))
            settings["ServiceUrl"] = map.ServiceUrl;

        if (map.DailyRequestLimit.HasValue)
            settings["DailyRequestLimit"] = map.DailyRequestLimit.Value;

        return settings;
    }

    /// <summary>
    /// 사용량 정보 텍스트 반환
    /// </summary>
    public static string GetUsageInfo(this IDefinedMapModel map)
    {
        if (map?.DailyRequestLimit == null) return "";

        var usage = map.TodayUsageCount ?? 0;
        var limit = map.DailyRequestLimit.Value;
        var percentage = (double)usage / limit * 100;

        return $"{usage:N0}/{limit:N0} ({percentage:F1}%)";
    }

    /// <summary>
    /// API 키 상태 아이콘 반환
    /// </summary>
    public static string GetApiKeyStatusIcon(this IDefinedMapModel map)
    {
        if (map == null) return "❓";

        if (!map.RequiresApiKey) return "🆓"; // 무료
        if (map.HasApiKey() && map.ValidateApiKey()) return "✅"; // 유효
        if (map.HasApiKey() && !map.ValidateApiKey()) return "⚠️"; // 유효하지 않음
        return "❌"; // API 키 없음
    }

    /// <summary>
    /// 기존 제공자 지도 유효성 검사 (기본 검사 + 제공자 특화 검사)
    /// </summary>
    public static List<string> ValidateDefinedMap(this IDefinedMapModel map)
    {
        var errors = ((IMapModel)map).Validate(); // 기본 유효성 검사 먼저

        if (map == null) return errors;

        if (string.IsNullOrWhiteSpace(map.GMapProviderName))
            errors.Add("GMap.NET Provider 이름은 필수입니다.");

        if (map.RequiresApiKey && string.IsNullOrEmpty(map.ApiKey))
            errors.Add("API 키가 필요합니다.");

        if (!string.IsNullOrEmpty(map.ApiKey) && !map.ValidateApiKey())
            errors.Add("API 키 형식이 올바르지 않습니다.");

        if (map.DailyRequestLimit.HasValue && map.DailyRequestLimit <= 0)
            errors.Add("일일 요청 제한은 0보다 커야 합니다.");

        // 벤더-스타일 조합 검증
        if (!IsValidVendorStyleCombination(map.Vendor, map.Style))
            errors.Add($"{map.Vendor}는 {map.Style} 스타일을 지원하지 않습니다.");

        return errors;
    }

    /// <summary>
    /// 벤더-스타일 조합 유효성 확인
    /// </summary>
    private static bool IsValidVendorStyleCombination(EnumMapVendor vendor, EnumMapStyle style)
    {
        return vendor switch
        {
            EnumMapVendor.Google => style == EnumMapStyle.Normal ||
                                   style == EnumMapStyle.Satellite ||
                                   style == EnumMapStyle.Hybrid ||
                                   style == EnumMapStyle.Terrain,

            EnumMapVendor.Microsoft => style == EnumMapStyle.Roads ||
                                      style == EnumMapStyle.Satellite ||
                                      style == EnumMapStyle.Hybrid,

            EnumMapVendor.OpenStreetMap => style == EnumMapStyle.Normal,

            _ => true // 기타 벤더는 모든 스타일 허용
        };
    }
}
#endregion

#region - 지리참조 기준점 확장 메서드 -
/// <summary>
/// 지리참조 기준점 확장 메서드
/// </summary>
public static class GeoControlPointExtensions
{
    /// <summary>
    /// 픽셀 좌표 텍스트 반환
    /// </summary>
    public static string GetPixelCoordText(this IGeoControlPointModel point)
    {
        if (point == null) return "(0, 0)";
        return $"({point.PixelX:F0}, {point.PixelY:F0})";
    }

    /// <summary>
    /// 지리 좌표 텍스트 반환
    /// </summary>
    public static string GetGeoCoordText(this IGeoControlPointModel point)
    {
        if (point == null) return "(0.000000, 0.000000)";
        return $"({point.Latitude:F6}, {point.Longitude:F6})";
    }

    /// <summary>
    /// 좌표 유효성 확인
    /// </summary>
    public static bool IsValidCoordinate(this IGeoControlPointModel point)
    {
        return point != null &&
               point.Latitude >= -90 && point.Latitude <= 90 &&
               point.Longitude >= -180 && point.Longitude <= 180 &&
               point.PixelX >= 0 && point.PixelY >= 0;
    }

    /// <summary>
    /// 다른 기준점과의 거리 계산 (Haversine 공식)
    /// </summary>
    public static double CalculateDistanceTo(this IGeoControlPointModel point, IGeoControlPointModel other)
    {
        if (point == null || other == null) return double.MaxValue;

        // Haversine 공식을 사용한 거리 계산 (미터)
        const double R = 6371000; // 지구 반지름 (미터)

        var lat1Rad = point.Latitude * Math.PI / 180;
        var lat2Rad = other.Latitude * Math.PI / 180;
        var deltaLatRad = (other.Latitude - point.Latitude) * Math.PI / 180;
        var deltaLngRad = (other.Longitude - point.Longitude) * Math.PI / 180;

        var a = Math.Sin(deltaLatRad / 2) * Math.Sin(deltaLatRad / 2) +
                Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                Math.Sin(deltaLngRad / 2) * Math.Sin(deltaLngRad / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return R * c;
    }

    /// <summary>
    /// 정확도 텍스트 반환
    /// </summary>
    public static string GetAccuracyText(this IGeoControlPointModel point)
    {
        if (point?.AccuracyMeters == null) return "정확도 정보 없음";

        return point.AccuracyMeters.Value switch
        {
            < 1 => $"{point.AccuracyMeters.Value * 100:F0}cm",
            < 1000 => $"{point.AccuracyMeters.Value:F1}m",
            _ => $"{point.AccuracyMeters.Value / 1000:F2}km"
        };
    }
}
#endregion

#region - 컬렉션 확장 메서드 -
/// <summary>
/// 지도 컬렉션 전용 확장 메서드
/// </summary>
public static class MapCollectionExtensions
{
    /// <summary>
    /// 사용 가능한 지도들만 필터링
    /// </summary>
    public static IEnumerable<T> WhereAvailable<T>(this IEnumerable<T> maps) where T : IMapModel
    {
        return maps?.Where(m => m.IsAvailable()) ?? Enumerable.Empty<T>();
    }

    /// <summary>
    /// 특정 카테고리의 지도들만 필터링
    /// </summary>
    public static IEnumerable<T> WhereCategory<T>(this IEnumerable<T> maps, EnumMapCategory category) where T : IMapModel
    {
        return maps?.Where(m => m.Category == category) ?? Enumerable.Empty<T>();
    }

    /// <summary>
    /// 커스텀 지도들만 필터링
    /// </summary>
    public static IEnumerable<ICustomMapModel> WhereCustomMaps(this IEnumerable<IMapModel> maps)
    {
        return maps?.OfType<ICustomMapModel>() ?? Enumerable.Empty<ICustomMapModel>();
    }

    /// <summary>
    /// 기존 제공자 지도들만 필터링
    /// </summary>
    public static IEnumerable<IDefinedMapModel> WhereDefinedMaps(this IEnumerable<IMapModel> maps)
    {
        return maps?.OfType<IDefinedMapModel>() ?? Enumerable.Empty<IDefinedMapModel>();
    }

    /// <summary>
    /// 처리 완료된 커스텀 지도들만 필터링
    /// </summary>
    public static IEnumerable<ICustomMapModel> WhereProcessed(this IEnumerable<ICustomMapModel> maps)
    {
        return maps?.Where(m => m.IsProcessed()) ?? Enumerable.Empty<ICustomMapModel>();
    }

    /// <summary>
    /// API 키가 설정된 제공자 지도들만 필터링
    /// </summary>
    public static IEnumerable<IDefinedMapModel> WhereHasApiKey(this IEnumerable<IDefinedMapModel> maps)
    {
        return maps?.Where(m => m.HasApiKey()) ?? Enumerable.Empty<IDefinedMapModel>();
    }

    /// <summary>
    /// 각 지도에 대해 액션 실행
    /// </summary>
    public static void ForEach<T>(this IEnumerable<T> maps, Action<T> action) where T : IMapModel
    {
        if (maps == null || action == null) return;

        foreach (var map in maps)
        {
            action(map);
        }
    }

    /// <summary>
    /// 지도 통계 정보 계산
    /// </summary>
    public static MapCollectionStats GetStats(this IEnumerable<IMapModel> maps)
    {
        if (maps == null) return new MapCollectionStats();

        var mapList = maps.ToList();
        var customMaps = mapList.WhereCustomMaps().ToList();
        var definedMaps = mapList.WhereDefinedMaps().ToList();

        return new MapCollectionStats
        {
            TotalCount = mapList.Count,
            AvailableCount = mapList.WhereAvailable().Count(),
            CustomMapCount = customMaps.Count,
            DefinedMapCount = definedMaps.Count,
            ProcessedCustomMapCount = customMaps.WhereProcessed().Count(),
            ApiKeyConfiguredCount = definedMaps.WhereHasApiKey().Count(),
            TotalTileSize = customMaps.Sum(m => m.TilesDirectorySize),
            AverageAge = mapList.Any() ? (int)mapList.Average(m => m.GetAgeInDays()) : 0
        };
    }
}

/// <summary>
/// 지도 컬렉션 통계 정보
/// </summary>
public class MapCollectionStats
{
    public int TotalCount { get; set; }
    public int AvailableCount { get; set; }
    public int CustomMapCount { get; set; }
    public int DefinedMapCount { get; set; }
    public int ProcessedCustomMapCount { get; set; }
    public int ApiKeyConfiguredCount { get; set; }
    public long TotalTileSize { get; set; }
    public int AverageAge { get; set; }

    public string GetFormattedTileSize()
    {
        return FormatBytes(TotalTileSize);
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes == 0) return "0 B";
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }
        return $"{number:n1} {suffixes[counter]}";
    }
}
#endregion

#region - 체이닝 확장 메서드 -
/// <summary>
/// 체이닝을 위한 유틸리티 확장 메서드
/// </summary>
public static class ChainingExtensions
{
    /// <summary>
    /// 조건부 실행
    /// </summary>
    public static T If<T>(this T obj, bool condition, Action<T> action) where T : class
    {
        if (condition && obj != null)
            action?.Invoke(obj);
        return obj;
    }

    /// <summary>
    /// 액션 실행 후 자기 반환 (체이닝용)
    /// </summary>
    public static T Do<T>(this T obj, Action<T> action) where T : class
    {
        if (obj != null)
            action?.Invoke(obj);
        return obj;
    }

    /// <summary>
    /// 디버그 출력 후 자기 반환
    /// </summary>
    public static T Dump<T>(this T obj, string label = "") where T : class
    {
        if (obj != null)
        {
            var displayText = obj is IMapModel map ? map.GetDisplayName() : obj.ToString();
            Console.WriteLine($"{label}: {displayText}");
        }
        return obj;
    }

    /// <summary>
    /// null이 아닌 경우에만 실행
    /// </summary>
    public static TResult? SafeCall<T, TResult>(this T obj, Func<T, TResult> func) where T : class
    {
        return obj == null ? default(TResult) : func(obj);
    }
}
#endregion
