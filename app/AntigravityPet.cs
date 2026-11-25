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
        public string BubbleStyle { get; set; } // simple | detailed
        public string PinnedConversationId { get; set; }
        public int ReadyTimeoutSec { get; set; } // 0 = never auto-hide, default 30s
        public bool SnapToAgy { get; set; }
        public bool HideOnFullscreen { get; set; }
        public string Accessory { get; set; }    // auto | none | glasses | cap | coffee | crown
        public double Scale { get; set; }       // 0.33 ~ 3.0 (default 1.0)
        public bool AutoWalk { get; set; }
        public bool EyeFollow { get; set; }
        public bool BoredomIdle { get; set; }
        public bool KeyMouseInteract { get; set; }
        public bool AutoStartOnBoot { get; set; }
        public bool CheckUpdatesOnStartup { get; set; }
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
            BubbleStyle = "simple";
            PinnedConversationId = "";
            ReadyTimeoutSec = 30;
            SnapToAgy = false;
            HideOnFullscreen = true;
            Accessory = "auto";
            Scale = 1.0;
            AutoWalk = true;
            EyeFollow = true;
            BoredomIdle = true;
            KeyMouseInteract = true;
            AutoStartOnBoot = true;
            CheckUpdatesOnStartup = true;
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
        public Color ThemeColor = Color.FromRgb(56, 189, 248);
        public Dictionary<string, string> Sounds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string SpritesheetPath;
        public string IdleImagePath;
        public bool HasFolderFrames;
        public bool AssetsLoaded;

        public List<BitmapSource>[] Rows = new List<BitmapSource>[9];
        public List<BitmapSource> LookFrames = new List<BitmapSource>();

        public void UnloadAssets()
        {
            if (Rows != null)
            {
                for (int r = 0; r < Rows.Length; r++)
                {
                    if (Rows[r] != null) Rows[r].Clear();
                }
            }
            if (LookFrames != null) LookFrames.Clear();
            AssetsLoaded = false;
        }
    }

    public class PendingApproval
    {
        public string Id;
        public string ConversationId;
        public string ToolName;
        public string Summary;
        public string Decision = "ask";
        public ManualResetEventSlim WaitHandle = new ManualResetEventSlim(false);
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
        [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
        [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);
        [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
        [DllImport("shell32.dll")] private static extern int SHQueryUserNotificationState(out int pquns);
        [DllImport("user32.dll")] private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);
        [DllImport("shcore.dll")] private static extern int SetProcessDpiAwareness(int value);
        [DllImport("user32.dll")] private static extern bool SetProcessDPIAware();
        [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);
        [DllImport("user32.dll", SetLastError = true)] private static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, out RAWINPUTHEADER pData, ref uint pcbSize, uint cbSizeHeader);

        private const uint MONITOR_DEFAULTTONEAREST = 2;

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public uint dwFlags;
            public IntPtr hwndTarget;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTHEADER
        {
            public uint dwType;
            public uint dwSize;
            public IntPtr hDevice;
            public IntPtr wParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
        }

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_TOPMOST = 0x00000008;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const int SW_RESTORE = 9;
        private const int SW_SHOW = 5;
        private const int PORT = 47821;
        private const int WM_DISPLAYCHANGE = 0x007E;
        private const int WM_DPICHANGED = 0x02E0;
        private const int WM_HOTKEY = 0x0312;
        private const int WM_INPUT = 0x00FF;
        private const uint RIDEV_REMOVE = 0x00000001;
        private const uint RIDEV_INPUTSINK = 0x00000100;
        private const uint RID_HEADER = 0x10000005;
        private const uint RIM_TYPEKEYBOARD = 1;
        private const int HOTKEY_ID = 0xA970;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_WIN = 0x0008;
        private const uint MOD_NOREPEAT = 0x4000;
        private const uint VK_P = 0x50;

        private const double BaseWidth = 236.0;
        private const double BaseHeight = 220.0;
        private const double BaseSpriteBox = 136.0;

        private readonly Grid _rootGrid;
        private readonly Grid _bubbleHost;
        private readonly Border _stackBack2;
        private readonly Border _stackBack1;
        private readonly Border _bubbleBorder;
        private readonly TextBlock _bubbleText;
        private readonly StackPanel _expandedStackPanel;
        private readonly DispatcherTimer _bubbleCollapseTimer;
        private bool _bubbleExpanded = false;
        private double _baseWindowHeight = BaseHeight;
        private readonly Grid _stageGrid;
        private readonly Ellipse _statusHalo;
        private readonly Ellipse _floorShadow;
        private readonly Image _spriteImage;
        private readonly Dictionary<string, BitmapSource> _composedFrameCache = new Dictionary<string, BitmapSource>();
        private readonly ScaleTransform _facingScale;
        private readonly ScaleTransform _animScale;
        private readonly ScaleTransform _dragScale;
        private readonly RotateTransform _animRotate;
        private readonly RotateTransform _dragRotate;
        private readonly TranslateTransform _animTranslate;
        private double _dragTargetAngle = 0;
        private double _dragCurrentAngle = 0;
        private double _lastDragScreenX = 0;
        private double _animTimeSec = 0;
        private double _bounceElapsedSec = -1.0;
        private bool _bounceWithHop = false;
        private bool _flipX = false;

        private double _overlayElapsedSec = -1.0;
        private double _overlayDurationSec = 1.6;
        private bool _overlayDriftUp = true;

        private enum BoredomTier { Normal = 0, MildBored = 1, Sleeping = 2, WakeUp = 3 }
        private BoredomTier _boredomTier = BoredomTier.Normal;
        private DateTime _idleActivityAt = DateTime.UtcNow;
        private DateTime _nextBoredActionAt = DateTime.UtcNow.AddSeconds(24);
        private DateTime _nextSleepZzzAt = DateTime.UtcNow;
        private double _wakeUpLeftSec = 0.0;
        private double _lookAroundLeftSec = 0.0;
        private bool _boredLookFlipX = false;

        private bool _rawKeyboardRegistered = false;
        private readonly Queue<DateTime> _keyWindow = new Queue<DateTime>();
        private DateTime _lastKeyAt = DateTime.MinValue;
        private DateTime _lastStarAt = DateTime.MinValue;
        private double _typingTapPulse = 0.0;
        private int _typingTapSign = 1;
        private DateTime _lastMouseFlipAt = DateTime.MinValue;
        private bool _mouseFlipX = false;
        private readonly TextBlock _fallbackLabel;
        private readonly Border _badgeBorder;
        private readonly TextBlock _badgeText;
        private readonly Border _overlayBorder;
        private readonly TextBlock _overlayText;
        private readonly TranslateTransform _overlayTranslate;
        private readonly Canvas _dizzyCanvas;
        private readonly ScaleTransform _dizzyScale;
        private readonly Polygon[] _dizzyStars = new Polygon[3];
        private readonly TranslateTransform[] _dizzyStarPos = new TranslateTransform[3];
        private readonly ScaleTransform[] _dizzyStarScale = new ScaleTransform[3];
        private readonly RotateTransform[] _dizzyStarRot = new RotateTransform[3];
        private double _dizzyLeftSec = 0.0;
        private double _dizzyAngle = 0.0;

        private readonly DispatcherTimer _animTimer;
        private readonly DispatcherTimer _behaviorTimer;
        private readonly DispatcherTimer _autoIdleTimer;
        private readonly DispatcherTimer _mouseFollowTimer;
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer();
        private readonly Random _rng = new Random();
        private readonly DateTime _sessionStart = DateTime.Now;

        private readonly Dictionary<string, PetPack> _packs = new Dictionary<string, PetPack>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TaskItem> _tasks = new Dictionary<string, TaskItem>(StringComparer.OrdinalIgnoreCase);
        private readonly List<RecentConversation> _recentConversations = new List<RecentConversation>();
        private readonly List<PendingApproval> _pendingApprovals = new List<PendingApproval>();
        private readonly object _approvalLock = new object();
        private bool _hiddenByFullscreen = false;
        private bool _fullscreenPeekActive = false;
        private double _savedNormalLeft = -1;
        private double _savedNormalTop = -1;
        private string _dockSide = "";
        private bool _dockPeekOut = false;
        private DateTime _dockHoverLastSeen = DateTime.MinValue;
        private DateTime _dockIgnoreHoverUntil = DateTime.MinValue;
        private DispatcherTimer _singleClickTimer;
        private bool _isDoubleClickGesture = false;
        private string _pendingSingleConvId = "";
        private string _pendingSingleTitle = "";
        private Border _approvalBar;
        private StackPanel _bubbleWrapperPanel;
        private string _availableUpdateVersion = "";
        private string _availableUpdateUrl = "";
        private string _availableUpdateZipUrl = "";
        private DateTime _updatePromptShownAt = DateTime.MinValue;
        private bool _isUpdatingSelf = false;
        private string _selfUpdateStatusMsg = "";

        private HttpListener _listener;
        private Thread _httpThread;
        private string _httpToken = "";
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
        private bool _lastDragWasTouch = false;
        private bool _suppressDockedHoverUntilCursorMoves = false;
        private bool _requireHoverExitBeforePeek = false;
        private bool _dockHoverAnchorNeedsCapture = false;
        private Point _dockTouchReleaseCursorPos;
        private int _activeTouchId = -1;
        private DateTime _lastTouchTapAt = DateTime.MinValue;
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

            Title = "Antigravity Pet";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = _config.Topmost;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;

            SyncAutoStartRegistry(_config.AutoStartOnBoot);
            SaveConfig();

            _rootGrid = new Grid { Background = Brushes.Transparent };
            _rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Content = _rootGrid;

            // 顶部可点击居中聊天框气泡（支持简易单气泡 & 详细堆叠展开模式）
            _bubbleHost = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Background = Brushes.Transparent,
                Margin = new Thickness(4, 4, 4, -2)
            };
            Grid.SetRow(_bubbleHost, 0);
            _rootGrid.Children.Add(_bubbleHost);

            _stackBack2 = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(228, 30, 41, 59)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(210, 148, 163, 184)),
                BorderThickness = new Thickness(2.0),
                CornerRadius = new CornerRadius(2),
                SnapsToDevicePixels = true,
                Height = 26,
                MinWidth = 100,
                Margin = new Thickness(0, 0, 0, 12),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false
            };
            _bubbleHost.Children.Add(_stackBack2);

            _stackBack1 = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(240, 23, 33, 52)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(230, 56, 189, 248)),
                BorderThickness = new Thickness(2.0),
                CornerRadius = new CornerRadius(2),
                SnapsToDevicePixels = true,
                Height = 26,
                MinWidth = 114,
                Margin = new Thickness(0, 0, 0, 6),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false
            };
            _bubbleHost.Children.Add(_stackBack1);

            _bubbleText = new TextBlock
            {
                Text = "",
                Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                FontFamily = new FontFamily("Consolas, Microsoft YaHei UI"),
                FontSize = 11.5,
                FontWeight = FontWeights.Bold,
                TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 208
            };
            _bubbleWrapperPanel = new StackPanel { Orientation = Orientation.Vertical };
            _bubbleWrapperPanel.Children.Add(_bubbleText);

            _approvalBar = new Border
            {
                Margin = new Thickness(0, 5, 0, 0),
                Visibility = Visibility.Collapsed
            };
            var appRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            var btnAllow = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(230, 16, 185, 129)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(110, 231, 183)),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(8, 2, 8, 2),
                Margin = new Thickness(0, 0, 6, 0),
                Cursor = Cursors.Hand,
                Child = new TextBlock { Text = "✓ 允许", Foreground = Brushes.White, FontSize = 10.5, FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Consolas, Microsoft YaHei UI") }
            };
            var btnDeny = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(230, 239, 68, 68)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(252, 165, 165)),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(8, 2, 8, 2),
                Cursor = Cursors.Hand,
                Child = new TextBlock { Text = "✕ 拒绝", Foreground = Brushes.White, FontSize = 10.5, FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Consolas, Microsoft YaHei UI") }
            };
            btnAllow.MouseLeftButtonUp += (s, e) => { e.Handled = true; ResolveCurrentApproval("allow"); };
            btnDeny.MouseLeftButtonUp += (s, e) => { e.Handled = true; ResolveCurrentApproval("deny"); };
            appRow.Children.Add(btnAllow);
            appRow.Children.Add(btnDeny);
            _approvalBar.Child = appRow;
            _bubbleWrapperPanel.Children.Add(_approvalBar);

            _bubbleBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(245, 15, 23, 42)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 56, 189, 248)),
                BorderThickness = new Thickness(2.0),
                CornerRadius = new CornerRadius(2),
                SnapsToDevicePixels = true,
                Padding = new Thickness(10, 5, 10, 5),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Child = _bubbleWrapperPanel,
                Visibility = Visibility.Hidden,
                IsHitTestVisible = true,
                Cursor = Cursors.Hand,
                ToolTip = "点击打开对应 Antigravity 对话",
                Effect = new DropShadowEffect { BlurRadius = 0, ShadowDepth = 3, Direction = 315, Color = Colors.Black, Opacity = 0.85 }
            };
            _bubbleHost.Children.Add(_bubbleBorder);

            _expandedStackPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Visibility = Visibility.Collapsed
            };
            _bubbleHost.Children.Add(_expandedStackPanel);

            _bubbleCollapseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
            _bubbleCollapseTimer.Tick += (s, e) =>
            {
                _bubbleCollapseTimer.Stop();
                if (_bubbleExpanded && !_bubbleHost.IsMouseOver)
                {
                    _bubbleExpanded = false;
                    UpdateVisualState();
                }
            };
            _bubbleHost.MouseEnter += (s, e) =>
            {
                _bubbleCollapseTimer.Stop();
                if (IsDetailedStyle() && _tasks.Count > 0 && !_bubbleExpanded)
                {
                    _bubbleExpanded = true;
                    UpdateVisualState();
                }
            };
            _bubbleHost.MouseLeave += (s, e) =>
            {
                if (_bubbleExpanded)
                {
                    _bubbleCollapseTimer.Stop();
                    _bubbleCollapseTimer.Start();
                }
            };

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

            _facingScale = new ScaleTransform(1.0, 1.0);
            _animScale = new ScaleTransform(1.0, 1.0);
            _dragScale = new ScaleTransform(1.0, 1.0);
            _animRotate = new RotateTransform(0.0);
            _dragRotate = new RotateTransform(0.0);
            _animTranslate = new TranslateTransform(0.0, 0.0);

            var spriteTransforms = new TransformGroup();
            spriteTransforms.Children.Add(_facingScale);
            spriteTransforms.Children.Add(_animScale);
            spriteTransforms.Children.Add(_dragScale);
            spriteTransforms.Children.Add(_animRotate);
            spriteTransforms.Children.Add(_dragRotate);
            spriteTransforms.Children.Add(_animTranslate);

            _spriteImage = new Image
            {
                Width = BaseSpriteBox,
                Height = BaseSpriteBox,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                SnapsToDevicePixels = false,
                UseLayoutRounding = false,
                RenderTransformOrigin = new Point(0.5, 1.0),
                RenderTransform = spriteTransforms
            };
            // 使用硬件加速高质量重采样抗锯齿（兼顾极低性能开销与旋转/缩放平滑度）
            RenderOptions.SetBitmapScalingMode(_spriteImage, BitmapScalingMode.HighQuality);
            RenderOptions.SetEdgeMode(_spriteImage, EdgeMode.Unspecified);
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

            _overlayTranslate = new TranslateTransform(0, 0);
            _overlayBorder = new Border
            {
                Padding = new Thickness(5, 1, 5, 2),
                CornerRadius = new CornerRadius(5),
                Background = new SolidColorBrush(Color.FromArgb(225, 15, 23, 42)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(200, 148, 163, 184)),
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 0, 0),
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false,
                RenderTransform = _overlayTranslate
            };
            _overlayText = new TextBlock
            {
                Text = "Zzz",
                Foreground = new SolidColorBrush(Color.FromRgb(186, 230, 253)),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _overlayBorder.Child = _overlayText;
            _stageGrid.Children.Add(_overlayBorder);

            _dizzyScale = new ScaleTransform(1.0, 1.0);
            _dizzyCanvas = new Canvas
            {
                Width = 84,
                Height = 30,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, -4, 0, 0),
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = _dizzyScale
            };
            var dizzyRing = new Ellipse
            {
                Width = 64,
                Height = 16,
                Stroke = new SolidColorBrush(Color.FromArgb(165, 250, 204, 21)),
                StrokeThickness = 1.6,
                StrokeDashArray = new DoubleCollection { 3.0, 2.5 },
                IsHitTestVisible = false
            };
            Canvas.SetLeft(dizzyRing, 10);
            Canvas.SetTop(dizzyRing, 7);
            _dizzyCanvas.Children.Add(dizzyRing);

            var starPts = new PointCollection
            {
                new Point(0, -7.5), new Point(2.1, -2.1),
                new Point(7.5, 0), new Point(2.1, 2.1),
                new Point(0, 7.5), new Point(-2.1, 2.1),
                new Point(-7.5, 0), new Point(-2.1, -2.1)
            };
            for (int i = 0; i < 3; i++)
            {
                _dizzyStarScale[i] = new ScaleTransform(1.0, 1.0);
                _dizzyStarRot[i] = new RotateTransform(0.0);
                _dizzyStarPos[i] = new TranslateTransform(42, 15);
                var tg = new TransformGroup();
                tg.Children.Add(_dizzyStarScale[i]);
                tg.Children.Add(_dizzyStarRot[i]);
                tg.Children.Add(_dizzyStarPos[i]);

                var star = new Polygon
                {
                    Points = starPts,
                    Fill = new SolidColorBrush(Color.FromRgb(250, 204, 21)),
                    Stroke = new SolidColorBrush(Color.FromRgb(254, 249, 195)),
                    StrokeThickness = 1.1,
                    RenderTransform = tg,
                    IsHitTestVisible = false,
                    Effect = new DropShadowEffect { Color = Color.FromRgb(245, 158, 11), BlurRadius = 6, ShadowDepth = 0, Opacity = 0.85 }
                };
                _dizzyStars[i] = star;
                _dizzyCanvas.Children.Add(star);
            }
            _stageGrid.Children.Add(_dizzyCanvas);

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

            _mouseFollowTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _mouseFollowTimer.Tick += OnMouseFollowTick;
            if (_config.KeyMouseInteract) _mouseFollowTimer.Start();

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
            UpdateAccessoryVisual();
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);
                ApplyTopmost(_config.Topmost, true);
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
            try { SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged; } catch { }
            SyncKeyMouseListeners();
            ApplyScale(_config.Scale, false);
            EnsureInsideScreenBounds(false);
            if (_config.CheckUpdatesOnStartup) CheckGitHubReleaseAsync();
        }

        private void SyncKeyMouseListeners()
        {
            if (_config.KeyMouseInteract)
            {
                RegisterRawKeyboard();
                if (_mouseFollowTimer != null && !_mouseFollowTimer.IsEnabled) _mouseFollowTimer.Start();
            }
            else
            {
                UnregisterRawKeyboard();
                if (_mouseFollowTimer != null && _mouseFollowTimer.IsEnabled) _mouseFollowTimer.Stop();
                _keyWindow.Clear();
                _lastKeyAt = DateTime.MinValue;
                _mouseFlipX = false;
            }
        }

        private void RegisterRawKeyboard()
        {
            if (_rawKeyboardRegistered) return;
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;
                var rid = new RAWINPUTDEVICE[]
                {
                    new RAWINPUTDEVICE
                    {
                        usUsagePage = 0x01,
                        usUsage = 0x06,
                        dwFlags = RIDEV_INPUTSINK,
                        hwndTarget = hwnd
                    }
                };
                if (RegisterRawInputDevices(rid, 1, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE))))
                {
                    _rawKeyboardRegistered = true;
                }
            }
            catch { }
        }

        private void UnregisterRawKeyboard()
        {
            if (!_rawKeyboardRegistered) return;
            try
            {
                var rid = new RAWINPUTDEVICE[]
                {
                    new RAWINPUTDEVICE
                    {
                        usUsagePage = 0x01,
                        usUsage = 0x06,
                        dwFlags = RIDEV_REMOVE,
                        hwndTarget = IntPtr.Zero
                    }
                };
                RegisterRawInputDevices(rid, 1, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE)));
            }
            catch { }
            _rawKeyboardRegistered = false;
        }

        private void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            if (_isClosing) return;
            try
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    ApplyScale(_config.Scale, false);
                    EnsureInsideScreenBounds(true);
                }));
            }
            catch { }
        }

        private void ApplyTopmost(bool topmost, bool frameChanged = false)
        {
            _config.Topmost = topmost;
            if (topmost && Topmost)
            {
                Topmost = false;
            }
            Topmost = topmost;
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                uint flags = SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE;
                if (frameChanged) flags |= SWP_FRAMECHANGED;
                if (IsVisible) flags |= SWP_SHOWWINDOW;
                SetWindowPos(hwnd, topmost ? HWND_TOPMOST : HWND_NOTOPMOST, 0, 0, 0, 0, flags);
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                ToggleVisibility();
                handled = true;
            }
            else if (msg == WM_INPUT && _config.KeyMouseInteract && !_isClosing)
            {
                try
                {
                    uint headerSize = (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER));
                    uint size = headerSize;
                    RAWINPUTHEADER hdr;
                    // 仅读取 RAWINPUTHEADER 验证设备类型，绝不解析或记录具体按键内容
                    if (GetRawInputData(lParam, RID_HEADER, out hdr, ref size, headerSize) != unchecked((uint)-1) &&
                        hdr.dwType == RIM_TYPEKEYBOARD)
                    {
                        OnGlobalKeyPressed();
                    }
                }
                catch { }
            }
            else if (msg == WM_DISPLAYCHANGE || msg == WM_DPICHANGED)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    ApplyScale(_config.Scale, false);
                    EnsureInsideScreenBounds(true);
                }));
            }
            return IntPtr.Zero;
        }

        private void OnWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _isClosing = true;
            SaveConfig();
            try { UnregisterRawKeyboard(); } catch { }
            try { if (_mouseFollowTimer != null) _mouseFollowTimer.Stop(); } catch { }
            try { SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged; } catch { }
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
            double vsLeft = SystemParameters.VirtualScreenLeft;
            double vsTop = SystemParameters.VirtualScreenTop;
            double vsRight = vsLeft + SystemParameters.VirtualScreenWidth;
            double vsBottom = vsTop + SystemParameters.VirtualScreenHeight;
            if (_config.Left >= vsLeft && _config.Top >= vsTop &&
                _config.Left < vsRight - 40 && _config.Top < vsBottom - 40)
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
            _dockSide = "";
            _dockPeekOut = false;
            _bubbleHost.HorizontalAlignment = HorizontalAlignment.Center;
            var wa = SystemParameters.WorkArea;
            Left = wa.Right - Width - 28;
            Top = wa.Bottom - Height - 24;
            UpdateVisualState();
            if (save) SaveConfig();
        }

        private double GetDpiScale()
        {
            try
            {
                var src = PresentationSource.FromVisual(this);
                if (src != null && src.CompositionTarget != null)
                {
                    double m11 = src.CompositionTarget.TransformToDevice.M11;
                    if (m11 > 0.1) return m11;
                }
            }
            catch { }
            try
            {
                using (var g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero))
                {
                    if (g.DpiX > 10) return g.DpiX / 96.0;
                }
            }
            catch { }
            return 1.0;
        }

        private void ApplyScale(double scale, bool save)
        {
            if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0) scale = 1.0;
            if (scale < 0.33) scale = 0.33;
            if (scale > 3.0) scale = 3.0;
            _config.Scale = Math.Round(scale, 2);
            if (_rootGrid != null)
            {
                _rootGrid.LayoutTransform = Transform.Identity;
            }
            _baseWindowHeight = Math.Round(BaseHeight * _config.Scale);
            Width = Math.Round(BaseWidth * _config.Scale);
            if (!_bubbleExpanded)
            {
                Height = Math.Round(_baseWindowHeight);
            }
            _stageGrid.Width = Math.Round(BaseSpriteBox * _config.Scale);
            _stageGrid.Height = Math.Round(BaseSpriteBox * _config.Scale);
            _spriteImage.Width = Math.Round(BaseSpriteBox * _config.Scale);
            _spriteImage.Height = Math.Round(BaseSpriteBox * _config.Scale);
            _statusHalo.Width = Math.Round(132 * _config.Scale);
            _statusHalo.Height = Math.Round(132 * _config.Scale);
            _floorShadow.Width = Math.Round(74 * _config.Scale);
            _floorShadow.Height = Math.Round(14 * _config.Scale);
            _bubbleText.MaxWidth = Math.Round((BaseWidth - 28) * _config.Scale);
            _bubbleHost.Margin = new Thickness(4, 4, 4, Math.Round(-2 * _config.Scale));
            double badgeSize = Math.Round(22 * _config.Scale);
            _badgeBorder.Width = badgeSize;
            _badgeBorder.Height = badgeSize;
            _badgeBorder.CornerRadius = new CornerRadius(badgeSize / 2.0);
            _badgeBorder.Margin = new Thickness(0, Math.Round(12 * _config.Scale), Math.Round(8 * _config.Scale), 0);
            _badgeText.FontSize = Math.Max(9.0, Math.Round(11 * _config.Scale));
            _overlayBorder.Padding = new Thickness(Math.Round(5 * _config.Scale), Math.Round(1 * _config.Scale), Math.Round(5 * _config.Scale), Math.Round(2 * _config.Scale));
            _overlayBorder.CornerRadius = new CornerRadius(Math.Round(5 * _config.Scale));
            _overlayBorder.Margin = new Thickness(0, Math.Round(2 * _config.Scale), 0, 0);
            _overlayText.FontSize = Math.Max(9.0, Math.Round(11 * _config.Scale));
            _dizzyScale.ScaleX = _config.Scale;
            _dizzyScale.ScaleY = _config.Scale;
            _dizzyCanvas.Margin = new Thickness(0, Math.Round(-4 * _config.Scale), 0, 0);
            if (IsLoaded) UpdateVisualState();
            ClampToWorkArea();
            if (save) SaveConfig();
        }

        private void ClampToWorkArea()
        {
            var wa = GetCurrentMonitorWorkArea();
            if (_fullscreenPeekActive)
            {
                if (Top < wa.Top) Top = wa.Top;
                if (Top > wa.Bottom - Height) Top = Math.Max(wa.Top, wa.Bottom - Height);
                return;
            }

            if (!string.IsNullOrEmpty(_dockSide))
            {
                Left = GetDockedTargetLeft(_dockSide, _dockPeekOut);
            }
            else
            {
                if (Left < wa.Left) Left = wa.Left;
                if (Left > wa.Right - Width) Left = Math.Max(wa.Left, wa.Right - Width);
            }
            if (Top < wa.Top) Top = wa.Top;
            if (Top > wa.Bottom - Height) Top = Math.Max(wa.Top, wa.Bottom - Height);
        }

        private void EnsureInsideScreenBounds(bool saveIfChanged)
        {
            if (_isClosing || _isDragging) return;
            double oldLeft = Left;
            double oldTop = Top;
            ClampToWorkArea();
            if (Math.Abs(Left - oldLeft) > 0.5 || Math.Abs(Top - oldTop) > 0.5)
            {
                if (_transient == TransientAction.Walk)
                {
                    CancelWalk();
                }
                if (saveIfChanged)
                {
                    SaveConfig();
                }
            }
        }

        private void ReloadPetPacks()
        {
            _spriteImage.Source = null;
            _composedFrameCache.Clear();
            if (_currentPack != null)
            {
                _currentPack.UnloadAssets();
                _currentPack = null;
            }
            foreach (var oldPack in _packs.Values)
            {
                oldPack.UnloadAssets();
            }
            _packs.Clear();

            LoadPacksFromDir(_pluginPetsDir);
            LoadPacksFromDir(_userPetsDir);

            string desired = ResolveSkinAlias(_config.Skin);
            if (_packs.ContainsKey(desired) && EnsurePackLoaded(_packs[desired]))
            {
                _currentPack = _packs[desired];
            }
            else
            {
                foreach (var candidate in _packs.Values)
                {
                    if (EnsurePackLoaded(candidate))
                    {
                        _currentPack = candidate;
                        _config.Skin = _currentPack.Id;
                        break;
                    }
                }
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
                if (ver < 1 || ver > 3) ver = 2;

                Color themeColor = ParseHexColor(GetStr(meta, "themeColor", ""), Color.FromRgb(56, 189, 248));
                var soundsMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (meta.ContainsKey("sounds") && meta["sounds"] is Dictionary<string, object>)
                {
                    var sDict = (Dictionary<string, object>)meta["sounds"];
                    foreach (var kv in sDict)
                    {
                        if (kv.Value != null)
                        {
                            string sp = System.IO.Path.Combine(dir, kv.Value.ToString());
                            if (File.Exists(sp)) soundsMap[kv.Key] = sp;
                        }
                    }
                }

                var pack = new PetPack
                {
                    Id = id,
                    DisplayName = displayName,
                    Description = desc,
                    FolderPath = dir,
                    SpriteVersion = ver,
                    ThemeColor = themeColor,
                    Sounds = soundsMap
                };
                for (int r = 0; r < 9; r++) pack.Rows[r] = new List<BitmapSource>();

                // 1. 探测 spritesheet 图集
                string sheetRel = GetStr(meta, "spritesheetPath", "spritesheet.png");
                string sheetPath = System.IO.Path.Combine(dir, sheetRel);
                if (!File.Exists(sheetPath))
                {
                    string altPng = System.IO.Path.Combine(dir, "spritesheet.png");
                    string altWebp = System.IO.Path.Combine(dir, "spritesheet.webp");
                    if (File.Exists(altPng)) sheetPath = altPng;
                    else if (File.Exists(altWebp)) sheetPath = altWebp;
                    else sheetPath = null;
                }
                pack.SpritesheetPath = sheetPath;

                // 2. 探测序列帧目录（idle/ 等）
                if (sheetPath == null)
                {
                    string idleSubDir = System.IO.Path.Combine(dir, "idle");
                    if (Directory.Exists(idleSubDir) &&
                        (Directory.GetFiles(idleSubDir, "*.png").Length > 0 || Directory.GetFiles(idleSubDir, "*.webp").Length > 0))
                    {
                        pack.HasFolderFrames = true;
                    }
                }

                // 3. 探测极简单张基础图（idleImage / image / idle.png / pet.png / avatar.png / 根目录首张图片）
                if (sheetPath == null && !pack.HasFolderFrames)
                {
                    string idleRel = GetStr(meta, "idleImage", "");
                    if (string.IsNullOrEmpty(idleRel)) idleRel = GetStr(meta, "image", "");
                    string[] candidates = new string[]
                    {
                        string.IsNullOrEmpty(idleRel) ? null : System.IO.Path.Combine(dir, idleRel),
                        System.IO.Path.Combine(dir, "idle.png"),
                        System.IO.Path.Combine(dir, "idle.webp"),
                        System.IO.Path.Combine(dir, "pet.png"),
                        System.IO.Path.Combine(dir, "pet.webp"),
                        System.IO.Path.Combine(dir, "avatar.png"),
                        System.IO.Path.Combine(dir, "avatar.webp")
                    };
                    foreach (string c in candidates)
                    {
                        if (!string.IsNullOrEmpty(c) && File.Exists(c))
                        {
                            pack.IdleImagePath = c;
                            break;
                        }
                    }
                    if (string.IsNullOrEmpty(pack.IdleImagePath))
                    {
                        var rootImgs = Directory.GetFiles(dir, "*.png")
                            .Concat(Directory.GetFiles(dir, "*.webp"))
                            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                            .FirstOrDefault();
                        if (!string.IsNullOrEmpty(rootImgs))
                        {
                            pack.IdleImagePath = rootImgs;
                        }
                    }
                }

                if (string.IsNullOrEmpty(pack.SpritesheetPath) && !pack.HasFolderFrames && string.IsNullOrEmpty(pack.IdleImagePath))
                {
                    return null;
                }

                return pack;
            }
            catch
            {
                return null;
            }
        }

        private static BitmapSource LoadFrozenBitmap(string filePath)
        {
            using (var ms = new MemoryStream(File.ReadAllBytes(filePath)))
            {
                var bmp = BitmapFrame.Create(ms, BitmapCreateOptions.IgnoreImageCache, BitmapCacheOption.OnLoad);
                bmp.Freeze();
                return bmp;
            }
        }

        private bool EnsurePackLoaded(PetPack pack)
        {
            if (pack == null) return false;
            if (pack.AssetsLoaded && pack.Rows[0] != null && pack.Rows[0].Count > 0) return true;

            try
            {
                pack.UnloadAssets();

                // 模式 A: 精灵图集 (8x9 / 8x11)
                if (!string.IsNullOrEmpty(pack.SpritesheetPath) && File.Exists(pack.SpritesheetPath))
                {
                    BitmapSource raw = LoadFrozenBitmap(pack.SpritesheetPath);
                    FormatConvertedBitmap bgra = new FormatConvertedBitmap(raw, PixelFormats.Bgra32, null, 0);
                    bgra.Freeze();

                    int cols = 8;
                    int rows = (pack.SpriteVersion == 1) ? 9 : 11;
                    int cellW = bgra.PixelWidth / cols;
                    int cellH = bgra.PixelHeight / rows;
                    if (cellW > 0 && cellH > 0)
                    {
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

                                if (r < 9) pack.Rows[r].Add(crop);
                                else pack.LookFrames.Add(crop);
                            }
                        }
                    }

                    if (pack.Rows[0].Count > 0)
                    {
                        pack.AssetsLoaded = true;
                        return true;
                    }
                    pack.UnloadAssets();
                }

                // 模式 B: 序列帧子目录（仅 idle/ 为必需，其他状态缺失时由程序动画自动回退）
                if (pack.HasFolderFrames)
                {
                    string[] rowFolders = { "idle", "running-right", "running-left", "waving", "jumping", "failed", "waiting", "running", "review" };
                    for (int r = 0; r < rowFolders.Length; r++)
                    {
                        string subDir = System.IO.Path.Combine(pack.FolderPath, rowFolders[r]);
                        if (!Directory.Exists(subDir)) continue;
                        var files = Directory.GetFiles(subDir, "*.png")
                            .Concat(Directory.GetFiles(subDir, "*.webp"))
                            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
                        foreach (string imgPath in files)
                        {
                            pack.Rows[r].Add(LoadFrozenBitmap(imgPath));
                        }
                    }
                    if (pack.Rows[0].Count > 0)
                    {
                        pack.AssetsLoaded = true;
                        return true;
                    }
                    pack.UnloadAssets();
                }

                // 模式 C: 极简单图模式（仅靠 1 张基础待机图即可运行）
                if (!string.IsNullOrEmpty(pack.IdleImagePath) && File.Exists(pack.IdleImagePath))
                {
                    pack.Rows[0].Add(LoadFrozenBitmap(pack.IdleImagePath));
                    pack.AssetsLoaded = true;
                    return true;
                }

                return false;
            }
            catch
            {
                pack.UnloadAssets();
                return false;
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
            PetPack target;
            if (_packs.TryGetValue(resolved, out target))
            {
                if (!EnsurePackLoaded(target)) return;

                if (_currentPack != null && !ReferenceEquals(_currentPack, target))
                {
                    _spriteImage.Source = null;
                    _currentPack.UnloadAssets();
                }

                _currentPack = target;
                _config.Skin = _currentPack.Id;
                _animFrame = 0;
                _composedFrameCache.Clear();
                _spriteImage.Visibility = Visibility.Visible;
                _fallbackLabel.Visibility = Visibility.Collapsed;
                SaveConfig();
                RebuildContextMenu();
                RenderCurrentFrame();
            }
        }

        private void SetupMouseEvents()
        {
            // 气泡聊天框可点击：若有新版本提示则新建对话让 Antigravity 安装，否则打开对应对话
            _bubbleBorder.MouseEnter += (s, e) =>
            {
                if (!string.IsNullOrEmpty(_availableUpdateVersion))
                {
                    _bubbleBorder.Background = new SolidColorBrush(Color.FromArgb(250, 59, 22, 128));
                }
                else
                {
                    _bubbleBorder.Background = new SolidColorBrush(Color.FromArgb(248, 30, 41, 59));
                }
                _bubbleBorder.BorderThickness = new Thickness(1.8);
            };
            _bubbleBorder.MouseLeave += (s, e) =>
            {
                if (!string.IsNullOrEmpty(_availableUpdateVersion))
                {
                    _bubbleBorder.Background = new SolidColorBrush(Color.FromArgb(245, 46, 16, 101));
                }
                else
                {
                    _bubbleBorder.Background = new SolidColorBrush(Color.FromArgb(236, 15, 23, 42));
                }
                _bubbleBorder.BorderThickness = new Thickness(1.2);
            };
            _bubbleBorder.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                if (!string.IsNullOrEmpty(_availableUpdateVersion) && !_isUpdatingSelf)
                {
                    StartTransient(TransientAction.Jump, 5);
                    PerformSelfUpdateAsync(_availableUpdateVersion, _availableUpdateZipUrl);
                    return;
                }
                if (_isUpdatingSelf) return;

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

            _singleClickTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(240) };
            _singleClickTimer.Tick += (ts, te) =>
            {
                _singleClickTimer.Stop();
                if (_transient == TransientAction.Walk) CancelWalk();
                if (_rng.Next(0, 2) == 0) StartTransient(TransientAction.Jump, 5);
                else StartTransient(TransientAction.Wave, 8);
            };

            Stylus.SetIsPressAndHoldEnabled(_stageGrid, false);
            Stylus.SetIsFlicksEnabled(_stageGrid, false);
            Stylus.SetIsTapFeedbackEnabled(_stageGrid, false);
            Stylus.SetIsTouchFeedbackEnabled(_stageGrid, false);

            _stageGrid.MouseLeftButtonDown += (s, e) =>
            {
                if (_activeTouchId != -1) return;
                _suppressDockedHoverUntilCursorMoves = false;
                BeginDrag(GetCursorLogicalPos(e), e.ClickCount >= 2, false);
                _stageGrid.CaptureMouse();
            };

            _stageGrid.MouseMove += (s, e) =>
            {
                if (!_isDragging || _activeTouchId != -1) return;
                UpdateDrag(GetCursorLogicalPos(e), 4.0);
            };

            _stageGrid.MouseLeftButtonUp += (s, e) =>
            {
                if (!_isDragging || _activeTouchId != -1) return;
                _stageGrid.ReleaseMouseCapture();
                EndDrag(_isDoubleClickGesture || e.ClickCount >= 2);
            };

            _stageGrid.TouchDown += (s, e) =>
            {
                if (_activeTouchId != -1 && _activeTouchId != e.TouchDevice.Id) return;
                _activeTouchId = e.TouchDevice.Id;
                var now = DateTime.Now;
                bool isDoubleTap = (now - _lastTouchTapAt).TotalMilliseconds <= 350.0;
                _lastTouchTapAt = isDoubleTap ? DateTime.MinValue : now;
                BeginDrag(WindowPosToLogicalScreen(e.GetTouchPoint(this).Position), isDoubleTap, true);
                _stageGrid.CaptureTouch(e.TouchDevice);
                e.Handled = true;
            };

            _stageGrid.TouchMove += (s, e) =>
            {
                if (!_isDragging || e.TouchDevice.Id != _activeTouchId) return;
                UpdateDrag(WindowPosToLogicalScreen(e.GetTouchPoint(this).Position), 6.0);
                if (_movedDuringDrag) _lastTouchTapAt = DateTime.MinValue;
                e.Handled = true;
            };

            _stageGrid.TouchUp += (s, e) =>
            {
                if (e.TouchDevice.Id != _activeTouchId) return;
                _activeTouchId = -1;
                if (_stageGrid.TouchesCaptured.Contains(e.TouchDevice))
                {
                    _stageGrid.ReleaseTouchCapture(e.TouchDevice);
                }
                if (_isDragging)
                {
                    EndDrag(_isDoubleClickGesture);
                }
                e.Handled = true;
            };

            _stageGrid.LostTouchCapture += (s, e) =>
            {
                if (e.TouchDevice.Id != _activeTouchId) return;
                _activeTouchId = -1;
                if (_isDragging)
                {
                    EndDrag(false);
                }
            };
        }

        private void BeginDrag(Point startScreenPos, bool isDoubleClick, bool isTouch)
        {
            InterruptBoredom(true);
            if (_transient == TransientAction.Walk) CancelWalk();
            _lastDragWasTouch = isTouch;
            _isDoubleClickGesture = isDoubleClick;
            if (_isDoubleClickGesture) _singleClickTimer.Stop();
            _isDragging = true;
            _movedDuringDrag = false;
            _dragStartScreen = startScreenPos;
            _lastDragScreenX = startScreenPos.X;
            _dragTargetAngle = 0;
            _dragStartWindow = new Point(Left, Top);
        }

        private void UpdateDrag(Point currentScreenPos, double threshold)
        {
            double dx = currentScreenPos.X - _dragStartScreen.X;
            double dy = currentScreenPos.Y - _dragStartScreen.Y;
            double vx = currentScreenPos.X - _lastDragScreenX;
            _lastDragScreenX = currentScreenPos.X;

            if (_movedDuringDrag || Math.Abs(dx) > threshold || Math.Abs(dy) > threshold)
            {
                _movedDuringDrag = true;
                _singleClickTimer.Stop();
                Left = _dragStartWindow.X + dx;
                Top = _dragStartWindow.Y + dy;
                _dragTargetAngle = Math.Max(-22.0, Math.Min(22.0, -vx * 0.85));
                _floorShadow.Opacity = 0.08;
                RenderCurrentFrame();
            }
        }

        private void EndDrag(bool isDoubleClick)
        {
            if (!_isDragging) return;
            _isDragging = false;
            _dragTargetAngle = 0;
            _floorShadow.Opacity = 0.22;

            if (_movedDuringDrag)
            {
                _singleClickTimer.Stop();
                SnapToScreenEdgeIfNear();
                ClampToWorkArea();
                SaveConfig();
                StartTransient(TransientAction.Jump, 5);
            }
            else if (isDoubleClick)
            {
                _isDoubleClickGesture = false;
                _singleClickTimer.Stop();
                StartTransient(TransientAction.Wave, 6);
                ShowQuickDispatchWindow();
            }
            else
            {
                _singleClickTimer.Stop();
                _singleClickTimer.Start();
            }
        }

        private Point WindowPosToLogicalScreen(Point localPos)
        {
            Point devicePt = PointToScreen(localPos);
            var src = PresentationSource.FromVisual(this);
            if (src != null && src.CompositionTarget != null)
            {
                return src.CompositionTarget.TransformFromDevice.Transform(devicePt);
            }
            return devicePt;
        }

        private Point GetCursorLogicalPos(MouseEventArgs fallbackArgs)
        {
            POINT pt;
            if (GetCursorPos(out pt))
            {
                var src = PresentationSource.FromVisual(this);
                if (src != null && src.CompositionTarget != null)
                {
                    return src.CompositionTarget.TransformFromDevice.Transform(new Point(pt.X, pt.Y));
                }
                return new Point(pt.X, pt.Y);
            }
            return WindowPosToLogicalScreen(fallbackArgs.GetPosition(this));
        }

        private void TriggerSquashBounce(bool withHop)
        {
            _bounceElapsedSec = 0.0;
            _bounceWithHop = withHop;
        }

        private void StartTransient(TransientAction action, int frameCount)
        {
            _transient = action;
            _transientFramesLeft = frameCount;
            _animFrame = 0;
            _animTickAcc = 0;
            if (action == TransientAction.Jump || action == TransientAction.Wave)
            {
                TriggerSquashBounce(action == TransientAction.Jump);
            }
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

        private void ShowHeadOverlay(string text, Color bg, Color fg, double durationSec, bool driftUp)
        {
            bool isDockedCompact = !string.IsNullOrEmpty(_dockSide) && !_dockPeekOut && !_fullscreenPeekActive && !_bubbleExpanded;
            if (isDockedCompact || !IsVisible) return;
            _overlayText.Text = text;
            _overlayBorder.Background = new SolidColorBrush(bg);
            _overlayText.Foreground = new SolidColorBrush(fg);
            _overlayElapsedSec = 0.0;
            _overlayDurationSec = Math.Max(0.4, durationSec);
            _overlayDriftUp = driftUp;
            _overlayBorder.Opacity = 0.0;
            _overlayTranslate.Y = 0.0;
            _overlayBorder.Visibility = Visibility.Visible;
        }

        private void TriggerDizzyEffect(double durationSec)
        {
            bool isDockedCompact = !string.IsNullOrEmpty(_dockSide) && !_dockPeekOut && !_fullscreenPeekActive && !_bubbleExpanded;
            if (isDockedCompact || !IsVisible) return;
            _dizzyLeftSec = Math.Max(_dizzyLeftSec, durationSec);
            _dizzyCanvas.Visibility = Visibility.Visible;
        }

        private void UpdateDizzyEffect(double dt)
        {
            if (_dizzyLeftSec <= 0.0)
            {
                if (_dizzyCanvas.Visibility != Visibility.Collapsed)
                {
                    _dizzyCanvas.Visibility = Visibility.Collapsed;
                }
                return;
            }
            _dizzyLeftSec -= dt;
            if (_dizzyLeftSec <= 0.0)
            {
                _dizzyLeftSec = 0.0;
                _dizzyCanvas.Visibility = Visibility.Collapsed;
                return;
            }

            _dizzyCanvas.Opacity = (_dizzyLeftSec < 0.35) ? Math.Max(0.0, _dizzyLeftSec / 0.35) : 1.0;
            _dizzyAngle += dt * 7.2;
            const double cx = 42.0;
            const double cy = 15.0;
            const double rx = 30.0;
            const double ry = 7.5;
            for (int i = 0; i < 3; i++)
            {
                double a = _dizzyAngle + i * (Math.PI * 2.0 / 3.0);
                double sx = cx + Math.Cos(a) * rx;
                double sy = cy + Math.Sin(a) * ry;
                double depth = 0.5 + 0.5 * Math.Sin(a); // 0=后方, 1=前方
                double s = 0.72 + depth * 0.42;
                _dizzyStarPos[i].X = sx;
                _dizzyStarPos[i].Y = sy;
                _dizzyStarScale[i].ScaleX = s;
                _dizzyStarScale[i].ScaleY = s;
                _dizzyStarRot[i].Angle = (a * 180.0 / Math.PI) * 1.4;
                _dizzyStars[i].Opacity = 0.52 + depth * 0.48;
            }
        }

        private void UpdateHeadOverlay(double dt)
        {
            UpdateDizzyEffect(dt);
            if (_overlayElapsedSec < 0.0) return;
            _overlayElapsedSec += dt;
            double p = _overlayElapsedSec / _overlayDurationSec;
            if (p >= 1.0)
            {
                _overlayElapsedSec = -1.0;
                _overlayBorder.Visibility = Visibility.Collapsed;
                return;
            }
            double alpha = (p < 0.15) ? (p / 0.15) : ((p > 0.7) ? Math.Max(0.0, (1.0 - p) / 0.3) : 1.0);
            _overlayBorder.Opacity = alpha;
            _overlayTranslate.Y = _overlayDriftUp
                ? (-p * 16.0 * _config.Scale)
                : (-Math.Sin(Math.Min(1.0, p * 2.5) * Math.PI * 0.5) * 6.0 * _config.Scale);
        }

        private void InterruptBoredom(bool withWakeUpAnim)
        {
            _idleActivityAt = DateTime.UtcNow;
            _lookAroundLeftSec = 0.0;
            if (_boredomTier == BoredomTier.Sleeping)
            {
                _boredomTier = withWakeUpAnim ? BoredomTier.WakeUp : BoredomTier.Normal;
                if (withWakeUpAnim)
                {
                    _wakeUpLeftSec = 0.32;
                    TriggerSquashBounce(true);
                    ShowHeadOverlay("!", Color.FromArgb(230, 15, 23, 42), Color.FromRgb(251, 191, 36), 0.7, false);
                }
            }
            else if (_boredomTier == BoredomTier.MildBored)
            {
                _boredomTier = BoredomTier.Normal;
            }
        }

        private void OnGlobalKeyPressed()
        {
            DateTime now = DateTime.UtcNow;
            while (_keyWindow.Count > 0 && (now - _keyWindow.Peek()).TotalSeconds > 1.5)
            {
                _keyWindow.Dequeue();
            }
            _keyWindow.Enqueue(now);
            _lastKeyAt = now;
            _typingTapPulse = 1.0;
            _typingTapSign = -_typingTapSign;

            InterruptBoredom(true);

            bool isDockedCompact = !string.IsNullOrEmpty(_dockSide) && !_dockPeekOut && !_fullscreenPeekActive;
            if (_state == PetState.Idle && !HasPendingApproval() && !_isDragging && !isDockedCompact && IsVisible)
            {
                if (_keyWindow.Count >= 8)
                {
                    // 高频连击触发/续期头顶独立旋转金星眩晕特效
                    TriggerDizzyEffect(1.5);
                }
            }
        }

        private void OnMouseFollowTick(object sender, EventArgs e)
        {
            if (!_config.KeyMouseInteract || !IsVisible || _isClosing || _isDragging || _transient == TransientAction.Walk) return;
            bool isDockedCompact = !string.IsNullOrEmpty(_dockSide) && !_dockPeekOut && !_fullscreenPeekActive;
            if (isDockedCompact || _boredomTier == BoredomTier.Sleeping) return;

            try
            {
                POINT pt;
                if (!GetCursorPos(out pt)) return;
                double mx = pt.X;
                var src = PresentationSource.FromVisual(this);
                if (src != null && src.CompositionTarget != null)
                {
                    mx = src.CompositionTarget.TransformFromDevice.Transform(new Point(pt.X, pt.Y)).X;
                }

                double cx = Left + Width * 0.5;
                double deadZone = BaseSpriteBox * 0.42 * _config.Scale;
                if (Math.Abs(mx - cx) <= deadZone) return;

                bool wantFlipLeft = (mx < cx);
                if (wantFlipLeft != _mouseFlipX && (DateTime.UtcNow - _lastMouseFlipAt).TotalSeconds >= 0.5)
                {
                    _mouseFlipX = wantFlipLeft;
                    _lastMouseFlipAt = DateTime.UtcNow;
                }
            }
            catch { }
        }

        private void UpdateBoredomStateMachine(double dt, bool isDockedCompact)
        {
            UpdateHeadOverlay(dt);

            if (_lookAroundLeftSec > 0.0)
            {
                _lookAroundLeftSec = Math.Max(0.0, _lookAroundLeftSec - dt);
            }

            if (!_config.BoredomIdle || !IsVisible || _state != PetState.Idle || HasPendingApproval() || _isDragging || isDockedCompact || _fullscreenPeekActive)
            {
                if (_boredomTier != BoredomTier.Normal && _state != PetState.Idle)
                {
                    _boredomTier = BoredomTier.Normal;
                }
                return;
            }

            if (_boredomTier == BoredomTier.WakeUp)
            {
                _wakeUpLeftSec -= dt;
                if (_wakeUpLeftSec <= 0.0) _boredomTier = BoredomTier.Normal;
                return;
            }

            if (_transient != TransientAction.None) return;

            DateTime now = DateTime.UtcNow;
            if (_config.KeyMouseInteract && (now - _lastKeyAt).TotalSeconds < 2.0)
            {
                _boredomTier = BoredomTier.Normal;
                return;
            }

            double idleSec = (now - _idleActivityAt).TotalSeconds;
            if (idleSec < 120.0)
            {
                _boredomTier = BoredomTier.Normal;
            }
            else if (idleSec < 600.0)
            {
                if (_boredomTier != BoredomTier.MildBored)
                {
                    _boredomTier = BoredomTier.MildBored;
                    _nextBoredActionAt = now.AddSeconds(_rng.Next(6, 16));
                }
                else if (now >= _nextBoredActionAt)
                {
                    _nextBoredActionAt = now.AddSeconds(_rng.Next(20, 41));
                    int roll = _rng.Next(0, 4);
                    if (roll == 0)
                    {
                        TriggerSquashBounce(true);
                    }
                    else if (roll == 1)
                    {
                        _boredLookFlipX = !_flipX;
                        _lookAroundLeftSec = 1.6;
                    }
                    else if (roll == 2 && _config.AutoWalk)
                    {
                        StartRandomWalk(true);
                    }
                    else
                    {
                        string[] cues = { "...", "?", "♪" };
                        string cue = cues[_rng.Next(0, cues.Length)];
                        ShowHeadOverlay(cue, Color.FromArgb(225, 15, 23, 42), Color.FromRgb(226, 232, 240), 1.6, true);
                    }
                }
            }
            else
            {
                _boredomTier = BoredomTier.Sleeping;
                if (now >= _nextSleepZzzAt)
                {
                    _nextSleepZzzAt = now.AddSeconds(2.3);
                    ShowHeadOverlay("Zzz", Color.FromArgb(215, 15, 23, 42), Color.FromRgb(147, 197, 253), 1.8, true);
                }
            }
        }

        private void StartRandomWalk(bool shortRange = false)
        {
            if (_currentPack == null) return;
            var wa = GetCurrentMonitorWorkArea();
            double minLeft = wa.Left + 16;
            double maxLeft = wa.Right - Width - 16;
            if (maxLeft - minLeft < 120) return;

            double dist = shortRange ? _rng.Next(55, 115) : _rng.Next(110, 260);
            int dir = _rng.Next(0, 2) == 0 ? -1 : 1;
            double candidate = Left + dir * dist;
            if (candidate < minLeft || candidate > maxLeft)
            {
                dir = -dir;
                candidate = Math.Max(minLeft, Math.Min(maxLeft, Left + dir * dist));
            }
            if (Math.Abs(candidate - Left) < 40) return;

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
                    agyAlive = GetAntigravityProcesses().Count > 0;
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

            if (_config.Topmost && IsVisible && !_isClosing)
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                }
            }

            // 全屏游戏/视频避让 & 全屏关键提醒贴边出现状态机
            SyncFullscreenAndEdgeState();

            // 兜底校验：分辨率切换/拔掉副屏/DPI变更后确保宠物仍在当前有效屏幕工作区内
            if (!_isDragging && !_fullscreenPeekActive && IsVisible)
            {
                EnsureInsideScreenBounds(false);
            }

            // 吸附在 Antigravity 窗口标题栏右上角边缘
            if (_config.SnapToAgy && IsVisible && !_isDragging && _transient != TransientAction.Walk && !_bubbleExpanded && !_fullscreenPeekActive && string.IsNullOrEmpty(_dockSide))
            {
                RECT agyRect;
                if (TryGetAntigravityWindowRect(out agyRect))
                {
                    var src = PresentationSource.FromVisual(this);
                    double scaleX = 1.0, scaleY = 1.0;
                    if (src != null && src.CompositionTarget != null)
                    {
                        scaleX = src.CompositionTarget.TransformToDevice.M11;
                        scaleY = src.CompositionTarget.TransformToDevice.M22;
                    }
                    double targetLeft = Math.Round((agyRect.Right / scaleX) - Width - 18);
                    double targetTop = Math.Round((agyRect.Top / scaleY) + 46);
                    if (Math.Abs(Left - targetLeft) > 6 || Math.Abs(Top - targetTop) > 6)
                    {
                        Left = targetLeft;
                        Top = targetTop;
                        ClampToWorkArea();
                    }
                }
            }

            var now = DateTime.Now;
            var stale = _tasks.Values
                .Where(t =>
                    (t.State == PetState.Running && (now - t.UpdatedAt).TotalMinutes > 10) ||
                    (t.State == PetState.Ready && _config.ReadyTimeoutSec > 0 && (now - t.UpdatedAt).TotalSeconds >= _config.ReadyTimeoutSec))
                .Select(t => t.ConversationId)
                .ToList();
            if (stale.Count > 0)
            {
                foreach (var k in stale) _tasks.Remove(k);
                RecomputeEffectiveState();
            }

            if (!string.IsNullOrEmpty(_availableUpdateVersion) && !_isUpdatingSelf &&
                _config.ReadyTimeoutSec > 0 && (now - _updatePromptShownAt).TotalSeconds >= _config.ReadyTimeoutSec)
            {
                _availableUpdateVersion = "";
                _availableUpdateUrl = "";
                _availableUpdateZipUrl = "";
                UpdateVisualState();
                SyncFullscreenAndEdgeState();
            }

            if (_config.AutoWalk && IsVisible && _state == PetState.Idle &&
                _transient == TransientAction.None && !_isDragging && !_stageGrid.IsMouseOver && !_fullscreenPeekActive && string.IsNullOrEmpty(_dockSide))
            {
                if (now >= _nextWalkAt)
                {
                    StartRandomWalk();
                    _nextWalkAt = now.AddSeconds(_rng.Next(16, 28));
                }
            }
        }

        private void UpdateProceduralAnimation(double dt)
        {
            _animTimeSec += dt;
            _typingTapPulse = Math.Max(0.0, _typingTapPulse - dt * 5.8);

            double targetScaleX = 1.0;
            double targetScaleY = 1.0;
            double targetRot = 0.0;
            double targetY = 0.0;
            bool isTypingNow = false;

            bool isDockedCompact = !string.IsNullOrEmpty(_dockSide) && !_dockPeekOut && !_fullscreenPeekActive;
            UpdateBoredomStateMachine(dt, isDockedCompact);

            if (!(_isDragging && _movedDuringDrag) && !isDockedCompact)
            {
                DateTime now = DateTime.UtcNow;
                while (_keyWindow.Count > 0 && (now - _keyWindow.Peek()).TotalSeconds > 1.5)
                {
                    _keyWindow.Dequeue();
                }
                bool isTypingActive = _config.KeyMouseInteract &&
                                      _state == PetState.Idle &&
                                      !HasPendingApproval() &&
                                      _transient == TransientAction.None &&
                                      (now - _lastKeyAt).TotalSeconds < 2.0;

                if (_transient == TransientAction.Walk)
                {
                    double wPhase = _animTimeSec * 9.5;
                    double hop = Math.Abs(Math.Sin(wPhase));
                    targetY = -hop * (7.5 * _config.Scale);
                    targetRot = Math.Sin(wPhase) * 5.5;
                    targetScaleY = 0.96 + hop * 0.07;
                    targetScaleX = 1.0 / Math.Sqrt(targetScaleY);
                }
                else if (isTypingActive)
                {
                    isTypingNow = true;
                    int kCount = _keyWindow.Count;
                    if (kCount >= 8)
                    {
                        // 高频打字（> 5 次/秒）：大幅度欢快弹跳抖动 + 头顶眩晕金星
                        double tPhase = _animTimeSec * 22.0;
                        double jump = Math.Abs(Math.Sin(tPhase));
                        targetY = -jump * (9.5 * _config.Scale);
                        targetRot = Math.Sin(tPhase) * 9.5 + _typingTapSign * _typingTapPulse * 4.0;
                        targetScaleY = 0.88 + jump * 0.22;
                        targetScaleX = 1.0 / Math.Sqrt(targetScaleY);
                    }
                    else if (kCount >= 4)
                    {
                        // 中频打字：节奏加快，敲击压扁与跳跃感更强
                        double mPhase = _animTimeSec * 14.0;
                        double hop = Math.Abs(Math.Sin(mPhase));
                        targetY = -(hop * 6.5 + _typingTapPulse * 3.0) * _config.Scale;
                        targetRot = _typingTapSign * _typingTapPulse * 9.5 + Math.Sin(mPhase) * 4.5;
                        targetScaleY = 1.0 - _typingTapPulse * 0.18 + hop * 0.06;
                        targetScaleX = 1.0 / Math.Sqrt(Math.Max(0.72, targetScaleY));
                    }
                    else
                    {
                        // 低频打字：每次敲击干脆果冻下压 + 左右交替点头 + 微弹
                        targetY = -Math.Sin(_typingTapPulse * Math.PI) * (4.0 * _config.Scale);
                        targetRot = _typingTapSign * _typingTapPulse * 7.0;
                        targetScaleY = 1.0 - _typingTapPulse * 0.14;
                        targetScaleX = 1.0 + _typingTapPulse * 0.12;
                    }
                }
                else if (_transient == TransientAction.None)
                {
                    switch (_state)
                    {
                        case PetState.Running:
                        {
                            double rPhase = _animTimeSec * 3.4;
                            targetY = -Math.Max(0.0, Math.Sin(rPhase)) * (2.0 * _config.Scale);
                            targetRot = Math.Sin(rPhase * 0.5) * 1.8;
                            targetScaleY = 1.0 + Math.Sin(rPhase) * 0.022;
                            targetScaleX = 1.0 - Math.Sin(rPhase) * 0.015;
                            break;
                        }
                        case PetState.NeedsInput:
                        {
                            double nPhase = _animTimeSec * 4.5;
                            targetY = -Math.Abs(Math.Sin(nPhase)) * (2.5 * _config.Scale);
                            targetScaleY = 1.0 + Math.Sin(nPhase * 2.0) * 0.025;
                            break;
                        }
                        case PetState.Ready:
                        {
                            double readyPhase = _animTimeSec * 6.0;
                            targetY = -Math.Abs(Math.Sin(readyPhase)) * (5.5 * _config.Scale);
                            targetRot = Math.Sin(readyPhase) * 4.0;
                            break;
                        }
                        case PetState.Blocked:
                        {
                            targetRot = Math.Sin(_animTimeSec * 14.0) * 2.5;
                            break;
                        }
                        default:
                        {
                            if (_boredomTier == BoredomTier.Sleeping)
                            {
                                // 睡觉：微压扁身体、放慢呼吸频率
                                double sleepBreath = Math.Sin(_animTimeSec * 1.1);
                                targetScaleY = 0.90 + sleepBreath * 0.015;
                                targetScaleX = 1.07 - sleepBreath * 0.010;
                                targetRot = 3.0;
                            }
                            else
                            {
                                // 待机：脚底固定，轻微周期性呼吸缩放
                                double breath = Math.Sin(_animTimeSec * 2.3);
                                targetScaleY = 1.0 + breath * 0.022;
                                targetScaleX = 1.0 - breath * 0.012;
                            }
                            break;
                        }
                    }
                }

                // 点击/互动果冻弹性缩放（Squash & Stretch）阻尼回弹
                if (_bounceElapsedSec >= 0.0)
                {
                    _bounceElapsedSec += dt;
                    const double duration = 0.48;
                    double p = _bounceElapsedSec / duration;
                    if (p >= 1.0)
                    {
                        _bounceElapsedSec = -1.0;
                        _bounceWithHop = false;
                    }
                    else
                    {
                        double env = Math.Exp(-5.5 * p);
                        double osc = Math.Cos(p * Math.PI * 3.5);
                        double squash = -0.22 * env * osc;
                        targetScaleY += squash;
                        targetScaleX -= squash * 0.85;
                        if (_bounceWithHop && p <= 0.65)
                        {
                            targetY += -Math.Sin((p / 0.65) * Math.PI) * (13.0 * _config.Scale);
                        }
                    }
                }
            }

            if (_transient != TransientAction.Walk)
            {
                if (_isDragging || isDockedCompact || _boredomTier == BoredomTier.Sleeping)
                {
                    _flipX = false;
                }
                else if (_lookAroundLeftSec > 0.0)
                {
                    _flipX = _boredLookFlipX;
                }
                else if (_config.KeyMouseInteract)
                {
                    _flipX = _mouseFlipX;
                }
                else
                {
                    _flipX = false;
                }
            }

            double lerp = isTypingNow ? 0.58 : 0.35;
            _animScale.ScaleX += (targetScaleX - _animScale.ScaleX) * lerp;
            _animScale.ScaleY += (targetScaleY - _animScale.ScaleY) * lerp;
            _animRotate.Angle += (targetRot - _animRotate.Angle) * lerp;
            _animTranslate.Y += (targetY - _animTranslate.Y) * (isTypingNow ? 0.62 : 0.42);
            _facingScale.ScaleX = _flipX ? -1.0 : 1.0;
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

            double restAngle = 0.0;
            if (!_isDragging && !string.IsNullOrEmpty(_dockSide) && !_fullscreenPeekActive)
            {
                bool hoverAllowed = DateTime.Now >= _dockIgnoreHoverUntil;
                bool activeHover = (hoverAllowed && IsCursorInDockedZone(_dockSide)) || _bubbleExpanded || HasUrgentNotification();
                bool prevPeek = _dockPeekOut;
                if (activeHover)
                {
                    _dockPeekOut = true;
                    _dockHoverLastSeen = DateTime.Now;
                }
                else if (_dockPeekOut && (DateTime.Now - _dockHoverLastSeen).TotalMilliseconds > 260)
                {
                    _dockPeekOut = false;
                }
                if (prevPeek != _dockPeekOut)
                {
                    UpdateVisualState();
                }

                double targetLeft = GetDockedTargetLeft(_dockSide, _dockPeekOut);
                if (Math.Abs(Left - targetLeft) > 0.8)
                {
                    Left += (targetLeft - Left) * 0.25;
                }
                restAngle = _dockPeekOut ? 0.0 : (_dockSide == "right" ? -14.0 : 14.0);
                _bubbleHost.HorizontalAlignment = _dockPeekOut
                    ? ((_dockSide == "right") ? HorizontalAlignment.Left : HorizontalAlignment.Right)
                    : HorizontalAlignment.Center;
                _floorShadow.Opacity = _dockPeekOut ? 0.16 : 0.0;
            }
            else if (!_isDragging && !_fullscreenPeekActive)
            {
                if (_bubbleHost.HorizontalAlignment != HorizontalAlignment.Center)
                {
                    _bubbleHost.HorizontalAlignment = HorizontalAlignment.Center;
                }
                _floorShadow.Opacity = 0.22;
            }

            if (_isDragging && _movedDuringDrag)
            {
                double dangle = Math.Sin(Environment.TickCount / 140.0) * 3.5;
                double target = _dragTargetAngle + dangle;
                _dragCurrentAngle += (target - _dragCurrentAngle) * 0.35;
                _dragTargetAngle *= 0.78;
                _dragRotate.Angle = _dragCurrentAngle;
                _dragScale.ScaleX += (0.94 - _dragScale.ScaleX) * 0.35;
                _dragScale.ScaleY += (1.07 - _dragScale.ScaleY) * 0.35;
            }
            else if (Math.Abs(_dragCurrentAngle - restAngle) > 0.1 || Math.Abs(_dragScale.ScaleX - 1.0) > 0.005)
            {
                _dragCurrentAngle += (restAngle - _dragCurrentAngle) * 0.32;
                if (Math.Abs(_dragCurrentAngle - restAngle) < 0.1) _dragCurrentAngle = restAngle;
                _dragRotate.Angle = _dragCurrentAngle;
                _dragScale.ScaleX += (1.0 - _dragScale.ScaleX) * 0.35;
                _dragScale.ScaleY += (1.0 - _dragScale.ScaleY) * 0.35;
            }

            UpdateProceduralAnimation(0.04);

            double frameInterval = (_isDragging && _movedDuringDrag) ? 0.09
                : ((_state == PetState.Idle && _transient == TransientAction.None) ? 0.22 : 0.14);
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

            bool showHalo = _state != PetState.Idle || HasPendingApproval() || !string.IsNullOrEmpty(_availableUpdateVersion);
            if (showHalo)
            {
                double t = Environment.TickCount / 260.0;
                _statusHalo.Opacity = 0.55 + Math.Sin(t) * 0.20;
            }
            else
            {
                _statusHalo.Opacity = 0;
            }
        }

        private void RenderCurrentFrame()
        {
            if (_currentPack == null || !EnsurePackLoaded(_currentPack)) return;

            if (_isDragging && _movedDuringDrag) { SetRowFrame(4); return; }
            if (_transient == TransientAction.Wave) { SetRowFrame(3); return; }
            if (_transient == TransientAction.Jump) { SetRowFrame(4); return; }
            if (_transient == TransientAction.Walk)
            {
                if (_walkDir < 0)
                {
                    if (_currentPack.Rows[2].Count > 0)
                    {
                        _flipX = false;
                        SetRowFrame(2);
                    }
                    else
                    {
                        _flipX = true;
                        SetRowFrame(_currentPack.Rows[1].Count > 0 ? 1 : 0);
                    }
                }
                else
                {
                    _flipX = false;
                    SetRowFrame(1);
                }
                _facingScale.ScaleX = _flipX ? -1.0 : 1.0;
                return;
            }

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
                        _spriteImage.Source = ComposeFrameWithAccessory(lookFrame, _currentPack.Id, GetEffectiveAccessoryId(), 9, 0);
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
            int actualRow = (rowIdx >= 0 && rowIdx < _currentPack.Rows.Length && _currentPack.Rows[rowIdx].Count > 0)
                ? rowIdx
                : 0;
            var list = _currentPack.Rows[actualRow];
            if (list.Count == 0) return;
            int frameIdx = _animFrame % list.Count;
            _spriteImage.Source = ComposeFrameWithAccessory(list[frameIdx], _currentPack.Id, GetEffectiveAccessoryId(), actualRow, frameIdx);
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
                InterruptBoredom(true);
            }

            RecomputeEffectiveState();

            if (_config.SoundAlert && !isPreview && newState != prevState &&
                (newState == PetState.Ready || newState == PetState.NeedsInput || newState == PetState.Blocked))
            {
                PlayPetSound(StateToApiString(newState));
            }
            UpdateAccessoryVisual();
            SyncFullscreenAndEdgeState();
        }

        private void PlayPetSound(string stateKey)
        {
            try
            {
                string wavPath;
                if (_currentPack != null && _currentPack.Sounds != null &&
                    _currentPack.Sounds.TryGetValue(stateKey, out wavPath) && File.Exists(wavPath))
                {
                    using (var sp = new SoundPlayer(wavPath))
                    {
                        sp.Play();
                    }
                    return;
                }
                if (stateKey == "ready" || stateKey == "needs_input") SystemSounds.Asterisk.Play();
            }
            catch { }
        }

        private static Color ParseHexColor(string hex, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(hex)) return fallback;
            try
            {
                object parsed = ColorConverter.ConvertFromString(hex.Trim());
                if (parsed is Color) return (Color)parsed;
            }
            catch { }
            return fallback;
        }

        private string GetEffectiveAccessoryId()
        {
            string pref = (_config.Accessory ?? "auto").Trim().ToLowerInvariant();
            if (pref == "none") return "none";
            int done = _config.CompletedTasksCount;
            if (pref == "auto")
            {
                if (done >= 300) return "crown";
                if (done >= 150) return "coffee";
                if (done >= 50) return "cap";
                if (done >= 10) return "glasses";
                return "none";
            }
            return pref;
        }

        private void UpdateAccessoryVisual()
        {
            _composedFrameCache.Clear();
            RenderCurrentFrame();
        }

        private static void GetFrameMotionOffset(int row, int frame, out int bodyX, out int bodyY, out bool flipX)
        {
            bodyX = 0;
            bodyY = 0;
            flipX = false;
            switch (row)
            {
                case 0:
                    if (frame == 2 || frame == 3) bodyY = 1;
                    break;
                case 1:
                    if ((frame % 4) == 1 || (frame % 4) == 3) bodyY = -1;
                    break;
                case 2:
                    flipX = true;
                    if ((frame % 4) == 1 || (frame % 4) == 3) bodyY = -1;
                    break;
                case 3:
                    if ((frame % 2) == 1) bodyY = -1;
                    break;
                case 4:
                    int[] jOff = new int[] { 1, -2, -5, -2, 0 };
                    bodyY = jOff[frame % jOff.Length];
                    break;
                case 5:
                    bodyX = ((frame % 2) == 0) ? -1 : 1;
                    break;
                case 6:
                    if ((frame % 2) == 1) bodyY = -1;
                    break;
                case 7:
                    int[] wOff = new int[] { 0, 1, 0, -1, 0, 1 };
                    bodyY = wOff[frame % wOff.Length];
                    break;
                case 8:
                    int[] rOff = new int[] { 0, -2, -4, -1, 0, -2 };
                    bodyY = rOff[frame % rOff.Length];
                    break;
                default:
                    break;
            }
        }

        private static int GetSkinAccessoryHeadOffset(string skinId)
        {
            if (skinId == "flutter-dash") return -1;
            if (skinId == "android-bugdroid") return 1;
            return 0;
        }

        private BitmapSource ComposeFrameWithAccessory(BitmapSource baseFrame, string skinId, string acc, int row, int frame)
        {
            if (baseFrame == null || string.IsNullOrEmpty(acc) || acc == "none") return baseFrame;
            string key = skinId + "|" + acc + "|" + row + "|" + frame;
            BitmapSource cached;
            if (_composedFrameCache.TryGetValue(key, out cached)) return cached;

            try
            {
                BitmapSource src = baseFrame;
                if (src.Format != PixelFormats.Bgra32)
                {
                    var converted = new FormatConvertedBitmap();
                    converted.BeginInit();
                    converted.Source = src;
                    converted.DestinationFormat = PixelFormats.Bgra32;
                    converted.EndInit();
                    src = converted;
                }

                int pw = src.PixelWidth;
                int ph = src.PixelHeight;
                int stride = pw * 4;
                byte[] pixels = new byte[stride * ph];
                src.CopyPixels(pixels, stride, 0);

                int bodyX, bodyY;
                bool flipX;
                GetFrameMotionOffset(row, frame, out bodyX, out bodyY, out flipX);
                int cx = 18 + bodyX;
                int cy = 22 + bodyY;
                int ey = cy - 7;
                int headY = cy - 11 + GetSkinAccessoryHeadOffset(skinId);

                const int gw = 36;
                const int gh = 39;
                byte[] ovR = new byte[gw * gh];
                byte[] ovG = new byte[gw * gh];
                byte[] ovB = new byte[gw * gh];
                byte[] ovA = new byte[gw * gh];

                Action<int, int, byte, byte, byte, byte> px = (x, y, r, g, b, a) =>
                {
                    if (x < 0 || x >= gw || y < 0 || y >= gh) return;
                    int idx = y * gw + x;
                    ovR[idx] = r;
                    ovG[idx] = g;
                    ovB[idx] = b;
                    ovA[idx] = a;
                };

                if (acc == "glasses")
                {
                    int gy = ey + 1;
                    for (int x = cx - 7; x <= cx + 6; x++) px(x, gy, 15, 23, 42, 245);
                    for (int x = cx - 6; x <= cx - 3; x++) for (int y = gy; y <= gy + 2; y++) px(x, y, 15, 23, 42, 230);
                    for (int x = cx + 2; x <= cx + 5; x++) for (int y = gy; y <= gy + 2; y++) px(x, y, 15, 23, 42, 230);
                    px(cx - 5, gy, 56, 189, 248, 255);
                    px(cx + 3, gy, 56, 189, 248, 255);
                }
                else if (acc == "cap")
                {
                    for (int x = cx - 7; x <= cx + 6; x++) for (int y = headY - 3; y <= headY; y++) px(x, y, 14, 165, 233, 255);
                    for (int x = cx - 5; x <= cx + 4; x++) px(x, headY - 4, 56, 189, 248, 255);
                    for (int x = cx - 3; x <= cx + 8; x++) px(x, headY, 2, 132, 199, 255);
                }
                else if (acc == "crown")
                {
                    for (int x = cx - 5; x <= cx + 4; x++) { px(x, headY - 1, 250, 204, 21, 255); px(x, headY, 245, 158, 11, 255); }
                    px(cx - 5, headY - 3, 250, 204, 21, 255); px(cx - 5, headY - 2, 250, 204, 21, 255);
                    px(cx - 1, headY - 4, 250, 204, 21, 255); px(cx, headY - 4, 250, 204, 21, 255);
                    px(cx - 1, headY - 3, 250, 204, 21, 255); px(cx, headY - 3, 250, 204, 21, 255);
                    px(cx - 1, headY - 2, 56, 189, 248, 255); px(cx, headY - 2, 56, 189, 248, 255);
                    px(cx + 4, headY - 3, 250, 204, 21, 255); px(cx + 4, headY - 2, 250, 204, 21, 255);
                }
                else if (acc == "coffee")
                {
                    int cupX = cx + 8;
                    int cupY = cy + 2;
                    for (int x = cupX; x <= cupX + 4; x++) for (int y = cupY; y <= cupY + 4; y++) px(x, y, 241, 245, 249, 255);
                    for (int x = cupX + 1; x <= cupX + 3; x++) px(x, cupY, 120, 53, 15, 255);
                    px(cupX + 5, cupY + 1, 241, 245, 249, 255);
                    px(cupX + 5, cupY + 3, 241, 245, 249, 255);
                    px(cupX + 6, cupY + 2, 241, 245, 249, 255);
                    int steamOff = frame % 2;
                    px(cupX + 1, cupY - 2 - steamOff, 203, 213, 225, 210);
                    px(cupX + 3, cupY - 3 + steamOff, 203, 213, 225, 210);
                }

                // 为配饰添加 1px 深色像素外轮廓（与角色本体融合）
                byte[] outA = (byte[])ovA.Clone();
                int[] odx = new int[] { 1, -1, 0, 0 };
                int[] ody = new int[] { 0, 0, 1, -1 };
                for (int y = 0; y < gh; y++)
                {
                    for (int x = 0; x < gw; x++)
                    {
                        int i = y * gw + x;
                        if (ovA[i] > 0) continue;
                        for (int k = 0; k < 4; k++)
                        {
                            int nx = x + odx[k];
                            int ny = y + ody[k];
                            if (nx >= 0 && nx < gw && ny >= 0 && ny < gh && ovA[ny * gw + nx] >= 220)
                            {
                                ovR[i] = 15; ovG[i] = 23; ovB[i] = 42; outA[i] = 255;
                                break;
                            }
                        }
                    }
                }

                double sx = pw / 192.0;
                double sy = ph / 208.0;
                for (int y = 0; y < gh; y++)
                {
                    for (int x = 0; x < gw; x++)
                    {
                        int i = y * gw + x;
                        byte a = outA[i];
                        if (a == 0) continue;
                        int drawX = flipX ? (gw - 1 - x) : x;
                        int px0 = (int)Math.Floor((6.0 + drawX * 5.0) * sx);
                        int py0 = (int)Math.Floor((6.0 + y * 5.0) * sy);
                        int px1 = (int)Math.Ceiling((6.0 + (drawX + 1) * 5.0) * sx);
                        int py1 = (int)Math.Ceiling((6.0 + (y + 1) * 5.0) * sy);
                        for (int pyy = Math.Max(0, py0); pyy < Math.Min(ph, py1); pyy++)
                        {
                            for (int pxx = Math.Max(0, px0); pxx < Math.Min(pw, px1); pxx++)
                            {
                                int pIdx = pyy * stride + pxx * 4;
                                if (a >= 250)
                                {
                                    pixels[pIdx + 0] = ovB[i];
                                    pixels[pIdx + 1] = ovG[i];
                                    pixels[pIdx + 2] = ovR[i];
                                    pixels[pIdx + 3] = a;
                                }
                                else
                                {
                                    double fa = a / 255.0;
                                    double ia = 1.0 - fa;
                                    pixels[pIdx + 0] = (byte)(ovB[i] * fa + pixels[pIdx + 0] * ia);
                                    pixels[pIdx + 1] = (byte)(ovG[i] * fa + pixels[pIdx + 1] * ia);
                                    pixels[pIdx + 2] = (byte)(ovR[i] * fa + pixels[pIdx + 2] * ia);
                                    pixels[pIdx + 3] = (byte)Math.Min(255, (int)(a + pixels[pIdx + 3] * ia));
                                }
                            }
                        }
                    }
                }

                var wb = new WriteableBitmap(pw, ph, 96, 96, PixelFormats.Bgra32, null);
                wb.WritePixels(new Int32Rect(0, 0, pw, ph), pixels, stride, 0);
                wb.Freeze();
                _composedFrameCache[key] = wb;
                return wb;
            }
            catch
            {
                return baseFrame;
            }
        }

        private Rect GetCurrentMonitorWorkArea()
        {
            try
            {
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    IntPtr hMon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
                    if (hMon != IntPtr.Zero)
                    {
                        MONITORINFO mi = new MONITORINFO();
                        mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
                        if (GetMonitorInfo(hMon, ref mi))
                        {
                            double sx = 1.0, sy = 1.0;
                            var src = PresentationSource.FromVisual(this);
                            if (src != null && src.CompositionTarget != null)
                            {
                                sx = src.CompositionTarget.TransformFromDevice.M11;
                                sy = src.CompositionTarget.TransformFromDevice.M22;
                            }
                            double l = mi.rcWork.Left * sx;
                            double t = mi.rcWork.Top * sy;
                            double w = Math.Max(100.0, (mi.rcWork.Right - mi.rcWork.Left) * sx);
                            double h = Math.Max(100.0, (mi.rcWork.Bottom - mi.rcWork.Top) * sy);
                            return new Rect(l, t, w, h);
                        }
                    }
                }
            }
            catch { }
            return SystemParameters.WorkArea;
        }

        private bool IsCursorInDockedZone(string side)
        {
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
                if (_suppressDockedHoverUntilCursorMoves)
                {
                    if (_dockHoverAnchorNeedsCapture)
                    {
                        _dockTouchReleaseCursorPos = new Point(mx, my);
                        _dockHoverAnchorNeedsCapture = false;
                        return false;
                    }
                    if (Math.Abs(mx - _dockTouchReleaseCursorPos.X) < 16.0 &&
                        Math.Abs(my - _dockTouchReleaseCursorPos.Y) < 16.0)
                    {
                        return false;
                    }
                    _suppressDockedHoverUntilCursorMoves = false;
                }
                var wa = GetCurrentMonitorWorkArea();
                double minX, maxX;
                if (!_dockPeekOut)
                {
                    // 窄触发区：仅覆盖屏幕边缘露出的宠物半身与色块
                    double narrowW = Math.Round(68.0 * _config.Scale);
                    if (side == "right")
                    {
                        minX = wa.Right - narrowW;
                        maxX = wa.Right + 6.0;
                    }
                    else
                    {
                        minX = wa.Left - 6.0;
                        maxX = wa.Left + narrowW;
                    }
                }
                else
                {
                    // 宽保持区：覆盖整个探出后的窗口与边缘联合区域
                    if (side == "right")
                    {
                        minX = Math.Min(Left, GetDockedTargetLeft("right", true)) - 8.0;
                        maxX = wa.Right + 8.0;
                    }
                    else
                    {
                        minX = wa.Left - 8.0;
                        maxX = Math.Max(Left + Width, GetDockedTargetLeft("left", true) + Width) + 8.0;
                    }
                }
                double minY = Top - 8.0;
                double maxY = Top + Height + 8.0;
                bool inZone = mx >= minX && mx <= maxX && my >= minY && my <= maxY;
                if (_requireHoverExitBeforePeek)
                {
                    if (!inZone) _requireHoverExitBeforePeek = false;
                    return false;
                }
                return inZone;
            }
            catch
            {
                return false;
            }
        }

        private double GetDockedTargetLeft(string side, bool expandedForHoverOrAlert)
        {
            var wa = GetCurrentMonitorWorkArea();
            // 贴边收起时略微外移 _stageGrid.Width * 0.03，使宠物身体中心贴合屏幕边缘、只露出半个身子
            double dockInset = _stageGrid.Width * 0.03;
            if (side == "left")
            {
                double halfPeek = wa.Left - Width / 2.0 - dockInset;
                if (expandedForHoverOrAlert)
                {
                    double fullPeek = wa.Left - 14.0;
                    return Math.Max(halfPeek, fullPeek);
                }
                return halfPeek;
            }
            else
            {
                double halfPeek = wa.Right - Width / 2.0 + dockInset;
                if (expandedForHoverOrAlert)
                {
                    double fullPeek = wa.Right - Width + 14.0;
                    return Math.Min(halfPeek, fullPeek);
                }
                return halfPeek;
            }
        }

        private void SnapToScreenEdgeIfNear()
        {
            var wa = GetCurrentMonitorWorkArea();
            double centerX = Left + Width / 2.0;
            double bodyHalfW = _stageGrid.Width * 0.28;
            bool hasCursor = false;
            double cursorX = centerX;
            if (_lastDragWasTouch)
            {
                hasCursor = true;
                cursorX = _lastDragScreenX;
            }
            else
            {
                try
                {
                    POINT pt;
                    if (GetCursorPos(out pt))
                    {
                        hasCursor = true;
                        cursorX = pt.X;
                        var src = PresentationSource.FromVisual(this);
                        if (src != null && src.CompositionTarget != null)
                        {
                            cursorX = src.CompositionTarget.TransformFromDevice.Transform(new Point(pt.X, pt.Y)).X;
                        }
                    }
                }
                catch { }
            }

            _dockPeekOut = false;
            if ((centerX - bodyHalfW <= wa.Left + 85.0) || (hasCursor && cursorX <= wa.Left + 60.0))
            {
                _dockSide = "left";
                _dockIgnoreHoverUntil = DateTime.Now.AddMilliseconds(700);
                _bubbleHost.HorizontalAlignment = HorizontalAlignment.Center;
            }
            else if ((centerX + bodyHalfW >= wa.Right - 85.0) || (hasCursor && cursorX >= wa.Right - 60.0))
            {
                _dockSide = "right";
                _dockIgnoreHoverUntil = DateTime.Now.AddMilliseconds(700);
                _bubbleHost.HorizontalAlignment = HorizontalAlignment.Center;
            }
            else
            {
                _dockSide = "";
                _bubbleHost.HorizontalAlignment = HorizontalAlignment.Center;
            }
            _requireHoverExitBeforePeek = !string.IsNullOrEmpty(_dockSide);
            if (_lastDragWasTouch && !string.IsNullOrEmpty(_dockSide))
            {
                _suppressDockedHoverUntilCursorMoves = true;
                _dockHoverAnchorNeedsCapture = true;
            }
            else
            {
                _suppressDockedHoverUntilCursorMoves = false;
                _dockHoverAnchorNeedsCapture = false;
            }
            UpdateVisualState();
            if (!string.IsNullOrEmpty(_dockSide))
            {
                Left = GetDockedTargetLeft(_dockSide, false);
            }
        }

        private bool HasPendingApproval()
        {
            lock (_approvalLock) { return _pendingApprovals.Count > 0; }
        }

        private bool HasUrgentNotification()
        {
            return _state == PetState.Ready || _state == PetState.NeedsInput || _state == PetState.Blocked || HasPendingApproval() || !string.IsNullOrEmpty(_availableUpdateVersion);
        }

        private bool IsOtherAppFullscreen()
        {
            try
            {
                int quns = 0;
                if (SHQueryUserNotificationState(out quns) == 0 && (quns == 2 || quns == 3 || quns == 4))
                {
                    return true;
                }
            }
            catch { }

            try
            {
                IntPtr fg = GetForegroundWindow();
                if (fg == IntPtr.Zero) return false;
                IntPtr selfHwnd = new WindowInteropHelper(this).Handle;
                if (fg == selfHwnd) return false;

                var cls = new StringBuilder(64);
                GetClassName(fg, cls, cls.Capacity);
                string clsName = cls.ToString();
                if (clsName == "Progman" || clsName == "WorkerW" || clsName == "Shell_TrayWnd" || clsName == "Shell_SecondaryTrayWnd")
                {
                    return false;
                }

                uint pid = 0;
                GetWindowThreadProcessId(fg, out pid);
                if (pid == (uint)Process.GetCurrentProcess().Id) return false;
                try
                {
                    using (var proc = Process.GetProcessById((int)pid))
                    {
                        if (string.Equals(proc.ProcessName, "Antigravity", StringComparison.OrdinalIgnoreCase))
                        {
                            return false;
                        }
                    }
                }
                catch { }

                RECT winRect;
                if (!GetWindowRect(fg, out winRect)) return false;
                IntPtr hMon = MonitorFromWindow(fg, MONITOR_DEFAULTTONEAREST);
                if (hMon == IntPtr.Zero) return false;
                MONITORINFO mi = new MONITORINFO();
                mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
                if (!GetMonitorInfo(hMon, ref mi)) return false;

                return winRect.Left <= mi.rcMonitor.Left + 2 &&
                       winRect.Top <= mi.rcMonitor.Top + 2 &&
                       winRect.Right >= mi.rcMonitor.Right - 2 &&
                       winRect.Bottom >= mi.rcMonitor.Bottom - 2;
            }
            catch
            {
                return false;
            }
        }

        private void SyncFullscreenAndEdgeState()
        {
            if (_isClosing || !_config.Enabled) return;

            if (!_config.HideOnFullscreen)
            {
                if (_fullscreenPeekActive)
                {
                    if (_savedNormalLeft >= 0 && _savedNormalTop >= 0)
                    {
                        Left = _savedNormalLeft;
                        Top = _savedNormalTop;
                        ClampToWorkArea();
                    }
                    _fullscreenPeekActive = false;
                }
                if (_hiddenByFullscreen)
                {
                    _hiddenByFullscreen = false;
                    if (!IsVisible) Show();
                }
                return;
            }

            bool isFs = IsOtherAppFullscreen();
            bool urgent = HasUrgentNotification();

            if (isFs)
            {
                if (urgent)
                {
                    if (!_fullscreenPeekActive)
                    {
                        _savedNormalLeft = Left;
                        _savedNormalTop = Top;
                        _fullscreenPeekActive = true;
                        _hiddenByFullscreen = false;
                    }
                    var wa = GetCurrentMonitorWorkArea();
                    string peekSide = (_dockSide == "left") ? "left" : "right";
                    double peekLeft = GetDockedTargetLeft(peekSide, true);
                    double peekTop = Math.Max(wa.Top + 40, Math.Min(wa.Bottom - Height - 24, Top));
                    _bubbleHost.HorizontalAlignment = (peekSide == "left") ? HorizontalAlignment.Right : HorizontalAlignment.Left;
                    if (Math.Abs(Left - peekLeft) > 1 || Math.Abs(Top - peekTop) > 1)
                    {
                        Left = peekLeft;
                        Top = peekTop;
                    }
                    if (!IsVisible) Show();
                    var hwnd = new WindowInteropHelper(this).Handle;
                    if (hwnd != IntPtr.Zero)
                    {
                        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                    }
                }
                else
                {
                    _fullscreenPeekActive = false;
                    _hiddenByFullscreen = true;
                    if (IsVisible) Hide();
                }
            }
            else
            {
                if (_fullscreenPeekActive)
                {
                    if (_savedNormalLeft >= 0 && _savedNormalTop >= 0)
                    {
                        Left = _savedNormalLeft;
                        Top = _savedNormalTop;
                        ClampToWorkArea();
                    }
                    _fullscreenPeekActive = false;
                    _bubbleHost.HorizontalAlignment = string.IsNullOrEmpty(_dockSide)
                        ? HorizontalAlignment.Center
                        : (_dockSide == "left" ? HorizontalAlignment.Right : HorizontalAlignment.Left);
                }
                if (_hiddenByFullscreen)
                {
                    _hiddenByFullscreen = false;
                    if (!IsVisible) Show();
                    if (_config.Topmost) ApplyTopmost(true);
                }
            }
        }

        private bool TryGetAntigravityWindowRect(out RECT foundRect)
        {
            RECT res = new RECT();
            bool found = false;
            try
            {
                var pids = new HashSet<int>(GetAntigravityProcesses().Select(p => p.Id));
                if (pids.Count == 0) { foundRect = res; return false; }
                EnumWindows((hWnd, lParam) =>
                {
                    if (!IsWindowVisible(hWnd) || IsIconic(hWnd)) return true;
                    uint pid;
                    GetWindowThreadProcessId(hWnd, out pid);
                    if (pids.Contains((int)pid))
                    {
                        RECT r;
                        if (GetWindowRect(hWnd, out r) && (r.Right - r.Left) > 300 && (r.Bottom - r.Top) > 200)
                        {
                            res = r;
                            found = true;
                            return false;
                        }
                    }
                    return true;
                }, IntPtr.Zero);
            }
            catch { }
            foundRect = res;
            return found;
        }

        private void ResolveCurrentApproval(string decision)
        {
            PendingApproval cur = null;
            lock (_approvalLock)
            {
                if (_pendingApprovals.Count > 0)
                {
                    cur = _pendingApprovals[0];
                    _pendingApprovals.RemoveAt(0);
                    cur.Decision = decision;
                    cur.WaitHandle.Set();
                }
            }
            UpdateVisualState();
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
                TaskItem pinnedTask = null;
                if (!string.IsNullOrWhiteSpace(_config.PinnedConversationId))
                {
                    _tasks.TryGetValue(_config.PinnedConversationId, out pinnedTask);
                }

                var top = pinnedTask ?? _tasks.Values
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
                if (_state == PetState.Ready && _config.ReadyTimeoutSec > 0)
                {
                    _autoIdleTimer.Interval = TimeSpan.FromSeconds(_config.ReadyTimeoutSec);
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

        private bool IsDetailedStyle()
        {
            return string.Equals(_config.BubbleStyle, "detailed", StringComparison.OrdinalIgnoreCase);
        }

        private void SetWindowSizeKeepingAnchor(double targetWidth, double targetHeight)
        {
            double baseW = Math.Round(BaseWidth * _config.Scale);
            if (targetWidth < baseW) targetWidth = baseW;
            if (targetHeight < _baseWindowHeight) targetHeight = _baseWindowHeight;
            double winW = Math.Round(targetWidth);
            double winH = Math.Round(targetHeight);
            if (Math.Abs(Width - winW) < 0.5 && Math.Abs(Height - winH) < 0.5) return;

            double centerX = Left + Width * 0.5;
            double bottom = Top + Height;
            Width = winW;
            Height = winH;

            double vsLeft = SystemParameters.VirtualScreenLeft;
            double vsTop = SystemParameters.VirtualScreenTop;
            double vsRight = vsLeft + SystemParameters.VirtualScreenWidth;
            if (!string.IsNullOrEmpty(_dockSide) || _fullscreenPeekActive)
            {
                string activeSide = !string.IsNullOrEmpty(_dockSide) ? _dockSide : "right";
                Left = GetDockedTargetLeft(activeSide, _dockPeekOut || _fullscreenPeekActive);
            }
            else
            {
                Left = Math.Max(vsLeft, Math.Min(vsRight - Width, centerX - Width * 0.5));
            }
            Top = Math.Max(vsTop, bottom - Height);
        }

        private double MeasureBubbleTextWidth(string text, double fontSize)
        {
            if (string.IsNullOrEmpty(text)) return 80;
            var ft = new FormattedText(
                text,
                System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface(FontFamily, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
                fontSize,
                Brushes.White);
            return ft.WidthIncludingTrailingWhitespace;
        }

        private Color GetStateAccentColor(PetState st)
        {
            switch (st)
            {
                case PetState.Running: return Color.FromRgb(56, 189, 248);
                case PetState.NeedsInput: return Color.FromRgb(245, 158, 11);
                case PetState.Ready: return Color.FromRgb(16, 185, 129);
                case PetState.Blocked: return Color.FromRgb(239, 68, 68);
                default: return Color.FromRgb(148, 163, 184);
            }
        }

        private string ShortDetailedState(PetState st, string rawMsg)
        {
            string m = (rawMsg ?? "").Trim();
            if (m == "思考规划中..." || m == "思考执行中...") return T("思考中", "Thinking");
            if (m == "编辑代码中..." || m == "修改代码中..." || m == "快速分析代码中...") return T("改代码", "Editing");
            if (m == "读取文件中...") return T("读文件", "Reading");
            if (m == "编写文件中...") return T("写文件", "Writing");
            if (m == "检索代码中...") return T("搜代码", "Searching");
            if (m == "执行命令中...") return T("跑命令", "Command");
            if (m == "运行检查中...") return T("检查中", "Checks");
            if (m == "处理结果中...") return T("收尾中", "Finishing");
            if (m == "查看屏幕中..." || m == "操作桌面应用中..." || m == "操控桌面中...") return T("看屏幕", "Screen");
            if (m == "搜索资料中...") return T("查资料", "Search");
            if (m == "调度子代理中...") return T("子代理", "Subagent");
            if (m == "等待你回答...") return T("待确认", "Input");
            if (m == "任务完成!") return T("已完成", "Ready");
            if (m.Contains("出错")) return T("报错", "Blocked");

            switch (st)
            {
                case PetState.NeedsInput: return T("待确认", "Input");
                case PetState.Blocked: return T("报错", "Blocked");
                case PetState.Ready: return T("已完成", "Ready");
                case PetState.Running: return T("运行中", "Running");
                default: return T("空闲", "Idle");
            }
        }

        private string FormatDetailedTaskLabel(TaskItem t)
        {
            if (t == null) return "";
            string taskName = !string.IsNullOrWhiteSpace(t.Title)
                ? Shorten(t.Title, 14)
                : (t.ConversationId == "default" || string.IsNullOrWhiteSpace(t.ConversationId)
                    ? "Antigravity"
                    : Shorten(t.ConversationId, 8));

            return taskName + " - " + ShortDetailedState(t.State, t.Message);
        }

        private List<TaskItem> GetOrderedActiveTasks()
        {
            return _tasks.Values
                .OrderByDescending(t => string.Equals(t.ConversationId, _config.PinnedConversationId ?? "", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                .ThenByDescending(t => (int)t.State)
                .ThenByDescending(t => t.UpdatedAt)
                .ToList();
        }

        private UIElement BuildExpandedTaskRow(TaskItem item, double maxTextWidth)
        {
            Color accent = GetStateAccentColor(item.State);
            bool isPinned = !string.IsNullOrWhiteSpace(_config.PinnedConversationId) &&
                string.Equals(item.ConversationId, _config.PinnedConversationId, StringComparison.OrdinalIgnoreCase);

            var rowPanel = new DockPanel { LastChildFill = true };
            var dot = new Ellipse
            {
                Width = 7,
                Height = 7,
                Fill = new SolidColorBrush(accent),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            DockPanel.SetDock(dot, Dock.Left);
            rowPanel.Children.Add(dot);

            var pinBtn = new TextBlock
            {
                Text = isPinned ? "📌" : "📍",
                FontSize = 10.5,
                Opacity = isPinned ? 1.0 : 0.45,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0),
                Cursor = Cursors.Hand,
                ToolTip = isPinned ? T("取消固定", "Unpin chat") : T("固定跟踪此对话", "Pin this chat")
            };
            DockPanel.SetDock(pinBtn, Dock.Right);
            rowPanel.Children.Add(pinBtn);

            var txt = new TextBlock
            {
                Text = FormatDetailedTaskLabel(item),
                Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                FontSize = 11.0,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = maxTextWidth
            };
            rowPanel.Children.Add(txt);

            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(245, 15, 23, 42)),
                BorderBrush = new SolidColorBrush(isPinned ? Color.FromRgb(250, 204, 21) : accent),
                BorderThickness = new Thickness(isPinned ? 2.0 : 1.8),
                CornerRadius = new CornerRadius(2),
                SnapsToDevicePixels = true,
                Padding = new Thickness(9, 4, 9, 4),
                Margin = new Thickness(0, 0, 0, 4),
                Cursor = Cursors.Hand,
                Child = rowPanel,
                Effect = new DropShadowEffect { BlurRadius = 0, ShadowDepth = 2.5, Direction = 315, Color = Colors.Black, Opacity = 0.8 }
            };

            pinBtn.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                _config.PinnedConversationId = isPinned ? "" : item.ConversationId;
                RecomputeEffectiveState();
                SaveConfig();
            };

            card.MouseEnter += (s, e) => { card.Background = new SolidColorBrush(Color.FromArgb(250, 30, 41, 59)); };
            card.MouseLeave += (s, e) => { card.Background = new SolidColorBrush(Color.FromArgb(242, 15, 23, 42)); };
            card.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                StartTransient(TransientAction.Jump, 5);
                if (item.State == PetState.Ready || item.State == PetState.NeedsInput || item.State == PetState.Blocked)
                {
                    _tasks.Remove(item.ConversationId);
                    RecomputeEffectiveState();
                }
                OpenAntigravityConversation(item.ConversationId, item.Title);
            };
            return card;
        }

        private void UpdateBubbleDisplay(Color badgeBg, bool isAlert, string defaultMsg)
        {
            double baseW = Math.Round(BaseWidth * _config.Scale);
            // 统一还原正常气泡基础样式
            _bubbleBorder.Width = double.NaN;
            _bubbleBorder.Height = double.NaN;
            _bubbleBorder.Padding = new Thickness(12, 6, 12, 6);
            _bubbleBorder.Margin = new Thickness(0);
            _bubbleBorder.Background = new SolidColorBrush(Color.FromArgb(242, 15, 23, 42));
            _bubbleText.Visibility = Visibility.Visible;

            PendingApproval topApproval = null;
            lock (_approvalLock)
            {
                if (_pendingApprovals.Count > 0) topApproval = _pendingApprovals[0];
            }
            if (topApproval != null)
            {
                _stackBack1.Visibility = Visibility.Collapsed;
                _stackBack2.Visibility = Visibility.Collapsed;
                _expandedStackPanel.Visibility = Visibility.Collapsed;
                _bubbleExpanded = false;
                _bubbleText.MaxWidth = Math.Round(300 * _config.Scale);
                _bubbleText.Text = T("审批: ", "Approve: ") + Shorten(topApproval.Summary, 24);
                _bubbleBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                _approvalBar.Visibility = Visibility.Visible;
                _bubbleBorder.Visibility = Visibility.Visible;
                double wNeed = Math.Max(baseW, Math.Min(Math.Round(360 * _config.Scale), MeasureBubbleTextWidth(_bubbleText.Text, 11.5) + 42));
                SetWindowSizeKeepingAnchor(wNeed, _baseWindowHeight + 28);
                return;
            }
            if (_approvalBar != null) _approvalBar.Visibility = Visibility.Collapsed;

            if (_isUpdatingSelf || !string.IsNullOrEmpty(_availableUpdateVersion))
            {
                _stackBack1.Visibility = Visibility.Collapsed;
                _stackBack2.Visibility = Visibility.Collapsed;
                _expandedStackPanel.Visibility = Visibility.Collapsed;
                _bubbleExpanded = false;
                _bubbleText.MaxWidth = Math.Round(300 * _config.Scale);
                _bubbleText.Text = _isUpdatingSelf && !string.IsNullOrEmpty(_selfUpdateStatusMsg)
                    ? _selfUpdateStatusMsg
                    : T("发现新版本 v" + _availableUpdateVersion + " · 点击一键更新", "New release v" + _availableUpdateVersion + " · Click to update");
                _bubbleBorder.Background = new SolidColorBrush(Color.FromArgb(245, 46, 16, 101));
                _bubbleBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(168, 85, 247));
                _bubbleBorder.Visibility = Visibility.Visible;
                double wNeed = Math.Max(baseW, Math.Min(Math.Round(360 * _config.Scale), MeasureBubbleTextWidth(_bubbleText.Text, 11.5) + 42));
                SetWindowSizeKeepingAnchor(wNeed, _baseWindowHeight);
                return;
            }

            if (_config.BubbleMode == "off")
            {
                _bubbleBorder.Visibility = Visibility.Hidden;
                _stackBack1.Visibility = Visibility.Collapsed;
                _stackBack2.Visibility = Visibility.Collapsed;
                _expandedStackPanel.Visibility = Visibility.Collapsed;
                _bubbleExpanded = false;
                SetWindowSizeKeepingAnchor(baseW, _baseWindowHeight);
                return;
            }

            // 贴边收起观察态：将聊天框直接精简为高对比度像素状态色块
            bool isDockedCompact = !string.IsNullOrEmpty(_dockSide) && !_dockPeekOut && !_fullscreenPeekActive && !_bubbleExpanded;
            if (isDockedCompact)
            {
                _bubbleHost.HorizontalAlignment = HorizontalAlignment.Center;
                _stackBack1.Visibility = Visibility.Collapsed;
                _stackBack2.Visibility = Visibility.Collapsed;
                _expandedStackPanel.Visibility = Visibility.Collapsed;
                _bubbleText.Visibility = Visibility.Collapsed;
                bool showBlock = (_state != PetState.Idle) && (_config.BubbleMode != "alerts" || isAlert);
                if (!showBlock)
                {
                    _bubbleBorder.Visibility = Visibility.Hidden;
                }
                else
                {
                    _bubbleBorder.Width = Math.Round(34 * _config.Scale);
                    _bubbleBorder.Height = Math.Round(14 * _config.Scale);
                    _bubbleBorder.Padding = new Thickness(0);
                    _bubbleBorder.Background = new SolidColorBrush(badgeBg);
                    _bubbleBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(15, 23, 42));
                    _bubbleBorder.Margin = (_dockSide == "right")
                        ? new Thickness(0, 0, Math.Round(58 * _config.Scale), 0)
                        : new Thickness(Math.Round(58 * _config.Scale), 0, 0, 0);
                    _bubbleBorder.Visibility = Visibility.Visible;
                }
                SetWindowSizeKeepingAnchor(baseW, _baseWindowHeight);
                return;
            }

            if (IsDetailedStyle())
            {
                var tasks = GetOrderedActiveTasks();
                var visibleTasks = _config.BubbleMode == "alerts"
                    ? tasks.Where(t => t.State == PetState.NeedsInput || t.State == PetState.Ready || t.State == PetState.Blocked).ToList()
                    : tasks;

                if (visibleTasks.Count == 0)
                {
                    _bubbleBorder.Visibility = Visibility.Hidden;
                    _stackBack1.Visibility = Visibility.Collapsed;
                    _stackBack2.Visibility = Visibility.Collapsed;
                    _expandedStackPanel.Visibility = Visibility.Collapsed;
                    _bubbleExpanded = false;
                    SetWindowSizeKeepingAnchor(baseW, _baseWindowHeight);
                    return;
                }

                double maxDetailTextW = Math.Round(330 * _config.Scale);
                double maxWinW = Math.Round(390 * _config.Scale);

                if (_bubbleExpanded && visibleTasks.Count >= 1)
                {
                    _bubbleBorder.Visibility = Visibility.Collapsed;
                    _stackBack1.Visibility = Visibility.Collapsed;
                    _stackBack2.Visibility = Visibility.Collapsed;
                    _expandedStackPanel.Children.Clear();

                    int showCount = Math.Min(5, visibleTasks.Count);
                    double widestText = 120;
                    for (int i = 0; i < showCount; i++)
                    {
                        double tw = MeasureBubbleTextWidth(FormatDetailedTaskLabel(visibleTasks[i]), 11.0);
                        if (tw > widestText) widestText = tw;
                    }
                    double cappedTextW = Math.Min(maxDetailTextW, widestText + 6);
                    for (int i = 0; i < showCount; i++)
                    {
                        _expandedStackPanel.Children.Add(BuildExpandedTaskRow(visibleTasks[i], cappedTextW));
                    }
                    _expandedStackPanel.Visibility = Visibility.Visible;
                    double targetW = Math.Min(maxWinW, Math.Max(baseW, Math.Ceiling(cappedTextW + 66)));
                    double extraH = Math.Max(0, (showCount - 1) * 31.0);
                    SetWindowSizeKeepingAnchor(targetW, _baseWindowHeight + extraH);
                    return;
                }

                _expandedStackPanel.Visibility = Visibility.Collapsed;

                var topTask = visibleTasks[0];
                bool isPinned = !string.IsNullOrWhiteSpace(_config.PinnedConversationId) &&
                    string.Equals(topTask.ConversationId, _config.PinnedConversationId, StringComparison.OrdinalIgnoreCase);
                string label = (isPinned ? "📌 " : "") + FormatDetailedTaskLabel(topTask);
                if (visibleTasks.Count > 1) label += " (+" + (visibleTasks.Count - 1) + ")";

                double labelW = Math.Min(maxDetailTextW, MeasureBubbleTextWidth(label, 11.5));
                double cardW = Math.Max(136, Math.Ceiling(labelW + 24));
                _bubbleText.MaxWidth = maxDetailTextW;
                _bubbleText.Text = label;
                _bubbleBorder.BorderBrush = new SolidColorBrush(isPinned ? Color.FromRgb(250, 204, 21) : GetStateAccentColor(topTask.State));
                _bubbleBorder.ToolTip = visibleTasks.Count > 1
                    ? T("悬停展开全部任务 · 点击打开: ", "Hover to expand all · Click to open: ") + topTask.Title
                    : (T("点击打开对话: ", "Click to open: ") + topTask.Title);
                _bubbleBorder.Visibility = Visibility.Visible;

                if (visibleTasks.Count >= 2)
                {
                    _stackBack1.Width = Math.Max(110, cardW - 14);
                    _stackBack1.BorderBrush = new SolidColorBrush(GetStateAccentColor(visibleTasks[1].State));
                    _stackBack1.Visibility = Visibility.Visible;
                }
                else
                {
                    _stackBack1.Visibility = Visibility.Collapsed;
                }

                if (visibleTasks.Count >= 3)
                {
                    _stackBack2.Width = Math.Max(92, cardW - 28);
                    _stackBack2.BorderBrush = new SolidColorBrush(GetStateAccentColor(visibleTasks[2].State));
                    _stackBack2.Visibility = Visibility.Visible;
                }
                else
                {
                    _stackBack2.Visibility = Visibility.Collapsed;
                }

                double targetWinW = Math.Min(maxWinW, Math.Max(baseW, cardW + 24));
                double stackExtraH = visibleTasks.Count >= 3 ? 12 : (visibleTasks.Count >= 2 ? 6 : 0);
                SetWindowSizeKeepingAnchor(targetWinW, _baseWindowHeight + stackExtraH);
                return;
            }

            // 简易模式
            _stackBack1.Visibility = Visibility.Collapsed;
            _stackBack2.Visibility = Visibility.Collapsed;
            _expandedStackPanel.Visibility = Visibility.Collapsed;
            _bubbleExpanded = false;
            _bubbleText.MaxWidth = Math.Round((BaseWidth - 28) * _config.Scale);
            SetWindowSizeKeepingAnchor(baseW, _baseWindowHeight);

            string msg = string.IsNullOrWhiteSpace(_statusMessage) ? defaultMsg : LocalizeBubbleMessage(_statusMessage);
            bool shouldShowBubble = !string.IsNullOrEmpty(msg) &&
                (_config.BubbleMode == "always" || (_config.BubbleMode == "alerts" && isAlert));

            if (shouldShowBubble)
            {
                _bubbleText.Text = msg;
                Color borderClr = (_state == PetState.Idle || _state == PetState.Running) && _currentPack != null
                    ? _currentPack.ThemeColor
                    : badgeBg;
                _bubbleBorder.BorderBrush = new SolidColorBrush(borderClr);
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

        private void UpdateVisualState()
        {
            PetState effectiveState = HasPendingApproval() ? PetState.NeedsInput : _state;
            Color haloColor = Colors.Transparent;
            string badge = "";
            Color badgeBg = Colors.DodgerBlue;
            string defaultMsg = "";

            switch (effectiveState)
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

            if (string.IsNullOrEmpty(badge) && !string.IsNullOrEmpty(_availableUpdateVersion))
            {
                haloColor = Color.FromRgb(192, 132, 252);
                badge = "↑";
                badgeBg = Color.FromRgb(168, 85, 247);
            }

            _statusHalo.Fill = new RadialGradientBrush(
                Color.FromArgb(185, haloColor.R, haloColor.G, haloColor.B),
                Color.FromArgb(0, haloColor.R, haloColor.G, haloColor.B));

            bool isDockedCompact = !string.IsNullOrEmpty(_dockSide) && !_dockPeekOut && !_fullscreenPeekActive && !_bubbleExpanded;
            if (!string.IsNullOrEmpty(badge) && !isDockedCompact)
            {
                _badgeText.Text = badge;
                _badgeBorder.Background = new SolidColorBrush(badgeBg);
                _badgeBorder.Visibility = Visibility.Visible;
            }
            else
            {
                _badgeBorder.Visibility = Visibility.Collapsed;
            }

            bool isAlert = (effectiveState == PetState.NeedsInput || effectiveState == PetState.Ready || effectiveState == PetState.Blocked);
            UpdateBubbleDisplay(badgeBg, isAlert, defaultMsg);
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
                foreach (var proc in GetAntigravityProcesses())
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

                    string agyExe = FindAntigravityExePath();

                    if (!string.IsNullOrEmpty(agyExe))
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

        private static Style _cachedMenuStyle;
        private static Style _cachedMenuItemStyle;
        private static Style _cachedSepStyle;

        private static void EnsureContextMenuStyles()
        {
            if (_cachedMenuStyle != null && _cachedMenuItemStyle != null && _cachedSepStyle != null) return;
            try
            {
                string menuXaml =
                    "<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='ContextMenu'>" +
                    "  <Setter Property='Background' Value='#1D1D21'/>" +
                    "  <Setter Property='BorderBrush' Value='#34343B'/>" +
                    "  <Setter Property='BorderThickness' Value='1'/>" +
                    "  <Setter Property='Foreground' Value='#F4F4F5'/>" +
                    "  <Setter Property='Padding' Value='4'/>" +
                    "  <Setter Property='HasDropShadow' Value='True'/>" +
                    "  <Setter Property='Template'>" +
                    "    <Setter.Value>" +
                    "      <ControlTemplate TargetType='ContextMenu'>" +
                    "        <Border Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='8' Padding='{TemplateBinding Padding}'>" +
                    "          <StackPanel IsItemsHost='True' KeyboardNavigation.DirectionalNavigation='Cycle'/>" +
                    "        </Border>" +
                    "      </ControlTemplate>" +
                    "    </Setter.Value>" +
                    "  </Setter>" +
                    "</Style>";

                string itemXaml =
                    "<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='MenuItem'>" +
                    "  <Setter Property='Foreground' Value='#F4F4F5'/>" +
                    "  <Setter Property='FontSize' Value='12.5'/>" +
                    "  <Setter Property='Padding' Value='10,7'/>" +
                    "  <Setter Property='MinHeight' Value='30'/>" +
                    "  <Setter Property='Template'>" +
                    "    <Setter.Value>" +
                    "      <ControlTemplate TargetType='MenuItem'>" +
                    "        <Grid>" +
                    "          <Border x:Name='Bd' Background='Transparent' CornerRadius='5' Padding='{TemplateBinding Padding}' Margin='1,1'>" +
                    "            <Grid>" +
                    "              <Grid.ColumnDefinitions>" +
                    "                <ColumnDefinition Width='18'/>" +
                    "                <ColumnDefinition Width='*'/>" +
                    "                <ColumnDefinition Width='18'/>" +
                    "              </Grid.ColumnDefinitions>" +
                    "              <TextBlock x:Name='CheckMark' Grid.Column='0' Text='✓' FontSize='11' FontWeight='Bold' Foreground='#60A5FA' VerticalAlignment='Center' HorizontalAlignment='Left' Visibility='Collapsed'/>" +
                    "              <ContentPresenter Grid.Column='1' ContentSource='Header' VerticalAlignment='Center' RecognizesAccessKey='True'/>" +
                    "              <TextBlock x:Name='Arrow' Grid.Column='2' Text='▸' FontSize='11' Foreground='#A1A1AA' VerticalAlignment='Center' HorizontalAlignment='Right' Visibility='Collapsed'/>" +
                    "            </Grid>" +
                    "          </Border>" +
                    "          <Popup x:Name='PART_Popup' Placement='Right' HorizontalOffset='-4' VerticalOffset='-4' IsOpen='{Binding IsSubmenuOpen, RelativeSource={RelativeSource TemplatedParent}}' AllowsTransparency='True' Focusable='False' PopupAnimation='Fade'>" +
                    "            <Border Background='#1D1D21' BorderBrush='#34343B' BorderThickness='1' CornerRadius='8' Padding='4' MinWidth='176'>" +
                    "              <StackPanel IsItemsHost='True' KeyboardNavigation.DirectionalNavigation='Cycle'/>" +
                    "            </Border>" +
                    "          </Popup>" +
                    "        </Grid>" +
                    "        <ControlTemplate.Triggers>" +
                    "          <Trigger Property='Role' Value='SubmenuHeader'>" +
                    "            <Setter TargetName='Arrow' Property='Visibility' Value='Visible'/>" +
                    "          </Trigger>" +
                    "          <Trigger Property='Role' Value='TopLevelHeader'>" +
                    "            <Setter TargetName='Arrow' Property='Visibility' Value='Visible'/>" +
                    "          </Trigger>" +
                    "          <Trigger Property='IsChecked' Value='True'>" +
                    "            <Setter TargetName='CheckMark' Property='Visibility' Value='Visible'/>" +
                    "          </Trigger>" +
                    "          <Trigger Property='IsHighlighted' Value='True'>" +
                    "            <Setter TargetName='Bd' Property='Background' Value='#2E2E36'/>" +
                    "          </Trigger>" +
                    "          <Trigger Property='IsSubmenuOpen' Value='True'>" +
                    "            <Setter TargetName='Bd' Property='Background' Value='#2E2E36'/>" +
                    "          </Trigger>" +
                    "          <Trigger Property='IsEnabled' Value='False'>" +
                    "            <Setter Property='Foreground' Value='#8E8E98'/>" +
                    "          </Trigger>" +
                    "        </ControlTemplate.Triggers>" +
                    "      </ControlTemplate>" +
                    "    </Setter.Value>" +
                    "  </Setter>" +
                    "</Style>";

                string sepXaml =
                    "<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Separator'>" +
                    "  <Setter Property='Height' Value='1'/>" +
                    "  <Setter Property='Margin' Value='8,4'/>" +
                    "  <Setter Property='Background' Value='#2E2E35'/>" +
                    "  <Setter Property='Template'>" +
                    "    <Setter.Value>" +
                    "      <ControlTemplate TargetType='Separator'>" +
                    "        <Border Height='1' Background='{TemplateBinding Background}' Margin='{TemplateBinding Margin}'/>" +
                    "      </ControlTemplate>" +
                    "    </Setter.Value>" +
                    "  </Setter>" +
                    "</Style>";

                _cachedMenuStyle = (Style)System.Windows.Markup.XamlReader.Parse(menuXaml);
                _cachedMenuItemStyle = (Style)System.Windows.Markup.XamlReader.Parse(itemXaml);
                _cachedSepStyle = (Style)System.Windows.Markup.XamlReader.Parse(sepXaml);
            }
            catch
            {
                _cachedMenuStyle = null;
                _cachedMenuItemStyle = null;
                _cachedSepStyle = null;
            }
        }

        private void ApplyMenuItemStyle(MenuItem item)
        {
            if (item == null || _cachedMenuItemStyle == null) return;
            item.Style = _cachedMenuItemStyle;
            foreach (object child in item.Items)
            {
                var sub = child as MenuItem;
                if (sub != null) ApplyMenuItemStyle(sub);
                else if (child is Separator && _cachedSepStyle != null) ((Separator)child).Style = _cachedSepStyle;
            }
        }

        private void RebuildContextMenu()
        {
            EnsureContextMenuStyles();
            var menu = new ContextMenu();
            if (_cachedMenuStyle != null) menu.Style = _cachedMenuStyle;

            string stateLabel = "● " + StateToFriendlyLabel(_state);
            menu.Items.Add(new MenuItem { Header = stateLabel, IsEnabled = false });
            menu.Items.Add(new Separator());

            menu.Items.Add(MakeMenuItem(T("⚡ 快捷派活", "⚡ Quick Dispatch"), () => ShowQuickDispatchWindow()));

            var skinHeader = new MenuItem
            {
                Header = T("🎭 切换宠物 (", "🎭 Switch Pet (") + _packs.Count + ") ▸",
                StaysOpenOnClick = true
            };
            menu.Items.Add(skinHeader);

            var skinSubItems = new List<UIElement>();
            foreach (var p in _packs.Values.OrderBy(x => GetLocalizedPackName(x)))
            {
                string pid = p.Id;
                var item = new MenuItem
                {
                    Header = "  " + GetLocalizedPackName(p),
                    IsCheckable = true,
                    IsChecked = (_currentPack != null && _currentPack.Id.Equals(pid, StringComparison.OrdinalIgnoreCase)),
                    Visibility = Visibility.Collapsed
                };
                item.Click += (s, e) => SwitchSkin(pid);
                skinSubItems.Add(item);
                menu.Items.Add(item);
            }
            var refreshPetsItem = MakeMenuItem("  " + T("🔄 刷新宠物列表", "🔄 Refresh Pets"), () =>
            {
                ReloadPetPacks();
                RebuildContextMenu();
                RenderCurrentFrame();
            });
            refreshPetsItem.Visibility = Visibility.Collapsed;
            skinSubItems.Add(refreshPetsItem);
            menu.Items.Add(refreshPetsItem);

            bool skinsExpanded = false;
            skinHeader.Click += (s, e) =>
            {
                skinsExpanded = !skinsExpanded;
                skinHeader.Header = T("🎭 切换宠物 (", "🎭 Switch Pet (") + _packs.Count + (skinsExpanded ? ") ▾" : ") ▸");
                Visibility vis = skinsExpanded ? Visibility.Visible : Visibility.Collapsed;
                foreach (var el in skinSubItems)
                {
                    el.Visibility = vis;
                }
            };

            if (!string.IsNullOrEmpty(_dockSide))
            {
                menu.Items.Add(MakeMenuItem(T("🔓 取消贴边", "🔓 Undock from Edge"), () =>
                {
                    ResetToCorner(true);
                    RebuildContextMenu();
                }));
            }
            else
            {
                menu.Items.Add(MakeMenuItem(T("⬅️ 贴靠左侧", "⬅️ Dock Left"), () =>
                {
                    _dockSide = "left"; _dockPeekOut = false; _dockIgnoreHoverUntil = DateTime.Now.AddMilliseconds(700); _requireHoverExitBeforePeek = true;
                    UpdateVisualState(); Left = GetDockedTargetLeft("left", false); RebuildContextMenu();
                }));
                menu.Items.Add(MakeMenuItem(T("➡️ 贴靠右侧", "➡️ Dock Right"), () =>
                {
                    _dockSide = "right"; _dockPeekOut = false; _dockIgnoreHoverUntil = DateTime.Now.AddMilliseconds(700); _requireHoverExitBeforePeek = true;
                    UpdateVisualState(); Left = GetDockedTargetLeft("right", false); RebuildContextMenu();
                }));
            }

            menu.Items.Add(MakeMenuItem(T("⚙️ 设置 (Antigravity 面板)", "⚙️ Settings (Antigravity)"), () =>
            {
                OpenAntigravityConversation("", "");
                ReportTaskState("settings-hint", PetState.Ready, T("请点右上角 ... → 桌面宠物设置", "Open ... → Antigravity Pet"), "", false);
            }));

            menu.Items.Add(new Separator());
            menu.Items.Add(MakeMenuItem(T("🙈 隐藏桌宠 (Win+Alt+P)", "🙈 Hide Pet (Win+Alt+P)"), () => Hide()));
            menu.Items.Add(MakeMenuItem(T("🚪 退出桌宠", "🚪 Quit Pet"), () => { _config.Enabled = false; SaveConfig(); Close(); }));

            foreach (object obj in menu.Items)
            {
                var mi = obj as MenuItem;
                if (mi != null) ApplyMenuItemStyle(mi);
                else if (obj is Separator && _cachedSepStyle != null) ((Separator)obj).Style = _cachedSepStyle;
            }

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

        private static readonly string[] AntigravityProcessNames = new[] { "Antigravity", "Antigravity IDE" };

        private static List<Process> GetAntigravityProcesses()
        {
            var list = new List<Process>();
            foreach (string name in AntigravityProcessNames)
            {
                try
                {
                    list.AddRange(Process.GetProcessesByName(name));
                }
                catch { }
            }
            return list;
        }

        private static string FindAntigravityExePath()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string[] candidates = new[]
            {
                System.IO.Path.Combine(localAppData, "Programs", "antigravity", "Antigravity.exe"),
                System.IO.Path.Combine(localAppData, "Programs", "Antigravity IDE", "Antigravity IDE.exe")
            };
            foreach (string p in candidates)
            {
                if (File.Exists(p)) return p;
            }
            return null;
        }

        private static string GenerateHttpToken()
        {
            byte[] buf = new byte[16];
            using (var rng = new System.Security.Cryptography.RNGCryptoServiceProvider())
            {
                rng.GetBytes(buf);
            }
            var sb = new StringBuilder(32);
            for (int i = 0; i < buf.Length; i++)
            {
                sb.Append(buf[i].ToString("x2"));
            }
            return sb.ToString();
        }

        private void WriteHttpBridgeFile()
        {
            try
            {
                string dir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".antigravity-pet");
                Directory.CreateDirectory(dir);
                string bridgePath = System.IO.Path.Combine(dir, "http-bridge.json");
                var json = new JavaScriptSerializer();
                File.WriteAllText(bridgePath, json.Serialize(new { port = PORT, token = _httpToken }), new UTF8Encoding(false));
            }
            catch { }
        }

        private void StartHttpServer()
        {
            try
            {
                _httpToken = GenerateHttpToken();
                WriteHttpBridgeFile();

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
                            ThreadPool.QueueUserWorkItem(state => HandleRequest((HttpListenerContext)state), ctx);
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

            var json = new JavaScriptSerializer();
            int statusCode = 200;
            string responseJson = "{\"ok\":true}";

            try
            {
                Dictionary<string, object> data = string.IsNullOrWhiteSpace(body)
                    ? new Dictionary<string, object>()
                    : (json.Deserialize<Dictionary<string, object>>(body) ?? new Dictionary<string, object>());

                if (path == "/health")
                {
                    object info = null;
                    Dispatcher.Invoke(() =>
                    {
                        var hwnd = new WindowInteropHelper(this).Handle;
                        bool win32Topmost = hwnd != IntPtr.Zero && ((GetWindowLong(hwnd, GWL_EXSTYLE) & WS_EX_TOPMOST) != 0);
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
                            bubbleStyle = IsDetailedStyle() ? "detailed" : "simple",
                            pinnedConversationId = _config.PinnedConversationId ?? "",
                            readyTimeoutSec = _config.ReadyTimeoutSec,
                            snapToAgy = _config.SnapToAgy,
                            hideOnFullscreen = _config.HideOnFullscreen,
                            accessory = _config.Accessory ?? "auto",
                            effectiveAccessory = GetEffectiveAccessoryId(),
                            autoWalk = _config.AutoWalk,
                            eyeFollow = _config.EyeFollow,
                            boredomIdle = _config.BoredomIdle,
                            keyMouseInteract = _config.KeyMouseInteract,
                            topmost = _config.Topmost,
                            win32Topmost = win32Topmost,
                            autoStartOnBoot = _config.AutoStartOnBoot,
                            checkUpdatesOnStartup = _config.CheckUpdatesOnStartup,
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
                    responseJson = json.Serialize(info);
                }
                else if (path == "/approval")
                {
                    string reqToken = ctx.Request.Headers["X-Pet-Token"] ?? "";
                    if (string.IsNullOrEmpty(reqToken))
                    {
                        reqToken = GetStr(data, "token", "");
                    }
                    if (!string.IsNullOrEmpty(_httpToken) && !string.Equals(reqToken, _httpToken, StringComparison.Ordinal))
                    {
                        statusCode = 403;
                        responseJson = "{\"ok\":false,\"error\":\"forbidden\"}";
                    }
                    else
                    {
                        string convId = GetStr(data, "conversationId", "default");
                        string toolName = GetStr(data, "toolName", "tool");
                        string summary = GetStr(data, "summary", toolName);
                        int waitSec = 45;
                        int.TryParse(GetStr(data, "timeoutSec", "45"), out waitSec);
                        if (waitSec < 5 || waitSec > 120) waitSec = 45;

                        var req = new PendingApproval
                        {
                            Id = Guid.NewGuid().ToString("N"),
                            ConversationId = convId,
                            ToolName = toolName,
                            Summary = summary
                        };
                        lock (_approvalLock) { _pendingApprovals.Add(req); }

                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            if (!IsVisible && _config.Enabled) Show();
                            UpdateVisualState();
                            SyncFullscreenAndEdgeState();
                            PlayPetSound("needs_input");
                        }));

                        req.WaitHandle.Wait(TimeSpan.FromSeconds(waitSec));
                        lock (_approvalLock) { _pendingApprovals.Remove(req); }
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            UpdateVisualState();
                            SyncFullscreenAndEdgeState();
                        }));
                        responseJson = json.Serialize(new { ok = true, decision = req.Decision });
                    }
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
                        if (action == "show") { _config.Enabled = true; SaveConfig(); Show(); if (_config.Topmost) ApplyTopmost(true); StartTransient(TransientAction.Wave, 8); }
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
                        else if (action == "resetpos") { ResetToCorner(true); RebuildContextMenu(); }
                        else if (action == "dockleft")
                        {
                            _dockSide = "left"; _dockPeekOut = false; _dockIgnoreHoverUntil = DateTime.Now.AddMilliseconds(700); _requireHoverExitBeforePeek = true;
                            UpdateVisualState(); Left = GetDockedTargetLeft("left", false); RebuildContextMenu();
                        }
                        else if (action == "dockright")
                        {
                            _dockSide = "right"; _dockPeekOut = false; _dockIgnoreHoverUntil = DateTime.Now.AddMilliseconds(700); _requireHoverExitBeforePeek = true;
                            UpdateVisualState(); Left = GetDockedTargetLeft("right", false); RebuildContextMenu();
                        }
                        else if (action == "undock") { ResetToCorner(true); RebuildContextMenu(); }
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
                        else if (action == "bubblestyle")
                        {
                            if (val == "simple" || val == "detailed")
                            {
                                _config.BubbleStyle = val;
                                if (val == "simple") _bubbleExpanded = false;
                                UpdateVisualState();
                                SaveConfig();
                                RebuildContextMenu();
                            }
                        }
                        else if (action == "pinconversation")
                        {
                            _config.PinnedConversationId = val ?? "";
                            RecomputeEffectiveState();
                            SaveConfig();
                        }
                        else if (action == "readytimeoutsec")
                        {
                            int sec;
                            if (int.TryParse(val, out sec) && sec >= 0 && sec <= 3600)
                            {
                                _config.ReadyTimeoutSec = sec;
                                RecomputeEffectiveState();
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
                        else if (action == "boredomidle")
                        {
                            _config.BoredomIdle = ParseBoolStr(val, !_config.BoredomIdle);
                            if (!_config.BoredomIdle) InterruptBoredom(false);
                            SaveConfig();
                            RebuildContextMenu();
                        }
                        else if (action == "keymouseinteract")
                        {
                            _config.KeyMouseInteract = ParseBoolStr(val, !_config.KeyMouseInteract);
                            SyncKeyMouseListeners();
                            SaveConfig();
                            RebuildContextMenu();
                        }
                        else if (action == "topmost")
                        {
                            ApplyTopmost(ParseBoolStr(val, !_config.Topmost));
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
                        else if (action == "checkupdatesonstartup")
                        {
                            _config.CheckUpdatesOnStartup = ParseBoolStr(val, !_config.CheckUpdatesOnStartup);
                            SaveConfig();
                        }
                        else if (action == "soundalert")
                        {
                            _config.SoundAlert = ParseBoolStr(val, !_config.SoundAlert);
                            SaveConfig();
                            RebuildContextMenu();
                        }
                        else if (action == "snaptoagy")
                        {
                            _config.SnapToAgy = ParseBoolStr(val, !_config.SnapToAgy);
                            SaveConfig();
                            RebuildContextMenu();
                        }
                        else if (action == "hideonfullscreen")
                        {
                            _config.HideOnFullscreen = ParseBoolStr(val, !_config.HideOnFullscreen);
                            SaveConfig();
                            RebuildContextMenu();
                        }
                        else if (action == "accessory")
                        {
                            _config.Accessory = string.IsNullOrWhiteSpace(val) ? "auto" : val.Trim().ToLowerInvariant();
                            UpdateAccessoryVisual();
                            SaveConfig();
                            RebuildContextMenu();
                        }
                        else if (action == "quickdispatch")
                        {
                            ShowQuickDispatchWindow();
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
                        else if (action == "checkupdate") { CheckGitHubReleaseAsync(); }
                        else if (action == "mockupdate")
                        {
                            _availableUpdateVersion = string.IsNullOrWhiteSpace(val) ? "9.9.9" : val.Trim().TrimStart('v', 'V');
                            _availableUpdateUrl = "https://github.com/Wlaeg/antigravity-pet/releases/latest";
                            _availableUpdateZipUrl = "";
                            _updatePromptShownAt = DateTime.Now;
                            UpdateVisualState();
                            SyncFullscreenAndEdgeState();
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
                statusCode = 500;
                responseJson = json.Serialize(new { ok = false, error = ex.Message });
            }

            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(responseJson);
                ctx.Response.StatusCode = statusCode;
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
            if (data.ContainsKey("bubbleStyle") && data["bubbleStyle"] != null)
            {
                string bs = data["bubbleStyle"].ToString().Trim().ToLowerInvariant();
                if (bs == "simple" || bs == "detailed") _config.BubbleStyle = bs;
            }
            if (data.ContainsKey("pinnedConversationId"))
            {
                _config.PinnedConversationId = data["pinnedConversationId"] != null ? data["pinnedConversationId"].ToString() : "";
            }
            if (data.ContainsKey("readyTimeoutSec") && data["readyTimeoutSec"] != null)
            {
                int sec;
                if (int.TryParse(data["readyTimeoutSec"].ToString(), out sec) && sec >= 0 && sec <= 3600) _config.ReadyTimeoutSec = sec;
            }
            if (data.ContainsKey("language") && data["language"] != null)
            {
                string lg = data["language"].ToString().Trim().ToLowerInvariant();
                if (lg == "auto" || lg == "en" || lg == "zh") _config.Language = lg;
            }
            if (data.ContainsKey("autoWalk")) { _config.AutoWalk = GetBool(data, "autoWalk", _config.AutoWalk); if (!_config.AutoWalk) CancelWalk(); }
            if (data.ContainsKey("eyeFollow")) _config.EyeFollow = GetBool(data, "eyeFollow", _config.EyeFollow);
            if (data.ContainsKey("boredomIdle")) { _config.BoredomIdle = GetBool(data, "boredomIdle", _config.BoredomIdle); if (!_config.BoredomIdle) InterruptBoredom(false); }
            if (data.ContainsKey("keyMouseInteract")) { _config.KeyMouseInteract = GetBool(data, "keyMouseInteract", _config.KeyMouseInteract); SyncKeyMouseListeners(); }
            if (data.ContainsKey("topmost")) ApplyTopmost(GetBool(data, "topmost", _config.Topmost));
            if (data.ContainsKey("autoStartOnBoot")) { _config.AutoStartOnBoot = GetBool(data, "autoStartOnBoot", _config.AutoStartOnBoot); SyncAutoStartRegistry(_config.AutoStartOnBoot); }
            if (data.ContainsKey("checkUpdatesOnStartup")) _config.CheckUpdatesOnStartup = GetBool(data, "checkUpdatesOnStartup", _config.CheckUpdatesOnStartup);
            if (data.ContainsKey("soundAlert")) _config.SoundAlert = GetBool(data, "soundAlert", _config.SoundAlert);
            if (data.ContainsKey("snapToAgy")) _config.SnapToAgy = GetBool(data, "snapToAgy", _config.SnapToAgy);
            if (data.ContainsKey("hideOnFullscreen")) _config.HideOnFullscreen = GetBool(data, "hideOnFullscreen", _config.HideOnFullscreen);
            if (data.ContainsKey("accessory") && data["accessory"] != null) { _config.Accessory = data["accessory"].ToString().ToLowerInvariant(); UpdateAccessoryVisual(); }

            UpdateVisualState();
            SaveConfig();
            RebuildContextMenu();
        }

        private void ShowQuickDispatchWindow()
        {
            var dlg = new Window
            {
                Width = 310,
                Height = 112,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                Topmost = true,
                ShowInTaskbar = false,
                Left = Math.Max(SystemParameters.VirtualScreenLeft, Left + Width * 0.5 - 155),
                Top = Math.Max(SystemParameters.VirtualScreenTop, Top - 118)
            };

            var root = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(248, 15, 23, 42)),
                BorderBrush = new SolidColorBrush(_currentPack != null ? _currentPack.ThemeColor : Color.FromRgb(56, 189, 248)),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(10, 8, 10, 8),
                Effect = new DropShadowEffect { BlurRadius = 0, ShadowDepth = 3, Direction = 315, Color = Colors.Black, Opacity = 0.85 }
            };

            var panel = new StackPanel { Orientation = Orientation.Vertical };
            var head = new TextBlock
            {
                Text = T("⚡ 快捷派活给 Antigravity", "⚡ Quick Dispatch to Antigravity"),
                Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas, Microsoft YaHei UI"),
                Margin = new Thickness(0, 0, 0, 6)
            };
            panel.Children.Add(head);

            var box = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 4, 6, 4),
                FontSize = 12,
                FontFamily = new FontFamily("Consolas, Microsoft YaHei UI")
            };
            panel.Children.Add(box);

            var skillsRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            string[] skills = { "/plan", "/code-review", "/compact", "/goal" };
            foreach (string sk in skills)
            {
                string captured = sk;
                var chip = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(2),
                    Padding = new Thickness(6, 2, 6, 2),
                    Margin = new Thickness(0, 0, 5, 0),
                    Cursor = Cursors.Hand,
                    Child = new TextBlock { Text = captured, Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)), FontSize = 10, FontFamily = new FontFamily("Consolas") }
                };
                chip.MouseLeftButtonUp += (s, e) => { SendDispatchToPanel(captured); dlg.Close(); };
                skillsRow.Children.Add(chip);
            }
            panel.Children.Add(skillsRow);
            root.Child = panel;
            dlg.Content = root;

            box.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(box.Text))
                {
                    SendDispatchToPanel(box.Text.Trim());
                    dlg.Close();
                }
                else if (e.Key == Key.Escape)
                {
                    dlg.Close();
                }
            };
            dlg.Deactivated += (s, e) => { try { dlg.Close(); } catch { } };
            dlg.Show();
            box.Focus();
        }

        private string GetLocalPluginVersion()
        {
            try
            {
                string manifestPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "plugin.json"));
                if (File.Exists(manifestPath))
                {
                    var json = new JavaScriptSerializer();
                    var dict = json.Deserialize<Dictionary<string, object>>(File.ReadAllText(manifestPath, Encoding.UTF8));
                    string v = GetStr(dict, "version", "");
                    if (!string.IsNullOrWhiteSpace(v)) return v.Trim().TrimStart('v', 'V');
                }
            }
            catch { }
            return "4.0.1";
        }

        private static int CompareSemVer(string a, string b)
        {
            string[] pa = (a ?? "0.0.0").TrimStart('v', 'V').Split('.');
            string[] pb = (b ?? "0.0.0").TrimStart('v', 'V').Split('.');
            int len = Math.Max(pa.Length, pb.Length);
            for (int i = 0; i < len; i++)
            {
                int va = 0, vb = 0;
                if (i < pa.Length) int.TryParse(new string(pa[i].TakeWhile(char.IsDigit).ToArray()), out va);
                if (i < pb.Length) int.TryParse(new string(pb[i].TakeWhile(char.IsDigit).ToArray()), out vb);
                if (va != vb) return va.CompareTo(vb);
            }
            return 0;
        }

        private void CheckGitHubReleaseAsync()
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    Thread.Sleep(1500);
                    if (_isClosing) return;
                    ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
                    string localVer = GetLocalPluginVersion();
                    var req = (HttpWebRequest)WebRequest.Create("https://api.github.com/repos/Wlaeg/antigravity-pet/releases/latest");
                    req.Method = "GET";
                    req.UserAgent = "AntigravityPet/" + localVer;
                    req.Accept = "application/vnd.github+json";
                    req.Timeout = 5000;
                    using (var resp = (HttpWebResponse)req.GetResponse())
                    using (var st = resp.GetResponseStream())
                    using (var rd = new StreamReader(st, Encoding.UTF8))
                    {
                        string raw = rd.ReadToEnd();
                        var json = new JavaScriptSerializer();
                        var data = json.Deserialize<Dictionary<string, object>>(raw);
                        string tag = GetStr(data, "tag_name", "").Trim().TrimStart('v', 'V');
                        string htmlUrl = GetStr(data, "html_url", "https://github.com/Wlaeg/antigravity-pet/releases/latest");
                        string zipUrl = GetStr(data, "zipball_url", "");
                        if (data != null && data.ContainsKey("assets") && data["assets"] is System.Collections.IEnumerable)
                        {
                            foreach (object obj in (System.Collections.IEnumerable)data["assets"])
                            {
                                var asset = obj as Dictionary<string, object>;
                                string aname = GetStr(asset, "name", "");
                                string dl = GetStr(asset, "browser_download_url", "");
                                if (!string.IsNullOrEmpty(dl) && aname.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                                {
                                    zipUrl = dl;
                                    break;
                                }
                            }
                        }
                        if (!string.IsNullOrEmpty(tag) && CompareSemVer(tag, localVer) > 0)
                        {
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                if (_isClosing) return;
                                _availableUpdateVersion = tag;
                                _availableUpdateUrl = htmlUrl;
                                _availableUpdateZipUrl = zipUrl;
                                _updatePromptShownAt = DateTime.Now;
                                if (!IsVisible && _config.Enabled) Show();
                                UpdateVisualState();
                                SyncFullscreenAndEdgeState();
                            }));
                        }
                    }
                }
                catch { }
            });
        }

        private void PerformSelfUpdateAsync(string ver, string zipUrl)
        {
            if (_isUpdatingSelf) return;
            _isUpdatingSelf = true;
            _selfUpdateStatusMsg = T("正在下载更新 v" + ver + "...", "Downloading v" + ver + "...");
            UpdateVisualState();

            ThreadPool.QueueUserWorkItem(_ =>
            {
                string tmpRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "agypet-upd-" + Guid.NewGuid().ToString("N"));
                try
                {
                    Directory.CreateDirectory(tmpRoot);
                    string targetZipUrl = !string.IsNullOrWhiteSpace(zipUrl)
                        ? zipUrl
                        : ("https://github.com/Wlaeg/antigravity-pet/releases/download/v" + ver + "/antigravity-pet-v" + ver + ".zip");
                    string zipFile = System.IO.Path.Combine(tmpRoot, "update.zip");
                    string extractDir = System.IO.Path.Combine(tmpRoot, "extracted");

                    ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
                    var req = (HttpWebRequest)WebRequest.Create(targetZipUrl);
                    req.Method = "GET";
                    req.UserAgent = "AntigravityPet/" + GetLocalPluginVersion();
                    req.AllowAutoRedirect = true;
                    req.Timeout = 30000;
                    using (var resp = (HttpWebResponse)req.GetResponse())
                    using (var st = resp.GetResponseStream())
                    using (var fs = File.Create(zipFile))
                    {
                        st.CopyTo(fs);
                    }

                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        _selfUpdateStatusMsg = T("正在安装 v" + ver + " 并重启...", "Installing v" + ver + "...");
                        UpdateVisualState();
                    }));

                    var expandProc = Process.Start(new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -Command \"Expand-Archive -LiteralPath '" + zipFile.Replace("'", "''") + "' -DestinationPath '" + extractDir.Replace("'", "''") + "' -Force\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                    expandProc.WaitForExit(20000);
                    if (expandProc.ExitCode != 0) throw new Exception("Expand-Archive failed");

                    string srcDir = extractDir;
                    if (!File.Exists(System.IO.Path.Combine(srcDir, "plugin.json")))
                    {
                        string[] subDirs = Directory.GetDirectories(extractDir);
                        foreach (string sd in subDirs)
                        {
                            if (File.Exists(System.IO.Path.Combine(sd, "plugin.json")))
                            {
                                srcDir = sd;
                                break;
                            }
                        }
                    }
                    if (!File.Exists(System.IO.Path.Combine(srcDir, "plugin.json")))
                    {
                        throw new Exception("Invalid package: missing plugin.json");
                    }

                    string pluginDir = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."));
                    string applyScript = System.IO.Path.Combine(tmpRoot, "apply.ps1");
                    int currentPid = Process.GetCurrentProcess().Id;
                    string psContent =
                        "$ErrorActionPreference='SilentlyContinue'\r\n" +
                        "for($i=0;$i -lt 40;$i++){ if(-not (Get-Process -Id " + currentPid + " -ErrorAction SilentlyContinue)){ break }; Start-Sleep -Milliseconds 100 }\r\n" +
                        "Stop-Process -Name 'AntigravityPet' -Force -ErrorAction SilentlyContinue\r\n" +
                        "Start-Sleep -Milliseconds 250\r\n" +
                        "robocopy '" + srcDir.Replace("'", "''") + "' '" + pluginDir.Replace("'", "''") + "' /E /XD node_modules .git /NFL /NDL /NJH /NJS | Out-Null\r\n" +
                        "$exe = Join-Path '" + pluginDir.Replace("'", "''") + "' 'app\\AntigravityPet.exe'\r\n" +
                        "if(-not (Test-Path $exe)){ powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path '" + pluginDir.Replace("'", "''") + "' 'build.ps1') }\r\n" +
                        "schtasks /Run /TN 'AntigravityPetLauncher' 2>$null | Out-Null\r\n" +
                        "if($LASTEXITCODE -ne 0 -and (Test-Path $exe)){ Start-Process -FilePath $exe }\r\n" +
                        "Remove-Item -LiteralPath '" + tmpRoot.Replace("'", "''") + "' -Recurse -Force -ErrorAction SilentlyContinue\r\n";
                    File.WriteAllText(applyScript, psContent, new UTF8Encoding(false));

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + applyScript + "\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });

                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        _config.Enabled = true;
                        SaveConfig();
                        Close();
                    }));
                }
                catch
                {
                    try { if (Directory.Exists(tmpRoot)) Directory.Delete(tmpRoot, true); } catch { }
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        _isUpdatingSelf = false;
                        _selfUpdateStatusMsg = "";
                        UpdateVisualState();
                        ReportTaskState("update-err", PetState.Blocked, T("自动更新失败，请检查网络", "Update failed, check network"), "", false);
                    }));
                }
            });
        }

        private void SendDispatchToPanel(string message, bool newConversation = false)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            string cid = newConversation ? "" : (!string.IsNullOrWhiteSpace(_activeConversationId) ? _activeConversationId : _lastConversationId);
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    string bridgePath = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        ".antigravity-pet", "panel-bridge.json");
                    if (!File.Exists(bridgePath))
                    {
                        Dispatcher.BeginInvoke(new Action(() => OpenAntigravityConversation(cid, _activeTitle)));
                        return;
                    }
                    var json = new JavaScriptSerializer();
                    var bridge = json.Deserialize<Dictionary<string, object>>(File.ReadAllText(bridgePath, Encoding.UTF8));
                    int port = 0;
                    if (bridge != null && bridge.ContainsKey("port")) int.TryParse(bridge["port"].ToString(), out port);
                    string token = GetStr(bridge, "token", "");
                    if (port <= 0) return;

                    var req = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:" + port + "/api/dispatch");
                    req.Method = "POST";
                    req.ContentType = "application/json; charset=utf-8";
                    req.Timeout = 4000;
                    byte[] body = Encoding.UTF8.GetBytes(json.Serialize(new { token = token, conversationId = cid, message = message, newConversation = newConversation }));
                    req.ContentLength = body.Length;
                    using (var st = req.GetRequestStream()) st.Write(body, 0, body.Length);
                    using (var resp = req.GetResponse()) { }
                }
                catch { }
            });
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

                if (_currentPack != null) _config.Skin = _currentPack.Id;
                File.WriteAllText(_configPath, _json.Serialize(_config), new UTF8Encoding(false));
            }
            catch { }
        }

        private static void EnablePerMonitorDpiAwareness()
        {
            try
            {
                // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = -4 (Windows 10 1703+)
                if (SetProcessDpiAwarenessContext(new IntPtr(-4))) return;
            }
            catch { }
            try
            {
                // PROCESS_PER_MONITOR_DPI_AWARE = 2 (Windows 8.1+)
                if (SetProcessDpiAwareness(2) == 0) return;
            }
            catch { }
            try { SetProcessDPIAware(); } catch { }
        }

        [STAThread]
        public static void Main(string[] args)
        {
            EnablePerMonitorDpiAwareness();
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
