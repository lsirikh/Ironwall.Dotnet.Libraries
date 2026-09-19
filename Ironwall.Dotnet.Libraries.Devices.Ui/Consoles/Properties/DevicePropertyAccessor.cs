using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Servers;
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Properties;
/****************************************************************************
   Purpose      : DevicePropertySpec 값을 실제 행 뷰모델에서 읽고 쓰는 공용 접근기 (FR-07 · FR-13)
   Created By   : GHLee
   Created On   : 9/19/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// <see cref="DevicePropertySpec"/> 한 줄을 실제 행 뷰모델·모델에 연결하는 다리. 폼은 이 클래스만 통해
/// 읽고 쓴다 — 카테고리별 캐스팅·널 처리를 폼 코드에 흩뿌리지 않기 위해서다.
/// </summary>
/// <remarks>
/// <para><b>왜 리플렉션인가</b> — 7 카테고리 행 뷰모델은 서로 다른 구체 타입이고 공통 인터페이스가 없는
/// 속성이 태반이다(<c>IpAddress</c> 는 제어기·카메라·경광등에만 있다). 매 카테고리마다 분기하는 어댑터를
/// 짜는 대신, 명세가 속성 이름을 들고 있고 접근기가 그 이름으로 찾는다 — 새 속성은 명세 한 줄로 끝난다.
/// <see cref="PropertyInfo"/> 조회는 (타입, 이름) 쌍으로 캐싱해 폼이 매 렌더마다 다시 찾지 않게 한다.</para>
/// <para><b>빈 문자열의 뜻이 타입마다 다르다</b> — nullable 속성(<c>string?</c>·<c>double?</c>)에 빈 칸은
/// "지운다"(null)지만, non-nullable <c>string</c>(예: <c>IpAddress</c>)에 빈 칸을 그대로 흘리면 서버가
/// 422 <c>EMPTY_STRING</c> 으로 되돌린다 — 여기서 미리 막는다. 참조형의 null 허용 여부는 런타임 타입만으로는
/// 구분이 안 돼(둘 다 <see cref="string"/>) <see cref="NullabilityInfoContext"/> 로 선언부 애노테이션을 읽는다.</para>
/// </remarks>
public static class DevicePropertyAccessor
{
    private const string ObjectOnlyMessage = "선택 목록에서 고른 항목으로만 바꿀 수 있습니다";

    #region - Read -
    /// <summary>원시 값(문자열이 아닌 실제 CLR 값)을 읽는다. <see cref="ReadText"/> 가 여기에 얹혀 표시 문자열을 만든다.</summary>
    public static object? Read(object rowViewModel, DevicePropertySpec spec)
    {
        if (rowViewModel == null) throw new ArgumentNullException(nameof(rowViewModel));
        if (spec == null) throw new ArgumentNullException(nameof(spec));

        if (spec.ViewModelPath != null)
        {
            var property = ResolveProperty(rowViewModel.GetType(), spec.ViewModelPath);
            return property?.GetValue(rowViewModel);
        }

        if (spec.AxisReader != null)
        {
            var model = ResolveModel(rowViewModel);
            return model == null ? null : spec.AxisReader(model);
        }

        return null;
    }

    /// <summary>화면 표시용 문자열. bool → "true"/"false", enum → 이름, 참조 객체(제어기·서버) → 표시 이름.</summary>
    public static string ReadText(object rowViewModel, DevicePropertySpec spec)
        => FormatText(Read(rowViewModel, spec));

    private static string FormatText(object? value)
    {
        switch (value)
        {
            case null:
                return string.Empty;
            case bool flag:
                return flag ? "true" : "false";
            case string text:
                return text;
            case IServerModel server:
                return server.Name ?? string.Empty;
            case IBaseDeviceModel device:
                return device.DeviceName ?? string.Empty;
            case IFormattable formattable:
                return formattable.ToString(null, CultureInfo.InvariantCulture);
            default:
                return value.ToString() ?? string.Empty;
        }
    }
    #endregion

    #region - Validate -
    /// <summary>
    /// 서버로 보내기 전 마지막 점검(FR-13). <c>null</c> 이면 통과. 잠긴 칸은 <see cref="DevicePropertySpec.LockReason"/> 을 그대로 돌려준다.
    /// </summary>
    public static string? Validate(DevicePropertySpec spec, string? text, bool isCreating)
    {
        if (spec == null) throw new ArgumentNullException(nameof(spec));

        if (spec.Writable == DevicePropertyWritable.No)
            return spec.LockReason ?? $"{spec.Label} 은(는) 읽기 전용입니다.";

        var isEmpty = string.IsNullOrEmpty(text);
        var trimmed = text?.Trim();
        var isWhitespaceOnly = !isEmpty && string.IsNullOrEmpty(trimmed);

        if (isWhitespaceOnly)
            return $"{spec.Label} 값에는 공백만 넣을 수 없습니다.";

        if (isCreating && spec.IsRequiredOnCreate && isEmpty)
            return $"{spec.Label} 값은 생성 시 필수입니다.";

        if (string.IsNullOrEmpty(trimmed))
            return null;   // 비우기는 TryWrite 단계에서 타입별로 판정한다(nullable=허용, non-nullable string=거부).

        switch (spec.Editor)
        {
            case DevicePropertyEditor.Integer:
                if (!int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
                    return $"{spec.Label} 값은 정수여야 합니다.";
                return ValidateRange(spec, intValue);

            case DevicePropertyEditor.Number:
                if (!double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var numValue))
                    return $"{spec.Label} 값은 숫자여야 합니다.";
                return ValidateRange(spec, numValue);

            case DevicePropertyEditor.Boolean:
                if (!bool.TryParse(trimmed, out _))
                    return $"{spec.Label} 값은 참/거짓이어야 합니다.";
                break;
        }

        if (spec.MaxLength.HasValue && trimmed!.Length > spec.MaxLength.Value)
            return $"{spec.Label} 값은 {spec.MaxLength.Value}자를 넘을 수 없습니다.";

        return null;
    }

    private static string? ValidateRange(DevicePropertySpec spec, double value)
    {
        if (spec.Min.HasValue && value < spec.Min.Value)
            return $"{spec.Label} 값은 {spec.Min.Value.ToString(CultureInfo.InvariantCulture)} 이상이어야 합니다.";
        if (spec.Max.HasValue && value > spec.Max.Value)
            return $"{spec.Label} 값은 {spec.Max.Value.ToString(CultureInfo.InvariantCulture)} 이하여야 합니다.";
        return null;
    }
    #endregion

    #region - Write -
    /// <summary>
    /// 문자열을 속성의 CLR 타입으로 바꿔 대입한다. <see cref="DevicePropertyOptionSource.Controllers"/>·
    /// <see cref="DevicePropertyOptionSource.Servers"/> 는 참조형이라 여기서 거부되고 <see cref="TryWriteObject"/> 를 타야 한다.
    /// </summary>
    public static bool TryWrite(object rowViewModel, DevicePropertySpec spec, string? text, out string? error)
    {
        if (rowViewModel == null) throw new ArgumentNullException(nameof(rowViewModel));
        if (spec == null) throw new ArgumentNullException(nameof(spec));

        error = null;

        if (spec.Writable == DevicePropertyWritable.No)
        {
            error = spec.LockReason ?? $"{spec.Label} 은(는) 읽기 전용입니다.";
            return false;
        }

        if (spec.OptionSource is DevicePropertyOptionSource.Controllers or DevicePropertyOptionSource.Servers)
        {
            error = ObjectOnlyMessage;
            return false;
        }

        if (spec.ViewModelPath == null)
        {
            error = $"{spec.Label} 은(는) 편집 경로가 없습니다.";
            return false;
        }

        var property = ResolveProperty(rowViewModel.GetType(), spec.ViewModelPath);
        if (property == null || !property.CanWrite)
        {
            error = $"{spec.Label} 은(는) 이 장비 종류에서 편집할 수 없습니다.";
            return false;
        }

        var trimmed = text?.Trim();
        var isEmpty = string.IsNullOrEmpty(trimmed);
        var targetType = property.PropertyType;
        var underlying = Nullable.GetUnderlyingType(targetType);
        var effectiveType = underlying ?? targetType;

        try
        {
            object? converted;

            if (isEmpty)
            {
                if (targetType.IsValueType)
                {
                    if (underlying == null)
                    {
                        error = $"{spec.Label} 값이 필요합니다.";
                        return false;
                    }
                    converted = null;
                }
                else if (IsNullableReference(property))
                {
                    converted = null;
                }
                else
                {
                    error = $"{spec.Label} 값은 비울 수 없습니다.";
                    return false;
                }
            }
            else if (effectiveType == typeof(string))
            {
                converted = trimmed;
            }
            else if (effectiveType.IsEnum)
            {
                if (!Enum.TryParse(effectiveType, trimmed, ignoreCase: true, out var enumValue) || !Enum.IsDefined(effectiveType, enumValue!))
                {
                    error = $"{spec.Label} 값 '{trimmed}' 은(는) 알 수 없는 값입니다.";
                    return false;
                }
                converted = enumValue;
            }
            else if (effectiveType == typeof(int))
            {
                if (!int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
                {
                    error = $"{spec.Label} 값은 정수여야 합니다.";
                    return false;
                }
                var rangeError = ValidateRange(spec, intValue);
                if (rangeError != null) { error = rangeError; return false; }
                converted = intValue;
            }
            else if (effectiveType == typeof(double))
            {
                if (!double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var numValue))
                {
                    error = $"{spec.Label} 값은 숫자여야 합니다.";
                    return false;
                }
                var rangeError = ValidateRange(spec, numValue);
                if (rangeError != null) { error = rangeError; return false; }
                converted = numValue;
            }
            else if (effectiveType == typeof(bool))
            {
                if (!bool.TryParse(trimmed, out var boolValue))
                {
                    error = $"{spec.Label} 값은 참/거짓이어야 합니다.";
                    return false;
                }
                converted = boolValue;
            }
            else
            {
                error = $"{spec.Label} 은(는) 이 편집기로 바꿀 수 없습니다.";
                return false;
            }

            property.SetValue(rowViewModel, converted);
            return true;
        }
        catch (Exception ex)
        {
            error = $"{spec.Label} 값을 적용하지 못했습니다: {ex.Message}";
            return false;
        }
    }

    /// <summary>제어기·서버처럼 목록에서 고른 참조 객체를 그대로 대입한다(카탈로그 값 텍스트 변환이 없는 칸).</summary>
    public static bool TryWriteObject(object rowViewModel, DevicePropertySpec spec, object? value, out string? error)
    {
        if (rowViewModel == null) throw new ArgumentNullException(nameof(rowViewModel));
        if (spec == null) throw new ArgumentNullException(nameof(spec));

        error = null;

        if (spec.Writable == DevicePropertyWritable.No)
        {
            error = spec.LockReason ?? $"{spec.Label} 은(는) 읽기 전용입니다.";
            return false;
        }

        if (spec.ViewModelPath == null)
        {
            error = $"{spec.Label} 은(는) 편집 경로가 없습니다.";
            return false;
        }

        var property = ResolveProperty(rowViewModel.GetType(), spec.ViewModelPath);
        if (property == null || !property.CanWrite)
        {
            error = $"{spec.Label} 은(는) 이 장비 종류에서 편집할 수 없습니다.";
            return false;
        }

        if (value != null && !property.PropertyType.IsInstanceOfType(value))
        {
            error = $"{spec.Label} 값의 형식이 올바르지 않습니다.";
            return false;
        }

        try
        {
            property.SetValue(rowViewModel, value);
            return true;
        }
        catch (Exception ex)
        {
            error = $"{spec.Label} 값을 적용하지 못했습니다: {ex.Message}";
            return false;
        }
    }
    #endregion

    #region - Reflection helpers -
    private static readonly ConcurrentDictionary<(Type Type, string Name), PropertyInfo?> PropertyCache = new();
    private static readonly ConcurrentDictionary<PropertyInfo, bool> NullableReferenceCache = new();
    private static readonly NullabilityInfoContext NullabilityContext = new();

    private static PropertyInfo? ResolveProperty(Type type, string name)
        => PropertyCache.GetOrAdd((type, name), key => key.Type.GetProperty(key.Name, BindingFlags.Public | BindingFlags.Instance));

    /// <summary>행 뷰모델의 공개 <c>Model</c> 속성(<c>BaseCustomViewModel&lt;T&gt;</c>)을 통해 도메인 모델을 얻는다.</summary>
    private static IBaseDeviceModel? ResolveModel(object rowViewModel)
    {
        var property = ResolveProperty(rowViewModel.GetType(), "Model");
        return property?.GetValue(rowViewModel) as IBaseDeviceModel;
    }

    /// <summary>참조형 속성이 <c>string?</c> 처럼 null 허용으로 선언됐는가 — 런타임 타입만으로는 알 수 없어 NRT 애노테이션을 읽는다.</summary>
    private static bool IsNullableReference(PropertyInfo property)
        => NullableReferenceCache.GetOrAdd(property, static p =>
        {
            var info = NullabilityContext.Create(p);
            return info.WriteState != NullabilityState.NotNull;
        });
    #endregion
}
