using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Media;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;

namespace AntigravityPet
{
    public enum PetState
    {
        Idle = 0,
        Running = 1,
        Ready = 2,
        Blocked = 3,
        NeedsInput = 4
    }

    public enum TransientAction
    {
        None,
        Wave,
        Jump,
        Walk
    }

    public class PetConfig
    {
        public bool Enabled { get; set; }
        public string Skin { get; set; }
        public double Left { get; set; }
        public double Top { get; set; }
        public bool Topmost { get; set; }
        public string BubbleMode { get; set; } // always | alerts | off
        public double Scale { get; set; }       // 0.75 | 1.0 | 1.3 | 1.5
        public bool AutoWalk { get; set; }
        public bool EyeFollow { get; set; }
        public bool AutoStartOnBoot { get; set; }
        public bool SoundAlert { get; set; }
        public string Language { get; set; }           // auto | en | zh
        public string AgyDetectedLocale { get; set; }
        public int CompletedTasksCount { get; set; }
        public int ToolCallsCount { get; set; }

        public PetConfig()
        {
            Enabled = true;
            Skin = "cyber-fox";
            Left = -1;
            Top = -1;
            Topmost = true;
            BubbleMode = "always";
            Scale = 1.0;
            AutoWalk = true;
            EyeFollow = true;
            AutoStartOnBoot = true;
            SoundAlert = false;
            Language = "auto";
            AgyDetectedLocale = "";
            CompletedTasksCount = 0;
            ToolCallsCount = 0;
        }
    }

    public class PetPack
    {
        public string Id;
        public string DisplayName;
        public string Description;
        public string FolderPath;
        public int SpriteVersion;

        public List<BitmapSource>[] Rows = new List<BitmapSource>[9];
        public List<BitmapSource> LookFrames = new List<BitmapSource>();
    }

    public class TaskItem
    {
        public string ConversationId;
        public string Title;
        public PetState State;
        public string Message;
        public DateTime UpdatedAt;
    }

    public class RecentConversation
    {
        public string ConversationId;
        public string Title;
        public string State;
        public string UpdatedAt;
    }

    public class PetWindow : Window
    {
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);
        [DllImport("user32.dll")] private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT lpPoint);
        [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int SW_RESTORE = 9;
        private const int SW_SHOW = 5;
        private const int PORT = 47821;
        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_ID = 0xA970;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_WIN = 0x0008;
        private const uint MOD_NOREPEAT = 0x4000;
        private const uint VK_P = 0x50;

        private const double BaseWidth = 236.0;
        private const double BaseHeight = 220.0;
        private const double BaseSpriteBox = 136.0;

        private readonly Grid _rootGrid;
        private readonly Border _bubbleBorder;
        private readonly TextBlock _bubbleText;
        private readonly Grid _stageGrid;
        private readonly Ellipse _statusHalo;
        private readonly Ellipse _floorShadow;
        private readonly Image _spriteImage;
        private readonly TextBlock _fallbackLabel;
        private readonly Border _badgeBorder;
        private readonly TextBlock _badgeText;

        private readonly DispatcherTimer _animTimer;
        private readonly DispatcherTimer _behaviorTimer;
        private readonly DispatcherTimer _autoIdleTimer;
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer();
        private readonly Random _rng = new Random();
        private readonly DateTime _sessionStart = DateTime.Now;

        private readonly Dictionary<string, PetPack> _packs = new Dictionary<string, PetPack>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TaskItem> _tasks = new Dictionary<string, TaskItem>(StringComparer.OrdinalIgnoreCase);
        private readonly List<RecentConversation> _recentConversations = new List<RecentConversation>();

        private HttpListener _listener;
        private Thread _httpThread;
        private volatile bool _isClosing = false;
        private bool _sawAntigravityProcess = false;
        private int _agyMissingTicks = 0;

        private HwndSource _hwndSource;

        private PetConfig _config;
        private readonly string _configPath;
        private readonly string _pluginPetsDir;
        private readonly string _userPetsDir;
        private readonly string _exePath;

        private PetPack _currentPack;
        private PetState _state = PetState.Idle;
        private string _statusMessage = "";
        private string _activeConversationId = "default";
        private string _lastConversationId = "default";
        private string _activeTitle = "";

        private TransientAction _transient = TransientAction.None;
        private int _transientFramesLeft = 0;
        private DateTime _lastWaveAt = DateTime.MinValue;
        private DateTime _nextWalkAt = DateTime.Now.AddSeconds(16);
        private double _walkTargetLeft = 0;
        private int _walkDir = 1;

        private int _animFrame = 0;
        private double _animTickAcc = 0;

        private bool _isDragging = false;
        private bool _movedDuringDrag = false;
        private Point _dragStartScreen;
        private Point _dragStartWindow;

        public PetWindow()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string cfgDir = System.IO.Path.Combine(appData, "AntigravityPet");
            Directory.CreateDirectory(cfgDir);
            _configPath = System.IO.Path.Combine(cfgDir, "config.json");
            _config = LoadConfig();
            _config.Enabled = true;

            _exePath = Process.GetCurrentProcess().MainModule.FileName;
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            _pluginPetsDir = System.IO.Path.GetFullPath(System.IO.Path.Combine(exeDir, "..", "pets"));
            _userPetsDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".antigravity-pet", "pets");

            SyncAutoStartRegistry(_config.AutoStartOnBoot);
            SaveConfig();

            Title = "Antigravity Pet";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = _config.Topmost;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;

            _rootGrid = new Grid { Background = Brushes.Transparent };
            _rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Content = _rootGrid;

            // 顶部可点击居中聊天框气泡
            _bubbleText = new TextBlock
            {
                Text = "",
                Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 208
            };
            _bubbleBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(236, 15, 23, 42)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(200, 56, 189, 248)),
                BorderThickness = new Thickness(1.2),
                CornerRadius = new CornerRadius(11),
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(4, 4, 4, 2),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Child = _bubbleText,
                Visibility = Visibility.Hidden,
                IsHitTestVisible = true,
                Cursor = Cursors.Hand,
                ToolTip = "点击打开对应 Antigravity 对话",
                Effect = new DropShadowEffect { BlurRadius = 10, ShadowDepth = 2, Opacity = 0.42 }
            };
            Grid.SetRow(_bubbleBorder, 0);
            _rootGrid.Children.Add(_bubbleBorder);

            // 下方角色舞台
            _stageGrid = new Grid
            {
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 6)
            };
            Grid.SetRow(_stageGrid, 1);
            _rootGrid.Children.Add(_stageGrid);

            _statusHalo = new Ellipse
            {
                Width = 116,
                Height = 116,
                Opacity = 0,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };
            _stageGrid.Children.Add(_statusHalo);

            _floorShadow = new Ellipse
            {
                Width = 74,
                Height = 14,
                Fill = new SolidColorBrush(Color.FromArgb(65, 15, 23, 42)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 4),
                IsHitTestVisible = false
            };
            _stageGrid.Children.Add(_floorShadow);

            _spriteImage = new Image
            {
                Width = BaseSpriteBox,
                Height = BaseSpriteBox,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            RenderOptions.SetBitmapScalingMode(_spriteImage, BitmapScalingMode.NearestNeighbor);
            _stageGrid.Children.Add(_spriteImage);

            _fallbackLabel = new TextBlock
            {
                Text = "🐾",
                FontSize = 48,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            _stageGrid.Children.Add(_fallbackLabel);

            _badgeBorder = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                Background = Brushes.DodgerBlue,
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(2),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 8, 8, 0),
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false,
                Effect = new DropShadowEffect { BlurRadius = 6, ShadowDepth = 1, Opacity = 0.4 }
            };
            _badgeText = new TextBlock
            {
                Text = "⚡",
                Foreground = Brushes.White,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _badgeBorder.Child = _badgeText;
            _stageGrid.Children.Add(_badgeBorder);

            ReloadPetPacks();
            ApplyScale(_config.Scale, false);
            RestoreWindowPosition();

            SetupMouseEvents();
            RebuildContextMenu();

            SourceInitialized += OnSourceInitialized;
            Closing += OnWindowClosing;

            _animTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
            _animTimer.Tick += OnAnimTick;
            _animTimer.Start();

            _behaviorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _behaviorTimer.Tick += OnBehaviorTick;
            _behaviorTimer.Start();

            // 任务完成后气泡保留 5 分钟，确保用户随时点击聊天框跳转对话
            _autoIdleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(300) };
            _autoIdleTimer.Tick += (s, e) =>
            {
                if (_state == PetState.Ready)
                {
                    ClearReminderTasks();
                }
            };

            StartHttpServer();
            UpdateVisualState();
            RenderCurrentFrame();
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);
            }
            _hwndSource = HwndSource.FromHwnd(hwnd);
            if (_hwndSource != null)
            {
                _hwndSource.AddHook(WndProc);
                if (!RegisterHotKey(hwnd, HOTKEY_ID, MOD_WIN | MOD_ALT | MOD_NOREPEAT, VK_P))
                {
                    RegisterHotKey(hwnd, HOTKEY_ID, MOD_WIN | MOD_ALT, VK_P);
                }
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                ToggleVisibility();
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void OnWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _isClosing = true;
            SaveConfig();
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero) UnregisterHotKey(hwnd, HOTKEY_ID);
                if (_hwndSource != null) _hwndSource.RemoveHook(WndProc);
            }
            catch { }
            try { if (_listener != null) _listener.Stop(); } catch { }
        }

        private void SyncAutoStartRegistry(bool enable)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", true))
                {
                    if (key == null) return;
                    if (key.GetValue("AntigravityPet") != null)
                    {
                        key.DeleteValue("AntigravityPet", false);
                    }
                }
            }
            catch { }
        }

        private void RestoreWindowPosition()
        {
            var wa = SystemParameters.WorkArea;
            if (_config.Left >= wa.Left && _config.Top >= wa.Top &&
                _config.Left < wa.Right - 40 && _config.Top < wa.Bottom - 40)
            {
                Left = _config.Left;
                Top = _config.Top;
            }
            else
            {
                ResetToCorner(false);
            }
        }

        private void ResetToCorner(bool save)
        {
            var wa = SystemParameters.WorkArea;
            Left = wa.Right - Width - 28;
            Top = wa.Bottom - Height - 24;
            if (save) SaveConfig();
        }

        private void ApplyScale(double scale, bool save)
        {
            if (scale < 0.5 || scale > 2.0) scale = 1.0;
            _config.Scale = scale;
            Width = Math.Round(BaseWidth * scale);
            Height = Math.Round(BaseHeight * scale);
            _stageGrid.Width = Math.Round(BaseSpriteBox * scale);
            _stageGrid.Height = Math.Round(BaseSpriteBox * scale);
            _spriteImage.Width = Math.Round(BaseSpriteBox * scale);
            _spriteImage.Height = Math.Round(BaseSpriteBox * scale);
            _statusHalo.Width = Math.Round(116 * scale);
            _statusHalo.Height = Math.Round(116 * scale);
            _floorShadow.Width = Math.Round(74 * scale);
            _floorShadow.Height = Math.Round(14 * scale);
            _bubbleText.MaxWidth = Math.Round((BaseWidth - 28) * scale);
            ClampToWorkArea();
            if (save) SaveConfig();
        }

        private void ClampToWorkArea()
        {
            var wa = SystemParameters.WorkArea;
            if (Left < wa.Left) Left = wa.Left;
            if (Top < wa.Top) Top = wa.Top;
            if (Left > wa.Right - Width) Left = Math.Max(wa.Left, wa.Right - Width);
            if (Top > wa.Bottom - Height) Top = Math.Max(wa.Top, wa.Bottom - Height);
        }

        private void ReloadPetPacks()
        {
            _packs.Clear();
            LoadPacksFromDir(_pluginPetsDir);
            LoadPacksFromDir(_userPetsDir);

            string desired = ResolveSkinAlias(_config.Skin);
            if (_packs.ContainsKey(desired))
            {
                _currentPack = _packs[desired];
            }
            else if (_packs.Count > 0)
            {
                _currentPack = _packs.Values.First();
                _config.Skin = _currentPack.Id;
            }
            else
            {
                _currentPack = null;
            }

            if (_currentPack == null)
            {
                _spriteImage.Visibility = Visibility.Collapsed;
                _fallbackLabel.Visibility = Visibility.Visible;
            }
            else
            {
                _spriteImage.Visibility = Visibility.Visible;
                _fallbackLabel.Visibility = Visibility.Collapsed;
            }
        }

        private void LoadPacksFromDir(string rootDir)
        {
            try
            {
                if (!Directory.Exists(rootDir)) return;
                foreach (var dir in Directory.GetDirectories(rootDir))
                {
                    var pack = TryLoadPack(dir);
                    if (pack != null)
                    {
                        _packs[pack.Id] = pack;
                    }
                }
            }
            catch { }
        }

        private PetPack TryLoadPack(string dir)
        {
            try
            {
                string manifestPath = System.IO.Path.Combine(dir, "pet.json");
                if (!File.Exists(manifestPath)) return null;

                var meta = _json.Deserialize<Dictionary<string, object>>(File.ReadAllText(manifestPath, Encoding.UTF8));
                if (meta == null) return null;

                string id = GetStr(meta, "id", System.IO.Path.GetFileName(dir));
                string displayName = GetStr(meta, "displayName", id);
                string desc = GetStr(meta, "description", "");
                int ver = 2;
                if (meta.ContainsKey("spriteVersionNumber") && meta["spriteVersionNumber"] != null)
                {
                    int.TryParse(meta["spriteVersionNumber"].ToString(), out ver);
                }
                if (ver != 1 && ver != 2) ver = 2;

                string sheetRel = GetStr(meta, "spritesheetPath", "spritesheet.png");
                string sheetPath = System.IO.Path.Combine(dir, sheetRel);
                if (!File.Exists(sheetPath))
                {
                    string altPng = System.IO.Path.Combine(dir, "spritesheet.png");
                    string altWebp = System.IO.Path.Combine(dir, "spritesheet.webp");
                    if (File.Exists(altPng)) sheetPath = altPng;
                    else if (File.Exists(altWebp)) sheetPath = altWebp;
                    else return null;
                }

                BitmapSource raw;
                using (var ms = new MemoryStream(File.ReadAllBytes(sheetPath)))
                {
                    raw = BitmapFrame.Create(ms, BitmapCreateOptions.IgnoreImageCache, BitmapCacheOption.OnLoad);
                    raw.Freeze();
                }

                FormatConvertedBitmap bgra = new FormatConvertedBitmap(raw, PixelFormats.Bgra32, null, 0);
                bgra.Freeze();

                int cols = 8;
                int rows = (ver == 1) ? 9 : 11;
                int cellW = bgra.PixelWidth / cols;
                int cellH = bgra.PixelHeight / rows;
                if (cellW <= 0 || cellH <= 0) return null;

                var pack = new PetPack
                {
                    Id = id,
                    DisplayName = displayName,
                    Description = desc,
                    FolderPath = dir,
                    SpriteVersion = ver
                };
                for (int r = 0; r < 9; r++) pack.Rows[r] = new List<BitmapSource>();

                byte[] buf = new byte[cellW * cellH * 4];
                int stride = cellW * 4;

                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < cols; c++)
                    {
                        var rect = new Int32Rect(c * cellW, r * cellH, cellW, cellH);
                        var crop = new CroppedBitmap(bgra, rect);
                        crop.Freeze();

                        crop.CopyPixels(buf, stride, 0);
                        if (!HasVisiblePixels(buf)) continue;

                        if (r < 9)
                        {
                            pack.Rows[r].Add(crop);
                        }
                        else
                        {
                            pack.LookFrames.Add(crop);
                        }
                    }
                }

                if (pack.Rows[0].Count == 0) return null;
                return pack;
            }
            catch
            {
                return null;
            }
        }

        private static bool HasVisiblePixels(byte[] bgra)
        {
            for (int i = 3; i < bgra.Length; i += 4)
            {
                if (bgra[i] > 8) return true;
            }
            return false;
        }

        private static string ResolveSkinAlias(string skin)
        {
            switch ((skin ?? "").Trim().ToLowerInvariant())
            {
                case "fox": return "cyber-fox";
                case "astro": return "astro-bot";
                case "cat": return "shadow-cat";
                case "ghost": return "byte-ghost";
                case "shiba": return "shiba-dog";
                case "dragon": return "pixel-dragon";
                case "android":
                case "bugdroid": return "android-bugdroid";
                case "dash":
                case "flutter": return "flutter-dash";
                case "": return "cyber-fox";
                default: return skin.Trim();
            }
        }

        private void SwitchSkin(string skinId)
        {
            string resolved = ResolveSkinAlias(skinId);
            if (_packs.ContainsKey(resolved))
            {
                _currentPack = _packs[resolved];
                _config.Skin = _currentPack.Id;
                _animFrame = 0;
                SaveConfig();
                RebuildContextMenu();
                RenderCurrentFrame();
            }
        }

        private void SetupMouseEvents()
        {
            // 气泡聊天框可点击：无论任务进行中还是结束后点击，自动打开对应对话
            _bubbleBorder.MouseEnter += (s, e) =>
            {
                _bubbleBorder.Background = new SolidColorBrush(Color.FromArgb(248, 30, 41, 59));
                _bubbleBorder.BorderThickness = new Thickness(1.8);
            };
            _bubbleBorder.MouseLeave += (s, e) =>
            {
                _bubbleBorder.Background = new SolidColorBrush(Color.FromArgb(236, 15, 23, 42));
                _bubbleBorder.BorderThickness = new Thickness(1.2);
            };
            _bubbleBorder.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                string convId = !string.IsNullOrWhiteSpace(_activeConversationId) ? _activeConversationId : _lastConversationId;
                string title = _activeTitle;

                // 若当前在结束或提醒状态，点击后消除未读高亮；若在运行中，保持进度条继续工作
                if (_state == PetState.Ready || _state == PetState.NeedsInput || _state == PetState.Blocked)
                {
                    ClearReminderTasks();
                }

                StartTransient(TransientAction.Jump, 5);
                OpenAntigravityConversation(convId, title);
            };

            _stageGrid.MouseEnter += (s, e) =>
            {
                if (!_isDragging && _state == PetState.Idle &&
                    (DateTime.Now - _lastWaveAt).TotalSeconds > 3.5)
                {
                    if (_transient == TransientAction.Walk) CancelWalk();
                    StartTransient(TransientAction.Wave, 8);
                    _lastWaveAt = DateTime.Now;
                }
            };

            _stageGrid.MouseLeftButtonDown += (s, e) =>
            {
                if (_transient == TransientAction.Walk) CancelWalk();
                _isDragging = true;
                _movedDuringDrag = false;
                _dragStartScreen = PointToScreen(e.GetPosition(this));
                _dragStartWindow = new Point(Left, Top);
                _stageGrid.CaptureMouse();
            };

            _stageGrid.MouseMove += (s, e) =>
            {
                if (!_isDragging) return;
                Point current = PointToScreen(e.GetPosition(this));
                double dx = current.X - _dragStartScreen.X;
                double dy = current.Y - _dragStartScreen.Y;
                if (Math.Abs(dx) > 5 || Math.Abs(dy) > 5)
                {
                    _movedDuringDrag = true;
                    Left = _dragStartWindow.X + dx;
                    Top = _dragStartWindow.Y + dy;
                }
            };

            _stageGrid.MouseLeftButtonUp += (s, e) =>
            {
                if (!_isDragging) return;
                _isDragging = false;
                _stageGrid.ReleaseMouseCapture();

                if (_movedDuringDrag)
                {
                    ClampToWorkArea();
                    SaveConfig();
                    StartTransient(TransientAction.Jump, 5);
                }
                else
                {
                    string convId = !string.IsNullOrWhiteSpace(_activeConversationId) ? _activeConversationId : _lastConversationId;
                    string title = _activeTitle;
                    StartTransient(TransientAction.Jump, 5);
                    if (_state == PetState.Ready || _state == PetState.NeedsInput || _state == PetState.Blocked)
                    {
                        ClearReminderTasks();
                    }
                    OpenAntigravityConversation(convId, title);
                }
            };
        }

        private void StartTransient(TransientAction action, int frameCount)
        {
            _transient = action;
            _transientFramesLeft = frameCount;
            _animFrame = 0;
            _animTickAcc = 0;
            RenderCurrentFrame();
        }

        private void CancelWalk()
        {
            if (_transient == TransientAction.Walk)
            {
                _transient = TransientAction.None;
                _transientFramesLeft = 0;
                SaveConfig();
            }
        }

        private void StartRandomWalk()
        {
            if (_currentPack == null) return;
            var wa = SystemParameters.WorkArea;
            double minLeft = wa.Left + 16;
            double maxLeft = wa.Right - Width - 16;
            if (maxLeft - minLeft < 120) return;

            double dist = _rng.Next(110, 260);
            int dir = _rng.Next(0, 2) == 0 ? -1 : 1;
            double candidate = Left + dir * dist;
            if (candidate < minLeft || candidate > maxLeft)
            {
                dir = -dir;
                candidate = Math.Max(minLeft, Math.Min(maxLeft, Left + dir * dist));
            }
            if (Math.Abs(candidate - Left) < 50) return;

            _walkDir = dir;
            _walkTargetLeft = candidate;
            _transient = TransientAction.Walk;
            _animFrame = 0;
        }

        private void OnBehaviorTick(object sender, EventArgs e)
        {
            if (_config.AutoStartOnBoot && !_isClosing)
            {
                bool agyAlive = false;
                try
                {
                    agyAlive = Process.GetProcessesByName("Antigravity").Length > 0;
                }
                catch { }

                if (agyAlive)
                {
                    _sawAntigravityProcess = true;
                    _agyMissingTicks = 0;
                }
                else if (_sawAntigravityProcess)
                {
                    _agyMissingTicks++;
                    if (_agyMissingTicks >= 2)
                    {
                        _config.Enabled = true;
                        SaveConfig();
                        Close();
                        return;
                    }
                }
            }
            else
            {
                _agyMissingTicks = 0;
            }

            var now = DateTime.Now;
            var stale = _tasks.Values.Where(t => t.State == PetState.Running && (now - t.UpdatedAt).TotalMinutes > 10).Select(t => t.ConversationId).ToList();
            if (stale.Count > 0)
            {
                foreach (var k in stale) _tasks.Remove(k);
                RecomputeEffectiveState();
            }

            if (_config.AutoWalk && IsVisible && _state == PetState.Idle &&
                _transient == TransientAction.None && !_isDragging && !_stageGrid.IsMouseOver)
            {
                if (now >= _nextWalkAt)
                {
                    StartRandomWalk();
                    _nextWalkAt = now.AddSeconds(_rng.Next(16, 28));
                }
            }
        }

        private void OnAnimTick(object sender, EventArgs e)
        {
            if (_transient == TransientAction.Walk)
            {
                double step = 3.2 * _config.Scale;
                double diff = _walkTargetLeft - Left;
                if (Math.Abs(diff) <= step)
                {
                    Left = _walkTargetLeft;
                    CancelWalk();
                }
                else
                {
                    Left += Math.Sign(diff) * step;
                }
            }

            double frameInterval = (_state == PetState.Idle && _transient == TransientAction.None) ? 0.22 : 0.14;
            _animTickAcc += 0.04;
            if (_animTickAcc >= frameInterval)
            {
                _animTickAcc = 0;
                _animFrame++;

                if (_transient == TransientAction.Wave || _transient == TransientAction.Jump)
                {
                    _transientFramesLeft--;
                    if (_transientFramesLeft <= 0)
                    {
                        _transient = TransientAction.None;
                        _animFrame = 0;
                    }
                }
                RenderCurrentFrame();
            }
            else if (_state == PetState.Idle && _transient == TransientAction.None && _config.EyeFollow)
            {
                RenderCurrentFrame();
            }

            if (_state != PetState.Idle)
            {
                double t = Environment.TickCount / 220.0;
                _statusHalo.Opacity = 0.26 + Math.Sin(t) * 0.12;
            }
            else
            {
                _statusHalo.Opacity = 0;
            }
        }

        private void RenderCurrentFrame()
        {
            if (_currentPack == null) return;

            if (_transient == TransientAction.Wave) { SetRowFrame(3); return; }
            if (_transient == TransientAction.Jump) { SetRowFrame(4); return; }
            if (_transient == TransientAction.Walk) { SetRowFrame(_walkDir >= 0 ? 1 : 2); return; }

            switch (_state)
            {
                case PetState.Running: SetRowFrame(7); break;
                case PetState.NeedsInput: SetRowFrame(6); break;
                case PetState.Blocked: SetRowFrame(5); break;
                case PetState.Ready: SetRowFrame(8); break;
                default:
                    BitmapSource lookFrame;
                    if (_config.EyeFollow && TryGetMouseLookFrame(out lookFrame))
                    {
                        _spriteImage.Source = lookFrame;
                    }
                    else
                    {
                        SetRowFrame(0);
                    }
                    break;
            }
        }

        private void SetRowFrame(int rowIdx)
        {
            if (_currentPack == null) return;
            var list = (rowIdx >= 0 && rowIdx < _currentPack.Rows.Length && _currentPack.Rows[rowIdx].Count > 0)
                ? _currentPack.Rows[rowIdx]
                : _currentPack.Rows[0];
            if (list.Count == 0) return;
            _spriteImage.Source = list[_animFrame % list.Count];
        }

        private bool TryGetMouseLookFrame(out BitmapSource frame)
        {
            frame = null;
            if (_currentPack == null || _currentPack.LookFrames.Count < 8 || !IsVisible) return false;

            try
            {
                POINT pt;
                if (!GetCursorPos(out pt)) return false;

                double mx = pt.X;
                double my = pt.Y;
                var src = PresentationSource.FromVisual(this);
                if (src != null && src.CompositionTarget != null)
                {
                    Point logical = src.CompositionTarget.TransformFromDevice.Transform(new Point(mx, my));
                    mx = logical.X;
                    my = logical.Y;
                }

                double cx = Left + Width * 0.5;
                double cy = Top + Height * 0.62;
                double dx = mx - cx;
                double dy = my - cy;
                double dist = Math.Sqrt(dx * dx + dy * dy);

                if (dist > 420.0 || dist < 26.0) return false;

                double deg = Math.Atan2(dy, dx) * 180.0 / Math.PI;
                if (deg < 0) deg += 360.0;
                int count = _currentPack.LookFrames.Count;
                int idx = (int)Math.Round(deg / (360.0 / count)) % count;
                frame = _currentPack.LookFrames[idx];
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void ReportTaskState(string conversationId, PetState newState, string message, string title, bool isToolCall)
        {
            if (string.IsNullOrWhiteSpace(conversationId)) conversationId = "default";

            bool isPreview = conversationId.Equals("preview", StringComparison.OrdinalIgnoreCase);
            if (!isPreview)
            {
                _lastConversationId = conversationId;
                if (!string.IsNullOrWhiteSpace(title)) _activeTitle = title;
                UpdateRecentConversation(conversationId, title, StateToApiString(newState));
            }

            if (isToolCall && newState == PetState.Running)
            {
                _config.ToolCallsCount++;
            }

            PetState prevState = _state;
            if (newState == PetState.Idle)
            {
                _tasks.Remove(conversationId);
            }
            else
            {
                string existingTitle = _tasks.ContainsKey(conversationId) ? _tasks[conversationId].Title : "";
                string useTitle = !string.IsNullOrWhiteSpace(title) ? title : existingTitle;
                if (newState == PetState.Ready && prevState != PetState.Ready && !isPreview)
                {
                    _config.CompletedTasksCount++;
                }
                _tasks[conversationId] = new TaskItem
                {
                    ConversationId = conversationId,
                    Title = useTitle,
                    State = newState,
                    Message = message ?? "",
                    UpdatedAt = DateTime.Now
                };
                if (_transient == TransientAction.Walk) CancelWalk();
            }

            RecomputeEffectiveState();

            if (_config.SoundAlert && !isPreview && newState != prevState &&
                (newState == PetState.Ready || newState == PetState.NeedsInput))
            {
                try { SystemSounds.Asterisk.Play(); } catch { }
            }
        }

        private void UpdateRecentConversation(string convId, string title, string st)
        {
            if (string.IsNullOrWhiteSpace(convId) || convId == "default" || convId == "preview") return;
            var existing = _recentConversations.FirstOrDefault(r => r.ConversationId.Equals(convId, StringComparison.OrdinalIgnoreCase));
            string cleanTitle = string.IsNullOrWhiteSpace(title) ? (existing != null ? existing.Title : convId.Substring(0, Math.Min(8, convId.Length))) : title;
            if (existing != null) _recentConversations.Remove(existing);
            _recentConversations.Insert(0, new RecentConversation
            {
                ConversationId = convId,
                Title = cleanTitle,
                State = st,
                UpdatedAt = DateTime.Now.ToString("HH:mm:ss")
            });
            if (_recentConversations.Count > 5)
            {
                _recentConversations.RemoveRange(5, _recentConversations.Count - 5);
            }
        }

        private void ClearReminderTasks()
        {
            var keys = _tasks.Values
                .Where(t => t.State == PetState.Ready || t.State == PetState.NeedsInput || t.State == PetState.Blocked)
                .Select(t => t.ConversationId)
                .ToList();
            foreach (var k in keys) _tasks.Remove(k);
            RecomputeEffectiveState();
        }

        private void RecomputeEffectiveState()
        {
            if (_tasks.Count == 0)
            {
                _state = PetState.Idle;
                _statusMessage = "";
                _activeConversationId = _lastConversationId;
                _autoIdleTimer.Stop();
            }
            else
            {
                var top = _tasks.Values
                    .OrderByDescending(t => (int)t.State)
                    .ThenByDescending(t => t.UpdatedAt)
                    .First();

                _state = top.State;
                _activeConversationId = top.ConversationId;
                if (!string.IsNullOrWhiteSpace(top.Title)) _activeTitle = top.Title;

                int sameCount = _tasks.Values.Count(t => t.State == _state);
                string baseMsg = top.Message;

                if (_state == PetState.Ready && (string.IsNullOrWhiteSpace(baseMsg) || baseMsg == "任务完成!"))
                {
                    if (!string.IsNullOrWhiteSpace(top.Title))
                        baseMsg = T("✓ " + Shorten(top.Title, 10) + " · 点击打开", "✓ " + Shorten(top.Title, 14) + " · Click");
                    else
                        baseMsg = T("✓ 任务完成 · 点击打开", "✓ Task Ready · Click");
                }
                else
                {
                    baseMsg = LocalizeBubbleMessage(baseMsg);
                }

                if (sameCount > 1 && !string.IsNullOrEmpty(baseMsg))
                {
                    _statusMessage = sameCount + T("个会话 · ", " tasks · ") + baseMsg;
                }
                else
                {
                    _statusMessage = baseMsg;
                }

                _autoIdleTimer.Stop();
                if (_state == PetState.Ready)
                {
                    _autoIdleTimer.Start();
                }
            }

            _animFrame = 0;
            UpdateVisualState();
            RenderCurrentFrame();
            RebuildContextMenu();
        }

        private static string Shorten(string s, int maxLen)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\r", " ").Replace("\n", " ").Trim();
            return s.Length <= maxLen ? s : (s.Substring(0, maxLen) + "…");
        }

        private bool IsEnglish()
        {
            string mode = (_config.Language ?? "auto").Trim().ToLowerInvariant();
            if (mode == "en" || mode.StartsWith("en-")) return true;
            if (mode == "zh" || mode.StartsWith("zh-")) return false;

            string agyLoc = (_config.AgyDetectedLocale ?? "").Trim().ToLowerInvariant();
            if (agyLoc.StartsWith("en")) return true;
            if (agyLoc.StartsWith("zh")) return false;

            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string storagePath = System.IO.Path.Combine(appData, "Antigravity", "app_storage.json");
                if (File.Exists(storagePath))
                {
                    string raw = File.ReadAllText(storagePath, Encoding.UTF8);
                    if (raw.IndexOf("\"locale\":\"zh", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        raw.IndexOf("\"language\":\"zh", StringComparison.OrdinalIgnoreCase) >= 0) return false;
                    if (raw.IndexOf("\"locale\":\"en", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        raw.IndexOf("\"language\":\"en", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                }
                string locPrefs = System.IO.Path.Combine(appData, "Antigravity", "localization-prefs.json");
                if (File.Exists(locPrefs))
                {
                    string rawPrefs = File.ReadAllText(locPrefs, Encoding.UTF8);
                    if (rawPrefs.IndexOf("\"useTw\":true", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        rawPrefs.IndexOf("chinese", StringComparison.OrdinalIgnoreCase) >= 0) return false;
                    if (rawPrefs.IndexOf("english", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                }
            }
            catch { }

            return System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("en", StringComparison.OrdinalIgnoreCase);
        }

        private string T(string zh, string en)
        {
            return IsEnglish() ? en : zh;
        }

        private string GetLocalizedPackName(PetPack p)
        {
            if (p == null) return "";
            if (!IsEnglish()) return p.DisplayName;
            switch ((p.Id ?? "").ToLowerInvariant())
            {
                case "cyber-fox": return "CyberFox";
                case "astro-bot": return "AstroBot";
                case "shadow-cat": return "ShadowCat";
                case "byte-ghost": return "ByteGhost";
                case "shiba-dog": return "ShibaDog";
                case "pixel-dragon": return "PixelDragon";
                case "android-bugdroid": return "Android Bugdroid";
                case "flutter-dash": return "Flutter Dash";
                default: return p.DisplayName;
            }
        }

        private string GetLocalizedPackDesc(PetPack p)
        {
            if (p == null) return "";
            if (!IsEnglish()) return p.Description;
            switch ((p.Id ?? "").ToLowerInvariant())
            {
                case "cyber-fox": return "Cyber blue fox companion (Default)";
                case "astro-bot": return "Mini astronaut with glowing visor";
                case "shadow-cat": return "Golden-eyed nimble black cat";
                case "byte-ghost": return "Cute floating neon ghost";
                case "shiba-dog": return "Warm fluffy Shiba Inu";
                case "pixel-dragon": return "Emerald mini dragon with golden horns";
                case "android-bugdroid": return "Classic Google Android green robot mascot";
                case "flutter-dash": return "Official Google Flutter & Dart blue bird mascot";
                default: return p.Description;
            }
        }

        private string LocalizeBubbleMessage(string msg)
        {
            if (string.IsNullOrWhiteSpace(msg) || !IsEnglish()) return msg;
            switch (msg.Trim())
            {
                case "思考规划中...": return "Thinking & planning...";
                case "执行命令中...": return "Running command...";
                case "读取文件中...": return "Reading file...";
                case "编写文件中...": return "Writing file...";
                case "修改代码中...":
                case "编辑代码中...": return "Editing code...";
                case "检索代码中...": return "Searching code...";
                case "运行检查中...": return "Running checks...";
                case "快速分析代码中...": return "Analyzing code...";
                case "分析长文本中...": return "Analyzing context...";
                case "搜索资料中...": return "Searching web...";
                case "调度子代理中...": return "Running subagent...";
                case "等待你回答...": return "Waiting for answer...";
                case "管理后台任务...": return "Managing tasks...";
                case "准备开始执行...": return "Starting execution...";
                case "查看屏幕中...": return "Viewing screen...";
                case "操作桌面应用中...":
                case "操控桌面中...": return "Controlling desktop...";
                case "更新桌宠设置...": return "Updating settings...";
                case "调用扩展工具中...":
                case "执行任务中...": return "Running tool...";
                case "处理结果中...":
                case "整理回复中...": return "Processing results...";
                case "工具出错 · 点击查看":
                case "遇到错误 · 点击查看":
                case "执行出错 · 点击查看": return "Blocked · Click to view";
                case "任务出错 · 点击查看":
                case "任务出错中断": return "Task error · Click to view";
                case "任务完成!":
                case "✓ 任务完成 · 点击打开": return "✓ Task Ready · Click";
                case "需要你确认!":
                case "需要你批准操作!": return "Needs your input!";
                case "工作中...":
                case "编写代码中...": return "Working...";
                default: return msg;
            }
        }

        private void UpdateVisualState()
        {
            Color haloColor = Colors.Transparent;
            string badge = "";
            Color badgeBg = Colors.DodgerBlue;
            string defaultMsg = "";

            switch (_state)
            {
                case PetState.Idle:
                    _badgeBorder.Visibility = Visibility.Collapsed;
                    break;
                case PetState.Running:
                    haloColor = Color.FromRgb(56, 189, 248);
                    badge = "⚡";
                    badgeBg = Color.FromRgb(14, 165, 233);
                    defaultMsg = T("工作中...", "Working...");
                    break;
                case PetState.NeedsInput:
                    haloColor = Color.FromRgb(251, 191, 36);
                    badge = "!";
                    badgeBg = Color.FromRgb(245, 158, 11);
                    defaultMsg = T("需要你确认!", "Needs your input!");
                    break;
                case PetState.Ready:
                    haloColor = Color.FromRgb(52, 211, 153);
                    badge = "✓";
                    badgeBg = Color.FromRgb(16, 185, 129);
                    defaultMsg = T("✓ 任务完成 · 点击打开", "✓ Task Ready · Click");
                    break;
                case PetState.Blocked:
                    haloColor = Color.FromRgb(248, 113, 113);
                    badge = "✕";
                    badgeBg = Color.FromRgb(239, 68, 68);
                    defaultMsg = T("遇到错误 · 点击查看", "Blocked · Click to view");
                    break;
            }

            _statusHalo.Fill = new RadialGradientBrush(
                Color.FromArgb(135, haloColor.R, haloColor.G, haloColor.B),
                Color.FromArgb(0, haloColor.R, haloColor.G, haloColor.B));

            if (!string.IsNullOrEmpty(badge))
            {
                _badgeText.Text = badge;
                _badgeBorder.Background = new SolidColorBrush(badgeBg);
                _badgeBorder.Visibility = Visibility.Visible;
            }

            bool isAlert = (_state == PetState.NeedsInput || _state == PetState.Ready || _state == PetState.Blocked);
            string msg = string.IsNullOrWhiteSpace(_statusMessage) ? defaultMsg : LocalizeBubbleMessage(_statusMessage);
            bool shouldShowBubble = !string.IsNullOrEmpty(msg) &&
                (_config.BubbleMode == "always" || (_config.BubbleMode == "alerts" && isAlert));

            if (shouldShowBubble)
            {
                _bubbleText.Text = msg;
                _bubbleBorder.BorderBrush = new SolidColorBrush(_state == PetState.Idle ? Color.FromRgb(56, 189, 248) : badgeBg);
                _bubbleBorder.ToolTip = string.IsNullOrWhiteSpace(_activeTitle)
                    ? T("点击打开对应 Antigravity 对话", "Click to open Antigravity conversation")
                    : (T("点击打开对话: ", "Open conversation: ") + _activeTitle);
                _bubbleBorder.Visibility = Visibility.Visible;
            }
            else
            {
                _bubbleBorder.Visibility = Visibility.Hidden;
            }
        }

        private void OpenAntigravityConversation(string conversationId, string titleHint)
        {
            string targetId = string.IsNullOrWhiteSpace(conversationId) ? _lastConversationId : conversationId;
            if (string.Equals(targetId, "preview", StringComparison.OrdinalIgnoreCase))
            {
                targetId = _lastConversationId;
            }

            // 第一级：Win32 枚举所有 Antigravity 顶层窗口强制唤醒并置前
            try
            {
                var agyPids = new HashSet<uint>();
                foreach (var proc in Process.GetProcessesByName("Antigravity"))
                {
                    agyPids.Add((uint)proc.Id);
                }

                IntPtr bestMatch = IntPtr.Zero;
                IntPtr fallbackWin = IntPtr.Zero;

                if (agyPids.Count > 0)
                {
                    EnumWindows((hWnd, lParam) =>
                    {
                        uint pid;
                        GetWindowThreadProcessId(hWnd, out pid);
                        if (!agyPids.Contains(pid)) return true;

                        RECT r;
                        if (!GetWindowRect(hWnd, out r)) return true;
                        int w = r.Right - r.Left;
                        int h = r.Bottom - r.Top;
                        if (w < 240 || h < 180) return true;

                        var sb = new StringBuilder(256);
                        GetWindowText(hWnd, sb, 256);
                        string winTitle = sb.ToString();
                        if (string.IsNullOrWhiteSpace(winTitle) && !IsWindowVisible(hWnd)) return true;

                        if (fallbackWin == IntPtr.Zero) fallbackWin = hWnd;
                        if (!string.IsNullOrWhiteSpace(titleHint) &&
                            winTitle.IndexOf(Shorten(titleHint, 12).Replace("…", ""), StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            bestMatch = hWnd;
                            return false;
                        }
                        if (winTitle.IndexOf("Antigravity", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            bestMatch = hWnd;
                        }
                        return true;
                    }, IntPtr.Zero);
                }

                IntPtr targetHwnd = bestMatch != IntPtr.Zero ? bestMatch : fallbackWin;
                if (targetHwnd != IntPtr.Zero)
                {
                    if (IsIconic(targetHwnd))
                    {
                        ShowWindow(targetHwnd, SW_RESTORE);
                    }
                    else
                    {
                        ShowWindow(targetHwnd, SW_SHOW);
                    }
                    // 发送空键盘事件解除前台锁定限制
                    keybd_event(0, 0, 0, UIntPtr.Zero);
                    BringWindowToTop(targetHwnd);
                    SetForegroundWindow(targetHwnd);
                    SwitchToThisWindow(targetHwnd, true);
                }
            }
            catch { }

            // 第二级：异步通过 Electron Deep-Link 唤醒主窗（即使已关闭到后台托盘或未启动）并路由到对应对话
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    string deepLink = (!string.IsNullOrWhiteSpace(targetId) &&
                                       !targetId.Equals("default", StringComparison.OrdinalIgnoreCase))
                        ? ("antigravity://conversation/" + Uri.EscapeDataString(targetId))
                        : "antigravity://open";

                    string agyExe = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "Programs", "antigravity", "Antigravity.exe");

                    if (File.Exists(agyExe))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = agyExe,
                            Arguments = deepLink,
                            UseShellExecute = false
                        });
                    }
                    else
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = deepLink,
                            UseShellExecute = true
                        });
                    }
                }
                catch { }
            });
        }

        private void RebuildContextMenu()
        {
            var menu = new ContextMenu();

            string stateLabel = T("状态: ", "Status: ") + StateToFriendlyLabel(_state) +
                (_tasks.Count > 1 ? (" (" + _tasks.Count + T("个会话)", " tasks)")) : "");
            menu.Items.Add(new MenuItem { Header = stateLabel, IsEnabled = false });

            double mins = Math.Max(0, Math.Round((DateTime.Now - _sessionStart).TotalMinutes));
            menu.Items.Add(new MenuItem
            {
                Header = string.Format(T("📊 已完成 {0} 次 · 工具 {1} 次 · 在线 {2} 分", "📊 Done {0} · Tools {1} · Up {2}m"), _config.CompletedTasksCount, _config.ToolCallsCount, mins),
                IsEnabled = false
            });
            menu.Items.Add(new Separator());

            var recentMenu = new MenuItem { Header = T("💬 最近对话 (点击打开)", "💬 Recent Chats (Click)") };
            if (_recentConversations.Count == 0)
            {
                recentMenu.Items.Add(new MenuItem { Header = T("暂无记录（点击打开 Antigravity）", "No history (Open Antigravity)") });
                ((MenuItem)recentMenu.Items[0]).Click += (s, e) => OpenAntigravityConversation(_lastConversationId, _activeTitle);
            }
            else
            {
                foreach (var rc in _recentConversations)
                {
                    string cid = rc.ConversationId;
                    string ctitle = rc.Title;
                    recentMenu.Items.Add(MakeMenuItem(string.Format("[{0}] {1}", rc.UpdatedAt, Shorten(ctitle, 18)), () =>
                    {
                        OpenAntigravityConversation(cid, ctitle);
                    }));
                }
            }
            menu.Items.Add(recentMenu);

            var skinMenu = new MenuItem { Header = T("🎭 切换宠物 (", "🎭 Switch Pet (") + _packs.Count + ")" };
            foreach (var p in _packs.Values.OrderBy(x => GetLocalizedPackName(x)))
            {
                string pid = p.Id;
                var item = new MenuItem
                {
                    Header = GetLocalizedPackName(p),
                    IsCheckable = true,
                    IsChecked = (_currentPack != null && _currentPack.Id.Equals(pid, StringComparison.OrdinalIgnoreCase))
                };
                item.Click += (s, e) => SwitchSkin(pid);
                skinMenu.Items.Add(item);
            }
            skinMenu.Items.Add(new Separator());
            skinMenu.Items.Add(MakeMenuItem(T("🔄 刷新宠物列表", "🔄 Refresh Pets"), () =>
            {
                ReloadPetPacks();
                RebuildContextMenu();
                RenderCurrentFrame();
            }));
            menu.Items.Add(skinMenu);

            var langMenu = new MenuItem { Header = T("🌐 语言 / Language", "🌐 Language") };
            langMenu.Items.Add(MakeCheckItem(T("🔄 跟随 Antigravity (自动)", "🔄 Follow Antigravity (Auto)"), (_config.Language ?? "auto") == "auto", () => { _config.Language = "auto"; UpdateVisualState(); SaveConfig(); RebuildContextMenu(); }));
            langMenu.Items.Add(MakeCheckItem("🇺🇸 English", (_config.Language ?? "auto") == "en", () => { _config.Language = "en"; UpdateVisualState(); SaveConfig(); RebuildContextMenu(); }));
            langMenu.Items.Add(MakeCheckItem("🇨🇳 简体中文", (_config.Language ?? "auto") == "zh", () => { _config.Language = "zh"; UpdateVisualState(); SaveConfig(); RebuildContextMenu(); }));
            menu.Items.Add(langMenu);

            var sizeMenu = new MenuItem { Header = T("📐 宠物尺寸", "📐 Pet Size") };
            sizeMenu.Items.Add(MakeCheckItem(T("小 (0.75x)", "Small (0.75x)"), Math.Abs(_config.Scale - 0.75) < 0.05, () => { ApplyScale(0.75, true); RebuildContextMenu(); }));
            sizeMenu.Items.Add(MakeCheckItem(T("标准 (1.0x)", "Normal (1.0x)"), Math.Abs(_config.Scale - 1.0) < 0.05, () => { ApplyScale(1.0, true); RebuildContextMenu(); }));
            sizeMenu.Items.Add(MakeCheckItem(T("大 (1.3x)", "Large (1.3x)"), Math.Abs(_config.Scale - 1.3) < 0.05, () => { ApplyScale(1.3, true); RebuildContextMenu(); }));
            sizeMenu.Items.Add(MakeCheckItem(T("特大 (1.5x)", "XL (1.5x)"), Math.Abs(_config.Scale - 1.5) < 0.05, () => { ApplyScale(1.5, true); RebuildContextMenu(); }));
            menu.Items.Add(sizeMenu);

            var bubbleMenu = new MenuItem { Header = T("💭 气泡模式", "💭 Bubble Mode") };
            bubbleMenu.Items.Add(MakeCheckItem(T("始终显示", "Always Show"), _config.BubbleMode == "always", () => { _config.BubbleMode = "always"; UpdateVisualState(); SaveConfig(); RebuildContextMenu(); }));
            bubbleMenu.Items.Add(MakeCheckItem(T("仅重要提醒", "Alerts Only"), _config.BubbleMode == "alerts", () => { _config.BubbleMode = "alerts"; UpdateVisualState(); SaveConfig(); RebuildContextMenu(); }));
            bubbleMenu.Items.Add(MakeCheckItem(T("关闭气泡", "Off"), _config.BubbleMode == "off", () => { _config.BubbleMode = "off"; UpdateVisualState(); SaveConfig(); RebuildContextMenu(); }));
            menu.Items.Add(bubbleMenu);

            menu.Items.Add(MakeCheckItem(T("🐾 待机桌面散步", "🐾 Idle Roaming"), _config.AutoWalk, () =>
            {
                _config.AutoWalk = !_config.AutoWalk;
                if (!_config.AutoWalk) CancelWalk();
                SaveConfig();
                RebuildContextMenu();
            }));

            menu.Items.Add(MakeCheckItem(T("👀 眼神跟随鼠标", "👀 Eye Follow Cursor"), _config.EyeFollow, () =>
            {
                _config.EyeFollow = !_config.EyeFollow;
                SaveConfig();
                RebuildContextMenu();
            }));

            menu.Items.Add(MakeCheckItem(T("📌 始终置顶", "📌 Always on Top"), _config.Topmost, () =>
            {
                _config.Topmost = !_config.Topmost;
                Topmost = _config.Topmost;
                SaveConfig();
                RebuildContextMenu();
            }));

            menu.Items.Add(MakeCheckItem(T("🔔 完成提示音效", "🔔 Completion Sound"), _config.SoundAlert, () =>
            {
                _config.SoundAlert = !_config.SoundAlert;
                SaveConfig();
                RebuildContextMenu();
            }));

            menu.Items.Add(MakeCheckItem(T("🚀 随 Antigravity 启动", "🚀 Launch with Antigravity"), _config.AutoStartOnBoot, () =>
            {
                _config.AutoStartOnBoot = !_config.AutoStartOnBoot;
                SyncAutoStartRegistry(_config.AutoStartOnBoot);
                SaveConfig();
                RebuildContextMenu();
            }));

            var testMenu = new MenuItem { Header = T("🧪 动作与状态预览", "🧪 Preview States") };
            testMenu.Items.Add(MakeMenuItem(T("⚡ Running (运行中)", "⚡ Running"), () => ReportTaskState("preview", PetState.Running, T("编写代码中...", "Writing code..."), "", false)));
            testMenu.Items.Add(MakeMenuItem(T("! Needs Input (需要确认)", "! Needs Input"), () => ReportTaskState("preview", PetState.NeedsInput, T("需要你批准操作!", "Needs your input!"), "", false)));
            testMenu.Items.Add(MakeMenuItem(T("✓ Ready (已完成)", "✓ Ready"), () => ReportTaskState("preview", PetState.Ready, T("✓ 任务完成 · 点击打开", "✓ Task Ready · Click"), "", false)));
            testMenu.Items.Add(MakeMenuItem(T("✕ Blocked (报错阻断)", "✕ Blocked"), () => ReportTaskState("preview", PetState.Blocked, T("执行出错 · 点击查看", "Blocked · Click to view"), "", false)));
            testMenu.Items.Add(MakeMenuItem(T("💤 Idle (恢复待机)", "💤 Idle"), () => { _tasks.Clear(); RecomputeEffectiveState(); }));
            testMenu.Items.Add(new Separator());
            testMenu.Items.Add(MakeMenuItem(T("👋 挥手打招呼", "👋 Wave"), () => StartTransient(TransientAction.Wave, 8)));
            testMenu.Items.Add(MakeMenuItem(T("🦘 开心跳一下", "🦘 Jump"), () => StartTransient(TransientAction.Jump, 5)));
            testMenu.Items.Add(MakeMenuItem(T("🐾 散步一下", "🐾 Walk"), () => { _tasks.Clear(); RecomputeEffectiveState(); StartRandomWalk(); }));
            menu.Items.Add(testMenu);

            menu.Items.Add(new Separator());
            menu.Items.Add(MakeMenuItem(T("📂 打开宠物文件夹", "📂 Open Pets Folder"), () =>
            {
                try
                {
                    Directory.CreateDirectory(_pluginPetsDir);
                    Process.Start("explorer.exe", _pluginPetsDir);
                }
                catch { }
            }));
            menu.Items.Add(MakeMenuItem(T("📍 重置到右下角", "📍 Reset Position"), () => ResetToCorner(true)));
            menu.Items.Add(new Separator());
            menu.Items.Add(MakeMenuItem(T("🙈 隐藏桌宠 (Win+Alt+P)", "🙈 Hide Pet (Win+Alt+P)"), () => Hide()));
            menu.Items.Add(MakeMenuItem(T("🚪 退出桌宠", "🚪 Quit Pet"), () => { _config.Enabled = false; SaveConfig(); Close(); }));

            _stageGrid.ContextMenu = menu;
        }

        private MenuItem MakeMenuItem(string header, Action onClick)
        {
            var item = new MenuItem { Header = header };
            item.Click += (s, e) => onClick();
            return item;
        }

        private MenuItem MakeCheckItem(string header, bool isChecked, Action onClick)
        {
            var item = new MenuItem { Header = header, IsCheckable = true, IsChecked = isChecked };
            item.Click += (s, e) => onClick();
            return item;
        }

        private void ToggleVisibility()
        {
            if (IsVisible) Hide();
            else
            {
                Show();
                StartTransient(TransientAction.Wave, 8);
            }
        }

        private void StartHttpServer()
        {
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add("http://127.0.0.1:" + PORT + "/");
                _listener.Start();

                _httpThread = new Thread(() =>
                {
                    while (!_isClosing && _listener != null && _listener.IsListening)
                    {
                        try
                        {
                            var ctx = _listener.GetContext();
                            HandleRequest(ctx);
                        }
                        catch
                        {
                            if (_isClosing) break;
                        }
                    }
                });
                _httpThread.IsBackground = true;
                _httpThread.Start();
            }
            catch { }
        }

        private void HandleRequest(HttpListenerContext ctx)
        {
            string path = (ctx.Request.Url.AbsolutePath ?? "/").ToLowerInvariant();
            string body = "";
            try
            {
                using (var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8))
                {
                    body = reader.ReadToEnd();
                }
            }
            catch { }

            string responseJson = "{\"ok\":true}";

            try
            {
                Dictionary<string, object> data = string.IsNullOrWhiteSpace(body)
                    ? new Dictionary<string, object>()
                    : (_json.Deserialize<Dictionary<string, object>>(body) ?? new Dictionary<string, object>());

                if (path == "/health")
                {
                    object info = null;
                    Dispatcher.Invoke(() =>
                    {
                        var hwnd = new WindowInteropHelper(this).Handle;
                        info = new
                        {
                            ok = true,
                            running = true,
                            enabled = _config.Enabled,
                            state = StateToApiString(_state),
                            skin = _currentPack != null ? _currentPack.Id : _config.Skin,
                            displayName = _currentPack != null ? GetLocalizedPackName(_currentPack) : "",
                            visible = IsVisible,
                            scale = _config.Scale,
                            bubbleMode = _config.BubbleMode,
                            autoWalk = _config.AutoWalk,
                            eyeFollow = _config.EyeFollow,
                            topmost = _config.Topmost,
                            autoStartOnBoot = _config.AutoStartOnBoot,
                            soundAlert = _config.SoundAlert,
                            language = string.IsNullOrWhiteSpace(_config.Language) ? "auto" : _config.Language,
                            effectiveLanguage = IsEnglish() ? "en" : "zh",
                            activeTasks = _tasks.Count,
                            completedTasksCount = _config.CompletedTasksCount,
                            toolCallsCount = _config.ToolCallsCount,
                            uptimeMinutes = Math.Max(0, (int)Math.Round((DateTime.Now - _sessionStart).TotalMinutes)),
                            activeConversationId = _activeConversationId,
                            lastConversationId = _lastConversationId,
                            activeTitle = _activeTitle,
                            recentConversations = _recentConversations.ToArray(),
                            availablePets = _packs.Values.OrderBy(p => GetLocalizedPackName(p)).Select(p => new { id = p.Id, displayName = GetLocalizedPackName(p), description = GetLocalizedPackDesc(p), version = p.SpriteVersion }).ToArray(),
                            hwnd = hwnd.ToInt64(),
                            left = Left,
                            top = Top
                        };
                    });
                    responseJson = _json.Serialize(info);
                }
                else if (path == "/state")
                {
                    string convId = GetStr(data, "conversationId", "default");
                    string st = GetStr(data, "state", "idle");
                    string msg = GetStr(data, "message", "");
                    string title = GetStr(data, "title", "");
                    bool isTool = GetBool(data, "isToolCall", false);
                    PetState parsed = ParseState(st);
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (!IsVisible && _config.Enabled) Show();
                        ReportTaskState(convId, parsed, msg, title, isTool);
                    }));
                }
                else if (path == "/skin")
                {
                    string sk = GetStr(data, "skin", "cyber-fox");
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (!IsVisible) Show();
                        SwitchSkin(sk);
                    }));
                }
                else if (path == "/reload")
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        ReloadPetPacks();
                        RebuildContextMenu();
                        RenderCurrentFrame();
                    }));
                }
                else if (path == "/control")
                {
                    string action = GetStr(data, "action", "show").ToLowerInvariant();
                    string val = GetStr(data, "value", "");
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (action == "show") { _config.Enabled = true; SaveConfig(); Show(); StartTransient(TransientAction.Wave, 8); }
                        else if (action == "hide") Hide();
                        else if (action == "toggle") ToggleVisibility();
                        else if (action == "focus" || action == "open")
                        {
                            string cid = !string.IsNullOrWhiteSpace(val) ? val : (!string.IsNullOrWhiteSpace(_activeConversationId) ? _activeConversationId : _lastConversationId);
                            OpenAntigravityConversation(cid, _activeTitle);
                        }
                        else if (action == "walk") { _tasks.Clear(); RecomputeEffectiveState(); StartRandomWalk(); }
                        else if (action == "wave") StartTransient(TransientAction.Wave, 8);
                        else if (action == "jump") StartTransient(TransientAction.Jump, 5);
                        else if (action == "resetpos") ResetToCorner(true);
                        else if (action == "scale")
                        {
                            double sc;
                            if (double.TryParse(val, out sc)) { ApplyScale(sc, true); RebuildContextMenu(); }
                        }
                        else if (action == "bubblemode")
                        {
                            if (val == "always" || val == "alerts" || val == "off")
                            {
                                _config.BubbleMode = val;
                                UpdateVisualState();
                                SaveConfig();
                                RebuildContextMenu();
                            }
                        }
                        else if (action == "autowalk")
                        {
                            _config.AutoWalk = ParseBoolStr(val, !_config.AutoWalk);
                            if (!_config.AutoWalk) CancelWalk();
                            SaveConfig();
                            RebuildContextMenu();
                        }
                        else if (action == "eyefollow")
                        {
                            _config.EyeFollow = ParseBoolStr(val, !_config.EyeFollow);
                            SaveConfig();
                            RebuildContextMenu();
                        }
                        else if (action == "topmost")
                        {
                            _config.Topmost = ParseBoolStr(val, !_config.Topmost);
                            Topmost = _config.Topmost;
                            SaveConfig();
                            RebuildContextMenu();
                        }
                        else if (action == "autostartonboot")
                        {
                            _config.AutoStartOnBoot = ParseBoolStr(val, !_config.AutoStartOnBoot);
                            SyncAutoStartRegistry(_config.AutoStartOnBoot);
                            SaveConfig();
                            RebuildContextMenu();
                        }
                        else if (action == "soundalert")
                        {
                            _config.SoundAlert = ParseBoolStr(val, !_config.SoundAlert);
                            SaveConfig();
                            RebuildContextMenu();
                        }
                        else if (action == "language")
                        {
                            string lval = (val ?? "auto").Trim().ToLowerInvariant();
                            if (lval == "auto" || lval == "en" || lval == "zh")
                            {
                                _config.Language = lval;
                                UpdateVisualState();
                                SaveConfig();
                                RebuildContextMenu();
                            }
                        }
                        else if (action == "agylocale")
                        {
                            _config.AgyDetectedLocale = val ?? "";
                            UpdateVisualState();
                            SaveConfig();
                            RebuildContextMenu();
                        }
                        else if (action == "updateconfig")
                        {
                            ApplyConfigMap(data);
                        }
                        else if (action == "quit")
                        {
                            bool keepEnabled = GetBool(data, "restart", false);
                            if (!keepEnabled) _config.Enabled = false;
                            SaveConfig();
                            Close();
                        }
                    }));
                }
            }
            catch (Exception ex)
            {
                responseJson = _json.Serialize(new { ok = false, error = ex.Message });
            }

            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(responseJson);
                ctx.Response.ContentType = "application/json; charset=utf-8";
                ctx.Response.ContentLength64 = bytes.Length;
                ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
                ctx.Response.OutputStream.Close();
            }
            catch { }
        }

        private void ApplyConfigMap(Dictionary<string, object> data)
        {
            if (data == null) return;
            if (data.ContainsKey("skin") && data["skin"] != null) SwitchSkin(data["skin"].ToString());
            if (data.ContainsKey("scale") && data["scale"] != null)
            {
                double sc;
                if (double.TryParse(data["scale"].ToString(), out sc)) ApplyScale(sc, false);
            }
            if (data.ContainsKey("bubbleMode") && data["bubbleMode"] != null)
            {
                string bm = data["bubbleMode"].ToString();
                if (bm == "always" || bm == "alerts" || bm == "off") _config.BubbleMode = bm;
            }
            if (data.ContainsKey("language") && data["language"] != null)
            {
                string lg = data["language"].ToString().Trim().ToLowerInvariant();
                if (lg == "auto" || lg == "en" || lg == "zh") _config.Language = lg;
            }
            if (data.ContainsKey("autoWalk")) { _config.AutoWalk = GetBool(data, "autoWalk", _config.AutoWalk); if (!_config.AutoWalk) CancelWalk(); }
            if (data.ContainsKey("eyeFollow")) _config.EyeFollow = GetBool(data, "eyeFollow", _config.EyeFollow);
            if (data.ContainsKey("topmost")) { _config.Topmost = GetBool(data, "topmost", _config.Topmost); Topmost = _config.Topmost; }
            if (data.ContainsKey("autoStartOnBoot")) { _config.AutoStartOnBoot = GetBool(data, "autoStartOnBoot", _config.AutoStartOnBoot); SyncAutoStartRegistry(_config.AutoStartOnBoot); }
            if (data.ContainsKey("soundAlert")) _config.SoundAlert = GetBool(data, "soundAlert", _config.SoundAlert);

            UpdateVisualState();
            SaveConfig();
            RebuildContextMenu();
        }

        private static string GetStr(Dictionary<string, object> dict, string key, string fallback)
        {
            if (dict != null && dict.ContainsKey(key) && dict[key] != null)
                return dict[key].ToString();
            return fallback;
        }

        private static bool GetBool(Dictionary<string, object> dict, string key, bool fallback)
        {
            if (dict != null && dict.ContainsKey(key) && dict[key] != null)
                return ParseBoolStr(dict[key].ToString(), fallback);
            return fallback;
        }

        private static bool ParseBoolStr(string s, bool fallback)
        {
            if (string.IsNullOrWhiteSpace(s)) return fallback;
            bool b;
            if (bool.TryParse(s, out b)) return b;
            if (s == "1") return true;
            if (s == "0") return false;
            return fallback;
        }

        private static PetState ParseState(string s)
        {
            switch ((s ?? "").ToLowerInvariant())
            {
                case "running": return PetState.Running;
                case "needs_input":
                case "needsinput":
                case "waiting": return PetState.NeedsInput;
                case "ready":
                case "review": return PetState.Ready;
                case "blocked":
                case "failed":
                case "error": return PetState.Blocked;
                default: return PetState.Idle;
            }
        }

        private static string StateToApiString(PetState st)
        {
            switch (st)
            {
                case PetState.Running: return "running";
                case PetState.NeedsInput: return "needs_input";
                case PetState.Ready: return "ready";
                case PetState.Blocked: return "blocked";
                default: return "idle";
            }
        }

        private string StateToFriendlyLabel(PetState st)
        {
            switch (st)
            {
                case PetState.Running: return T("⚡ 运行中 (Running)", "⚡ Running");
                case PetState.NeedsInput: return T("! 需要输入 (Needs Input)", "! Needs Input");
                case PetState.Ready: return T("✓ 已完成 (Ready)", "✓ Ready");
                case PetState.Blocked: return T("✕ 出错阻断 (Blocked)", "✕ Blocked");
                default: return T("💤 待机休息 (Idle)", "💤 Idle");
            }
        }

        private PetConfig LoadConfig()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var cfg = _json.Deserialize<PetConfig>(File.ReadAllText(_configPath, Encoding.UTF8));
                    if (cfg != null)
                    {
                        if (string.IsNullOrWhiteSpace(cfg.BubbleMode)) cfg.BubbleMode = "always";
                        if (string.IsNullOrWhiteSpace(cfg.Language)) cfg.Language = "auto";
                        if (cfg.Scale <= 0 || double.IsNaN(cfg.Scale) || double.IsInfinity(cfg.Scale)) cfg.Scale = 1.0;
                        if (double.IsNaN(cfg.Left) || double.IsInfinity(cfg.Left)) cfg.Left = -1;
                        if (double.IsNaN(cfg.Top) || double.IsInfinity(cfg.Top)) cfg.Top = -1;
                        return cfg;
                    }
                }
            }
            catch { }
            return new PetConfig();
        }

        private void SaveConfig()
        {
            try
            {
                if (!double.IsNaN(Left) && !double.IsInfinity(Left)) _config.Left = Left;
                else if (double.IsNaN(_config.Left) || double.IsInfinity(_config.Left)) _config.Left = -1;

                if (!double.IsNaN(Top) && !double.IsInfinity(Top)) _config.Top = Top;
                else if (double.IsNaN(_config.Top) || double.IsInfinity(_config.Top)) _config.Top = -1;

                _config.Topmost = Topmost;
                if (_currentPack != null) _config.Skin = _currentPack.Id;
                File.WriteAllText(_configPath, _json.Serialize(_config), new UTF8Encoding(false));
            }
            catch { }
        }

        [STAThread]
        public static void Main(string[] args)
        {
            bool createdNew;
            using (var mutex = new Mutex(true, "AntigravityPetSingleInstanceMutex", out createdNew))
            {
                if (!createdNew)
                {
                    try
                    {
                        var req = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:" + PORT + "/control");
                        req.Method = "POST";
                        req.Timeout = 500;
                        byte[] data = Encoding.UTF8.GetBytes("{\"action\":\"show\"}");
                        req.ContentType = "application/json";
                        req.ContentLength = data.Length;
                        using (var stream = req.GetRequestStream()) stream.Write(data, 0, data.Length);
                        using (req.GetResponse()) { }
                    }
                    catch { }
                    return;
                }

                var app = new Application();
                app.ShutdownMode = ShutdownMode.OnMainWindowClose;
                var win = new PetWindow();
                app.MainWindow = win;
                win.Show();
                app.Run();
            }
        }
    }
}
