using System;
using System.Globalization;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
/****************************************************************************
   Purpose      : 워크벤치 표시 변환기 — 상태 글자 · 아이콘 · 카테고리 표기
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
