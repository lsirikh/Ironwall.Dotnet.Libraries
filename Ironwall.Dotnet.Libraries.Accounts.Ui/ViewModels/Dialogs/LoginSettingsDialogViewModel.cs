using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Utils.Consoles.Dialogs;
using System;
using Action = System.Action;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Dialogs;

/****************************************************************************
   Purpose      : 로그인 설정 창 — SSO 연동 켜기 · 끄기 (사용자 2026-10-16)
   Company      : Sensorway Co., Ltd.
   Notes        : 로그인 화면의 톱니가 연다(로그인 전에도). 저장은 ILoginSettingsPort 가 한다 — 라이브러리는 설정 파일을 모른다.
                  두 단계: ① 고치기([저장] · [취소]) ② 저장 뒤 재시작 안내([지금 다시 시작] · [나중에], 재시작 길이 없으면 [닫기]만).
****************************************************************************/
public sealed class LoginSettingsDialogViewModel : PropertyChangedBase
{
    /// <param name="port">설정 창구. 생성 때 저장된 값을 읽는다(못 읽으면 예외 — 부르는 쪽이 창을 열지 않는다).</param>
    /// <param name="ssoStatus">지금 SSO 상태 한 줄(연결됨 · 없음 · 꺼짐).</param>
    /// <param name="close">창을 닫는다.</param>
    public LoginSettingsDialogViewModel(ILoginSettingsPort port, string ssoStatus, Action close, ILogService? log = null)
    {
        _port = port;
        _close = close;
        _log = log;
        SsoStatus = ssoStatus;
        _saved = port.ReadIsSsoEnabled();
        _isSsoEnabled = _saved;
    }

    /// <summary>SSO 연동 사용(고치는 값). 저장 전에는 설정 파일에 닿지 않는다.</summary>
    public bool IsSsoEnabled
    {
        get => _isSsoEnabled;
        set
        {
            if (_isSsoEnabled == value || IsSaved) return;
            _isSsoEnabled = value;
            NotifyOfPropertyChange(nameof(IsSsoEnabled));
            NotifyOfPropertyChange(nameof(IsPrimaryEnabled));
        }
    }

    /// <summary>지금 SSO 상태 한 줄 — 읽기 전용.</summary>
    public string SsoStatus { get; }

    /// <summary>저장을 마쳤다 — 재시작 안내 단계. 이 단계에서는 값을 고치지 않는다.</summary>
    public bool IsSaved { get; private set; }

    /// <summary>값을 고칠 수 있는가(저장 전).</summary>
    public bool IsEditable => !IsSaved;

    public string PrimaryText => !IsSaved ? "저장" : _port.CanRestart ? "지금 다시 시작" : string.Empty;
    public string SecondaryText => !IsSaved ? "취소" : _port.CanRestart ? "나중에" : "닫기";

    /// <summary>바꾼 것이 없으면 [저장] 이 꺼진다 — 재시작 안내가 헛나가지 않게.</summary>
    public bool IsPrimaryEnabled => IsSaved || _isSsoEnabled != _saved;

    /// <summary>버튼 줄 왼쪽 안내(재시작 뒤 적용 · 실패 까닭).</summary>
    public string Message { get => _message; private set { _message = value; NotifyOfPropertyChange(nameof(Message)); } }
    public DialogMessageSeverity MessageSeverity { get => _severity; private set { _severity = value; NotifyOfPropertyChange(nameof(MessageSeverity)); } }

    /// <summary>주 동작 — 저장 단계면 저장, 안내 단계면 다시 시작.</summary>
    public void Primary()
    {
        if (!IsPrimaryEnabled) return;
        if (!IsSaved) { Save(); return; }
        if (!_port.CanRestart) return;
        try
        {
            _port.Restart();
        }
        catch (Exception ex)
        {
            _log?.Warning($"[LoginSettings] 다시 시작 실패: {ex.GetType().Name} {ex.Message}");
            SetMessage("다시 시작하지 못했습니다. 프로그램을 직접 다시 시작하세요.", DialogMessageSeverity.Critical);
        }
    }

    /// <summary>보조 동작 — 취소 · 나중에 · 닫기. 저장 전이면 아무것도 쓰지 않는다.</summary>
    public void Secondary() => _close();

    private void Save()
    {
        try
        {
            _port.WriteIsSsoEnabled(_isSsoEnabled);
        }
        catch (Exception ex)
        {
            _log?.Warning($"[LoginSettings] 저장 실패: {ex.GetType().Name} {ex.Message}");
            SetMessage("저장하지 못했습니다. 설정 파일을 확인하세요.", DialogMessageSeverity.Critical);
            return;
        }

        _log?.Info($"[LoginSettings] SSO 연동 사용={_isSsoEnabled} 저장 — 다시 시작하면 적용");
        _saved = _isSsoEnabled;
        IsSaved = true;
        SetMessage("저장했습니다. 프로그램을 다시 시작하면 적용됩니다.", DialogMessageSeverity.Info);
        NotifyOfPropertyChange(nameof(IsSaved));
        NotifyOfPropertyChange(nameof(IsEditable));
        NotifyOfPropertyChange(nameof(PrimaryText));
        NotifyOfPropertyChange(nameof(SecondaryText));
        NotifyOfPropertyChange(nameof(IsPrimaryEnabled));
    }

    private void SetMessage(string message, DialogMessageSeverity severity)
    {
        MessageSeverity = severity;
        Message = message;
    }

    private readonly ILoginSettingsPort _port;
    private readonly Action _close;
    private readonly ILogService? _log;
    private bool _saved;
    private bool _isSsoEnabled;
    private string _message = string.Empty;
    private DialogMessageSeverity _severity;
}
