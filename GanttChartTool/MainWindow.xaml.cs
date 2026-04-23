using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GanttChartTool
{
    public partial class MainWindow : Window
    {
        private bool _isResizingLeft = false, _isResizingRight = false, _isDraggingNote = false, _isResizingNote = false, _isDraggingNoteTarget = false, _isSync = false;
        private Point _dragStartPos; private object? _dragTarget = null;
        private double _origX, _origY, _origWidth, _origHeight;
        private ScrollViewer? _dgScroll;

        public MainWindow() { InitializeComponent(); this.DataContext = new MainViewModel(); }
        private void Window_Loaded(object sender, RoutedEventArgs e) { _dgScroll = GetVisualChild<ScrollViewer>(MainDataGrid); if (_dgScroll != null) _dgScroll.ScrollChanged += (s, ev) => { if (!_isSync) { _isSync = true; GanttScrollViewer.ScrollToVerticalOffset(ev.VerticalOffset); _isSync = false; } }; }
        private void GanttScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e) { if (!_isSync && _dgScroll != null) { _isSync = true; _dgScroll.ScrollToVerticalOffset(e.VerticalOffset); _isSync = false; } }

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
                TimeSpan snapInterval = vm.IsSnapToDay ? TimeSpan.FromDays(1) : vm.SelectedInterval.TimeSpan;
                TimeSpan diff = TimeSpan.FromTicks((long)Math.Round((double)movedTime.Ticks / snapInterval.Ticks) * snapInterval.Ticks);
                if (bar.IsDragging && !_isResizingLeft && !_isResizingRight) { bar.Start = bar.OriginalStart.Value.Add(diff); bar.End = bar.OriginalEnd.Value.Add(diff); }
                else if (_isResizingLeft) { DateTime next = bar.OriginalStart.Value.Add(diff); if (next < bar.End.Value) bar.Start = next; }
                else if (_isResizingRight) { DateTime next = bar.OriginalEnd.Value.Add(diff); if (next > bar.Start.Value) bar.End = next; }
            }
        }

        private void Bar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) { if (_dragTarget is BarItem bar) { bar.Release(); _isResizingLeft = _isResizingRight = false; ((FrameworkElement)sender).ReleaseMouseCapture(); _dragTarget = null; if (this.DataContext is MainViewModel vm) { vm.SuspendUpdates = false; vm.UpdateAll(); } } }

        // ★背景クリックで選択解除
        private void ChartBackground_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is Canvas || e.OriginalSource is Grid || e.OriginalSource is ScrollViewer || e.OriginalSource is Border)
            {
                Keyboard.ClearFocus();
                ((MainViewModel)this.DataContext).ClearSelection();
            }
        }

        // --- メモ（Note） ---
        private void NoteTextBox_GotFocus(object sender, RoutedEventArgs e) { if (sender is TextBox tb && tb.DataContext is NoteItem note) ((MainViewModel)this.DataContext).SelectNote(note); }
        private void NoteTail_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (sender is FrameworkElement el && el.DataContext is NoteItem note) { ((MainViewModel)this.DataContext).SelectNote(note); e.Handled = true; } }
        private void NoteBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (sender is FrameworkElement el && el.DataContext is NoteItem note) { if (!(e.OriginalSource is TextBox)) { ((MainViewModel)this.DataContext).SelectNote(note); e.Handled = true; } } }
        private void Note_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (sender is FrameworkElement el && el.DataContext is NoteItem note) { ((MainViewModel)this.DataContext).SelectNote(note); _isDraggingNote = true; _dragTarget = note; _dragStartPos = e.GetPosition(null); _origX = note.X; _origY = note.Y; el.CaptureMouse(); e.Handled = true; } }
        private void Note_MouseMove(object sender, MouseEventArgs e) { if (_isDraggingNote && _dragTarget is NoteItem note) { var cur = e.GetPosition(null); note.X = _origX + (cur.X - _dragStartPos.X); note.Y = _origY + (cur.Y - _dragStartPos.Y); } }
        private void Note_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) { if (_isDraggingNote) { _isDraggingNote = false; ((FrameworkElement)sender).ReleaseMouseCapture(); _dragTarget = null; } }
        private void NoteResize_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (sender is FrameworkElement el && el.DataContext is NoteItem note) { ((MainViewModel)this.DataContext).SelectNote(note); _isResizingNote = true; _dragTarget = note; _dragStartPos = e.GetPosition(null); _origWidth = note.Width; _origHeight = note.Height; el.CaptureMouse(); e.Handled = true; } }
        private void NoteResize_MouseMove(object sender, MouseEventArgs e) { if (_isResizingNote && _dragTarget is NoteItem note) { var cur = e.GetPosition(null); note.Width = Math.Max(80, _origWidth + (cur.X - _dragStartPos.X)); note.Height = Math.Max(40, _origHeight + (cur.Y - _dragStartPos.Y)); } }
        private void NoteResize_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) { if (_isResizingNote) { _isResizingNote = false; ((FrameworkElement)sender).ReleaseMouseCapture(); _dragTarget = null; } }
        private void NoteTarget_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (sender is FrameworkElement el && el.DataContext is NoteItem note) { ((MainViewModel)this.DataContext).SelectNote(note); _isDraggingNoteTarget = true; _dragTarget = note; _dragStartPos = e.GetPosition(null); _origX = note.TargetX; _origY = note.TargetY; el.CaptureMouse(); e.Handled = true; } }
        private void NoteTarget_MouseMove(object sender, MouseEventArgs e) { if (_isDraggingNoteTarget && _dragTarget is NoteItem note) { var cur = e.GetPosition(null); note.TargetX = _origX + (cur.X - _dragStartPos.X); note.TargetY = _origY + (cur.Y - _dragStartPos.Y); } }
        private void NoteTarget_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) { if (_isDraggingNoteTarget) { _isDraggingNoteTarget = false; ((FrameworkElement)sender).ReleaseMouseCapture(); _dragTarget = null; } }

        private void ToggleCallout_Click(object sender, RoutedEventArgs e) { if (sender is MenuItem mi && mi.DataContext is NoteItem note) { ((MainViewModel)this.DataContext).SelectNote(note); note.IsCallout = !note.IsCallout; if (note.IsCallout) { note.TargetX = note.X - 20; note.TargetY = note.Y + note.Height + 30; note.RefreshTail(); } } }
        private void AddNote_Click(object sender, RoutedEventArgs e) { ((MainViewModel)this.DataContext).AddNewNote(); }
        private void AddCallout_Click(object sender, RoutedEventArgs e) { if (this.DataContext is MainViewModel vm) { var note = new NoteItem { Text = "吹き出しメモ", X = 200, Y = 100, IsCallout = true, TargetX = 100, TargetY = 150 }; note.RefreshTail(); vm.Notes.Add(note); vm.SelectNote(note); } }
        private void DeleteNote_Click(object sender, RoutedEventArgs e) { if (sender is MenuItem mi && mi.DataContext is NoteItem note) ((MainViewModel)this.DataContext).DeleteNote(note); }
        private void AddTask_Click(object sender, RoutedEventArgs e) { ((MainViewModel)this.DataContext).AddNewTask(); }
        private void InsertAbove_Click(object sender, RoutedEventArgs e) { if (MainDataGrid.SelectedItem is TaskItem t) ((MainViewModel)this.DataContext).InsertTaskAbove(t); }
        private void InsertBelow_Click(object sender, RoutedEventArgs e) { if (MainDataGrid.SelectedItem is TaskItem t) ((MainViewModel)this.DataContext).InsertTaskBelow(t); }
        private void DeleteRow_Click(object sender, RoutedEventArgs e) { if (MainDataGrid.SelectedItem is TaskItem t) ((MainViewModel)this.DataContext).DeleteTask(t); }
        private void MoveUp_Click(object sender, RoutedEventArgs e) { if (MainDataGrid.SelectedItem is TaskItem t) ((MainViewModel)this.DataContext).MoveTaskUp(t); }
        private void MoveDown_Click(object sender, RoutedEventArgs e) { if (MainDataGrid.SelectedItem is TaskItem t) ((MainViewModel)this.DataContext).MoveTaskDown(t); }
        private void Indent_Click(object sender, RoutedEventArgs e) { if (MainDataGrid.SelectedItem is TaskItem t) t.IndentLevel++; }
        private void Outdent_Click(object sender, RoutedEventArgs e) { if (MainDataGrid.SelectedItem is TaskItem t && t.IndentLevel > 0) t.IndentLevel--; }

        private void Save_Click(object sender, RoutedEventArgs e) { var vm = (MainViewModel)this.DataContext; if (string.IsNullOrEmpty(vm.CurrentFilePath)) SaveAs_Click(sender, e); else { vm.SaveToFile(vm.CurrentFilePath); MessageBox.Show("保存しました。"); } }
        private void SaveAs_Click(object sender, RoutedEventArgs e) { var vm = (MainViewModel)this.DataContext; var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "JSON|*.json", FileName = "tasks.json" }; if (dlg.ShowDialog() == true) { vm.SaveToFile(dlg.FileName); MessageBox.Show("保存しました。"); } }
        private void Load_Click(object sender, RoutedEventArgs e) { var vm = (MainViewModel)this.DataContext; var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "JSON|*.json" }; if (dlg.ShowDialog() == true) vm.LoadFromFile(dlg.FileName); }

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