using System.Runtime.CompilerServices;
using Ironwall.Dotnet.Libraries.CameraPopup;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Probe.Gis;
using Ironwall.Probe.Popup;
using Xunit;

// 이 파일의 대역 타입은 일부러 팝업 이름공간 밖에 둔다 — 시험 어셈블리 이름공간(…CameraPopup.Tests)은
// 기본 출처 접두사와 겹쳐 무엇이든 "팝업 출처" 로 보이기 때문이다.
namespace Ironwall.Probe.Gis
{
    /// <summary>GIS 지도 VM 대역(팝업 아님).</summary>
    internal sealed class FakeMapViewModel
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void SelectCameraPopup(object popup) => throw new InvalidOperationException("GIS bug in SelectedCameraPopup setter");

        [MethodImpl(MethodImplOptions.NoInlining)]
        public int IndexBug() => new List<int>()[3];

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void CallPopupLibraryBadly() => FrameCodec.Encode(ReadOnlySpan<byte>.Empty);
    }
}

namespace Ironwall.Probe.Popup
{
    /// <summary>지도 위 팝업 VM 대역(팝업 전용 타입 — AddOrigin 으로 등록).</summary>
    internal sealed class FakePopupViewModel
    {
        private readonly FakeMapViewModel _map;

        public FakePopupViewModel(FakeMapViewModel map) => _map = map;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void RaiseSelectRequested() => _map.SelectCameraPopup(this);

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void OwnBug() => throw new InvalidOperationException("popup bug");

        [MethodImpl(MethodImplOptions.NoInlining)]
        public int OwnBclBug() => new List<int>()[3];

        [MethodImpl(MethodImplOptions.NoInlining)]
        public async Task OwnAsyncBugAsync()
        {
            await Task.Yield();
            throw new InvalidOperationException("popup async bug");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public async Task AwaitGisAsync()
        {
            await Task.Run(() => _map.SelectCameraPopup(this));
        }
    }
}

namespace Ironwall.Dotnet.Libraries.CameraPopup.Tests.Unit
{
    /// <summary>
    /// M3 — FR-27 경계는 "팝업 코드가 던진 예외" 만 처리됨으로 본다. 팝업 VM 이 동기로 부른 GIS 코드(MapViewModel)의
    /// 예외는 스택에 팝업 타입이 끼어 있어도 GIS 버그로 드러나야 한다(가려지면 안 된다).
    /// </summary>
    public class FaultOriginTests
    {
        static FaultOriginTests() => CameraPopupFaults.AddOrigin("Ironwall.Probe.Popup.FakePopupViewModel");

        private static Exception Catch(Action action)
        {
            try { action(); }
            catch (Exception ex) { return ex; }
            throw new Xunit.Sdk.XunitException("no exception thrown");
        }

        private static async Task<Exception> CatchAsync(Func<Task> action)
        {
            try { await action(); }
            catch (Exception ex) { return ex; }
            throw new Xunit.Sdk.XunitException("no exception thrown");
        }

        [Fact]
        public void should_not_mark_popup_origin_when_gis_code_called_from_popup_throws()
        {
            // Arrange
            var popup = new FakePopupViewModel(new FakeMapViewModel());

            // Act
            var ex = Catch(popup.RaiseSelectRequested);

            // Assert — 스택에 팝업 VM 이 있어도 던진 곳은 GIS
            Assert.Contains("FakePopupViewModel", ex.StackTrace);
            Assert.False(CameraPopupFaults.IsPopupOrigin(ex));
            Assert.False(CameraPopupFaults.IsPopupOrigin(new AggregateException(ex)));
        }

        [Fact]
        public async Task should_not_mark_popup_origin_when_popup_awaits_gis_code_that_throws()
        {
            var popup = new FakePopupViewModel(new FakeMapViewModel());

            var ex = await CatchAsync(popup.AwaitGisAsync);

            Assert.False(CameraPopupFaults.IsPopupOrigin(ex));
        }

        [Fact]
        public void should_not_mark_popup_origin_when_gis_code_hits_framework_exception()
        {
            var ex = Catch(() => new FakeMapViewModel().IndexBug());

            Assert.False(CameraPopupFaults.IsPopupOrigin(ex));
        }

        [Fact]
        public void should_mark_popup_origin_when_popup_code_itself_throws()
        {
            var popup = new FakePopupViewModel(new FakeMapViewModel());

            Assert.True(CameraPopupFaults.IsPopupOrigin(Catch(popup.OwnBug)));
            Assert.True(CameraPopupFaults.IsPopupOrigin(Catch(() => popup.OwnBclBug())));   // 프레임워크 안에서 터져도 가장 안쪽 사용자 코드가 팝업
            Assert.True(CameraPopupFaults.IsPopupOrigin(new AggregateException(Catch(popup.OwnBug))));
        }

        [Fact]
        public async Task should_mark_popup_origin_when_popup_async_code_throws()
        {
            var popup = new FakePopupViewModel(new FakeMapViewModel());

            var ex = await CatchAsync(popup.OwnAsyncBugAsync);

            Assert.True(CameraPopupFaults.IsPopupOrigin(ex));
        }

        [Fact]
        public void should_mark_popup_origin_when_popup_library_throws()
        {
            var ex = Catch(() => new FakeMapViewModel().CallPopupLibraryBadly());

            Assert.True(CameraPopupFaults.IsPopupOrigin(ex));
        }

        [Fact]
        public void should_not_mark_popup_origin_when_exception_was_never_thrown()
        {
            Assert.False(CameraPopupFaults.IsPopupOrigin(new InvalidOperationException("never thrown")));
            Assert.False(CameraPopupFaults.IsPopupOrigin(null));
        }
    }
}
