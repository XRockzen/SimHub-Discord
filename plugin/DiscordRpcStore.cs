using System;
using System.Security.Cryptography;
using System.Text;

namespace User.DiscordControl
{
    /// <summary>
    /// Relie le client RPC aux réglages du plugin. Le secret et les jetons sont chiffrés
    /// pour le compte Windows courant (DPAPI) avant d'être écrits dans le fichier de réglages.
    /// </summary>
    internal sealed class DiscordRpcStore : IDiscordRpcStore
    {
        private readonly DiscordControlPlugin _plugin;

        public DiscordRpcStore(DiscordControlPlugin plugin)
        {
            _plugin = plugin;
        }

        private DiscordControlSettings S { get { return _plugin.Settings; } }

        public string ClientId { get { return S.RpcClientId; } }
        public string Scopes   { get { return S.RpcScopes; } }

        public string ClientSecret
        {
            get { return Unprotect(S.RpcClientSecretProtected); }
            set { S.RpcClientSecretProtected = Protect(value); }
        }

        public string AccessToken
        {
            get { return Unprotect(S.RpcAccessTokenProtected); }
            set { S.RpcAccessTokenProtected = Protect(value); }
        }

        public string RefreshToken
        {
            get { return Unprotect(S.RpcRefreshTokenProtected); }
            set { S.RpcRefreshTokenProtected = Protect(value); }
        }

        public DateTime TokenExpiresUtc
        {
            get { return S.RpcTokenExpiresUtc; }
            set { S.RpcTokenExpiresUtc = value; }
        }

        public void Save()
        {
            _plugin.SaveSettings();
        }

        private static string Protect(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            var data = ProtectedData.Protect(Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(data);
        }

        private static string Unprotect(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            try
            {
                var data = ProtectedData.Unprotect(Convert.FromBase64String(value), null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(data);
            }
            catch (Exception)
            {
                return "";
            }
        }
    }
}
