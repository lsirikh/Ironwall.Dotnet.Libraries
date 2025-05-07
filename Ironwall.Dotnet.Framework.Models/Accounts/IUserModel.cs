namespace Ironwall.Dotnet.Framework.Models.Accounts;

public interface IUserModel : IUserBaseModel
{
    string? EmployeeNumber { get; set; }
    string? Birth { get; set; }
    string? Phone { get; set; }
    string? Address { get; set; }
    string? EMail { get; set; }
    string? Image { get; set; }
    string? Position { get; set; }
    string? Department { get; set; }
    string? Company { get; set; }
    
}