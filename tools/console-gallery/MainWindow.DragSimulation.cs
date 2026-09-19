using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using System.Text;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace ConsoleGallery;

public partial class MainWindow
{
    /// <summary>
    /// 드래그를 <b>입력 없이</b> 재현한다 — 핸들의 Thumb 이벤트를 직접 일으키고 포인터 자리만
    /// <see cref="DragPointer.Override"/> 로 알려 준다. 사용자의 마우스를 건드리지 않는다.
    /// 결과(드롭존 상태 · Draft · 순서)는 <c>drag-simulation.txt</c> 에 기대값과 나란히 적는다.
    /// </summary>
    private async Task SimulateDragsAsync(string directory, Func<string, double, System.Action, Task> shot)
    {
        var log = new StringBuilder();
        var pointer = new Point();
        DragPointer.Override = relativeTo => TranslatePoint(pointer, (UIElement)relativeTo);

        Point CenterOf(FrameworkElement e, double fy = 0.5) => e.TranslatePoint(new Point(e.ActualWidth / 2, e.ActualHeight * fy), this);
        string PartKeys() => string.Join(" | ", Parts.Select(p => p.Split(' ')[0]));

        try
        {
            // A. 장비 3건 → 부대 칩 (N회 호출 → Draft)
            OnStateMultiple(this, new RoutedEventArgs());
            await Task.Delay(300);
            var rowHandle = Descendants<DragHandle>(DeviceGrid).First(h => ReferenceEquals(h.DataContext, Rows[0]));
            var chips = Descendants<DropZoneChrome>(this).ToList();
            var unitChip = chips.First(c => c.DataContext is FakeZone { Value: "2소대" });
            var blockedChip = chips.First(c => c.DataContext is FakeZone { Value: "ENC-API-1" });

            Start(rowHandle);
            pointer = CenterOf(rowHandle);
            Delta(rowHandle, 3, 3);                                     // 데드존 미만 — 아직 끌기가 아니다
            log.AppendLine($"A0 deadzone        unit={DropZone.GetState(unitChip)}  (expect None)");

            pointer = CenterOf(unitChip);
            Delta(rowHandle, 60, 300);
            log.AppendLine($"A1 over allowed    unit={DropZone.GetState(unitChip)} (expect Hover)  blocked={DropZone.GetState(blockedChip)} (expect Blocked)");
            await shot("14-drag-3-devices-over-unit-chip", 1280, () => { });

            pointer = CenterOf(blockedChip);
            Delta(rowHandle, 400, 300);
            log.AppendLine($"A2 over blocked    unit={DropZone.GetState(unitChip)} (expect Available)  blocked={DropZone.GetState(blockedChip)} (expect Blocked)");
            await shot("15-drag-over-blocked-server-chip", 1280, () => { });

            Complete(rowHandle, canceled: false);
            log.AppendLine($"A3 drop on blocked draft={Tray.Count} (expect 0)  unit={DropZone.GetState(unitChip)} (expect None)");

            Start(rowHandle);
            pointer = CenterOf(unitChip);
            Delta(rowHandle, 60, 300);
            Complete(rowHandle, canceled: false);
            log.AppendLine($"A4 drop on unit    draft={Tray.Count} (expect 3)  calls={string.Join(",", Tray.Entries.Select(e => e.CallKind).Distinct())}");
            await shot("16-draft-tray-3-entries", 1280, () => { });

            var summary = await Tray.ApplyAsync();
            log.AppendLine($"A5 applied         {summary.ToMessage()}  units={string.Join(",", Rows.Take(3).Select(r => r.Unit))} (expect all 2소대)");
            await shot("17-draft-applied", 1280, () => { });

            // B. 순서 드래그 — 부품 0번을 3번 아래로
            OnStateSingle(this, new RoutedEventArgs());
            await Task.Delay(300);
            var before = PartKeys();
            var partHandles = Descendants<DragHandle>(PartsList).ToList();
            Start(partHandles[0]);
            pointer = CenterOf((FrameworkElement)PartsList.ItemContainerGenerator.ContainerFromIndex(3), 0.8);
            Delta(partHandles[0], 0, 90);
            log.AppendLine($"B1 reorder hover   list={DropZone.GetState(PartsList)} (expect Hover)");
            await shot("18-reorder-insertion-line", 1280, () => { });
            Complete(partHandles[0], canceled: false);
            log.AppendLine($"B2 reorder         {before}  ->  {PartKeys()}  (expect door right after ups)");

            // C. 취소(Esc · 캡처 상실) — 순서가 그대로여야 한다
            await Task.Delay(200);
            before = PartKeys();
            partHandles = Descendants<DragHandle>(PartsList).ToList();
            Start(partHandles[0]);
            pointer = CenterOf((FrameworkElement)PartsList.ItemContainerGenerator.ContainerFromIndex(4), 0.8);
            Delta(partHandles[0], 0, 120);
            Complete(partHandles[0], canceled: true);
            log.AppendLine($"C1 cancel          unchanged={before == PartKeys()} (expect True)  list={DropZone.GetState(PartsList)} (expect None)");

            // D. 키보드 폴백 — 드래그와 같은 담당을 부른다
            var keyboard = Microsoft.Xaml.Behaviors.Interaction.GetBehaviors(PartsList).OfType<ReorderKeyboardBehavior>().Single();
            PartsList.SelectedIndex = 0;
            before = PartKeys();
            var moved = keyboard.MoveSelection(+1);
            log.AppendLine($"D1 Alt+Down        moved={moved} (expect True)  {before}  ->  {PartKeys()}");
            PartsList.SelectedIndex = 0;
            log.AppendLine($"D2 Alt+Up at top   moved={keyboard.MoveSelection(-1)} (expect False)");

            // E. 데드존 미만에서 놓으면 클릭 = 그 행 선택
            DeviceGrid.UnselectAll();
            rowHandle = Descendants<DragHandle>(DeviceGrid).First(h => ReferenceEquals(h.DataContext, Rows[2]));
            Start(rowHandle);
            Delta(rowHandle, 2, 2);
            Complete(rowHandle, canceled: false);
            log.AppendLine($"E1 click fallback  selected={(DeviceGrid.SelectedItem as FakeDevice)?.Number} (expect {Rows[2].Number})");
        }
        catch (Exception ex)
        {
            log.AppendLine("EXCEPTION " + ex);
        }
        finally
        {
            DragPointer.Override = null;
            System.IO.File.WriteAllText(System.IO.Path.Combine(directory, "drag-simulation.txt"), log.ToString());
        }
    }

    private static void Start(DragHandle handle) => handle.RaiseEvent(new DragStartedEventArgs(0, 0));

    private static void Delta(DragHandle handle, double dx, double dy) => handle.RaiseEvent(new DragDeltaEventArgs(dx, dy));

    private static void Complete(DragHandle handle, bool canceled) => handle.RaiseEvent(new DragCompletedEventArgs(0, 0, canceled));

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var deeper in Descendants<T>(child)) yield return deeper;
        }
    }
}
