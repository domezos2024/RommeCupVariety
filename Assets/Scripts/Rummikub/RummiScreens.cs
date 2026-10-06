using RommeCup.App;
using RommeCup.Core;
using UnityEngine.UI;

namespace RommeCup.Rummikub
{
    public class RummiScreens : ScreensBase
    {
        readonly RummiModule mod;
        Button bCommit, bDraw, bReset, bHint;

        public static readonly string[] RulePages =
        {
            "<b>Ziel</b>\n\nWer zuerst alle Steine von seiner Bank ablegt, gewinnt die Runde. Gespielt wird mit 106 Steinen: die Zahlen 1 bis 13 in den vier Farben Schwarz, Rot, Blau und Orange, jede zweimal, dazu 2 Joker. Die Steine liegen verdeckt im Vorrat, jede Person zieht 14 Steine. Wer den höchsten Stein zieht, beginnt.",
            "<b>Kombinationen</b>\n\n<b>Gruppe:</b> 3 oder 4 Steine mit gleicher Zahl in verschiedenen Farben (z. B. 7 Schwarz, 7 Rot, 7 Blau).\n\n<b>Reihe:</b> mindestens 3 aufeinanderfolgende Zahlen derselben Farbe (z. B. 4-5-6 Blau). Die 1 folgt nicht auf die 13.\n\nAuf dem Tisch bilden nebeneinander liegende Steine eine Kombination, eine Lücke trennt Kombinationen.",
            "<b>Erstauslage</b>\n\nDas erste Auslegen muss zusammen mindestens <b>30 Punkte</b> ergeben (Summe der Zahlen) und darf nur aus Steinen der eigenen Bank bestehen. Ein Joker zählt den Wert des Steins, den er ersetzt.\n\nVor der Erstauslage darf nichts an den Tisch angelegt oder umgebaut werden.",
            "<b>Anlegen und Umbauen</b>\n\nNach der Erstauslage darfst du an Tisch-Kombinationen anlegen und den Tisch frei umbauen: Reihen teilen, Steine verschieben, Gruppen erweitern. Am Ende deines Zuges müssen alle Kombinationen gültig sein und du musst mindestens einen eigenen Stein gelegt haben.\n\nSteine vom Tisch dürfen nie zurück auf die Bank.",
            "<b>Joker</b>\n\nDer Joker ersetzt jeden Stein. Nach der Erstauslage darfst du einen Joker vom Tisch durch den passenden Stein ersetzen, musst ihn aber im selben Zug wieder ausspielen. Ein Joker auf der Bank kostet am Rundenende 30 Punkte.",
            "<b>Ziehen und Rundenende</b>\n\nWer nicht legen kann oder will, zieht einen Stein aus dem Vorrat - damit endet der Zug. Ist der Vorrat leer, wird gepasst. Passen alle reihum, gewinnt die Person mit dem geringsten Bankwert.\n\n<b>Wertung:</b> Die anderen verlieren die Summe ihrer Reststeine, der Sieger erhält die Summe dieser Punkte. Nach der letzten Runde gewinnt der höchste Gesamtstand.",
            "<b>Bedienung</b>\n\n- Steine mit dem Finger ziehen und ablegen.\n- Auf einen besetzten Platz ziehen schiebt die Steine nach rechts.\n- 'Farbe' / 'Zahl' sortiert die Bank.\n- 'Tipp' zeigt einen möglichen Zug.\n- 'Zurücknehmen' holt gelegte Steine zurück.\n- <b>Spielhilfe:</b> ungültige Kombinationen schimmern rot, gültige neue grün.",
        };

        public RummiScreens(RummiModule m) : base(m) { mod = m; }

        public void ShowHud()
        {
            Reset();
            var side = BuildTop();
            var st = new UiKit.Stack(side, 12);
            bCommit = st.Add(UiKit.Button(side, "Zug beenden", mod.Play.Commit, Theme.Green, 36), 118);
            bDraw = st.Add(UiKit.Button(side, "Stein ziehen", mod.Play.Draw, Theme.Blue, 32), 96);
            bReset = st.Add(UiKit.Button(side, "Zurücknehmen", mod.Play.ResetTurn, Theme.Grey, 28), 80);
            bHint = st.Add(UiKit.Button(side, "Tipp", mod.Play.Hint, Theme.Grey, 28), 80);
            var sort = st.Add(UiKit.Rect(side, "Sort", UnityEngine.Vector2.zero, UnityEngine.Vector2.one), 80);
            UiKit.Row(sort, 10, UiKit.Button(sort, "Farbe", () => mod.Play.Sort(true), Theme.Grey, 28), UiKit.Button(sort, "Zahl", () => mod.Play.Sort(false), Theme.Grey, 28));
            st.Skip(18);
            st.Add(UiKit.Button(side, "Regeln", () => ShowRules(0), Theme.Grey, 26), 66);
            st.Add(UiKit.Button(side, "Menü", ConfirmLeave, Theme.Grey, 26), 66);
        }

        public void UpdateHud(RummiPlay p)
        {
            if (status == null || p.St == null) return;
            var st = p.St;
            UpdateTop("Runde " + st.round + " / " + st.rounds + "\nVorrat: " + st.pool, st.players, st.current, st.t == "state", p.Seat);
            bool my = p.MyTurn;
            bCommit.interactable = my && p.PlacedCount > 0;
            bDraw.interactable = my;
            bReset.interactable = my && p.PlacedCount > 0;
            bHint.interactable = my;
            bHint.gameObject.SetActive(RummiPlay.Help);
            UiKit.SetText(bDraw, st.pool > 0 ? "Stein ziehen" : "Passen");
            if (st.t != "state") status.text = st.text;
            else if (my) status.text = p.Melded ? (p.PlacedCount > 0 ? "Gelegt: " + p.PlacedCount + " - 'Zug beenden', wenn alles gültig ist" : "Du bist am Zug - lege Steine oder ziehe einen") : "Erstauslage: " + p.MeldPoints + " von 30 Punkten";
            else status.text = st.players[st.current].name + " ist am Zug  ·  " + st.text;
        }
    }
}
