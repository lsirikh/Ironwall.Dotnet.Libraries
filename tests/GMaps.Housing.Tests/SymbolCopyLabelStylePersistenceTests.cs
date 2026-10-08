using System.Reflection;
using System.Runtime.CompilerServices;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Db.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Ironwall.Dotnet.Monitoring.Models.Symbols.Defines;
using Moq;
using Xunit;

namespace GMaps.Housing.Tests;

/// <summary>
/// 결함 D2 회귀(카탈로그 MAP-104 복제 · MAP-105 붙여넣기 · MAP-106 삭제 되돌리기 — "같은 속성").
/// 타입별 INSERT/복원 SQL 은 라벨 스타일 6종(글자색 · 배경색 · 글꼴 · 굵게 · 기울임 · 최대폭)을 쓰지 않는다(전용 부분 UPDATE 전략).
/// 복사 경로는 INSERT 뒤 곧바로 Fetch 한 모델로 마커를 만들므로, 스타일을 이어 쓰지 않으면 복사본이 그 자리에서 기본 라벨로 보였다.
/// 지도 DB 에는 붙지 않는다 — MapViewModel 을 생성자 없이 만들고 가짜 IGMapDbSymbolService 만 꽂는다.
/// </summary>
public class SymbolCopyLabelStylePersistenceTests
{
    private static (MapViewModel vm, Mock<IGMapDbSymbolService> db, List<string> calls) Vm()
    {
        var vm = (MapViewModel)RuntimeHelpers.GetUninitializedObject(typeof(MapViewModel));
        var db = new Mock<IGMapDbSymbolService>();
        var calls = new List<string>();
        typeof(MapViewModel).GetField("_gMapDbSymbolService", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(vm, db.Object);
        db.Setup(d => d.UpdateSymbolLabelStyleAsync(It.IsAny<int>(), It.IsAny<EnumColorType>(), It.IsAny<EnumColorType>(),
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
          .Callback(() => calls.Add("style")).ReturnsAsync(true);
        return (vm, db, calls);
    }

    private static T Styled<T>(T m) where T : ISymbolModel
    {
        m.Title = "원본"; m.TitleColor = EnumColorType.Red; m.TitleBackground = EnumColorType.Yellow; m.TitleFontFamily = "Malgun Gothic";
        m.TitleBold = true; m.TitleItalic = true; m.TitleMaxWidth = 120; m.Latitude = 37.5; m.Longitude = 127.0;
        return m;
    }

    private static Task<ISymbolModel?> Copy(MapViewModel vm, ISymbolModel clone)
    {
        var method = typeof(MapViewModel).GetMethod("CreateSymbolCopyAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (Task<ISymbolModel?>)method.Invoke(vm, new object[] { clone, new PointLatLng(37.5, 127.0), new PointLatLng(37.5001, 127.0001), true, CancellationToken.None })!;
    }

    private static void VerifyStyleWritten(Mock<IGMapDbSymbolService> db, int id) =>
        db.Verify(d => d.UpdateSymbolLabelStyleAsync(id, EnumColorType.Red, EnumColorType.Yellow, "Malgun Gothic", true, true, 120, It.IsAny<CancellationToken>()), Times.Once);

    [Fact]
    public async Task should_write_label_style_to_new_row_before_fetch_when_geometric_symbol_is_duplicated()
    {
        var (vm, db, calls) = Vm();
        db.Setup(d => d.InsertGeometrySymbolAsync(It.IsAny<IGeometricSymbolModel>(), It.IsAny<CancellationToken>())).Callback(() => calls.Add("insert")).ReturnsAsync(77);
        db.Setup(d => d.FetchGeometrySymbolAsync(77, It.IsAny<CancellationToken>())).Callback(() => calls.Add("fetch")).ReturnsAsync(new GeometricSymbolModel { Id = 77 });

        var result = await Copy(vm, Styled(new GeometricSymbolModel { Id = 5 }));

        Assert.Equal(77, result!.Id);
        VerifyStyleWritten(db, 77);
        Assert.Equal(new[] { "insert", "style", "fetch" }, calls);   // Fetch 결과가 새 스타일을 읽도록 순서 보장
    }

    [Fact]
    public async Task should_write_label_style_when_line_pidsgroup_and_plain_symbols_are_copied()
    {
        var (vm, db, _) = Vm();
        db.Setup(d => d.InsertLineSymbolAsync(It.IsAny<ILineSymbolModel>(), It.IsAny<CancellationToken>())).ReturnsAsync(11);
        db.Setup(d => d.InsertPidsGroupSymbolAsync(It.IsAny<IPidsGroupSymbolModel>(), It.IsAny<CancellationToken>())).ReturnsAsync(12);
        db.Setup(d => d.InsertSymbolAsync(It.IsAny<ISymbolModel>(), It.IsAny<CancellationToken>())).ReturnsAsync(13);
        db.Setup(d => d.InsertPidsSymbolAsync(It.IsAny<IPidsSymbolModel>(), It.IsAny<CancellationToken>())).ReturnsAsync(14);

        var line = Styled(new LineSymbolModel()); line.LinePoints = new List<GeoPoint> { new(37.5, 127.0, 0), new(37.5, 127.001, 0) };
        var group = Styled(new PidsGroupSymbolModel()); group.LinePoints = new List<GeoPoint> { new(37.5, 127.0, 0), new(37.5, 127.001, 0) };
        await Copy(vm, line);
        await Copy(vm, group);
        await Copy(vm, Styled(new SymbolModel()));
        await Copy(vm, Styled(new PidsSymbolModel { LinkedDeviceId = 3 }));

        foreach (var id in new[] { 11, 12, 13, 14 }) VerifyStyleWritten(db, id);
    }

    [Fact]
    public async Task should_not_fail_copy_when_label_style_write_throws()
    {
        var (vm, db, _) = Vm();
        db.Setup(d => d.InsertGeometrySymbolAsync(It.IsAny<IGeometricSymbolModel>(), It.IsAny<CancellationToken>())).ReturnsAsync(21);
        db.Setup(d => d.FetchGeometrySymbolAsync(21, It.IsAny<CancellationToken>())).ReturnsAsync(new GeometricSymbolModel { Id = 21 });
        db.Setup(d => d.UpdateSymbolLabelStyleAsync(It.IsAny<int>(), It.IsAny<EnumColorType>(), It.IsAny<EnumColorType>(),
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
          .ThrowsAsync(new InvalidOperationException("DB 끊김"));

        var result = await Copy(vm, Styled(new GeometricSymbolModel()));

        Assert.Equal(21, result!.Id);   // 스타일 쓰기 실패가 복사 자체를 죽이지 않는다
    }

    [Fact]
    public async Task should_write_label_style_when_deleted_symbol_is_restored_by_undo_with_same_or_new_id()
    {
        var (vm, db, _) = Vm();
        var method = typeof(MapViewModel).GetMethod("RestoreOrInsertSymbolAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

        // ① 같은 Id 로 복원
        var same = Styled(new GeometricSymbolModel { Id = 40 });
        db.Setup(d => d.RestoreGeometrySymbolAsync(same, It.IsAny<CancellationToken>())).ReturnsAsync(40);
        await (Task)method.Invoke(vm, new object[] { nameof(GMapGeometricMarker), same, CancellationToken.None })!;
        VerifyStyleWritten(db, 40);

        // ② Id 가 점유돼 새 Id 로 INSERT 폴백
        var taken = Styled(new GeometricSymbolModel { Id = 41 });
        db.Setup(d => d.RestoreGeometrySymbolAsync(taken, It.IsAny<CancellationToken>())).ThrowsAsync(new IdCollisionException(41));
        db.Setup(d => d.InsertGeometrySymbolAsync(taken, It.IsAny<CancellationToken>())).Callback(() => taken.Id = 90).ReturnsAsync(90);
        await (Task)method.Invoke(vm, new object[] { nameof(GMapGeometricMarker), taken, CancellationToken.None })!;
        VerifyStyleWritten(db, 90);
    }
}
