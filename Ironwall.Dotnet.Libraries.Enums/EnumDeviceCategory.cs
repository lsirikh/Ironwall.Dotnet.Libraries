namespace Ironwall.Dotnet.Libraries.Enums
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 5/26/2023 1:06:26 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    /// <summary>
    /// 장비 <b>카테고리</b>(판별자) — 서버 <c>category_device</c> 에 대응한다.
    /// </summary>
    /// <remarks>
    /// <para>서버 어휘는 <b>소문자 7값</b>이다 —
    /// <c>controller · sensor · camera · speaker · enclosure · lamp · gate</c>
    /// (배포 스웨거 8.0.1 <c>EnumDeviceCategory</c> 실측 2026-09-18).
    /// 운영 6.3.2 는 같은 이름의 enum 에 <c>gate</c> 가 <b>없는 6값</b>이다 —
    /// 늘어난 값이므로 관용 수용이면 양쪽을 함께 견딘다.</para>
    /// <para>이 축은 <b>종류축(<see cref="EnumDeviceType"/>)과 다르다</b> —
    /// 카테고리는 "무엇인가"(경로가 정한다), 종류는 그 안의 제품 종류다.
    /// 파싱은 항상 <b>미지 값 폴백</b>(<see cref="None"/>)을 둔다(브로커 명세 요구).</para>
    /// <para>⚠ <c>Gate</c> 는 <b>맨 끝에</b> 붙였다 — 중간에 끼우면 <c>Etc</c> 의 서수가 밀린다.</para>
    /// </remarks>
    public enum EnumDeviceCategory
    {
        None,
        Controller,
        Sensor,
        Camera,
        Speaker,
        Enclosure,
        Lamp,
        Etc,
        /// <summary>통문 — 서버 8.0.1 이 추가한 7번째 카테고리(<c>gate</c>). 6.3.2 에는 없다.</summary>
        Gate,
    }
}
