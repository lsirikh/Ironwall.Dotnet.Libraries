namespace Ironwall.Dotnet.Libraries.Enums
{
    /// <summary>
    /// GOP 권한 매트릭스의 동작(열) 단일소스. 서버 <c>enums.py</c>(PermissionVerb) 권위와 1:1.
    /// 모듈별 활성 여부는 <see cref="PermissionCatalog.IsVerbAllowed"/>로 판정한다
    /// (Control은 Cameras만, AuditLogs는 View만). — PRD-GOP-01 Phase 5 IMPL-05.
    /// </summary>
    public enum EnumPermissionVerb
    {
        View,     // 조회
        Edit,     // 편집
        Delete,   // 삭제
        Control,  // 제어
    }
}
