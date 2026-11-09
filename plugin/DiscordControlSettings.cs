namespace User.DiscordControl
{
    /// <summary>
    /// Réglages enregistrés par SimHub. Les noms des propriétés sont ceux du fichier JSON :
    /// les renommer ferait perdre les réglages existants.
    /// Une touche à 0 (code VK) signifie « aucun raccourci ».
    /// </summary>
    public class DiscordControlSettings
    {
        public int VolumeStep { get; set; }

        public byte MuteKey          { get; set; }
        public bool MuteCtrl         { get; set; }
        public bool MuteShift        { get; set; }
        public bool MuteAlt          { get; set; }

        public byte DeafenKey        { get; set; }
        public bool DeafenCtrl       { get; set; }
        public bool DeafenShift      { get; set; }
        public bool DeafenAlt        { get; set; }

        public byte PttKey           { get; set; }
        public bool PttCtrl          { get; set; }
        public bool PttShift         { get; set; }
        public bool PttAlt           { get; set; }

        public byte OverlayKey       { get; set; }
        public bool OverlayCtrl      { get; set; }
        public bool OverlayShift     { get; set; }
        public bool OverlayAlt       { get; set; }

        public byte CameraKey        { get; set; }
        public bool CameraCtrl       { get; set; }
        public bool CameraShift      { get; set; }
        public bool CameraAlt        { get; set; }

        public byte ScreenShareKey   { get; set; }
        public bool ScreenShareCtrl  { get; set; }
        public bool ScreenShareShift { get; set; }
        public bool ScreenShareAlt   { get; set; }

        // API locale de Discord (RPC). Secret et jetons sont chiffrés par DiscordRpcStore.
        public string   RpcClientId              { get; set; }
        public string   RpcClientSecretProtected { get; set; }
        public string   RpcAccessTokenProtected  { get; set; }
        public string   RpcRefreshTokenProtected { get; set; }
        public System.DateTime RpcTokenExpiresUtc { get; set; }
        public string   RpcScopes                { get; set; }
        public bool     RpcAutoConnect           { get; set; }
        public string   RpcSoundName             { get; set; }
        public string   RpcSoundGuildId          { get; set; }
        public string   RpcSoundId               { get; set; }

        public DiscordControlSettings()
        {
            VolumeStep = 10;
            RpcClientId = "";
            RpcScopes = "rpc rpc.voice.read rpc.voice.write rpc.video.write rpc.screenshare.write";
            RpcAutoConnect = true;
        }
    }
}
