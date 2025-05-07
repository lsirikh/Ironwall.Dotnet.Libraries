using System.ComponentModel;

namespace Ironwall.Dotnet.Libraries.Base.Models;
public interface ICommonMessageModel
{
    string Explain { get; set; }
    IMessageModel MessageModel { get; set; }
    string Title { get; set; }

    event PropertyChangedEventHandler PropertyChanged;

    void OnPropertyChanged(string propertyName);
}