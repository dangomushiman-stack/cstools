using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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

    public class IntervalOption
    {
        public string Name { get; set; } = "";
        public TimeSpan TimeSpan { get; set; }
    }

    public class ProjectSaveData
    {
        public ObservableCollection<TaskItem> Tasks { get; set; } = new();
        public ObservableCollection<NoteItem> Notes { get; set; } = new();
        public DateTime ProjectStartDate { get; set; } = new DateTime(2026, 4, 1);
        public int DisplayDays { get; set; } = 30;
        public bool IsProgressLineVisible { get; set; } = true;
        public bool IsWorkDayAdjustmentEnabled { get; set; } = true;
        public long IntervalTicks { get; set; } = 0; 
        public bool IsSnapToDay { get; set; } = false;
        public bool IsNumericMode { get; set; } = false;
        public bool IsDependencyFocusEnabled { get; set; } = true;
        
        // ★修正：過去のJSONファイルを読み込むための互換性プロパティとして復活
        public bool IsHourlyMode { get; set; } = false; 
    }

    public class DependencyLine { public string PathData { get; set; } = ""; public Brush LineBrush { get; set; } = Brushes.DarkOrange; }
    public class GridDayItem { public DateTime Date { get; set; } public double Left { get; set; } public TimeSpan Interval { get; set; } public string Label { get; set; } = ""; public string DayText => !string.IsNullOrEmpty(Label) ? Label : (Interval.TotalDays < 1 ? Date.ToString("H:mm") : Date.ToString("MM/dd")); }
    public class HolidayBand { public double Left { get; set; } public double Width { get; set; } public Brush BackgroundColor { get; set; } = Brushes.Transparent; }

    public class MainViewModel : ViewModelBase
    {
        public ObservableCollection<TaskItem> Tasks { get; set; } = new();
        public ObservableCollection<NoteItem> Notes { get; set; } = new();
        public ObservableCollection<GridDayItem> GridDays { get; set; } = new();
        public ObservableCollection<HolidayBand> HolidayBands { get; set; } = new();
        public ObservableCollection<DependencyLine> DependencyLines { get; set; } = new();
        public ObservableCollection<IntervalOption> IntervalOptions { get; } = new() { new IntervalOption { Name = "1時間", TimeSpan = TimeSpan.FromHours(1) }, new IntervalOption { Name = "3時間", TimeSpan = TimeSpan.FromHours(3) }, new IntervalOption { Name = "6時間", TimeSpan = TimeSpan.FromHours(6) }, new IntervalOption { Name = "半日", TimeSpan = TimeSpan.FromHours(12) }, new IntervalOption { Name = "1日", TimeSpan = TimeSpan.FromDays(1) }, new IntervalOption { Name = "2日", TimeSpan = TimeSpan.FromDays(2) }, new IntervalOption { Name = "3日", TimeSpan = TimeSpan.FromDays(3) }, new IntervalOption { Name = "1週間", TimeSpan = TimeSpan.FromDays(7) } };

        public ObservableCollection<string> AvailableLineColors { get; } = new ObservableCollection<string> { "DarkOrange", "Crimson", "RoyalBlue", "SeaGreen", "DimGray" };
        public ObservableCollection<string> AvailableBarColors { get; } = new ObservableCollection<string> { "SteelBlue", "MediumSeaGreen", "Tomato", "MediumPurple", "DimGray", "Goldenrod", "Teal" };

        [JsonIgnore] public bool IsInternalLoading { get; set; } = false;
        private bool _hasUnsavedChanges = false;
        [JsonIgnore]
        public bool HasUnsavedChanges 
        { 
            get => _hasUnsavedChanges; 
            set { _hasUnsavedChanges = value; OnPropertyChanged(); OnPropertyChanged(nameof(WindowTitle)); } 
        }
        public void MarkDirty() { if (!IsInternalLoading) HasUnsavedChanges = true; }

        private IntervalOption _selectedInterval;
        public IntervalOption SelectedInterval { get => _selectedInterval; set { _selectedInterval = value; MarkDirty(); OnPropertyChanged(); OnPropertyChanged(nameof(IsHourlyMode)); UpdateAll(); } }
        public bool IsHourlyMode => !IsNumericMode && SelectedInterval?.TimeSpan.TotalDays < 1;
        public bool IsDateMode => !IsNumericMode;
        public string AxisStartCaption => IsNumericMode ? "数値軸:" : "開始日:";
        private bool _isSnapToDay = false;
        public bool IsSnapToDay { get => _isSnapToDay; set { _isSnapToDay = value; MarkDirty(); OnPropertyChanged(); } }
        private bool _isNumericMode = false;
        public bool IsNumericMode
        {
            get => _isNumericMode;
            set
            {
                _isNumericMode = value;
                MarkDirty();
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsDateMode));
                OnPropertyChanged(nameof(IsHourlyMode));
                OnPropertyChanged(nameof(AxisStartCaption));
                UpdateAll();
            }
        }
        private DateTime _projectStartDate = new DateTime(2026, 4, 1);
        public DateTime ProjectStartDate { get => _projectStartDate; set { _projectStartDate = value; MarkDirty(); OnPropertyChanged(); UpdateAll(); } }
        private int _displayDays = 30; public int DisplayDays { get => _displayDays; set { _displayDays = Math.Max(7, value); MarkDirty(); OnPropertyChanged(); UpdateAll(); } }
        public double TodayLeft => (SelectedInterval == null || IsNumericMode) ? 0 : (DateTime.Now - ProjectStartDate).TotalHours / SelectedInterval.TimeSpan.TotalHours * GanttSettings.DayWidth;
        private TaskItem? _sourceTaskForLink = null, _memoTask;
        public TaskItem? MemoTask { get => _memoTask; set { _memoTask = value; OnPropertyChanged(); } }
        private TaskItem? _dataGridSelectedTask, _selectedTask;
        public TaskItem? DataGridSelectedTask { get => _dataGridSelectedTask; set { if (_dataGridSelectedTask != null) _dataGridSelectedTask.IsRowSelected = false; _dataGridSelectedTask = value; if (_dataGridSelectedTask != null) _dataGridSelectedTask.IsRowSelected = true; OnPropertyChanged(); SelectedTask = value; MemoTask = value; } }
        public TaskItem? SelectedTask { get => _selectedTask; set { _selectedTask = value; OnPropertyChanged(); UpdateHighlight(); } }
        private bool _isLinkMode, _isProgressLineVisible = true, _isWorkDayAdjustmentEnabled = true;
        public bool IsLinkMode { get => _isLinkMode; set { _isLinkMode = value; OnPropertyChanged(); if (!_isLinkMode) ClearSelection(); } }
        public bool IsProgressLineVisible { get => _isProgressLineVisible; set { _isProgressLineVisible = value; MarkDirty(); OnPropertyChanged(); } }
        public bool IsWorkDayAdjustmentEnabled { get => _isWorkDayAdjustmentEnabled; set { _isWorkDayAdjustmentEnabled = value; MarkDirty(); OnPropertyChanged(); } }
        private bool _isDependencyFocusEnabled = true;
        public bool IsDependencyFocusEnabled
        {
            get => _isDependencyFocusEnabled;
            set
            {
                _isDependencyFocusEnabled = value;
                MarkDirty();
                OnPropertyChanged();
                UpdateHighlight();
            }
        }
        public double ChartHeight => Math.Max(400, Tasks.Count * GanttSettings.RowHeight);
        public double ChartWidth => DisplayDays * GanttSettings.DayWidth;
        private string _progressLinePath = "";
        public string ProgressLinePath { get => _progressLinePath; set { _progressLinePath = value; OnPropertyChanged(); } }
        private string _currentFilePath = ""; public string CurrentFilePath { get => _currentFilePath; set { _currentFilePath = value; OnPropertyChanged(); OnPropertyChanged(nameof(WindowTitle)); } }
        
        public string WindowTitle => (string.IsNullOrEmpty(CurrentFilePath) ? "PL Gantt Tool - 新規プロジェクト" : $"PL Gantt Tool - {Path.GetFileName(CurrentFilePath)}") + (HasUnsavedChanges ? " *" : "");
        
        public bool SuspendUpdates { get; set; } = false;
        
        public MainViewModel() 
        { 
            IsInternalLoading = true;
            _selectedInterval = IntervalOptions[4]; 

            Tasks.CollectionChanged += (s, e) => {
                MarkDirty();
                if (e.NewItems != null) foreach (INotifyPropertyChanged item in e.NewItems) AttachDirtyTracker(item);
            };
            Notes.CollectionChanged += (s, e) => {
                MarkDirty();
                if (e.NewItems != null) foreach (INotifyPropertyChanged item in e.NewItems) AttachDirtyTracker(item);
            };

            UpdateAll(); 
            IsInternalLoading = false;
            HasUnsavedChanges = false;
        }

        private void AttachDirtyTracker(INotifyPropertyChanged item)
        {
            item.PropertyChanged -= Item_PropertyChanged;
            item.PropertyChanged += Item_PropertyChanged;
            if (item is TaskItem t)
            {
                t.MainBar.PropertyChanged -= Item_PropertyChanged;
                t.MainBar.PropertyChanged += Item_PropertyChanged;
                t.SubBar.PropertyChanged -= Item_PropertyChanged;
                t.SubBar.PropertyChanged += Item_PropertyChanged;
            }
        }

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsSelected" || e.PropertyName == "IsRowSelected" || e.PropertyName == "IsRelevant" || 
                e.PropertyName == "IsDragging" || e.PropertyName == "GhostVisibility" || e.PropertyName == "GhostLeft" || e.PropertyName == "GhostWidth" ||
                e.PropertyName == "CalloutVisibility" || e.PropertyName == "TailPathData" || e.PropertyName == "Top" || e.PropertyName == "RowTop" || e.PropertyName == "Left" || e.PropertyName == "Width") 
                return; 
            
            MarkDirty();
        }

        public void SelectNote(NoteItem targetNote) { ClearSelection(); if (targetNote != null) targetNote.IsSelected = true; }
        
        public void OnTaskBarClicked(TaskItem clickedTask) 
        { 
            foreach (var note in Notes) note.IsSelected = false; 
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
                    if (currentIds.Contains(_sourceTaskForLink.Id)) currentIds.Remove(_sourceTaskForLink.Id); else currentIds.Add(_sourceTaskForLink.Id); 
                    clickedTask.PredecessorId = string.Join(", ", currentIds); 
                    _sourceTaskForLink.IsSelected = false; _sourceTaskForLink = null; 
                } 
                UpdateAll(); 
            } 
        }
        
        public void ClearSelection() 
        { 
            if (_dataGridSelectedTask != null) _dataGridSelectedTask.IsRowSelected = false; 
            _dataGridSelectedTask = null; 
            OnPropertyChanged(nameof(DataGridSelectedTask)); 
            SelectedTask = null; 
            MemoTask = null; 
            if (_sourceTaskForLink != null) { _sourceTaskForLink.IsSelected = false; _sourceTaskForLink = null; } 
            foreach (var note in Notes) note.IsSelected = false; 
        }
        
        public void AddNewTask()
        {
            var start = ProjectStartDate.Date;
            var end = start.Add(SelectedInterval.TimeSpan);

            var newTask = new TaskItem(UpdateAll, () => ProjectStartDate, () => SelectedInterval.TimeSpan)
            {
                Id = Guid.NewGuid().ToString().Substring(0, 4),
                Name = "新規タスク",
                MainBar =
                {
                    Start = start,
                    End = end
                }
            };

            Tasks.Add(newTask);
            RefreshRowIndices();
            UpdateAll();
        }
        public void AddNewNote() { var note = new NoteItem { TimePosition = ProjectStartDate.AddHours(12) }; note.GetProjectStart = () => ProjectStartDate; note.GetGridInterval = () => SelectedInterval.TimeSpan; Notes.Add(note); SelectNote(note); }
        public void DeleteNote(NoteItem note) { if (note != null) Notes.Remove(note); }
        public void InsertTaskAbove(TaskItem target) { if (target != null) InsertTaskAt(Tasks.IndexOf(target), target.IndentLevel); }
        public void InsertTaskBelow(TaskItem target) { if (target != null) InsertTaskAt(Tasks.IndexOf(target) + 1, target.IndentLevel); }
        public void DeleteTask(TaskItem target) { if (target != null && Tasks.Contains(target)) { Tasks.Remove(target); RefreshRowIndices(); UpdateAll(); } }
        public void MoveTaskUp(TaskItem target) { if (target == null) return; int index = Tasks.IndexOf(target); if (index > 0) { Tasks.Move(index, index - 1); RefreshRowIndices(); UpdateAll(); } }
        public void MoveTaskDown(TaskItem target) { if (target == null) return; int index = Tasks.IndexOf(target); if (index >= 0 && index < Tasks.Count - 1) { Tasks.Move(index, index + 1); RefreshRowIndices(); UpdateAll(); } }
        private void InsertTaskAt(int index, int indentLevel)
        {
            var start = ProjectStartDate.Date;
            var end = start.Add(SelectedInterval.TimeSpan);

            var newTask = new TaskItem(UpdateAll, () => ProjectStartDate, () => SelectedInterval.TimeSpan)
            {
                Id = Guid.NewGuid().ToString().Substring(0, 4),
                Name = "新規タスク",
                MainBar =
                {
                    Start = start,
                    End = end
                },
                IndentLevel = indentLevel
            };

            if (index >= 0 && index <= Tasks.Count)
                Tasks.Insert(index, newTask);
            else
                Tasks.Add(newTask);

            RefreshRowIndices();
            UpdateAll();
        }
        private void RefreshRowIndices() { for (int i = 0; i < Tasks.Count; i++) Tasks[i].RowIndex = i; }

        public void UpdateAll()
        {
            if (SuspendUpdates || SelectedInterval == null) return;
            GridDays.Clear(); for (int i = 0; i < DisplayDays; i++) GridDays.Add(new GridDayItem { Date = ProjectStartDate.Add(TimeSpan.FromTicks(SelectedInterval.TimeSpan.Ticks * i)), Left = i * GanttSettings.DayWidth, Interval = SelectedInterval.TimeSpan, Label = IsNumericMode ? i.ToString() : "" });
            HolidayBands.Clear(); DateTime current = ProjectStartDate.Date, end = ProjectStartDate.Add(TimeSpan.FromTicks(SelectedInterval.TimeSpan.Ticks * DisplayDays));
            if (!IsNumericMode) while (current <= end.Date.AddDays(1)) { if (current.DayOfWeek == DayOfWeek.Saturday || current.DayOfWeek == DayOfWeek.Sunday) { double left = ((current - ProjectStartDate).TotalHours / SelectedInterval.TimeSpan.TotalHours) * GanttSettings.DayWidth, width = (((current.AddDays(1) - ProjectStartDate).TotalHours / SelectedInterval.TimeSpan.TotalHours) * GanttSettings.DayWidth) - left; if (left + width > 0 && left < ChartWidth) HolidayBands.Add(new HolidayBand { Left = left, Width = width, BackgroundColor = current.DayOfWeek == DayOfWeek.Saturday ? Brushes.AliceBlue : Brushes.MistyRose }); } current = current.AddDays(1); }
            UpdateGroups(); UpdateDependencies(); UpdateHighlight(); UpdateProgressLine();
            foreach (var t in Tasks) t.RefreshDisplay(); foreach (var n in Notes) n.RefreshDisplay();
            OnPropertyChanged(nameof(ChartHeight)); OnPropertyChanged(nameof(ChartWidth)); OnPropertyChanged(nameof(TodayLeft));
        }

        private void UpdateProgressLine()
        {
            if (IsNumericMode) { ProgressLinePath = ""; return; }
            if (Tasks.Count == 0) { ProgressLinePath = $"M {TodayLeft},0 L {TodayLeft},{ChartHeight}"; return; }
            string path = $"M {TodayLeft},0 "; DateTime cmpDate = IsHourlyMode ? DateTime.Now : DateTime.Today;
            foreach (var task in Tasks.OrderBy(t => t.RowIndex)) { double rowTop = task.RowTop, ptX = (task.Progress >= 100 || (task.MainBar.Start.HasValue && cmpDate >= task.MainBar.End)) ? task.MainBar.Left + task.MainBar.Width : (task.MainBar.Start.HasValue && cmpDate < task.MainBar.Start.Value && task.Progress <= 0) ? TodayLeft : task.MainBar.Left + task.ProgressWidth; if (Math.Abs(ptX - TodayLeft) > 0.1) { path += $"L {TodayLeft},{rowTop + 8} L {ptX},{rowTop + 20} L {TodayLeft},{rowTop + 32} "; } }
            path += $"L {TodayLeft},{ChartHeight}"; ProgressLinePath = path;
        }
        private void UpdateGroups() { for (int i = 0; i < Tasks.Count; i++) Tasks[i].IsGroup = (i < Tasks.Count - 1) && Tasks[i + 1].IndentLevel > Tasks[i].IndentLevel; }
        private void UpdateDependencies()
        {
            DependencyLines.Clear(); var converter = new BrushConverter();
            foreach (var target in Tasks.Where(x => !string.IsNullOrWhiteSpace(x.PredecessorId))) { foreach (var id in target.PredecessorId.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)) { var source = Tasks.FirstOrDefault(t => t.Id == id); if (source != null && source.EffectiveEnd.HasValue && target.MainBar.Start.HasValue) { double startX = source.SubBar.Visibility == Visibility.Visible ? source.SubBar.Left + source.SubBar.Width : source.MainBar.Left + source.MainBar.Width, startY = source.Top + (source.BarHeight / 2), endX = target.MainBar.Left, endY = target.Top + (target.BarHeight / 2); DependencyLines.Add(new DependencyLine { PathData = $"M {startX},{startY} L {startX + 10},{startY} L {startX + 10},{endY} L {endX},{endY}", LineBrush = (Brush)converter.ConvertFromString(source.LineColorName) ?? Brushes.DarkOrange }); } } }
        }
        private void UpdateHighlight()
        {
            if (!IsDependencyFocusEnabled)
            {
                foreach (var t in Tasks) t.IsRelevant = true;
                return;
            }

            foreach (var t in Tasks) t.IsRelevant = (SelectedTask == null);
            if (SelectedTask == null) return;
            SelectedTask.IsRelevant = true;
            HighlightPredecessors(SelectedTask);
            HighlightSuccessors(SelectedTask);
        }
        private void HighlightPredecessors(TaskItem task) { if (string.IsNullOrWhiteSpace(task.PredecessorId)) return; foreach (var id in task.PredecessorId.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)) { var pre = Tasks.FirstOrDefault(t => t.Id == id); if (pre != null && !pre.IsRelevant) { pre.IsRelevant = true; HighlightPredecessors(pre); } } }
        private void HighlightSuccessors(TaskItem task) { var successors = Tasks.Where(t => !string.IsNullOrWhiteSpace(t.PredecessorId) && t.PredecessorId.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).Contains(task.Id)); foreach (var suc in successors) { if (!suc.IsRelevant) { suc.IsRelevant = true; HighlightSuccessors(suc); } } }

        public void SaveToFile(string filePath) 
        { 
            var data = new ProjectSaveData { Tasks = Tasks, Notes = Notes, ProjectStartDate = ProjectStartDate, DisplayDays = DisplayDays, IsProgressLineVisible = IsProgressLineVisible, IsWorkDayAdjustmentEnabled = IsWorkDayAdjustmentEnabled, IntervalTicks = SelectedInterval.TimeSpan.Ticks, IsSnapToDay = IsSnapToDay, IsNumericMode = IsNumericMode, IsDependencyFocusEnabled = IsDependencyFocusEnabled }; 
            File.WriteAllText(filePath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true })); 
            CurrentFilePath = filePath; 
            HasUnsavedChanges = false; 
        }
        
        public void LoadFromFile(string filePath) 
        { 
            if (!File.Exists(filePath)) return; 
            try 
            { 
                var data = JsonSerializer.Deserialize<ProjectSaveData>(File.ReadAllText(filePath), new JsonSerializerOptions { NumberHandling = JsonNumberHandling.AllowReadingFromString }); 
                if (data != null) 
                { 
                    IsInternalLoading = true; 
                    ProjectStartDate = data.ProjectStartDate; DisplayDays = data.DisplayDays; IsProgressLineVisible = data.IsProgressLineVisible; IsWorkDayAdjustmentEnabled = data.IsWorkDayAdjustmentEnabled; IsSnapToDay = data.IsSnapToDay; IsNumericMode = data.IsNumericMode; IsDependencyFocusEnabled = data.IsDependencyFocusEnabled; 
                    SelectedInterval = IntervalOptions.FirstOrDefault(x => x.TimeSpan.Ticks == data.IntervalTicks) ?? (data.IsHourlyMode ? IntervalOptions[0] : IntervalOptions[4]); 
                    
                    Tasks.Clear(); foreach (var t in data.Tasks) { t.SetReferences(UpdateAll, () => ProjectStartDate, () => SelectedInterval.TimeSpan); t.MigrateOldData(); Tasks.Add(t); } 
                    Notes.Clear(); 
                    if (data.Notes != null) 
                    {
                        foreach (var n in data.Notes) { n.GetProjectStart = () => ProjectStartDate; n.GetGridInterval = () => SelectedInterval.TimeSpan; Notes.Add(n); }
                    }
                    RefreshRowIndices(); UpdateAll(); CurrentFilePath = filePath; 
                    IsInternalLoading = false; 
                    HasUnsavedChanges = false; 
                } 
            } catch (Exception ex) { MessageBox.Show($"エラー: {ex.Message}"); } 
        }
    }
}