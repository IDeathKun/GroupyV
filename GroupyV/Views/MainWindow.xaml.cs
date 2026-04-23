using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using GroupyV.ViewModels;

namespace GroupyV.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainWindowViewModel();
            SourceInitialized += OnSourceInitialized;
        }

        // ── Initialisation du hook Win32 ─────────────────────────────────────────

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            var handle = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(handle)?.AddHook(WndProc);
        }

        /// <summary>
        /// Intercepte WM_GETMINMAXINFO pour que la fenêtre maximisée
        /// respecte la zone de travail (= écran sans la barre des tâches).
        /// Fonctionne aussi en multi-écrans.
        /// </summary>
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam,
                               ref bool handled)
        {
            const int WM_GETMINMAXINFO = 0x0024;
            if (msg == WM_GETMINMAXINFO)
            {
                ApplyWorkAreaConstraint(hwnd, lParam);
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void ApplyWorkAreaConstraint(IntPtr hwnd, IntPtr lParam)
        {
            var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);

            // Écran sur lequel se trouve la fenêtre (multi-écrans supporté)
            var monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
            if (monitor != IntPtr.Zero)
            {
                var info = new NativeMethods.MONITORINFO
                {
                    cbSize = Marshal.SizeOf(typeof(NativeMethods.MONITORINFO))
                };
                NativeMethods.GetMonitorInfo(monitor, ref info);

                // WorkArea = zone visible (hors barre des tâches)
                // MonitorArea = zone physique complète de l'écran
                int offsetX = Math.Abs(info.rcWork.left   - info.rcMonitor.left);
                int offsetY = Math.Abs(info.rcWork.top    - info.rcMonitor.top);
                int width   = Math.Abs(info.rcWork.right  - info.rcWork.left);
                int height  = Math.Abs(info.rcWork.bottom - info.rcWork.top);

                mmi.ptMaxPosition.x = offsetX;
                mmi.ptMaxPosition.y = offsetY;
                mmi.ptMaxSize.x     = width;
                mmi.ptMaxSize.y     = height;

                mmi.ptMinTrackSize.x = (int)MinWidth;
                mmi.ptMinTrackSize.y = (int)MinHeight;
            }

            Marshal.StructureToPtr(mmi, lParam, true);
        }

        // ── Contrôles de fenêtre ─────────────────────────────────────────────────

        private void DragWindow(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
                Maximize_Click(sender, e);
            else if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
            => Application.Current.Shutdown();

        private void Minimize_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void Maximize_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;

        // ── Structures et P/Invoke ───────────────────────────────────────────────

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public NativeMethods.POINT ptReserved;
            public NativeMethods.POINT ptMaxSize;
            public NativeMethods.POINT ptMaxPosition;
            public NativeMethods.POINT ptMinTrackSize;
            public NativeMethods.POINT ptMaxTrackSize;
        }

        private static class NativeMethods
        {
            public const int MONITOR_DEFAULTTONEAREST = 2;

            [DllImport("user32.dll")]
            public static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

            [DllImport("user32.dll", CharSet = CharSet.Auto)]
            public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

            [StructLayout(LayoutKind.Sequential)]
            public struct POINT
            {
                public int x, y;
            }

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
            public struct MONITORINFO
            {
                public int    cbSize;
                public RECT   rcMonitor;
                public RECT   rcWork;
                public uint   dwFlags;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct RECT
            {
                public int left, top, right, bottom;
            }
        }
    }
}
