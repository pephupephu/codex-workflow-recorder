using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace CodexWorkflowRecorder
{
    static class NativeMethods
    {
        public const int WH_MOUSE_LL = 14;
        public const int WH_KEYBOARD_LL = 13;
        public const int WM_LBUTTONDOWN = 0x201;
        public const int WM_RBUTTONDOWN = 0x204;
        public const int WM_KEYDOWN = 0x100;
        public const int WM_SYSKEYDOWN = 0x104;

        public delegate IntPtr LowLevelHookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelHookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int x; public int y; }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData; public uint flags; public uint time; public IntPtr dwExtraInfo; }

        [StructLayout(LayoutKind.Sequential)]
        public struct KBDLLHOOKSTRUCT { public uint vkCode; public uint scanCode; public uint flags; public uint time; public IntPtr dwExtraInfo; }
    }

    public class RecorderEngine
    {
        private NativeMethods.LowLevelHookProc _mouseProc;
        private NativeMethods.LowLevelHookProc _keyboardProc;
        private IntPtr _mouseHookId = IntPtr.Zero;
        private IntPtr _keyboardHookId = IntPtr.Zero;

        private readonly string _outputDir;
        private readonly string _eventLogPath;
        private readonly string _screenshotsDir;
        private readonly string _summaryPath;
        private readonly string _stopSignalPath;
        private int _screenshotCounter = 0;
        private bool _isRunning = false;
        private DateTime _lastScreenshotTime = DateTime.MinValue;
        private readonly int _minScreenshotIntervalMs = 400;
        private int _stepCounter = 0;
        private string _recordingName = "unnamed";
        private HiddenForm _form;
        private StreamWriter _eventWriter;

        public RecorderEngine(string outputDir, string name)
        {
            _outputDir = outputDir;
            _recordingName = name;
            _screenshotsDir = Path.Combine(outputDir, "screenshots");
            Directory.CreateDirectory(_screenshotsDir);
            _eventLogPath = Path.Combine(outputDir, "events.jsonl");
            _summaryPath = Path.Combine(outputDir, "workflow-recording.json");
            _stopSignalPath = Path.Combine(outputDir, "stop.signal");
        }

        public void Start()
        {
            _isRunning = true;
            _eventWriter = new StreamWriter(_eventLogPath, false, Encoding.UTF8);
            _eventWriter.AutoFlush = true;

            _mouseProc = new NativeMethods.LowLevelHookProc(MouseHookCallback);
            _keyboardProc = new NativeMethods.LowLevelHookProc(KeyboardHookCallback);

            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                IntPtr moduleHandle = NativeMethods.GetModuleHandle(curModule.ModuleName);
                _mouseHookId = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _mouseProc, moduleHandle, 0);
                _keyboardHookId = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _keyboardProc, moduleHandle, 0);
            }

            WriteEvent("recording_started", string.Format("\"recording_name\":\"{0}\"", EscapeJson(_recordingName)));

            _form = new HiddenForm();
            _form.Shown += (s, e) =>
            {
                // Start a timer to check for stop signal
                Timer checkTimer = new Timer();
                checkTimer.Interval = 500;
                checkTimer.Tick += (ts, te) =>
                {
                    if (_isRunning && File.Exists(_stopSignalPath))
                    {
                        try { File.Delete(_stopSignalPath); } catch { }
                        _form.Close();
                    }
                };
                checkTimer.Start();
            };
            Application.Run(_form);
        }

        public void StopNow()
        {
            if (!_isRunning) return;
            _isRunning = false;

            if (_mouseHookId != IntPtr.Zero) NativeMethods.UnhookWindowsHookEx(_mouseHookId);
            if (_keyboardHookId != IntPtr.Zero) NativeMethods.UnhookWindowsHookEx(_keyboardHookId);

            WriteEvent("recording_stopped", string.Format("\"total_steps\":{0},\"total_screenshots\":{1}", _stepCounter, _screenshotCounter));
            WriteSummary();

            if (_eventWriter != null)
            {
                _eventWriter.Flush();
                _eventWriter.Close();
                _eventWriter = null;
            }
        }

        private void ForceStop()
        {
            if (_form != null && !_form.IsDisposed)
            {
                _form.Invoke(new Action(() => { _form.Close(); }));
            }
        }

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                if (wParam == (IntPtr)NativeMethods.WM_LBUTTONDOWN)
                    RecordAction("click", "left");
                else if (wParam == (IntPtr)NativeMethods.WM_RBUTTONDOWN)
                    RecordAction("click", "right");
            }
            return NativeMethods.CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
        }

        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)NativeMethods.WM_KEYDOWN || wParam == (IntPtr)NativeMethods.WM_SYSKEYDOWN))
            {
                NativeMethods.KBDLLHOOKSTRUCT hookStruct = (NativeMethods.KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(NativeMethods.KBDLLHOOKSTRUCT));

                if (hookStruct.vkCode == 123)
                {
                    if ((NativeMethods.GetAsyncKeyState(0x11) & 0x8000) != 0 &&
                        (NativeMethods.GetAsyncKeyState(0x10) & 0x8000) != 0)
                    {
                        ForceStop();
                        return (IntPtr)1;
                    }
                }

                if (IsNotableKey(hookStruct.vkCode))
                    RecordAction("keypress", ((Keys)hookStruct.vkCode).ToString());
            }
            return NativeMethods.CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
        }

        private bool IsNotableKey(uint vkCode)
        {
            if (vkCode == 13 || vkCode == 9 || vkCode == 27 || vkCode == 32 || vkCode == 8 || vkCode == 46) return true;
            if (vkCode >= 37 && vkCode <= 90) return true;
            if (vkCode >= 96 && vkCode <= 123) return true;
            if (vkCode >= 186 && vkCode <= 222) return true;
            return false;
        }

        private void RecordAction(string actionType, string detail)
        {
            _stepCounter++;

            string windowTitle = GetActiveWindowTitle();

            string screenshotFile = "";
            TimeSpan sinceLast = DateTime.Now - _lastScreenshotTime;
            if (sinceLast.TotalMilliseconds >= _minScreenshotIntervalMs)
            {
                _screenshotCounter++;
                screenshotFile = string.Format("step-{0:D4}.png", _stepCounter);
                CaptureScreen(Path.Combine(_screenshotsDir, screenshotFile));
                _lastScreenshotTime = DateTime.Now;
            }

            NativeMethods.POINT cursor;
            NativeMethods.GetCursorPos(out cursor);

            string ts = DateTime.Now.ToString("HH:mm:ss.fff");
            string windowEscaped = EscapeJson(windowTitle);
            string detailEscaped = EscapeJson(detail);
            string actionEscaped = EscapeJson(actionType);

            WriteEvent("action", string.Format(
                "\"step\":{0},\"time\":\"{1}\",\"action\":\"{2}\",\"detail\":\"{3}\",\"x\":{4},\"y\":{5},\"window\":\"{6}\",\"screenshot\":\"{7}\"",
                _stepCounter, ts, actionEscaped, detailEscaped, cursor.x, cursor.y, windowEscaped, EscapeJson(screenshotFile)));
        }

        private string GetActiveWindowTitle()
        {
            try
            {
                IntPtr hWnd = NativeMethods.GetForegroundWindow();
                StringBuilder sb = new StringBuilder(256);
                NativeMethods.GetWindowText(hWnd, sb, sb.Capacity);
                return sb.ToString().Trim();
            }
            catch { return ""; }
        }

        private void CaptureScreen(string filePath)
        {
            try
            {
                int screenLeft = SystemInformation.VirtualScreen.Left;
                int screenTop = SystemInformation.VirtualScreen.Top;
                int screenWidth = SystemInformation.VirtualScreen.Width;
                int screenHeight = SystemInformation.VirtualScreen.Height;

                using (Bitmap bitmap = new Bitmap(screenWidth, screenHeight))
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.CopyFromScreen(screenLeft, screenTop, 0, 0, new Size(screenWidth, screenHeight));
                    ImageCodecInfo jpgCodec = GetEncoderInfo("image/jpeg");
                    if (jpgCodec != null)
                    {
                        using (EncoderParameters p = new EncoderParameters(1))
                        {
                            p.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 70L);
                            bitmap.Save(filePath, jpgCodec, p);
                        }
                    }
                    else
                    {
                        bitmap.Save(filePath, ImageFormat.Png);
                    }
                }
            }
            catch { }
        }

        private ImageCodecInfo GetEncoderInfo(string mimeType)
        {
            foreach (ImageCodecInfo codec in ImageCodecInfo.GetImageEncoders())
                if (codec.MimeType == mimeType) return codec;
            return null;
        }

        private void WriteEvent(string eventType, string dataContent)
        {
            try
            {
                if (_eventWriter != null)
                {
                    string line = string.Format("{{\"type\":\"{0}\",\"data\":{{{1}}}}}",
                        EscapeJson(eventType), dataContent);
                    _eventWriter.WriteLine(line);
                }
            }
            catch { }
        }

        private string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            StringBuilder sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (c == '\\') sb.Append("\\\\");
                else if (c == '"') sb.Append("\\\"");
                else if (c == '\r') continue;
                else if (c == '\n') sb.Append("\\n");
                else if (c < 0x20) continue;
                else sb.Append(c);
            }
            return sb.ToString();
        }

        private void WriteSummary()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine("  \"recording_name\": \"" + EscapeJson(_recordingName) + "\",");
                sb.AppendLine("  \"recorded_at\": \"" + DateTime.Now.ToString("o") + "\",");
                sb.AppendLine("  \"total_steps\": " + _stepCounter + ",");
                sb.AppendLine("  \"total_screenshots\": " + _screenshotCounter + ",");
                sb.AppendLine("  \"source\": \"codex-workflow-recorder-windows\"");
                sb.AppendLine("}");
                File.WriteAllText(_summaryPath, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }
    }

    public class HiddenForm : Form
    {
        public HiddenForm()
        {
            WindowState = FormWindowState.Normal;
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.None;
            Opacity = 0;
            Width = 0;
            Height = 0;
            Load += (s, e) =>
            {
                Location = new Point(-32000, -32000);
            };
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_CLOSE = 0x0010;
            if (m.Msg == WM_CLOSE)
            {
                // Find the engine and stop it before closing
                var engine = Program.GetEngine();
                if (engine != null) engine.StopNow();
            }
            base.WndProc(ref m);
        }
    }

    public class Program
    {
        private static RecorderEngine _engine;

        public static RecorderEngine GetEngine() { return _engine; }

        [STAThread]
        public static void Main(string[] args)
        {
            string outputDir = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "codex-recording-" + Process.GetCurrentProcess().Id);
            string name = args.Length > 1 ? args[1] : "unnamed";

            Directory.CreateDirectory(outputDir);
            File.WriteAllText(Path.Combine(outputDir, "recorder.pid"), Process.GetCurrentProcess().Id.ToString(), Encoding.UTF8);
            File.WriteAllText(Path.Combine(outputDir, "recording-name.txt"), name, Encoding.UTF8);

            _engine = new RecorderEngine(outputDir, name);

            IntPtr handle = GetConsoleWindow();
            ShowWindow(handle, 0);

            _engine.Start();
        }

        [DllImport("kernel32.dll")] static extern IntPtr GetConsoleWindow();
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    }
}
