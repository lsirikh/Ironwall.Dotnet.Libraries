using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Settings.CameraPopup;

/****************************************************************************
   Purpose      : 설정 블록 통지 바탕 — 디스패처에 기대지 않는다(헤드리스 시험)
   Created By   : Claude (T-03)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// <see cref="INotifyPropertyChanged"/> 최소 구현. Caliburn <c>PropertyChangedBase</c> 는 UI 스레드로 마샬하므로
/// 헤드리스 시험에서 순서가 흔들린다 — 이 블록은 설정 창(UI 스레드)에서만 쓰여 마샬이 필요 없다.
/// </summary>
public abstract class CameraPopupObservable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Raise(name);
        return true;
    }

    protected void Raise(params string?[] names)
    {
        foreach (var name in names) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
