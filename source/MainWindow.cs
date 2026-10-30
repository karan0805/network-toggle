using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace NetworkToggle
{
    public class MainWindow : Window
    {
        const double SurfaceWidth = 400;
        const double BaseHeight = 284;
        const double RowHeight = 60;
        const double CornerRadius = 30;
        const int WifiNameInterval = 5;
        const int MessageSeconds = 8;
        const string BackupNote = "The other connections stay ready as a backup.";

        const string EthernetIcon = "";
        const string WifiIcon = "";
        const string PhoneIcon = "";

        static readonly Color EthernetGlow = Color.FromArgb(0xB3, 0x2F, 0x86, 0xFF);
        static readonly Color WifiGlow = Color.FromArgb(0xB3, 0x22, 0xC5, 0x8B);
        static readonly Color TetheringGlow = Color.FromArgb(0xB3, 0xA7, 0x6B, 0xFA);
        static readonly Color IdleGlow = Color.FromArgb(0x4D, 0x9A, 0xA0, 0xAE);

        class Row
        {
            public Link Link;
            public Button Button;
            public TextBlock State;
        }

        readonly Grid surface;
        readonly TextBlock statusLabel, statusName, statusDetail, noteText;
        readonly TextBlock downValue, downUnit, upValue, upUnit, linkValue, linkUnit;
        readonly StackPanel rowsPanel;
        readonly Border thumb;
        readonly TranslateTransform thumbMove = new TranslateTransform();
        readonly ScaleTransform thumbStretch = new ScaleTransform();
        readonly GradientStop glowStop = new GradientStop(IdleGlow, 0);
        readonly DispatcherTimer timer;
        readonly Dictionary<string, long[]> lastBytes = new Dictionary<string, long[]>();
        List<Row> rows = new List<Row>();
        DateTime lastSample = DateTime.MinValue;
        int thumbIndex = -1;
        bool thumbPlaced;
        bool switching;
        string wifiName;
        string message;
        DateTime messageExpires = DateTime.MinValue;
        int refreshCount;

        public MainWindow()
        {
            Title = "Network Toggle";
            Width = SurfaceWidth;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            UseLayoutRounding = true;

            surface = LoadSurface();
            Content = surface;

            statusLabel = Find<TextBlock>("StatusLabel");
            statusName = Find<TextBlock>("StatusName");
            statusDetail = Find<TextBlock>("StatusDetail");
            noteText = Find<TextBlock>("NoteText");
            downValue = Find<TextBlock>("DownValue");
            downUnit = Find<TextBlock>("DownUnit");
            upValue = Find<TextBlock>("UpValue");
            upUnit = Find<TextBlock>("UpUnit");
            linkValue = Find<TextBlock>("LinkValue");
            linkUnit = Find<TextBlock>("LinkUnit");
            rowsPanel = Find<StackPanel>("Rows");
            thumb = Find<Border>("Thumb");

            TransformGroup thumbTransform = new TransformGroup();
            thumbTransform.Children.Add(thumbStretch);
            thumbTransform.Children.Add(thumbMove);
            thumb.RenderTransform = thumbTransform;

            RadialGradientBrush glow = new RadialGradientBrush();
            glow.GradientStops.Add(glowStop);
            glow.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
            Find<Ellipse>("Glow").Fill = glow;

            noteText.Text = BackupNote;
            surface.MouseLeftButtonDown += delegate { DragMove(); };
            Find<Button>("CloseButton").Click += delegate { Close(); };
            Find<Button>("MinimizeButton").Click += delegate { WindowState = WindowState.Minimized; };
            Find<Button>("RestoreButton").Click += delegate { RestoreDefaults(); };

            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += delegate { RefreshStatus(); };
            Closed += delegate { timer.Stop(); };

            SourceInitialized += delegate
            {
                Glass.BlurBehind(new WindowInteropHelper(this).Handle);
                FitToRows();
            };

            Loaded += delegate
            {
                RefreshStatus();
                timer.Start();
                BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, Duration(260)));
            };

            SyncRows(Network.Links());
        }

        static Grid LoadSurface()
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("MainWindow.xaml"))
            {
                return (Grid)XamlReader.Load(stream);
            }
        }

        T Find<T>(string name) where T : class
        {
            return (T)surface.FindName(name);
        }

        static Duration Duration(int milliseconds)
        {
            return new Duration(TimeSpan.FromMilliseconds(milliseconds));
        }

        void FitToRows()
        {
            double height = BaseHeight + RowHeight * rows.Count;
            Height = height;
            surface.Height = height;
            surface.Clip = new RectangleGeometry(new Rect(0, 0, SurfaceWidth, height), CornerRadius, CornerRadius);

            IntPtr handle = new WindowInteropHelper(this).Handle;
            if (handle == IntPtr.Zero) return;
            double scale = HwndSource.FromHwnd(handle).CompositionTarget.TransformToDevice.M11;
            Glass.RoundCorners(handle, SurfaceWidth, height, CornerRadius, scale);
        }

        void SyncRows(List<Link> links)
        {
            bool sameRows = links.Select(l => l.Id).SequenceEqual(rows.Select(r => r.Link.Id));
            if (sameRows)
            {
                for (int i = 0; i < rows.Count; i++) rows[i].Link = links[i];
                return;
            }

            rows = links.Select(MakeRow).ToList();
            rowsPanel.Children.Clear();
            foreach (Row row in rows) rowsPanel.Children.Add(row.Button);
            FitToRows();
        }

        Row MakeRow(Link link)
        {
            Row row = new Row { Link = link };

            TextBlock icon = new TextBlock
            {
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 20,
                Text = IconFor(link.Kind),
                VerticalAlignment = VerticalAlignment.Center
            };

            TextBlock title = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI Semibold"),
                FontSize = 14,
                Text = link.Title
            };

            row.State = new TextBlock
            {
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromArgb(0xBF, 0xFF, 0xFF, 0xFF)),
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            StackPanel text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };
            text.Children.Add(title);
            text.Children.Add(row.State);

            DockPanel content = new DockPanel { Margin = new Thickness(22, 0, 22, 0) };
            content.Children.Add(icon);
            content.Children.Add(text);

            row.Button = new Button
            {
                Height = RowHeight,
                Style = (Style)surface.Resources["SegmentButton"],
                Content = content
            };
            row.Button.Click += delegate { SwitchTo(row.Link); };
            return row;
        }

        static string IconFor(LinkKind kind)
        {
            if (kind == LinkKind.WiFi) return WifiIcon;
            return kind == LinkKind.Tethering ? PhoneIcon : EthernetIcon;
        }

        static Color GlowFor(LinkKind kind)
        {
            if (kind == LinkKind.WiFi) return WifiGlow;
            return kind == LinkKind.Tethering ? TetheringGlow : EthernetGlow;
        }

        void RefreshStatus()
        {
            List<Link> links = Network.Links();
            if (refreshCount++ % WifiNameInterval == 0) RefreshWifiName();
            double seconds = (DateTime.UtcNow - lastSample).TotalSeconds;
            lastSample = DateTime.UtcNow;

            Dictionary<string, double[]> speeds = new Dictionary<string, double[]>();
            foreach (Link link in links.Where(l => l.Connected))
            {
                speeds[link.Id] = MeasureSpeed(link.Adapter, seconds);
            }

            Render(links, Network.ActiveId(), speeds);
        }

        void Render(List<Link> links, string activeId, Dictionary<string, double[]> speeds)
        {
            SyncRows(links);

            foreach (Row row in rows)
            {
                bool isActive = row.Link.Id == activeId;
                row.State.Text = StateText(row.Link, isActive);
                row.Button.IsEnabled = !switching && !isActive;
            }

            if (switching) return;

            Link active = links.FirstOrDefault(l => l.Id == activeId);
            statusLabel.Text = active == null ? "Internet" : "Internet is using";
            statusName.Text = active != null ? active.Title
                : activeId != null ? "Other connection"
                : "Not connected";
            statusDetail.Text = active != null && active.Kind == LinkKind.WiFi ? wifiName : "";

            double[] speed = null;
            if (active != null) speeds.TryGetValue(active.Id, out speed);
            ShowSpeed(downValue, downUnit, speed == null ? -1 : speed[0]);
            ShowSpeed(upValue, upUnit, speed == null ? -1 : speed[1]);
            ShowSpeed(linkValue, linkUnit, active == null ? -1 : active.Speed / 1000000.0);

            MoveThumb(rows.FindIndex(r => r.Link.Id == activeId));
            noteText.Text = DateTime.UtcNow < messageExpires ? message : BackupNote;
        }

        async void RefreshWifiName()
        {
            wifiName = await Task.Run(() => Network.WifiName());
        }

        string StateText(Link link, bool isActive)
        {
            string state = isActive ? "In use" : link.Connected ? "On standby" : "Click to connect";
            bool named = link.Kind == LinkKind.WiFi && link.Connected && wifiName != null;
            return named ? wifiName + " · " + state : state;
        }

        double[] MeasureSpeed(NetworkInterface adapter, double seconds)
        {
            IPInterfaceStatistics stats = adapter.GetIPStatistics();
            long[] now = { stats.BytesReceived, stats.BytesSent };
            long[] previous;
            bool hasPrevious = lastBytes.TryGetValue(adapter.Id, out previous);
            lastBytes[adapter.Id] = now;
            if (!hasPrevious || seconds <= 0 || seconds > 5) return null;

            return new[]
            {
                Math.Max(0, now[0] - previous[0]) * 8 / 1000000.0 / seconds,
                Math.Max(0, now[1] - previous[1]) * 8 / 1000000.0 / seconds
            };
        }

        static void ShowSpeed(TextBlock value, TextBlock unit, double mbps)
        {
            if (mbps < 0)
            {
                value.Text = "–";
                unit.Text = "";
            }
            else if (mbps >= 1000)
            {
                value.Text = (mbps / 1000).ToString("0.#");
                unit.Text = "Gbps";
            }
            else
            {
                value.Text = mbps.ToString(mbps < 10 ? "0.0" : "0");
                unit.Text = "Mbps";
            }
        }

        void MoveThumb(int index)
        {
            if (thumbPlaced && index == thumbIndex) return;

            double target = Math.Max(index, 0) * RowHeight;
            thumb.Opacity = index < 0 ? 0 : 1;

            if (thumbPlaced && thumbIndex >= 0 && index >= 0)
            {
                DoubleAnimation slide = new DoubleAnimation(target, Duration(620))
                {
                    EasingFunction = new ElasticEase { Oscillations = 1, Springiness = 6, EasingMode = EasingMode.EaseOut }
                };
                thumbMove.BeginAnimation(TranslateTransform.YProperty, slide);
                thumbStretch.BeginAnimation(ScaleTransform.ScaleYProperty, Squash(1.18));
                thumbStretch.BeginAnimation(ScaleTransform.ScaleXProperty, Squash(0.96));
            }
            else
            {
                thumbMove.BeginAnimation(TranslateTransform.YProperty, null);
                thumbMove.Y = target;
            }

            Color color = index < 0 ? IdleGlow : GlowFor(rows[index].Link.Kind);
            glowStop.BeginAnimation(GradientStop.ColorProperty, new ColorAnimation(color, Duration(500)));

            thumbIndex = index;
            thumbPlaced = true;
        }

        static DoubleAnimationUsingKeyFrames Squash(double peak)
        {
            DoubleAnimationUsingKeyFrames squash = new DoubleAnimationUsingKeyFrames();
            squash.KeyFrames.Add(new EasingDoubleKeyFrame(peak, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150)),
                new QuadraticEase { EasingMode = EasingMode.EaseOut }));
            squash.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(560)),
                new ElasticEase { Oscillations = 1, Springiness = 5, EasingMode = EasingMode.EaseOut }));
            return squash;
        }

        void ShowMessage(string text)
        {
            message = text;
            messageExpires = DateTime.UtcNow.AddSeconds(MessageSeconds);
            noteText.Text = text;
        }

        async void SwitchTo(Link target)
        {
            if (switching) return;

            if (!target.Connected)
            {
                ShowMessage(Network.OpenWifiList());
                return;
            }

            switching = true;
            statusLabel.Text = "Switching to";
            statusName.Text = target.Title;
            statusDetail.Text = "";
            noteText.Text = "";
            MoveThumb(rows.FindIndex(r => r.Link.Id == target.Id));
            RefreshStatus();

            string error = await Task.Run(() => Network.Use(target));

            switching = false;
            ShowMessage(error ?? "Switched. Restart any download that was already running.");
            RefreshWifiName();
            RefreshStatus();
        }

        async void RestoreDefaults()
        {
            if (switching) return;
            switching = true;
            noteText.Text = "";

            await Task.Run(() => Network.RestoreDefaults());

            switching = false;
            ShowMessage("Windows is choosing the connection again.");
            RefreshStatus();
        }
    }
}
