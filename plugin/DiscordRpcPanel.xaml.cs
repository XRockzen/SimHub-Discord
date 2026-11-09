using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace User.DiscordControl
{
    public partial class DiscordRpcPanel : UserControl
    {
        private readonly DispatcherTimer _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(0.5) };
        private DiscordControlPlugin _plugin;
        private bool _loading;
        private string _shownLog = "";

        public DiscordRpcPanel()
        {
            InitializeComponent();
            _timer.Tick += (s, e) => RefreshView();
            Loaded += (s, e) => { LoadFields(); _timer.Start(); };
            Unloaded += (s, e) => _timer.Stop();
        }

        internal void Attach(DiscordControlPlugin plugin)
        {
            _plugin = plugin;
        }

        private void LoadFields()
        {
            if (_plugin == null) return;
            _loading = true;
            var s = _plugin.Settings;
            TxtClientId.Text = s.RpcClientId;
            TxtScopes2.Text = s.RpcScopes;
            ChkAutoConnect.IsChecked = s.RpcAutoConnect;
            if (!string.IsNullOrEmpty(s.RpcSoundName) && SoundCombo.Items.Count == 0)
            {
                SoundCombo.Items.Add(new RpcSound { Name = s.RpcSoundName, GuildId = s.RpcSoundGuildId, SoundId = s.RpcSoundId });
                SoundCombo.SelectedIndex = 0;
            }
            _loading = false;
            RefreshView();
        }

        private void SaveFields()
        {
            var s = _plugin.Settings;
            s.RpcClientId = TxtClientId.Text.Trim();
            s.RpcScopes = TxtScopes2.Text.Trim();
            s.RpcAutoConnect = ChkAutoConnect.IsChecked == true;
            if (TxtClientSecret.Password.Length > 0)
            {
                _plugin.RpcStore.ClientSecret = TxtClientSecret.Password.Trim();
                TxtClientSecret.Clear();
            }
            _plugin.SaveSettings();
        }

        private void RefreshView()
        {
            if (_plugin == null) return;
            var rpc = _plugin.Rpc;

            switch (rpc.State)
            {
                case RpcState.Ready:
                    TxtStatus.Text = "Connecté : " + rpc.UserName;
                    break;
                case RpcState.Connecting:
                    TxtStatus.Text = "Connexion à Discord…";
                    break;
                case RpcState.WaitingForAuthorization:
                    TxtStatus.Text = "Valide la fenêtre d'autorisation dans Discord";
                    break;
                case RpcState.NeedsAuthorization:
                    TxtStatus.Text = "Autorisation requise";
                    break;
                default:
                    TxtStatus.Text = "Déconnecté";
                    break;
            }

            TxtVoice.Text = rpc.IsReady
                ? string.Format("Micro {0} · Sourdine {1} · Sortie {2:0}% · Entrée {3:0}% · Salon : {4}",
                    rpc.MicMuted ? "coupé" : "actif", rpc.Deafened ? "oui" : "non",
                    rpc.OutputVolume, rpc.InputVolume,
                    string.IsNullOrEmpty(rpc.VoiceChannel) ? "aucun" : rpc.VoiceChannel)
                : "";
            TxtScopes.Text = rpc.IsReady ? "Droits accordés : " + rpc.GrantedScopes : "";

            string log = rpc.RecentLog();
            if (log != _shownLog)
            {
                _shownLog = log;
                TxtLog.Text = log;
                TxtLog.ScrollToEnd();
            }
        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            SaveFields();
            _plugin.Rpc.Disconnect();
            _plugin.Rpc.Connect(true);
        }

        private void Disconnect_Click(object sender, RoutedEventArgs e)
        {
            _plugin.Rpc.Disconnect();
        }

        private void Forget_Click(object sender, RoutedEventArgs e)
        {
            _plugin.Rpc.ForgetAuthorization();
        }

        private void Command_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as FrameworkElement;
            if (button != null) _plugin.RunRpcCommand(button.Tag as string);
        }

        private void LoadSounds_Click(object sender, RoutedEventArgs e)
        {
            _plugin.Rpc.LoadSounds(sounds => Dispatcher.BeginInvoke(new Action(() =>
            {
                _loading = true;
                SoundCombo.Items.Clear();
                foreach (var sound in sounds.OrderBy(x => x.Name)) SoundCombo.Items.Add(sound);
                _loading = false;
                if (SoundCombo.Items.Count > 0) SoundCombo.SelectedIndex = 0;
            })));
        }

        private void SoundCombo_Changed(object sender, SelectionChangedEventArgs e)
        {
            var sound = SoundCombo.SelectedItem as RpcSound;
            if (_loading || sound == null) return;
            var s = _plugin.Settings;
            s.RpcSoundName = sound.Name;
            s.RpcSoundGuildId = sound.GuildId;
            s.RpcSoundId = sound.SoundId;
        }
    }
}
