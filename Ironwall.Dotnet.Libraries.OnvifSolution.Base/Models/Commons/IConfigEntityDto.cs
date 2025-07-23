namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Commons;

public interface IConfigEntityDto
{
    string Name { get; init; }
    string Token { get; init; }
    int UseCount { get; init; }
}