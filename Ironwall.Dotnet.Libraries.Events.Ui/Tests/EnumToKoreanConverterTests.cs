using System;
using System.Globalization;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.Converters;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/// <summary>
/// 이벤트 UI 표시 전용 enum→한글 변환(EnumKoreanMap / EnumToKoreanConverter) 회귀 테스트.
/// 표시 계층 전용 — 원본 enum 값/데이터는 변경하지 않으며, 매핑에 없으면 ToString() 폴백.
/// </summary>
public class EnumToKoreanConverterTests
{
    private readonly EnumToKoreanConverter _sut = new();

    private object? Convert(object? value)
        => _sut.Convert(value!, typeof(string), null!, CultureInfo.InvariantCulture);

    [Fact]
    public void should_return_korean_when_known_event_type()
    {
        Assert.Equal("침입", EnumKoreanMap.To(EnumEventType.Intrusion));
        Assert.Equal("장애", EnumKoreanMap.To(EnumEventType.Fault));
    }

    [Fact]
    public void should_return_korean_when_known_detection_type()
    {
        Assert.Equal("케이블 절단", EnumKoreanMap.To(EnumDetectionType.CABLE_CUTTING));
        Assert.Equal("AI 탐지", EnumKoreanMap.To(EnumDetectionType.AI_DETECT));
    }

    [Fact]
    public void should_return_korean_when_known_fault_type()
    {
        Assert.Equal("제어기 장애", EnumKoreanMap.To(EnumFaultType.FAULT_CONTROLLER));
        Assert.Equal("케이블 절단", EnumKoreanMap.To(EnumFaultType.FAULT_CABLE_CUTTING));
    }

    [Fact]
    public void should_return_korean_when_known_device_type()
    {
        // 사용자 지목 사례 — "Fence"가 그대로 노출되던 지점
        Assert.Equal("펜스센서", EnumKoreanMap.To(EnumDeviceType.Fence));
        Assert.Equal("제어기", EnumKoreanMap.To(EnumDeviceType.Controller));
        Assert.Equal("카메라", EnumKoreanMap.To(EnumDeviceType.IpCamera));
    }

    [Fact]
    public void should_return_actioned_label_when_status_true_false()
    {
        // 이벤트 문맥: True=조치완료, False=미조치 (IsActioned = Status == True)
        Assert.Equal("조치완료", EnumKoreanMap.To(EnumTrueFalse.True));
        Assert.Equal("미조치", EnumKoreanMap.To(EnumTrueFalse.False));
    }

    [Fact]
    public void should_fallback_to_tostring_when_value_unmapped()
    {
        // 매핑 딕셔너리에 없는(정의되지 않은) 값 → 원문(ToString()) 폴백, 크래시/빈칸 금지
        var unmapped = (EnumEventType)9999;
        Assert.Equal(unmapped.ToString(), EnumKoreanMap.To(unmapped));
    }

    [Fact]
    public void should_fallback_to_tostring_when_type_not_handled()
    {
        // switch에서 처리하지 않는 enum 타입(EnumDeviceCategory) → default 분기(ToString())
        Assert.Equal(EnumDeviceCategory.Camera.ToString(), EnumKoreanMap.To(EnumDeviceCategory.Camera));
    }

    [Fact]
    public void should_return_empty_when_value_is_null()
    {
        Assert.Equal(string.Empty, EnumKoreanMap.To(null));
        Assert.Equal(string.Empty, Convert(null));
    }

    [Fact]
    public void should_return_korean_when_converter_convert_called()
    {
        Assert.Equal("펜스 장애", Convert(EnumFaultType.FAULT_FENCE));
        Assert.Equal("펜스센서", Convert(EnumDeviceType.Fence));
    }

    [Fact]
    public void should_throw_when_convertback_called()
    {
        Assert.Throws<NotSupportedException>(
            () => _sut.ConvertBack("침입", typeof(EnumEventType), null!, CultureInfo.InvariantCulture));
    }
}
