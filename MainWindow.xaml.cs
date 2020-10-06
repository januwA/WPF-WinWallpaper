using System;
using io = System.IO;
using System.Windows;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Threading;

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
      input = inputFile.Text;
      if (input.Length == 0 || wProgman == 0) return;

      closePlay();

      string arguments = $"-noborder -x 1920 -y 1080 -hide_banner -loglevel panic";

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
        EnumWindows(delegate(IntPtr hwnd, IntPtr Lparam) {
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

    private void Window_Closed(object sender, EventArgs e)
    {
      closePlay();
    }
  }
}
