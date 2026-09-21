using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
/****************************************************************************
   Purpose      : 워크벤치 표시 변환기 — 상태 글자 · 아이콘 · 흐림 · 램프 색
   Created By   : Claude
   Created On   : 2026-09-20
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// Draft 상태를 <b>한국어 글자</b>로. 색이 아니라 글자로 구분한다 —
/// 라이트 테마에서 Primary·Selection·Focus 가 전부 같은 색이라 색으로는 상태를 못 가른다.
/// </summary>
public sealed class MappingStateTextConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        MappingDraftState.Added => "추가",
        MappingDraftState.Edited => "수정",
        MappingDraftState.Removed => "해제",
        _ => string.Empty,
    };

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>
/// 아이콘 이름 문자열 → <c>PackIconKind</c>. 문자열-enum 변환은 <b>실패해도 화면이 죽지 않아야</b> 한다.
/// </summary>
/// <remarks>
/// MDIX 의 <c>Kind</c> 에 없는 이름을 XAML 에 문자열로 적으면 런타임에 조용히 깨진다.
/// 여기서 한 번 파싱해 실패하면 물음표 아이콘으로 떨어뜨린다.
/// </remarks>
public sealed class MappingIconKindConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var name = value as string ?? value?.ToString() ?? string.Empty;
        return Enum.TryParse(typeof(MaterialDesignThemes.Wpf.PackIconKind), name, false, out var parsed)
            ? parsed!
            : MaterialDesignThemes.Wpf.PackIconKind.HelpCircleOutline;
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>
/// 조건이 맞으면 흐리게. <see cref="DimOn"/> 이 <c>false</c> 면 "값이 <c>false</c> 일 때 흐리게" 가 된다.
/// </summary>
/// <remarks>
/// 흐림은 0.55 하나로 통일한다 — 등록됨·중지·해제가 서로 다른 농도면 "무엇이 더 꺼진 것인지" 가 읽히지 않는다.
/// </remarks>
public sealed class MappingDimConverter : IValueConverter
{
    /// <summary>이 값일 때 흐리게 한다(기본 <c>true</c>).</summary>
    public bool DimOn { get; set; } = true;

    /// <summary>흐림 농도.</summary>
    public double DimOpacity { get; set; } = 0.55;

    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool flag && flag == DimOn ? DimOpacity : 1.0;

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>
/// 경광등 색 → 브러시. <b>"hex 리터럴 0" 규칙의 명시적 예외</b>이며, 리터럴은 이 한 곳에만 있다.
/// </summary>
/// <remarks>
/// Red·Orange·Green·Blue·White 는 장비가 <b>실제로 내는 점등색</b>이라 테마와 무관하게 고정한다.
/// 테마 토큰(<c>StatusCriticalBrush</c> 등)을 재사용하면 라이트/다크에서 색이 바뀌어 <b>다른 색을 지시</b>하게 된다.
/// 흰색은 흰 바탕에서 사라지므로 테두리를 함께 쓰는 자리에만 놓는다.
/// </remarks>
public sealed class MappingLampColorConverter : IValueConverter
{
    private static readonly SolidColorBrush _red = Frozen(0xD3, 0x2F, 0x2F);
    private static readonly SolidColorBrush _orange = Frozen(0xEF, 0x8E, 0x1B);
    private static readonly SolidColorBrush _green = Frozen(0x2E, 0x7D, 0x32);
    private static readonly SolidColorBrush _blue = Frozen(0x1B, 0x6E, 0xC2);
    private static readonly SolidColorBrush _white = Frozen(0xF5, 0xF5, 0xF5);
    private static readonly SolidColorBrush _unknown = Frozen(0x9E, 0x9E, 0x9E);

    private static SolidColorBrush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        EnumLampColor.Red => _red,
        EnumLampColor.Orange => _orange,
        EnumLampColor.Green => _green,
        EnumLampColor.Blue => _blue,
        EnumLampColor.White => _white,
        _ => _unknown,
    };

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>참이면 접는다(<see cref="Visibility.Collapsed"/>) — <c>BooleanToVisibilityConverter</c> 의 반대.</summary>
public sealed class MappingInverseBoolToVisibilityConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool flag && flag ? Visibility.Collapsed : Visibility.Visible;

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>카테고리 와이어 값 → 한국어 표기. 모르는 값은 원값을 그대로 보여 준다.</summary>
public sealed class MappingCategoryTextConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => Ironwall.Dotnet.Libraries.Messages.Dto.Integrations.EventMappingRules.CategoryLabel(value as string);

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
