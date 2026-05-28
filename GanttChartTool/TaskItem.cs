using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;

namespace GanttChartTool
{
    public class TaskItem : ViewModelBase
    {
        private Action? _onUpdate;
        public TaskItem() { }
        public TaskItem(Action onUpdate, Func<DateTime> getProjectStart, Func<TimeSpan> getGridInterval) 
        { 
            SetReferences(onUpdate, getProjectStart, getGridInterval);
        }

        public void SetReferences(Action onUpdate, Func<DateTime> getProjectStart, Func<TimeSpan> getGridInterval)
        {
            _onUpdate = onUpdate;
            MainBar.GetProjectStart = SubBar.GetProjectStart = getProjectStart;
            MainBar.GetGridInterval = SubBar.GetGridInterval = getGridInterval;
            MainBar.GetIsSelected = SubBar.GetIsSelected = () => IsSelected;
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
        public double Length
        {
            get => Math.Max(0, MainBar.EndNumber - MainBar.StartNumber);
            set { if (value >= 0) MainBar.EndNumber = MainBar.StartNumber + value; OnPropertyChanged(); }
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

        [JsonIgnore] public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); MainBar.Refresh(); SubBar.Refresh(); } }
        private bool _isSelected;
        
        [JsonIgnore] public bool IsRowSelected { get => _isRowSelected; set { _isRowSelected = value; OnPropertyChanged(); } }
        private bool _isRowSelected;

        [JsonIgnore] public bool IsRelevant { get => _isRelevant; set { _isRelevant = value; OnPropertyChanged(); OnPropertyChanged(nameof(BarOpacity)); } }
        private bool _isRelevant = true;
        
        [JsonIgnore] public double BarOpacity => IsGroup ? 1.0 : (IsRelevant ? 0.8 : 0.2);
        [JsonIgnore] public Thickness IndentMargin => new Thickness(IndentLevel * 20, 0, 0, 0);

        [JsonIgnore] public double BarHeight => IsGroup ? 12 : 30;

        [JsonIgnore] public double Top => (RowIndex * GanttSettings.RowHeight) + ((GanttSettings.RowHeight - BarHeight) / 2);
        [JsonIgnore] public double RowTop => RowIndex * GanttSettings.RowHeight;
        [JsonIgnore] public double ProgressWidth => MainBar.Width * (Progress / 100.0);

        [JsonIgnore] public DateTime? EffectiveEnd => SubBar.Visibility == Visibility.Visible ? SubBar.End : MainBar.End;

        public void RefreshDisplay() 
        { 
            MainBar.Refresh();
            SubBar.Refresh();
            OnPropertyChanged(nameof(Top)); 
            OnPropertyChanged(nameof(RowTop));
            OnPropertyChanged(nameof(WorkDays));
            OnPropertyChanged(nameof(Length));
            OnPropertyChanged(nameof(RemainingWorkDays));
            OnPropertyChanged(nameof(ProgressWidth));
        }
    }
}