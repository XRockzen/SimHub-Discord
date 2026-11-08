using System;
using System.Collections.Generic;
using System.Diagnostics;
using NAudio.CoreAudioApi;

namespace User.DiscordControl
{
    /// <summary>
    /// Pilote la session audio de Discord dans le mixeur Windows (Core Audio, via NAudio).
    /// </summary>
    internal class DiscordAudioController
    {
        internal static readonly string[] ProcessNames =
            { "Discord", "DiscordPTB", "DiscordCanary", "DiscordDevelopment" };

        private DateTime _lastRefresh;

        public bool  IsDiscordRunning { get; private set; }
        public float Volume           { get; private set; }
        public bool  IsMuted          { get; private set; }

        public int VolumePercent
        {
            get { return (int)Math.Round(Volume * 100); }
        }

        public void Refresh(bool force)
        {
            if (!force && (DateTime.Now - _lastRefresh).TotalSeconds < 2) return;
            DoRefresh();
            _lastRefresh = DateTime.Now;
        }

        public void AdjustVolume(float delta)
        {
            WithDiscordSession(session =>
            {
                float next = Clamp(session.SimpleAudioVolume.Volume + delta);
                session.SimpleAudioVolume.Volume = next;
                Volume = next;
            });
        }

        public void SetVolume(float level)
        {
            float clamped = Clamp(level);
            WithDiscordSession(session =>
            {
                session.SimpleAudioVolume.Volume = clamped;
                Volume = clamped;
            });
        }

        public void ToggleMute()
        {
            WithDiscordSession(session =>
            {
                bool muted = !session.SimpleAudioVolume.Mute;
                session.SimpleAudioVolume.Mute = muted;
                IsMuted = muted;
            });
        }

        private static float Clamp(float level)
        {
            return Math.Max(0f, Math.Min(1f, level));
        }

        private void DoRefresh()
        {
            try
            {
                var session = FindDiscordSession();
                IsDiscordRunning = session != null;
                Volume           = session != null ? session.SimpleAudioVolume.Volume : 0;
                IsMuted          = session != null && session.SimpleAudioVolume.Mute;
            }
            catch (Exception ex)
            {
                SimHub.Logging.Current.Warn(string.Format(
                    "[DiscordControl] DoRefresh error: {0}", ex.Message));
                IsDiscordRunning = false;
            }
        }

        private void WithDiscordSession(Action<AudioSessionControl> action)
        {
            try
            {
                var session = FindDiscordSession();
                if (session != null)
                {
                    action(session);
                    IsDiscordRunning = true;
                }
            }
            catch (Exception ex)
            {
                SimHub.Logging.Current.Warn(string.Format(
                    "[DiscordControl] WithDiscordSession error: {0}", ex.Message));
            }
        }

        private static AudioSessionControl FindDiscordSession()
        {
            try
            {
                var discordPids = new HashSet<int>();
                foreach (var name in ProcessNames)
                    foreach (var proc in Process.GetProcessesByName(name))
                        discordPids.Add(proc.Id);

                if (discordPids.Count == 0) return null;

                var enumerator = new MMDeviceEnumerator();
                var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                if (device == null) return null;

                var sessionManager = device.AudioSessionManager;
                if (sessionManager == null) return null;

                var sessions = sessionManager.Sessions;
                if (sessions == null) return null;

                for (int i = 0; i < sessions.Count; i++)
                {
                    var session = sessions[i];
                    if (session == null) continue;

                    try
                    {
                        if (discordPids.Contains((int)session.GetProcessID))
                            return session;
                    }
                    catch { }
                }

                // Repli : recherche par identifiant de session
                for (int i = 0; i < sessions.Count; i++)
                {
                    var session = sessions[i];
                    if (session == null) continue;

                    try
                    {
                        string sessionId = session.GetSessionIdentifier;
                        if (sessionId != null && sessionId.IndexOf("discord", StringComparison.OrdinalIgnoreCase) >= 0)
                            return session;
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                SimHub.Logging.Current.Warn(string.Format(
                    "[DiscordControl] FindDiscordSession error: {0}", ex.Message));
            }

            return null;
        }
    }
}
