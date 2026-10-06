using System;
using System.Collections.Generic;
using System.Linq;
using RommeCup.Core;

namespace RommeCup.Karten
{
    public class CPlayer
    {
        public string Name;
        public bool Ai, Online = true, Melded;
        public List<int> Hand = new List<int>();
        public int Score, LastDelta;
    }

    public class CardGame
    {
        const string M = "CardGame";
        public readonly List<CPlayer> P = new List<CPlayer>();
        public List<int> Stock = new List<int>(), Discard = new List<int>();
        public List<List<int>> Table = new List<List<int>>();
        public int Current, Round, Rounds = 3, Winner = -1, Took = -1, Actor = -1, Mult = 1, Turns, Dealer = -1;
        public bool Drawn, RoundOver, MatchOver, MeldedAtStart;
        public string LastEvent = "";
        public readonly HashSet<int> TurnJokers = new HashSet<int>();
        readonly Random rnd = new Random();
        List<List<int>> snapTable;
        List<int> snapHand;
        bool snapMelded;

        public int HandValue(int i) => Cards.HandValue(P[i].Hand);

        public void StartRound()
        {
            Round++;
            RoundOver = false; MatchOver = false; Winner = -1; Mult = 1; Turns = 0;
            Table.Clear(); Discard.Clear(); TurnJokers.Clear();
            Stock = Shuffle(Enumerable.Range(0, Cards.Count).ToList());
            foreach (var p in P) { p.Hand.Clear(); p.Melded = false; p.LastDelta = 0; }
            for (int k = 0; k < Cards.HandSize; k++) foreach (var p in P) p.Hand.Add(Pop());
            Discard.Add(Pop());
            Dealer = Dealer < 0 ? rnd.Next(P.Count) : (Dealer + 1) % P.Count;
            Current = (Dealer + 1) % P.Count;
            BeginTurn();
            LastEvent = P[Dealer].Name + " hat gegeben - " + P[Current].Name + " beginnt.";
            Log.I(M, "round " + Round + " players=" + P.Count);
        }

        List<int> Shuffle(List<int> d)
        {
            for (int i = d.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); (d[i], d[j]) = (d[j], d[i]); }
            return d;
        }

        int Pop() { int c = Stock[Stock.Count - 1]; Stock.RemoveAt(Stock.Count - 1); return c; }

        void BeginTurn()
        {
            Drawn = false; Took = -1; TurnJokers.Clear(); snapTable = null; snapHand = null;
            MeldedAtStart = P[Current].Melded;
        }

        void Snap()
        {
            snapTable = Table.Select(m => new List<int>(m)).ToList();
            snapHand = new List<int>(P[Current].Hand);
            snapMelded = P[Current].Melded;
        }

        string Gate(int s, bool needDrawn)
        {
            if (RoundOver) return "Die Runde ist beendet.";
            if (s != Current) return "Du bist nicht am Zug.";
            if (needDrawn && !Drawn) return "Ziehe zuerst eine Karte.";
            if (!needDrawn && Drawn) return "Du hast bereits gezogen.";
            return null;
        }

        public string DrawStock(int s)
        {
            var e = Gate(s, false);
            if (e != null) return e;
            if (Stock.Count == 0 && Discard.Count > 1)
            {
                var top = Discard[Discard.Count - 1];
                Stock = Shuffle(Discard.Take(Discard.Count - 1).ToList());
                Discard = new List<int> { top };
                LastEvent = "Der Stapel wurde neu gemischt.";
            }
            if (Stock.Count == 0) return "Der Stapel ist leer - nimm die Ablagekarte.";
            P[s].Hand.Add(Pop());
            Drawn = true; Actor = s; Snap();
            LastEvent = P[s].Name + " zieht vom Stapel.";
            return null;
        }

        public string DrawDiscard(int s)
        {
            var e = Gate(s, false);
            if (e != null) return e;
            if (Discard.Count == 0) return "Die Ablage ist leer.";
            Took = Discard[Discard.Count - 1];
            Discard.RemoveAt(Discard.Count - 1);
            P[s].Hand.Add(Took);
            Drawn = true; Actor = s; Snap();
            LastEvent = P[s].Name + " nimmt " + Cards.Name(Took) + " von der Ablage.";
            return null;
        }

        public string Meld(int s, List<List<int>> groups)
        {
            var e = Gate(s, true);
            if (e != null) return e;
            var p = P[s];
            var all = groups.SelectMany(g => g).ToList();
            if (groups.Count == 0 || all.Distinct().Count() != all.Count || all.Any(c => !p.Hand.Contains(c))) return "Diese Karten sind nicht in deiner Hand.";
            int pts = 0; var infos = new List<MeldInfo>();
            foreach (var g in groups)
            {
                var mi = Cards.Arrange(g);
                if (mi == null) return "Keine gültige Kombination.";
                infos.Add(mi); pts += mi.Points;
            }
            if (!p.Melded && pts < Cards.InitialMeld) return "Erstauslage: mindestens " + Cards.InitialMeld + " Punkte nötig (aktuell " + pts + ").";
            foreach (var c in all) p.Hand.Remove(c);
            foreach (var mi in infos) Table.Add(mi.Cards);
            p.Melded = true; Actor = s;
            LastEvent = p.Name + " legt " + all.Count + " Karten aus (" + pts + " Punkte).";
            CheckWin(s);
            return null;
        }

        public string LayOff(int s, int meld, List<int> cards)
        {
            var e = Gate(s, true);
            if (e != null) return e;
            var p = P[s];
            if (!p.Melded) return "Erst die Erstauslage machen.";
            if (meld < 0 || meld >= Table.Count || cards.Count == 0 || cards.Distinct().Count() != cards.Count || cards.Any(c => !p.Hand.Contains(c))) return "Ungültige Auswahl.";
            var mi = Cards.Arrange(Table[meld], cards);
            if (mi == null) return "Das passt dort nicht an.";
            foreach (var c in cards) p.Hand.Remove(c);
            Table[meld] = mi.Cards; Actor = s;
            LastEvent = p.Name + " legt " + cards.Count + " Karte(n) an.";
            CheckWin(s);
            return null;
        }

        public string Swap(int s, int meld, int joker, int with)
        {
            var e = Gate(s, true);
            if (e != null) return e;
            var p = P[s];
            if (!p.Melded) return "Erst die Erstauslage machen.";
            if (meld < 0 || meld >= Table.Count || !p.Hand.Contains(with)) return "Ungültige Auswahl.";
            var mi = Cards.Swapped(Table[meld], joker, with);
            if (mi == null) return "Dieser Joker lässt sich so nicht tauschen.";
            p.Hand.Remove(with); p.Hand.Add(joker);
            Table[meld] = mi.Cards; TurnJokers.Add(joker); Actor = s;
            LastEvent = p.Name + " tauscht einen Joker.";
            return null;
        }

        public string Discarding(int s, int card)
        {
            var e = Gate(s, true);
            if (e != null) return e;
            var p = P[s];
            if (!p.Hand.Contains(card)) return "Diese Karte hast du nicht.";
            if (Cards.IsJoker(card) && p.Hand.Any(c => !Cards.IsJoker(c))) return "Joker dürfen nicht abgelegt werden.";
            if (card == Took && p.Hand.Count > 1) return "Die genommene Ablagekarte darf nicht sofort zurück.";
            p.Hand.Remove(card);
            Discard.Add(card); Actor = s;
            LastEvent = p.Name + " legt " + Cards.Name(card) + " ab.";
            if (p.Hand.Count == 0) { Finish(s); return null; }
            Turns++;
            if (Turns > 260) { Blocked(); return null; }
            Current = (Current + 1) % P.Count;
            BeginTurn();
            return null;
        }

        public string Undo(int s)
        {
            var e = Gate(s, true);
            if (e != null) return e;
            if (snapHand == null) return "Nichts zurückzusetzen.";
            Table = snapTable.Select(m => new List<int>(m)).ToList();
            P[s].Hand = new List<int>(snapHand);
            P[s].Melded = snapMelded;
            TurnJokers.Clear();
            LastEvent = P[s].Name + " setzt den Zug zurück.";
            return null;
        }

        void CheckWin(int s) { if (P[s].Hand.Count == 0) Finish(s); }

        void Blocked()
        {
            int best = Enumerable.Range(0, P.Count).OrderBy(HandValue).First();
            Finish(best);
            LastEvent = "Zu viele Züge - " + P[best].Name + " hat die wenigsten Punkte.";
        }

        void Finish(int w)
        {
            RoundOver = true; Winner = w;
            Mult = P[w].Hand.Count == 0 && !MeldedAtStart && Current == w ? 2 : 1;
            int sum = 0;
            for (int i = 0; i < P.Count; i++)
            {
                if (i == w) continue;
                int v = HandValue(i) * Mult;
                P[i].LastDelta = -v; P[i].Score -= v; sum += v;
            }
            P[w].LastDelta = sum; P[w].Score += sum;
            MatchOver = Round >= Rounds;
            LastEvent = P[w].Name + " gewinnt die Runde" + (Mult == 2 ? " mit einer Rommé-Hand - doppelte Punkte!" : "!");
            Log.I(M, LastEvent);
        }
    }
}
