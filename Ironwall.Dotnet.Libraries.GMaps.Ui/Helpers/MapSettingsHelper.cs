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
    /// <summary>
    /// Symbol3D 섹션 저장 — 섹션을 통째로 교체하므로 현재 읽은 값(간격·높이·접점 폴백)을 함께 실어 신규 키가 소실되지 않게 한다(FR-17).
    /// </summary>
    public static Task SaveSymbol3DAsync(bool isEnabled)
    {
        var current = Utils.Symbol3DFeature.Current;
        return SaveSettingAsync("Symbol3D", new
        {
            IsEnabled = isEnabled,
            current.Directory,
            current.FencePostSpacingM,
            current.FenceHeightM,
            current.DoorContactFallback,
        });
    }
    #region - Constants -
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
    /// 맵 앵커(사이트 고정) 설정을 JSON에 저장 (AppSettings.MapAnchor)
    /// </summary>
    public static async Task SaveMapAnchorAsync(MapAnchorModel anchor, ILogService? log = default)
    {
        await SaveSettingAsync("MapAnchor", anchor, log);
    }

    /// <summary>
    /// 지도 회전 상태(나침반 ON/OFF + 회전각)를 JSON에 저장 (AppSettings.MapRotation) —
    /// 사용자 요구 2026-07-28 "재시작해도 설정 유지". 부팅 복원은 ConfigureCommonMapSettings.
    /// </summary>
    public static async Task SaveMapRotationAsync(MapRotationModel rotation, ILogService? log = default)
    {
        await SaveSettingAsync("MapRotation", rotation, log);
    }

    /// <summary>
    /// 지도 2.5D 기울이기 설정(kill-switch·각도·줌 게이트)을 JSON에 저장 (AppSettings.MapTilt) —
    /// map-tilt-25d PRD FR-07. 토글은 즉시, 각도는 debounce 1 s 후 호출. 부팅 복원은 MapViewModel(홈 줌 이후).
    /// </summary>
    public static async Task SaveMapTiltAsync(MapTiltModel tilt, ILogService? log = default)
    {
        await SaveSettingAsync("MapTilt", tilt, log);
    }

    /// <summary>
    /// 방위각 심볼(나침반 컨트롤) 위치+설정을 JSON에 저장 (AppSettings.MapCompass) —
    /// GMap_Compass_Control FR-08. 부팅 복원은 MapViewModel.
    /// </summary>
    public static async Task SaveMapCompassAsync(MapCompassModel compass, ILogService? log = default)
    {
        await SaveSettingAsync("MapCompass", compass, log);
    }

    /// <summary>강풍모드 인디케이터 위치+설정 저장 (AppSettings.MapWindyIndicator) — GMap_Map_Instruments FR-12.</summary>
    public static async Task SaveMapWindyIndicatorAsync(MapWindyIndicatorModel windy, ILogService? log = default)
    {
        await SaveSettingAsync("MapWindyIndicator", windy, log);
    }

    /// <summary>탐지·장애 인디케이터 위치+설정 저장 (AppSettings.MapDetectionFault) — GMap_Map_Instruments FR-12.</summary>
    public static async Task SaveMapDetectionFaultAsync(MapDetectionFaultModel detFault, ILogService? log = default)
    {
        await SaveSettingAsync("MapDetectionFault", detFault, log);
    }

    /// <summary>계기 보기(View) 가시성 저장 (AppSettings.MapInstrumentVisibility) — GMap_Map_Instruments FR-16.</summary>
    public static async Task SaveMapInstrumentVisibilityAsync(MapInstrumentVisibilityModel vis, ILogService? log = default)
    {
        await SaveSettingAsync("MapInstrumentVisibility", vis, log);
    }

    /// <summary>이벤트 시 지도 이동 설정 저장 (AppSettings.MapEventFlyTo) — event-map-flyto PRD §3. 저장 직전 이동 시간을 다시 정규화한다(0~5000 정수).</summary>
    public static async Task SaveMapEventFlyToAsync(MapEventFlyToModel flyTo, ILogService? log = default)
    {
        flyTo.DurationMs = MapEventFlyToModel.NormalizeDurationMs(flyTo.DurationMs);
        await SaveSettingAsync("MapEventFlyTo", flyTo, log);
    }

    /// <summary>
    /// appsettings(AppSettings.MapEventFlyTo)에서 <b>동기</b> 로드 — DI 등록(startup)용. 키 없거나 실패면 기본값.
    /// 이동 시간은 여기서 정규화(파일의 9999 · 999.9999 · 음수). 최대 줌은 지도 줌 범위를 아는 MapViewModel 이 자른다.
    /// </summary>
    public static MapEventFlyToModel LoadMapEventFlyTo(ILogService? log = default)
    {
        try
        {
            var fp = AppSettingsLocation.AppSettingsReadPath;
            if (!File.Exists(fp)) return new MapEventFlyToModel();
            var t = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(fp))?[APP_SETTINGS_SECTION]?["MapEventFlyTo"];
            var model = new MapEventFlyToModel();
            if (t != null)
            {
                model.IsDetectionEnabled = t["IsDetectionEnabled"]?.ToObject<bool>() ?? model.IsDetectionEnabled;
                model.IsFaultEnabled = t["IsFaultEnabled"]?.ToObject<bool>() ?? model.IsFaultEnabled;
                // 지도 범위를 아직 모른다 — 정수로만 만들고(넉넉한 0~64, int 넘침 방지) 범위 자르기는 MapViewModel 이 한다.
                model.MaxZoom = MapEventFlyToModel.NormalizeMaxZoom(t["MaxZoom"]?.ToObject<double>() ?? model.MaxZoom, 0, 64);
                model.DurationMs = MapEventFlyToModel.NormalizeDurationMs(t["DurationMs"]?.ToObject<double>() ?? model.DurationMs);
            }
            log?.Info($"[EventFlyTo] 로드(appsettings.MapEventFlyTo): {model}");
            return model;
        }
        catch (Exception ex)
        {
            log?.Warning($"[EventFlyTo] 로드 실패, 기본값 사용: {ex.Message}");
            return new MapEventFlyToModel();
        }
    }

    /// <summary>
    /// appsettings(AppSettings.Map3D)에서 <b>동기</b> 로드 — 3D 게이트 켜기 · map-3d 서버 주소. 키 없거나 실패면 기본값(켜짐 · 127.0.0.1:8765).
    /// </summary>
    public static Map3DModel LoadMap3D(ILogService? log = default)
    {
        var model = new Map3DModel();
        try
        {
            var fp = AppSettingsLocation.AppSettingsReadPath;
            var t = File.Exists(fp) ? JsonConvert.DeserializeObject<JObject>(File.ReadAllText(fp))?[APP_SETTINGS_SECTION]?["Map3D"] : null;
            if (t != null)
            {
                model.IsEnabled = t["IsEnabled"]?.ToObject<bool>() ?? model.IsEnabled;
                model.ServerUrl = t["ServerUrl"]?.ToString() ?? model.ServerUrl;
            }
            if (model.Normalize()) log?.Warning($"[Map3D] ServerUrl 이 올바르지 않아 고쳐 씀 → {model.ServerUrl}");
            log?.Info($"[Map3D] 로드(appsettings.Map3D): {model}");
        }
        catch (Exception ex)
        {
            log?.Warning($"[Map3D] 로드 실패, 기본값 사용: {ex.Message}");
            model = new Map3DModel();
        }
        return model;
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

    // (appsettings 정리 2026-08-04) SaveTileDirectoryAsync 제거 — 호출자 0곳인 고아 헬퍼,
    // TileDirectory 키 자체가 읽기 0건으로 사장(감사 확인). 기본맵 폴더는 MapDataDirectory가 정식 후속.

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

    // (제거) SaveCurrentMapStateAsync — 호출자 0의 사장 메서드였고 LastZoom/LastPosition 키는
    // 읽는 코드가 전무한 사장 키였다(zoom-float-halfstep FR-09b 처분, Z-23). 세션 상태 저장이
    // 다시 필요해지면 실효줌(EffectiveZoom) 단일값 저장으로 재설계할 것.

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
                MapAnchor = appSettings["MapAnchor"]?.ToObject<MapAnchorModel>(),
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
            var fp = AppSettingsLocation.AppSettingsReadPath;
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
            var filePath = AppSettingsLocation.PrepareAppSettingsWrite();

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
            var filePath = AppSettingsLocation.PrepareAppSettingsWrite();
            var json = await ReadSettingsFileAsync();
            var jObject = JsonConvert.DeserializeObject<JObject>(json) ?? new JObject();

            // AppSettings 섹션 확인/생성 — 없을 때만 새로 만들어 넣는다(SaveSettingAsync 와 같은 방식).
            //   결함 NAV-D1(MAP-040): 예전엔 이미 있는 절을 그 키에 다시 대입했고, Json.NET 은 부모가 있는 토큰을 대입하면
            //   복제본을 넣으므로 아래 쓰기가 떨어져 나간 원본에만 들어가 MapName 등이 파일에 남지 않았다(재시작하면 이전 지도).
            if (jObject[APP_SETTINGS_SECTION] is not JObject appSettingsSection)
            {
                appSettingsSection = new JObject();
                jObject[APP_SETTINGS_SECTION] = appSettingsSection;
            }

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
        var filePath = AppSettingsLocation.AppSettingsReadPath;

        if (!File.Exists(filePath))
        {
            // 파일이 없으면 기본 구조 생성
            var defaultSettings = new JObject
            {
                [APP_SETTINGS_SECTION] = new JObject()
            };
            var defaultJson = JsonConvert.SerializeObject(defaultSettings, Formatting.Indented);
            await File.WriteAllTextAsync(AppSettingsLocation.PrepareAppSettingsWrite(), defaultJson);
            return defaultJson;
        }

        return await File.ReadAllTextAsync(filePath);
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
