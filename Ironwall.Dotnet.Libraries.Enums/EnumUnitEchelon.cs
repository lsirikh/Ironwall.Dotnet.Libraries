using System.Runtime.Serialization;

namespace Ironwall.Dotnet.Libraries.Enums
{
    /// <summary>
    /// 부대 제대(echelon) — 편제 계층의 단계. GOP API <b>8.0</b> 신설.
    /// <para>와이어 값은 멤버명과 동일한 <b>PascalCase 문자열</b>이다
    /// (배포 스웨거 8.0.1 <c>components.schemas.EnumUnitEchelon</c> 실측:
    /// <c>Division·Regiment·Battalion·Company·Outpost</c>).
    /// 정수 서수가 아니므로 DTO 프로퍼티에 <c>StringEnumConverter</c> 를 지정해 쓴다.</para>
    /// </summary>
    /// <remarks>
    /// <para>★ <b>다섯 값으로 시작한다</b> — 소대(<c>Platoon</c>)는 넣지 않는다.
    /// 요구사항 원문의 최하위 제대가 소초(Outpost)이고, 서버 PostgreSQL enum 은 값 추가는 되지만
    /// <b>제거가 안 된다</b>. 서버가 넣지 않은 값을 우리가 먼저 만들면 와이어에 없는 값이 생긴다.</para>
    /// <para>★ <see cref="EnumMilitaryUnitSize"/> 와 <b>절대 혼용하지 않는다</b> —
    /// 그것은 군대부호 계급장 렌더용 11값 enum(개인~군집단)이고, 이것은 서버 편제 5값 문자열이다.
    /// 값 집합·용도·표현이 모두 다르다.</para>
    /// <para>★ 선언 순서가 <b>제대 서열</b>이다(위 → 아래). 서수를 와이어로 보내지는 않지만
    /// "상위 부대는 엄격히 상위 제대" 규칙을 지역에서 검사할 때 이 순서를 쓴다.</para>
    /// </remarks>
    public enum EnumUnitEchelon
    {
        /// <summary>사단.</summary>
        [EnumMember(Value = "Division")]
        Division = 0,

        /// <summary>연대.</summary>
        [EnumMember(Value = "Regiment")]
        Regiment = 1,

        /// <summary>대대.</summary>
        [EnumMember(Value = "Battalion")]
        Battalion = 2,

        /// <summary>중대.</summary>
        [EnumMember(Value = "Company")]
        Company = 3,

        /// <summary>소초 — 현재 계약의 <b>최하위</b> 제대.</summary>
        [EnumMember(Value = "Outpost")]
        Outpost = 4,
    }
}
