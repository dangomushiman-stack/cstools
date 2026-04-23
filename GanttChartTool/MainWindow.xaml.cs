using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GanttChartTool
{
    public partial class MainWindow : Window
    {
        private bool _isResizingLeft = false;
        private bool _isResizingRight = false;
        
        private bool _isDraggingNote = false;
        private bool _isResizingNote = false;
        
        private Point _dragStartPos;
        private object? _dragTarget = null;
        
        private double _origX, _origY;
        private double _origWidth, _origHeight;

        private ScrollViewer? _dgScroll;
        private bool _isSync = false;

        public MainWindow() { InitializeComponent(); this.DataContext = new MainViewModel(); }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _dgScroll = GetVisualChild<ScrollViewer>(MainDataGrid);
            if (_dgScroll != null) _dgScroll.ScrollChanged += (s, ev) => { if (!_isSync) { _isSync = true; GanttScrollViewer.ScrollToVerticalOffset(ev.VerticalOffset); _isSync = false; } };
        }

        private void GanttScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e) { if (!_isSync && _dgScroll != null) { _isSync = true; _dgScroll.ScrollToVerticalOffset(e.VerticalOffset); _isSync = false; } }

        private void Bar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement el && el.DataContext is TaskItem task)
            {
                var vm = (MainViewModel)this.DataContext;
                vm.OnTaskBarClicked(task);
                
                BarItem bar = (el.Tag as string == "SubBar") ? task.SubBar : task.MainBar;

                if (bar != null && bar.Start.HasValue && bar.End.HasValue && !vm.IsLinkMode)
                {
                    bar.Snapshot();
                    _dragTarget = bar;
                    _dragStartPos = e.GetPosition(null);

                    vm.SuspendUpdates = true;
                    el.CaptureMouse();
                }
                
                e.Handled = true; 
            }
        }

        private void BarResizeLeft_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement el && el.DataContext is TaskItem task)
            {
                var vm = (MainViewModel)this.DataContext;
                vm.OnTaskBarClicked(task);
                BarItem bar = (el.Tag as string == "SubBar") ? task.SubBar : task.MainBar;

                if (bar != null && bar.Start.HasValue && bar.End.HasValue && !vm.IsLinkMode)
                {
                    _isResizingLeft = true;
                    bar.Snapshot();
                    _dragTarget = bar;
                    _dragStartPos = e.GetPosition(null);

                    vm.SuspendUpdates = true;
                    el.CaptureMouse();
                }
                
                e.Handled = true;
            }
        }

        private void BarResizeRight_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement el && el.DataContext is TaskItem task)
            {
                var vm = (MainViewModel)this.DataContext;
                vm.OnTaskBarClicked(task);
                BarItem bar = (el.Tag as string == "SubBar") ? task.SubBar : task.MainBar;

                if (bar != null && bar.Start.HasValue && bar.End.HasValue && !vm.IsLinkMode)
                {
                    _isResizingRight = true;
                    bar.Snapshot();
                    _dragTarget = bar;
                    _dragStartPos = e.GetPosition(null);

                    vm.SuspendUpdates = true;
                    el.CaptureMouse();
                }
                
                e.Handled = true;
            }
        }

        private void Bar_MouseMove(object sender, MouseEventArgs e)
        {
            if (_dragTarget is BarItem bar && this.DataContext is MainViewModel vm && bar.OriginalStart.HasValue && bar.OriginalEnd.HasValue)
            {
                if (bar.IsDragging || _isResizingLeft || _isResizingRight)
                {
                    // 1. 移動ピクセルを「何マス分か」に変換
                    double dx = e.GetPosition(null).X - _dragStartPos.X;
                    double intervals = dx / GanttSettings.DayWidth;
                    
                    // 2. マス数を「論理的な時間（TimeSpan）」に変換
                    TimeSpan movedTime = TimeSpan.FromTicks((long)(vm.SelectedInterval.TimeSpan.Ticks * intervals));
                    
                    // 3. 設定された「スナップ単位」に合わせて時間を丸める
                    TimeSpan snapInterval = vm.IsSnapToDay ? TimeSpan.FromDays(1) : vm.SelectedInterval.TimeSpan;
                    long roundedTicks = (long)Math.Round((double)movedTime.Ticks / snapInterval.Ticks) * snapInterval.Ticks;
                    TimeSpan diff = TimeSpan.FromTicks(roundedTicks);

                    // 4. 丸められた時間を元の位置に足し合わせる
                    if (bar.IsDragging && !_isResizingLeft && !_isResizingRight)
                    {
                        bar.Start = bar.OriginalStart.Value.Add(diff);
                        bar.End = bar.OriginalEnd.Value.Add(diff);
                    }
                    else if (_isResizingLeft)
                    {
                        DateTime newStart = bar.OriginalStart.Value.Add(diff);
                        if (newStart < bar.End.Value) bar.Start = newStart;
                    }
                    else if (_isResizingRight)
                    {
                        DateTime newEnd = bar.OriginalEnd.Value.Add(diff);
                        if (newEnd > bar.Start.Value) bar.End = newEnd;
                    }
                }
            }
        }

        private void Bar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_dragTarget is BarItem bar)
            {
                bar.Release();
                _isResizingLeft = false;
                _isResizingRight = false;

                ((FrameworkElement)sender).ReleaseMouseCapture();
                _dragTarget = null;

                if (this.DataContext is MainViewModel vm)
                {
                    vm.SuspendUpdates = false;
                    vm.UpdateAll();
                }
            }
        }

        private void ChartBackground_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) 
        { 
            ((MainViewModel)this.DataContext).ClearSelection(); 
        }

        private void Note_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement el && el.DataContext is NoteItem note)
            {
                _isDraggingNote = true; _dragTarget = note; _dragStartPos = e.GetPosition(null); _origX = note.X; _origY = note.Y; el.CaptureMouse(); e.Handled = true;
            }
        }

        private void Note_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingNote && _dragTarget is NoteItem note)
            {
                var cur = e.GetPosition(null);
                note.X = _origX + (cur.X - _dragStartPos.X);
                note.Y = _origY + (cur.Y - _dragStartPos.Y);
            }
        }

        private void Note_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) { if (_isDraggingNote) { _isDraggingNote = false; ((FrameworkElement)sender).ReleaseMouseCapture(); } }

        private void NoteResize_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement el && el.DataContext is NoteItem note)
            {
                _isResizingNote = true; _dragTarget = note; _dragStartPos = e.GetPosition(null); 
                _origWidth = note.Width; _origHeight = note.Height; 
                el.CaptureMouse(); e.Handled = true;
            }
        }

        private void NoteResize_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isResizingNote && _dragTarget is NoteItem note)
            {
                var cur = e.GetPosition(null);
                note.Width = Math.Max(80, _origWidth + (cur.X - _dragStartPos.X));
                note.Height = Math.Max(40, _origHeight + (cur.Y - _dragStartPos.Y));
            }
        }

        private void NoteResize_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) { if (_isResizingNote) { _isResizingNote = false; ((FrameworkElement)sender).ReleaseMouseCapture(); } }

        private void AddNote_Click(object sender, RoutedEventArgs e) { ((MainViewModel)this.DataContext).AddNewNote(); }
        private void DeleteNote_Click(object sender, RoutedEventArgs e) { if (sender is MenuItem mi && mi.DataContext is NoteItem note) ((MainViewModel)this.DataContext).DeleteNote(note); }
        
        private void AddTask_Click(object sender, RoutedEventArgs e) { ((MainViewModel)this.DataContext).AddNewTask(); }
        private void Indent_Click(object sender, RoutedEventArgs e) { if (MainDataGrid.SelectedItem is TaskItem t) t.IndentLevel++; }
        private void Outdent_Click(object sender, RoutedEventArgs e) { if (MainDataGrid.SelectedItem is TaskItem t && t.IndentLevel > 0) t.IndentLevel--; }
        private void InsertAbove_Click(object sender, RoutedEventArgs e) { if (MainDataGrid.SelectedItem is TaskItem t) ((MainViewModel)this.DataContext).InsertTaskAbove(t); }
        private void InsertBelow_Click(object sender, RoutedEventArgs e) { if (MainDataGrid.SelectedItem is TaskItem t) ((MainViewModel)this.DataContext).InsertTaskBelow(t); }
        private void DeleteRow_Click(object sender, RoutedEventArgs e) { if (MainDataGrid.SelectedItem is TaskItem t) ((MainViewModel)this.DataContext).DeleteTask(t); }

        private void MoveUp_Click(object sender, RoutedEventArgs e) { if (MainDataGrid.SelectedItem is TaskItem t) ((MainViewModel)this.DataContext).MoveTaskUp(t); }
        private void MoveDown_Click(object sender, RoutedEventArgs e) { if (MainDataGrid.SelectedItem is TaskItem t) ((MainViewModel)this.DataContext).MoveTaskDown(t); }

        private void Save_Click(object sender, RoutedEventArgs e) 
        { 
            if (this.DataContext is MainViewModel vm) 
            {
                if (string.IsNullOrEmpty(vm.CurrentFilePath)) SaveAs_Click(sender, e);
                else { vm.SaveToFile(vm.CurrentFilePath); MessageBox.Show("上書き保存しました。", "保存完了"); }
            } 
        }

        private void SaveAs_Click(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is MainViewModel vm)
            {
                var dialog = new Microsoft.Win32.SaveFileDialog();
                dialog.Filter = "JSONファイル (*.json)|*.json|すべてのファイル (*.*)|*.*";
                dialog.DefaultExt = ".json";
                dialog.FileName = string.IsNullOrEmpty(vm.CurrentFilePath) ? "tasks.json" : System.IO.Path.GetFileName(vm.CurrentFilePath);

                if (dialog.ShowDialog() == true) { vm.SaveToFile(dialog.FileName); MessageBox.Show("保存しました。", "保存完了"); }
            }
        }

        private void Load_Click(object sender, RoutedEventArgs e) 
        { 
            if (this.DataContext is MainViewModel vm) 
            {
                var dialog = new Microsoft.Win32.OpenFileDialog();
                dialog.Filter = "JSONファイル (*.json)|*.json|すべてのファイル (*.*)|*.*";
                if (dialog.ShowDialog() == true) { vm.LoadFromFile(dialog.FileName); }
            } 
        }

        private static T? GetVisualChild<T>(DependencyObject parent) where T : Visual
        {
            T? child = default;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                Visual v = (Visual)VisualTreeHelper.GetChild(parent, i);
                child = v as T ?? GetVisualChild<T>(v);
                if (child != null) break;
            }
            return child;
        }
    }
}