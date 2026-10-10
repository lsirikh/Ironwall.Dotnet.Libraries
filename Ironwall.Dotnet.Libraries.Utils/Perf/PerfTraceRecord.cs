using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Ironwall.Dotnet.Libraries.Utils.Perf;

/// <summary>
/// 계측 한 줄. jsonl 모양:
/// <c>{"t":"…Z","wall":"…Z","mono":12.345,"pid":1,"tid":5,"stage":"enqueued","kind":"detection","id":"9001","extra":{…}}</c>.
/// <c>t</c> = 프로세스 시작 때 잡은 UTC + Stopwatch 경과(고해상도 · 단조), <c>wall</c> = 그 순간의 <see cref="DateTime.UtcNow"/>.
/// </summary>
public sealed class PerfTraceRecord
{
    public DateTime T { get; init; }
    public DateTime Wall { get; init; }
    public double MonoMs { get; init; }
    public int Pid { get; init; }
    public int Tid { get; init; }
    public string Stage { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public string? Id { get; init; }
    public IReadOnlyDictionary<string, object?>? Extra { get; init; }

    /// <summary>extra 값을 문자열로(없으면 null).</summary>
    public string? ExtraString(string key)
    {
        if (Extra is null || !Extra.TryGetValue(key, out var value) || value is null) return null;
        return value switch
        {
            string s => s,
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString(),
        };
    }

    /// <summary>extra 값을 수로(없거나 수가 아니면 null).</summary>
    public double? ExtraNumber(string key)
    {
        if (Extra is null || !Extra.TryGetValue(key, out var value) || value is null) return null;
        return value switch
        {
            double d => d,
            float f => f,
            int i => i,
            long l => l,
            short s => s,
            byte b => b,
            uint ui => ui,
            ulong ul => ul,
            decimal m => (double)m,
            string str when double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null,
        };
    }

    // ───────────────────────── 직렬화 ─────────────────────────

    /// <summary>jsonl 한 줄(개행 없음).</summary>
    public string ToJsonLine()
    {
        using var buffer = new MemoryStream(256);
        WriteTo(buffer);
        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    /// <summary>스트림에 한 줄(개행 없음)을 UTF-8 로 쓴다.</summary>
    public void WriteTo(Stream stream)
    {
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartObject();
        writer.WriteString("t", T.ToString("O", CultureInfo.InvariantCulture));
        writer.WriteString("wall", Wall.ToString("O", CultureInfo.InvariantCulture));
        writer.WriteNumber("mono", Math.Round(MonoMs, 3));
        writer.WriteNumber("pid", Pid);
        writer.WriteNumber("tid", Tid);
        writer.WriteString("stage", Stage);
        writer.WriteString("kind", Kind);
        if (Id is null) writer.WriteNull("id");
        else writer.WriteString("id", Id);
        if (Extra is { Count: > 0 })
        {
            writer.WritePropertyName("extra");
            writer.WriteStartObject();
            foreach (var (key, value) in Extra) WriteValue(writer, key, value);
            writer.WriteEndObject();
        }
        writer.WriteEndObject();
    }

    private static void WriteValue(Utf8JsonWriter writer, string key, object? value)
    {
        switch (value)
        {
            case null: writer.WriteNull(key); break;
            case string s: writer.WriteString(key, s); break;
            case bool b: writer.WriteBoolean(key, b); break;
            case int i: writer.WriteNumber(key, i); break;
            case long l: writer.WriteNumber(key, l); break;
            case double d when double.IsFinite(d): writer.WriteNumber(key, Math.Round(d, 3)); break;
            case float f when float.IsFinite(f): writer.WriteNumber(key, Math.Round(f, 3)); break;
            case decimal m: writer.WriteNumber(key, m); break;
            case short sh: writer.WriteNumber(key, sh); break;
            case byte by: writer.WriteNumber(key, by); break;
            case uint ui: writer.WriteNumber(key, ui); break;
            case ulong ul: writer.WriteNumber(key, ul); break;
            case DateTime dt: writer.WriteString(key, dt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)); break;
            case Enum e: writer.WriteString(key, e.ToString()); break;
            case IFormattable fm: writer.WriteString(key, fm.ToString(null, CultureInfo.InvariantCulture)); break;
            default: writer.WriteString(key, value.ToString()); break;
        }
    }

    /// <summary>jsonl 한 줄을 읽는다. 빈 줄 · 깨진 줄은 false(던지지 않는다).</summary>
    public static bool TryParse(string? line, out PerfTraceRecord? record)
    {
        record = null;
        if (string.IsNullOrWhiteSpace(line)) return false;
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;
            if (!TryGetTime(root, "t", out var t)) return false;
            TryGetTime(root, "wall", out var wall);
            Dictionary<string, object?>? extra = null;
            if (root.TryGetProperty("extra", out var ex) && ex.ValueKind == JsonValueKind.Object)
            {
                extra = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var p in ex.EnumerateObject()) extra[p.Name] = ReadValue(p.Value);
            }
            record = new PerfTraceRecord
            {
                T = t,
                Wall = wall == default ? t : wall,
                MonoMs = root.TryGetProperty("mono", out var mono) && mono.TryGetDouble(out var m) ? m : 0,
                Pid = root.TryGetProperty("pid", out var pid) && pid.TryGetInt32(out var pi) ? pi : 0,
                Tid = root.TryGetProperty("tid", out var tid) && tid.TryGetInt32(out var ti) ? ti : 0,
                Stage = root.TryGetProperty("stage", out var st) && st.ValueKind == JsonValueKind.String ? st.GetString()! : string.Empty,
                Kind = root.TryGetProperty("kind", out var kd) && kd.ValueKind == JsonValueKind.String ? kd.GetString()! : string.Empty,
                Id = root.TryGetProperty("id", out var id) ? id.ValueKind switch
                {
                    JsonValueKind.String => id.GetString(),
                    JsonValueKind.Number => id.GetRawText(),
                    _ => null,
                } : null,
                Extra = extra,
            };
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    internal static bool TryGetTime(JsonElement root, string name, out DateTime value)
    {
        value = default;
        if (!root.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.String) return false;
        return TryParseTime(el.GetString(), out value);
    }

    /// <summary>ISO-8601 시각을 UTC 로(시간대 없으면 UTC 로 본다).</summary>
    internal static bool TryParseTime(string? text, out DateTime value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (!DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto))
            return false;
        value = dto.UtcDateTime;
        return true;
    }

    private static object? ReadValue(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Number => el.TryGetDouble(out var d) ? d : null,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => el.GetRawText(),
    };
}
