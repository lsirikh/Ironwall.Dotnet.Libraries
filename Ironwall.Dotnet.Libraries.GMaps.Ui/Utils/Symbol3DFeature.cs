using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

/// <summary>
/// Read once per process (appsettings AppSettings.Symbol3D.IsEnabled). 3D housing is opt-in: existing installations retain 2D
/// until explicitly enabled. Note: the 2D fallback glyph set (<see cref="GMapSymbols.GMapMarkerPidsFallbackControl"/>, 8 device types
/// that had no dedicated 2D art) is a 2D improvement independent of this flag (R-2 decision).
/// </summary>
/// <summary>
/// <c>AppSettings.Symbol3D</c> 섹션의 전체 값(FR-17). 키가 없으면 각 기본값으로 폴백한다 —
/// 설치본 appsettings 에 신규 키가 없어도 동작이 바뀌지 않도록.
/// </summary>
/// <param name="IsEnabled">3D 하우징 전역 on/off(프로세스당 1회 읽기)</param>
/// <param name="Directory">OBJ 검색 루트(null=기본 위치)</param>
/// <param name="FencePostSpacingM">그룹 값이 NULL 일 때 쓰는 철망 기둥 간격(m)</param>
/// <param name="FenceHeightM">그룹 값이 NULL 일 때 쓰는 철망 높이(m)</param>
/// <param name="DoorContactFallback">SYNC_DEVICE/OPERATION_EVENT 부재 시 접점 이벤트로 개폐를 유도할지(R-02)</param>
public sealed record Symbol3DSettings(
    bool IsEnabled,
    string? Directory,
    double FencePostSpacingM = Helpers.Fence.FenceDefaults.PostSpacingM,
    double FenceHeightM = Helpers.Fence.FenceDefaults.FenceHeightM,
    bool DoorContactFallback = true)
{
    public static Symbol3DSettings Disabled { get; } = new(false, null);
}

public static class Symbol3DFeature
{
    private static readonly Lazy<Symbol3DSettings> Settings = new(Read);
    /// <summary>프로세스당 1회 읽은 설정 전체(FR-17).</summary>
    public static Symbol3DSettings Current => Settings.Value;
    public static bool IsEnabled => Current.IsEnabled;
    public static string? Directory => Current.Directory;
    public static double FencePostSpacingM => Current.FencePostSpacingM;
    public static double FenceHeightM => Current.FenceHeightM;
    public static bool DoorContactFallback => Current.DoorContactFallback;
    /// <summary>실제로 읽은 파일 경로와 결과/오류 — 진단용(플래그가 왜 false 인지 추적).</summary>
    public static string? LastDiagnostic { get; private set; }

    private static Symbol3DSettings Read()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        var (settings, error) = ReadSettings(path);
        LastDiagnostic = $"{path} → IsEnabled={settings.IsEnabled} · PostSpacing={settings.FencePostSpacingM}m · Height={settings.FenceHeightM}m · ContactFallback={settings.DoorContactFallback}{(error is null ? "" : " · " + error)}";
        if (error is not null) System.Diagnostics.Trace.TraceWarning("Symbol3D settings: {0}", error);
        return settings;
    }

    /// <summary>하위 호환 — (enabled, directory, error) 튜플. 신규 코드는 <see cref="ReadSettings"/> 를 쓴다.</summary>
    public static (bool enabled, string? directory, string? error) ReadFile(string path)
    {
        var (settings, error) = ReadSettings(path);
        return (settings.IsEnabled, settings.Directory, error);
    }

    /// <summary>
    /// 주어진 appsettings.json 을 앱과 동일한 규칙으로 해석한다(<c>AppSettings.Symbol3D</c> 또는 루트 <c>Symbol3D</c>).
    /// 예외는 삼키지 않고 <c>error</c> 로 돌려준다 — 테스트·진단이 "왜 false 인지"를 볼 수 있게.
    /// 개별 키의 타입 오류는 해당 키만 기본값으로 폴백하고 <c>error</c> 에 병기한다(섹션 전체를 잃지 않는다).
    /// </summary>
    public static (Symbol3DSettings settings, string? error) ReadSettings(string path)
    {
        try
        {
            var root = JObject.Parse(File.ReadAllText(path));
            var section = root["AppSettings"]?["Symbol3D"] ?? root["Symbol3D"];
            if (section is null) return (Symbol3DSettings.Disabled, "Symbol3D 섹션 없음");

            var problems = new List<string>();
            bool ReadBool(string key, bool fallback)
            {
                var t = section[key];
                if (t is null || t.Type == JTokenType.Null) return fallback;
                if (t.Type == JTokenType.Boolean) return t.Value<bool>();
                if (t.Type == JTokenType.String && bool.TryParse(t.Value<string>(), out var b)) return b;
                problems.Add($"{key}: '{t}' 는 bool 이 아님 → {fallback}"); return fallback;
            }
            double ReadRange(string key, double fallback, double min, double max)
            {
                var t = section[key];
                if (t is null || t.Type == JTokenType.Null) return fallback;
                double v;
                if (t.Type is JTokenType.Float or JTokenType.Integer) v = t.Value<double>();
                else if (t.Type == JTokenType.String && double.TryParse(t.Value<string>(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed)) v = parsed;
                else { problems.Add($"{key}: '{t}' 는 숫자가 아님 → {fallback}"); return fallback; }
                if (double.IsFinite(v) && v >= min && v <= max) return v;
                problems.Add($"{key}: {v} 는 {min}~{max} 범위 밖 → {fallback}"); return fallback;
            }

            var settings = new Symbol3DSettings(
                IsEnabled: ReadBool("IsEnabled", false),
                Directory: section["Directory"]?.Type == JTokenType.String ? section["Directory"]!.Value<string>() : null,
                FencePostSpacingM: ReadRange("FencePostSpacingM", Helpers.Fence.FenceDefaults.PostSpacingM, Helpers.Fence.FenceDefaults.PostSpacingMinM, Helpers.Fence.FenceDefaults.PostSpacingMaxM),
                FenceHeightM: ReadRange("FenceHeightM", Helpers.Fence.FenceDefaults.FenceHeightM, Helpers.Fence.FenceDefaults.FenceHeightMinM, Helpers.Fence.FenceDefaults.FenceHeightMaxM),
                DoorContactFallback: ReadBool("DoorContactFallback", true));
            return (settings, problems.Count == 0 ? null : string.Join("; ", problems));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Newtonsoft.Json.JsonException or FormatException or InvalidCastException)
        { return (Symbol3DSettings.Disabled, ex.GetType().Name + ": " + ex.Message); }
    }
}
