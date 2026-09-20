using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers;
/****************************************************************************
   Purpose      : 서버 쓰기 본문 조립 — 관측 필드 금지 · null 로 지우지 않기 (N-12)
   Created By   : GHLee
   Created On   : 9/20/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 서버 수정 본문 전용 DTO — <b>관측 필드와 빈 값이 나가지 못한다</b>.
/// </summary>
/// <remarks>
/// <para><c>PATCH</c> 는 RFC 7396 이다: <b>키가 <c>null</c> 이면 그 값이 지워진다.</b>
/// <see cref="ServerDto"/> 를 그대로 보내면 받지 못한 <c>hostname</c>·<c>user_name</c>·<c>user_password</c>·
/// <c>threshold_config</c> 가 <c>null</c> 로 실려 <b>서버에 있던 값이 사라진다</b>(목록 기본 프로필이 <c>basic</c> 이라
/// 계정과 임계는 애초에 오지 않는다 — A-devices S-05). 그래서 <b>값이 없으면 키째 뺀다</b>.</para>
/// <para><c>status</c> 는 <b>관측 필드</b>다. <c>PATCH /api/servers/{id}</c> 본문에 실으면 7.0 이 422
/// (<c>OBSERVED_FIELD</c>)로 거부한다. <see cref="ServerDto.Status"/> 는 기본값이 <c>"NORMAL"</c> 이라
/// 가만히 두면 <b>반드시 실린다</b> — 그래서 이 타입은 본문을 <see cref="ServerPatchDtoConverter"/> 가 직접 적는다. 상태를 바꾸는 입구는
/// <c>PATCH /api/servers/{id}/status</c> 뿐이고 그것은 서버 매니저의 몫이다(이 콘솔은 쓰지 않는다).</para>
/// <para><c>ShouldSerialize*</c> 는 <b>그 타입의 모든 직렬화</b>에 걸린다 — 그래서 읽기용
/// <see cref="ServerDto"/> 를 건드리지 않고 <b>쓰기 전용 파생</b>을 따로 둔다(레포 교훈:
/// <c>SuppressDeviceOnRestWrite</c> 가 NATS 본문을 깼던 일).</para>
/// </remarks>
[JsonConverter(typeof(ServerPatchDtoConverter))]
public sealed class ServerPatchDto : ServerDto
{
}

/// <summary>
/// <see cref="ServerPatchDto"/> 의 본문을 <b>열거해서</b> 쓴다 — 무엇이 나가는지 한 곳에서 다 보인다.
/// </summary>
/// <remarks>
/// <para><c>ShouldSerializeXxx()</c> 를 파생 클래스에 두는 방법은 <b>통하지 않는다</b>(실측):
/// Newtonsoft 는 그 메서드를 <b>속성을 선언한 타입</b>에서 찾는데, <c>status</c>·<c>id</c>·<c>created_at</c> 은
/// <see cref="ServerDto"/>·<c>BaseDto</c> 가 선언했다. 그 타입을 고치면 읽기 경로와 다른 창까지 함께 바뀐다
/// (레포 교훈: <c>ShouldSerialize</c> 는 그 DTO 의 <b>모든</b> 직렬화에 걸린다). 그래서 쓰기 전용 타입에
/// <b>타입 단위 변환기</b>를 달아 본문을 직접 적는다.</para>
/// <para>키가 <c>null</c> 이면 RFC 7396 에서 <b>서버가 그 값을 지운다</b> — 값이 없으면 키째 뺀다.</para>
/// </remarks>
public sealed class ServerPatchDtoConverter : JsonConverter
{
    /// <summary>이 본문에 <b>일부러 싣지 않는</b> 것 — 관측 필드와 서버가 정하는 값.</summary>
    public static readonly IReadOnlyList<string> IntentionallyDropped = new[]
    {
        nameof(ServerDto.Id),            // 경로에 이미 있다
        nameof(ServerDto.Status),        // 관측 필드 — 실으면 422(OBSERVED_FIELD)
        "CreatedAt",                     // 서버가 정한다(우리 기본값은 "지금" 이라 더 위험하다)
        "UpdatedAt",
    };

    public override bool CanConvert(Type objectType) => objectType == typeof(ServerPatchDto);

    public override bool CanRead => false;

    public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        => throw new NotSupportedException("ServerPatchDto 는 쓰기 전용입니다 — 읽기는 ServerDto 를 씁니다.");

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        if (value is not ServerPatchDto dto) { writer.WriteNull(); return; }

        writer.WriteStartObject();
        Write(writer, "category_id", dto.CategoryId);
        Write(writer, "name", dto.Name ?? string.Empty);
        Write(writer, "ip_address", dto.IpAddress ?? string.Empty);
        Write(writer, "port", dto.Port);

        if (dto.Hostname != null) Write(writer, "hostname", dto.Hostname);
        if (dto.UserName != null) Write(writer, "user_name", dto.UserName);
        if (dto.UserPassword != null) Write(writer, "user_password", dto.UserPassword);
        if (dto.ThresholdConfig != null) { writer.WritePropertyName("threshold_config"); dto.ThresholdConfig.WriteTo(writer); }
        if (dto.UnitId != null) Write(writer, "unit_id", dto.UnitId.Value);
        writer.WriteEndObject();
    }

    private static void Write(JsonWriter writer, string name, object value)
    {
        writer.WritePropertyName(name);
        writer.WriteValue(value);
    }
}

/// <summary>상세 "접속 · 설정" 에서 사람이 고친 것. <c>null</c> 은 <b>손대지 않았다</b>는 뜻이다.</summary>
public sealed class ServerEditDraft
{
    public string? Name { get; set; }
    public string? IpAddress { get; set; }
    public int? Port { get; set; }
    public string? Hostname { get; set; }
    public string? UserName { get; set; }

    /// <summary>새로 입력한 비밀번호. <c>null</c> 이면 손대지 않은 것이다 — 화면의 마스킹 값을 되돌려 보내지 않는다.</summary>
    public string? NewPassword { get; set; }

    public double? CpuWarning { get; set; }
    public double? CpuCritical { get; set; }
    public double? RamWarning { get; set; }
    public double? RamCritical { get; set; }
    public double? DiskWarning { get; set; }
    public double? DiskCritical { get; set; }
    public double? NetworkWarningMbps { get; set; }
    public double? NetworkCriticalMbps { get; set; }

    public bool HasThresholdEdit =>
        CpuWarning is not null || CpuCritical is not null || RamWarning is not null || RamCritical is not null
        || DiskWarning is not null || DiskCritical is not null || NetworkWarningMbps is not null || NetworkCriticalMbps is not null;
}

/// <summary>한 칸의 잘못 — 칸 키와 사람이 읽는 까닭.</summary>
public sealed record ServerFieldError(string Key, string Message);

/// <summary>
/// 서버 쓰기 본문 조립 — <b>순수 함수</b>. 네트워크를 모른다.
/// </summary>
public static class ServerRequestBuilder
{
    public const string NameKey = "name";
    public const string IpKey = "ip_address";
    public const string PortKey = "port";

    /// <summary>
    /// 수정 본문. <b>받아 온 것으로 채우고</b> 고친 칸만 덮어쓴다 — 빈 DTO 에서 만들지 않는다.
    /// </summary>
    /// <param name="fetched">보내기 <b>직전</b>에 다시 받은 단건(<c>view=full</c>). 이것이 기준선이다.</param>
    /// <param name="draft">사람이 고친 칸.</param>
    /// <param name="contract">서버 계약 세대 — <c>unit_id</c> 는 8.0 이상에서만 실린다.</param>
    public static ServerPatchDto BuildPatch(ServerDto fetched, ServerEditDraft draft, EnumServerContract contract)
    {
        if (fetched is null) throw new ArgumentNullException(nameof(fetched));
        if (draft is null) throw new ArgumentNullException(nameof(draft));

        var body = new ServerPatchDto();
        CopyProperties(fetched, body);

        if (draft.Name is not null) body.Name = draft.Name.Trim();
        if (draft.IpAddress is not null) body.IpAddress = draft.IpAddress.Trim();
        if (draft.Port is not null) body.Port = draft.Port.Value;
        if (draft.Hostname is not null) body.Hostname = draft.Hostname.Trim();
        if (draft.UserName is not null) body.UserName = draft.UserName.Trim();
        if (draft.NewPassword is not null) body.UserPassword = draft.NewPassword;

        body.ThresholdConfig = BuildThresholds(fetched.ThresholdConfig, draft);

        // 8.0 미만에는 unit_id 자체가 없다 — 7.0 이후 쓰기 스키마는 additionalProperties:false 라 실리면 422 다.
        if (contract < EnumServerContract.V8_0) body.UnitId = null;

        return body;
    }

    /// <summary>
    /// 임계 본문. 손댄 값이 없고 받아 온 값도 없으면 <c>null</c> — 그러면 키째 빠진다(지우지 않는다).
    /// </summary>
    public static JObject? BuildThresholds(JObject? fetched, ServerEditDraft draft)
    {
        if (draft is null) throw new ArgumentNullException(nameof(draft));
        if (!draft.HasThresholdEdit) return fetched;

        var root = fetched is null ? new JObject() : (JObject)fetched.DeepClone();
        Set(root, "cpu", "warning", draft.CpuWarning);
        Set(root, "cpu", "critical", draft.CpuCritical);
        Set(root, "ram", "warning", draft.RamWarning);
        Set(root, "ram", "critical", draft.RamCritical);
        Set(root, "disk", "warning", draft.DiskWarning);
        Set(root, "disk", "critical", draft.DiskCritical);
        Set(root, "network", "warning_mbps", draft.NetworkWarningMbps);
        Set(root, "network", "critical_mbps", draft.NetworkCriticalMbps);
        return root;
    }

    /// <summary>받아 온 임계에서 한 값을 읽는다. 없으면 <c>null</c>.</summary>
    public static double? ReadThreshold(JObject? thresholds, string group, string key)
        => thresholds?[group] is JObject section && section[key] is JValue value && value.Type is JTokenType.Float or JTokenType.Integer
            ? value.Value<double>()
            : null;

    /// <summary>보내기 전 지역 검사. 빈 목록이면 보내도 된다.</summary>
    public static IReadOnlyList<ServerFieldError> Validate(ServerEditDraft draft, ServerDto fetched)
    {
        if (draft is null) throw new ArgumentNullException(nameof(draft));
        if (fetched is null) throw new ArgumentNullException(nameof(fetched));

        var errors = new List<ServerFieldError>();
        var name = (draft.Name ?? fetched.Name ?? string.Empty).Trim();
        var ip = (draft.IpAddress ?? fetched.IpAddress ?? string.Empty).Trim();
        var port = draft.Port ?? fetched.Port;

        if (name.Length == 0) errors.Add(new ServerFieldError(NameKey, "이름은 비울 수 없습니다"));
        if (ip.Length == 0) errors.Add(new ServerFieldError(IpKey, "주소는 비울 수 없습니다"));
        if (port is < 1 or > 65535) errors.Add(new ServerFieldError(PortKey, "포트는 1~65535 입니다"));
        return errors;
    }

    /// <summary>
    /// 스피커 → 서버 배정 본문. <b>방금 받아 온 것을 그대로 복사</b>하고 <c>server_id</c> 하나만 바꾼다.
    /// </summary>
    /// <remarks>
    /// JSON 재직렬화로 복제하지 않는다 — <c>ShouldSerializeServer()</c> 같은 조건 직렬화가 걸린 값이
    /// 복제 도중 <b>조용히 사라진다</b>. 속성 복사는 그 함정을 비켜 간다.
    /// </remarks>
    public static SpeakerDeviceDto BuildSpeakerAssign(SpeakerDeviceDto fetched, int serverId)
    {
        if (fetched is null) throw new ArgumentNullException(nameof(fetched));
        if (serverId <= 0) throw new ArgumentOutOfRangeException(nameof(serverId));

        var body = new SpeakerDeviceDto();
        CopyProperties(fetched, body);
        body.ServerId = serverId;
        return body;
    }

    /// <summary>
    /// 같은 이름의 공개 읽기/쓰기 속성을 전부 옮긴다(상속 포함). 감사 테스트가 이 목록을 그대로 훑는다.
    /// </summary>
    public static IReadOnlyList<PropertyInfo> WritableProperties(Type type)
        => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
               .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0)
               .ToList();

    private static void CopyProperties(object source, object target)
    {
        foreach (var property in WritableProperties(source.GetType()))
        {
            var mirror = target.GetType().GetProperty(property.Name, BindingFlags.Public | BindingFlags.Instance);
            if (mirror is null || !mirror.CanWrite || !mirror.PropertyType.IsAssignableFrom(property.PropertyType)) continue;
            mirror.SetValue(target, property.GetValue(source));
        }
    }

    private static void Set(JObject root, string group, string key, double? value)
    {
        if (value is null) return;
        if (root[group] is not JObject section) root[group] = section = new JObject();
        section[key] = value.Value;
    }
}
