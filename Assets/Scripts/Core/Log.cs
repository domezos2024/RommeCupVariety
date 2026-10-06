using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;

namespace RommeCup.Core
{
    public static class Log
    {
        public static bool Enabled = true;
        static string F(string m, string fn, string msg) => "[RC][" + m + "." + fn + "][T" + Thread.CurrentThread.ManagedThreadId + "] " + msg;
        public static void I(string module, string msg, [CallerMemberName] string fn = "") { if (Enabled) Debug.Log(F(module, fn, msg)); }
        public static void W(string module, string msg, [CallerMemberName] string fn = "") { if (Enabled) Debug.LogWarning(F(module, fn, msg)); }
        public static void E(string module, string msg, [CallerMemberName] string fn = "") => Debug.LogError(F(module, fn, msg));
        public static void Var(string module, string name, object val, [CallerMemberName] string fn = "") => I(module, name + "=" + val, fn);
    }
}
