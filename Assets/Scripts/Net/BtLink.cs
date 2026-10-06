using System;
using System.Collections;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace RommeCup.Net
{
    public class BtLink
    {
        const string M = "BtLink";
        AndroidJavaObject j;
        public event Action<int, string> Connected, Message;
        public event Action<int> Disconnected;
        public event Action<string, string> Found, ConnectFailed;
        public event Action<string> Error;
        public event Action DiscoveryDone, Listening;

        public string LocalName => Call<string>("localName") ?? "Spielerin";

        T Call<T>(string fn, params object[] a)
        {
            try { return j != null ? j.Call<T>(fn, a) : default; }
            catch (Exception e) { Core.Log.E(M, fn + ": " + e.Message); return default; }
        }

        void Do(string fn, params object[] a)
        {
            try { j?.Call(fn, a); }
            catch (Exception e) { Core.Log.E(M, fn + ": " + e.Message); }
        }

        public IEnumerator Prepare(Action<bool, string> done)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            int sdk;
            using (var v = new AndroidJavaClass("android.os.Build$VERSION")) sdk = v.GetStatic<int>("SDK_INT");
            var perms = sdk >= 31
                ? new[] { "android.permission.BLUETOOTH_CONNECT", "android.permission.BLUETOOTH_SCAN", "android.permission.BLUETOOTH_ADVERTISE" }
                : new[] { "android.permission.ACCESS_FINE_LOCATION" };
            int pending = 0; bool denied = false;
            foreach (var p in perms) if (!Permission.HasUserAuthorizedPermission(p)) pending++;
            if (pending > 0)
            {
                var cb = new PermissionCallbacks();
                cb.PermissionGranted += _ => pending--;
                cb.PermissionDenied += _ => { pending--; denied = true; };
                cb.PermissionDeniedAndDontAskAgain += _ => { pending--; denied = true; };
                Permission.RequestUserPermissions(perms, cb);
                float t = 0;
                while (pending > 0 && t < 60) { t += Time.unscaledDeltaTime; yield return null; }
            }
            if (denied) { done(false, "Bluetooth-Berechtigung wurde verweigert. Bitte in den App-Einstellungen erlauben."); yield break; }
            try
            {
                using var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                var act = up.GetStatic<AndroidJavaObject>("currentActivity");
                j = new AndroidJavaObject("com.domezos.rommecup.RummiBluetooth", act);
            }
            catch (Exception e) { Core.Log.E(M, e.Message); done(false, "Bluetooth-Modul konnte nicht geladen werden."); yield break; }
            if (!Call<bool>("supported")) { done(false, "Dieses Gerät hat kein Bluetooth."); yield break; }
            if (!Call<bool>("enabled"))
            {
                Do("requestEnable");
                float t = 0;
                while (!Call<bool>("enabled") && t < 30) { t += Time.unscaledDeltaTime; yield return null; }
                if (!Call<bool>("enabled")) { done(false, "Bluetooth ist ausgeschaltet."); yield break; }
            }
            Core.Log.I(M, "ready sdk=" + sdk);
            done(true, null);
#else
            yield return null;
            done(false, "Bluetooth ist nur auf Android-Geräten verfügbar. Nutze den Solo-Modus.");
#endif
        }

        public void StartHost(int maxClients) => Do("startHost", maxClients);
        public void StopAccepting() => Do("stopAccepting");
        public void MakeDiscoverable(int sec) => Do("makeDiscoverable", sec);
        public void StartDiscovery() => Do("startDiscovery");
        public void StopDiscovery() => Do("stopDiscovery");
        public void Connect(string addr) => Do("connect", addr);
        public bool Send(int id, string line) => Call<bool>("send", id, line);
        public void Close(int id) => Do("close", id);
        public void Shutdown() { Do("shutdown"); j?.Dispose(); j = null; }

        public void Poll()
        {
            if (j == null) return;
            for (int i = 0; i < 64; i++)
            {
                var s = Call<string>("poll");
                if (s == null) return;
                var p = s.Split(new[] { '|' }, 3);
                int.TryParse(p.Length > 1 ? p[1] : "", out var id);
                switch (p[0])
                {
                    case "C": Connected?.Invoke(id, p.Length > 2 ? p[2] : "?"); break;
                    case "M": Message?.Invoke(id, p.Length > 2 ? p[2] : ""); break;
                    case "D": Disconnected?.Invoke(id); break;
                    case "F": Found?.Invoke(p[1], p.Length > 2 ? p[2] : p[1]); break;
                    case "X": ConnectFailed?.Invoke(p[1], p.Length > 2 ? p[2] : ""); break;
                    case "Z": DiscoveryDone?.Invoke(); break;
                    case "H": Listening?.Invoke(); break;
                    case "E": Error?.Invoke(s.Substring(2)); break;
                }
            }
        }
    }
}
