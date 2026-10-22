using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

/****************************************************************************
   Purpose      : 3D 영역 패키지(.iw3d — 다른 PC 의 GIS 로 옮기는 한 개의 파일, facade-package-contract B) 순수 함수 — WPF · GMap 무의존
                  (헤드리스 시험: tests/GMaps.Ui.Tests/Map3DPackageTests).
                  ① 파일 이름 · 대화상자 거르개 ② 가져오기 전 GIS 쪽 검사(속은 zip, manifest.json 의 format = "ironwall-3d") ③ 가져오기 응답 해석 · 쉬운 말.
   Note         : 확장자가 달라도 속을 열어 manifest 로 판정한다(계약). 해시 · 경로 순회 · 판 검사는 엔진(POST /api/jobs/import)이 한다 —
                  GIS 는 엉뚱한 파일(다른 zip · 그림 등)을 큰 업로드 전에 쉬운 말로 거르는 것까지만.
   Created On   : 2026-10-22 · Sensorway Co., Ltd.
****************************************************************************/

/// <summary>가져오기 전 검사 결과 — Error 가 null 이면 올려도 된다. Name = manifest 의 영역 이름(없으면 null).</summary>
public sealed record Map3DPackageCheck(string? Error, string? Name);

/// <summary>가져오기 응답(POST /api/jobs/import → {id, name, conflict}) — Conflict = 같은 id 가 있어 새 id 로 들였을 때 엔진 알림(없으면 null).</summary>
public sealed record Map3DImportResult(string Id, string? Name, string? Conflict);

public static class Map3DPackage
{
    public const string Extension = ".iw3d", Format = "ironwall-3d", ManifestName = "manifest.json";
    /// <summary>저장 · 열기 대화상자 거르개.</summary>
    public const string FileFilter = "Ironwall 3D 영역 (*.iw3d)|*.iw3d|모든 파일 (*.*)|*.*";
    /// <summary>엔진 크기 상한(계약 예: 2 GB) — 넘으면 올리기 전에 거른다.</summary>
    public const long MaxBytes = 2L * 1024 * 1024 * 1024;

    public const string OldEngineText = "엔진이 옛 판이라 3D 영역 내보내기 · 가져오기를 쓸 수 없습니다";
    public const string NotPackageText = "Ironwall 3D 영역 파일(.iw3d)이 아닙니다 — 다른 GIS 에서 「이 영역 내보내기」로 만든 파일을 고르세요";
    public const string TooBigText = "파일이 너무 큽니다(2 GB 까지)";

    /// <summary>내보낼 파일 이름 — 영역 이름(없으면 id)에서 파일 이름에 못 쓰는 글자를 '_' 로, 끝에 .iw3d.</summary>
    public static string ExportFileName(string title)
    {
        var bad = Path.GetInvalidFileNameChars();
        var name = new string((title ?? "").Trim().Select(c => bad.Contains(c) ? '_' : c).ToArray()).Trim('.', ' ');
        if (name.Length > 80) name = name[..80];
        return (name.Length == 0 ? "3d-area" : name) + Extension;
    }

    /// <summary>
    /// 가져오기 전 검사 — 크기 · zip 인지 · manifest.json 의 format. 스트림은 읽기만 하고 닫지 않는다(호출자가 처음부터 다시 올린다).
    /// </summary>
    public static Map3DPackageCheck Inspect(Stream stream)
    {
        if (stream.CanSeek && stream.Length > MaxBytes) return new(TooBigText, null);
        try
        {
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            var entry = zip.GetEntry(ManifestName);
            if (entry == null || entry.Length > 4 * 1024 * 1024) return new(NotPackageText, null);
            using var reader = new StreamReader(entry.Open());
            if (JToken.Parse(reader.ReadToEnd()) is not JObject m || Str(m["format"]) != Format) return new(NotPackageText, null);
            return new(null, Str(m["name"]));
        }
        catch (Exception ex) when (ex is InvalidDataException or JsonException or IOException) { return new(NotPackageText, null); }
    }

    /// <summary>POST /api/jobs/import 응답 — id 가 없으면 null. conflict 는 글이면 그대로, true · 객체면 기본 안내, false · null 이면 없음.</summary>
    public static Map3DImportResult? ParseImport(string json)
    {
        JObject o;
        try { o = JObject.Parse(json); }
        catch (JsonException) { return null; }
        if (Str(o["id"]) is not { } id) return null;
        var c = o["conflict"];
        string? conflict = c?.Type switch
        {
            JTokenType.String => Str(c),
            JTokenType.Boolean => c.Value<bool>() ? "같은 영역이 이미 있어 새 이름으로 들였습니다" : null,
            JTokenType.Object => Str(c["message"]) ?? Str(c["detail"]) ?? "같은 영역이 이미 있어 새 이름으로 들였습니다",
            _ => null,
        };
        return new Map3DImportResult(id, Str(o["name"]), conflict);
    }

    /// <summary>가져오기 끝 한 줄 — "3D 영역 '이름' 을(를) 가져왔습니다"(충돌이면 " — 같은 영역이 이미 있어 …").</summary>
    public static string ImportedText(Map3DImportResult r)
        => $"3D 영역 '{Map3DGuide.AreaTitle(r.Id, r.Name)}' 을(를) 가져왔습니다" + (r.Conflict is { } c ? $" — {c}" : "");

    /// <summary>진행 한 줄 — "3D 영역 내보내는 중… 42%"(전체 크기를 모르면 받은 MB).</summary>
    public static string ProgressText(string verb, long done, long? total)
        => total is > 0 ? $"3D 영역 {verb} 중… {Math.Clamp(done * 100 / total.Value, 0, 100)}%" : $"3D 영역 {verb} 중… {done / (1024d * 1024):0.0} MB";

    private static string? Str(JToken? t) => t?.Type == JTokenType.String && t.Value<string>() is { Length: > 0 } s ? s : null;
}
