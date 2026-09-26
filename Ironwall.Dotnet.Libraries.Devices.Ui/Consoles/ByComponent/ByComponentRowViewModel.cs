using Ironwall.Dotnet.Libraries.Devices.Api.Models;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using System;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.ByComponent;
/****************************************************************************
   Purpose      : "부품으로 찾기" 결과 행 뷰모델 (FR-17)
   Created By   : GHLee
   Created On   : 9/19/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 부품 건강 값의 표시 분류 — 색은 <c>Console.Pill</c> 점 + 글자 색을 함께 바꾸는 데 쓰이고,
/// 점(형태) 자체는 항상 그려진다(색만으로 뜻을 전하지 않는다 — drag-first-ux.md 의 시각 원칙과 동일 취지).
/// </summary>
public enum EnumComponentHealthKind
{
    /// <summary>건강 값이 없거나 어휘 밖 — <c>UNKNOWN</c> 취급.</summary>
    Unknown = 0,
    Ok,
    Warn,
    Crit,
}

/// <summary>
/// "부품으로 찾기" 탭의 결과 행 — <see cref="ComponentStateRowDto"/> 한 개를 화면에 맞게 편 <b>읽기 전용</b> 사영이다.
/// </summary>
/// <remarks>
/// <para><b>모든 값은 생성 시 1회 확정</b>이다(서버 값이 바뀌면 재조회로 새 인스턴스를 만든다 — 이 행 자체를
/// 갱신하지 않는다). 그래서 <see cref="Caliburn.Micro.PropertyChangedBase"/> 를 상속하지 않는다.</para>
///
/// <para><b>DeviceName · DeviceNumber 를 채우지 못하는 이유</b> — <c>GET /api/devices/by-component</c> 는
/// 헌장 P2-6("참조에는 종류축이 없다")에 따라 이름·번호를 <b>의도적으로 싣지 않는다</b>(정본은 장비 캐시,
/// <see cref="ComponentStateRowDto"/> 의 remarks 참조). 이 탭은 계약상 <c>DeviceProvider</c> 를 주입받지 않는다
/// (통합자가 의존하는 생성자 시그니처가 고정돼 있다) — 그래서 <see cref="DeviceName"/> 은 항상 비워 두고,
/// <see cref="DeviceNumber"/> 는 행이 가진 유일한 정수인 <see cref="ComponentStateRowDto.Id"/> 를 그대로 보여준다.
/// 이것은 서버 <c>number_device</c> 가 <b>아니다</b> — 실제 이름·장비번호가 필요하면 같은 id 로 해당 카테고리
/// 탭에서 찾아야 한다(알려진 한계, 최종 보고에 명시).</para>
///
/// <para><see cref="FaultReason"/> 도 같은 이유로 항상 비어 있다 — <see cref="ComponentStateRowDto"/> 에
/// 고장 사유 필드 자체가 없다(카탈로그의 <c>component_fault</c> 어휘는 있지만 이 행 DTO 는 사유 코드를 싣지 않는다).</para>
/// </remarks>
public sealed class ByComponentRowViewModel
{
    #region - Ctors -
    public ByComponentRowViewModel(ComponentStateRowDto dto, ICatalogService catalog)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));
        if (catalog == null) throw new ArgumentNullException(nameof(catalog));

        RowKey = dto.RowKey;
        DeviceName = string.Empty;                              // 알려진 한계 — remarks 참조
        DeviceNumber = dto.Id;                                   // 최선의 대안(내부 id) — number_device 아님
        CategoryText = ToCategoryText(dto.CategoryDevice);
        ComponentKey = dto.Component;
        ComponentTypeLabel = string.IsNullOrWhiteSpace(dto.ComponentType)
            ? Dash
            : catalog.LabelOf(DeviceSpecCatalogDto.VOCAB_COMPONENT_TYPE, dto.ComponentType);
        StateText = StateLabel(dto.State);
        StateTooltip = UnknownStateTooltip(dto.State);
        var (healthText, healthKind) = ToHealth(dto.Health);
        HealthText = healthText;
        HealthKind = healthKind;
        FaultReason = string.Empty;                              // 알려진 한계 — remarks 참조
        LastChangeText = string.IsNullOrWhiteSpace(dto.ObservedAt) ? NoObservation : dto.ObservedAt!;
    }
    #endregion

    #region - Properties -
    /// <summary>DS-1 대응 복합 키 — 같은 장비의 같은 유형 부품이 둘이어도 행마다 다르다.</summary>
    public string RowKey { get; }

    /// <summary>알려진 한계로 항상 빈 문자열 — remarks 참조.</summary>
    public string DeviceName { get; }

    /// <summary>행의 <see cref="ComponentStateRowDto.Id"/> — 서버 <c>number_device</c> 가 아니다(remarks 참조).</summary>
    public int DeviceNumber { get; }

    /// <summary>장비 카테고리 한글 표시 — 제어기/센서/카메라/스피커/함체/경광등/통문.</summary>
    public string CategoryText { get; }

    /// <summary>부품 <c>key</c> — 장비 형상 선언의 자유 문자열(닫힌 어휘 아님).</summary>
    public string ComponentKey { get; }

    /// <summary>부품 유형의 카탈로그 표시명. 유형이 없으면(옛 데이터) "—".</summary>
    public string ComponentTypeLabel { get; }

    /// <summary>상태 한글 표시. 어휘 밖 값은 "알 수 없음"(원문은 <see cref="StateTooltip"/>), 축 자체가 없으면 "—".</summary>
    public string StateText { get; }

    /// <summary>어휘 밖 상태일 때만 원문을 담은 툴팁 글 — 아는 값이면 <c>null</c>(툴팁을 띄우지 않는다).</summary>
    public string? StateTooltip { get; }

    /// <summary>건강 한글 표시.</summary>
    public string HealthText { get; }

    /// <summary>건강 값의 색·모양 분류(Ok/Warn/Crit/Unknown).</summary>
    public EnumComponentHealthKind HealthKind { get; }

    /// <summary>알려진 한계로 항상 빈 문자열 — remarks 참조.</summary>
    public string FaultReason { get; }

    /// <summary>
    /// "마지막 변화" 열 — <c>observed_at</c> 원문을 <b>그대로</b>(마이크로초까지) 보여준다.
    /// 값이 바뀔 때만 움직이고 워치독이 없어 "마지막 보고"라고 적으면 거짓이다(CW L535-543).
    /// </summary>
    public string LastChangeText { get; }
    #endregion

    #region - Implementations -
    private const string Dash = "—";
    private const string NoObservation = "보고 없음";

    private static readonly IReadOnlyDictionary<string, string> CategoryLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["controller"] = "제어기",
        ["sensor"] = "센서",
        ["camera"] = "카메라",
        ["speaker"] = "스피커",
        ["enclosure"] = "함체",
        ["lamp"] = "경광등",
        ["gate"] = "통문",
    };

    private static readonly IReadOnlyDictionary<string, string> StateLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["OPEN"] = "열림",
        ["CLOSED"] = "닫힘",
        ["RUNNING"] = "구동 중",
        ["ON"] = "켜짐",
        ["OFF"] = "꺼짐",
        ["IDLE"] = "대기",
    };

    private static string ToCategoryText(string? categoryDevice)
    {
        if (string.IsNullOrWhiteSpace(categoryDevice)) return Dash;
        var trimmed = categoryDevice.Trim();
        return CategoryLabels.TryGetValue(trimmed, out var label) ? label : trimmed;
    }

    /// <summary>모르는 상태 값의 화면 글 — 영문 원문을 그대로 보이지 않는다(U-18 D-4 4.1).</summary>
    public const string UnknownState = "알 수 없음";

    /// <summary>
    /// 부품 상태 코드 → 한국어. 결과 열과 상태 필터 칩이 <b>같은 표</b>를 쓴다(칩만 영문이던 결함 — U-18 D-4 4.1).
    /// 비었으면 "—", 어휘 밖이면 "알 수 없음".
    /// </summary>
    public static string StateLabel(string? state)
    {
        if (string.IsNullOrWhiteSpace(state)) return Dash;
        return StateLabels.TryGetValue(state.Trim(), out var label) ? label : UnknownState;
    }

    /// <summary>어휘 밖 상태일 때 원문을 보여 줄 툴팁 글. 아는 값 · 빈 값이면 <c>null</c>.</summary>
    public static string? UnknownStateTooltip(string? state)
        => string.IsNullOrWhiteSpace(state) || StateLabels.ContainsKey(state.Trim())
            ? null
            : $"받은 값: {state.Trim()}";

    // 한글 라벨의 정본은 DeviceEnumDisplay.ComponentHealthKorean 하나뿐이다 — 필터 칩(ByComponentViewModel)도
    // 같은 표를 읽는다. 여기서는 색·점 모양(Kind)만 이 행 전용으로 분류한다(표시 문구가 아니다).
    private static (string Text, EnumComponentHealthKind Kind) ToHealth(string? health)
    {
        var trimmed = health?.Trim().ToUpperInvariant();
        var kind = trimmed switch
        {
            "OK" => EnumComponentHealthKind.Ok,
            "DEGRADED" => EnumComponentHealthKind.Warn,
            "FAULT" => EnumComponentHealthKind.Crit,
            _ => EnumComponentHealthKind.Unknown,
        };
        return (DeviceEnumDisplay.ComponentHealthKorean(trimmed), kind);
    }
    #endregion
}
