using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Map3D;

/****************************************************************************
   Purpose      : 3D 영역마다 3D 로 바뀌는 줌 — GIS 로컬 설정(%LocalAppData%\Ironwall\Gis\map3d-areas.json, {"영역 id": 줌}).
   Note         : 배지 「3D 줌 바꾸기…」에서 map-3d 가 PATCH 를 모를 때(404 · 405)만 여기에 저장하고, 이 값이 서버 값보다 앞선다
                  (Map3DMath.ResolveAreaEnterZoom). 서버가 받아 주면 그 영역의 로컬 값은 지운다.
                  파일이 없거나 깨졌으면 빈 목록. 저장은 임시 파일에 쓴 뒤 바꿔 끼운다(쓰다 끊겨도 옛 파일이 남는다).
                  WPF 무의존 — tests/GMaps.Ui.Tests 가 실코드를 링크해 시험한다.
   Created On   : 2026-10-18 · Sensorway Co., Ltd.
****************************************************************************/
public sealed class Map3DAreaZoomStore
{
    /// <summary>%LocalAppData%\Ironwall\Gis\map3d-areas.json.</summary>
    public static string DefaultPath
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ironwall", "Gis", "map3d-areas.json");

    private readonly string _path;
    private Dictionary<string, double>? _zooms;

    public Map3DAreaZoomStore(string path) => _path = path;

    private Dictionary<string, double> Zooms => _zooms ??= Load(_path);

    /// <summary>파일을 읽는다 — 없거나 읽지 못하면 빈 목록(지도 게이트가 매 프레임 묻는 값이라 예외를 내지 않는다).</summary>
    private static Dictionary<string, double> Load(string path)
    {
        try { return File.Exists(path) ? Parse(File.ReadAllText(path)) : new(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return new(); }
    }

    /// <summary>그 영역의 로컬 줌 — 없으면 null.</summary>
    public double? Get(string id) => Zooms.TryGetValue(id, out var zoom) ? zoom : null;

    /// <summary>그 영역의 줌을 저장한다.</summary>
    public void Set(string id, double zoom)
    {
        Zooms[id] = zoom;
        Save();
    }

    /// <summary>그 영역의 로컬 줌을 지운다 — 있었으면 저장하고 true.</summary>
    public bool Remove(string id)
    {
        if (!Zooms.Remove(id)) return false;
        Save();
        return true;
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        string temp = _path + ".tmp";
        File.WriteAllText(temp, Serialize(Zooms));
        File.Move(temp, _path, overwrite: true);
    }

    /// <summary>{"id": 줌, …} 해석 — 유한한 수가 아닌 항목은 건너뛰고, 형식이 틀리면 빈 목록.</summary>
    internal static Dictionary<string, double> Parse(string json)
    {
        var map = new Dictionary<string, double>(StringComparer.Ordinal);
        try
        {
            if (JToken.Parse(json) is not JObject o) return map;
            foreach (var p in o.Properties())
                if (p.Value.Type is JTokenType.Float or JTokenType.Integer && double.IsFinite(p.Value.Value<double>()))
                    map[p.Name] = p.Value.Value<double>();
        }
        catch (JsonException) { }
        return map;
    }

    internal static string Serialize(IReadOnlyDictionary<string, double> zooms)
        => new JObject(zooms.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => new JProperty(p.Key, p.Value))).ToString(Formatting.Indented);
}
