using System.Collections.Generic;
using System.Linq;

namespace RommeCup.Rummikub
{
    public static class SetFinder
    {
        class Cand { public int[] Kinds; public int Jokers, Points; public int Len => Kinds.Length + Jokers; }

        public static List<int[]> Best(IList<int> tiles, int minPoints)
        {
            var jk = tiles.Where(Tiles.IsJoker).ToList();
            var cnt = new int[52];
            foreach (var t in tiles) if (!Tiles.IsJoker(t)) cnt[Tiles.Kind(t)]++;
            var cands = Candidates(cnt, jk.Count).OrderByDescending(c => c.Len).ThenBy(c => c.Jokers).ToList();
            var cur = new List<Cand>(); List<Cand> best = null; int bestScore = 0, nodes = 0;
            void Dfs(int i, int ju, int pts, int n)
            {
                if (++nodes > 30000) return;
                int sc = pts >= minPoints ? n * 1000 + pts - ju * 40 : 0;
                if (sc > bestScore) { bestScore = sc; best = new List<Cand>(cur); }
                for (int k = i; k < cands.Count; k++)
                {
                    var c = cands[k];
                    if (ju + c.Jokers > jk.Count) continue;
                    bool ok = true;
                    foreach (var kd in c.Kinds) if (--cnt[kd] < 0) ok = false;
                    if (ok) { cur.Add(c); Dfs(k + 1, ju + c.Jokers, pts + c.Points, n + c.Len); cur.RemoveAt(cur.Count - 1); }
                    foreach (var kd in c.Kinds) cnt[kd]++;
                }
            }
            Dfs(0, 0, 0, 0);
            var res = new List<int[]>();
            if (best == null) return res;
            var used = new HashSet<int>(); int jUsed = 0;
            foreach (var c in best)
            {
                var ids = new List<int>();
                foreach (var kd in c.Kinds) { var id = tiles.First(t => !used.Contains(t) && !Tiles.IsJoker(t) && Tiles.Kind(t) == kd); used.Add(id); ids.Add(id); }
                for (int j = 0; j < c.Jokers; j++) ids.Add(jk[jUsed++]);
                var m = Meld.Check(ids);
                if (m.Valid) res.Add(m.Ordered);
            }
            return res;
        }

        static List<Cand> Candidates(int[] cnt, int jokers)
        {
            var l = new List<Cand>();
            for (int c = 0; c < 4; c++)
                for (int s = 1; s <= 11; s++)
                {
                    var kinds = new List<int>(); int need = 0, pts = 0;
                    for (int v = s; v <= 13; v++)
                    {
                        int kd = c * 13 + v - 1;
                        if (cnt[kd] > 0) kinds.Add(kd); else if (++need > jokers) break;
                        pts += v;
                        int len = v - s + 1;
                        if (len >= 3 && kinds.Count >= 1 && kinds.Count >= len - 2)
                            l.Add(new Cand { Kinds = kinds.ToArray(), Jokers = need, Points = pts });
                    }
                }
            for (int v = 1; v <= 13; v++)
            {
                var have = new List<int>();
                for (int c = 0; c < 4; c++) if (cnt[c * 13 + v - 1] > 0) have.Add(c * 13 + v - 1);
                int n = have.Count;
                for (int mask = 1; mask < 1 << n; mask++)
                {
                    var ks = new List<int>();
                    for (int b = 0; b < n; b++) if ((mask & 1 << b) != 0) ks.Add(have[b]);
                    for (int size = 3; size <= 4; size++)
                    {
                        int j = size - ks.Count;
                        if (j < 0 || j > jokers || (ks.Count == 1 && j > 0 && size > 3)) continue;
                        l.Add(new Cand { Kinds = ks.ToArray(), Jokers = j, Points = v * size });
                    }
                }
            }
            return l;
        }

        public static bool TryLayOff(int[] cells, List<int> rack, out int tile)
        {
            tile = -1;
            foreach (var s in Board.Segments(cells))
                foreach (var t in rack)
                    foreach (var arr in new[] { s.Tiles.Append(t).ToArray(), new[] { t }.Concat(s.Tiles).ToArray() })
                    {
                        var m = Meld.Check(arr);
                        if (!m.Valid) continue;
                        Board.Clear(cells, s);
                        int col = arr[0] == t ? s.Col - 1 : s.Col;
                        if (Board.Place(cells, m.Ordered, s.Row, col) || Board.Place(cells, m.Ordered, s.Row, s.Col) || Board.Place(cells, m.Ordered)) { tile = t; return true; }
                        Board.Write(cells, s.Row, s.Col, s.Tiles);
                    }
            return false;
        }
    }

    public static class AiPlayer
    {
        public static int[] Plan(GameState s, int seat)
        {
            var p = s.P[seat];
            var rack = new List<int>(p.Rack);
            var cells = (int[])s.Table.Clone();
            bool changed = false;
            foreach (var set in SetFinder.Best(rack, p.Melded ? 0 : Rules.InitialMeld))
            {
                if (!Board.Place(cells, set)) break;
                foreach (var t in set) rack.Remove(t);
                changed = true;
            }
            if (p.Melded)
                for (int round = 0; round < 8; round++)
                {
                    for (int guard = 0; guard < 30 && SetFinder.TryLayOff(cells, rack, out var t); guard++) { rack.Remove(t); changed = true; }
                    if (!Borrow(cells, rack)) break;
                    changed = true;
                }
            return changed ? cells : null;
        }

        static bool Borrow(int[] cells, List<int> rack)
        {
            foreach (var s in Board.Segments(cells))
            {
                var m = Meld.Check(s.Tiles);
                var opts = new List<int>();
                if (m.Kind == MeldKind.Run && s.Tiles.Length >= 4) { opts.Add(0); opts.Add(s.Tiles.Length - 1); }
                else if (m.Kind == MeldKind.Group && s.Tiles.Length == 4) for (int i = 0; i < 4; i++) opts.Add(i);
                foreach (var oi in opts)
                {
                    int x = s.Tiles[oi];
                    var set = SetFinder.Best(new List<int>(rack) { x }, 0).FirstOrDefault(z => z.Contains(x));
                    if (set == null) continue;
                    var rest = s.Tiles.Where((_, k) => k != oi).ToArray();
                    if (!Meld.Check(rest).Valid) continue;
                    int col = m.Kind == MeldKind.Run && oi == 0 ? s.Col + 1 : s.Col;
                    Board.Clear(cells, s);
                    Board.Write(cells, s.Row, col, rest);
                    if (!Board.Place(cells, set))
                    {
                        for (int i = 0; i < rest.Length; i++) cells[s.Row * Board.Cols + col + i] = -1;
                        Board.Write(cells, s.Row, s.Col, s.Tiles);
                        continue;
                    }
                    foreach (var t in set) if (t != x) rack.Remove(t);
                    return true;
                }
            }
            return false;
        }
    }
}
