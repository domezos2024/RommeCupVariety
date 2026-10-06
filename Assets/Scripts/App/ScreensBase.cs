using System.Collections;
using System.Collections.Generic;
using System.Linq;
using RommeCup.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RommeCup.App
{
    [System.Serializable]
    public class PInfo
    {
        public string name;
        public int tiles, score, delta, hand;
        public bool melded, ai, online = true;
    }

    public interface IRoundMsg { PInfo[] Players { get; } int Winner { get; } int Round { get; } int Rounds { get; } string Text { get; } bool Over { get; } }

    public interface ILobby { int Count { get; } string Name(int i); bool Ai(int i); void AddAi(); void RemoveAi(); void BroadcastLobby(); int Rounds { get; set; } }

    public interface ISession
    {
        string Title { get; }
        string Tagline { get; }
        string Unit { get; }
        string[] Rules { get; }
        bool IsHost { get; }
        ILobby Lobby { get; }
        List<KeyValuePair<string, string>> Devices { get; }
        bool Help { get; set; }
        int MySeat { get; }
        bool RoundShowing { get; }
        void StartSolo(int ai, int rounds);
        void HostBt();
        void JoinBt();
        void Rescan();
        void ConnectTo(string addr, string name);
        void StartHostedMatch();
        void NextRound();
        void Leave();
        void RefreshPlay();
    }

    public abstract class ScreensBase
    {
        protected readonly ISession ses;
        protected GameApp App => GameApp.I;
        protected RectTransform S => App.ScreenLayer;
        protected RectTransform Hud => App.HudLayer;
        protected RectTransform Modal => App.ModalLayer;
        protected RectTransform ToastL => App.ToastLayer;
        public bool OnModeSelect { get; private set; }
        public bool ModalOpen => Modal.childCount > 0;
        protected Text status, info, toastText;
        readonly Image[] chips = new Image[4];
        readonly Text[] chipText = new Text[4];
        Image toastBox;
        Coroutine toastCo;
        int rounds = 3, ais = 2;

        protected ScreensBase(ISession s) { ses = s; }

        protected void Reset(bool keepHud = false)
        {
            UiKit.Clear(S); UiKit.Clear(Modal); if (!keepHud) { UiKit.Clear(Hud); status = null; }
            OnModeSelect = false;
        }

        protected RectTransform Card(Vector2 size, string title)
        {
            var img = UiKit.Box(S, Theme.Panel, true, "Card");
            UiKit.Center(img, new Vector2(.5f, .5f), size);
            var t = UiKit.Label(img.transform, title, 54, Theme.Brass, TextAnchor.MiddleCenter, FontStyle.Bold, true);
            UiKit.Place(t, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -100), new Vector2(0, -18));
            var line = UiKit.Shade(img.transform, Theme.Line);
            UiKit.Place(line, new Vector2(.2f, 1), new Vector2(.8f, 1), new Vector2(0, -104), new Vector2(0, -102));
            return (RectTransform)img.transform;
        }

        protected Button Btn(RectTransform p, string text, System.Action a, Color c, float y, float h = 92, float x0 = .1f, float x1 = .9f, int size = 34)
        {
            var b = UiKit.Button(p, text, a, c, size);
            UiKit.Place(b, new Vector2(x0, 1), new Vector2(x1, 1), new Vector2(0, y - h), new Vector2(0, y));
            return b;
        }

        protected Text Lbl(RectTransform p, string text, float y, float h, int size, Color c, TextAnchor a = TextAnchor.MiddleCenter, FontStyle st = FontStyle.Normal)
        {
            var l = UiKit.Label(p, text, size, c, a, st);
            UiKit.Place(l, new Vector2(.06f, 1), new Vector2(.94f, 1), new Vector2(0, y - h), new Vector2(0, y));
            return l;
        }

        protected void Choice(RectTransform p, float y, int[] vals, int cur, System.Action<int> set, string suffix = "")
        {
            var row = UiKit.Rect(p, "Choice", new Vector2(.18f, 1), new Vector2(.82f, 1), new Vector2(0, y - 84), new Vector2(0, y));
            var btns = new Button[vals.Length];
            for (int i = 0; i < vals.Length; i++)
            {
                int v = vals[i];
                btns[i] = UiKit.Button(row, v + suffix, null, v == cur ? Theme.Brass * .8f : Theme.Glass, 34);
                btns[i].onClick.AddListener(() => { set(v); for (int k = 0; k < btns.Length; k++) UiKit.SetColor(btns[k], vals[k] == v ? Theme.Brass * .8f : Theme.Glass); });
            }
            UiKit.Row(row, 16, btns);
        }

        public void ShowModeSelect()
        {
            Reset();
            OnModeSelect = true;
            var c = Card(new Vector2(860, 800), ses.Title);
            Btn(c, "Solo gegen den Computer", ShowSolo, Theme.Green, -140);
            Btn(c, "Bluetooth: Spiel eröffnen", ses.HostBt, Theme.Blue, -250);
            Btn(c, "Bluetooth: Spiel beitreten", ses.JoinBt, Theme.Blue, -360);
            Btn(c, "Spielregeln", () => ShowRules(0), Theme.Grey, -470);
            Btn(c, "Zurück zum Hauptmenü", () => App.ShowMainMenu(), Theme.Grey, -580, 84, .25f, .75f, 30);
            Lbl(c, ses.Tagline, -690, 90, 26, Theme.Dim, TextAnchor.MiddleCenter, FontStyle.Italic);
        }

        void ShowSolo()
        {
            Reset();
            var c = Card(new Vector2(860, 700), "Solo gegen den Computer");
            Lbl(c, "Mitspieler am Tisch", -125, 50, 32, Theme.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            Choice(c, -180, new[] { 1, 2, 3 }, ais, v => ais = v);
            Lbl(c, "Runden", -290, 50, 32, Theme.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            Choice(c, -345, new[] { 1, 3, 5 }, rounds, v => rounds = v);
            Btn(c, "Spiel beginnen", () => ses.StartSolo(ais, rounds), Theme.Green, -470, 100);
            Btn(c, "Zurück", ShowModeSelect, Theme.Grey, -590, 80, .3f, .7f, 30);
        }

        public void ShowBusy(string text)
        {
            Reset();
            var c = Card(new Vector2(860, 400), "Bitte warten");
            Lbl(c, text, -125, 130, 34, Theme.Text);
            Btn(c, "Abbrechen", ses.Leave, Theme.Grey, -280, 84, .3f, .7f, 30);
        }

        public void ShowError(string text)
        {
            Reset();
            var c = Card(new Vector2(960, 460), "Hinweis");
            Lbl(c, text, -120, 190, 34, Theme.Text);
            Btn(c, "OK", ShowModeSelect, Theme.Green, -340, 90, .3f, .7f);
            Sfx.Play("error");
        }

        public void ShowHostLobby()
        {
            var h = ses.Lobby;
            if (h == null) return;
            Reset();
            var c = Card(new Vector2(1060, 940), "Spiel eröffnen");
            Lbl(c, "Dein Gerät ist sichtbar. Mitspieler wählen 'Bluetooth: Spiel beitreten'.", -110, 70, 28, Theme.Dim);
            float y = -190;
            for (int i = 0; i < 4; i++)
            {
                bool used = i < h.Count;
                var slot = UiKit.Box(c, used ? (h.Ai(i) ? Theme.Grey : Theme.Blue) : Theme.Glass, false, "Slot");
                UiKit.Place(slot, new Vector2(.15f, 1), new Vector2(.85f, 1), new Vector2(0, y - 76), new Vector2(0, y));
                UiKit.Label(slot.transform, used ? h.Name(i) + (i == 0 ? "  (Du, Gastgeber)" : "") : "freier Platz", 32, used ? Theme.Text : Theme.Dim, TextAnchor.MiddleCenter, used ? FontStyle.Bold : FontStyle.Italic);
                y -= 90;
            }
            Btn(c, "+ Computer", () => { h.AddAi(); h.BroadcastLobby(); ShowHostLobby(); }, Theme.Grey, y - 10, 80, .15f, .48f, 30);
            Btn(c, "- Computer", () => { h.RemoveAi(); h.BroadcastLobby(); ShowHostLobby(); }, Theme.Grey, y - 10, 80, .52f, .85f, 30);
            Lbl(c, "Runden", y - 105, 46, 30, Theme.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            Choice(c, y - 155, new[] { 1, 3, 5 }, h.Rounds, v => { h.Rounds = v; h.BroadcastLobby(); });
            var start = Btn(c, "Spiel starten", ses.StartHostedMatch, Theme.Green, y - 270, 96, .15f, .62f);
            start.interactable = h.Count >= 2;
            Btn(c, "Abbrechen", ses.Leave, Theme.Grey, y - 270, 96, .66f, .85f, 30);
        }

        public void ShowJoin(string text)
        {
            Reset();
            var c = Card(new Vector2(1060, 940), "Spiel beitreten");
            Lbl(c, text, -110, 66, 30, Theme.Dim);
            float y = -190;
            foreach (var d in ses.Devices.Take(6))
            {
                var dev = d;
                Btn(c, dev.Value, () => ses.ConnectTo(dev.Key, dev.Value), Theme.Blue, y, 80, .12f, .88f, 30);
                y -= 92;
            }
            Lbl(c, "Der Gastgeber muss 'Spiel eröffnen' gewählt haben. Bereits gekoppelte Geräte erscheinen sofort.", -750, 80, 25, Theme.Dim, TextAnchor.MiddleCenter, FontStyle.Italic);
            Btn(c, "Neu suchen", ses.Rescan, Theme.Blue, -840, 80, .15f, .48f, 30);
            Btn(c, "Abbrechen", ses.Leave, Theme.Grey, -840, 80, .52f, .85f, 30);
        }

        public void ShowClientLobby(PInfo[] players, int rnds)
        {
            Reset();
            var c = Card(new Vector2(960, 740), "Warteraum");
            float y = -130;
            foreach (var p in players)
            {
                var slot = UiKit.Box(c, p.ai ? Theme.Grey : Theme.Blue, false, "Slot");
                UiKit.Place(slot, new Vector2(.15f, 1), new Vector2(.85f, 1), new Vector2(0, y - 76), new Vector2(0, y));
                UiKit.Label(slot.transform, p.name, 32, Theme.Text);
                y -= 90;
            }
            Lbl(c, "Runden: " + rnds + "   -   Der Gastgeber startet das Spiel.", y - 20, 60, 30, Theme.Dim);
            Btn(c, "Verlassen", ses.Leave, Theme.Grey, -630, 80, .3f, .7f, 30);
        }

        public void ShowRules(int page)
        {
            var pages = ses.Rules;
            UiKit.Clear(Modal);
            UiKit.Modal(Modal, new Vector2(1280, 860), out var c);
            var t = UiKit.Label(c, "Spielregeln", 46, Theme.Brass, TextAnchor.MiddleCenter, FontStyle.Bold, true);
            UiKit.Place(t, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -90), new Vector2(0, -18));
            var pg = UiKit.Label(c, (page + 1) + " / " + pages.Length, 26, Theme.Dim, TextAnchor.MiddleRight, FontStyle.Normal);
            UiKit.Place(pg, new Vector2(.7f, 1), new Vector2(.95f, 1), new Vector2(0, -80), new Vector2(0, -30));
            var body = UiKit.Label(c, pages[page], 32, Theme.Text, TextAnchor.UpperLeft, FontStyle.Normal);
            body.lineSpacing = 1.12f;
            UiKit.Place(body, new Vector2(.06f, 0), new Vector2(.94f, 1), new Vector2(0, 140), new Vector2(0, -110));
            Btn(c, "<", () => ShowRules((page + pages.Length - 1) % pages.Length), Theme.Grey, -730, 90, .05f, .17f, 44);
            Btn(c, ">", () => ShowRules((page + 1) % pages.Length), Theme.Grey, -730, 90, .19f, .31f, 44);
            Button help = null;
            help = Btn(c, HelpText(), () => { ses.Help = !ses.Help; UiKit.SetText(help, HelpText()); ses.RefreshPlay(); }, Theme.Blue, -730, 90, .35f, .7f, 30);
            Btn(c, "Schließen", () => UiKit.Clear(Modal), Theme.Green, -730, 90, .73f, .95f, 30);
        }

        string HelpText() => "Spielhilfe: " + (ses.Help ? "an" : "aus");

        public bool CloseModal()
        {
            if (!ModalOpen || ses.RoundShowing) return false;
            UiKit.Clear(Modal);
            return true;
        }

        public void ConfirmLeave()
        {
            UiKit.Clear(Modal);
            UiKit.Modal(Modal, new Vector2(860, 400), out var c);
            Lbl(c, "Partie wirklich verlassen?", -60, 110, 42, Theme.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            Btn(c, "Verlassen", () => { UiKit.Clear(Modal); ses.Leave(); }, Theme.Red, -240, 96, .1f, .48f);
            Btn(c, "Weiterspielen", () => UiKit.Clear(Modal), Theme.Green, -240, 96, .52f, .9f);
        }

        public void Toast(string text)
        {
            if (toastBox == null)
            {
                toastBox = UiKit.Box(ToastL, new Color(.06f, .05f, .045f, .92f), true, "Toast");
                UiKit.Center(toastBox, new Vector2(.42f, .5f), new Vector2(1040, 92));
                toastText = UiKit.Label(toastBox.transform, "", 32, Theme.Text);
                toastBox.raycastTarget = false;
            }
            toastText.text = text;
            toastBox.gameObject.SetActive(true);
            if (toastCo != null) App.StopCoroutine(toastCo);
            toastCo = App.StartCoroutine(HideToast());
        }

        IEnumerator HideToast() { yield return new WaitForSecondsRealtime(2.6f); if (toastBox) toastBox.gameObject.SetActive(false); }

        public void HideRound() { if (ModalOpen && !ses.RoundShowing) UiKit.Clear(Modal); }

        protected RectTransform BuildTop()
        {
            var top = UiKit.Rect(Hud, "TopBar", new Vector2(0, 1), new Vector2(.835f, 1), new Vector2(14, -108), new Vector2(0, -12));
            var ib = UiKit.Box(top, Theme.Dark, false, "Info");
            UiKit.Place(ib, Vector2.zero, new Vector2(.15f, 1));
            info = UiKit.Label(ib.transform, "", 26, Theme.Text, TextAnchor.MiddleLeft, FontStyle.Normal);
            info.rectTransform.offsetMin = new Vector2(16, 0);
            var row = UiKit.Rect(top, "Chips", new Vector2(.16f, 0), Vector2.one);
            for (int i = 0; i < 4; i++)
            {
                chips[i] = UiKit.Box(row, Theme.Dark, false, "Chip");
                chipText[i] = UiKit.Label(chips[i].transform, "", 27, Theme.Text);
                chips[i].gameObject.SetActive(false);
            }
            var stBox = UiKit.Box(Hud, Theme.Dark, false, "Status");
            UiKit.Place(stBox, new Vector2(.12f, 1), new Vector2(.835f, 1), new Vector2(0, -166), new Vector2(0, -118));
            status = UiKit.Label(stBox.transform, "", 28, Theme.Text);
            var side = UiKit.Rect(Hud, "Side", new Vector2(.845f, 0), new Vector2(1, 1), new Vector2(0, 12), new Vector2(-12, -12));
            return side;
        }

        protected void UpdateTop(string infoText, PInfo[] players, int current, bool live, int seat)
        {
            info.text = infoText;
            int n = players.Length;
            var act = new List<Component>();
            for (int i = 0; i < 4; i++)
            {
                bool on = i < n;
                chips[i].gameObject.SetActive(on);
                if (!on) continue;
                act.Add(chips[i]);
                var p = players[i];
                bool cur = live && i == current;
                chips[i].color = cur ? new Color(.86f, .71f, .42f, .95f) : Theme.Dark;
                var col = cur ? "#1C1712" : "#F5EEDC";
                chipText[i].text = "<color=" + col + ">" + (i == seat ? "<b>" + p.name + "</b> (Du)" : p.name) + (p.online ? "" : " (getrennt)") + "</color>\n<size=22><color=" + col + ">" + p.tiles + " " + ses.Unit + "  ·  " + p.score + " Pkt" + (p.melded ? "" : "  ·  ohne Auslage") + "</color></size>";
                chipText[i].GetComponent<Shadow>().enabled = !cur;
            }
            UiKit.Row((RectTransform)chips[0].transform.parent, 12, act.ToArray());
        }

        void Scores(RectTransform c, IRoundMsg m, float y, bool total)
        {
            var order = Enumerable.Range(0, m.Players.Length).OrderByDescending(i => total ? m.Players[i].score : m.Players[i].delta).ToArray();
            var head = UiKit.Rect(c, "Head", new Vector2(.06f, 1), new Vector2(.94f, 1), new Vector2(0, y - 44), new Vector2(0, y));
            Col(head, "Name", 0, .4f, TextAnchor.MiddleLeft, Theme.Dim, 26, 24);
            Col(head, "Rest", .4f, .56f, TextAnchor.MiddleCenter, Theme.Dim, 26);
            Col(head, "Runde", .56f, .76f, TextAnchor.MiddleCenter, Theme.Dim, 26);
            Col(head, "Gesamt", .76f, .97f, TextAnchor.MiddleCenter, Theme.Dim, 26);
            y -= 50;
            foreach (var i in order)
            {
                var p = m.Players[i];
                bool top = i == m.Winner || (total && i == order[0]);
                var row = UiKit.Box(c, top ? new Color(.86f, .71f, .42f, .28f) : Theme.Glass, top, "Row");
                UiKit.Place(row, new Vector2(.06f, 1), new Vector2(.94f, 1), new Vector2(0, y - 66), new Vector2(0, y));
                var rt = (RectTransform)row.transform;
                Col(rt, p.name + (i == ses.MySeat ? " (Du)" : ""), 0, .4f, TextAnchor.MiddleLeft, Theme.Text, 30, 24);
                Col(rt, p.hand.ToString(), .4f, .56f, TextAnchor.MiddleCenter, Theme.Text, 30);
                Col(rt, (p.delta > 0 ? "+" : "") + p.delta, .56f, .76f, TextAnchor.MiddleCenter, p.delta >= 0 ? Theme.Good : Theme.Bad, 30);
                Col(rt, p.score.ToString(), .76f, .97f, TextAnchor.MiddleCenter, Theme.Brass, 34);
                y -= 76;
            }
        }

        static void Col(RectTransform p, string t, float a, float b, TextAnchor anchor, Color c, int size, float pad = 0)
        {
            var l = UiKit.Label(p, t, size, c, anchor, FontStyle.Bold);
            UiKit.Place(l, new Vector2(a, 0), new Vector2(b, 1), new Vector2(pad, 0), Vector2.zero);
        }

        public void ShowRoundEnd(IRoundMsg m)
        {
            UiKit.Clear(Modal);
            UiKit.Modal(Modal, new Vector2(1160, 780), out var c);
            bool me = m.Winner == ses.MySeat;
            var t = UiKit.Label(c, me ? "Runde gewonnen" : m.Players[m.Winner].name + " gewinnt Runde " + m.Round, 50, Theme.Brass, TextAnchor.MiddleCenter, FontStyle.Bold, true);
            UiKit.Place(t, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -96), new Vector2(0, -18));
            Lbl(c, m.Text, -98, 48, 28, Theme.Dim);
            Scores(c, m, -160, false);
            if (ses.IsHost) Btn(c, "Nächste Runde (" + (m.Round + 1) + " von " + m.Rounds + ")", ses.NextRound, Theme.Green, -670, 92, .22f, .78f);
            else Lbl(c, "Warte auf den Gastgeber ...", -670, 92, 32, Theme.Text);
        }

        public void ShowMatchEnd(IRoundMsg m, int champ)
        {
            UiKit.Clear(Modal);
            UiKit.Modal(Modal, new Vector2(1160, 860), out var c);
            bool me = champ == ses.MySeat;
            var t = UiKit.Label(c, me ? "Gewonnen!" : m.Players[champ].name + " gewinnt die Partie", me ? 72 : 54, Theme.Brass, TextAnchor.MiddleCenter, FontStyle.Bold, true);
            UiKit.Place(t, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -110), new Vector2(0, -18));
            Lbl(c, "Endstand nach " + m.Rounds + (m.Rounds == 1 ? " Runde" : " Runden") + "  ·  " + m.Text, -112, 48, 28, Theme.Dim);
            Scores(c, m, -175, true);
            var bar = UiKit.Rect(c, "Buttons", new Vector2(.1f, 0), new Vector2(.9f, 0), new Vector2(0, 40), new Vector2(0, 134));
            var list = new List<Component>();
            if (ses.IsHost) list.Add(UiKit.Button(bar, "Neue Partie", ses.NextRound, Theme.Green, 34));
            list.Add(UiKit.Button(bar, "Zum Spielmenü", ses.Leave, Theme.Grey, 34));
            UiKit.Row(bar, 24, list.ToArray());
        }
    }
}
