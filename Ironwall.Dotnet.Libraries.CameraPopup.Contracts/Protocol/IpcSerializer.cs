using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

/// <summary>디코드 결과.</summary>
public enum DecodeStatus
{
    Ok = 0,
    /// <summary>봉투는 멀쩡하나 모르는 종류 — 버리고 계속.</summary>
    UnknownType = 1,
    /// <summary>JSON · 봉투 파손.</summary>
    Malformed = 2,
    /// <summary>판 불일치.</summary>
    IncompatibleVersion = 3,
}

/// <summary>
/// 봉투 <c>{"v":1,"type":"Hello","id":7,"body":{…}}</c> 직렬화. camelCase · 열거형은 문자열 · null 생략.
/// 모든 함수는 순수하며 예외를 밖으로 던지지 않는다(디코드는 상태 코드로 알린다).
/// </summary>
public static class IpcSerializer
{
    public static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    /// <summary>메시지를 봉투 UTF-8 바이트로.</summary>
    public static byte[] Serialize(IIpcMessage message, long id)
    {
        ArgumentNullException.ThrowIfNull(message);
        var name = MessageRegistry.NameOf(message.GetType());
        using var buffer = new MemoryStream(256);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("v", ProtocolVersion.Current);
            writer.WriteString("type", name);
            writer.WriteNumber("id", id);
            writer.WritePropertyName("body");
            JsonSerializer.Serialize(writer, message, message.GetType(), Options);
            writer.WriteEndObject();
        }
        return buffer.ToArray();
    }

    /// <summary>봉투 바이트를 메시지로. 실패해도 던지지 않는다.</summary>
    public static DecodeStatus TryDeserialize(ReadOnlySpan<byte> utf8, out IIpcMessage? message, out long id, out string? typeName)
    {
        message = null;
        id = 0;
        typeName = null;
        try
        {
            var reader = new Utf8JsonReader(utf8);
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return DecodeStatus.Malformed;
            if (!root.TryGetProperty("v", out var v) || !v.TryGetInt32(out var version)) return DecodeStatus.Malformed;
            if (!root.TryGetProperty("type", out var t) || t.ValueKind != JsonValueKind.String) return DecodeStatus.Malformed;
            typeName = t.GetString();
            if (root.TryGetProperty("id", out var idEl) && idEl.TryGetInt64(out var parsedId)) id = parsedId;
            if (!ProtocolVersion.IsCompatible(version)) return DecodeStatus.IncompatibleVersion;
            if (typeName is null || !MessageRegistry.TryGetType(typeName, out var clrType)) return DecodeStatus.UnknownType;
            if (!root.TryGetProperty("body", out var body) || body.ValueKind != JsonValueKind.Object) return DecodeStatus.Malformed;
            message = body.Deserialize(clrType, Options) as IIpcMessage;
            return message is null ? DecodeStatus.Malformed : DecodeStatus.Ok;
        }
        catch (JsonException)
        {
            return DecodeStatus.Malformed;
        }
        catch (NotSupportedException)
        {
            return DecodeStatus.Malformed;
        }
        catch (InvalidOperationException)
        {
            return DecodeStatus.Malformed;
        }
    }
}
