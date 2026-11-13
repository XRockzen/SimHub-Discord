using GameReaderCommon;
using SimHub.Plugins;
using System;
using System.Windows.Media;

namespace User.DiscordControl
{
    [PluginDescription("Control Discord volume, mute, mic, deafen, PTT — mappable to button box via SimHub actions")]
    [PluginAuthor("Amaury")]
    [PluginName("Discord Control")]
    public class DiscordControlPlugin : IPlugin, IDataPlugin, IWPFSettingsV2
    {
        internal DiscordControlSettings Settings;
        private  DiscordAudioController _audio;
        private  int _frameCounter;
        private  readonly object _saveLock = new object();

        internal DiscordRpcClient Rpc      { get; private set; }
        internal DiscordRpcStore  RpcStore { get; private set; }

        public PluginManager PluginManager { get; set; }
        public ImageSource   PictureIcon   { get { return PluginIcon.Image; } }
        public string        LeftMenuTitle { get { return "Discord Control"; } }

        public void Init(PluginManager pluginManager)
        {
            SimHub.Logging.Current.Info("[DiscordControl] Starting " + PluginUpdater.Format(PluginUpdater.CurrentVersion));

            PluginUpdater.CleanupPrevious();
            PluginUpdater.CheckInBackground();

            Settings = this.ReadCommonSettings<DiscordControlSettings>(
                "GeneralSettings", () => new DiscordControlSettings());

            _audio = new DiscordAudioController();

            // Propriétés (dashboards, overlays)
            this.AttachDelegate("DiscordVolume",    () => _audio.VolumePercent);
            this.AttachDelegate("DiscordIsMuted",   () => _audio.IsMuted);
            this.AttachDelegate("DiscordIsRunning", () => _audio.IsDiscordRunning);

            // Volume de l'application Discord dans le mixeur Windows (pas le volume global)
            this.AddAction("DiscordVolumeUp", (a, b) =>
            {
                _audio.AdjustVolume(Settings.VolumeStep / 100f);
                SimHub.Logging.Current.Info(string.Format("[DiscordControl] Volume → {0}%", _audio.VolumePercent));
            });

            this.AddAction("DiscordVolumeDown", (a, b) =>
            {
                _audio.AdjustVolume(-Settings.VolumeStep / 100f);
                SimHub.Logging.Current.Info(string.Format("[DiscordControl] Volume → {0}%", _audio.VolumePercent));
            });

            this.AddAction("DiscordAppMuteToggle", (a, b) =>
            {
                _audio.ToggleMute();
                SimHub.Logging.Current.Info(string.Format("[DiscordControl] App mute → {0}", _audio.IsMuted));
            });

            this.AddAction("DiscordVolumeMax", (a, b) => _audio.SetVolume(1.0f));
            this.AddAction("DiscordVolumeMin", (a, b) => _audio.SetVolume(0.0f));

            // Raccourcis globaux : le même raccourci doit être configuré dans Discord
            this.AddAction("DiscordMicToggle", (a, b) =>
            {
                KeyboardHelper.SendKeystroke(Settings.MuteKey, Settings.MuteCtrl,
                    Settings.MuteShift, Settings.MuteAlt);
                SimHub.Logging.Current.Info("[DiscordControl] Mic toggle sent");
            });

            this.AddAction("DiscordDeafenToggle", (a, b) =>
            {
                KeyboardHelper.SendKeystroke(Settings.DeafenKey, Settings.DeafenCtrl,
                    Settings.DeafenShift, Settings.DeafenAlt);
                SimHub.Logging.Current.Info("[DiscordControl] Deafen toggle sent");
            });

            this.AddAction("DiscordOverlayToggle", (a, b) =>
                KeyboardHelper.SendKeystroke(Settings.OverlayKey, Settings.OverlayCtrl,
                    Settings.OverlayShift, Settings.OverlayAlt));

            this.AddAction("DiscordCameraToggle", (a, b) =>
                KeyboardHelper.SendKeystroke(Settings.CameraKey, Settings.CameraCtrl,
                    Settings.CameraShift, Settings.CameraAlt));

            this.AddAction("DiscordScreenShareToggle", (a, b) =>
                KeyboardHelper.SendKeystroke(Settings.ScreenShareKey, Settings.ScreenShareCtrl,
                    Settings.ScreenShareShift, Settings.ScreenShareAlt));

            // Push-to-talk : la touche reste enfoncée tant que le bouton est maintenu
            this.AddInputMapping("DiscordPushToTalk",
                inputPressed: (a, b) => KeyboardHelper.KeyDown(Settings.PttKey, Settings.PttCtrl,
                    Settings.PttShift, Settings.PttAlt),
                inputReleased: (a, b) => KeyboardHelper.KeyUp(Settings.PttKey, Settings.PttCtrl,
                    Settings.PttShift, Settings.PttAlt));

            _audio.Refresh(true);

            InitRpc();
        }

        // ─── API locale de Discord (RPC) ──────────────────────────────────

        private void InitRpc()
        {
            RpcStore = new DiscordRpcStore(this);
            Rpc = new DiscordRpcClient(RpcStore, line => SimHub.Logging.Current.Info("[DiscordControl] RPC : " + line));

            this.AttachDelegate("DiscordRpcConnected",    () => Rpc.IsReady);
            this.AttachDelegate("DiscordRpcMicMuted",     () => Rpc.MicMuted);
            this.AttachDelegate("DiscordRpcDeafened",     () => Rpc.Deafened);
            this.AttachDelegate("DiscordRpcOutputVolume", () => (int)Math.Round(Rpc.OutputVolume));
            this.AttachDelegate("DiscordRpcInputVolume",  () => (int)Math.Round(Rpc.InputVolume));
            this.AttachDelegate("DiscordRpcVoiceChannel", () => Rpc.VoiceChannel);

            AddRpcAction("DiscordRpcMicToggle",          "mic");
            AddRpcAction("DiscordRpcDeafenToggle",       "deafen");
            AddRpcAction("DiscordRpcOutputVolumeUp",     "out+");
            AddRpcAction("DiscordRpcOutputVolumeDown",   "out-");
            AddRpcAction("DiscordRpcInputVolumeUp",      "in+");
            AddRpcAction("DiscordRpcInputVolumeDown",    "in-");
            AddRpcAction("DiscordRpcHangUp",             "hangup");
            AddRpcAction("DiscordRpcCameraToggle",       "video");
            AddRpcAction("DiscordRpcScreenShareToggle",  "share");
            AddRpcAction("DiscordRpcSoundboardPlay",     "sound");

            if (Settings.RpcAutoConnect && !string.IsNullOrEmpty(RpcStore.AccessToken))
                Rpc.Connect(false);
        }

        private void AddRpcAction(string name, string command)
        {
            this.AddAction(name, (a, b) => RunRpcCommand(command));
        }

        internal void RunRpcCommand(string command)
        {
            switch (command)
            {
                case "mic":    Rpc.ToggleMic(); break;
                case "deafen": Rpc.ToggleDeafen(); break;
                case "out+":   Rpc.AdjustOutputVolume(Settings.VolumeStep); break;
                case "out-":   Rpc.AdjustOutputVolume(-Settings.VolumeStep); break;
                case "in+":    Rpc.AdjustInputVolume(Settings.VolumeStep); break;
                case "in-":    Rpc.AdjustInputVolume(-Settings.VolumeStep); break;
                case "hangup": Rpc.HangUp(); break;
                case "video":  Rpc.ToggleVideo(); break;
                case "share":  Rpc.ToggleScreenShare(); break;
                case "sound":
                    if (!string.IsNullOrEmpty(Settings.RpcSoundId))
                        Rpc.PlaySound(Settings.RpcSoundGuildId, Settings.RpcSoundId);
                    break;
            }
        }

        internal void SaveSettings()
        {
            lock (_saveLock) this.SaveCommonSettings("GeneralSettings", Settings);
        }

        public void DataUpdate(PluginManager pluginManager, ref GameData data)
        {
            // Rafraîchit l'état audio environ toutes les 60 frames (1 s à 60 fps)
            _frameCounter++;
            if (_frameCounter % 60 == 0)
                _audio.Refresh(false);
        }

        public void End(PluginManager pluginManager)
        {
            if (Rpc != null) Rpc.Stop();
            SaveSettings();
        }

        public System.Windows.Controls.Control GetWPFSettingsControl(PluginManager pluginManager)
        {
            return new DiscordControlControl(this);
        }
    }
}
