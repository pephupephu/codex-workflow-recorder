using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace CodexWorkflowRecorder
{
    public class ReplayEngine
    {
        [DllImport("user32.dll")]
        static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        static extern short VkKeyScan(char ch);

        const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        const uint MOUSEEVENTF_LEFTUP = 0x0004;
        const uint KEYEVENTF_KEYDOWN = 0x0000;
        const uint KEYEVENTF_KEYUP = 0x0002;

        private string _eventsFile;
        private string _screenshotsDir;
        private bool _verbose;
        private int _speedMultiplier = 1;

        public ReplayEngine(string recordingDir, bool verbose = true)
        {
            _eventsFile = Path.Combine(recordingDir, "events.jsonl");
            _screenshotsDir = Path.Combine(recordingDir, "replay-screenshots");
            _verbose = verbose;
        }

        public void SetSpeedMultiplier(int multiplier)
        {
            _speedMultiplier = Math.Max(1, multiplier);
        }

        public bool Replay()
        {
            if (!File.Exists(_eventsFile))
            {
                LogError("Events file not found: " + _eventsFile);
                return false;
            }

            Directory.CreateDirectory(_screenshotsDir);

            string[] lines = File.ReadAllLines(_eventsFile, Encoding.UTF8);
            Log("=== Replay Engine Starting ===");
            Log("Events: " + lines.Length + " lines");

            List<ReplayEvent> events = ParseEvents(lines);
            if (events.Count == 0)
            {
                LogError("No replayable events found");
                return false;
            }

            Log("Found " + events.Count + " replayable events");
            Log("WARNING: Do not use mouse/keyboard during replay!");
            Log("Starting in 3 seconds...");
            Thread.Sleep(1000);
            Log("2...");
            Thread.Sleep(1000);
            Log("1...");
            Thread.Sleep(1000);
            Log("GO!");
            Thread.Sleep(500);

            DateTime? lastEventTime = null;
            int successCount = 0;

            for (int i = 0; i < events.Count; i++)
            {
                ReplayEvent evt = events[i];

                if (lastEventTime.HasValue && evt.Timestamp.HasValue)
                {
                    double ms = (evt.Timestamp.Value - lastEventTime.Value).TotalMilliseconds;
                    int delay = (int)(ms / _speedMultiplier);
                    if (delay > 0 && delay < 60000)
                    {
                        Thread.Sleep(delay);
                    }
                }

                string actionStr;
                if (evt.Action == "click")
                    actionStr = "Click at (" + evt.X + ", " + evt.Y + ")";
                else
                    actionStr = "Key: " + evt.Detail;
                Log("Step " + (i + 1) + "/" + events.Count + ": " + actionStr);

                bool success = ExecuteEvent(evt);
                if (success) successCount++;

                lastEventTime = evt.Timestamp;
                TakeScreenshot("step-" + (i + 1).ToString("D4"));
            }

            Log("");
            Log("=== Replay Complete ===");
            Log("Successfully executed " + successCount + "/" + events.Count + " events");
            return successCount > 0;
        }

        public List<ReplayEvent> ParseEvents(string[] lines)
        {
            List<ReplayEvent> events = new List<ReplayEvent>();
            foreach (string line in lines)
            {
                try
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed)) continue;

                    string pattern1 = "\\{ \"type\": \"(?<type>[^\"]+)\", \"data\": \\{(?<data>.+)\\} \\}";
                    Match match = Regex.Match(trimmed, pattern1);
                    if (!match.Success)
                    {
                        string pattern2 = "\\{\"type\":\"(?<type>[^\"]+)\",\"data\":\\{(?<data>.+)\\}\\}";
                        match = Regex.Match(trimmed, pattern2);
                    }

                    if (!match.Success) continue;

                    string type = match.Groups["type"].Value;
                    string data = match.Groups["data"].Value;

                    if (type == "recording_started" || type == "recording_stopped")
                        continue;

                    if (type == "action")
                    {
                        ReplayEvent evt = new ReplayEvent();
                        evt.Type = type;

                        string stepStr, timeStr, xStr, yStr;
                        ExtractField(data, "step", out stepStr);
                        int stepVal; int.TryParse(stepStr, out stepVal); evt.Step = stepVal;

                        ExtractField(data, "time", out timeStr);
                        DateTime parsedTime;
                        if (DateTime.TryParse(timeStr, out parsedTime))
                            evt.Timestamp = parsedTime;
                        else
                        {
                            TimeSpan ts;
                            if (TimeSpan.TryParse(timeStr, out ts))
                                evt.Timestamp = DateTime.Today.Add(ts);
                        }

                        string actionVal; ExtractField(data, "action", out actionVal); evt.Action = actionVal;
                        string detailVal; ExtractField(data, "detail", out detailVal); evt.Detail = detailVal;
                        ExtractField(data, "x", out xStr);
                        ExtractField(data, "y", out yStr);
                        int xVal; int.TryParse(xStr, out xVal); evt.X = xVal;
                        int yVal; int.TryParse(yStr, out yVal); evt.Y = yVal;
                        string winVal; ExtractField(data, "window", out winVal); evt.WindowTitle = winVal;

                        events.Add(evt);
                    }
                }
                catch { }
            }
            return events;
        }

        private void ExtractField(string data, string field, out string value)
        {
            string pat = "\"" + field + "\":\\s*\"(?<val>(?:[^\"\\\\]|\\\\.)*)\"";
            Match match = Regex.Match(data, pat);
            if (match.Success)
            {
                value = match.Groups["val"].Value;
                return;
            }

            pat = "\"" + field + "\":\\s*(?<val>[0-9]+)";
            match = Regex.Match(data, pat);
            if (match.Success)
            {
                value = match.Groups["val"].Value;
                return;
            }

            value = "";
        }

        private bool ExecuteEvent(ReplayEvent evt)
        {
            try
            {
                if (evt.Action == "click")
                {
                    SetCursorPos(evt.X, evt.Y);
                    Thread.Sleep(80);
                    mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
                    Thread.Sleep(40);
                    mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
                    Thread.Sleep(80);
                    return true;
                }
                else if (evt.Action == "keypress")
                {
                    return SimulateKeyPress(evt.Detail);
                }
                return false;
            }
            catch (Exception ex)
            {
                LogError("  Failed: " + ex.Message);
                return false;
            }
        }

        private bool SimulateKeyPress(string keyDetail)
        {
            if (string.IsNullOrEmpty(keyDetail)) return false;

            byte vkCode = 0;

            if (keyDetail.Length == 1)
            {
                char c = keyDetail[0];
                short scan = VkKeyScan(c);
                vkCode = (byte)(scan & 0xFF);
                if (vkCode != 0)
                {
                    PressKey(vkCode);
                    return true;
                }
            }

            string k = keyDetail.ToLower();
            if (k == "return" || k == "enter") vkCode = 0x0D;
            else if (k == "tab") vkCode = 0x09;
            else if (k == "escape" || k == "esc") vkCode = 0x1B;
            else if (k == "space") vkCode = 0x20;
            else if (k == "backspace") vkCode = 0x08;
            else if (k == "delete" || k == "del") vkCode = 0x2E;
            else if (k == "up") vkCode = 0x26;
            else if (k == "down") vkCode = 0x28;
            else if (k == "left") vkCode = 0x25;
            else if (k == "right") vkCode = 0x27;
            else if (k == "f12") vkCode = 0x7B;
            else if (k == "f5") vkCode = 0x74;
            else if (k.StartsWith("d") && k.Length == 2 && char.IsDigit(k[1]))
                vkCode = (byte)(0x30 + (k[1] - '0'));
            else if (k.StartsWith("num") && k.Length == 4 && char.IsDigit(k[3]))
                vkCode = (byte)(0x60 + (k[3] - '0'));
            else if (k == "control" || k == "controlkey") vkCode = 0x11;
            else if (k == "menu" || k == "alt") vkCode = 0x12;
            else if (k == "shift" || k == "shiftkey") vkCode = 0x10;
            else
            {
                LogError("Unknown key: " + keyDetail);
                return false;
            }

            if (vkCode != 0)
            {
                PressKey(vkCode);
                return true;
            }
            return false;
        }

        private void PressKey(byte vkCode)
        {
            keybd_event(vkCode, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
            Thread.Sleep(40);
            keybd_event(vkCode, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            Thread.Sleep(40);
        }

        private void TakeScreenshot(string label)
        {
            try
            {
                int screenLeft = SystemInformation.VirtualScreen.Left;
                int screenTop = SystemInformation.VirtualScreen.Top;
                int w = SystemInformation.VirtualScreen.Width;
                int h = SystemInformation.VirtualScreen.Height;

                using (Bitmap bitmap = new Bitmap(w, h))
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.CopyFromScreen(screenLeft, screenTop, 0, 0, new Size(w, h));
                    string filePath = Path.Combine(_screenshotsDir, label + ".png");
                    bitmap.Save(filePath, ImageFormat.Png);
                }
            }
            catch { }
        }

        private void Log(string message)
        {
            if (_verbose)
                Console.WriteLine("[Replayer] " + message);
        }

        private void LogError(string message)
        {
            Console.Error.WriteLine("[Replayer] ERROR: " + message);
        }

        public class ReplayEvent
        {
            public string Type { get; set; }
            public int Step { get; set; }
            public DateTime? Timestamp { get; set; }
            public string Action { get; set; }
            public string Detail { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
            public string WindowTitle { get; set; }
        }
    }

    public class ReplayProgram
    {
        [STAThread]
        public static void Main(string[] args)
        {
            Console.WriteLine("=== Codex Workflow Replayer ===");

            if (args.Length < 1)
            {
                Console.WriteLine("Usage: replayer.exe <recording-directory> [--speed N] [--dry-run]");
                return;
            }

            string recDir = args[0];
            if (!Directory.Exists(recDir))
            {
                Console.Error.WriteLine("Recording directory not found: " + recDir);
                return;
            }

            ReplayEngine replayer = new ReplayEngine(recDir);

            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] == "--speed" && i + 1 < args.Length)
                {
                    int speed;
                    if (int.TryParse(args[++i], out speed))
                        replayer.SetSpeedMultiplier(speed);
                }
                else if (args[i] == "--dry-run")
                {
                    Console.WriteLine("[Dry-run mode - preview only]");
                    string eventsPath = Path.Combine(recDir, "events.jsonl");
                    string[] evtLines = File.ReadAllLines(eventsPath, Encoding.UTF8);
                    ReplayEngine engine = new ReplayEngine(recDir);
                    List<ReplayEngine.ReplayEvent> events = engine.ParseEvents(evtLines);
                    foreach (ReplayEngine.ReplayEvent evt in events)
                    {
                        string actionStr;
                        if (evt.Action == "click")
                            actionStr = "Click (" + evt.X + "," + evt.Y + ")";
                        else
                            actionStr = "Key: " + evt.Detail;
                        Console.WriteLine("  Step " + evt.Step + ": " + actionStr +
                            " [" + evt.WindowTitle + "]");
                    }
                    Console.WriteLine("Total: " + events.Count + " events");
                    return;
                }
            }

            bool success = replayer.Replay();
            Environment.Exit(success ? 0 : 1);
        }
    }
}