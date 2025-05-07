using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.ViewModel.Models;

namespace Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
public interface IBaseViewModel<T> where T : IBaseModel
{
    T Model { get; set; }
}