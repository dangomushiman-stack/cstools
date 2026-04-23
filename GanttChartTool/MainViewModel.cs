using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Media;

namespace GanttChartTool
{
    public static class GanttSettings
    {
        public const double DayWidth = 40;
        public const double RowHeight = 40;
    }

    public static class GanttLogic
    {
        public static int CalculateWorkDays(DateTime start, DateTime end)
        {
            if (start > end) return 0;
            int days = 0;
            DateTime current = start;
            while (current.Date < end.Date)
            {
                if (current.DayOfWeek != DayOfWeek.Saturday && current.DayOfWeek != DayOfWeek.Sunday)
                    days++;
                current = current.AddDays(1);
            }
            return days;
        }

        public static DateTime AddWorkDays(DateTime start, int workDays)
        {
            DateTime result = start;
            while (workDays > 0)
            {
                result = result.AddDays(1);
                if (result.DayOfWeek != DayOfWeek.Saturday && result.DayOfWeek != DayOfWeek.Sunday)
                    workDays--;
            }
            return result;
        }
    }

    public class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class StringConverter : JsonConverter<string>
    {
        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number) return reader.TryGetInt32(out int i) ? i.ToString() : reader.GetDouble().ToString();
            return reader.GetString() ?? "";
        }
        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) => writer.WriteStringValue(value);
    }

    public class NoteItem : ViewModelBase
    {
        public NoteItem() { }
        private string _text = "新しいメモ";
        public string Text { get => _text; set { _text = value; OnPropertyChanged(); } }
        private double _x = 100;
        public double X { get => _x; set { _x = value; OnPropertyChanged(); } }
        private double _y = 50;
        public double Y { get => _y; set { _y = value; OnPropertyChanged(); } }
        private double _width = 150;
        public double Width { get => _width; set { _width = value; OnPropertyChanged(); } }
        private double _height = 80;
        public double Height { get => _height; set { _height = value; OnPropertyChanged(); } }
    }

    public class BarItem : ViewModelBase
    {
        private DateTime? _start, _end, _origStart, _origEnd;
        private string _name = "";
        private string _colorName = "SteelBlue";
        private bool _isDragging;

        [JsonIgnore] public Func<DateTime>? GetProjectStart;
        [JsonIgnore] public Func<bool>? GetIsHourlyMode;
        [JsonIgnore] public Func<bool>? GetIsSelected; // ★親が選択中か知るための連絡網を追加
        [JsonIgnore] public Action? OnChanged;

        public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
        public string ColorName { get => _colorName; set { _colorName = value; OnPropertyChanged(); OnPropertyChanged(nameof(Brush)); } }
        
        public DateTime? Start { get => _start; set { _start = value; OnPropertyChanged(); Refresh(); OnChanged?.Invoke(); } }
        public DateTime? End { get => _end; set { _end = value; OnPropertyChanged(); Refresh(); OnChanged?.Invoke(); } }

        [JsonIgnore] public bool IsDragging { get => _isDragging; set { _isDragging = value; OnPropertyChanged(); OnPropertyChanged(nameof(GhostVisibility)); } }
        [JsonIgnore] public DateTime? OriginalStart { get => _origStart; set { _origStart = value; Refresh(); } }
        [JsonIgnore] public DateTime? OriginalEnd { get => _origEnd; set { _origEnd = value; Refresh(); } }

        // ★選択中なら強制的にオレンジ色（DarkOrange）にする
        [JsonIgnore] public Brush Brush { get { if (GetIsSelected?.Invoke() == true) return Brushes.DarkOrange; try { return (Brush)new BrushConverter().ConvertFromString(ColorName)!; } catch { return Brushes.SteelBlue; } } }
        
        [JsonIgnore] public Visibility Visibility => (Start.HasValue && End.HasValue) ? Visibility.Visible : Visibility.Collapsed;
        [JsonIgnore] public Visibility GhostVisibility => IsDragging ? Visibility.Visible : Visibility.Collapsed;

        [JsonIgnore] public double Left => CalculateX(Start);
        [JsonIgnore] public double Width => CalculateWidth(Start, End);
        [JsonIgnore] public double GhostLeft => CalculateX(OriginalStart);
        [JsonIgnore] public double GhostWidth => CalculateWidth(OriginalStart, OriginalEnd);

        private double CalculateX(DateTime? dt) => (dt == null || GetProjectStart == null) ? 0 : 
            ((GetIsHourlyMode?.Invoke() ?? false) ? (dt.Value - GetProjectStart()).TotalHours : (dt.Value - GetProjectStart()).TotalDays) * GanttSettings.DayWidth;

        private double CalculateWidth(DateTime? s, DateTime? e) => (s == null || e == null) ? 0 : 
            Math.Max(0, ((GetIsHourlyMode?.Invoke() ?? false) ? (e.Value - s.Value).TotalHours : (e.Value - s.Value).TotalDays) * GanttSettings.DayWidth);

        public void Refresh() 
        { 
            OnPropertyChanged(nameof(Left)); OnPropertyChanged(nameof(Width)); 
            OnPropertyChanged(nameof(GhostLeft)); OnPropertyChanged(nameof(GhostWidth)); 
            OnPropertyChanged(nameof(Visibility)); OnPropertyChanged(nameof(GhostVisibility));
            OnPropertyChanged(nameof(Brush)); // ★色が変わるかもしれないので通知
        }

        public void Snapshot() { OriginalStart = Start; OriginalEnd = End; IsDragging = true; }
        public void Release() { IsDragging = false; }
    }

    public class ProjectSaveData
    {
        public ObservableCollection<TaskItem> Tasks { get; set; } = new();
        public ObservableCollection<NoteItem> Notes { get; set; } = new();
        public DateTime ProjectStartDate { get; set; } = new DateTime(2026, 4, 1);
        public int DisplayDays { get; set; } = 30;
        public bool IsProgressLineVisible { get; set; } = true;
        public bool IsWorkDayAdjustmentEnabled { get; set; } = true;
        public bool IsHourlyMode { get; set; } = false;
    }

    public class TaskItem : ViewModelBase
    {
        private Action? _onUpdate;
        public TaskItem() { }
        public TaskItem(Action onUpdate, Func<DateTime> getProjectStart, Func<bool> getIsHourlyMode) 
        { 
            SetReferences(onUpdate, getProjectStart, getIsHourlyMode);
        }

        public void SetReferences(Action onUpdate, Func<DateTime> getProjectStart, Func<bool> getIsHourlyMode)
        {
            _onUpdate = onUpdate;
            MainBar.GetProjectStart = SubBar.GetProjectStart = getProjectStart;
            MainBar.GetIsHourlyMode = SubBar.GetIsHourlyMode = getIsHourlyMode;
            MainBar.GetIsSelected = SubBar.GetIsSelected = () => IsSelected; // ★部品に連絡網を渡す
            MainBar.OnChanged = SubBar.OnChanged = onUpdate;
        }

        [JsonConverter(typeof(StringConverter))]
        public string Id { get => _id; set { _id = value; OnPropertyChanged(); } }
        private string _id = "";
        
        public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
        private string _name = "";
        
        public string Assignee { get => _assignee; set { _assignee = value; OnPropertyChanged(); } }
        private string _assignee = "";

        public string Memo { get => _memo; set { _memo = value; OnPropertyChanged(); } }
        private string _memo = "";
        
        public bool IsGroup { get => _isGroup; set { _isGroup = value; OnPropertyChanged(); OnPropertyChanged(nameof(BarOpacity)); OnPropertyChanged(nameof(Top)); } }
        private bool _isGroup;
        
        public int IndentLevel { get => _indentLevel; set { _indentLevel = value; OnPropertyChanged(); OnPropertyChanged(nameof(IndentMargin)); _onUpdate?.Invoke(); } }
        private int _indentLevel;
        
        public BarItem MainBar { get; set; } = new BarItem { ColorName = "SteelBlue" };
        public BarItem SubBar { get; set; } = new BarItem { ColorName = "DimGray" };

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }

        public void MigrateOldData()
        {
            if (ExtensionData == null) return;

            if (ExtensionData.TryGetValue("Start", out var st) && st.ValueKind == JsonValueKind.String && DateTime.TryParse(st.GetString(), out var dt1)) MainBar.Start = dt1;
            if (ExtensionData.TryGetValue("End", out var ed) && ed.ValueKind == JsonValueKind.String && DateTime.TryParse(ed.GetString(), out var dt2)) MainBar.End = dt2;
            if (ExtensionData.TryGetValue("SubTaskStart", out var sst) && sst.ValueKind == JsonValueKind.String && DateTime.TryParse(sst.GetString(), out var dt3)) SubBar.Start = dt3;
            if (ExtensionData.TryGetValue("SubTaskEnd", out var sed) && sed.ValueKind == JsonValueKind.String && DateTime.TryParse(sed.GetString(), out var dt4)) SubBar.End = dt4;
            
            if (ExtensionData.TryGetValue("BarColorName", out var bc) && bc.ValueKind == JsonValueKind.String) MainBar.ColorName = bc.GetString() ?? "SteelBlue";
            if (ExtensionData.TryGetValue("SubTaskBarColorName", out var sbc) && sbc.ValueKind == JsonValueKind.String) SubBar.ColorName = sbc.GetString() ?? "DimGray";
            if (ExtensionData.TryGetValue("SubTaskName", out var sn) && sn.ValueKind == JsonValueKind.String) SubBar.Name = sn.GetString() ?? "";

            ExtensionData.Clear();
        }

        [JsonIgnore]
        public int WorkDays
        {
            get => MainBar.Start.HasValue && MainBar.End.HasValue ? GanttLogic.CalculateWorkDays(MainBar.Start.Value, MainBar.End.Value) : 0;
            set { if (MainBar.Start.HasValue && value >= 0) { MainBar.End = GanttLogic.AddWorkDays(MainBar.Start.Value, value); OnPropertyChanged(); OnPropertyChanged(nameof(RemainingWorkDays)); } }
        }

        [JsonIgnore]
        public int RemainingWorkDays
        {
            get
            {
                var today = DateTime.Today;
                if (!MainBar.Start.HasValue || !MainBar.End.HasValue) return 0;
                if (today >= MainBar.End.Value.Date) return 0;
                if (today <= MainBar.Start.Value.Date) return WorkDays;
                return GanttLogic.CalculateWorkDays(today, MainBar.End.Value);
            }
        }

        private double _progress;
        public double Progress { get => _progress; set { _progress = Math.Max(0, Math.Min(100, value)); OnPropertyChanged(); OnPropertyChanged(nameof(ProgressWidth)); _onUpdate?.Invoke(); } }
        
        private string _predecessorId = "";
        public string PredecessorId { get => _predecessorId; set { _predecessorId = value; OnPropertyChanged(); _onUpdate?.Invoke(); } }
        
        private int _rowIndex;
        public int RowIndex { get => _rowIndex; set { _rowIndex = value; OnPropertyChanged(); OnPropertyChanged(nameof(Top)); OnPropertyChanged(nameof(RowTop)); _onUpdate?.Invoke(); } }
        
        private string _lineColorName = "DarkOrange";
        public string LineColorName { get => _lineColorName; set { _lineColorName = value; OnPropertyChanged(); _onUpdate?.Invoke(); } }

        // ★選択フラグが変わったら、自分の中のバーにも再描画（色の更新）を指示する
        [JsonIgnore] public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); MainBar.Refresh(); SubBar.Refresh(); } }
        private bool _isSelected;
        
        [JsonIgnore] public bool IsRowSelected { get => _isRowSelected; set { _isRowSelected = value; OnPropertyChanged(); } }
        private bool _isRowSelected;

        [JsonIgnore] public bool IsRelevant { get => _isRelevant; set { _isRelevant = value; OnPropertyChanged(); OnPropertyChanged(nameof(BarOpacity)); } }
        private bool _isRelevant = true;
        
        [JsonIgnore] public double BarOpacity => IsGroup ? 1.0 : (IsRelevant ? 0.8 : 0.2);
        [JsonIgnore] public Thickness IndentMargin => new Thickness(IndentLevel * 20, 0, 0, 0);

        [JsonIgnore] public double BarHeight => IsGroup ? 12 : 30;

        [JsonIgnore] public double Top => 40 + (RowIndex * GanttSettings.RowHeight) + ((GanttSettings.RowHeight - BarHeight) / 2);
        [JsonIgnore] public double RowTop => 40 + (RowIndex * GanttSettings.RowHeight);
        [JsonIgnore] public double ProgressWidth => MainBar.Width * (Progress / 100.0);

        [JsonIgnore] public DateTime? EffectiveEnd => SubBar.Visibility == Visibility.Visible ? SubBar.End : MainBar.End;

        public void RefreshDisplay() 
        { 
            MainBar.Refresh();
            SubBar.Refresh();
            OnPropertyChanged(nameof(Top)); 
            OnPropertyChanged(nameof(RowTop));
            OnPropertyChanged(nameof(WorkDays));
            OnPropertyChanged(nameof(RemainingWorkDays));
            OnPropertyChanged(nameof(ProgressWidth));
        }
    }

    public class DependencyLine { public string PathData { get; set; } = ""; public Brush LineBrush { get; set; } = Brushes.DarkOrange; }

    public class GridDayItem
    {
        public DateTime Date { get; set; }
        public double Left { get; set; }
        public bool IsHourly { get; set; }
        public string DayText => IsHourly ? Date.ToString("H:mm") : Date.ToString("MM/dd");
        public Brush BackgroundColor => IsHourly ? Brushes.Transparent : ((Date.DayOfWeek == DayOfWeek.Saturday) ? Brushes.AliceBlue : (Date.DayOfWeek == DayOfWeek.Sunday) ? Brushes.MistyRose : Brushes.Transparent);
    }

    public class MainViewModel : ViewModelBase
    {
        public ObservableCollection<TaskItem> Tasks { get; set; } = new();
        public ObservableCollection<NoteItem> Notes { get; set; } = new();
        public ObservableCollection<GridDayItem> GridDays { get; set; } = new();
        public ObservableCollection<DependencyLine> DependencyLines { get; set; } = new();
        
        private DateTime _projectStartDate = new DateTime(2026, 4, 1);
        public DateTime ProjectStartDate { get => _projectStartDate; set { _projectStartDate = value; OnPropertyChanged(); UpdateAll(); } }

        private int _displayDays = 30;
        public int DisplayDays { get => _displayDays; set { _displayDays = Math.Max(7, value); OnPropertyChanged(); UpdateAll(); } }

        public double TodayLeft => IsHourlyMode ? (DateTime.Now - ProjectStartDate).TotalHours * GanttSettings.DayWidth : (DateTime.Today - ProjectStartDate).TotalDays * GanttSettings.DayWidth;
        
        private TaskItem? _sourceTaskForLink = null;
        
        public string[] AvailableLineColors { get; } = new[] { "DarkOrange", "Crimson", "RoyalBlue", "SeaGreen", "DimGray" };
        public string[] AvailableBarColors { get; } = new[] { "SteelBlue", "MediumSeaGreen", "Tomato", "MediumPurple", "DimGray", "Goldenrod", "Teal" };

        private TaskItem? _dataGridSelectedTask;
        [JsonIgnore] 
        public TaskItem? DataGridSelectedTask 
        { 
            get => _dataGridSelectedTask; 
            set 
            { 
                if (_dataGridSelectedTask != null) _dataGridSelectedTask.IsRowSelected = false;
                _dataGridSelectedTask = value; 
                if (_dataGridSelectedTask != null) _dataGridSelectedTask.IsRowSelected = true;

                OnPropertyChanged(); 
                SelectedTask = value; 
                MemoTask = value; 
            } 
        }

        private TaskItem? _memoTask;
        [JsonIgnore] public TaskItem? MemoTask { get => _memoTask; set { _memoTask = value; OnPropertyChanged(); } }
        private TaskItem? _selectedTask;
        [JsonIgnore] public TaskItem? SelectedTask { get => _selectedTask; set { _selectedTask = value; OnPropertyChanged(); UpdateHighlight(); } }
        private bool _isLinkMode;
        [JsonIgnore] public bool IsLinkMode { get => _isLinkMode; set { _isLinkMode = value; OnPropertyChanged(); if (!_isLinkMode) ClearSelection(); } }
        
        private bool _isProgressLineVisible = true;
        public bool IsProgressLineVisible { get => _isProgressLineVisible; set { _isProgressLineVisible = value; OnPropertyChanged(); } }

        private bool _isWorkDayAdjustmentEnabled = true;
        public bool IsWorkDayAdjustmentEnabled { get => _isWorkDayAdjustmentEnabled; set { _isWorkDayAdjustmentEnabled = value; OnPropertyChanged(); } }

        private bool _isHourlyMode = false;
        public bool IsHourlyMode { get => _isHourlyMode; set { _isHourlyMode = value; OnPropertyChanged(); UpdateAll(); } }

        [JsonIgnore] public double ChartHeight => Math.Max(400, Tasks.Count * GanttSettings.RowHeight + 80);
        [JsonIgnore] public double ChartWidth => DisplayDays * GanttSettings.DayWidth;

        private string _progressLinePath = "";
        [JsonIgnore]
        public string ProgressLinePath { get => _progressLinePath; set { _progressLinePath = value; OnPropertyChanged(); } }

        private string _currentFilePath = "";
        [JsonIgnore] public string CurrentFilePath { get => _currentFilePath; set { _currentFilePath = value; OnPropertyChanged(); OnPropertyChanged(nameof(WindowTitle)); } }
        [JsonIgnore] public string WindowTitle => string.IsNullOrEmpty(CurrentFilePath) ? "PL Gantt Tool - 新規プロジェクト" : $"PL Gantt Tool - {Path.GetFileName(CurrentFilePath)}";

        [JsonIgnore] public bool SuspendUpdates { get; set; } = false;

        public MainViewModel() { UpdateAll(); }

        public void OnTaskBarClicked(TaskItem clickedTask)
        {
            DataGridSelectedTask = clickedTask;
            SelectedTask = clickedTask;
            MemoTask = null;
            if (IsLinkMode)
            {
                if (_sourceTaskForLink == null) { _sourceTaskForLink = clickedTask; _sourceTaskForLink.IsSelected = true; }
                else if (_sourceTaskForLink == clickedTask) { _sourceTaskForLink.IsSelected = false; _sourceTaskForLink = null; }
                else 
                { 
                    var currentIds = string.IsNullOrWhiteSpace(clickedTask.PredecessorId) ? new List<string>() : clickedTask.PredecessorId.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                    string sourceIdStr = _sourceTaskForLink.Id;
                    if (currentIds.Contains(sourceIdStr)) currentIds.Remove(sourceIdStr); else currentIds.Add(sourceIdStr);
                    clickedTask.PredecessorId = string.Join(", ", currentIds);
                    _sourceTaskForLink.IsSelected = false; _sourceTaskForLink = null; 
                }
                // ★結線後、必ずUI側に矢印を引き直すように更新通知を投げる
                UpdateAll();
            }
        }

        public void ClearSelection() { if (_dataGridSelectedTask != null) _dataGridSelectedTask.IsRowSelected = false; _dataGridSelectedTask = null; OnPropertyChanged(nameof(DataGridSelectedTask)); SelectedTask = null; MemoTask = null; if (_sourceTaskForLink != null) { _sourceTaskForLink.IsSelected = false; _sourceTaskForLink = null; } }
        
        public void AddNewTask() 
        { 
            var newTask = new TaskItem(UpdateAll, () => ProjectStartDate, () => IsHourlyMode) { Id = Guid.NewGuid().ToString().Substring(0,4), RowIndex = Tasks.Count, Name = "新規タスク", Progress = 0, IndentLevel = 0 };
            newTask.MainBar.Start = ProjectStartDate.AddHours(9);
            newTask.MainBar.End = ProjectStartDate.AddHours(17);
            Tasks.Add(newTask); 
            RefreshRowIndices();
            UpdateAll(); 
        }
        
        public void AddNewNote() { Notes.Add(new NoteItem { X = 50, Y = 50 }); }
        public void DeleteNote(NoteItem note) { if (note != null) Notes.Remove(note); }
        public void InsertTaskAbove(TaskItem target) { if (target != null) InsertTaskAt(Tasks.IndexOf(target), target.IndentLevel); }
        public void InsertTaskBelow(TaskItem target) { if (target != null) InsertTaskAt(Tasks.IndexOf(target) + 1, target.IndentLevel); }
        public void DeleteTask(TaskItem target) { if (target != null && Tasks.Contains(target)) { Tasks.Remove(target); RefreshRowIndices(); UpdateAll(); } }
        public void MoveTaskUp(TaskItem target) { if (target == null) return; int index = Tasks.IndexOf(target); if (index > 0) { Tasks.Move(index, index - 1); RefreshRowIndices(); UpdateAll(); } }
        public void MoveTaskDown(TaskItem target) { if (target == null) return; int index = Tasks.IndexOf(target); if (index >= 0 && index < Tasks.Count - 1) { Tasks.Move(index, index + 1); RefreshRowIndices(); UpdateAll(); } }

        private void InsertTaskAt(int index, int indentLevel)
        {
            var newTask = new TaskItem(UpdateAll, () => ProjectStartDate, () => IsHourlyMode) { Id = Guid.NewGuid().ToString().Substring(0,4), Name = "新規タスク", Progress = 0, IndentLevel = indentLevel };
            newTask.MainBar.Start = ProjectStartDate.AddHours(9);
            newTask.MainBar.End = ProjectStartDate.AddHours(17);
            if (index >= 0 && index <= Tasks.Count) Tasks.Insert(index, newTask); else Tasks.Add(newTask);
            RefreshRowIndices(); UpdateAll();
        }

        private void RefreshRowIndices() { for (int i = 0; i < Tasks.Count; i++) Tasks[i].RowIndex = i; }
        
        public void UpdateAll() 
        { 
            if (SuspendUpdates) return;

            RefreshGrid();
            UpdateGroups(); 
            UpdateDependencies(); 
            UpdateHighlight(); 
            UpdateProgressLine();

            foreach (var t in Tasks) t.RefreshDisplay();
            OnPropertyChanged(nameof(ChartHeight)); 
            OnPropertyChanged(nameof(ChartWidth));
            OnPropertyChanged(nameof(TodayLeft));
        }

        private void UpdateProgressLine()
        {
            if (Tasks.Count == 0)
            {
                ProgressLinePath = $"M {TodayLeft},0 L {TodayLeft},{ChartHeight}";
                return;
            }

            var sortedTasks = Tasks.OrderBy(t => t.RowIndex).ToList();
            string path = $"M {TodayLeft},0 ";

            foreach (var task in sortedTasks)
            {
                double rowTop = task.RowTop;
                double rowHeight = GanttSettings.RowHeight;
                double ptX;

                DateTime cmpDate = IsHourlyMode ? DateTime.Now : DateTime.Today;

                if (task.Progress >= 100 || (task.MainBar.Start.HasValue && cmpDate >= task.MainBar.End)) ptX = task.MainBar.Left + task.MainBar.Width;
                else if (task.MainBar.Start.HasValue && cmpDate < task.MainBar.Start.Value && task.Progress <= 0) ptX = TodayLeft;
                else ptX = task.MainBar.Left + task.ProgressWidth;

                if (Math.Abs(ptX - TodayLeft) > 0.1)
                {
                    path += $"L {TodayLeft},{rowTop + rowHeight * 0.2} ";
                    path += $"L {ptX},{rowTop + rowHeight * 0.5} ";
                    path += $"L {TodayLeft},{rowTop + rowHeight * 0.8} ";
                }
            }

            path += $"L {TodayLeft},{ChartHeight}";
            ProgressLinePath = path;
        }

        private void RefreshGrid()
        {
            GridDays.Clear();
            for (int i = 0; i < DisplayDays; i++)
            {
                var date = IsHourlyMode ? ProjectStartDate.AddHours(i) : ProjectStartDate.AddDays(i);
                GridDays.Add(new GridDayItem { Date = date, Left = i * GanttSettings.DayWidth, IsHourly = this.IsHourlyMode });
            }
        }

        private void UpdateGroups() { for (int i = 0; i < Tasks.Count; i++) { if (i < Tasks.Count - 1) Tasks[i].IsGroup = Tasks[i + 1].IndentLevel > Tasks[i].IndentLevel; else Tasks[i].IsGroup = false; } }

        private void UpdateDependencies()
        {
            DependencyLines.Clear();
            var converter = new BrushConverter();
            foreach (var target in Tasks)
            {
                if (string.IsNullOrWhiteSpace(target.PredecessorId)) continue;
                var preIds = target.PredecessorId.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var idStr in preIds)
                {
                    var source = Tasks.FirstOrDefault(t => t.Id == idStr);
                    if (source != null && source.EffectiveEnd.HasValue && target.MainBar.Start.HasValue)
                    {
                        double startX = source.SubBar.Visibility == Visibility.Visible ? source.SubBar.Left + source.SubBar.Width : source.MainBar.Left + source.MainBar.Width;
                        double startY = source.Top + (source.BarHeight / 2);
                        double endX = target.MainBar.Left;
                        double endY = target.Top + (target.BarHeight / 2);
                        string path = $"M {startX},{startY} L {startX + 10},{startY} L {startX + 10},{endY} L {endX},{endY}";
                        Brush taskBrush = (Brush)converter.ConvertFromString(source.LineColorName) ?? Brushes.DarkOrange;
                        DependencyLines.Add(new DependencyLine { PathData = path, LineBrush = taskBrush });
                    }
                }
            }
        }

        private void UpdateHighlight()
        {
            foreach (var t in Tasks) t.IsRelevant = (SelectedTask == null);
            if (SelectedTask == null) return;
            SelectedTask.IsRelevant = true;
            HighlightPredecessors(SelectedTask); HighlightSuccessors(SelectedTask);
        }

        private void HighlightPredecessors(TaskItem task)
        {
            if (string.IsNullOrWhiteSpace(task.PredecessorId)) return;
            var preIds = task.PredecessorId.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var idStr in preIds) { var pre = Tasks.FirstOrDefault(t => t.Id == idStr); if (pre != null && !pre.IsRelevant) { pre.IsRelevant = true; HighlightPredecessors(pre); } }
        }

        private void HighlightSuccessors(TaskItem task)
        {
            string idStr = task.Id;
            var successors = Tasks.Where(t => !string.IsNullOrWhiteSpace(t.PredecessorId) && t.PredecessorId.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).Contains(idStr));
            foreach (var suc in successors) { if (!suc.IsRelevant) { suc.IsRelevant = true; HighlightSuccessors(suc); } }
        }

        public void SaveToFile(string filePath)
        {
            var saveData = new ProjectSaveData { 
                Tasks = this.Tasks, 
                Notes = this.Notes, 
                ProjectStartDate = this.ProjectStartDate, 
                DisplayDays = this.DisplayDays,
                IsProgressLineVisible = this.IsProgressLineVisible,
                IsWorkDayAdjustmentEnabled = this.IsWorkDayAdjustmentEnabled,
                IsHourlyMode = this.IsHourlyMode
            };
            File.WriteAllText(filePath, JsonSerializer.Serialize(saveData, new JsonSerializerOptions { WriteIndented = true }));
            CurrentFilePath = filePath;
        }

        public void LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath)) return;
            string jsonString = File.ReadAllText(filePath);
            try
            {
                var options = new JsonSerializerOptions { NumberHandling = JsonNumberHandling.AllowReadingFromString };
                var data = JsonSerializer.Deserialize<ProjectSaveData>(jsonString, options);
                
                if (data != null)
                {
                    ProjectStartDate = data.ProjectStartDate;
                    DisplayDays = data.DisplayDays;
                    IsProgressLineVisible = data.IsProgressLineVisible;
                    IsWorkDayAdjustmentEnabled = data.IsWorkDayAdjustmentEnabled;
                    IsHourlyMode = data.IsHourlyMode;

                    Tasks.Clear();
                    foreach (var task in data.Tasks) 
                    { 
                        task.SetReferences(UpdateAll, () => ProjectStartDate, () => IsHourlyMode); 
                        task.MigrateOldData(); 
                        Tasks.Add(task); 
                    }
                    
                    Notes.Clear();
                    if (data.Notes != null) foreach (var note in data.Notes) Notes.Add(note);
                    
                    RefreshRowIndices(); 
                    UpdateAll(); 
                    CurrentFilePath = filePath;
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"ファイルの読み込みに失敗しました。\n詳細: {ex.Message}");
            }
        }
    }
}