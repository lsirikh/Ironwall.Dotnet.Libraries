using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// Caliburn 의 <b>정적</b> <c>IoC</c> 델리게이트를 테스트 동안만 설치하고, 끝나면 <b>이전 값으로 되돌린다</b>.
/// </summary>
/// <remarks>
/// <para><c>BasePanelViewModel()</c>(매개변수 없는 생성자)은 <c>IoC.Get&lt;IEventAggregator&gt;()</c> 를 부른다.
/// 그 생성자를 타는 VM 을 만드는 테스트는 스스로 IoC 를 준비해야 한다 — 종전엔 준비 없이, 다른 테스트 클래스가
/// 우연히 설치해 둔 델리게이트에 기대고 있었다. 그 클래스가 <c>Dispose</c> 에서 <c>null</c> 로 되돌린 뒤에 돌면
/// <c>NullReferenceException</c>, 살아 있는 동안 돌면 통과 — 실행 순서에 따라 0~2건이 오가는 flaky 였다.</para>
/// <para>정적 상태이므로 이 범위를 쓰는 클래스는 전부 <c>[Collection("CaliburnIoC")]</c> 로 직렬화한다.</para>
/// </remarks>
internal sealed class TestIoCScope : IDisposable
{
    private readonly Func<Type, string, object> _getInstance;
    private readonly Func<Type, IEnumerable<object>> _getAllInstances;
    private readonly Action<object> _buildUp;

    public TestIoCScope()
    {
        _getInstance = IoC.GetInstance;
        _getAllInstances = IoC.GetAllInstances;
        _buildUp = IoC.BuildUp;

        IoC.GetInstance = (type, key) => type == typeof(IEventAggregator) ? new EventAggregator() : null!;
        IoC.GetAllInstances = type => Enumerable.Empty<object>();
        IoC.BuildUp = obj => { };
    }

    public void Dispose()
    {
        IoC.GetInstance = _getInstance;
        IoC.GetAllInstances = _getAllInstances;
        IoC.BuildUp = _buildUp;
    }
}
