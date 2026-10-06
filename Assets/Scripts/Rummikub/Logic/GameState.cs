using System.Collections.Generic;
using System.Linq;
using RommeCup.Core;

namespace RommeCup.Rummikub
{
    public class Player
    {
        public string Name;
        public readonly List<int> Rack = new List<int>();
        public bool Melded, Ai, Online = true;
        public int Score, LastDelta;
    }

    public class GameState
    {
        const string M = "GameState";
        public readonly List<Player> P = new List<Player>();
        public List<int> Pool = new List<int>();
        public int[] Table = Board.Empty();
        public int Current, Round, Rounds = 1, Passes, Winner = -1, StartSeat = -1;
        public bool Blocked, RoundOver;
        public string LastEvent = "";
        readonly System.Random rng = new System.Random();

        public bool MatchOver => RoundOver && Round >= Rounds;

        public void StartRound()
        {
            Round++;
            Pool = Enumerable.Range(0, Tiles.Count).OrderBy(_ => rng.Next()).ToList();
            Table = Board.Empty();
            foreach (var p in P)
            {
                p.Rack.Clear(); p.Melded = false; p.LastDelta = 0;
                for (int i = 0; i < 14; i++) p.Rack.Add(Take());
            }
            if (StartSeat < 0) StartSeat = rng.Next(P.Count);
            Current = (StartSeat + Round - 1) % P.Count; Passes = 0; Winner = -1; Blocked = false; RoundOver = false;
            LastEvent = Round == 1 ? P[Current].Name + " hat den höchsten Stein gezogen und beginnt." : "Runde " + Round + " - " + P[Current].Name + " beginnt.";
            Log.I(M, "round=" + Round + " start=" + Current);
        }

        int Take() { var t = Pool[^1]; Pool.RemoveAt(Pool.Count - 1); return t; }

        public int HandValue(int s) => P[s].Rack.Sum(Tiles.Penalty);

        public string Commit(int seat, int[] cells)
        {
            if (RoundOver || seat != Current) return "Du bist nicht am Zug.";
            if (cells == null || cells.Length != Board.Size) return "Ungültige Daten.";
            var p = P[seat];
            var err = Rules.Validate(Table, cells, p.Rack, p.Melded, out var placed, out _);
            if (err != null) { Log.I(M, "reject seat=" + seat + " " + err); return err; }
            var nc = (int[])cells.Clone();
            Board.Normalize(nc);
            Table = nc;
            foreach (var t in placed) p.Rack.Remove(t);
            p.Melded = true; Passes = 0;
            LastEvent = p.Name + " hat " + placed.Count + (placed.Count == 1 ? " Stein" : " Steine") + " gelegt.";
            Log.I(M, LastEvent);
            if (p.Rack.Count == 0) EndRound(seat, false); else Next();
            return null;
        }

        public void Draw(int seat)
        {
            if (RoundOver || seat != Current) return;
            if (Pool.Count > 0) { P[seat].Rack.Add(Take()); Passes = 0; LastEvent = P[seat].Name + " hat einen Stein gezogen."; }
            else
            {
                Passes++; LastEvent = P[seat].Name + " passt.";
                if (Passes >= P.Count) { EndBlocked(); return; }
            }
            Next();
        }

        void Next() => Current = (Current + 1) % P.Count;

        void EndBlocked()
        {
            int w = 0;
            for (int i = 1; i < P.Count; i++) if (HandValue(i) < HandValue(w)) w = i;
            EndRound(w, true);
        }

        void EndRound(int w, bool blocked)
        {
            int wv = blocked ? HandValue(w) : 0, sum = 0;
            for (int i = 0; i < P.Count; i++)
            {
                if (i == w) continue;
                int d = HandValue(i) - wv;
                P[i].LastDelta = -d; sum += d;
            }
            P[w].LastDelta = sum;
            foreach (var p in P) p.Score += p.LastDelta;
            RoundOver = true; Winner = w; Blocked = blocked;
            LastEvent = blocked ? "Kein Stein mehr im Vorrat - " + P[w].Name + " hat die wenigsten Punkte." : P[w].Name + " hat alle Steine abgelegt!";
            Log.I(M, "roundEnd winner=" + w + " blocked=" + blocked);
        }
    }
}
