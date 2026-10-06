using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RommeCup.Core;
using UnityEngine;

namespace RommeCup.App
{
    public class AutoTest : MonoBehaviour
    {
        const string M = "AutoTest";
        public static bool Requested => Environment.GetCommandLineArgs().Contains("-autotest");
        string dir;
        readonly List<string> errors = new List<string>();
        readonly List<string> steps = new List<string>();
        int frames;
        float start;

        static string Arg(string name)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return null;
        }

        void OnEnable() => Application.logMessageReceived += OnLog;
        void OnDisable() => Application.logMessageReceived -= OnLog;
        void OnLog(string c, string st, LogType t) { if ((t == LogType.Error || t == LogType.Exception || t == LogType.Assert) && errors.Count < 40) errors.Add(t + ": " + c + " | " + st.Split('\n')[0]); }
        void Update() => frames++;

        IEnumerator Start()
        {
            dir = Arg("-shots") ?? Path.Combine(Application.persistentDataPath, "shots");
            Directory.CreateDirectory(dir);
            start = Time.realtimeSinceStartup;
            var app = GameApp.I;
            yield return W(2.5f); yield return Shot("01_menu");
            app.Open(app.Rummi); yield return W(1.2f); yield return Shot("02_rummi_modus");
            app.Rummi.Screens.ShowRules(1); yield return W(.6f); yield return Shot("03_rummi_regeln");
            UiKit.Clear(app.ModalLayer);
            app.Rummi.StartSolo(3, 1); yield return W(1.2f); yield return Shot("04_rummi_austeilen");
            yield return W(4); yield return Shot("05_rummi_start");
            app.Rummi.Play.Sort(true); yield return W(1.5f); yield return Shot("06_rummi_sortiert");
            yield return Until(() => app.Rummi.Play.MyTurn, 40);
            app.Rummi.Play.Hint(); yield return W(1.2f); yield return Shot("07_rummi_tipp");
            app.Rummi.Host.Autopilot = true; Time.timeScale = 5;
            yield return Until(() => app.Rummi.RoundShowing || (app.Rummi.Play.St != null && app.Rummi.Play.St.pool < 40), 200);
            Time.timeScale = 1; yield return W(3); yield return Shot("08_rummi_spielmitte");
            Time.timeScale = 8;
            yield return Until(() => app.Rummi.RoundShowing, 300);
            Time.timeScale = 1; yield return W(2.5f); yield return Shot("09_rummi_rundenende");
            app.ShowMainMenu(); yield return W(1);
            app.Open(app.Karten); yield return Until(() => app.Karten.Table.Ready, 30); yield return W(1.5f); yield return Shot("10_romme_modus");
            app.Karten.StartSolo(2, 1); yield return W(1.2f); yield return Shot("11_romme_austeilen");
            yield return W(4); yield return Shot("12_romme_start");
            yield return Until(() => app.Karten.Play.MyDraw, 40);
            app.Karten.Play.DrawStock(); yield return W(1.8f);
            app.Karten.Play.Hint(); yield return W(1.2f); yield return Shot("13_romme_tipp");
            app.Karten.Host.Autopilot = true; Time.timeScale = 5;
            yield return Until(() => app.Karten.RoundShowing || (app.Karten.Play.St != null && app.Karten.Play.St.table != null && app.Karten.Play.St.table.Length > 14), 200);
            Time.timeScale = 1; yield return W(3); yield return Shot("14_romme_spielmitte");
            Time.timeScale = 8;
            yield return Until(() => app.Karten.RoundShowing, 300);
            Time.timeScale = 1; yield return W(2.5f); yield return Shot("15_romme_rundenende");
            app.ShowMainMenu(); yield return W(1);
            float secs = Time.realtimeSinceStartup - start;
            var rep = new List<string> { "steps=" + steps.Count, "seconds=" + secs.ToString("0"), "avgFps=" + (frames / secs).ToString("0.0"), "errors=" + errors.Count };
            rep.AddRange(steps); rep.AddRange(errors);
            File.WriteAllLines(Path.Combine(dir, "report.txt"), rep);
            Log.I(M, "done errors=" + errors.Count);
            Application.Quit();
        }

        static IEnumerator W(float s) { yield return new WaitForSecondsRealtime(s); }

        IEnumerator Until(Func<bool> c, float timeout)
        {
            float t = 0;
            while (!c() && t < timeout) { t += Time.unscaledDeltaTime; yield return null; }
            if (t >= timeout) steps.Add("TIMEOUT after step " + steps.Count);
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            Destroy(tex);
            steps.Add(name + " fps=" + (1f / Mathf.Max(.001f, Time.unscaledDeltaTime)).ToString("0"));
            Log.I(M, "shot " + name);
        }
    }
}
