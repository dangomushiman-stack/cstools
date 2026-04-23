using System;
using System.Text.Json.Serialization;
using System.Windows;

namespace GanttChartTool
{
    public class NoteItem : ViewModelBase
    {
        public NoteItem() { }
        
        private string _text = "新しいメモ";
        public string Text { get => _text; set { _text = value; OnPropertyChanged(); } }
        
        private double _x = 100, _y = 50, _width = 150, _height = 80;
        public double X { get => _x; set { _x = value; OnPropertyChanged(); RefreshTail(); } }
        public double Y { get => _y; set { _y = value; OnPropertyChanged(); RefreshTail(); } }
        public double Width { get => _width; set { _width = value; OnPropertyChanged(); RefreshTail(); } }
        public double Height { get => _height; set { _height = value; OnPropertyChanged(); RefreshTail(); } }

        private bool _isCallout = false, _isSelected = false;
        public bool IsCallout { get => _isCallout; set { _isCallout = value; OnPropertyChanged(); OnPropertyChanged(nameof(CalloutVisibility)); RefreshTail(); } }
        
        [JsonIgnore] 
        public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }

        private double _targetX = 50, _targetY = 150;
        public double TargetX { get => _targetX; set { _targetX = value; OnPropertyChanged(); RefreshTail(); } }
        public double TargetY { get => _targetY; set { _targetY = value; OnPropertyChanged(); RefreshTail(); } }

        [JsonIgnore] 
        public Visibility CalloutVisibility => IsCallout ? Visibility.Visible : Visibility.Collapsed;
        
        [JsonIgnore] 
        public string TailPathData { get; private set; } = "";

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