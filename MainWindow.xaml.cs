using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media; // 获取Matrix
using System.Windows.Media.Animation; // 渐入渐出
using explaina.Services;
using System.Windows.Forms; // 托盘显示
using System.Runtime.InteropServices; // 导入Windows API
using System;
using System.Windows.Interop;
using System.ComponentModel; // 引入CancelEventArgs
using System.Threading;


namespace explaina
{
    public partial class MainWindow : Window
    {
        private string StartText = @"# 欢迎使用 Explaina ( •̀ ω •́ )✧ 

Explaina 是随时随地调用 LLM 做名词解释的轻量化 Windows 工具。在使用 Explaina 前，请在任务栏托盘内右键点击 Explaina 图标，选择设置，填入您可用的 OpenRouter API Key；

使用 Explaina，您可以
- 选择您想要予以解释的文本
- 按下 **Ctrl+2** 调出 Explaina 对选择的文本进行解释
- 按下 **Esc** 退出窗口

Explaina 默认使用 `google/gemini-2.0-flash-exp:free`，如果您偏好其他模型，请在设置中填入 OpenRouter 上对应的模型路径 (Model Identifier for use in API)

每按下一次 **Ctrl+2**，即向 OpenRouter 发起一次调用；Explaina 在后台占用您约 60 MB 的内存。

随时随地提问吧！";

        #region 全局热键/常量/API
        private const int HOTKEY_ID = 9000;
        private const uint MOD_CONTROL = 0x0002; // Ctrl键
        private const uint VK_2 = 0x32; // 2键的虚拟键码
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetClipboardFormatName(uint format, StringBuilder lpszFormatName, int cchMaxCount);
        [DllImport("user32.dll")]
        static extern bool OpenClipboard(IntPtr hWndNewOwner);
        [DllImport("user32.dll")]
        static extern bool CloseClipboard();
        [DllImport("user32.dll")]
        static extern bool EmptyClipboard();
        [DllImport("user32.dll")]
        static extern IntPtr GetClipboardData(uint uFormat);
        private IntPtr _windowHandle;
        private HwndSource _source;
        // OpenRouter Service
        private OpenrouterService _openrouterService;
        private StringBuilder _currentResponse;
        #endregion


        private string _apiKey;
        private string _modelName;
        private string _promptSuffix;
        private bool _autoStart;
        public MainWindow()
        {
            InitializeComponent();
            _markdownService = new MarkdownService();
            _currentResponse = new StringBuilder();
            // 初始化 OpenrouterService
            _apiKey = SettingsService.GetApiKey();
            _modelName = SettingsService.GetModelName();
            _promptSuffix = SettingsService.GetPromptSuffix();
            _autoStart = SettingsService.GetAutoStart();

            _openrouterService = new OpenrouterService(_apiKey);
            _openrouterService.MessageReceived += OnMessageReceived;
            // 让右键菜单消失
            MarkdownViewer.ContextMenu = null;
            // 初始化托盘图标
            InitializeNotifyIcon();
            // 监听Esc键
            PreviewKeyDown += MainWindow_PreviewKeyDown;

            // 窗口加载完成后
            Loaded += (s, e) =>
            {
                _windowHandle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                _source = HwndSource.FromHwnd(_windowHandle);
                _source.AddHook(HwndHook);
                RegisterHotKey(_windowHandle, HOTKEY_ID, MOD_CONTROL, VK_2);
                RenderMarkdown(StartText);
            };
        }


        // 消息接收
        private void OnMessageReceived(object sender, string message)
        {
            Dispatcher.Invoke(() =>
            {
                _currentResponse.Append(message);
                RenderMarkdown(_currentResponse.ToString());
            });
        }
        // 消息发送
        public async Task<string> SendPromptAsync(string prompt, string model = "google/gemini-2.0-flash-exp:free")
        {
            try
            {
                _currentResponse.Clear();
                string response = await _openrouterService.SendMessageStreamAsync(prompt, model);
                return response;
            }
            catch (Exception ex)
            {
                string errorMessage = $"发送请求时出错: {ex.Message}";
                RenderMarkdown($"## 错误\n\n{errorMessage}");
                return null;
            }
        }


        // 拖动逻辑
        private bool isDragging = false;
        private Point lastPosition;
        private void Window_MouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            isDragging = true;
            lastPosition = e.GetPosition(this);
            Mouse.Capture((IInputElement)sender);
        }
        private void Window_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (isDragging && e.RightButton == MouseButtonState.Pressed)
            {
                Point currentPosition = e.GetPosition(this);
                double deltaX = currentPosition.X - lastPosition.X;
                double deltaY = currentPosition.Y - lastPosition.Y;

                Left += deltaX;
                Top += deltaY;
            }
        }
        private void Window_MouseRightButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            isDragging = false;
            Mouse.Capture(null);
        }


        // Markdown 渲染
        private readonly MarkdownService _markdownService;
        public void RenderMarkdown(string markdownText)
        {
            if (string.IsNullOrEmpty(markdownText))
            {
                return;
            }

            var flowDocument = _markdownService.ConvertMarkdownToFlowDocument(markdownText);
            MarkdownViewer.Document = flowDocument;
        }
        private void RenderSampleMarkdown(object sender, RoutedEventArgs e)
        {
            // 示例Markdown文本
            var sampleMarkdown = @"Markdown 示例";
            RenderMarkdown(sampleMarkdown);
        }


        private NotifyIcon notifyIcon;
        // 创建托盘图标和托盘右键菜单
        private void InitializeNotifyIcon()
        {
            notifyIcon = new NotifyIcon();
            notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location);
            notifyIcon.Text = "Explaina";
            notifyIcon.Visible = true;

            // 创建右键菜单
            ContextMenuStrip contextMenu = new ContextMenuStrip();
            // + 设置
            ToolStripMenuItem settingsItem = new ToolStripMenuItem("设置");
            settingsItem.Click += (s, e) => { ShowSettingsWindow(); };
            contextMenu.Items.Add(settingsItem);
            // + 退出应用
            ToolStripMenuItem exitItem = new ToolStripMenuItem("退出");
            exitItem.Click += (s, e) => { System.Windows.Application.Current.Shutdown(); };
            contextMenu.Items.Add(exitItem);
            // 应用右键菜单
            notifyIcon.ContextMenuStrip = contextMenu;
        }
        // 显示设置窗口
        private void ShowSettingsWindow()
        {
            var settingsWindow = new SettingsWindow(_apiKey, _modelName, _promptSuffix, _autoStart);
            if (settingsWindow.ShowDialog() == true)
            {
                // 获取新设置
                _apiKey = settingsWindow.ApiKey;
                _modelName = settingsWindow.ModelName;
                _promptSuffix = settingsWindow.PromptSuffix;
                _autoStart = settingsWindow.AutoStart;
                
                // 重新初始化服务
                if (_openrouterService != null)
                {
                    _openrouterService.MessageReceived -= OnMessageReceived;
                }
                
                _openrouterService = new OpenrouterService(_apiKey);
                _openrouterService.MessageReceived += OnMessageReceived;
            }
        }
        // 在应用程序关闭时释放资源
        protected override void OnClosed(EventArgs e)
        {
            notifyIcon.Dispose();
            if (_openrouterService != null)
            {
                _openrouterService.MessageReceived -= OnMessageReceived;
            }
            base.OnClosed(e);
            if (_windowHandle != IntPtr.Zero)
            {
                UnregisterHotKey(_windowHandle, HOTKEY_ID);
            }
        }
        // 窗口最小化时隐藏到托盘
        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide(); // 隐藏窗口
            }

            base.OnStateChanged(e);
        }
        // 窗口关闭时隐藏到托盘
        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = true;
            WindowState = WindowState.Minimized;
            Hide();

            base.OnClosing(e);
        }


        // 处理 Ctrl+2
        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                string prompt = GetSelectedText();
                RenderMarkdown("处理中...\n\n" + prompt + "\n\n" + _promptSuffix);
                System.Drawing.Point mousePosition = System.Windows.Forms.Cursor.Position;

                // 考虑缩放
                PresentationSource source = PresentationSource.FromVisual(this);
                if (source != null)
                {
                    Matrix transformMatrix = source.CompositionTarget.TransformFromDevice;
                    System.Windows.Point wpfPoint = transformMatrix.Transform(new System.Windows.Point(mousePosition.X, mousePosition.Y));

                    Left = wpfPoint.X + 10;
                    Top = wpfPoint.Y - Height - 10;
                    if (Top < 0) Top = 0;
                    double screenWidth = SystemParameters.VirtualScreenWidth;
                    if (Left + Width > screenWidth) Left = screenWidth - Width;
                }
                else
                {
                    Left = mousePosition.X + 10;
                    Top = mousePosition.Y - Height - 10;
                    if (Top < 0) Top = 0;
                    double screenWidth = SystemParameters.PrimaryScreenWidth;
                    if (Left + Width > screenWidth) Left = screenWidth - Width;
                }

                WindowAnimationHelper.FadeIn(this);

                // 调用 OpenRouter 服务
                Dispatcher.InvokeAsync(async () =>
                {
                    if (string.IsNullOrWhiteSpace(prompt))
                    {
                        RenderMarkdown(@"未选中任何文本");
                    }
                    else
                    {
                        await SendPromptAsync(prompt +"\n\n"+ _promptSuffix, _modelName);
                    }

                });
                handled = true;
            }
            return IntPtr.Zero;
        }
        // 处理Esc键
        private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                WindowAnimationHelper.FadeOut(this, completedAction: () =>
                {
                    WindowState = WindowState.Minimized;
                    Hide();
                });
                e.Handled = true;
            }
        }
        // 获取选中内容
        private string GetSelectedText()
        {
            string selectedText = string.Empty;
            System.Windows.IDataObject oldClipboard = System.Windows.Clipboard.GetDataObject();
            try
            {
                SendKeys.SendWait("^c");
                System.Threading.Thread.Sleep(100);
                if (System.Windows.Clipboard.ContainsText())
                {
                    selectedText = System.Windows.Clipboard.GetText();
                }
            }
            catch (Exception ex)
            {
                RenderMarkdown($"获取选中内容失败: {ex.Message}");
            }
            finally
            {
                try
                {
                    if (oldClipboard != null)
                    {
                        System.Windows.Clipboard.SetDataObject(oldClipboard, true);
                    }
                }
                catch
                {
                    // 什么也不干
                }
            }
            return selectedText;
        }
    }
    public static class WindowAnimationHelper
    {
        // 窗口渐入效果
        public static void FadeIn(Window window, double duration = 0.3, Action completedAction = null)
        {
            // 初始透明度设为0
            window.Opacity = 0;
            window.Show();
            window.WindowState = WindowState.Normal;
            window.Activate();
            

            // 创建透明度动画
            DoubleAnimation fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(duration)
            };

            if (completedAction != null)
            {
                fadeIn.Completed += (s, e) => completedAction();
            }

            // 应用动画
            window.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        }

        // 窗口渐出效果
        public static void FadeOut(Window window, double duration = 0.3, Action completedAction = null)
        {
            // 创建透明度动画
            DoubleAnimation fadeOut = new DoubleAnimation
            {
                From = window.Opacity,
                To = 0,
                Duration = TimeSpan.FromSeconds(duration)
            };

            if (completedAction != null)
            {
                fadeOut.Completed += (s, e) => completedAction();
            }
            else
            {
                fadeOut.Completed += (s, e) =>
                {
                    window.Hide();
                    // 当窗口隐藏后，将透明度重置为1，为下次显示做准备
                    window.Opacity = 1;
                };
            }

            // 应用动画
            window.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }
    }
}