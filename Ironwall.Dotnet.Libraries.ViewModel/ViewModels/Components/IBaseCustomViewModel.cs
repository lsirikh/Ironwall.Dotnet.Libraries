using Ironwall.Dotnet.Libraries.Base.Models;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
public interface IBaseCustomViewModel<T> : ISelectableBaseViewModel where T : IBaseModel
{
    T Model { get; }
    void Dispose();
    void OnLoaded(object sender, SizeChangedEventArgs e);
    void UpdateModel(T model);
}