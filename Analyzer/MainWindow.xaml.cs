using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Shell;

namespace Analyzer;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            CaptionHeight = 0,
            CornerRadius = new CornerRadius(0),
            GlassFrameThickness = new Thickness(0),
            UseAeroCaptionButtons = false
        });


        MSBuildLocator.RegisterDefaults(); // важно вызвать один раз
        MainFrame.Navigate(new OpenPage());
    }

    private bool _isDragging = false;
    private Point _dragStartScreenPos;
    private bool _isMouseDown = false;

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        _isMouseDown = true;
        _dragStartScreenPos = PointToScreen(e.GetPosition(this));
    }

    private void TitleBar_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isMouseDown && e.LeftButton == MouseButtonState.Pressed)
        {
            Point currentPos = PointToScreen(e.GetPosition(this));
            Vector diff = currentPos - _dragStartScreenPos;

            if (!_isDragging && (Math.Abs(diff.X) > 5 || Math.Abs(diff.Y) > 5))
            {
                _isDragging = true;

                if (WindowState == WindowState.Maximized)
                {
                    double percentX = e.GetPosition(this).X / ActualWidth;
                    double targetX = currentPos.X - (RestoreBounds.Width * percentX);
                    double targetY = currentPos.Y - 10; // отступ от верха

                    WindowState = WindowState.Normal;
                    Left = targetX;
                    Top = targetY;
                }

                DragMove();
            }
        }
    }

    private void TitleBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        _isMouseDown = false;
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }



    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        this.WindowState = WindowState.Minimized;
    }

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        this.WindowState = (this.WindowState == WindowState.Normal)
            ? WindowState.Maximized
            : WindowState.Normal;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }
}