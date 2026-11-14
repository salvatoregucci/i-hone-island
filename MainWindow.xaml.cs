using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using LibreHardwareMonitor.Hardware;
using Microsoft.VisualBasic.Devices;
using PerformanceCounter = System.Diagnostics.PerformanceCounter;

namespace iLand;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer timer;
    private readonly Computer computer;
    private readonly PerformanceCounter cpuCounter;
    private bool expanded;
    private int currentPage;
    private double swipeStart;
    private bool mouseDown;

    private const double CompactWidth = 190;
    private const double CompactHeight = 44;
    private const double ExpandedWidth = 410;
    private const double ExpandedHeight = 290;

    public MainWindow()
    {
        InitializeComponent();

        Left = (SystemParameters.PrimaryScreenWidth - CompactWidth) / 2;
        Top = 12;

        computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsMotherboardEnabled = true
        };

        try
        {
            computer.Open();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not open hardware sensors: {ex.Message}");
        }

        cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        cpuCounter.NextValue();

        timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => UpdateHardware();
        timer.Start();
        UpdateHardware();
    }

    private void AnimateIsland(double width, double height)
    {
        double center = Left + Width / 2;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = TimeSpan.FromMilliseconds(380);

        BeginAnimation(WidthProperty, new DoubleAnimation(width, duration) { EasingFunction = ease });
        BeginAnimation(HeightProperty, new DoubleAnimation(height, duration) { EasingFunction = ease });
        BeginAnimation(LeftProperty, new DoubleAnimation(center - width / 2, duration) { EasingFunction = ease });
    }

    private void Expand()
    {
        if (expanded) return;

        expanded = true;
        ExpandedContent.Visibility = Visibility.Visible;
        AnimateIsland(ExpandedWidth, ExpandedHeight);
    }

    private void Collapse()
    {
        if (!expanded) return;

        expanded = false;
        AnimateIsland(CompactWidth, CompactHeight);

        var delay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        delay.Tick += (_, _) =>
        {
            delay.Stop();
            if (!expanded) ExpandedContent.Visibility = Visibility.Collapsed;
        };
        delay.Start();
    }

    private void Island_MouseEnter(object sender, MouseEventArgs e) => Expand();

    private void Island_MouseLeave(object sender, MouseEventArgs e)
    {
        var delay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        delay.Tick += (_, _) =>
        {
            delay.Stop();
            if (!Island.IsMouseOver) Collapse();
        };
        delay.Start();
    }

    private void ChangePage(int direction)
    {
        currentPage = (currentPage + direction + 3) % 3;
        HomePage.Visibility = Visibility.Collapsed;
        MonitorPage.Visibility = Visibility.Collapsed;
        TemperaturePage.Visibility = Visibility.Collapsed;

        switch (currentPage)
        {
            case 0:
                HomePage.Visibility = Visibility.Visible;
                IslandTitle.Text = "iLand";
                break;
            case 1:
                MonitorPage.Visibility = Visibility.Visible;
                IslandTitle.Text = "Мониторинг";
                break;
            case 2:
                TemperaturePage.Visibility = Visibility.Visible;
                IslandTitle.Text = "Температуры";
                break;
        }

        PageIndicator.Text = $"{currentPage + 1} / 3";
    }

    private void PreviousPage(object sender, RoutedEventArgs e) => ChangePage(-1);
    private void NextPage(object sender, RoutedEventArgs e) => ChangePage(1);

    private void Island_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        ChangePage(e.Delta > 0 ? -1 : 1);
        e.Handled = true;
    }

    private void Island_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        swipeStart = e.GetPosition(this).X;
        mouseDown = true;
        CaptureMouse();
    }

    private void Island_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!mouseDown) return;

        double difference = e.GetPosition(this).X - swipeStart;
        if (Math.Abs(difference) > 65 && expanded)
            ChangePage(difference > 0 ? -1 : 1);

        mouseDown = false;
        ReleaseMouseCapture();
    }

    private void UpdateHardware()
    {
        try
        {
            double cpu = cpuCounter.NextValue();
            CpuText.Text = $"CPU  {cpu:F0}%";
            CpuBar.Value = Math.Clamp(cpu, 0, 100);

            var memory = new ComputerInfo();
            double total = memory.TotalPhysicalMemory;
            double available = memory.AvailablePhysicalMemory;
            double ram = total > 0 ? (total - available) / total * 100 : 0;
            RamText.Text = $"RAM  {ram:F0}%";
            RamBar.Value = Math.Clamp(ram, 0, 100);

            double? cpuTemp = null;
            double? gpuTemp = null;
            double? gpuLoad = null;
            double? vram = null;
            var fanSpeeds = new List<double>();

            foreach (var hardware in computer.Hardware)
            {
                hardware.Update();
                foreach (var sensor in hardware.Sensors)
                {
                    if (sensor.Value is not double value) continue;

                    if (hardware.HardwareType == HardwareType.Cpu && sensor.SensorType == SensorType.Temperature &&
                        (cpuTemp is null || sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase)))
                        cpuTemp = value;

                    bool isGpu = hardware.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel;
                    if (isGpu && sensor.SensorType == SensorType.Temperature &&
                        (gpuTemp is null || sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase)))
                        gpuTemp = value;

                    if (isGpu && sensor.SensorType == SensorType.Load &&
                        sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase))
                        gpuLoad = value;

                    if (isGpu && sensor.SensorType == SensorType.SmallData &&
                        sensor.Name.Contains("Memory", StringComparison.OrdinalIgnoreCase))
                        vram = value;

                    if (sensor.SensorType == SensorType.Fan)
                        fanSpeeds.Add(value);
                }
            }

            CpuTempText.Text = cpuTemp.HasValue ? $"CPU  {cpuTemp:F0} °C" : "CPU  Датчик недоступен";
            GpuTempText.Text = gpuTemp.HasValue ? $"GPU  {gpuTemp:F0} °C" : "GPU  Датчик недоступен";
            GpuText.Text = gpuLoad.HasValue ? $"GPU  {gpuLoad:F0}%" : "GPU  --";
            GpuBar.Value = gpuLoad.HasValue ? Math.Clamp(gpuLoad.Value, 0, 100) : 0;
            VramText.Text = vram.HasValue ? $"VRAM  {vram:F0} MB" : "VRAM  --";
            FanText.Text = fanSpeeds.Count > 0
                ? $"Вентиляторы  {string.Join(" / ", fanSpeeds.Select(x => $"{x:F0} RPM"))}"
                : "Вентиляторы  Недоступны";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Hardware update failed: {ex.Message}");
        }

        ClockText.Text = DateTime.Now.ToString("HH:mm:ss");
        DateText.Text = DateTime.Now.ToString("dddd, dd MMMM");
    }

    protected override void OnClosed(EventArgs e)
    {
        timer.Stop();
        try { computer.Close(); }
        catch (Exception ex) { Debug.WriteLine($"Could not close hardware monitor: {ex.Message}"); }
        cpuCounter.Dispose();
        base.OnClosed(e);
    }
}
