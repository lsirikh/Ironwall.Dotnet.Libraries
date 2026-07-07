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
    /// <summary>계정 잠금 상태(서버 is_locked). 표시·잠금해제 게이팅용. 기본 false.</summary>
    bool IsLocked { get; set; }
    /// <summary>잠금 사유(서버 lock_reason). 표시용(툴팁).</summary>
    string? LockReason { get; set; }
    void Clear();
    void Update(IAccountModel model);
}