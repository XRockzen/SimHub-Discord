using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace User.DiscordControl
{
    internal enum RpcState
    {
        Disconnected,
        Connecting,
        WaitingForAuthorization,
        NeedsAuthorization,
        Ready
    }

    /// <summary>Identifiants et jetons de l'application Discord, stockés hors du client.</summary>
    internal interface IDiscordRpcStore
    {
        string   ClientId      { get; }
        string   ClientSecret  { get; }
        string   Scopes        { get; }
        string   AccessToken   { get; set; }
        string   RefreshToken  { get; set; }
        DateTime TokenExpiresUtc { get; set; }
        void     Save();
    }

    internal class RpcSound
    {
        public string Name;
        public string GuildId;
        public string SoundId;
        public override string ToString() { return Name; }
    }

    /// <summary>
    /// Client de l'API locale de Discord (RPC) sur son canal nommé.
    /// Une session tourne dans un thread dédié ; les réponses arrivent sur ce même thread.
    /// </summary>
    internal sealed class DiscordRpcClient
    {
        private const int OpHandshake = 0, OpFrame = 1, OpClose = 2, OpPing = 3, OpPong = 4;
        private const int MaxFrameLength = 16 * 1024 * 1024;
        private const int MaxLogLines = 60;
        private const string UserAgent = "SimHub-Discord (https://github.com/XRockzen/SimHub-Discord, 1.0)";
        private const string RedirectUri = "http://localhost";

        private readonly IDiscordRpcStore _store;
        private readonly Action<string> _logSink;
        private readonly string _pipePrefix;
        private readonly string _tokenUrl;

        private readonly object _writeLock = new object();
        private readonly Dictionary<string, Action<JToken, string>> _pending =
            new Dictionary<string, Action<JToken, string>>();
        private readonly List<string> _log = new List<string>();
        private readonly AutoResetEvent _wake = new AutoResetEvent(false);

        private Thread _thread;
        private NamedPipeClientStream _pipe;
        private volatile bool _wanted, _stopping, _allowAuthorize;
        private bool _refreshTried;
        private string _lastLine;

        public RpcState State            { get; private set; }
        public bool     MicMuted         { get; private set; }
        public bool     Deafened         { get; private set; }
        public double   OutputVolume     { get; private set; }
        public double   InputVolume      { get; private set; }
        public string   VoiceChannel     { get; private set; }
        public string   UserName         { get; private set; }
        public string   GrantedScopes    { get; private set; }

        public bool IsReady { get { return State == RpcState.Ready; } }

        public DiscordRpcClient(IDiscordRpcStore store, Action<string> logSink,
            string pipePrefix = "discord-ipc-", string tokenUrl = "https://discord.com/api/oauth2/token")
        {
            _store = store;
            _logSink = logSink;
            _pipePrefix = pipePrefix;
            _tokenUrl = tokenUrl;
            VoiceChannel = "";
            UserName = "";
            GrantedScopes = "";
        }

        // ─── Cycle de vie ─────────────────────────────────────────────────

        /// <param name="allowAuthorize">Autorise la fenêtre d'autorisation de Discord si aucun jeton n'est valide.</param>
        public void Connect(bool allowAuthorize)
        {
            _allowAuthorize = allowAuthorize;
            _wanted = true;
            if (_thread == null)
            {
                _thread = new Thread(Run) { IsBackground = true, Name = "DiscordRpc" };
                _thread.Start();
            }
            _wake.Set();
        }

        public void Disconnect()
        {
            _wanted = false;
            ClosePipe();
            _wake.Set();
        }

        public void Stop()
        {
            _stopping = true;
            Disconnect();
            if (_thread != null) _thread.Join(2000);
        }

        /// <summary>Oublie les jetons : la prochaine connexion redemandera l'autorisation.</summary>
        public void ForgetAuthorization()
        {
            Disconnect();
            ClearTokens();
            Log("Autorisation oubliée");
        }

        private void Run()
        {
            while (!_stopping)
            {
                if (!_wanted)
                {
                    if (State != RpcState.NeedsAuthorization) State = RpcState.Disconnected;
                    _wake.WaitOne();
                    continue;
                }

                try
                {
                    RunSession();
                }
                catch (Exception ex)
                {
                    if (_wanted && !_stopping) Log("Connexion perdue : " + ex.Message);
                }
                finally
                {
                    ClosePipe();
                    lock (_pending) _pending.Clear();
                    VoiceChannel = "";
                }

                if (_wanted && !_stopping)
                {
                    State = RpcState.Connecting;
                    _wake.WaitOne(5000);
                }
            }
            State = RpcState.Disconnected;
        }

        private void RunSession()
        {
            State = RpcState.Connecting;
            _refreshTried = false;

            var pipe = OpenPipe();
            if (pipe == null) throw new IOException("Discord n'est pas lancé");
            _pipe = pipe;

            WriteFrame(OpHandshake, new JObject { { "v", 1 }, { "client_id", _store.ClientId } });

            while (true)
            {
                int op;
                byte[] payload = ReadFrame(pipe, out op);

                if (op == OpFrame)
                    HandleFrame(JObject.Parse(Encoding.UTF8.GetString(payload)));
                else if (op == OpPing)
                    WriteRaw(OpPong, payload);
                else if (op == OpClose)
                    HandleClose(Encoding.UTF8.GetString(payload));
            }
        }

        private NamedPipeClientStream OpenPipe()
        {
            for (int i = 0; i < 10; i++)
            {
                // Asynchronous obligatoire : sur un canal synchrone, une lecture
                // en attente bloquerait toutes les écritures.
                var pipe = new NamedPipeClientStream(".", _pipePrefix + i,
                    PipeDirection.InOut, PipeOptions.Asynchronous);
                try
                {
                    pipe.Connect(200);
                    return pipe;
                }
                catch (Exception)
                {
                    pipe.Dispose();
                }
            }
            return null;
        }

        private void ClosePipe()
        {
            var pipe = Interlocked.Exchange(ref _pipe, null);
            if (pipe == null) return;
            try { pipe.Dispose(); } catch (Exception) { }
        }

        private void HandleClose(string json)
        {
            var close = JObject.Parse(json);
            int code = (int?)close["code"] ?? 0;
            string message = (string)close["message"] ?? "";
            if (code >= 4000)
            {
                Log(string.Format("Refusé par Discord ({0}) : {1}", code, message));
                _wanted = false;
            }
            throw new IOException(string.Format("fermeture par Discord ({0}) {1}", code, message));
        }

        // ─── Trames ───────────────────────────────────────────────────────

        private static byte[] ReadFrame(Stream stream, out int op)
        {
            var header = new byte[8];
            ReadExact(stream, header);
            op = BitConverter.ToInt32(header, 0);
            int length = BitConverter.ToInt32(header, 4);
            if (length < 0 || length > MaxFrameLength)
                throw new IOException("Trame invalide");
            var payload = new byte[length];
            ReadExact(stream, payload);
            return payload;
        }

        private static void ReadExact(Stream stream, byte[] buffer)
        {
            int offset = 0;
            while (offset < buffer.Length)
            {
                int read = stream.Read(buffer, offset, buffer.Length - offset);
                if (read <= 0) throw new EndOfStreamException("canal fermé");
                offset += read;
            }
        }

        private void WriteFrame(int op, JObject body)
        {
            WriteRaw(op, Encoding.UTF8.GetBytes(body.ToString(Newtonsoft.Json.Formatting.None)));
        }

        private void WriteRaw(int op, byte[] payload)
        {
            var frame = new byte[8 + payload.Length];
            BitConverter.GetBytes(op).CopyTo(frame, 0);
            BitConverter.GetBytes(payload.Length).CopyTo(frame, 4);
            payload.CopyTo(frame, 8);

            lock (_writeLock)
            {
                var pipe = _pipe;
                if (pipe == null) throw new IOException("canal fermé");
                pipe.Write(frame, 0, frame.Length);
            }
        }

        private bool Send(string cmd, JObject args, string evt, Action<JToken, string> callback)
        {
            string nonce = Guid.NewGuid().ToString();
            var frame = new JObject { { "cmd", cmd }, { "args", args ?? new JObject() }, { "nonce", nonce } };
            if (evt != null) frame["evt"] = evt;

            if (callback != null) lock (_pending) _pending[nonce] = callback;
            try
            {
                WriteFrame(OpFrame, frame);
                return true;
            }
            catch (Exception ex)
            {
                lock (_pending) _pending.Remove(nonce);
                Log(cmd + " : envoi impossible : " + ex.Message);
                return false;
            }
        }

        private void HandleFrame(JObject frame)
        {
            string cmd = (string)frame["cmd"];
            string evt = (string)frame["evt"];
            JToken data = frame["data"];

            if (cmd == "DISPATCH")
            {
                HandleEvent(evt, data);
                return;
            }

            string nonce = (string)frame["nonce"];
            Action<JToken, string> callback = null;
            if (nonce != null)
            {
                lock (_pending)
                {
                    if (_pending.TryGetValue(nonce, out callback))
                        _pending.Remove(nonce);
                }
            }

            string error = null;
            if (evt == "ERROR")
                error = string.Format("{0} ({1})", (string)data["message"], (int?)data["code"] ?? 0);

            if (callback != null) callback(data, error);
            else if (error != null) Log(cmd + " : " + error);
        }

        private void HandleEvent(string evt, JToken data)
        {
            switch (evt)
            {
                case "READY":
                    BeginAuthentication();
                    break;
                case "VOICE_SETTINGS_UPDATE":
                    ApplyVoiceSettings(data);
                    break;
                case "VOICE_CHANNEL_SELECT":
                    RefreshVoiceChannel();
                    break;
                case "ERROR":
                    Log("Erreur Discord : " + (string)data["message"]);
                    break;
            }
        }

        // ─── Authentification ─────────────────────────────────────────────

        private void BeginAuthentication()
        {
            bool expiresSoon = _store.TokenExpiresUtc < DateTime.UtcNow.AddDays(1);
            if (!string.IsNullOrEmpty(_store.RefreshToken) && expiresSoon)
                RefreshAccessToken();

            if (!string.IsNullOrEmpty(_store.AccessToken))
                Authenticate();
            else
                Authorize();
        }

        private void Authorize()
        {
            if (!_allowAuthorize)
            {
                GiveUpAuthorization("Autorisation requise : clique sur Connecter");
                return;
            }
            _allowAuthorize = false;

            if (string.IsNullOrEmpty(_store.ClientSecret))
            {
                GiveUpAuthorization("Secret de l'application manquant");
                return;
            }

            State = RpcState.WaitingForAuthorization;
            Log("Demande d'autorisation : valide la fenêtre dans Discord");

            var scopes = new JArray(_store.Scopes.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Cast<object>().ToArray());
            Send("AUTHORIZE", new JObject { { "client_id", _store.ClientId }, { "scopes", scopes } }, null, (data, error) =>
            {
                if (error != null)
                {
                    GiveUpAuthorization("Autorisation refusée : " + error);
                    return;
                }

                if (ExchangeCode((string)data["code"]))
                    Authenticate();
                else
                    GiveUpAuthorization("Échange du code d'autorisation impossible");
            });
        }

        private void Authenticate()
        {
            Send("AUTHENTICATE", new JObject { { "access_token", _store.AccessToken } }, null, (data, error) =>
            {
                if (error == null)
                {
                    OnAuthenticated(data);
                    return;
                }

                Log("Jeton refusé : " + error);
                if (!_refreshTried && !string.IsNullOrEmpty(_store.RefreshToken) && RefreshAccessToken())
                {
                    Authenticate();
                    return;
                }

                ClearTokens();
                GiveUpAuthorization("Autorisation requise : clique sur Connecter");
            });
        }

        private void OnAuthenticated(JToken data)
        {
            var user = data["user"];
            UserName = user != null ? (string)user["global_name"] ?? (string)user["username"] ?? "" : "";
            var scopes = data["scopes"] as JArray;
            GrantedScopes = scopes != null ? string.Join(" ", scopes.Select(s => (string)s)) : "";
            State = RpcState.Ready;
            Log(string.Format("Connecté : {0} (droits : {1})", UserName, GrantedScopes));

            Subscribe("VOICE_SETTINGS_UPDATE");
            Subscribe("VOICE_CHANNEL_SELECT");
            Send("GET_VOICE_SETTINGS", null, null, (settings, error) =>
            {
                if (error != null) Log("GET_VOICE_SETTINGS : " + error);
                else ApplyVoiceSettings(settings);
            });
            RefreshVoiceChannel();
        }

        private void Subscribe(string evt)
        {
            Send("SUBSCRIBE", null, evt, (data, error) =>
            {
                if (error != null) Log("Abonnement " + evt + " : " + error);
            });
        }

        private void GiveUpAuthorization(string message)
        {
            Log(message);
            State = RpcState.NeedsAuthorization;
            _wanted = false;
            ClosePipe();
        }

        private bool ExchangeCode(string code)
        {
            var form = new NameValueCollection
            {
                { "grant_type", "authorization_code" },
                { "code", code },
                { "client_id", _store.ClientId },
                { "client_secret", _store.ClientSecret },
                { "redirect_uri", RedirectUri }
            };

            if (RequestTokens(form)) return true;

            // Selon l'application, Discord exige ou refuse redirect_uri pour un code RPC
            form.Remove("redirect_uri");
            return RequestTokens(form);
        }

        private bool RefreshAccessToken()
        {
            _refreshTried = true;
            return RequestTokens(new NameValueCollection
            {
                { "grant_type", "refresh_token" },
                { "refresh_token", _store.RefreshToken },
                { "client_id", _store.ClientId },
                { "client_secret", _store.ClientSecret }
            });
        }

        private bool RequestTokens(NameValueCollection form)
        {
            try
            {
                using (var web = new WebClient())
                {
                    web.Headers[HttpRequestHeader.UserAgent] = UserAgent;
                    var reply = JObject.Parse(Encoding.UTF8.GetString(web.UploadValues(_tokenUrl, "POST", form)));
                    _store.AccessToken = (string)reply["access_token"];
                    _store.RefreshToken = (string)reply["refresh_token"];
                    _store.TokenExpiresUtc = DateTime.UtcNow.AddSeconds((int?)reply["expires_in"] ?? 0);
                    _store.Save();
                    return true;
                }
            }
            catch (WebException ex)
            {
                Log(string.Format("Échange de jeton ({0}) : {1}", form["grant_type"], DescribeTokenError(ex)));
            }
            catch (Exception ex)
            {
                Log(string.Format("Échange de jeton ({0}) : {1}", form["grant_type"], ex.Message));
            }
            return false;
        }

        private static string DescribeTokenError(WebException ex)
        {
            if (ex.Response == null) return ex.Message;
            using (var reader = new StreamReader(ex.Response.GetResponseStream(), Encoding.UTF8))
            {
                try
                {
                    var body = JObject.Parse(reader.ReadToEnd());
                    return string.Format("{0} {1}", (string)body["error"], (string)body["error_description"]).Trim();
                }
                catch (Exception)
                {
                    return ex.Message;
                }
            }
        }

        private void ClearTokens()
        {
            _store.AccessToken = "";
            _store.RefreshToken = "";
            _store.TokenExpiresUtc = DateTime.MinValue;
            _store.Save();
        }

        // ─── État vocal ───────────────────────────────────────────────────

        private void ApplyVoiceSettings(JToken data)
        {
            MicMuted = (bool?)data["mute"] ?? MicMuted;
            Deafened = (bool?)data["deaf"] ?? Deafened;

            var input = data["input"];
            if (input != null) InputVolume = (double?)input["volume"] ?? InputVolume;
            var output = data["output"];
            if (output != null) OutputVolume = (double?)output["volume"] ?? OutputVolume;
        }

        private void RefreshVoiceChannel()
        {
            Send("GET_SELECTED_VOICE_CHANNEL", null, null, (data, error) =>
            {
                if (error != null)
                {
                    Log("GET_SELECTED_VOICE_CHANNEL : " + error);
                    return;
                }
                if (data == null || data.Type == JTokenType.Null)
                {
                    VoiceChannel = "";
                    return;
                }
                string name = (string)data["name"];
                VoiceChannel = string.IsNullOrEmpty(name) ? "Appel" : name;
            });
        }

        // ─── Commandes ────────────────────────────────────────────────────

        public void ToggleMic()
        {
            bool next = !MicMuted;
            if (Execute("SET_VOICE_SETTINGS", new JObject { { "mute", next } })) MicMuted = next;
        }

        public void ToggleDeafen()
        {
            bool next = !Deafened;
            if (Execute("SET_VOICE_SETTINGS", new JObject { { "deaf", next } })) Deafened = next;
        }

        public void AdjustOutputVolume(double delta)
        {
            double next = Math.Max(0, Math.Min(200, OutputVolume + delta));
            if (Execute("SET_VOICE_SETTINGS", new JObject { { "output", new JObject { { "volume", next } } } }))
                OutputVolume = next;
        }

        public void AdjustInputVolume(double delta)
        {
            double next = Math.Max(0, Math.Min(100, InputVolume + delta));
            if (Execute("SET_VOICE_SETTINGS", new JObject { { "input", new JObject { { "volume", next } } } }))
                InputVolume = next;
        }

        public void HangUp()
        {
            Execute("SELECT_VOICE_CHANNEL", new JObject { { "channel_id", JValue.CreateNull() } });
        }

        // Commandes présentes dans le client mais non documentées par Discord
        public void ToggleVideo()       { Execute("TOGGLE_VIDEO", new JObject()); }
        public void ToggleScreenShare() { Execute("TOGGLE_SCREENSHARE", new JObject()); }

        public void PlaySound(string guildId, string soundId)
        {
            Execute("PLAY_SOUNDBOARD_SOUND", new JObject { { "guild_id", guildId }, { "sound_id", soundId } });
        }

        public void LoadSounds(Action<List<RpcSound>> done)
        {
            if (!RequireReady("GET_SOUNDBOARD_SOUNDS")) return;
            Send("GET_SOUNDBOARD_SOUNDS", null, null, (data, error) =>
            {
                if (error != null)
                {
                    Log("GET_SOUNDBOARD_SOUNDS : " + error);
                    return;
                }

                var sounds = new List<RpcSound>();
                foreach (var item in data as JArray ?? new JArray())
                {
                    sounds.Add(new RpcSound
                    {
                        Name    = (string)item["name"],
                        GuildId = (string)item["guild_id"],
                        SoundId = (string)(item["sound_id"] ?? item["id"])
                    });
                }
                Log(string.Format("GET_SOUNDBOARD_SOUNDS : {0} sons", sounds.Count));
                done(sounds);
            });
        }

        private bool Execute(string cmd, JObject args)
        {
            if (!RequireReady(cmd)) return false;
            return Send(cmd, args, null, (data, error) => Log(cmd + " : " + (error ?? "OK")));
        }

        private bool RequireReady(string cmd)
        {
            if (IsReady) return true;
            Log(cmd + " : non connecté à Discord");
            return false;
        }

        // ─── Journal ──────────────────────────────────────────────────────

        public string RecentLog()
        {
            lock (_log) return string.Join(Environment.NewLine, _log);
        }

        private void Log(string line)
        {
            lock (_log)
            {
                if (line == _lastLine) return;
                _lastLine = line;
                _log.Add(DateTime.Now.ToString("HH:mm:ss ") + line);
                if (_log.Count > MaxLogLines) _log.RemoveAt(0);
            }
            if (_logSink != null) _logSink(line);
        }
    }
}
