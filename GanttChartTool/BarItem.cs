using System;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Media;

namespace GanttChartTool
{
    public class BarItem : ViewModelBase
    {
        private DateTime? _start, _end, _origStart, _origEnd;
        private string _name = "", _colorName = "SteelBlue";
        private bool _isDragging;
        
        [JsonIgnore] public Func<DateTime>? GetProjectStart;
        [JsonIgnore] public Func<TimeSpan>? GetGridInterval;
        [JsonIgnore] public Func<bool>? GetIsSelected;
        [JsonIgnore] public Action? OnChanged;
        
        public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
        public string ColorName { get => _colorName; set { _colorName = value; OnPropertyChanged(); OnPropertyChanged(nameof(Brush)); } }
        
        public DateTime? Start { get => _start; set { _start = value; OnPropertyChanged(); Refresh(); OnChanged?.Invoke(); } }
        public DateTime? End { get => _end; set { _end = value; OnPropertyChanged(); Refresh(); OnChanged?.Invoke(); } }
        
        [JsonIgnore] public bool IsDragging { get => _isDragging; set { _isDragging = value; OnPropertyChanged(); OnPropertyChanged(nameof(GhostVisibility)); } }
        [JsonIgnore] public DateTime? OriginalStart { get => _origStart; set { _origStart = value; Refresh(); } }
        [JsonIgnore] public DateTime? OriginalEnd { get => _origEnd; set { _origEnd = value; Refresh(); } }
        
        [JsonIgnore] public Brush Brush { get { if (GetIsSelected?.Invoke() == true) return Brushes.DarkOrange; try { return (Brush)new BrushConverter().ConvertFromString(ColorName)!; } catch { return Brushes.SteelBlue; } } }
        
        [JsonIgnore] public Visibility Visibility => (Start.HasValue && End.HasValue) ? Visibility.Visible : Visibility.Collapsed;
        [JsonIgnore] public Visibility GhostVisibility => IsDragging ? Visibility.Visible : Visibility.Collapsed;
        
        [JsonIgnore] public double Left => CalculateX(Start);
        [JsonIgnore] public double Width => CalculateWidth(Start, End);
        [JsonIgnore] public double GhostLeft => CalculateX(OriginalStart);
        [JsonIgnore] public double GhostWidth => CalculateWidth(OriginalStart, OriginalEnd);
        
        private double CalculateX(DateTime? dt) => (dt == null || GetProjectStart == null || GetGridInterval == null) ? 0 : ((dt.Value - GetProjectStart()).TotalHours / GetGridInterval().TotalHours) * GanttSettings.DayWidth;
        private double CalculateWidth(DateTime? s, DateTime? e) => (s == null || e == null || GetGridInterval == null) ? 0 : Math.Max(0, ((e.Value - s.Value).TotalHours / GetGridInterval().TotalHours) * GanttSettings.DayWidth);
        
        public void Refresh() 
        { 
            OnPropertyChanged(nameof(Left)); 
            OnPropertyChanged(nameof(Width)); 
            OnPropertyChanged(nameof(GhostLeft)); 
            OnPropertyChanged(nameof(GhostWidth)); 
            OnPropertyChanged(nameof(Visibility)); 
            OnPropertyChanged(nameof(GhostVisibility)); 
            OnPropertyChanged(nameof(Brush)); 
        }
        
        public void Snapshot() { OriginalStart = Start; OriginalEnd = End; IsDragging = true; }
        public void Release() { IsDragging = false; }
    }
}