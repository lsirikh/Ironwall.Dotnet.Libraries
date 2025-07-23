namespace Ironwall.Dotnet.Monitoring.Models.Accounts;

public interface IAccountModel : IUserModel
{
    string? EmployeeNumber { get; set; }
    DateTime? Birth { get; set; }
    string? Phone { get; set; }
    string? Address { get; set; }
    string? EMail { get; set; }
    string? Image { get; set; }
    string? Position { get; set; }
    string? Department { get; set; }
    string? Company { get; set; }
    void Clear();
    void Update(IAccountModel model);
}