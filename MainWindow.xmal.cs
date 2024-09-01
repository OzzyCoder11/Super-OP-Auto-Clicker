// YOU CAN USE DNSPY OR OTHER TOOLS TO CHECK THIS
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace AutoClicker
{
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const uint MOUSEEVENTF_LEFTDOWN = 0x02;
        private const uint MOUSEEVENTF_LEFTUP = 0x04;

        private const int HOTKEY_ID = 9000;
        private bool isRunning = false;
        private CancellationTokenSource cancellationTokenSource;
        private string currentHotkey = "Ctrl+Z";
        private ModifierKeys _hotkeyModifiers = ModifierKeys.Control;
        private Key _hotkeyKey = Key.Z;

        public MainWindow()
        {
            InitializeComponent();
            UpdateHotkeyLabel();
            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            RegisterGlobalHotKey();
            RegisterEmergencyStopHotKey();
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            UnregisterGlobalHotKey();
            UnregisterEmergencyStopHotKey();
        }

        private void RegisterGlobalHotKey()
        {
            var windowHandle = new WindowInteropHelper(this).Handle;
            var helper = new HwndSourceHook(HwndHook);
            HwndSource.FromHwnd(windowHandle)?.AddHook(helper);

            uint virtualKeyCode = (uint)KeyInterop.VirtualKeyFromKey(_hotkeyKey);
            uint modifiers = (uint)_hotkeyModifiers;

            if (!RegisterHotKey(windowHandle, HOTKEY_ID, modifiers, virtualKeyCode))
            {
                MessageBox.Show("Hotkey registration failed. Please try another key combination.");
            }
        }

        private void UnregisterGlobalHotKey()
        {
            var windowHandle = new WindowInteropHelper(this).Handle;
            UnregisterHotKey(windowHandle, HOTKEY_ID);
        }

        private void RegisterEmergencyStopHotKey()
        {
            var windowHandle = new WindowInteropHelper(this).Handle;
            uint virtualKeyCode = (uint)KeyInterop.VirtualKeyFromKey(Key.Escape);
            if (!RegisterHotKey(windowHandle, HOTKEY_ID + 1, 0, virtualKeyCode))
            {
                MessageBox.Show("Emergency stop hotkey registration failed.");
            }
        }

        private void UnregisterEmergencyStopHotKey()
        {
            var windowHandle = new WindowInteropHelper(this).Handle;
            UnregisterHotKey(windowHandle, HOTKEY_ID + 1);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (id == HOTKEY_ID)
                {
                    ToggleClicker();
                    handled = true;
                }
                else if (id == HOTKEY_ID + 1) // Emergency Stop Hotkey
                {
                    EmergencyStop();
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        private void SetHotkeyButton_Click(object sender, RoutedEventArgs e)
        {
            StatusLabel.Text = "Updating hotkey...";
            this.KeyDown += MainWindow_KeyDown;
            SetHotkeyButton.IsEnabled = false;
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            _hotkeyModifiers = Keyboard.Modifiers;
            _hotkeyKey = e.Key;

            currentHotkey = $"{_hotkeyModifiers}+{_hotkeyKey}";
            UpdateHotkeyLabel();

            SetHotkeyButton.IsEnabled = true;
            this.KeyDown -= MainWindow_KeyDown;

            UnregisterGlobalHotKey();
            RegisterGlobalHotKey();

            StatusLabel.Text = $"Hotkey updated to: {currentHotkey}";
        }

        private void UpdateHotkeyLabel()
        {
            HotkeyLabel.Text = $"Current Hotkey: {currentHotkey}";
        }

        private async void ToggleClicker()
        {
            if (isRunning)
            {
                isRunning = false;
                cancellationTokenSource?.Cancel();
                StatusLabel.Text = "Status: Stopped";
            }
            else
            {
                isRunning = true;
                cancellationTokenSource = new CancellationTokenSource();
                StatusLabel.Text = "Status: Running";
                await Task.Run(() => AutoClicker(cancellationTokenSource.Token));
            }
        }

        private void AutoClicker(CancellationToken token)
        {
            var spinWait = new SpinWait();
            while (!token.IsCancellationRequested)
            {
                mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
                spinWait.SpinOnce(); // Keeps CPU usage somewhat in check
            }
        }

        private void EmergencyStop()
        {
            isRunning = false;
            cancellationTokenSource?.Cancel();
            StatusLabel.Text = "Status: Stopped";
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
