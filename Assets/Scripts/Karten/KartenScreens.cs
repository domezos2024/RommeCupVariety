using RommeCup.App;
using RommeCup.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RommeCup.Karten
{
    public class KartenScreens : ScreensBase
    {
        readonly KartenModule mod;
        Button bStock, bTake, bLay, bConfirm, bDiscard, bReset, bHint;

        public static readonly string[] RulePages =
        {
            "<b>Ziel</b>\n\nWer zuerst alle Karten losgeworden ist, gewinnt die Runde. Gespielt wird mit 110 Karten: zwei französische Blätter zu je 52 Karten (rote und blaue Rückseite) und 6 Joker. Der Geber teilt jedem 13 Karten aus und deckt eine Karte als Ablage auf. Links vom Geber wird begonnen.",
            "<b>Ein Zug</b>\n\n1. Ziehe die oberste Karte vom Stapel oder nimm die oberste Karte der Ablage (antippen).\n2. Lege nach Wunsch Kombinationen aus oder an.\n3. Lege zum Schluss genau eine Karte auf die Ablage - das beendet den Zug. Die gerade von der Ablage genommene Karte darf nicht sofort zurück.",
            "<b>Kombinationen</b>\n\n<b>Reihe (Folge):</b> mindestens 3 aufeinanderfolgende Karten derselben Farbe (z. B. Herz 4-5-6). Das Ass liegt unten (A-2-3) oder oben (D-K-A), aber nicht 'um die Ecke'.\n\n<b>Satz (Gruppe):</b> 3 oder 4 Karten gleichen Werts in verschiedenen Farben (z. B. Pik 9, Herz 9, Kreuz 9).\n\nJoker ersetzen jede Karte, höchstens ein Joker pro Kombination.",
            "<b>Erstauslage</b>\n\nDas erste Auslegen muss zusammen mindestens <b>40 Punkte</b> ergeben. Dafür dürfen mehrere Kombinationen genutzt werden: Karten wählen, 'Auslegen' tippen (wird vorgemerkt), mit 'Erstauslage' bestätigen.\n\nPunkte: Ass 11 (in A-2-3 nur 1), Bube, Dame, König und 10 je 10, sonst Augenzahl. Ein Joker zählt wie die ersetzte Karte.",
            "<b>Anlegen und Joker tauschen</b>\n\nNach der Erstauslage darfst du Karten an alle Kombinationen auf dem Tisch anlegen: Karten wählen und die Kombination antippen.\n\nEinen Joker auf dem Tisch kannst du gegen die passende Karte aus der Hand tauschen. Der Joker kommt auf die Hand und darf nicht abgelegt werden - er muss ausgespielt werden.",
            "<b>Rundenende und Wertung</b>\n\nDie Runde endet, sobald jemand keine Karten mehr hat. Alle anderen verlieren die Punkte ihrer Restkarten (Joker 20), der Sieger erhält die Summe.\n\n<b>Hand-Rommé:</b> Wer alle Karten in einem einzigen Zug ohne vorherige Auslage loswird, erhält doppelte Punkte.\n\nNach der letzten Runde gewinnt der höchste Gesamtstand.",
            "<b>Bedienung</b>\n\n- Karten antippen wählt sie aus (sie heben sich an).\n- 'Auslegen' legt die Auswahl als Kombination aus.\n- Eine Tischkombination antippen legt die Auswahl dort an.\n- 'Ablegen' beendet den Zug mit der gewählten Karte.\n- 'Zurücknehmen' macht den Zug rückgängig.\n- 'Tipp' zeigt einen möglichen Zug.\n- <b>Spielhilfe:</b> gültige Auswahl schimmert grün.",
        };

        public KartenScreens(KartenModule m) : base(m) { mod = m; }

        public void ShowHud()
        {
            Reset();
            var p = mod.Play;
            var side = BuildTop();
            var st = new UiKit.Stack(side, 9);
            bStock = st.Add(UiKit.Button(side, "Vom Stapel ziehen", p.DrawStock, Theme.Blue, 27), 80);
            bTake = st.Add(UiKit.Button(side, "Ablage nehmen", p.TakeDiscard, Theme.Blue, 27), 80);
            bLay = st.Add(UiKit.Button(side, "Auslegen", p.Lay, Theme.Green, 30), 88);
            bConfirm = st.Add(UiKit.Button(side, "Erstauslage", p.ConfirmInitial, Theme.Green, 26), 80);
            bDiscard = st.Add(UiKit.Button(side, "Ablegen", p.Discard, new Color(.4f, .3f, .16f), 30), 88);
            bReset = st.Add(UiKit.Button(side, "Zurücknehmen", p.ResetTurn, Theme.Grey, 25), 68);
            bHint = st.Add(UiKit.Button(side, "Tipp", p.Hint, Theme.Grey, 25), 68);
            var sort = st.Add(UiKit.Rect(side, "Sort", Vector2.zero, Vector2.one), 68);
            UiKit.Row(sort, 10, UiKit.Button(sort, "Farbe", () => p.Sort(true), Theme.Grey, 25), UiKit.Button(sort, "Wert", () => p.Sort(false), Theme.Grey, 25));
            st.Skip(10);
            st.Add(UiKit.Button(side, "Regeln", () => ShowRules(0), Theme.Grey, 24), 60);
            st.Add(UiKit.Button(side, "Menü", ConfirmLeave, Theme.Grey, 24), 60);
        }

        public void UpdateHud(KartenPlay p)
        {
            if (status == null || p.St == null) return;
            var st = p.St;
            UpdateTop("Runde " + st.round + " / " + st.rounds + "\nStapel: " + st.pool, st.players, st.current, st.t == "state", p.Seat);
            bool draw = p.MyDraw, play = p.MyPlay;
            bStock.interactable = draw && st.pool > 0;
            bTake.interactable = draw && st.dcount > 0;
            bLay.interactable = play && p.SelCount >= 3;
            bConfirm.interactable = play && !p.Melded && p.HasStaged && p.StagedPoints >= Cards.InitialMeld;
            UiKit.SetText(bConfirm, play && !p.Melded && p.HasStaged ? "Erstauslage " + p.StagedPoints + "/" + Cards.InitialMeld : "Erstauslage");
            bDiscard.interactable = play && p.SelCount == 1;
            bReset.interactable = play;
            bHint.interactable = draw || play;
            bHint.gameObject.SetActive(KartenPlay.Help);
            if (st.t != "state") status.text = st.text;
            else if (draw) status.text = "Du bist am Zug - tippe auf den Stapel oder die Ablage";
            else if (play)
            {
                if (p.SelCount > 0 && KartenPlay.Help) status.text = p.SelValid ? "Auswahl: gültige Kombination (" + p.SelPoints + " Punkte)" : "Auswahl: " + p.SelCount + (p.SelCount == 1 ? " Karte" : " Karten");
                else status.text = p.Melded ? "Lege aus oder an - 'Ablegen' beendet den Zug" : "Erstauslage: " + p.StagedPoints + " von " + Cards.InitialMeld + " Punkten";
            }
            else status.text = st.players[st.current].name + " ist am Zug  ·  " + st.text;
        }
    }
}
