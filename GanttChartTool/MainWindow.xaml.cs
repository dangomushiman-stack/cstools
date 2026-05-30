using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GanttChartTool
{
    public partial class MainWindow : Window
    {
        private bool _isResizingLeft = false, _isResizingRight = false, _isDraggingNote = false, _isResizingNote = false, _isDraggingNoteTarget = false, _isSync = false, _isPanningChart = false;
        private Point _dragStartPos; private object? _dragTarget = null;
        private Point _panStartPos;
        private FrameworkElement? _panCaptureElement = null;
        private double _origX, _origY, _origWidth, _origHeight, _panStartHorizontalOffset, _panStartVerticalOffset;
        private DateTime _origTime, _origTargetTime; 
        private ScrollViewer? _dgScroll;

        public MainWindow() 
        { 
            InitializeComponent(); 
            this.DataContext = new MainViewModel(); 
            // ★追加：ウィンドウを閉じる時にチェック処理を挟む
            this.Closing += Window_Closing;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) 
        { 
            _dgScroll = GetVisualChild<ScrollViewer>(MainDataGrid); 
            if (_dgScroll != null) 
            {
                _dgScroll.ScrollChanged += (s, ev) => 
                { 
                    if (!_isSync && Math.Abs(GanttScrollViewer.VerticalOffset - ev.VerticalOffset) > 0.1) 
                    { 
                        _isSync = true; 
                        GanttScrollViewer.ScrollToVerticalOffset(ev.VerticalOffset); 
                        _isSync = false; 
                    } 
                }; 
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl+S は Window.InputBindings / CommandBindings 側で処理します。
            // PreviewKeyDown では処理しないことで、TextBox や DataGrid にフォーカスがある場合も
            // WPF のコマンド経由で安定して保存できるようにします。
        }

        private void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Save_Click(sender, new RoutedEventArgs());
            e.Handled = true;
        }
        private void GanttScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e) 
        { 
            // 日付ヘッダーは縦スクロールさせず、横スクロールだけ本文と同期する。
            if (Math.Abs(GanttHeaderScrollViewer.HorizontalOffset - e.HorizontalOffset) > 0.1)
                GanttHeaderScrollViewer.ScrollToHorizontalOffset(e.HorizontalOffset);

            // 左側の表とは縦スクロールだけ同期する。
            if (!_isSync && _dgScroll != null && Math.Abs(_dgScroll.VerticalOffset - e.VerticalOffset) > 0.1) 
            { 
                _isSync = true; 
                _dgScroll.ScrollToVerticalOffset(e.VerticalOffset); 
                _isSync = false; 
            } 
        }

        // 未保存の変更がある場合、続行前に保存確認する
        private bool ConfirmSaveIfNeeded(string message)
        {
            var vm = (MainViewModel)this.DataContext;
            if (!vm.HasUnsavedChanges) return true;

            var result = MessageBox.Show(
                message,
                "保存の確認",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                Save_Click(this, new RoutedEventArgs());
                return !vm.HasUnsavedChanges;
            }

            return result == MessageBoxResult.No;
        }

        // ★追加：ウィンドウを閉じる（終了する）ときの確認
        private void Window_Closing(object? sender, CancelEventArgs e)
        {
            if (!ConfirmSaveIfNeeded("変更が保存されていません。保存して終了しますか？\n（[いいえ] を選ぶと変更は破棄されます）"))
                e.Cancel = true;
        }

        private void Bar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement el && el.DataContext is TaskItem task)
            {
                var vm = (MainViewModel)this.DataContext; vm.OnTaskBarClicked(task);
                BarItem bar = (el.Tag as string == "SubBar") ? task.SubBar : task.MainBar;
                if (bar != null && bar.Start.HasValue && bar.End.HasValue && !vm.IsLinkMode) { bar.Snapshot(); _dragTarget = bar; _dragStartPos = e.GetPosition(null); vm.SuspendUpdates = true; el.CaptureMouse(); }
                e.Handled = true;
            }
        }

        private void BarResizeLeft_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { HandleResize(sender, e, true); }
        private void BarResizeRight_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { HandleResize(sender, e, false); }
        private void HandleResize(object sender, MouseButtonEventArgs e, bool isLeft)
        {
            if (sender is FrameworkElement el && el.DataContext is TaskItem task)
            {
                var vm = (MainViewModel)this.DataContext; vm.OnTaskBarClicked(task);
                BarItem bar = (el.Tag as string == "SubBar") ? task.SubBar : task.MainBar;
                if (bar != null && bar.Start.HasValue && bar.End.HasValue && !vm.IsLinkMode) { if (isLeft) _isResizingLeft = true; else _isResizingRight = true; bar.Snapshot(); _dragTarget = bar; _dragStartPos = e.GetPosition(null); vm.SuspendUpdates = true; el.CaptureMouse(); }
                e.Handled = true;
            }
        }

        private void Bar_MouseMove(object sender, MouseEventArgs e)
        {
            if (_dragTarget is BarItem bar && this.DataContext is MainViewModel vm && bar.OriginalStart.HasValue && bar.OriginalEnd.HasValue)
            {
                double dx = e.GetPosition(null).X - _dragStartPos.X;
                TimeSpan movedTime = TimeSpan.FromTicks((long)(vm.SelectedInterval.TimeSpan.Ticks * (dx / GanttSettings.DayWidth)));
                TimeSpan snapInterval = vm.SelectedSnapTimeSpan;
                if (snapInterval.Ticks <= 0) snapInterval = vm.SelectedInterval.TimeSpan;
                TimeSpan diff = TimeSpan.FromTicks((long)Math.Round((double)movedTime.Ticks / snapInterval.Ticks) * snapInterval.Ticks);
                if (bar.IsDragging && !_isResizingLeft && !_isResizingRight) { bar.Start = bar.OriginalStart.Value.Add(diff); bar.End = bar.OriginalEnd.Value.Add(diff); }
                else if (_isResizingLeft) { DateTime next = bar.OriginalStart.Value.Add(diff); if (next < bar.End.Value) bar.Start = next; }
                else if (_isResizingRight) { DateTime next = bar.OriginalEnd.Value.Add(diff); if (next > bar.Start.Value) bar.End = next; }
            }
        }

        private void Bar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) { if (_dragTarget is BarItem bar) { bar.Release(); _isResizingLeft = _isResizingRight = false; ((FrameworkElement)sender).ReleaseMouseCapture(); _dragTarget = null; if (this.DataContext is MainViewModel vm) { vm.SuspendUpdates = false; vm.UpdateAll(); } } }

        private void ChartBackground_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is Canvas || e.OriginalSource is Grid || e.OriginalSource is ScrollViewer || e.OriginalSource is Border)
            {
                Keyboard.ClearFocus();
                ((MainViewModel)this.DataContext).ClearSelection();

                _isPanningChart = true;
                _panStartPos = e.GetPosition(this);
                _panStartHorizontalOffset = GanttScrollViewer.HorizontalOffset;
                _panStartVerticalOffset = GanttScrollViewer.VerticalOffset;

                // ScrollViewer自身をCaptureすると、MouseMove/MouseUpが背景Gridへ戻らず、
                // ドラッグ状態のまま固まったように見える場合があるため、
                // 実際にイベントを受けている背景要素をCaptureする。
                _panCaptureElement = sender as FrameworkElement;
                _panCaptureElement?.CaptureMouse();
                if (_panCaptureElement != null) _panCaptureElement.Cursor = Cursors.SizeAll;

                e.Handled = true;
            }
        }

        private void ChartBackground_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isPanningChart) return;

            // マウスボタンが離れたのにMouseUpを取り逃がした場合の保険。
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                EndChartPan();
                return;
            }

            var current = e.GetPosition(this);
            double dx = current.X - _panStartPos.X;
            double dy = current.Y - _panStartPos.Y;

            double nextX = Math.Max(0, _panStartHorizontalOffset - dx);
            double nextY = Math.Max(0, _panStartVerticalOffset - dy);

            if (Math.Abs(GanttScrollViewer.HorizontalOffset - nextX) > 0.1)
                GanttScrollViewer.ScrollToHorizontalOffset(nextX);
            if (Math.Abs(GanttScrollViewer.VerticalOffset - nextY) > 0.1)
                GanttScrollViewer.ScrollToVerticalOffset(nextY);

            e.Handled = true;
        }

        private void ChartBackground_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isPanningChart) return;

            EndChartPan();
            e.Handled = true;
        }

        private void EndChartPan()
        {
            _isPanningChart = false;
            if (_panCaptureElement != null)
            {
                _panCaptureElement.Cursor = Cursors.Arrow;
                if (_panCaptureElement.IsMouseCaptured) _panCaptureElement.ReleaseMouseCapture();
            }
            _panCaptureElement = null;
        }

        private void NoteTextBox_GotFocus(object sender, RoutedEventArgs e) { if (sender is TextBox tb && tb.DataContext is NoteItem note) ((MainViewModel)this.DataContext).SelectNote(note); }
        private void NoteTail_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (sender is FrameworkElement el && el.DataContext is NoteItem note) { ((MainViewModel)this.DataContext).SelectNote(note); e.Handled = true; } }
        private void NoteBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (sender is FrameworkElement el && el.DataContext is NoteItem note) { if (!(e.OriginalSource is TextBox)) { ((MainViewModel)this.DataContext).SelectNote(note); e.Handled = true; } } }
        
        private void Note_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) 
        { 
            if (sender is FrameworkElement el && el.DataContext is NoteItem note) 
            { 
                ((MainViewModel)this.DataContext).SelectNote(note); 
                _isDraggingNote = true; _dragTarget = note; _dragStartPos = e.GetPosition(null); 
                _origTime = note.TimePosition;
                _origY = note.Y; 
                el.CaptureMouse(); e.Handled = true; 
            } 
        }
        
        private void Note_MouseMove(object sender, MouseEventArgs e) 
        { 
            if (_isDraggingNote && _dragTarget is NoteItem note && this.DataContext is MainViewModel vm) 
            { 
                var cur = e.GetPosition(null);
                double dx = cur.X - _dragStartPos.X;
                TimeSpan diff = TimeSpan.FromTicks((long)(vm.SelectedInterval.TimeSpan.Ticks * (dx / GanttSettings.DayWidth)));
                
                note.TimePosition = _origTime.Add(diff);
                note.Y = _origY + (cur.Y - _dragStartPos.Y); 
            } 
        }

        private void Note_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) { if (_isDraggingNote) { _isDraggingNote = false; ((FrameworkElement)sender).ReleaseMouseCapture(); _dragTarget = null; } }
        
        private void NoteResize_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (sender is FrameworkElement el && el.DataContext is NoteItem note) { ((MainViewModel)this.DataContext).SelectNote(note); _isResizingNote = true; _dragTarget = note; _dragStartPos = e.GetPosition(null); _origWidth = note.Width; _origHeight = note.Height; el.CaptureMouse(); e.Handled = true; } }
        private void NoteResize_MouseMove(object sender, MouseEventArgs e) { if (_isResizingNote && _dragTarget is NoteItem note) { var cur = e.GetPosition(null); note.Width = Math.Max(80, _origWidth + (cur.X - _dragStartPos.X)); note.Height = Math.Max(40, _origHeight + (cur.Y - _dragStartPos.Y)); } }
        private void NoteResize_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) { if (_isResizingNote) { _isResizingNote = false; ((FrameworkElement)sender).ReleaseMouseCapture(); _dragTarget = null; } }
        
        private void NoteTarget_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) 
        { 
            if (sender is FrameworkElement el && el.DataContext is NoteItem note) 
            { 
                ((MainViewModel)this.DataContext).SelectNote(note); 
                _isDraggingNoteTarget = true; _dragTarget = note; _dragStartPos = e.GetPosition(null); 
                _origTargetTime = note.TargetTimePosition; 
                _origY = note.TargetY; 
                el.CaptureMouse(); e.Handled = true; 
            } 
        }
        
        private void NoteTarget_MouseMove(object sender, MouseEventArgs e) 
        { 
            if (_isDraggingNoteTarget && _dragTarget is NoteItem note && this.DataContext is MainViewModel vm) 
            { 
                var cur = e.GetPosition(null);
                double dx = cur.X - _dragStartPos.X;
                TimeSpan diff = TimeSpan.FromTicks((long)(vm.SelectedInterval.TimeSpan.Ticks * (dx / GanttSettings.DayWidth)));
                
                note.TargetTimePosition = _origTargetTime.Add(diff); 
                note.TargetY = _origY + (cur.Y - _dragStartPos.Y); 
            } 
        }

        private void NoteTarget_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) { if (_isDraggingNoteTarget) { _isDraggingNoteTarget = false; ((FrameworkElement)sender).ReleaseMouseCapture(); _dragTarget = null; } }

        private void ToggleCallout_Click(object sender, RoutedEventArgs e) 
        { 
            if (sender is MenuItem mi && mi.DataContext is NoteItem note) 
            { 
                ((MainViewModel)this.DataContext).SelectNote(note); 
                note.IsCallout = !note.IsCallout; 
                if (note.IsCallout) 
                { 
                    note.TargetTimePosition = note.TimePosition.AddDays(1); 
                    note.TargetY = note.Y + note.Height + 30; 
                    note.RefreshTail(); 
                } 
            } 
        }

        private void AddNote_Click(object sender, RoutedEventArgs e) { ((MainViewModel)this.DataContext).AddNewNote(); }
        private void AddCallout_Click(object sender, RoutedEventArgs e) { if (this.DataContext is MainViewModel vm) { var note = new NoteItem { Text = "吹き出しメモ", TimePosition = vm.ProjectStartDate.AddDays(1), TargetTimePosition = vm.ProjectStartDate.AddDays(2), TargetY = 200, IsCallout = true }; note.GetProjectStart = () => vm.ProjectStartDate; note.GetGridInterval = () => vm.SelectedInterval.TimeSpan; note.RefreshTail(); vm.Notes.Add(note); vm.SelectNote(note); } }
        private void DeleteNote_Click(object sender, RoutedEventArgs e) { if (sender is MenuItem mi && mi.DataContext is NoteItem note) ((MainViewModel)this.DataContext).DeleteNote(note); }
        private void AddTask_Click(object sender, RoutedEventArgs e) { ((MainViewModel)this.DataContext).AddNewTask(); }
        private void InsertAbove_Click(object sender, RoutedEventArgs e) { if (MainDataGrid.SelectedItem is TaskItem t) ((MainViewModel)this.DataContext).InsertTaskAbove(t); }
        private void InsertBelow_Click(object sender, RoutedEventArgs e) { if (MainDataGrid.SelectedItem is TaskItem t) ((MainViewModel)this.DataContext).InsertTaskBelow(t); }
        private void DeleteRow_Click(object sender, RoutedEventArgs e) { if (MainDataGrid.SelectedItem is TaskItem t) ((MainViewModel)this.DataContext).DeleteTask(t); }
        private void MoveUp_Click(object sender, RoutedEventArgs e) { if (MainDataGrid.SelectedItem is TaskItem t) ((MainViewModel)this.DataContext).MoveTaskUp(t); }
        private void MoveDown_Click(object sender, RoutedEventArgs e) { if (MainDataGrid.SelectedItem is TaskItem t) ((MainViewModel)this.DataContext).MoveTaskDown(t); }
        private void Indent_Click(object sender, RoutedEventArgs e) { if (MainDataGrid.SelectedItem is TaskItem t) t.IndentLevel++; }
        private void Outdent_Click(object sender, RoutedEventArgs e) { if (MainDataGrid.SelectedItem is TaskItem t && t.IndentLevel > 0) t.IndentLevel--; }

        private void NewProject_Click(object sender, RoutedEventArgs e)
        {
            if (!ConfirmSaveIfNeeded("現在のプロジェクトに変更があります。新規作成する前に保存しますか？\n（[いいえ] を選ぶと変更は破棄されます）")) return;

            DataContext = new MainViewModel();
        }

        private void NewWindow_Click(object sender, RoutedEventArgs e)
        {
            var window = new MainWindow();
            window.Show();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var vm = (MainViewModel)this.DataContext;
            if (string.IsNullOrEmpty(vm.CurrentFilePath))
            {
                SaveAs_Click(sender, e);
                return;
            }

            vm.SaveToFile(vm.CurrentFilePath);
        }

        private void SaveAs_Click(object sender, RoutedEventArgs e)
        {
            var vm = (MainViewModel)this.DataContext;
            string fileName = string.IsNullOrEmpty(vm.CurrentFilePath)
                ? "tasks.gntj"
                : System.IO.Path.ChangeExtension(System.IO.Path.GetFileName(vm.CurrentFilePath), ".gntj");

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Gantt Project (*.gntj)|*.gntj|JSON (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = ".gntj",
                AddExtension = true,
                FileName = fileName
            };

            if (dlg.ShowDialog() == true)
            {
                vm.SaveToFile(dlg.FileName);
                MessageBox.Show("保存しました。");
            }
        }
        
        // ★変更：読み込み前に未保存の変更がないかチェック
        private void Load_Click(object sender, RoutedEventArgs e) 
        { 
            if (!ConfirmSaveIfNeeded("現在のプロジェクトに変更があります。読み込む前に保存しますか？")) return;

            var vm = (MainViewModel)this.DataContext;
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Gantt Project (*.gntj)|*.gntj|JSON (*.json)|*.json|All files (*.*)|*.*", DefaultExt = ".gntj" }; 
            if (dlg.ShowDialog() == true) vm.LoadFromFile(dlg.FileName); 
        }

        private void MainDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.C && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                if (e.OriginalSource is TextBox || e.OriginalSource is ComboBox) return;
                if (MainDataGrid.SelectedItems.Count > 0 && this.DataContext is MainViewModel vm)
                {
                    var sb = new System.Text.StringBuilder(); sb.AppendLine("ID\tタスク名\t担当者\t色\t%\t営業日\t開始\t終了\t後作業名\t後開始\t後終了\t後色\t先行");
                    foreach (var item in MainDataGrid.SelectedItems) { if (item is TaskItem t) { string fmt = vm.IsHourlyMode ? "MM/dd HH:mm" : "MM/dd"; sb.AppendLine($"{t.Id}\t{new string('　', t.IndentLevel) + t.Name}\t{t.Assignee}\t{t.MainBar.ColorName}\t{t.Progress}\t{t.WorkDays}\t{t.MainBar.Start?.ToString(fmt)}\t{t.MainBar.End?.ToString(fmt)}\t{t.SubBar.Name}\t{t.SubBar.Start?.ToString(fmt)}\t{t.SubBar.End?.ToString(fmt)}\t{t.SubBar.ColorName}\t{t.PredecessorId}"); } }
                    Clipboard.SetText(sb.ToString()); e.Handled = true;
                }
            }
        }

        private static T? GetVisualChild<T>(DependencyObject parent) where T : Visual { for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) { Visual v = (Visual)VisualTreeHelper.GetChild(parent, i); T? child = v as T ?? GetVisualChild<T>(v); if (child != null) return child; } return null; }
    }
}