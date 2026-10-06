using System.Collections.Generic;
using System.Linq;

namespace RommeCup.Karten
{
    public enum MeldKind { None, Set, Run }

    public class MeldInfo
    {
        public MeldKind Kind;
        public List<int> Cards;
        public int Points;
    }

    public static class Cards
    {
        public const int Count = 110, JokerBase = 104, InitialMeld = 40, HandSize = 13, JokerValue = 20;
        static readonly int[] PosValue = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 10, 10, 10, 11 };
        static readonly string[] RankNames = { "A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "B", "D", "K" };
        public static readonly string[] SuitNames = { "Herz", "Karo", "Pik", "Kreuz" };

        public static bool IsJoker(int id) => id >= JokerBase;
        public static int Suit(int id) => id % 52 / 13;
        public static int Rank(int id) => id % 52 % 13;
        public static int Face(int id) => IsJoker(id) ? 52 : id % 52;
        public static int Value(int id) => IsJoker(id) ? JokerValue : Rank(id) == 0 ? 11 : Rank(id) >= 9 ? 10 : Rank(id) + 1;
        public static string Name(int id) => IsJoker(id) ? "Joker" : SuitNames[Suit(id)] + " " + RankNames[Rank(id)];

        public static int HandValue(IEnumerable<int> hand) => hand.Sum(Value);

        static int Idx(int rank, int start, int n)
        {
            if (rank != 0) return rank;
            if (start == 0) return 0;
            return start + n - 1 == 13 ? 13 : -1;
        }

        public static MeldInfo Arrange(IList<int> cards)
        {
            int n = cards.Count;
            if (n < 3 || n > 14) return null;
            var nat = cards.Where(c => !IsJoker(c)).ToList();
            int jk = n - nat.Count;
            if (jk > 1 || nat.Count < 2 || cards.Distinct().Count() != n) return null;
            if (nat.All(c => Rank(c) == Rank(nat[0])))
            {
                if (n > 4 || nat.Select(Suit).Distinct().Count() != nat.Count) return null;
                int rp = Rank(nat[0]) == 0 ? 11 : PosValue[Rank(nat[0])];
                var ord = nat.OrderBy(Suit).ToList();
                ord.AddRange(cards.Where(IsJoker));
                return new MeldInfo { Kind = MeldKind.Set, Cards = ord, Points = rp * n };
            }
            if (nat.Any(c => Suit(c) != Suit(nat[0]))) return null;
            MeldInfo best = null;
            int joker = cards.FirstOrDefault(IsJoker);
            for (int start = 0; start + n <= 14; start++)
            {
                var pos = new int[n];
                bool ok = true;
                foreach (var c in nat)
                {
                    int ix = Idx(Rank(c), start, n) - start;
                    if (ix < 0 || ix >= n || pos[ix] != 0) { ok = false; break; }
                    pos[ix] = c + 1;
                }
                if (!ok) continue;
                int pts = 0; var list = new List<int>(n);
                for (int k = 0; k < n; k++) { pts += PosValue[start + k]; list.Add(pos[k] == 0 ? joker : pos[k] - 1); }
                if (best == null || pts > best.Points) best = new MeldInfo { Kind = MeldKind.Run, Cards = list, Points = pts };
            }
            return best;
        }

        public static MeldInfo Arrange(IEnumerable<int> a, IEnumerable<int> b) => Arrange(a.Concat(b).ToList());

        public static MeldInfo Swapped(IList<int> meld, int joker, int with)
        {
            if (!meld.Contains(joker) || IsJoker(with)) return null;
            var l = meld.Where(c => c != joker).ToList();
            l.Add(with);
            return Arrange(l);
        }
    }
}
