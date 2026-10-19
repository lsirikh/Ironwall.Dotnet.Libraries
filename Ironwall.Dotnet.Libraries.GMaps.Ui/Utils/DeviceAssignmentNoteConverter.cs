using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;
using Ironwall.Dotnet.Monitoring.Models.Devices;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

/// <summary>
/// 속성 판 「연결디바이스」 콤보 항목 옆 흐린 글 — values[0]=장비, values[1]=장비 → 할당 글 사전
/// (<c>GMapPropertyPidsControl.DeviceAssignmentNotes</c>). 다른 마커에 연결된 장비면 "· 정문1 에 할당됨", 아니면 빈 글.
/// </summary>
public class DeviceAssignmentNoteConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
        => values.Length > 1 && values[0] is IBaseDeviceModel device
           && values[1] is IReadOnlyDictionary<IBaseDeviceModel, string> notes
           && notes.TryGetValue(device, out var note) ? note : string.Empty;

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
