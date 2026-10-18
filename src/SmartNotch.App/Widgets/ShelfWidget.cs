using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using SmartNotch.App.Services;

namespace SmartNotch.App.Widgets;

/// <summary>
/// Shelf 1:1 identik dengan Nusa dari Instagram Reel (Kotak putus-putus 'Release to add', paginasi, drag-in & drag-out).
/// </summary>
public sealed class ShelfWidget : INotchWidget
{
    public static string Dir => System.IO.Path.Combine(CalendarService.Dir, "Shelf");

    private readonly StackPanel _filesRow = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    private readonly Grid _dropArea = new();
    private readonly TextBlock _headerTime = Ui.T("12:00", 11, true, Ui.B("#D0FFFFFF"));
    private Point _dragStart;

    public string Title => "Shelf";
    public FrameworkElement View { get; }

    public ShelfWidget()
    {
        Directory.CreateDirectory(Dir);

        // Header: "Shelf" di kiri, "[🟢]  19:21  [||||]" di tengah (1:1 Nusa Reel)
        var head = new Grid { Margin = new Thickness(6, 0, 6, 8) };
        var title = Ui.T("Shelf", 13, true, Brushes.White);
        title.HorizontalAlignment = HorizontalAlignment.Left;
        title.VerticalAlignment = VerticalAlignment.Center;

        var centerStatus = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var gDot = new Ellipse { Width = 6, Height = 6, Fill = Ui.B("#30D158"), Margin = new Thickness(0, 0, 6, 0) };
        var waveMini = Ui.T("||||", 9, true, Ui.B("#80FFFFFF"));
        waveMini.Margin = new Thickness(6, 0, 0, 0);
        centerStatus.Children.Add(gDot);
        centerStatus.Children.Add(_headerTime);
        centerStatus.Children.Add(waveMini);

        head.Children.Add(title);
        head.Children.Add(centerStatus);

        // Dashed border 'Release to add' (1:1 Nusa Reel)
        var dashedBorder = new Border
        {
            Height = 84,
            CornerRadius = new CornerRadius(14),
            Background = Ui.B("#0DFFFFFF"),
            BorderBrush = Ui.B("#3AFFFFFF"),
            BorderThickness = new Thickness(1.5),
            ClipToBounds = true
        };
        // Setup visual dash via child grid
        var dropContent = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var iconBox = new TextBlock
        {
            Text = "📥",
            FontSize = 22,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 4)
        };
        var dropText = Ui.T("Release to add", 12, false, Ui.B("#9EFFFFFF"), HorizontalAlignment.Center);
        dropContent.Children.Add(iconBox);
        dropContent.Children.Add(dropText);

        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _filesRow,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        _dropArea.Children.Add(dropContent);
        _dropArea.Children.Add(scroll);
        dashedBorder.Child = _dropArea;

        // Bottom pagination dots: • • ━
        var dotsRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 6, 0, 0)
        };
        dotsRow.Children.Add(new Ellipse { Width = 4, Height = 4, Fill = Ui.B("#44FFFFFF"), Margin = new Thickness(2, 0, 2, 0) });
        dotsRow.Children.Add(new Ellipse { Width = 4, Height = 4, Fill = Ui.B("#44FFFFFF"), Margin = new Thickness(2, 0, 2, 0) });
        dotsRow.Children.Add(new Rectangle { Width = 12, Height = 3, RadiusX = 1.5, RadiusY = 1.5, Fill = Brushes.White, Margin = new Thickness(2, 0, 2, 0) });

        var root = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        root.Children.Add(head);
        root.Children.Add(dashedBorder);
        root.Children.Add(dotsRow);
        View = root;
    }

    public void OnShown()
    {
        _headerTime.Text = DateTime.Now.ToString("HH:mm");
        Refresh();
    }

    public void AddFiles(IDataObject data)
    {
        if (data.GetData(DataFormats.FileDrop) is not string[] paths) return;
        Directory.CreateDirectory(Dir);
        foreach (var p in paths)
        {
            try
            {
                if (!File.Exists(p)) continue;
                var name = System.IO.Path.GetFileName(p);
                var dest = System.IO.Path.Combine(Dir, name);
                int n = 1;
                while (File.Exists(dest))
                    dest = System.IO.Path.Combine(Dir, $"{System.IO.Path.GetFileNameWithoutExtension(name)} ({n++}){System.IO.Path.GetExtension(name)}");
                File.Copy(p, dest);
            }
            catch { }
        }
        Refresh();
    }

    private void Refresh()
    {
        _filesRow.Children.Clear();
        var files = new DirectoryInfo(Dir).GetFiles().OrderByDescending(f => f.CreationTime).ToList();

        // Jika tidak ada file: tampilkan icon tray "Release to add"
        _dropArea.Children[0].Visibility = files.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _dropArea.Children[1].Visibility = files.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        foreach (var f in files)
        {
            var name = f.Name.Length > 15 ? f.Name[..12] + "..." + f.Extension : f.Name;
            var x = Ui.Btn("✕", () => { try { f.Delete(); } catch { } Refresh(); }, 10);
            x.Padding = new Thickness(4, 2, 4, 2);

            var chip = new Border
            {
                Background = Ui.B("#25FFFFFF"),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 6, 6, 6),
                Margin = new Thickness(4, 0, 4, 0),
                Cursor = Cursors.Hand,
                ToolTip = f.FullName,
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children = { Ui.T("📄 " + name, 12, false, Brushes.White), x }
                }
            };

            chip.MouseLeftButtonDown += (_, e) =>
            {
                _dragStart = e.GetPosition(null);
                if (e.ClickCount == 2)
                {
                    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(f.FullName) { UseShellExecute = true }); } catch { }
                }
            };
            chip.MouseMove += (_, e) =>
            {
                if (e.LeftButton != MouseButtonState.Pressed) return;
                var d = e.GetPosition(null) - _dragStart;
                if (Math.Abs(d.X) < 6 && Math.Abs(d.Y) < 6) return;
                NotchWindow.Instance.Pinned = true;
                try
                {
                    DragDrop.DoDragDrop(chip, new DataObject(DataFormats.FileDrop, new[] { f.FullName }), DragDropEffects.Copy | DragDropEffects.Move);
                }
                finally
                {
                    NotchWindow.Instance.Pinned = false;
                }
            };
            _filesRow.Children.Add(chip);
        }
    }
}
