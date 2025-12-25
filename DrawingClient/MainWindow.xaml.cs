using DrawingShared;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.IO;
using System.Windows.Media.Imaging;
using PdfSharp.Pdf;
using PdfSharp.Drawing;

namespace DrawingClient
{
    public partial class MainWindow : Window
    {
        private TcpClient? _tcpClient;
        private NetworkStream? _networkStream;
        private bool _isDrawing;
        private Point _lastPoint;
        private Point _startPoint;
        private CancellationTokenSource? _cts;
        private string _currentTool = "Brush";
        private double _currentBrushSize = 3.0;
        private Shape? _previewShape;
        private UIElement? _selectedShape;
        private Brush? _originalStrokeBrush;
        private bool _isDraggingShape;
        private Point _dragStartPoint;
        private bool _isConnected = false;

        public MainWindow()
        {
            try
            {
                InitializeComponent();
                
                // Set initial cursor
                DrawingCanvas.Cursor = Cursors.Pen;
                
                // Force window to foreground
                this.Topmost = true;
                this.Activate();
                this.Focus();
                
                // Log to console for debugging
                Console.WriteLine("DrawingClient GUI initialized successfully");
                
                // Show window explicitly
                this.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing window: {ex.Message}\n{ex.StackTrace}", "Initialization Error");
                throw;
            }
        }
        
        private void ToolPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ToolPicker?.SelectedItem is ComboBoxItem item && item.Tag is string tool)
            {
                _currentTool = tool;
                
                // Change cursor based on tool (only if DrawingCanvas is initialized)
                if (DrawingCanvas != null)
                {
                    DrawingCanvas.Cursor = tool switch
                    {
                        "Pointer" => Cursors.Arrow,
                        "Eraser" => Cursors.Hand,
                        _ => Cursors.Cross
                    };
                }
            }
        }
        
        private void BrushSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (e != null)
            {
                _currentBrushSize = e.NewValue;
                if (BrushSizeText != null)
                {
                    BrushSizeText.Text = ((int)_currentBrushSize).ToString();
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Auto-connect removed. User must click Connect.
        }

        private async void Connect_Click(object sender, RoutedEventArgs e)
        {
            if (_isConnected)
            {
                MessageBox.Show("Already connected to server.", "Connection Status", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var serverIp = ServerIpInput.Text.Trim();
            var serverPortText = ServerPortInput.Text.Trim();

            if (string.IsNullOrEmpty(serverIp))
            {
                MessageBox.Show("Please enter the server IP address.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(serverPortText, out int serverPort) || serverPort <= 0 || serverPort > 65535)
            {
                MessageBox.Show("Please enter a valid port number (1-65535).", "Invalid Port", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await ConnectToServerAsync(serverIp, serverPort);
        }

        private async void Disconnect_Click(object sender, RoutedEventArgs e)
        {
            if (!_isConnected)
            {
                MessageBox.Show("Not connected to any server.", "Connection Status", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            await DisconnectFromServerAsync();
            MessageBox.Show("Disconnected from server successfully.", "Disconnected", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task ConnectToServerAsync(string serverIp, int serverPort)
        {
            try
            {
                StatusText.Text = "Connecting...";
                StatusText.Foreground = Brushes.Orange;

                _tcpClient = new TcpClient();
                _cts = new CancellationTokenSource();

                // Connect to the TCP server
                await _tcpClient.ConnectAsync(serverIp, serverPort);
                _networkStream = _tcpClient.GetStream();
                _isConnected = true;

                StatusText.Text = $"Connected to {serverIp}:{serverPort}";
                StatusText.Foreground = Brushes.Green;

                // Start receiving messages from server
                _ = ReceiveMessagesAsync();
            }
            catch (Exception ex)
            {
                StatusText.Text = "Connection Failed";
                StatusText.Foreground = Brushes.Red;
                _isConnected = false;
                MessageBox.Show($"Failed to connect to server:\n\n{ex.Message}\n\nPlease check:\n• Server is running\n• IP address is correct\n• Port number is correct\n• Firewall settings", 
                    "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task DisconnectFromServerAsync()
        {
            try
            {
                _cts?.Cancel();
                _networkStream?.Close();
                _tcpClient?.Close();
                _isConnected = false;

                StatusText.Text = "Disconnected";
                StatusText.Foreground = Brushes.Red;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during disconnect: {ex.Message}", "Disconnect Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async Task ReceiveMessagesAsync()
        {
            var buffer = new byte[8192]; // Larger buffer for drawing data

            try
            {
                while (_isConnected && _networkStream != null && _tcpClient != null && _tcpClient.Connected)
                {
                    int bytesRead = await _networkStream.ReadAsync(buffer, 0, buffer.Length, _cts?.Token ?? CancellationToken.None);

                    if (bytesRead == 0)
                    {
                        // Server closed the connection
                        break;
                    }

                    // Deserialize the received drawing event
                    var json = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    var drawEvent = JsonSerializer.Deserialize<DrawEvent>(json);

                    if (drawEvent != null)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            if (drawEvent.Type == EventType.Clear)
                            {
                                DrawingCanvas.Children.Clear();
                            }
                            else if (drawEvent.Type == EventType.Erase)
                            {
                                EraseAtPoint(new Point(drawEvent.StartX, drawEvent.StartY));
                            }
                            else
                            {
                                DrawShape(drawEvent);
                            }
                        });
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    if (_isConnected)
                    {
                        StatusText.Text = "Connection Lost";
                        StatusText.Foreground = Brushes.Red;
                        _isConnected = false;
                        MessageBox.Show($"Lost connection to server:\n{ex.Message}", "Connection Lost", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                });
            }
        }

        private void DrawingCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var currentPoint = e.GetPosition(DrawingCanvas);

                // Pointer tool - allows selection and moving shapes only
                if (_currentTool == "Pointer")
                {
                    // Find shape at click position
                    var hitShape = FindShapeAtPoint(currentPoint);
                    if (hitShape != null)
                    {
                        // Deselect previously selected shape
                        if (_selectedShape != null && _selectedShape is Shape prevShape && _selectedShape != hitShape)
                        {
                            if (_originalStrokeBrush != null)
                            {
                                prevShape.Stroke = _originalStrokeBrush;
                            }
                            prevShape.StrokeThickness -= 2;
                        }

                        // Select shape and prepare for dragging
                        _selectedShape = hitShape;
                        if (hitShape is Shape shape)
                        {
                            if (_selectedShape != hitShape || _originalStrokeBrush == null)
                            {
                                _originalStrokeBrush = shape.Stroke;
                                shape.Stroke = Brushes.Blue; // Highlight selected shape
                                shape.StrokeThickness += 2;
                            }
                            
                            var shapeType = GetShapeTypeName(shape);
                            StatusText.Text = $"Selected: {shapeType} at ({currentPoint.X:F0}, {currentPoint.Y:F0})";
                            StatusText.Foreground = Brushes.Blue;
                            
                            // Start dragging
                            _isDraggingShape = true;
                            _dragStartPoint = currentPoint;
                            DrawingCanvas.CaptureMouse();
                        }
                    }
                    else
                    {
                        // Deselect if clicking on empty space
                        if (_selectedShape != null && _selectedShape is Shape prevShape)
                        {
                            if (_originalStrokeBrush != null)
                            {
                                prevShape.Stroke = _originalStrokeBrush;
                            }
                            prevShape.StrokeThickness -= 2;
                            _selectedShape = null;
                            _originalStrokeBrush = null;
                        }
                        
                        StatusText.Text = _isConnected ? "Connected" : "Disconnected";
                        StatusText.Foreground = _isConnected ? Brushes.Green : Brushes.Red;
                    }
                    return; // Pointer tool never draws
                }

                // Eraser tool - erase shapes at click position
                if (_currentTool == "Eraser")
                {
                    EraseAtPoint(currentPoint);
                }

                _isDrawing = true;
                _startPoint = currentPoint;
                _lastPoint = _startPoint;
                DrawingCanvas.CaptureMouse();
            }
        }

        private void DrawingCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            var currentPoint = e.GetPosition(DrawingCanvas);

            // Pointer tool - handle shape dragging
            if (_currentTool == "Pointer" && _isDraggingShape && e.LeftButton == MouseButtonState.Pressed && _selectedShape != null)
            {
                var deltaX = currentPoint.X - _dragStartPoint.X;
                var deltaY = currentPoint.Y - _dragStartPoint.Y;
                
                if (_selectedShape is Line line)
                {
                    line.X1 += deltaX;
                    line.Y1 += deltaY;
                    line.X2 += deltaX;
                    line.Y2 += deltaY;
                }
                else if (_selectedShape is Shape shape)
                {
                    var margin = shape.Margin;
                    shape.Margin = new Thickness(
                        margin.Left + deltaX,
                        margin.Top + deltaY,
                        margin.Right,
                        margin.Bottom
                    );
                }
                
                _dragStartPoint = currentPoint;
                return;
            }

            // Eraser works even without mouse down initially
            if (_currentTool == "Eraser" && e.LeftButton == MouseButtonState.Pressed)
            {
                EraseAtPoint(currentPoint);
                return;
            }

            if (!_isDrawing || e.LeftButton != MouseButtonState.Pressed)
                return;

            var colorTag = ((ComboBoxItem)ColorPicker.SelectedItem)?.Tag?.ToString() ?? "#000000";

            // Only Brush tool supports freehand drawing
            if (_currentTool == "Brush")
            {
                // Create continuous brush strokes
                var drawEvent = new DrawEvent
                {
                    Type = EventType.Draw,
                    Shape = ShapeType.Brush,
                    StartX = _lastPoint.X,
                    StartY = _lastPoint.Y,
                    EndX = currentPoint.X,
                    EndY = currentPoint.Y,
                    Color = colorTag,
                    Width = _currentBrushSize
                };

                DrawShape(drawEvent);
                _ = SendDrawEventAsync(drawEvent);
                _lastPoint = currentPoint;
            }
            else
            {
                // Preview shape while dragging
                if (_previewShape != null)
                {
                    DrawingCanvas.Children.Remove(_previewShape);
                }

                var drawEvent = new DrawEvent
                {
                    Type = EventType.Draw,
                    Shape = GetShapeType(_currentTool),
                    StartX = _startPoint.X,
                    StartY = _startPoint.Y,
                    EndX = currentPoint.X,
                    EndY = currentPoint.Y,
                    Color = colorTag,
                    Width = _currentBrushSize
                };

                _previewShape = CreateShapeVisual(drawEvent);
                if (_previewShape != null)
                {
                    DrawingCanvas.Children.Add(_previewShape);
                }
            }
        }

        private void DrawingCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            // Handle Pointer tool shape dragging end
            if (_isDraggingShape)
            {
                _isDraggingShape = false;
                DrawingCanvas.ReleaseMouseCapture();
                return;
            }

            if (_isDrawing)
            {
                var currentPoint = e.GetPosition(DrawingCanvas);
                var colorTag = ((ComboBoxItem)ColorPicker.SelectedItem)?.Tag?.ToString() ?? "#000000";

                // For shapes (not brush, pointer, or eraser), send final shape
                if (_currentTool != "Brush" && _currentTool != "Pointer" && _currentTool != "Eraser")
                {
                    // Remove preview
                    if (_previewShape != null)
                    {
                        DrawingCanvas.Children.Remove(_previewShape);
                        _previewShape = null;
                    }

                    // Send final shape
                    var drawEvent = new DrawEvent
                    {
                        Type = EventType.Draw,
                        Shape = GetShapeType(_currentTool),
                        StartX = _startPoint.X,
                        StartY = _startPoint.Y,
                        EndX = currentPoint.X,
                        EndY = currentPoint.Y,
                        Color = colorTag,
                        Width = _currentBrushSize
                    };

                    DrawShape(drawEvent);
                    _ = SendDrawEventAsync(drawEvent);
                }

                _isDrawing = false;
                DrawingCanvas.ReleaseMouseCapture();
            }
        }

        private ShapeType GetShapeType(string tool)
        {
            return tool switch
            {
                "Brush" => ShapeType.Brush,
                "Eraser" => ShapeType.Eraser,
                "Circle" => ShapeType.Circle,
                "Square" => ShapeType.Square,
                "Rectangle" => ShapeType.Rectangle,
                "Triangle" => ShapeType.Triangle,
                "Pointer" => ShapeType.Pointer,
                _ => ShapeType.Brush
            };
        }

        private UIElement? FindShapeAtPoint(Point point)
        {
            
            for (int i = DrawingCanvas.Children.Count - 1; i >= 0; i--)
            {
                var child = DrawingCanvas.Children[i];
                if (IsPointInShape(child, point))
                {
                    return child;
                }
            }
            return null;
        }

        private string GetShapeTypeName(Shape shape)
        {
            return shape switch
            {
                Line => "Brush Stroke",
                Ellipse => "Circle",
                System.Windows.Shapes.Rectangle => "Rectangle/Square",
                Polygon => "Triangle",
                _ => "Shape"
            };
        }

        private void EraseAtPoint(Point point)
        {
            
            var shapesToRemove = new List<UIElement>();
            
            foreach (UIElement child in DrawingCanvas.Children)
            {
                if (IsPointInShape(child, point))
                {
                    shapesToRemove.Add(child);
                }
            }

            foreach (var shape in shapesToRemove)
            {
                DrawingCanvas.Children.Remove(shape);
                
                // Send erase event to other clients
                var eraseEvent = new DrawEvent
                {
                    Type = EventType.Erase,
                    Shape = ShapeType.Eraser,
                    StartX = point.X,
                    StartY = point.Y,
                    EndX = point.X,
                    EndY = point.Y,
                    Width = _currentBrushSize * 2
                };
                _ = SendDrawEventAsync(eraseEvent);
            }
        }

        private bool IsPointInShape(UIElement element, Point point)
        {
            if (element is Line line)
            {
              
                var dist = DistanceToLineSegment(point, new Point(line.X1, line.Y1), new Point(line.X2, line.Y2));
                return dist < Math.Max(line.StrokeThickness + 3, 8);
            }
            else if (element is Polygon polygon)
            {
                
                if (polygon.RenderedGeometry != null)
                {
                    return polygon.RenderedGeometry.FillContains(point) || 
                           polygon.RenderedGeometry.StrokeContains(new Pen(Brushes.Black, polygon.StrokeThickness + 3), point);
                }
            }
            else if (element is Shape shape)
            {
                
                if (shape.RenderedGeometry != null)
                {
                    var margin = shape.Margin;
                    var translatedPoint = new Point(point.X - margin.Left, point.Y - margin.Top);
                    
                    return shape.RenderedGeometry.FillContains(translatedPoint) || 
                           shape.RenderedGeometry.StrokeContains(new Pen(Brushes.Black, shape.StrokeThickness + 3), translatedPoint);
                }
            }
            return false;
        }

        private double DistanceToLineSegment(Point point, Point lineStart, Point lineEnd)
        {
            var dx = lineEnd.X - lineStart.X;
            var dy = lineEnd.Y - lineStart.Y;
            var lengthSquared = dx * dx + dy * dy;

            if (lengthSquared == 0)
                return Distance(point, lineStart);

            var t = Math.Max(0, Math.Min(1, ((point.X - lineStart.X) * dx + (point.Y - lineStart.Y) * dy) / lengthSquared));
            var projection = new Point(lineStart.X + t * dx, lineStart.Y + t * dy);
            return Distance(point, projection);
        }

        private double Distance(Point p1, Point p2)
        {
            var dx = p2.X - p1.X;
            var dy = p2.Y - p1.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private void DrawShape(DrawEvent drawEvent)
        {
            var shape = CreateShapeVisual(drawEvent);
            if (shape != null)
            {
                DrawingCanvas.Children.Add(shape);
            }
        }

        private Shape? CreateShapeVisual(DrawEvent drawEvent)
        {
            var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(drawEvent.Color);

            switch (drawEvent.Shape)
            {
                case ShapeType.Brush:
                    return new Line
                    {
                        X1 = drawEvent.StartX,
                        Y1 = drawEvent.StartY,
                        X2 = drawEvent.EndX,
                        Y2 = drawEvent.EndY,
                        Stroke = brush,
                        StrokeThickness = drawEvent.Width,
                        StrokeStartLineCap = PenLineCap.Round,
                        StrokeEndLineCap = PenLineCap.Round
                    };

                case ShapeType.Circle:
                    {
                        var width = Math.Abs(drawEvent.EndX - drawEvent.StartX);
                        var height = Math.Abs(drawEvent.EndY - drawEvent.StartY);
                        var size = Math.Max(width, height);
                        var left = Math.Min(drawEvent.StartX, drawEvent.EndX);
                        var top = Math.Min(drawEvent.StartY, drawEvent.EndY);

                        return new Ellipse
                        {
                            Width = size,
                            Height = size,
                            Stroke = brush,
                            StrokeThickness = drawEvent.Width,
                            Fill = Brushes.Transparent,
                            Margin = new Thickness(left, top, 0, 0)
                        };
                    }

                case ShapeType.Square:
                    {
                        var width = Math.Abs(drawEvent.EndX - drawEvent.StartX);
                        var height = Math.Abs(drawEvent.EndY - drawEvent.StartY);
                        var size = Math.Max(width, height);
                        var left = Math.Min(drawEvent.StartX, drawEvent.EndX);
                        var top = Math.Min(drawEvent.StartY, drawEvent.EndY);

                        return new System.Windows.Shapes.Rectangle
                        {
                            Width = size,
                            Height = size,
                            Stroke = brush,
                            StrokeThickness = drawEvent.Width,
                            Fill = Brushes.Transparent,
                            Margin = new Thickness(left, top, 0, 0)
                        };
                    }

                case ShapeType.Rectangle:
                    {
                        var width = Math.Abs(drawEvent.EndX - drawEvent.StartX);
                        var height = Math.Abs(drawEvent.EndY - drawEvent.StartY);
                        var left = Math.Min(drawEvent.StartX, drawEvent.EndX);
                        var top = Math.Min(drawEvent.StartY, drawEvent.EndY);

                        return new System.Windows.Shapes.Rectangle
                        {
                            Width = width,
                            Height = height,
                            Stroke = brush,
                            StrokeThickness = drawEvent.Width,
                            Fill = Brushes.Transparent,
                            Margin = new Thickness(left, top, 0, 0)
                        };
                    }

                case ShapeType.Triangle:
                    {
                        var points = new PointCollection
                        {
                            new Point(drawEvent.StartX, drawEvent.EndY), // Bottom-left
                            new Point((drawEvent.StartX + drawEvent.EndX) / 2, drawEvent.StartY), // Top-center
                            new Point(drawEvent.EndX, drawEvent.EndY) // Bottom-right
                        };

                        return new Polygon
                        {
                            Points = points,
                            Stroke = brush,
                            StrokeThickness = drawEvent.Width,
                            Fill = Brushes.Transparent
                        };
                    }

                default:
                    return null;
            }
        }

        private async void Clear_Click(object sender, RoutedEventArgs e)
        {
            DrawingCanvas.Children.Clear();
            var clearEvent = new DrawEvent { Type = EventType.Clear };
            await SendDrawEventAsync(clearEvent);
        }

        private async Task SendDrawEventAsync(DrawEvent drawEvent)
        {
            if (_isConnected && _networkStream != null && _tcpClient != null && _tcpClient.Connected)
            {
                try
                {
                    var json = JsonSerializer.Serialize(drawEvent);
                    var buffer = Encoding.UTF8.GetBytes(json);
                    await _networkStream.WriteAsync(buffer, 0, buffer.Length);
                    await _networkStream.FlushAsync();
                }
                catch (Exception)
                {
                    // Connection lost, update UI
                    Dispatcher.Invoke(() =>
                    {
                        StatusText.Text = "Connection Lost";
                        StatusText.Foreground = Brushes.Red;
                        _isConnected = false;
                    });
                }
            }
        }

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            await DisconnectFromServerAsync();
        }

        private void Download_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get the Downloads folder path
                string downloadsPath =  System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                string fileName = $"Whiteboard_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                string filePath = System.IO.Path.Combine(downloadsPath, fileName);

                // Create PDF document
                PdfDocument document = new PdfDocument();
                document.Info.Title = "Whiteboard Drawing";
                
                PdfPage page = document.AddPage();
                page.Width = XUnit.FromPoint(DrawingCanvas.ActualWidth);
                page.Height = XUnit.FromPoint(DrawingCanvas.ActualHeight);

                using (XGraphics gfx = XGraphics.FromPdfPage(page))
                {
                    // Render canvas to bitmap
                    RenderTargetBitmap renderBitmap = new RenderTargetBitmap(
                        (int)DrawingCanvas.ActualWidth,
                        (int)DrawingCanvas.ActualHeight,
                        96d, 96d,
                        PixelFormats.Pbgra32);
                    
                    renderBitmap.Render(DrawingCanvas);

                    // Save bitmap to memory stream
                    BitmapEncoder encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(renderBitmap));
                    
                    using (MemoryStream memoryStream = new MemoryStream())
                    {
                        encoder.Save(memoryStream);
                        memoryStream.Position = 0;
                        
                        // Draw image to PDF
                        XImage image = XImage.FromStream(memoryStream);
                        gfx.DrawImage(image, 0, 0, page.Width, page.Height);
                    }
                }

                // Save PDF
                document.Save(filePath);
                document.Close();

                MessageBox.Show($"Drawing saved successfully!\n\nLocation: {filePath}", 
                    "Download Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save drawing: {ex.Message}", 
                    "Download Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}