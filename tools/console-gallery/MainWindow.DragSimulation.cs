using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using System.Text;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace ConsoleGallery;

public partial class MainWindow
{
    /// <summary>
    /// 드래그를 <b>입력 없이</b> 재현한다 — 손잡이의 Thumb 이벤트를 직접 일으키고 포인터 자리만
    /// <see cref="DragPointer.Override"/> 로 알려 준다. 사용자의 마우스를 건드리지 않는다.
    /// 결과(드롭존 상태 · Draft · 순서 · 폭)는 <c>drag-simulation.txt</c> 에 기대값과 나란히 적는다.
    /// </summary>
    /// <remarks>
    /// Thumb 이벤트의 이동량에는 <b>일부러 0</b> 을 넣는다. 실제 WPF 에서 그 값은 "손잡이 기준" 좌표라
    /// 손잡이가 같이 움직이면 누적이 아니라 증분이 된다 — 커널이 그 값에 기대지 않는다는 것을 이 재현이 증명한다.
    /// </remarks>
    private async Task SimulateDragsAsync(string directory, Func<string, double, System.Action, Task> shot)
    {
        var log = new StringBuilder();
        var pointer = new Point();
        DragPointer.Override = relativeTo => TranslatePoint(pointer, (UIElement)relativeTo);

        Point CenterOf(FrameworkElement e, double fy = 0.5) => e.TranslatePoint(new Point(e.ActualWidth / 2, e.ActualHeight * fy), this);
        string PartKeys() => string.Join(" | ", Parts.Select(p => p.Split(' ')[0]));
        void Press(DragHandle h) { pointer = CenterOf(h); h.RaiseEvent(new DragStartedEventArgs(0, 0)); }
        void MoveTo(Thumb h, Point p) { pointer = p; h.RaiseEvent(new DragDeltaEventArgs(0, 0)); }
        static void Release(Thumb h, bool canceled = false) => h.RaiseEvent(new DragCompletedEventArgs(0, 0, canceled));

        try
        {
            // A. 장비 3건 → 부대 칩 (N회 호출 → Draft)
            OnStateMultiple(this, new RoutedEventArgs());
            await Task.Delay(300);
            var rowHandle = Descendants<DragHandle>(DeviceGrid).First(h => ReferenceEquals(h.DataContext, Rows[0]));
            var chips = Descendants<DropZoneChrome>(this).ToList();
            var unitChip = chips.First(c => c.DataContext is FakeZone { Value: "2소대" });
            var blockedChip = chips.First(c => c.DataContext is FakeZone { Value: "ENC-API-1" });

            Press(rowHandle);
            var origin = pointer;
            MoveTo(rowHandle, new Point(origin.X + 5, origin.Y + 5));              // 7.07 DIU — 데드존 안
            log.AppendLine($"A0 deadzone        unit={DropZone.GetState(unitChip)}  (expect None)");

            MoveTo(rowHandle, CenterOf(unitChip));
            log.AppendLine($"A1 over allowed    unit={DropZone.GetState(unitChip)} (expect Hover)  blocked={DropZone.GetState(blockedChip)} (expect Blocked)");
            await shot("14-drag-3-devices-over-unit-chip", 1280, () => { });

            MoveTo(rowHandle, CenterOf(blockedChip));
            log.AppendLine($"A2 over blocked    unit={DropZone.GetState(unitChip)} (expect Available)  blocked={DropZone.GetState(blockedChip)} (expect Blocked)");
            await shot("15-drag-over-blocked-server-chip", 1280, () => { });

            Release(rowHandle);
            log.AppendLine($"A3 drop on blocked draft={Tray.Count} (expect 0)  unit={DropZone.GetState(unitChip)} (expect None)");

            Press(rowHandle);
            MoveTo(rowHandle, CenterOf(unitChip));
            Release(rowHandle);
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
            Press(partHandles[0]);
            MoveTo(partHandles[0], CenterOf((FrameworkElement)PartsList.ItemContainerGenerator.ContainerFromIndex(3), 0.8));
            log.AppendLine($"B1 reorder hover   list={DropZone.GetState(PartsList)} (expect Hover)");
            await shot("18-reorder-insertion-line", 1280, () => { });
            Release(partHandles[0]);
            log.AppendLine($"B2 reorder         {before}  ->  {PartKeys()}  (expect door right after ups)");

            // C. 취소(Esc · 캡처 상실) — 순서가 그대로여야 한다
            await Task.Delay(200);
            before = PartKeys();
            partHandles = Descendants<DragHandle>(PartsList).ToList();
            Press(partHandles[0]);
            MoveTo(partHandles[0], CenterOf((FrameworkElement)PartsList.ItemContainerGenerator.ContainerFromIndex(4), 0.8));
            Release(partHandles[0], canceled: true);
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
            Press(rowHandle);
            MoveTo(rowHandle, new Point(pointer.X + 2, pointer.Y + 2));
            Release(rowHandle);
            log.AppendLine($"E1 click fallback  selected={(DeviceGrid.SelectedItem as FakeDevice)?.Number} (expect {Rows[2].Number})");

            // F. 경계 끌기 — 손잡이가 같이 움직여도(이동량 = 0 으로 흉내) 폭이 누적으로 바뀐다
            var splitter = Descendants<Thumb>(Shell).First(t => t.Name == "PART_Splitter");
            Shell.DetailWidth = 340;
            await Task.Delay(100);
            pointer = CenterOf(splitter);
            var startX = pointer.X;
            splitter.RaiseEvent(new DragStartedEventArgs(0, 0));
            MoveTo(splitter, new Point(startX - 4, pointer.Y));
            log.AppendLine($"F0 splitter deadzone  width={Shell.DetailWidth:0} (expect 340)");
            MoveTo(splitter, new Point(startX - 60, pointer.Y));
            await Task.Delay(60);
            MoveTo(splitter, new Point(startX - 100, pointer.Y));
            log.AppendLine($"F1 splitter dragged   width={Shell.DetailWidth:0} (expect 440)");
            MoveTo(splitter, new Point(startX - 400, pointer.Y));
            log.AppendLine($"F2 splitter clamped   width={Shell.DetailWidth:0} (expect 480)");
            await shot("19-splitter-dragging-480", 1280, () => { });
            Release(splitter, canceled: true);
            log.AppendLine($"F3 splitter cancel    width={Shell.DetailWidth:0} (expect 340)");

            // G. 다크 테마에서 '놓을 수 없음' 해치가 토큰을 따라오는가 — 그림으로 확인한다(20번)
            OnToggleTheme(this, new RoutedEventArgs());
            OnStateSingle(this, new RoutedEventArgs());
            await Task.Delay(300);
            // 부품을 끌면 장비용 칩은 전부 '놓을 수 없음'이 된다 — 해치가 한눈에 보인다.
            partHandles = Descendants<DragHandle>(PartsList).ToList();
            Press(partHandles[1]);
            MoveTo(partHandles[1], CenterOf((FrameworkElement)PartsList.ItemContainerGenerator.ContainerFromIndex(3), 0.2));
            await shot("20-dark-blocked-hatch", 1280, () => { });
            Release(partHandles[1], canceled: true);
            OnToggleTheme(this, new RoutedEventArgs());
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
