using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace User.DiscordControl
{
    /// <summary>
    /// Envoie des frappes clavier globales (Win32) pour déclencher les raccourcis de Discord.
    /// Un code de touche à 0 signifie « aucun raccourci » : rien n'est envoyé.
    /// </summary>
    internal static class KeyboardHelper
    {
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const uint KEYEVENTF_KEYUP = 0x0002;

        private const byte VK_CONTROL = 0xA2;
        private const byte VK_SHIFT   = 0xA0;
        private const byte VK_ALT     = 0xA4;

        /// <summary>Appuie puis relâche la combinaison.</summary>
        public static void SendKeystroke(byte vkCode, bool ctrl, bool shift, bool alt)
        {
            if (vkCode == 0) return;

            KeyDown(vkCode, ctrl, shift, alt);
            Thread.Sleep(20);
            KeyUp(vkCode, ctrl, shift, alt);
        }

        /// <summary>Maintient la combinaison enfoncée (push-to-talk).</summary>
        public static void KeyDown(byte vkCode, bool ctrl, bool shift, bool alt)
        {
            if (vkCode == 0) return;

            if (ctrl)  keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
            if (shift) keybd_event(VK_SHIFT,   0, 0, UIntPtr.Zero);
            if (alt)   keybd_event(VK_ALT,     0, 0, UIntPtr.Zero);
            keybd_event(vkCode, 0, 0, UIntPtr.Zero);
        }

        /// <summary>Relâche la combinaison.</summary>
        public static void KeyUp(byte vkCode, bool ctrl, bool shift, bool alt)
        {
            if (vkCode == 0) return;

            keybd_event(vkCode, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            if (alt)   keybd_event(VK_ALT,     0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            if (shift) keybd_event(VK_SHIFT,   0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            if (ctrl)  keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
    }
}
