using System.ComponentModel;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Settings.CameraPopup;

/****************************************************************************
   Purpose      : 줄마다 "손댐" 표시 — 커널 ConsoleField.IsTouched 에 {Binding Touched[Mode]} 로 묶는다
   Created By   : Claude (T-03)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 묶음 이름 → 저장본과 다른가. 인덱서 바인딩(<c>Touched[Mode]</c>)이 쓰고, 값이 바뀌면 <c>Item[]</c> 를 알린다.
/// </summary>
public sealed class CameraPopupTouchedMap : INotifyPropertyChanged
{
    private readonly Func<string, bool> _isTouched;

    public CameraPopupTouchedMap(Func<string, bool> isTouched)
        => _isTouched = isTouched ?? throw new ArgumentNullException(nameof(isTouched));

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool this[string group] => _isTouched(group);

    internal void Refresh() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
}
