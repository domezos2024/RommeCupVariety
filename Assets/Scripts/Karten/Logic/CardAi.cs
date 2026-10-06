using System.Collections.Generic;
using System.Linq;

namespace RommeCup.Karten
{
    public class Cover
    {
        public List<List<int>> Groups = new List<List<int>>();
        public int Points;
    }

    public class Act
    {
        public string K;
        public List<List<int>> Groups;
        public List<int> Cards;
        public int Meld = -1, Card = -1, Joker = -1;
    }

    public static class CardAi
    {
        static readonly int[] PosValue = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 10, 10, 10, 11 };

        sealed class Finder
        {
            public int[] A;
            public bool[] Used;
            public int Nodes;
            public Cover Best = new Cover();
            public List<List<int>> Cur = new List<List<int>>();
            public int[] SufValue;

            public void Run(int i, int pts)
            {
                if (++Nodes > 7000) return;
                while (i < A.Length && (Used[i] || Cards.IsJoker(A[i]))) i++;
                if (pts > Best.Points) Best = new Cover { Points = pts, Groups = Cur.Select(g => new List<int>(g)).ToList() };
                if (i >= A.Length || pts + SufValue[i] + 20 <= Best.Points) return;
                foreach (var (idx, mp) in Candidates(i).ToList())
                {
                    foreach (var k in idx) Used[k] = true;
                    Cur.Add(idx.Select(k => A[k]).ToList());
                    Run(i + 1, pts + mp);
                    Cur.RemoveAt(Cur.Count - 1);
                    foreach (var k in idx) Used[k] = false;
                }
                Run(i + 1, pts);
            }

            int FindFree(int from, int suit, int rank, List<int> taken)
            {
                for (int j = from; j < A.Length; j++)
                    if (!Used[j] && !Cards.IsJoker(A[j]) && Cards.Suit(A[j]) == suit && Cards.Rank(A[j]) == rank && !taken.Contains(j)) return j;
                return -1;
            }

            int FreeJoker()
            {
                for (int j = A.Length - 1; j >= 0 && Cards.IsJoker(A[j]); j--) if (!Used[j]) return j;
                return -1;
            }

            IEnumerable<(List<int>, int)> Candidates(int i)
            {
                int c = A[i], suit = Cards.Suit(c), rank = Cards.Rank(c), jx = FreeJoker();
                var others = new List<int>();
                for (int s = 0; s < 4; s++)
                {
                    if (s == suit) continue;
                    int j = FindFree(i + 1, s, rank, new List<int>());
                    if (j >= 0) others.Add(j);
                }
                int rp = Cards.Value(c);
                int m = others.Count;
                for (int mask = 1; mask < 1 << m; mask++)
                {
                    var l = new List<int> { i };
                    for (int b = 0; b < m; b++) if ((mask >> b & 1) == 1) l.Add(others[b]);
                    if (l.Count >= 3) yield return (new List<int>(l), rp * l.Count);
                    if (jx >= 0 && l.Count >= 2 && l.Count <= 3) { var lj = new List<int>(l) { jx }; yield return (lj, rp * lj.Count); }
                }
                var anchors = rank == 0 ? new[] { 0, 13 } : new[] { rank };
                foreach (int a in anchors)
                    for (int s = 0; s <= a; s++)
                        for (int e = System.Math.Max(a, s + 2); e <= 13; e++)
                        {
                            var l = new List<int>(); bool joker = false, ok = true; int pts = 0;
                            for (int q = s; q <= e && ok; q++)
                            {
                                pts += PosValue[q];
                                if (q == a) { l.Add(i); continue; }
                                int j = FindFree(i + 1, suit, q % 13, l);
                                if (j >= 0) l.Add(j);
                                else if (!joker && jx >= 0) { l.Add(jx); joker = true; }
                                else ok = false;
                            }
                            if (!ok) { if (e > a) break; continue; }
                            yield return (l, pts);
                        }
            }
        }

        public static Cover Best(IEnumerable<int> hand)
        {
            var a = hand.OrderBy(c => Cards.IsJoker(c) ? 1 : 0).ThenBy(Cards.Suit).ThenBy(Cards.Rank).ToArray();
            var suf = new int[a.Length + 1];
            for (int i = a.Length - 1; i >= 0; i--) suf[i] = suf[i + 1] + (Cards.IsJoker(a[i]) ? 0 : Cards.Value(a[i]));
            var f = new Finder { A = a, Used = new bool[a.Length], SufValue = suf };
            f.Run(0, 0);
            return f.Best;
        }

        static int Keep(List<int> hand, int c)
        {
            int k = 0;
            foreach (var o in hand)
            {
                if (o == c || Cards.IsJoker(o)) continue;
                if (Cards.Rank(o) == Cards.Rank(c) && Cards.Suit(o) != Cards.Suit(c)) k += 3;
                if (Cards.Suit(o) == Cards.Suit(c))
                {
                    int d = System.Math.Abs(Cards.Rank(o) - Cards.Rank(c));
                    if (d == 1) k += 3; else if (d == 2) k += 1;
                    if ((Cards.Rank(c) == 0 && Cards.Rank(o) >= 11) || (Cards.Rank(o) == 0 && Cards.Rank(c) >= 11)) k += 2;
                }
            }
            return k;
        }

        public static int WorstCard(List<int> h, int took)
        {
            var cand = h.Where(c => !Cards.IsJoker(c) && c != took).ToList();
            if (cand.Count == 0) cand = h.Where(c => c != took).ToList();
            if (cand.Count == 0) cand = h.ToList();
            return cand.OrderBy(c => Keep(h, c)).ThenByDescending(Cards.Value).First();
        }

        static int PickDiscard(CardGame g, int seat) => WorstCard(g.P[seat].Hand, g.Took);

        public static Act Next(CardGame g, int seat, int step)
        {
            var p = g.P[seat];
            if (!g.Drawn)
            {
                if (g.Discard.Count > 0)
                {
                    int top = g.Discard[g.Discard.Count - 1];
                    bool use = p.Melded && g.Table.Any(m => Cards.Arrange(m, new[] { top }) != null);
                    if (!use) use = Best(p.Hand.Append(top)).Points > Best(p.Hand).Points;
                    if (use) return new Act { K = "disc" };
                }
                return new Act { K = "stock" };
            }
            if (step < 30)
            {
                if (!p.Melded)
                {
                    var cv = Best(p.Hand);
                    if (cv.Points >= Cards.InitialMeld) return new Act { K = "meld", Groups = cv.Groups };
                }
                else
                {
                    for (int mi = 0; mi < g.Table.Count; mi++)
                    {
                        int jk = g.Table[mi].FirstOrDefault(Cards.IsJoker);
                        if (jk < Cards.JokerBase) continue;
                        foreach (var c in p.Hand.Where(x => !Cards.IsJoker(x)))
                            if (Cards.Swapped(g.Table[mi], jk, c) != null) return new Act { K = "swap", Meld = mi, Joker = jk, Card = c };
                    }
                    foreach (var c in p.Hand)
                    {
                        if (Cards.IsJoker(c) && !g.TurnJokers.Contains(c) && p.Hand.Count > 1) continue;
                        for (int mi = 0; mi < g.Table.Count; mi++)
                            if (Cards.Arrange(g.Table[mi], new[] { c }) != null) return new Act { K = "layoff", Meld = mi, Cards = new List<int> { c } };
                    }
                    var cv = Best(p.Hand);
                    if (cv.Groups.Count > 0) return new Act { K = "meld", Groups = cv.Groups };
                }
            }
            return new Act { K = "discard", Card = PickDiscard(g, seat) };
        }

        public static string Apply(CardGame g, int seat, Act a)
        {
            switch (a.K)
            {
                case "stock": return g.DrawStock(seat);
                case "disc": return g.DrawDiscard(seat);
                case "meld": return g.Meld(seat, a.Groups);
                case "layoff": return g.LayOff(seat, a.Meld, a.Cards);
                case "swap": return g.Swap(seat, a.Meld, a.Joker, a.Card);
                default: return g.Discarding(seat, a.Card);
            }
        }
    }
}
