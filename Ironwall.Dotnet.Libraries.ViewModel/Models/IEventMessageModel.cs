namespace Ironwall.Dotnet.Libraries.ViewModel.Models;

public interface IEventMessageModel<T>
{
    T Value { get; set; }
}