using System;
using io = System.IO;
using System.Windows;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO;
using System.Windows.Media.Imaging;

namespace WinWallpaper
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        [DllImport("Kernel32.dll")]
        public static extern int OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("Kernel32.dll")]
        public static extern bool TerminateProcess(int hProcess, uint uExitCode);

        [DllImport("user32.dll")]
        public static extern int GetWindowThreadProcessId(int hWnd, out UInt32 lpdwProcessId);

        [DllImport("user32.dll")]
        public static extern int FindWindowA(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        public static extern int SetParent(int hWndChild, int hWndNewParent);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern int FindWindowExA(IntPtr hWndParent, IntPtr hWndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern int SendMessageTimeoutA(int hWnd, int Msg, IntPtr wParam, IntPtr lParam, int fuFlags, int uTimeout, int lpdwResult);

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        string input = "";
        string ffplayExe;
        int wProgman = 0;
        int wFFplay = 0;
        int wWorker = 0;
        UInt32 ffplayPID = 0;
        System.Diagnostics.Process process = null;
        bool isPlaying = false;


        private System.Windows.Forms.NotifyIcon notifyIcon = null;

        public MainWindow()
        {
            InitializeComponent();

            ffplayExe = GetFullPath("ffplay.exe");
            if (ffplayExe == null) ffplayExe = AppDomain.CurrentDomain.BaseDirectory + "ffplay.exe";
            if (System.IO.File.Exists(ffplayExe) == false)
            {
                MessageBox.Show("未找到[ffplay.exe]，播放功能无法使用", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Windows.Application.Current.Shutdown();
            }

            wProgman = FindWindowA("Progman", null);

            // 初始化命令行
            process = new System.Diagnostics.Process();
            process.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            process.StartInfo.FileName = ffplayExe;


            // 托盘图标
            //设置托盘的各个属性
            notifyIcon = new System.Windows.Forms.NotifyIcon();
            notifyIcon.Text = "WinWallpaper";

            notifyIcon.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(notifyIcon_MouseClick);

            notifyIcon.ContextMenu = new System.Windows.Forms.ContextMenu(new System.Windows.Forms.MenuItem[] {
       new System.Windows.Forms.MenuItem("exit", new EventHandler(exit_Click))
      });

            //窗体状态改变时候触发
            StateChanged += new EventHandler(SysTray_StateChanged);
        }

        private System.Drawing.Icon ImageWpfToGDI(System.Windows.Media.ImageSource image)
        {
            MemoryStream ms = new MemoryStream();
            var encoder = new System.Windows.Media.Imaging.BmpBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image as System.Windows.Media.Imaging.BitmapSource));
            encoder.Save(ms);
            ms.Flush();
            return new System.Drawing.Icon(ms);
        }


        private void notifyIcon_MouseClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                notifyIcon.Visible = false;
                Visibility = Visibility.Visible;
                WindowState = WindowState.Normal;
                Activate();
            }
        }

        private void exit_Click(object sender, EventArgs e)
        {
            notifyIcon.Dispose();
            System.Windows.Application.Current.Shutdown();
        }


        private void SysTray_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                if (notifyIcon.Icon == null)
                {

                    string curExePath = io.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, System.AppDomain.CurrentDomain.FriendlyName);
                    notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(curExePath);
                }
                Visibility = Visibility.Hidden;
                notifyIcon.Visible = true;
                WindowState = WindowState.Normal;
            }
        }

        private string GetFullPath(string fileName)
        {
            if (io.File.Exists(fileName))
                return io.Path.GetFullPath(fileName);

            var values = Environment.GetEnvironmentVariable("PATH");
            foreach (var path in values.Split(io.Path.PathSeparator))
            {
                var fullPath = io.Path.Combine(path, fileName);
                if (io.File.Exists(fullPath))
                    return fullPath;
            }
            return null;
        }

        /// <summary>
        /// 拖拽视频
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Drop1(object sender, DragEventArgs e)
        {
            string[] filenames = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (filenames.Length > 0)
            {
                input = filenames[0];
                inputFile.Text = input;
            }
        }

        /// <summary>
        /// 选择视频
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog()
            {
                Filter = "(*.*)|*.*"
            };
            if (openFileDialog.ShowDialog() == false) return;
            input = openFileDialog.FileName;
            inputFile.Text = input;
        }

        /// <summary>
        /// 播放
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            var bounds = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
            var width = bounds.Width;
            var height = bounds.Height;

            input = inputFile.Text;
            if (input.Length == 0 || wProgman == 0) return;

            closePlay();

            string arguments = $"-noborder -x {width} -y {height} -hide_banner -loglevel panic";

            if (loop.IsChecked == true)
            {
                arguments += " -loop 0";
            }

            if (an.IsChecked == true)
            {
                arguments += " -an";
            }

            if (vn.IsChecked == true)
            {
                arguments += " -vn";
            }

            arguments += $" \"{input}\"";

            process.StartInfo.Arguments = arguments;

            startPlay();
        }

        void startPlay()
        {
            if (!isPlaying)
            {
                process.Start();
                isPlaying = true;
            }
        }

        void closePlay()
        {
            if (isPlaying)
            {
                try
                {
                    //process.CloseMainWindow();
                    //process.Close();
                    if (ffplayPID != 0)
                    {
                        int hProcess = OpenProcess(0x00100000 | 0x0001, false, ffplayPID);
                        TerminateProcess(hProcess, 0);
                        ffplayPID = 0;
                    }
                }
                catch
                {

                }

                isPlaying = false;
            }
        }

        /// <summary>
        /// 置底
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            wFFplay = FindWindowA("SDL_app", null);
            GetWindowThreadProcessId(wFFplay, out ffplayPID);

            if (wWorker == 0)
            {
                SendMessageTimeoutA(wProgman, 0x52C, IntPtr.Zero, IntPtr.Zero, 0, 1000, 0);
                EnumWindows(delegate (IntPtr hwnd, IntPtr Lparam)
                {
                    int SHELLDLL_DefView = FindWindowExA(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                    if (SHELLDLL_DefView != 0)
                    {
                        wWorker = FindWindowExA(IntPtr.Zero, hwnd, "WorkerW", null);
                        ShowWindow((IntPtr)wWorker, 0);
                        return false;
                    }
                    return true;
                }, IntPtr.Zero);
            }
            SetParent(wFFplay, wProgman);
        }

        /// <summary>
        /// 窗口关闭时
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Closed(object sender, EventArgs e)
        {
            closePlay();
            notifyIcon.Dispose();
        }
    }
}
