using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace SpotifyAdBlock
{
    public partial class frmMain : Form
    {
        public static bool HideForm = true;
        public static int ProcessID;
        public static float OriginalVolume = 0f;

        [DllImport("user32.dll")]
        internal static extern int PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        public frmMain()
        {
            InitializeComponent();
        }


        #region Form Events
        private void frmMain_Load(object sender, EventArgs e)
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT || Environment.OSVersion.Version.Major < 6)
            {
                MessageBox.Show("Sorry, this app only supports Windows 7 and above.", "Spotify Ad Blocker",
                                MessageBoxButtons.OK, MessageBoxIcon.Stop);
                Application.Exit();
            }
            //this.Hide();
            timerScanAd.Enabled = true;
        }

        private void notifyIcon_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            this.Show();
        }

        private void closeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void resetVolumeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SoundControl.SetApplicationVolume((uint)ProcessID, OriginalVolume);
            notifyIcon.ShowBalloonTip(1000, "Spotify Ad Blocker", "Resetting volume to original...", ToolTipIcon.None);
        }

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason != CloseReason.FormOwnerClosing && e.CloseReason != CloseReason.UserClosing) return;
            this.Hide();
            e.Cancel = true;
        }

        #endregion

        private void timerScanAd_Tick(object sender, EventArgs e)
        {
            if (HideForm)
            {
                HideForm = false;
                this.Hide();
            }
            lblStatus.Text = "Scanning Spotify...";
            var processes = Process.GetProcessesByName("spotify");
            if (processes.Length <= 0) return;
            if (ProcessID != processes[0].Id)
            {
                ProcessID = processes[0].Id;
            }
            float? volume = SoundControl.GetApplicationVolume((uint)ProcessID);
            if (volume == null) return;
            bool mute = volume <= (1f/100f);
            var title = processes[0].MainWindowTitle;
            var data = title.Split(new[] {" - "}, StringSplitOptions.RemoveEmptyEntries);
            if (data.Length <= 1) return;
            var songdata = string.Join(" - ", data, 1, data.Length - 1).Split(new[] { " – " }, StringSplitOptions.RemoveEmptyEntries);
            var adstatus = CheckSongIsAd(songdata[0], songdata[1]);
            if (adstatus && !mute)
            {
                OriginalVolume = (float)volume;
                SoundControl.SetApplicationVolume((uint)ProcessID, 1f / 1000f);
                //Force a play after volume is set as Spotify will stop the ad.
                PostMessage(processes[0].MainWindowHandle, 0x319, IntPtr.Zero, new IntPtr(0xE0000L));
                lblStatus.Text = "Ad detected, muting.";
                notifyIcon.ShowBalloonTip(1000, "Spotify Ad Blocker", "Muting Spotify, ad detected.", ToolTipIcon.None);
            }
            else if (!adstatus && mute)
            {
                if (OriginalVolume == 0f)
                    OriginalVolume = 1f;
                SoundControl.SetApplicationVolume((uint)ProcessID, OriginalVolume);
                lblStatus.Text = "Ad not detected.";
                notifyIcon.ShowBalloonTip(1000, "Spotify Ad Blocker", "Ad no longer detected, unmuting Spotify.", ToolTipIcon.None);
            }
        }

        private bool CheckSongIsAd(string artist, string song)
        {
            //I'm assuming since we are blocking ads on free accounts, there will be an internet connection.
            var wc = new WebClient() {Proxy = null};
            try
            {
                var uri = ("http://ws.spotify.com/search/1/track.json?q=" +
                           HttpUtility.UrlEncode(String.Format("{0} {1}",
                                                               artist, song)));
                var response = Regex.Unescape(wc.DownloadString(uri));
                return
                    !(response.Contains(string.Format("\"name\": \"{0}\"", artist)) &&
                      response.Contains(string.Format("\"name\": \"{0}\"", song)));
            }
            catch (Exception e)
            {
                return false;
            }
        }
    }

    public class SoundControl
    {
        [ComImport]
        [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        internal class MMDeviceEnumerator
        {
        }

        internal enum EDataFlow
        {
            eRender,
            eCapture,
            eAll,
            EDataFlow_enum_count
        }

        internal enum ERole
        {
            eConsole,
            eMultimedia,
            eCommunications,
            ERole_enum_count
        }

        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IMMDeviceEnumerator
        {
            int NotImpl1();

            [PreserveSig]
            int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice ppDevice);

            // the rest is not implemented
        }

        [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IMMDevice
        {
            [PreserveSig]
            int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);

            // the rest is not implemented
        }

        [Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IAudioSessionManager2
        {
            int NotImpl1();
            int NotImpl2();

            [PreserveSig]
            int GetSessionEnumerator(out IAudioSessionEnumerator SessionEnum);

            // the rest is not implemented
        }

        [Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IAudioSessionEnumerator
        {
            [PreserveSig]
            int GetCount(out int SessionCount);

            [PreserveSig]
            int GetSession(int SessionCount, out IAudioSessionControl Session);
        }

        [Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IAudioSessionControl
        {
            int NotImpl1();

            [PreserveSig]
            int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);

            // the rest is not implemented
        }

        [Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface ISimpleAudioVolume
        {
            [PreserveSig]
            int SetMasterVolume(float fLevel, ref Guid EventContext);

            [PreserveSig]
            int GetMasterVolume(out float pfLevel);

            [PreserveSig]
            int SetMute(bool bMute, ref Guid EventContext);

            [PreserveSig]
            int GetMute(out bool pbMute);
        }

        public static IEnumerable<string> EnumerateApplications()
        {
            // get the speakers (1st render + multimedia) device
            IMMDeviceEnumerator deviceEnumerator = (IMMDeviceEnumerator)(new MMDeviceEnumerator());
            IMMDevice speakers;
            deviceEnumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out speakers);

            // activate the session manager. we need the enumerator
            Guid IID_IAudioSessionManager2 = typeof(IAudioSessionManager2).GUID;
            object o;
            speakers.Activate(ref IID_IAudioSessionManager2, 0, IntPtr.Zero, out o);
            IAudioSessionManager2 mgr = (IAudioSessionManager2)o;

            // enumerate sessions for on this device
            IAudioSessionEnumerator sessionEnumerator;
            mgr.GetSessionEnumerator(out sessionEnumerator);
            int count;
            sessionEnumerator.GetCount(out count);

            for (int i = 0; i < count; i++)
            {
                IAudioSessionControl ctl;
                IAudioSessionControl2 ctl2;

                sessionEnumerator.GetSession(i, out ctl);

                ctl2 = ctl as IAudioSessionControl2;

                string dn;

                uint pid = 0;

                string sout1 = "";
                string sout2 = "";

                if (ctl2 != null)
                {
                    ctl2.GetSessionIdentifier(out sout1);
                    ctl2.GetProcessId(out pid);
                    ctl2.GetSessionInstanceIdentifier(out sout2);
                }

                //ctl.GetDisplayName(out dn);
                //  ctl2.GetProcessId(out pid);

                //yield return pid.ToString();
                yield return pid.ToString() + ": " + sout1 + " :: " + sout2;

                if (ctl != null)
                    Marshal.ReleaseComObject(ctl);

                if (ctl2 != null)
                    Marshal.ReleaseComObject(ctl2);
            }

            Marshal.ReleaseComObject(sessionEnumerator);
            Marshal.ReleaseComObject(mgr);
            Marshal.ReleaseComObject(speakers);
            Marshal.ReleaseComObject(deviceEnumerator);
        }

        public enum AudioSessionState
        {
            AudioSessionStateInactive = 0,
            AudioSessionStateActive = 1,
            AudioSessionStateExpired = 2
        }

        public enum AudioSessionDisconnectReason
        {
            DisconnectReasonDeviceRemoval = 0,
            DisconnectReasonServerShutdown = (DisconnectReasonDeviceRemoval + 1),
            DisconnectReasonFormatChanged = (DisconnectReasonServerShutdown + 1),
            DisconnectReasonSessionLogoff = (DisconnectReasonFormatChanged + 1),
            DisconnectReasonSessionDisconnected = (DisconnectReasonSessionLogoff + 1),
            DisconnectReasonExclusiveModeOverride = (DisconnectReasonSessionDisconnected + 1)
        }

        [Guid("24918ACC-64B3-37C1-8CA9-74A66E9957A8"),
    InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IAudioSessionEvents
        {
            [PreserveSig]
            int OnDisplayNameChanged([MarshalAs(UnmanagedType.LPWStr)] string NewDisplayName, Guid EventContext);
            [PreserveSig]
            int OnIconPathChanged([MarshalAs(UnmanagedType.LPWStr)] string NewIconPath, Guid EventContext);
            [PreserveSig]
            int OnSimpleVolumeChanged(float NewVolume, bool newMute, Guid EventContext);
            [PreserveSig]
            int OnChannelVolumeChanged(UInt32 ChannelCount, IntPtr NewChannelVolumeArray, UInt32 ChangedChannel, Guid EventContext);
            [PreserveSig]
            int OnGroupingParamChanged(Guid NewGroupingParam, Guid EventContext);
            [PreserveSig]
            int OnStateChanged(AudioSessionState NewState);
            [PreserveSig]
            int OnSessionDisconnected(AudioSessionDisconnectReason DisconnectReason);
        }

        [Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IAudioSessionControl2
        {
            [PreserveSig]
            int GetState(out AudioSessionState state);
            [PreserveSig]
            int GetDisplayName([Out(), MarshalAs(UnmanagedType.LPWStr)] out string name);
            [PreserveSig]
            int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string value, Guid EventContext);
            [PreserveSig]
            int GetIconPath([Out(), MarshalAs(UnmanagedType.LPWStr)] out string Path);
            [PreserveSig]
            int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string Value, Guid EventContext);
            [PreserveSig]
            int GetGroupingParam(out Guid GroupingParam);
            [PreserveSig]
            int SetGroupingParam(Guid Override, Guid Eventcontext);
            [PreserveSig]
            int RegisterAudioSessionNotification(IAudioSessionEvents NewNotifications);
            [PreserveSig]
            int UnregisterAudioSessionNotification(IAudioSessionEvents NewNotifications);
            [PreserveSig]
            int GetSessionIdentifier([Out(), MarshalAs(UnmanagedType.LPWStr)] out string retVal);
            [PreserveSig]
            int GetSessionInstanceIdentifier([Out(), MarshalAs(UnmanagedType.LPWStr)] out string retVal);
            [PreserveSig]
            int GetProcessId(out UInt32 retvVal);
            [PreserveSig]
            int IsSystemSoundsSession();
            [PreserveSig]
            int SetDuckingPreference(bool optOut);
        }

        private static ISimpleAudioVolume GetVolumeObject(uint name)
        {
            // get the speakers (1st render + multimedia) device
            IMMDeviceEnumerator deviceEnumerator = (IMMDeviceEnumerator)(new MMDeviceEnumerator());
            IMMDevice speakers;
            deviceEnumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out speakers);

            // activate the session manager. we need the enumerator
            Guid IID_IAudioSessionManager2 = typeof(IAudioSessionManager2).GUID;
            object o;
            speakers.Activate(ref IID_IAudioSessionManager2, 0, IntPtr.Zero, out o);
            IAudioSessionManager2 mgr = (IAudioSessionManager2)o;

            // enumerate sessions for on this device
            IAudioSessionEnumerator sessionEnumerator;
            mgr.GetSessionEnumerator(out sessionEnumerator);
            int count;
            sessionEnumerator.GetCount(out count);

            // search for an audio session with the required name
            // NOTE: we could also use the process id instead of the app name (with IAudioSessionControl2)
            ISimpleAudioVolume volumeControl = null;
            for (int i = 0; i < count; i++)
            {
                IAudioSessionControl ctl1;
                sessionEnumerator.GetSession(i, out ctl1);
                var ctl = ctl1 as IAudioSessionControl2;
                uint id;
                ctl.GetProcessId(out id);
                //string dn;
                //ctl.GetDisplayName(out dn);
                //if (string.Compare(name, dn, StringComparison.OrdinalIgnoreCase) == 0)
                if (id == name)
                {
                    volumeControl = ctl as ISimpleAudioVolume;
                    break;
                }
                Marshal.ReleaseComObject(ctl);
            }
            Marshal.ReleaseComObject(sessionEnumerator);
            Marshal.ReleaseComObject(mgr);
            Marshal.ReleaseComObject(speakers);
            Marshal.ReleaseComObject(deviceEnumerator);
            return volumeControl;
        }

        public static void SetApplicationMute(uint name, bool mute)
        {
            ISimpleAudioVolume volume = GetVolumeObject(name);
            if (volume == null)
                return;

            Guid guid = Guid.Empty;
            volume.SetMute(mute, ref guid);
        }

        public static bool? GetApplicationMute(uint name)
        {
            ISimpleAudioVolume volume = GetVolumeObject(name);
            if (volume == null)
                return null;

            bool mute;
            volume.GetMute(out mute);
            return mute;
        }

        public static void SetApplicationVolume(uint name, float volum)
        {
            ISimpleAudioVolume volume = GetVolumeObject(name);
            if (volume == null)
                return;

            Guid guid = Guid.Empty;
            volume.SetMasterVolume(volum, ref guid);
        }

        public static float? GetApplicationVolume(uint name)
        {
            ISimpleAudioVolume volume = GetVolumeObject(name);
            if (volume == null)
                return null;

            float mute;
            volume.GetMasterVolume(out mute);
            return mute;
        }
    }
}