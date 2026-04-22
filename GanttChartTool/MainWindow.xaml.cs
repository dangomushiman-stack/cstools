using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GanttChartTool
{
    public partial class MainWindow : Window
    {
        private bool _isDraggingTask = false;
        private bool _isResizingTaskLeft = false;
        private bool _isResizingTaskRight = false;
        
        private bool _isDraggingNote = false;
        private bool _isResizingNote = false;
        
        // --- 追加：クラスの先頭付近の変数宣言に追加 ---
        private bool _isDraggingSubTask = false;
        private bool _isResizingSubTaskLeft = false;
        private bool _isResizingSubTaskRight = false;
        private DateTime? _origSubTaskStart, _origSubTaskEnd;

        private Point _dragStartPos;
        private object? _dragTarget = null;
        private DateTime _origStart, _origEnd;
        
        private int _origWorkDays;

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

        private void TaskBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement el && el.DataContext is TaskItem task)
            {
                var vm = (MainViewModel)this.DataContext;
                vm.OnTaskBarClicked(task);
                if (!vm.IsLinkMode) 
                { 
                    _isDraggingTask = true; 
                    _dragTarget = task; 
                    _dragStartPos = e.GetPosition(null); 
                    _origStart = task.Start; 
                    _origEnd = task.End; 
                    
                    _origWorkDays = task.WorkDays;
                    
                    task.OriginalStart = task.Start;
                    task.OriginalEnd = task.End;
                    task.IsDragging = true;

                    vm.SuspendUpdates = true; 
                    el.CaptureMouse(); 
                }
                e.Handled = true;
            }
        }

        private void TaskBarResizeLeft_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement el && el.DataContext is TaskItem task)
            {
                var vm = (MainViewModel)this.DataContext;
                vm.OnTaskBarClicked(task);
                if (!vm.IsLinkMode)
                {
                    _isResizingTaskLeft = true;
                    _dragTarget = task;
                    _dragStartPos = e.GetPosition(null);
                    _origStart = task.Start;
                    _origEnd = task.End;
                    
                    task.OriginalStart = task.Start;
                    task.OriginalEnd = task.End;
                    task.IsDragging = true;

                    vm.SuspendUpdates = true;
                    el.CaptureMouse();
                    e.Handled = true;
                }
            }
        }

        private void TaskBarResizeRight_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement el && el.DataContext is TaskItem task)
            {
                var vm = (MainViewModel)this.DataContext;
                vm.OnTaskBarClicked(task);
                if (!vm.IsLinkMode)
                {
                    _isResizingTaskRight = true;
                    _dragTarget = task;
                    _dragStartPos = e.GetPosition(null);
                    _origStart = task.Start;
                    _origEnd = task.End;
                    
                    task.OriginalStart = task.Start;
                    task.OriginalEnd = task.End;
                    task.IsDragging = true;

                    vm.SuspendUpdates = true;
                    el.CaptureMouse();
                    e.Handled = true;
                }
            }
        }

        private void TaskBar_MouseMove(object sender, MouseEventArgs e)
        {
            if (_dragTarget is TaskItem task && this.DataContext is MainViewModel vm)
            {
                int units = (int)Math.Round((e.GetPosition(null).X - _dragStartPos.X) / GanttSettings.DayWidth);
                
                if (_isDraggingTask)
                {
                    if (vm.IsHourlyMode)
                    {
                        task.Start = _origStart.AddHours(units);
                        task.End = _origEnd.AddHours(units);
                    }
                    else
                    {
                        task.Start = _origStart.AddDays(units); 
                        if (vm.IsWorkDayAdjustmentEnabled) task.WorkDays = _origWorkDays; 
                        else task.End = _origEnd.AddDays(units); 
                    }
                }
                else if (_isResizingTaskLeft)
                {
                    DateTime newStart = vm.IsHourlyMode ? _origStart.AddHours(units) : _origStart.AddDays(units);
                    if (newStart < task.End) task.Start = newStart;
                }
                else if (_isResizingTaskRight)
                {
                    DateTime newEnd = vm.IsHourlyMode ? _origEnd.AddHours(units) : _origEnd.AddDays(units);
                    if (newEnd > task.Start) task.End = newEnd;
                }
            }
        }

        private void TaskBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) 
        { 
            if (_isDraggingTask || _isResizingTaskLeft || _isResizingTaskRight) 
            { 
                _isDraggingTask = false; 
                _isResizingTaskLeft = false;
                _isResizingTaskRight = false;
                
                ((FrameworkElement)sender).ReleaseMouseCapture(); 
                
                if (_dragTarget is TaskItem task)
                {
                    task.IsDragging = false;
                }

                if (this.DataContext is MainViewModel vm)
                {
                    vm.SuspendUpdates = false; 
                    vm.UpdateAll(); 
                }
            } 
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
        private void ChartBackground_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { ((MainViewModel)this.DataContext).ClearSelection(); }
        
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


        // --- 追加：任意の場所に以下のメソッド群を追加 ---
        private void SubTaskBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement el && el.DataContext is TaskItem task && task.SubTaskStart.HasValue && task.SubTaskEnd.HasValue)
            {
                var vm = (MainViewModel)this.DataContext;
                vm.OnTaskBarClicked(task);
                if (!vm.IsLinkMode)
                {
                    _isDraggingSubTask = true;
                    _dragTarget = task;
                    _dragStartPos = e.GetPosition(null);
                    _origSubTaskStart = task.SubTaskStart;
                    _origSubTaskEnd = task.SubTaskEnd;

                    vm.SuspendUpdates = true;
                    el.CaptureMouse();
                    e.Handled = true;
                }
            }
        }

        private void SubTaskBarResizeLeft_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement el && el.DataContext is TaskItem task && task.SubTaskStart.HasValue && task.SubTaskEnd.HasValue)
            {
                var vm = (MainViewModel)this.DataContext;
                vm.OnTaskBarClicked(task);
                if (!vm.IsLinkMode)
                {
                    _isResizingSubTaskLeft = true;
                    _dragTarget = task;
                    _dragStartPos = e.GetPosition(null);
                    _origSubTaskStart = task.SubTaskStart;
                    _origSubTaskEnd = task.SubTaskEnd;

                    vm.SuspendUpdates = true;
                    el.CaptureMouse();
                    e.Handled = true;
                }
            }
        }

        private void SubTaskBarResizeRight_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement el && el.DataContext is TaskItem task && task.SubTaskStart.HasValue && task.SubTaskEnd.HasValue)
            {
                var vm = (MainViewModel)this.DataContext;
                vm.OnTaskBarClicked(task);
                if (!vm.IsLinkMode)
                {
                    _isResizingSubTaskRight = true;
                    _dragTarget = task;
                    _dragStartPos = e.GetPosition(null);
                    _origSubTaskStart = task.SubTaskStart;
                    _origSubTaskEnd = task.SubTaskEnd;

                    vm.SuspendUpdates = true;
                    el.CaptureMouse();
                    e.Handled = true;
                }
            }
        }

        private void SubTaskBar_MouseMove(object sender, MouseEventArgs e)
        {
            if (_dragTarget is TaskItem task && this.DataContext is MainViewModel vm && _origSubTaskStart.HasValue && _origSubTaskEnd.HasValue)
            {
                if (_isDraggingSubTask || _isResizingSubTaskLeft || _isResizingSubTaskRight)
                {
                    int units = (int)Math.Round((e.GetPosition(null).X - _dragStartPos.X) / GanttSettings.DayWidth);

                    if (_isDraggingSubTask)
                    {
                        if (vm.IsHourlyMode)
                        {
                            task.SubTaskStart = _origSubTaskStart.Value.AddHours(units);
                            task.SubTaskEnd = _origSubTaskEnd.Value.AddHours(units);
                        }
                        else
                        {
                            task.SubTaskStart = _origSubTaskStart.Value.AddDays(units);
                            task.SubTaskEnd = _origSubTaskEnd.Value.AddDays(units);
                        }
                    }
                    else if (_isResizingSubTaskLeft)
                    {
                        DateTime newStart = vm.IsHourlyMode ? _origSubTaskStart.Value.AddHours(units) : _origSubTaskStart.Value.AddDays(units);
                        if (newStart < task.SubTaskEnd.Value) task.SubTaskStart = newStart;
                    }
                    else if (_isResizingSubTaskRight)
                    {
                        DateTime newEnd = vm.IsHourlyMode ? _origSubTaskEnd.Value.AddHours(units) : _origSubTaskEnd.Value.AddDays(units);
                        if (newEnd > task.SubTaskStart.Value) task.SubTaskEnd = newEnd;
                    }
                }
            }
        }

        private void SubTaskBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingSubTask || _isResizingSubTaskLeft || _isResizingSubTaskRight)
            {
                _isDraggingSubTask = false;
                _isResizingSubTaskLeft = false;
                _isResizingSubTaskRight = false;

                ((FrameworkElement)sender).ReleaseMouseCapture();

                if (this.DataContext is MainViewModel vm)
                {
                    vm.SuspendUpdates = false;
                    vm.UpdateAll();
                }
            }
        }


    }
}