#nullable enable
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;

namespace MouseDroid
{
    /// <summary>
    /// - Min/Max define the target window
    /// - HardMax sets the visual scale maximum
    /// </summary>
    public class TimingZone
    {
        public double Min { get; set; }
        public double Max { get; set; }
        public double HardMax { get; set; }
    }

    /// <summary>
    /// Configurable keyboard controls for the speedrun timer
    /// </summary>
    public class KeyBindings
    {
        public Keys StartKey { get; set; } = Keys.C;
        public Keys SplitKey { get; set; } = Keys.Escape;
        public Keys EndKey { get; set; } = Keys.E;
    }

    public class TimingConfig
    {
        public TimingZone Timer1 { get; set; } = new TimingZone();
        public TimingZone Timer2 { get; set; } = new TimingZone();
        public TimingZone Timer3 { get; set; } = new TimingZone();
        public KeyBindings Keys { get; set; } = new KeyBindings();
        public bool DarkMode { get; set; } = true;
    }

    /// <summary>
    /// Visual progress bar showing elapsed time with target window highlight.
    /// Displays timing progress with a red bar and gold target zone.
    /// </summary>
    public class TimingBar : Control
    {
        /// <summary>Current elapsed time to display</summary>
        public double ElapsedMs { get; set; } = 0;
        /// <summary>Maximum time for scaling the bar width</summary>
        public double MaxMs { get; set; } = 1000;
        /// <summary>Start of the target timing window</summary>
        public double TargetMinMs { get; set; } = 200;
        /// <summary>End of the target timing window</summary>
        public double TargetMaxMs { get; set; } = 300;

        public bool DarkMode { get; set; } = true;

        public TimingBar()
        {
            DoubleBuffered = true;
            Height = 20;
            Dock = DockStyle.Top;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            int width = Width;
            int height = Height;

            g.Clear(DarkMode ? Color.FromArgb(30, 30, 30) : Color.LightGray);
            double denom = MaxMs <= 0 ? 1.0 : MaxMs;
            int tMin = (int)Math.Round(Math.Max(0.0, Math.Min(TargetMinMs, denom)) / denom * width);
            int tMax = (int)Math.Round(Math.Max(0.0, Math.Min(TargetMaxMs, denom)) / denom * width);
            using (Brush targetBrush = new SolidBrush(Color.FromArgb(150, Color.Gold)))
            {
                g.FillRectangle(targetBrush, tMin, 0, Math.Max(1, tMax - tMin), height);
            }
            double elapsedClamped = Math.Max(0.0, Math.Min(ElapsedMs, denom));
            int fillWidth = (int)Math.Round((elapsedClamped / denom) * width);
            using (Brush fillBrush = new SolidBrush(Color.Red))
            {
                g.FillRectangle(fillBrush, 0, 0, Math.Min(width, fillWidth), height);
            }

            base.OnPaint(e);
        }
    }

    public class TimingForm : Form
    {
        private Label label1, label2, label3;
        private TimingBar bar1, bar2, bar3;
        private System.Windows.Forms.Timer updateTimer;
        private Button configButton;

        // State machine for tracking which timer is currently active
    private enum State { Idle, Timer1, Timer2, Timer3 }
    private State currentState = State.Idle;

    private readonly Stopwatch stopwatch = new Stopwatch();
    // Timing markers for each segment:
    // tCReleasedMs: When C key is released to start Timer1
    // tEscape1Ms: When Escape is pressed to end Timer1/start Timer2
    // tEscape2Ms: When Escape is pressed to end Timer2/start Timer3
    // tEMs: When E is pressed to end Timer3
    // stateStartMs: Start time of current timing state
    private long tCReleasedMs, tEscape1Ms, tEscape2Ms, tEMs, stateStartMs;
        private TimingConfig config;

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private static IntPtr hookID = IntPtr.Zero;
        private LowLevelKeyboardProc? hookCallback;

        public TimingForm()
        {
            Text = "Mouse Droid";
            Width = 800;
            Height = 400;
            Font = new Font("Consolas", 12, FontStyle.Bold);

            config = LoadConfig();

            if (config.DarkMode)
            {
                BackColor = Color.FromArgb(18, 18, 18);
                ForeColor = Color.White;
            }
            else
            {
                BackColor = Color.White;
                ForeColor = Color.Black;
            }

            label1 = new Label { Text = "Timer 1:", Dock = DockStyle.Top, Height = 25, ForeColor = ForeColor, BackColor = BackColor };
            bar1 = CreateBar(config.Timer1);

            label2 = new Label { Text = "Timer 2:", Dock = DockStyle.Top, Height = 25, ForeColor = ForeColor, BackColor = BackColor };
            bar2 = CreateBar(config.Timer2);

            label3 = new Label { Text = "Timer 3:", Dock = DockStyle.Top, Height = 25, ForeColor = ForeColor, BackColor = BackColor };
            bar3 = CreateBar(config.Timer3);

            configButton = new Button
            {
                Text = "Configure Timers",
                Dock = DockStyle.Top,
                Height = 30,
                BackColor = config.DarkMode ? Color.FromArgb(40, 40, 40) : Color.LightGray,
                ForeColor = config.DarkMode ? Color.White : Color.Black
            };
            configButton.Click += (s, e) => ShowConfigDialog();

            Controls.Add(configButton);
            Controls.Add(bar3);
            Controls.Add(label3);
            Controls.Add(bar2);
            Controls.Add(label2);
            Controls.Add(bar1);
            Controls.Add(label1);

            updateTimer = new System.Windows.Forms.Timer { Interval = 10 };
            updateTimer.Tick += UpdateTimer_Tick;
            updateTimer.Start();

            hookCallback = HookCallback;
            hookID = SetHook(hookCallback);
            stopwatch.Start();
            FormClosed += (s, e) => UnhookWindowsHookEx(hookID);
        }

        private TimingBar CreateBar(TimingZone zone)
        {
            return new TimingBar
            {
                MaxMs = zone.HardMax,
                TargetMinMs = zone.Min,
                TargetMaxMs = zone.Max,
                DarkMode = config.DarkMode
            };
        }

        private void ShowConfigDialog()
        {
            var dlg = new ConfigDialog(config);
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                config = dlg.UpdatedConfig;
                SaveConfig(config);
                ResetBarsOnly();
                Application.Restart();
            }
        }

        /// <summary>
        /// Updates the active timer display every 10ms, showing current elapsed time
        /// in both the numeric label and visual progress bar
        /// </summary>
        private void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            double elapsed = stopwatch.ElapsedMilliseconds - stateStartMs;
            elapsed = Math.Round(elapsed);

            // Auto-reset if current timer exceeds 10 minutes
            const double timeoutMs = 600000;
            if (currentState != State.Idle && elapsed > timeoutMs)
            {
                ResetBarsOnly();
                return;
            }

            switch (currentState)
            {
                case State.Timer1:
                    label1.Text = $"Timer 1: {elapsed:F0} ms";
                    bar1.ElapsedMs = elapsed;
                    bar1.Invalidate();
                    break;
                case State.Timer2:
                    label2.Text = $"Timer 2: {elapsed:F0} ms";
                    bar2.ElapsedMs = elapsed;
                    bar2.Invalidate();
                    break;
                case State.Timer3:
                    label3.Text = $"Timer 3: {elapsed:F0} ms";
                    bar3.ElapsedMs = elapsed;
                    bar3.Invalidate();
                    break;
            }
        }

        private void ResetBarsOnly()
        {
            currentState = State.Idle;
            bar1.ElapsedMs = bar2.ElapsedMs = bar3.ElapsedMs = 0;
            bar1.Invalidate();
            bar2.Invalidate();
            bar3.Invalidate();

            label1.Text = "Timer 1:";
            label2.Text = "Timer 2:";
            label3.Text = "Timer 3:";
        }

        /// <summary>
        /// Loads timing thresholds, key bindings, and theme settings from config.json.
        /// Creates default configuration if file doesn't exist or is invalid.
        /// Default timings are set for a specific speedrun sequence:
        /// - Timer1: 40-60ms (90ms max) - Quick initial action
        /// - Timer2: 1060-1100ms (1500ms max) - Delayed second action
        /// - Timer3: 230-250ms (500ms max) - Final action
        /// </summary>
        private TimingConfig LoadConfig()
        {
            const string path = "config.json";
            if (!File.Exists(path))
            {
                return new TimingConfig
                {
                    Timer1 = new TimingZone { Min = 40, Max = 60, HardMax = 90 },
                    Timer2 = new TimingZone { Min = 1060, Max = 1100, HardMax = 1500 },
                    Timer3 = new TimingZone { Min = 230, Max = 250, HardMax = 500 }
                };
            }

            try
            {
                var json = File.ReadAllText(path);
                var cfg = JsonSerializer.Deserialize<TimingConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (cfg == null)
                {
                    return new TimingConfig();
                }

                cfg.Timer1 ??= new TimingZone { Min = 40, Max = 60, HardMax = 90 };
                cfg.Timer2 ??= new TimingZone { Min = 1060, Max = 1100, HardMax = 1500 };
                cfg.Timer3 ??= new TimingZone { Min = 230, Max = 250, HardMax = 500 };

                return cfg;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error reading config. Using defaults.\n" + ex.Message);
                return new TimingConfig();
            }
        }

        private void SaveConfig(TimingConfig cfg)
        {
            File.WriteAllText("config.json", JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true }));
        }

        /// <summary>
        /// Global keyboard hook callback that intercepts keyboard events
        /// system-wide, allowing the timer to work even when not focused.
        /// Safely marshals key events to the UI thread for processing.
        /// </summary>
        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && lParam != IntPtr.Zero)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                Keys key = (Keys)vkCode;

                try
                {
                    BeginInvoke(new Action(() => ProcessKey(wParam, key)));
                }
                catch
                {
                }
            }
            return CallNextHookEx(hookID, nCode, wParam, lParam);
        }

        /// <summary>
        /// Core timing logic for handling keyboard events:
        /// - StartKey (C): Press to reset, release to start Timer1
        /// - SplitKey (Esc): End current timer, start next
        /// - EndKey (E): End Timer3 and return to idle
        /// 
        /// Each timer tracks elapsed time from the end of the previous timer
        /// </summary>
        private void ProcessKey(IntPtr wParam, Keys key)
        {
            if (wParam == (IntPtr)WM_KEYDOWN)
            {
                if (key == config.Keys.StartKey)
                {
                    ResetBarsOnly();
                }
                else if (key == config.Keys.SplitKey)
                {
                    if (currentState == State.Timer1)
                    {
                        tEscape1Ms = stopwatch.ElapsedMilliseconds;
                        double finalMs = (double)(tEscape1Ms - tCReleasedMs);
                        finalMs = Math.Round(finalMs);
                        label1.Text = $"Timer 1: {finalMs:F0} ms";
                        bar1.ElapsedMs = finalMs;
                        bar1.Invalidate();

                        currentState = State.Timer2;
                        stateStartMs = stopwatch.ElapsedMilliseconds;

                        bar2.ElapsedMs = 0;
                        bar2.Invalidate();
                        bar3.ElapsedMs = 0;
                        bar3.Invalidate();
                    }
                    else if (currentState == State.Timer2)
                    {
                        tEscape2Ms = stopwatch.ElapsedMilliseconds;
                        double finalMs = (double)(tEscape2Ms - tEscape1Ms);
                        finalMs = Math.Round(finalMs);
                        label2.Text = $"Timer 2: {finalMs:F0} ms";
                        bar2.ElapsedMs = finalMs;
                        bar2.Invalidate();

                        currentState = State.Timer3;
                        stateStartMs = stopwatch.ElapsedMilliseconds;

                        bar3.ElapsedMs = 0;
                        bar3.Invalidate();
                    }
                }
                else if (key == config.Keys.EndKey && currentState == State.Timer3)
                {
                    tEMs = stopwatch.ElapsedMilliseconds;
                    double finalMs = (double)(tEMs - tEscape2Ms);
                    finalMs = Math.Round(finalMs);
                    label3.Text = $"Timer 3: {finalMs:F0} ms";
                    bar3.ElapsedMs = finalMs;
                    bar3.Invalidate();

                    currentState = State.Idle;
                }
            }
            else if (wParam == (IntPtr)WM_KEYUP && key == config.Keys.StartKey && currentState == State.Idle)
            {
                tCReleasedMs = stopwatch.ElapsedMilliseconds;

                bar1.ElapsedMs = bar2.ElapsedMs = bar3.ElapsedMs = 0;
                bar1.Invalidate();
                bar2.Invalidate();
                bar3.Invalidate();

                currentState = State.Timer1;
                stateStartMs = stopwatch.ElapsedMilliseconds;
            }
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process cur = Process.GetCurrentProcess())
            using (ProcessModule module = cur.MainModule!)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(module.ModuleName), 0);
            }
        }

        [DllImport("user32.dll")] private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)] private static extern IntPtr GetModuleHandle(string lpModuleName);

        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.Run(new TimingForm());
        }
    }
}
