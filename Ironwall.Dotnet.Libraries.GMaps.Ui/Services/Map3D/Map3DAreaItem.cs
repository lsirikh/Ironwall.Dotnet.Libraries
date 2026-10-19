using System;
using System.Collections.Generic;
using System.Windows.Input;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Map3D;

/****************************************************************************
   Purpose      : 2D 지도의 3D 영역 하나(구운 장면) — 지도 메뉴 「3D 영역 목록」과 영역 배지 우클릭 메뉴가 같은 항목 · 같은 동작을 쓴다.
   Note         : 편집 심볼이 아니다 — DB 에 저장하지 않고 map-3d 서버 목록(GET /api/jobs 의 scenes)에서만 온다.
   Created On   : 2026-10-17 · Sensorway Co., Ltd.
****************************************************************************/

/// <summary>영역 메뉴 한 줄(글 + 명령).</summary>
public sealed record Map3DAreaAction(string Header, ICommand Command);

public sealed class Map3DAreaItem
{
    /// <param name="scene">서버 장면.</param>
    /// <param name="view">「이 영역 3D 로 보기」.</param>
    /// <param name="rebake">「다시 만들기」.</param>
    /// <param name="delete">「지우기」(확인 뒤 서버 DELETE).</param>
    public Map3DAreaItem(Map3DSceneInfo scene, Action<Map3DAreaItem> view, Action<Map3DAreaItem> rebake, Action<Map3DAreaItem> delete)
        : this(scene, scene.EnterZoom ?? Map3DMath.EnterZoom, view, null, rebake, delete) { }

    /// <param name="enterZoom">그 영역의 3D 로 바뀌는 줌(로컬 &gt; 서버 &gt; 세팅 — Map3DMath.ResolveAreaEnterZoom 결과).</param>
    /// <param name="changeZoom">「3D 줌 바꾸기…」(다시 굽지 않고 줌만). 없으면 메뉴에 넣지 않는다.</param>
    public Map3DAreaItem(Map3DSceneInfo scene, double enterZoom, Action<Map3DAreaItem> view, Action<Map3DAreaItem>? changeZoom,
                         Action<Map3DAreaItem> rebake, Action<Map3DAreaItem> delete)
        : this(scene, enterZoom, view, changeZoom, null, rebake, delete) { }

    /// <param name="resetHeights">「직접 입력한 건물 높이 모두 자동으로…」(확인 뒤 서버 DELETE). 직접 입력이 없거나 null 이면 메뉴에 넣지 않는다.</param>
    public Map3DAreaItem(Map3DSceneInfo scene, double enterZoom, Action<Map3DAreaItem> view, Action<Map3DAreaItem>? changeZoom,
                         Action<Map3DAreaItem>? resetHeights, Action<Map3DAreaItem> rebake, Action<Map3DAreaItem> delete)
    {
        Scene = scene;
        EnterZoom = enterZoom;
        var actions = new List<Map3DAreaAction> { new("이 영역 3D 로 보기", new RelayCommand(() => view(this))) };
        if (changeZoom != null) actions.Add(new("3D 줌 바꾸기…", new RelayCommand(() => changeZoom(this))));
        if (resetHeights != null && scene.ManualHeights > 0)
            actions.Add(new($"{Map3DBuilding.ManualCountText(scene.ManualHeights)} — 모두 자동으로…", new RelayCommand(() => resetHeights(this))));
        actions.Add(new("다시 만들기", new RelayCommand(() => rebake(this))));
        actions.Add(new("지우기…", new RelayCommand(() => delete(this))));
        Actions = actions;
    }

    public Map3DSceneInfo Scene { get; }
    public string Id => Scene.Id;
    public double[] Bbox => Scene.Bbox;
    /// <summary>그 영역의 3D 로 바뀌는 줌.</summary>
    public double EnterZoom { get; }
    public string Title => Map3DGuide.AreaTitle(Scene.Id, Scene.Name);
    /// <summary>지도 메뉴 「3D 영역 목록」 한 줄 — "이름 · 줌 17 이상"(직접 입력한 높이가 있으면 " · 직접 입력한 건물 높이 n개").</summary>
    public string MenuHeader => Map3DGuide.AreaMenuHeader(Title, EnterZoom) + (Scene.ManualHeights > 0 ? $" · {Map3DBuilding.ManualCountText(Scene.ManualHeights)}" : "");
    public string ToolTip => Map3DGuide.AreaToolTip(Title, Scene.Created, Scene.Buildings, EnterZoom)
                             + (Scene.ManualHeights > 0 ? $"\n{Map3DBuilding.ManualCountText(Scene.ManualHeights)}" : "")
                             + (Scene.ManualUnmatched > 0 ? $"\n다시 붙이지 못한 직접 입력 {Scene.ManualUnmatched}개" : "");
    public IReadOnlyList<Map3DAreaAction> Actions { get; }
}
