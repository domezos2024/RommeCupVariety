using System.Collections;
using System.Collections.Generic;
using RommeCup.Core;
using RommeCup.Karten;
using RommeCup.Rummikub;
using UnityEngine;
using UnityEngine.UI;

namespace RommeCup.App
{
    public interface IGameModule
    {
        string Title { get; }
        string Subtitle { get; }
        bool Available { get; }
        void Open();
        void Update(float dt);
        bool Back();
        void Close();
    }

    public class GameApp : MonoBehaviour
    {
        const string M = "GameApp";
        public static GameApp I;
        public RummiModule Rummi;
        public KartenModule Karten;
        public readonly List<IGameModule> Modules = new List<IGameModule>();
        public RectTransform ScreenLayer, HudLayer, ModalLayer, ToastLayer;
        public bool Booted;
        IGameModule active;

        public static string PlayerName
        {
            get => PlayerPrefs.GetString("name2", "Spieler");
            set { PlayerPrefs.SetString("name2", string.IsNullOrWhiteSpace(value) ? "Spieler" : value.Trim()); PlayerPrefs.Save(); }
        }

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Log.I(M, "start " + Application.version + " " + SystemInfo.deviceModel + " " + Screen.width + "x" + Screen.height);
            Mats.Init(); UiKit.Init(); Sfx.Init(gameObject); Fx.Init(); Env.Init();
            UiKit.VignetteLayer();
            var canvas = UiKit.Canvas("UI", 10);
            var safe = UiKit.SafeRoot(canvas);
            ScreenLayer = UiKit.Full(safe, "Screen");
            HudLayer = UiKit.Full(safe, "Hud");
            ModalLayer = UiKit.Full(canvas.transform, "Modal");
            ToastLayer = UiKit.Full(safe, "Toast");
            StartCoroutine(Boot());
        }

        IEnumerator Boot()
        {
            var bar = Loading();
            yield return null;
            Env.Textures();
            Rummi = new RummiModule(this);
            yield return Rummi.Board.Build(p => { if (bar) bar.fillAmount = .15f + p * .85f; });
            Karten = new KartenModule(this);
            Modules.Add(Rummi); Modules.Add(Karten);
            Booted = true;
            ShowMainMenu();
            yield return Karten.Table.Build();
            if (AutoTest.Requested) gameObject.AddComponent<AutoTest>();
        }

        Image Loading()
        {
            UiKit.Clear(ScreenLayer);
            var t = UiKit.Label(ScreenLayer, "Romme Cup Variety", 86, Theme.Brass, TextAnchor.MiddleCenter, FontStyle.Bold, true);
            UiKit.Place(t, new Vector2(0, .5f), new Vector2(1, .5f), new Vector2(0, 40), new Vector2(0, 160));
            var s = UiKit.Label(ScreenLayer, "Der Spieltisch wird vorbereitet ...", 32, Theme.Dim, TextAnchor.MiddleCenter, FontStyle.Italic);
            UiKit.Place(s, new Vector2(0, .5f), new Vector2(1, .5f), new Vector2(0, -30), new Vector2(0, 30));
            var bg = UiKit.Box(ScreenLayer, new Color(1, 1, 1, .08f), false, "Bar");
            UiKit.Place(bg, new Vector2(.3f, .5f), new Vector2(.7f, .5f), new Vector2(0, -90), new Vector2(0, -78));
            var fill = UiKit.Box(bg.transform, Theme.Brass, false, "Fill");
            fill.sprite = null; fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillAmount = .1f;
            return fill;
        }

        void Update()
        {
            if (!Booted) return;
            float dt = Time.deltaTime;
            foreach (var m in Modules) m.Update(dt);
            if (Input.GetKeyDown(KeyCode.Escape) && (active == null || !active.Back())) { if (active != null) ShowMainMenu(); else Application.Quit(); }
        }

        public void Open(IGameModule m) { active = m; m.Open(); }

        public void ShowMainMenu()
        {
            active = null;
            UiKit.Clear(ScreenLayer); UiKit.Clear(HudLayer); UiKit.Clear(ModalLayer);
            foreach (var m in Modules) m.Close();
            var col = UiKit.Box(ScreenLayer, Theme.Panel, true, "MainMenu");
            UiKit.Place(col, new Vector2(0, 0), new Vector2(0, 1), new Vector2(40, 40), new Vector2(760, -40));
            var c = (RectTransform)col.transform;
            var title = UiKit.Label(c, "Romme Cup Variety", 68, Theme.Brass, TextAnchor.MiddleCenter, FontStyle.Bold, true);
            UiKit.Place(title, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -130), new Vector2(0, -36));
            var sub = UiKit.Label(c, "Klassische Legespiele am Spieltisch", 30, Theme.Dim, TextAnchor.MiddleCenter, FontStyle.Italic);
            UiKit.Place(sub, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -180), new Vector2(0, -130));
            var line = UiKit.Shade(c, Theme.Line);
            UiKit.Place(line, new Vector2(.15f, 1), new Vector2(.85f, 1), new Vector2(0, -196), new Vector2(0, -194));
            var nl = UiKit.Label(c, "Dein Name", 28, Theme.Dim, TextAnchor.MiddleLeft, FontStyle.Normal);
            UiKit.Place(nl, new Vector2(.09f, 1), new Vector2(.91f, 1), new Vector2(0, -256), new Vector2(0, -216));
            var inp = UiKit.Input(c, PlayerName, 34);
            UiKit.Place(inp, new Vector2(.09f, 1), new Vector2(.91f, 1), new Vector2(0, -340), new Vector2(0, -262));
            inp.onEndEdit.AddListener(v => PlayerName = v);
            float y = -380;
            foreach (var m in Modules)
            {
                var mod = m;
                var b = UiKit.Button(c, mod.Title, () => Open(mod), Theme.Green, 44);
                UiKit.Place(b, new Vector2(.09f, 1), new Vector2(.91f, 1), new Vector2(0, y - 140), new Vector2(0, y));
                var lt = (RectTransform)b.transform.Find("Text"); lt.offsetMin = new Vector2(0, 38);
                var st = UiKit.Label(b.transform, mod.Subtitle, 25, Theme.Dim, TextAnchor.LowerCenter, FontStyle.Normal);
                st.rectTransform.offsetMin = new Vector2(0, 22);
                b.interactable = mod.Available;
                y -= 160;
            }
            var snd = UiKit.Button(c, Sfx.On ? "Ton: an" : "Ton: aus", null, Theme.Grey, 28);
            snd.onClick.AddListener(() => { Sfx.On = !Sfx.On; UiKit.SetText(snd, Sfx.On ? "Ton: an" : "Ton: aus"); });
            UiKit.Place(snd, new Vector2(.09f, 1), new Vector2(.48f, 1), new Vector2(0, y - 84), new Vector2(0, y - 6));
            var quit = UiKit.Button(c, "Beenden", Application.Quit, Theme.Grey, 28);
            UiKit.Place(quit, new Vector2(.52f, 1), new Vector2(.91f, 1), new Vector2(0, y - 84), new Vector2(0, y - 6));
            var ver = UiKit.Label(c, "Version " + Application.version, 22, new Color(1, 1, 1, .35f), TextAnchor.LowerCenter, FontStyle.Normal);
            UiKit.Place(ver, Vector2.zero, new Vector2(1, 0), new Vector2(0, 14), new Vector2(0, 50));
        }
    }
}
