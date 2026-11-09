using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace User.DiscordControl
{
    public partial class DiscordControlControl : UserControl
    {
        private readonly DiscordControlPlugin _plugin;
        private bool _loading;

        private static readonly List<KeyItem> Keys = new List<KeyItem>();

        static DiscordControlControl()
        {
            Keys.Add(new KeyItem { Name = "(aucune)", Code = 0 });
            for (int i = 0x70; i <= 0x87; i++)
                Keys.Add(new KeyItem { Name = "F" + (i - 0x6F), Code = (byte)i });
            for (char c = 'A'; c <= 'Z'; c++)
                Keys.Add(new KeyItem { Name = c.ToString(), Code = (byte)c });
            for (int i = 0; i <= 9; i++)
                Keys.Add(new KeyItem { Name = "Num" + i, Code = (byte)(0x60 + i) });
        }

        public DiscordControlControl(DiscordControlPlugin plugin)
        {
            InitializeComponent();
            _plugin = plugin;
            RpcPanel.Attach(plugin);
            TxtVersion.Text = "Discord Control " + PluginUpdater.Format(PluginUpdater.CurrentVersion);
            Loaded += (s, e) =>
            {
                LoadSettings();
                PluginUpdater.Changed += OnUpdateChanged;
                RefreshUpdateBanner();
            };
            Unloaded += (s, e) => PluginUpdater.Changed -= OnUpdateChanged;
        }

        // ─── Mise à jour ──────────────────────────────────────────────────

        private bool _manualCheck;

        private void OnUpdateChanged()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                RefreshUpdateBanner();
                if (!_manualCheck) return;
                _manualCheck = false;
                if (PluginUpdater.LastError != null)
                    TxtUpdateStatus.Text = "Vérification impossible : " + PluginUpdater.LastError;
                else if (PluginUpdater.Available == null)
                    TxtUpdateStatus.Text = "Tu as la dernière version (" + PluginUpdater.Format(PluginUpdater.CurrentVersion) + ")";
                else
                    TxtUpdateStatus.Text = "";
            }));
        }

        private void RefreshUpdateBanner()
        {
            if (PluginUpdater.PendingRestartVersion != null)
            {
                UpdateBanner.Visibility = Visibility.Visible;
                UpdateButtons.Visibility = Visibility.Collapsed;
                TxtUpdateTitle.Text = "✔ Mise à jour " + PluginUpdater.Format(PluginUpdater.PendingRestartVersion) + " installée";
                TxtUpdateNotes.Text = "Redémarre SimHub pour l'activer.";
                return;
            }

            var info = PluginUpdater.Available;
            if (info == null || PluginUpdater.Dismissed)
            {
                UpdateBanner.Visibility = Visibility.Collapsed;
                return;
            }

            UpdateBanner.Visibility = Visibility.Visible;
            UpdateButtons.Visibility = Visibility.Visible;
            BtnUpdateInstall.IsEnabled = true;
            TxtUpdateTitle.Text = string.Format("⬆ Mise à jour {0} disponible  (installée : {1})",
                PluginUpdater.Format(info.Version), PluginUpdater.Format(PluginUpdater.CurrentVersion));
            TxtUpdateNotes.Text = string.IsNullOrEmpty(info.Notes) ? "" : info.Notes.Replace("\r", "");
        }

        private void BtnUpdateInstall_Click(object sender, RoutedEventArgs e)
        {
            BtnUpdateInstall.IsEnabled = false;
            TxtUpdateTitle.Text = "Téléchargement et vérification…";
            PluginUpdater.InstallInBackground(error => Dispatcher.BeginInvoke(new Action(() =>
            {
                if (error == null)
                {
                    RefreshUpdateBanner();
                    return;
                }
                BtnUpdateInstall.IsEnabled = true;
                TxtUpdateTitle.Text = "Échec de la mise à jour";
                TxtUpdateNotes.Text = error;
            })));
        }

        private void BtnUpdateNotes_Click(object sender, RoutedEventArgs e)
        {
            var info = PluginUpdater.Available;
            if (info != null && !string.IsNullOrEmpty(info.HtmlUrl))
                try { Process.Start(info.HtmlUrl); } catch (Exception) { }
        }

        private void BtnUpdateLater_Click(object sender, RoutedEventArgs e)
        {
            PluginUpdater.Dismissed = true;
            RefreshUpdateBanner();
        }

        private void LnkCheckUpdate_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _manualCheck = true;
            PluginUpdater.Dismissed = false;
            TxtUpdateStatus.Text = "Recherche de mise à jour…";
            PluginUpdater.CheckInBackground();
        }

        private void LoadSettings()
        {
            _loading = true;

            VolumeStepCombo.Items.Clear();
            int[] steps = { 5, 10, 15, 20, 25 };
            foreach (var step in steps) VolumeStepCombo.Items.Add(step.ToString());

            var s = _plugin.Settings;
            VolumeStepCombo.SelectedItem = s.VolumeStep.ToString();

            InitKeyCombo(MuteKeyCombo,    s.MuteKey);
            InitKeyCombo(DeafenKeyCombo,  s.DeafenKey);
            InitKeyCombo(PttKeyCombo,     s.PttKey);
            InitKeyCombo(OverlayKeyCombo, s.OverlayKey);
            InitKeyCombo(CameraKeyCombo,  s.CameraKey);
            InitKeyCombo(ShareKeyCombo,   s.ScreenShareKey);

            MuteCtrl.IsChecked     = s.MuteCtrl;
            MuteShift.IsChecked    = s.MuteShift;
            MuteAlt.IsChecked      = s.MuteAlt;
            DeafenCtrl.IsChecked   = s.DeafenCtrl;
            DeafenShift.IsChecked  = s.DeafenShift;
            DeafenAlt.IsChecked    = s.DeafenAlt;
            PttCtrl.IsChecked      = s.PttCtrl;
            PttShift.IsChecked     = s.PttShift;
            PttAlt.IsChecked       = s.PttAlt;
            OverlayCtrl.IsChecked  = s.OverlayCtrl;
            OverlayShift.IsChecked = s.OverlayShift;
            OverlayAlt.IsChecked   = s.OverlayAlt;
            CameraCtrl.IsChecked   = s.CameraCtrl;
            CameraShift.IsChecked  = s.CameraShift;
            CameraAlt.IsChecked    = s.CameraAlt;
            ShareCtrl.IsChecked    = s.ScreenShareCtrl;
            ShareShift.IsChecked   = s.ScreenShareShift;
            ShareAlt.IsChecked     = s.ScreenShareAlt;

            _loading = false;
            RefreshStatus();
        }

        private static void InitKeyCombo(ComboBox combo, byte selected)
        {
            combo.Items.Clear();
            int selectedIndex = 0;
            for (int i = 0; i < Keys.Count; i++)
            {
                combo.Items.Add(Keys[i].Name);
                if (Keys[i].Code == selected) selectedIndex = i;
            }
            combo.SelectedIndex = selectedIndex;
        }

        private static byte GetKeyFromCombo(ComboBox combo)
        {
            if (combo.SelectedIndex < 0 || combo.SelectedIndex >= Keys.Count) return 0;
            return Keys[combo.SelectedIndex].Code;
        }

        private void RefreshStatus()
        {
            var diag = new StringBuilder();

            int totalProcs = 0;
            foreach (var name in DiscordAudioController.ProcessNames)
            {
                var procs = Process.GetProcessesByName(name);
                if (procs.Length > 0)
                {
                    diag.AppendLine(string.Format("{0}: {1} processus", name, procs.Length));
                    totalProcs += procs.Length;
                }
            }
            if (totalProcs == 0) diag.AppendLine("Aucun processus Discord trouvé");

            try
            {
                var audio = new DiscordAudioController();
                audio.Refresh(true);

                if (audio.IsDiscordRunning)
                {
                    TxtStatus.Text = "Discord détecté";
                    TxtStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
                    TxtVolume.Text = string.Format("{0}%{1}", audio.VolumePercent,
                        audio.IsMuted ? " (MUET)" : "");
                    diag.AppendLine(string.Format("Session audio trouvée: vol={0}%", audio.VolumePercent));
                }
                else
                {
                    TxtStatus.Text = "Discord non détecté";
                    TxtStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xF3, 0x8B, 0xA8));
                    TxtVolume.Text = "—";
                    diag.AppendLine("Aucune session audio Discord");
                    diag.AppendLine("→ Assurez-vous que Discord joue/a joué du son récemment");
                }
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "Erreur";
                TxtVolume.Text = "—";
                diag.AppendLine("Erreur: " + ex.Message);
            }

            TxtDebug.Text = diag.ToString().TrimEnd();
        }

        private void ApplyAudio(Action<DiscordAudioController> change)
        {
            var audio = new DiscordAudioController();
            audio.Refresh(true);
            change(audio);
            RefreshStatus();
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshStatus();
        }

        private void BtnVolUp_Click(object sender, RoutedEventArgs e)
        {
            ApplyAudio(audio => audio.AdjustVolume(_plugin.Settings.VolumeStep / 100f));
        }

        private void BtnVolDown_Click(object sender, RoutedEventArgs e)
        {
            ApplyAudio(audio => audio.AdjustVolume(-_plugin.Settings.VolumeStep / 100f));
        }

        private void BtnMuteToggle_Click(object sender, RoutedEventArgs e)
        {
            ApplyAudio(audio => audio.ToggleMute());
        }

        private void VolumeStepCombo_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_loading) return;
            int step;
            if (int.TryParse(VolumeStepCombo.SelectedItem as string, out step))
                _plugin.Settings.VolumeStep = step;
        }

        // Deux signatures : le XAML branche ce handler sur les cases à cocher et sur les listes
        private void SaveKeybinds(object sender, RoutedEventArgs e)
        {
            if (!_loading) SaveKeybindsInternal();
        }

        private void SaveKeybinds(object sender, SelectionChangedEventArgs e)
        {
            if (!_loading) SaveKeybindsInternal();
        }

        private void SaveKeybindsInternal()
        {
            var s = _plugin.Settings;

            s.MuteKey   = GetKeyFromCombo(MuteKeyCombo);
            s.MuteCtrl  = MuteCtrl.IsChecked == true;
            s.MuteShift = MuteShift.IsChecked == true;
            s.MuteAlt   = MuteAlt.IsChecked == true;

            s.DeafenKey   = GetKeyFromCombo(DeafenKeyCombo);
            s.DeafenCtrl  = DeafenCtrl.IsChecked == true;
            s.DeafenShift = DeafenShift.IsChecked == true;
            s.DeafenAlt   = DeafenAlt.IsChecked == true;

            s.PttKey   = GetKeyFromCombo(PttKeyCombo);
            s.PttCtrl  = PttCtrl.IsChecked == true;
            s.PttShift = PttShift.IsChecked == true;
            s.PttAlt   = PttAlt.IsChecked == true;

            s.OverlayKey   = GetKeyFromCombo(OverlayKeyCombo);
            s.OverlayCtrl  = OverlayCtrl.IsChecked == true;
            s.OverlayShift = OverlayShift.IsChecked == true;
            s.OverlayAlt   = OverlayAlt.IsChecked == true;

            s.CameraKey   = GetKeyFromCombo(CameraKeyCombo);
            s.CameraCtrl  = CameraCtrl.IsChecked == true;
            s.CameraShift = CameraShift.IsChecked == true;
            s.CameraAlt   = CameraAlt.IsChecked == true;

            s.ScreenShareKey   = GetKeyFromCombo(ShareKeyCombo);
            s.ScreenShareCtrl  = ShareCtrl.IsChecked == true;
            s.ScreenShareShift = ShareShift.IsChecked == true;
            s.ScreenShareAlt   = ShareAlt.IsChecked == true;
        }

        private class KeyItem
        {
            public string Name { get; set; }
            public byte   Code { get; set; }
        }
    }
}
