
using Ironwall.Dotnet.Libraries.Base.Services;

namespace Ironwall.Dotnet.Libraries.Db.Utils;

public interface IExcelImporter: IService
{
    Task LoadExcelDataAsync(CancellationToken token = default);
    Task<bool> ImportExcelToDbAsync(string filePath, CancellationToken token = default);
    bool Result { get; set; }
}