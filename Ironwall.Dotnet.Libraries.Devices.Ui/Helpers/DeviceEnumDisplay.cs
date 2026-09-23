using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Utils.Converters;
using System;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
/****************************************************************************
   Purpose      : 장비 콘솔 전역의 enum·코드값 → 한글 표시 라벨 단일 정본(device-console enum-korean-consistency).
                  그리드 · 상세 폼 콤보 · 부품 필터 칩 · 셋업/결선맵이 전부 이 표 하나를 함께 읽는다 —
                  같은 개념(운영 상태 · 장비 종류 · 부품 건강)이 화면마다 다른 말이나 raw 영문 이름으로
                  갈라지지 않게 한다.
   ⚠ 표시 계층 전용   : enum 정의/DTO/NATS/서버 전송 값은 절대 바꾸지 않는다. 매핑에 없는 값은
                  원문(코드 · ToString())을 그대로 보존한다 — 모르는 값을 기본값으로 밀어 넣으면
                  억제 범위가 조용히 넓어지는 것과 같은 종류의 사고다(가공하지 말고 드러낸다).
   Created By   : GHLee
   Created On   : 9/23/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 장비 콘솔(Devices.Ui) 전역에서 쓰는 enum·코드값 한글 표시 헬퍼.
/// </summary>
/// <remarks>
/// <para><b>왜 <see cref="UiKoreanMap"/>(Utils) 를 그대로 재사용하지 않는 값이 있는가</b> —
/// <see cref="EnumDeviceStatus"/> 는 GMaps 심볼 팝업(<c>UiKoreanMap</c>)과 장비 콘솔 그리드가
/// 이미 서로 다른 말을 쓰고 있었다(정상/장애/비활성 vs 운영/오류/중지 — 목업
/// <c>docs/design/window-layout-system-storyboard.html</c> <c>DEV_ST</c> 표가 정본).
/// 콘솔 화면끼리(그리드 · 상세 폼 · 카테고리)는 하나로 모으되, 맵 팝업의 기존 말은 이 정리 범위 밖이라
/// 건드리지 않는다 — 대신 콘솔 쪽 정본은 여기 하나로만 둔다(<see cref="StatusKorean"/>).</para>
/// <para><see cref="EnumDeviceType"/>·<see cref="EnumCameraType"/>·<see cref="EnumCameraMode"/> 는
/// 다른 화면(맵 팝업)과 갈릴 이유가 없는 값이라 <see cref="UiKoreanMap"/> 을 그대로 부른다 —
/// 세 번째 사본을 만들지 않는다.</para>
/// </remarks>
public static class DeviceEnumDisplay
{
    // ── 장비 운영 상태 — 콘솔 그리드 "상태" 컬럼(구 BaseDeviceViewModel.StatusDisplay)의 정본 ──
    private static readonly IReadOnlyDictionary<EnumDeviceStatus, string> _status = new Dictionary<EnumDeviceStatus, string>
    {
        [EnumDeviceStatus.ACTIVATED]   = "운영",
        [EnumDeviceStatus.ERROR]       = "오류",
        [EnumDeviceStatus.DEACTIVATED] = "중지",
    };

    /// <summary>못 알아보는 값은 원문 이름을 그대로 보인다(하네스가 조용히 빈칸을 내지 않게).</summary>
    public static string StatusKorean(EnumDeviceStatus status)
        => _status.TryGetValue(status, out var s) ? s : status.ToString();

    // ── 장비 카테고리 — 상세 폼 읽기 전용 "카테고리" 칸의 정본(ByComponentRowViewModel.CategoryLabels 와 같은 말) ──
    private static readonly IReadOnlyDictionary<EnumDeviceCategory, string> _category = new Dictionary<EnumDeviceCategory, string>
    {
        [EnumDeviceCategory.None]       = "없음",
        [EnumDeviceCategory.Controller] = "제어기",
        [EnumDeviceCategory.Sensor]     = "센서",
        [EnumDeviceCategory.Camera]     = "카메라",
        [EnumDeviceCategory.Speaker]    = "스피커",
        [EnumDeviceCategory.Enclosure]  = "함체",
        [EnumDeviceCategory.Lamp]       = "경광등",
        [EnumDeviceCategory.Etc]        = "기타",
        [EnumDeviceCategory.Gate]       = "통문",
    };

    public static string CategoryKorean(EnumDeviceCategory category)
        => _category.TryGetValue(category, out var s) ? s : category.ToString();

    // ── 부품 건강 — "부품으로 찾기" 결과 행 배지(구 ByComponentRowViewModel.ToHealth)의 정본 ──
    private static readonly IReadOnlyDictionary<string, string> _componentHealth = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["OK"]       = "정상",
        ["DEGRADED"] = "주의",
        ["FAULT"]    = "고장",
        ["UNKNOWN"]  = "미확인",
    };

    /// <summary>서버 건강 코드(대소문자 무관) → 한글. 빈 값·어휘 밖 값은 "미확인"(서버 강한 4값 어휘 중 하나로 귀결).</summary>
    public static string ComponentHealthKorean(string? health)
    {
        var trimmed = health?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return "미확인";
        return _componentHealth.TryGetValue(trimmed, out var s) ? s : trimmed;
    }

    /// <summary>enum 값 → 한글. 타입별 정본 표로 나눠 읽는다(이 파일 remarks 참조).</summary>
    public static string KoreanOf(Enum value) => value switch
    {
        EnumDeviceStatus v   => StatusKorean(v),
        EnumDeviceCategory v => CategoryKorean(v),
        EnumDeviceType v     => UiKoreanMap.To(v),
        EnumCameraType v     => UiKoreanMap.To(v),
        EnumCameraMode v     => UiKoreanMap.To(v),
        _                    => value.ToString(),
    };

    /// <summary>
    /// "한국어 (코드)" 병기 — <c>CatalogOption.Display</c>(device-console-v8 목업 규칙: 라벨+코드 병기,
    /// 같으면 코드 숨김)와 정확히 같은 규칙. 매핑을 못 찾아 한글=코드가 같아지면 코드를 또 보이지 않는다.
    /// </summary>
    public static string Bilingual(string korean, string code)
        => string.IsNullOrEmpty(code) || string.Equals(korean, code, StringComparison.Ordinal)
            ? korean
            : $"{korean} ({code})";

    /// <summary>CLR enum 콤보(<c>DevicePropertyFormViewModel.ResolveOptions</c> 의 <c>ClrEnum</c> 선택지)의 "한국어 (코드)" 표시.</summary>
    public static string EnumBilingual(Enum value) => Bilingual(KoreanOf(value), value.ToString());

    /// <summary>
    /// 셋업·결선맵 "종류" 열·콤보의 센서 종류 코드 → "한국어 (코드)". 레거시(6.3) 코드는
    /// <see cref="EnumDeviceType"/> 이름과 같아 그 표를 빌리고, v7.0+ 카탈로그 코드처럼 enum 이 모르는
    /// 값은 원문 그대로 보인다(지어내지 않는다 — 서버가 모르는 종류를 보내면 422 이고, 그 422 가 진단 정보다).
    /// </summary>
    public static string SensorTypeBilingual(string? code)
    {
        var trimmed = code?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return code ?? string.Empty;

        return Enum.TryParse<EnumDeviceType>(trimmed, ignoreCase: true, out var type) && Enum.IsDefined(type)
            ? Bilingual(UiKoreanMap.To(type), trimmed)
            : trimmed;
    }

    /// <summary>
    /// <see cref="SensorTypeBilingual"/> 의 역방향 — "한국어 (코드)" 에서 코드만 되돌린다. 편집형
    /// 콤보(<c>WiringView.xaml</c> "종류" 열·콤보)는 사람이 드롭다운에서 병기 표시를 고르든, 칸에 코드를
    /// 직접 새로 타이핑하든 <b>둘 다</b> 같은 칸에 떨어진다 — 병기 형식이 아니면(직접 타이핑) 입력을
    /// 그대로 코드로 받아들인다(지어내지 않는다).
    /// </summary>
    public static string ExtractSensorTypeCode(string? display)
    {
        var trimmed = display?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return trimmed ?? string.Empty;

        var openIndex = trimmed.LastIndexOf(" (", StringComparison.Ordinal);
        if (openIndex > 0 && trimmed.EndsWith(")", StringComparison.Ordinal))
        {
            var code = trimmed.Substring(openIndex + 2, trimmed.Length - openIndex - 3);
            if (!string.IsNullOrWhiteSpace(code)) return code;
        }

        return trimmed;
    }
}
