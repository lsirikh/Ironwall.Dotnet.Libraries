using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.EventWindow;

/// <summary>작은 INotifyPropertyChanged 바탕(호스트는 Caliburn 을 쓰지 않는다).</summary>
internal abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Raise(name);
        return true;
    }

    protected void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
