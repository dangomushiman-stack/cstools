using System;
using System.Text.Json.Serialization;
using System.Windows;

namespace GanttChartTool
{
    public class NoteItem : ViewModelBase
    {
        public NoteItem() { }

        [JsonIgnore] public Func<DateTime>? GetProjectStart;
        [JsonIgnore] public Func<TimeSpan>? GetGridInterval;

        private string _text = "新しいメモ";
        public string Text { get => _text; set { _text = value; OnPropertyChanged(); } }

        private DateTime _timePosition = DateTime.Now;
        public DateTime TimePosition { get => _timePosition; set { _timePosition = value; OnPropertyChanged(); RefreshDisplay(); } }

        private DateTime _targetTimePosition = DateTime.Now.AddDays(1);
        public DateTime TargetTimePosition { get => _targetTimePosition; set { _targetTimePosition = value; OnPropertyChanged(); RefreshDisplay(); } }

        [JsonIgnore] public double X => CalculateX(TimePosition);
        [JsonIgnore] public double TargetX => CalculateX(TargetTimePosition);

        private double _y = 50, _width = 150, _height = 80;
        public double Y { get => _y; set { _y = value; OnPropertyChanged(); RefreshDisplay(); } }
        public double Width { get => _width; set { _width = value; OnPropertyChanged(); RefreshDisplay(); } }
        public double Height { get => _height; set { _height = value; OnPropertyChanged(); RefreshDisplay(); } }

        // ★復活：しっぽのY座標（縦方向はピクセルのまま）
        private double _targetY = 150;
        public double TargetY { get => _targetY; set { _targetY = value; OnPropertyChanged(); RefreshDisplay(); } }

        private bool _isCallout = false, _isSelected = false;
        public bool IsCallout { get => _isCallout; set { _isCallout = value; OnPropertyChanged(); OnPropertyChanged(nameof(CalloutVisibility)); RefreshTail(); } }
        
        [JsonIgnore] public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }
        [JsonIgnore] public Visibility CalloutVisibility => IsCallout ? Visibility.Visible : Visibility.Collapsed;
        [JsonIgnore] public string TailPathData { get; private set; } = "";

        private double CalculateX(DateTime dt)
        {
            if (GetProjectStart == null || GetGridInterval == null) return 0;
            return ((dt - GetProjectStart()).TotalHours / GetGridInterval().TotalHours) * GanttSettings.DayWidth;
        }

        public void RefreshDisplay()
        {
            OnPropertyChanged(nameof(X));
            OnPropertyChanged(nameof(TargetX));
            RefreshTail();
        }

        public void RefreshTail()
        {
            if (!IsCallout) return;
            double cx = X + Width / 2, cy = Y + Height / 2;
            double dx = TargetX - cx, dy = TargetY - cy;
            double len = Math.Sqrt(dx * dx + dy * dy);
            
            if (len < 1) { TailPathData = ""; OnPropertyChanged(nameof(TailPathData)); return; }
            
            double nx = dx / len, ny = dy / len;
            double px = -ny * 12, py = nx * 12;
            TailPathData = $"M {cx + px},{cy + py} L {TargetX},{TargetY} L {cx - px},{cy - py} Z";
            OnPropertyChanged(nameof(TailPathData));
        }
    }
}