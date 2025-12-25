namespace DrawingShared
{
    public enum EventType { Draw, Clear, Erase }
    public enum ShapeType { Brush, Circle, Square, Rectangle, Triangle, Pointer, Eraser }

    public class DrawEvent
    {
        public EventType Type { get; set; } = EventType.Draw;
        public ShapeType Shape { get; set; } = ShapeType.Brush;
        public double StartX { get; set; }
        public double StartY { get; set; }
        public double EndX { get; set; }
        public double EndY { get; set; }
        public string Color { get; set; } = "#000000";
        public double Width { get; set; } = 2.0;
    }
}
