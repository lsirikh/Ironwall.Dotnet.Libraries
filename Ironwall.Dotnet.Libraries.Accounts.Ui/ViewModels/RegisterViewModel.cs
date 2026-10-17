using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Monitoring.Models.Accounts;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels;
/****************************************************************************
   Purpose      : 회원가입 입력 VM — 라이브러리 이관(전략 A) + C-1 수정
   Created By   : GHLee
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
   Notes        : [C-1 CRITICAL 수정] 기존 ctor 의 IoC.Get<IAccountModel>()(공유 싱글톤 참조)를
                  제거하고 전용 IAccountModel 을 DI 주입받는다. AccountUiModule 팩토리가
                  new AccountModel() 을 넘겨 LoginViewModel 공유 모델과 격리한다 → 등록
                  다이얼로그 오픈 시 헤더 사용자 표시가 사라지지 않는다.
****************************************************************************/
public class RegisterViewModel : AccountViewModel
{
    #region - Ctors -
    public RegisterViewModel(IEventAggregator eventAggregator
                            , ILogService log
                            , IAccountModel model)
                            : base(eventAggregator, log, model)
    {
    }
    #endregion
}
