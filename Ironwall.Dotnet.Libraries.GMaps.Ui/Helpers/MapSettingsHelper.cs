using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using System.Diagnostics;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 8/7/2025 4:16:20 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public static class MapSettingsHelper
{
    #region - Constants -
    private const string SETTINGS_FILE_NAME = "appsettings.json";
    private const string APP_SETTINGS_SECTION = "AppSettings";

    // 파일 read-modify-write 직렬화 — 동시 저장 시 손실/JSON 손상 방지(기존 무잠금 버그 수정, I-05)
    private static readonly SemaphoreSlim _fileLock = new(1, 1);
    #endregion

    #region - Public Methods -

    /// <summary>
    /// 홈 위치를 JSON에 저장
    /// </summary>
    public static async Task SaveHomePositionAsync(HomePositionModel homePosition, ILogService? log = default)
    {
        await SaveSettingAsync("HomePosition", homePosition, log);
    }

    /// <summary>
    /// 지도 타입을 JSON에 저장
    /// </summary>
    public static async Task SaveMapTypeAsync(string mapType, ILogService? log = default)
    {
        await SaveSettingAsync("MapType", mapType, log);
    }

    /// <summary>
    /// 지도 모드를 JSON에 저장
    /// </summary>
    public static async Task SaveMapModeAsync(string mapMode, ILogService? log = default)
    {
        await SaveSettingAsync("MapMode", mapMode, log);
    }

    /// <summary>
    /// 지도 이름을 JSON에 저장
    /// </summary>
    public static async Task SaveMapNameAsync(string mapName, ILogService? log = default)
    {
        await SaveSettingAsync("MapName", mapName, log);
    }

    /// <summary>
    /// 타일 디렉토리를 JSON에 저장
    /// </summary>
    public static async Task SaveTileDirectoryAsync(string tileDirectory, ILogService? log = default)
    {
        if (Directory.Exists(tileDirectory))
        {

            // 쓰기 권한 확인
            if (HasWritePermission(tileDirectory))
            {
                await SaveSettingAsync("TileDirectory", tileDirectory);
                log?.Info($"타일 디렉토리 변경 및 저장 완료: {tileDirectory}");
            }
            else
            {
                log?.Warning($"선택한 폴더에 쓰기 권한이 없습니다: {tileDirectory}");
            }
            
        }
        else
        {
            log?.Error($"유효하지 않은 폴더입니다: {tileDirectory}");

            Directory.CreateDirectory(tileDirectory);
            System.Diagnostics.Process.Start("explorer.exe", tileDirectory);
            
            log?.Info($"타일 폴더 생성 및 열기: {tileDirectory}");

            // 쓰기 권한 확인
            if (HasWritePermission(tileDirectory))
            {
                await SaveSettingAsync("TileDirectory", tileDirectory, log);
                log?.Info($"타일 디렉토리 변경 및 저장 완료: {tileDirectory}");
            }
            else
            {
                log?.Warning($"선택한 폴더에 쓰기 권한이 없습니다: {tileDirectory}");
            }
        }

        
    }

    /// <summary>
    /// 전체 지도 설정을 한번에 저장
    /// </summary>
    public static async Task SaveMapSettingsAsync(GMapSetupModel mapSettings, ILogService? log = default)
    {
        var settings = new
        {
            HomePosition = mapSettings.HomePosition,
            MapType = mapSettings.MapType,
            MapMode = mapSettings.MapMode,
            MapName = mapSettings.MapName
        };

        await SaveMultipleSettingsAsync(settings, log);
    }

    /// <summary>
    /// 현재 지도 상태를 JSON에 저장 (MapViewModel용)
    /// </summary>
    public static async Task SaveCurrentMapStateAsync(
        HomePositionModel homePosition,
        string mapName,
        double zoom,
        PointLatLng currentPosition)
    {
        var settings = new
        {
            HomePosition = homePosition,
            MapName = mapName,
            LastZoom = zoom,
            LastPosition = new
            {
                Latitude = currentPosition.Lat,
                Longitude = currentPosition.Lng
            }
        };

        await SaveMultipleSettingsAsync(settings);
    }

    /// <summary>
    /// JSON에서 지도 설정 로드
    /// </summary>
    public static async Task<GMapSetupModel> LoadMapSettingsAsync()
    {
        try
        {
            var json = await ReadSettingsFileAsync();
            var jObject = JsonConvert.DeserializeObject<JObject>(json);
            var appSettings = jObject[APP_SETTINGS_SECTION];

            if (appSettings == null)
                return CreateDefaultMapSettings();

            var mapSettings = new GMapSetupModel
            {
                HomePosition = appSettings["HomePosition"]?.ToObject<HomePositionModel>(),
                MapType = appSettings["MapType"]?.ToString(),
                MapMode = appSettings["MapMode"]?.ToString(),
                MapName = appSettings["MapName"]?.ToString()
            };

            return mapSettings;
        }
        catch (Exception)
        {
            return CreateDefaultMapSettings();
        }
    }

    /// <summary>추적 설정을 appsettings(AppSettings.Tracking)에 저장.</summary>
    public static async Task SaveTrackingSettingsAsync(ITrackingSetupModel model, ILogService? log = default)
    {
        await SaveSettingAsync("Tracking", new
        {
            model.IsTrackingEnabled,
            model.TrailMaxPoints,
            model.DefaultTtlSec,
            model.LostTtlSec,
            model.MinVisibleZoom,
            model.MaxSpeedMs,
            model.GapThresholdSec,
            model.MaxPlaybackHours,
            model.RetentionDays,
            model.DataSource,
            model.CameraAimRadiusMeters,
        }, log);
    }

    /// <summary>appsettings(AppSettings.Tracking)에서 추적 설정 로드(async, 없으면 기본값).</summary>
    public static async Task<TrackingSetupModel> LoadTrackingSettingsAsync()
    {
        try { return MapTracking(JsonConvert.DeserializeObject<JObject>(await ReadSettingsFileAsync())?[APP_SETTINGS_SECTION]?["Tracking"]); }
        catch (Exception) { return new TrackingSetupModel(); }
    }

    /// <summary>appsettings(AppSettings.Tracking)에서 추적 설정 <b>동기</b> 로드 — DI 등록(startup)용. 없으면 기본값.</summary>
    public static TrackingSetupModel LoadTrackingSettings(ILogService? log = default)
    {
        try
        {
            var fp = GetSettingsFilePath();
            if (!File.Exists(fp)) return new TrackingSetupModel();
            var model = MapTracking(JsonConvert.DeserializeObject<JObject>(File.ReadAllText(fp))?[APP_SETTINGS_SECTION]?["Tracking"]);
            log?.Info($"[TrackingSetup] 로드(appsettings.Tracking): DataSource={model.DataSource}, MaxPlaybackHours={model.MaxPlaybackHours}");
            return model;
        }
        catch (Exception ex)
        {
            log?.Warning($"[TrackingSetup] 로드 실패, 기본값 사용: {ex.Message}");
            return new TrackingSetupModel();
        }
    }

    /// <summary>JToken(AppSettings.Tracking) → TrackingSetupModel. 누락 키는 기본값 폴백.</summary>
    private static TrackingSetupModel MapTracking(JToken? t)
    {
        var def = new TrackingSetupModel();
        if (t == null) return def;
        return new TrackingSetupModel
        {
            IsTrackingEnabled = t["IsTrackingEnabled"]?.ToObject<bool>() ?? def.IsTrackingEnabled,
            TrailMaxPoints = t["TrailMaxPoints"]?.ToObject<int>() ?? def.TrailMaxPoints,
            DefaultTtlSec = t["DefaultTtlSec"]?.ToObject<int>() ?? def.DefaultTtlSec,
            LostTtlSec = t["LostTtlSec"]?.ToObject<int>() ?? def.LostTtlSec,
            MinVisibleZoom = t["MinVisibleZoom"]?.ToObject<double>() ?? def.MinVisibleZoom,
            MaxSpeedMs = t["MaxSpeedMs"]?.ToObject<double>() ?? def.MaxSpeedMs,
            GapThresholdSec = t["GapThresholdSec"]?.ToObject<int>() ?? def.GapThresholdSec,
            MaxPlaybackHours = t["MaxPlaybackHours"]?.ToObject<int>() ?? def.MaxPlaybackHours,
            RetentionDays = t["RetentionDays"]?.ToObject<int>() ?? def.RetentionDays,
            DataSource = t["DataSource"]?.ToObject<Ironwall.Dotnet.Libraries.Enums.EnumTrackDataSource>() ?? def.DataSource,
            CameraAimRadiusMeters = t["CameraAimRadiusMeters"]?.ToObject<double>() ?? def.CameraAimRadiusMeters,
        };
    }

    #endregion

    #region - Private Methods -

    /// <summary>
    /// 단일 설정 저장
    /// </summary>
    private static async Task SaveSettingAsync<T>(string propertyName, T value, ILogService? log = default)
    {
        await _fileLock.WaitAsync();
        try
        {
            var filePath = GetSettingsFilePath();

            // DebugSaveProcess에서 성공한 로직을 그대로 사용
            var beforeContent = await File.ReadAllTextAsync(filePath);
            var jObject = JsonConvert.DeserializeObject<JObject>(beforeContent);

            if (jObject == null)
            {
                jObject = new JObject();
            }

            var appSettings = jObject[APP_SETTINGS_SECTION] as JObject;
            if (appSettings == null)
            {
                appSettings = new JObject();
                jObject[APP_SETTINGS_SECTION] = appSettings;
            }

            // 값 설정 (DebugSaveProcess와 동일)
            if (value == null)
            {
                appSettings.Remove(propertyName);
            }
            else
            {
                appSettings[propertyName] = JToken.FromObject(value);
            }

            // 전체 JSON 생성
            var output = JsonConvert.SerializeObject(jObject, Formatting.Indented);

            // 변경사항 확인
            var changed = !string.Equals(beforeContent.Trim(), output.Trim());

            if (changed)
            {
                // 파일 저장 (DebugSaveProcess와 동일)
                await File.WriteAllTextAsync(filePath, output);

                // 저장 후 확인
                var afterContent = await File.ReadAllTextAsync(filePath);
                var actuallyChanged = !string.Equals(beforeContent.Trim(), afterContent.Trim());

                if (!actuallyChanged)
                {
                    throw new InvalidOperationException("파일이 실제로 변경되지 않았습니다.");
                }
            }
            log?.Info($"AppConfig Json 작성 완료: {filePath}({value})");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"지도 설정 저장 실패 '{propertyName}': {ex.Message}", ex);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// 여러 설정 동시 저장
    /// </summary>
    private static async Task SaveMultipleSettingsAsync(object settings, ILogService? log = default)
    {
        await _fileLock.WaitAsync();
        try
        {
            var filePath = GetSettingsFilePath();
            var json = await ReadSettingsFileAsync();
            var jObject = JsonConvert.DeserializeObject<JObject>(json) ?? new JObject();

            // AppSettings 섹션 확인/생성
            var appSettingsSection = jObject[APP_SETTINGS_SECTION] as JObject ?? new JObject();
            jObject[APP_SETTINGS_SECTION] = appSettingsSection;

            // 설정 객체의 모든 속성 저장
            var settingsObject = JObject.FromObject(settings);
            foreach (var property in settingsObject.Properties())
            {
                appSettingsSection[property.Name] = property.Value;
            }

            // 파일 저장
            var output = JsonConvert.SerializeObject(jObject, Formatting.Indented);
            await File.WriteAllTextAsync(filePath, output);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"지도 설정 저장 실패: {ex.Message}", ex);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// 설정 파일 읽기
    /// </summary>
    private static async Task<string> ReadSettingsFileAsync()
    {
        var filePath = GetSettingsFilePath();

        if (!File.Exists(filePath))
        {
            // 파일이 없으면 기본 구조 생성
            var defaultSettings = new JObject
            {
                [APP_SETTINGS_SECTION] = new JObject()
            };
            var defaultJson = JsonConvert.SerializeObject(defaultSettings, Formatting.Indented);
            await File.WriteAllTextAsync(filePath, defaultJson);
            return defaultJson;
        }

        return await File.ReadAllTextAsync(filePath);
    }

    /// <summary>
    /// 설정 파일 경로 반환
    /// </summary>
    private static string GetSettingsFilePath()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        return Path.Combine(baseDir, SETTINGS_FILE_NAME);
    }

    /// <summary>
    /// 기본 지도 설정 생성
    /// </summary>
    private static GMapSetupModel CreateDefaultMapSettings()
    {
        return new GMapSetupModel
        {
            MapType = "GoogleSatelliteMap",
            MapMode = "ServerAndCache",
            MapName = "기본 지도",
            HomePosition = new HomePositionModel
            {
                Position = new CoordinateModel(37.648425, 126.904284, 0.0),
                Zoom = 15,
                IsAvailable = false
            }
        };
    }

    /// <summary>
    /// 폴더 쓰기 권한 확인
    /// </summary>
    private static bool HasWritePermission(string folderPath)
    {
        try
        {
            var testFile = Path.Combine(folderPath, $"test_{Guid.NewGuid()}.tmp");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion
}