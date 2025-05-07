namespace Ironwall.Dotnet.Framework.Models;

public interface IEventMessageModel<T>
{
    T Value { get; set; }
}