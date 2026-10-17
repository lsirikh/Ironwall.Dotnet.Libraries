using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;

/****************************************************************************
   Purpose      : 셸 밖에 따로 뜬 비모달 창이 셸 종료에 말없이 버려지지 않게 한다 (U-27 · V-17)
   Created By   : Claude
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 셸이 소유한 <b>비모달</b> 창 한 벌 — 셸이 끝나기 전에 "닫아도 되는가" 를 물을 수 있는 창.
/// <para><b>왜 필요한가</b>: 셸(소유자)이 닫히면 WPF 는 소유 창을 <c>Closing</c> 없이 함께 없앤다 — 창의 닫기 판정(<c>CanCloseAsync</c>)이
/// 불리지 않아 적용하지 않은 편집이 말없이 버려졌다(V-17). 셸은 창을 여는 쪽(런처)을 모르므로, 런처가 여기에 창을 올려 두고 셸이 종료 전에 차례로 묻는다.</para>
/// <para>모달 창(조립기 · 결선 · 매핑 작업대 · 서버 지표 이력 …)은 올리지 않는다 — 떠 있는 동안 셸이 막혀 사람이 셸을 닫을 수 없다.</para>
/// </summary>
public interface IGuardedWindow
{
    /// <summary>로그에 남길 이름(예: "부대 편제").</summary>
    string Name { get; }

    /// <summary>닫아도 되면 <c>true</c>. 남은 것이 있으면 창이 스스로 묻는다(✕ 와 같은 판정).</summary>
    Task<bool> CanCloseAsync(CancellationToken cancellationToken = default);

    /// <summary>이 창을 앞으로 — 최소화돼 있으면 복원하고 활성화한다(종료를 막은 창을 사람이 바로 보게).</summary>
    void BringToFront();

    /// <summary>
    /// <b>묻지 않고</b> 닫는다 — 사람이 이미 답한 뒤(종료에 동의) 또는 사람이 고를 수 없는 강제 종료(OS 로그오프 · 종료)에서만 쓴다.
    /// 남은 편집은 버린다.
    /// </summary>
    Task CloseWithoutAskingAsync();
}

/// <summary>지금 떠 있는 <see cref="IGuardedWindow"/> 목록. 런처가 창을 띄울 때 올리고 창이 닫히면 내린다.</summary>
public interface IGuardedWindowRegistry
{
    /// <summary>창을 올린다. 돌려받은 것을 <see cref="IDisposable.Dispose"/> 하면 내린다(여러 번 불러도 한 번만 내린다).</summary>
    IDisposable Register(IGuardedWindow window);

    /// <summary>지금 떠 있는 창들 — 올린 순서대로의 사본(도는 동안 목록이 바뀌어도 안전하다).</summary>
    IReadOnlyList<IGuardedWindow> OpenWindows { get; }
}

/// <summary>
/// <see cref="IGuardedWindowRegistry"/> 의 기본 구현.
/// <para><b>스레드</b>: 올리고 내리는 쪽은 UI 스레드(런처)지만 목록은 잠금으로 지킨다 — 사본만 밖으로 내보낸다.</para>
/// </summary>
public sealed class GuardedWindowRegistry : IGuardedWindowRegistry
{
    private readonly object _gate = new();
    private readonly List<IGuardedWindow> _windows = new();

    public IDisposable Register(IGuardedWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        lock (_gate)
        {
            if (!_windows.Contains(window)) _windows.Add(window);
        }
        return new Registration(this, window);
    }

    public IReadOnlyList<IGuardedWindow> OpenWindows
    {
        get { lock (_gate) return _windows.ToArray(); }
    }

    private void Unregister(IGuardedWindow window)
    {
        lock (_gate) _windows.Remove(window);
    }

    private sealed class Registration : IDisposable
    {
        private GuardedWindowRegistry? _owner;
        private readonly IGuardedWindow _window;

        public Registration(GuardedWindowRegistry owner, IGuardedWindow window)
        {
            _owner = owner;
            _window = window;
        }

        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Unregister(_window);
    }
}
