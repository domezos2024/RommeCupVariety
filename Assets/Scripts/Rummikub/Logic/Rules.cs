using System;
using System.Collections.Generic;
using System.Linq;

namespace RommeCup.Rummikub
{
    public enum TileColor { Schwarz, Rot, Blau, Orange, Joker }

    public static class Tiles
    {
        public const int Count = 106;
        public static bool IsJoker(int id) => id >= 104;
        public static TileColor Color(int id) => id >= 104 ? TileColor.Joker : (TileColor)(id % 52 / 13);
        public static int Number(int id) => id >= 104 ? 0 : id % 13 + 1;
        public static int Kind(int id) => id >= 104 ? 52 : id % 52;
        public static int Penalty(int id) => id >= 104 ? 30 : Number(id);
        public static string Name(int id) => IsJoker(id) ? "Joker" : Color(id) + " " + Number(id);
    }

    public enum MeldKind { Invalid, Group, Run }

    public struct MeldResult
    {
        public MeldKind Kind;
        public int Points;
        public int[] Ordered;
        public bool Valid => Kind != MeldKind.Invalid;
    }

    public static class Meld
    {
        public static MeldResult Check(IList<int> t)
        {
            if (t.Count < 3 || t.Count > 13) return default;
            var r = RunAsLaid(t);
            if (r.Valid) return r;
            var nj = new List<int>(); var jk = new List<int>();
            foreach (var x in t) (Tiles.IsJoker(x) ? jk : nj).Add(x);
            if (nj.Count == 0) return default;
            r = RunSorted(nj, jk);
            return r.Valid ? r : Group(nj, jk);
        }

        static MeldResult RunAsLaid(IList<int> t)
        {
            int n = t.Count, start = int.MinValue; var c = TileColor.Joker;
            for (int i = 0; i < n; i++)
            {
                if (Tiles.IsJoker(t[i])) continue;
                int s = Tiles.Number(t[i]) - i; var col = Tiles.Color(t[i]);
                if (start == int.MinValue) { start = s; c = col; }
                else if (s != start || col != c) return default;
            }
            if (start == int.MinValue || start < 1 || start + n - 1 > 13) return default;
            return new MeldResult { Kind = MeldKind.Run, Points = n * start + n * (n - 1) / 2, Ordered = t.ToArray() };
        }

        static MeldResult RunSorted(List<int> nj, List<int> jk)
        {
            var c = Tiles.Color(nj[0]);
            if (nj.Any(x => Tiles.Color(x) != c)) return default;
            var s = nj.OrderBy(Tiles.Number).ToList();
            for (int i = 1; i < s.Count; i++) if (Tiles.Number(s[i]) == Tiles.Number(s[i - 1])) return default;
            int n = s.Count + jk.Count, min = Tiles.Number(s[0]), max = Tiles.Number(s[^1]);
            int gaps = max - min + 1 - s.Count;
            if (gaps > jk.Count) return default;
            int end = Math.Min(13, max + jk.Count - gaps), start = end - n + 1;
            if (start < 1) return default;
            var o = new int[n]; int si = 0, ji = 0;
            for (int v = start, k = 0; v <= end; v++, k++)
                o[k] = si < s.Count && Tiles.Number(s[si]) == v ? s[si++] : jk[ji++];
            return new MeldResult { Kind = MeldKind.Run, Points = n * start + n * (n - 1) / 2, Ordered = o };
        }

        static MeldResult Group(List<int> nj, List<int> jk)
        {
            int n = nj.Count + jk.Count, num = Tiles.Number(nj[0]);
            if (n > 4 || nj.Any(x => Tiles.Number(x) != num)) return default;
            if (nj.Select(Tiles.Color).Distinct().Count() != nj.Count) return default;
            return new MeldResult { Kind = MeldKind.Group, Points = num * n, Ordered = nj.OrderBy(Tiles.Color).Concat(jk).ToArray() };
        }
    }

    public static class Board
    {
        public const int Cols = 20, Rows = 8, Size = Cols * Rows;

        public struct Seg { public int Row, Col; public int[] Tiles; }

        public static int[] Empty(int size = Size) { var a = new int[size]; for (int i = 0; i < size; i++) a[i] = -1; return a; }

        public static List<Seg> Segments(int[] cells)
        {
            var res = new List<Seg>();
            int rows = cells.Length / Cols;
            for (int r = 0; r < rows; r++)
            {
                int c = 0;
                while (c < Cols)
                {
                    if (cells[r * Cols + c] < 0) { c++; continue; }
                    int s = c; var l = new List<int>();
                    while (c < Cols && cells[r * Cols + c] >= 0) l.Add(cells[r * Cols + c++]);
                    res.Add(new Seg { Row = r, Col = s, Tiles = l.ToArray() });
                }
            }
            return res;
        }

        public static bool Fits(int[] cells, int row, int col, int len)
        {
            if (col < 0 || col + len > Cols || row < 0 || row >= cells.Length / Cols) return false;
            for (int i = col - 1; i <= col + len; i++)
                if (i >= 0 && i < Cols && cells[row * Cols + i] >= 0) return false;
            return true;
        }

        public static void Write(int[] cells, int row, int col, int[] set) { for (int i = 0; i < set.Length; i++) cells[row * Cols + col + i] = set[i]; }

        public static void Clear(int[] cells, Seg s) { for (int i = 0; i < s.Tiles.Length; i++) cells[s.Row * Cols + s.Col + i] = -1; }

        public static bool Place(int[] cells, int[] set, int prefRow = -1, int prefCol = -1)
        {
            if (prefRow >= 0 && Fits(cells, prefRow, prefCol, set.Length)) { Write(cells, prefRow, prefCol, set); return true; }
            int rows = cells.Length / Cols;
            for (int r = 0; r < rows; r++)
                for (int c = 0; c + set.Length <= Cols; c++)
                    if (Fits(cells, r, c, set.Length)) { Write(cells, r, c, set); return true; }
            return false;
        }

        public static void Normalize(int[] cells)
        {
            foreach (var s in Segments(cells))
            {
                var m = Meld.Check(s.Tiles);
                if (m.Valid) Write(cells, s.Row, s.Col, m.Ordered);
            }
        }
    }

    public static class Rules
    {
        public const int InitialMeld = 30;

        public static string Validate(int[] oldCells, int[] newCells, ICollection<int> rack, bool melded, out List<int> placed, out int meldPoints)
        {
            placed = new List<int>(); meldPoints = 0;
            var oldSet = new HashSet<int>(oldCells.Where(x => x >= 0));
            var newList = newCells.Where(x => x >= 0).ToList();
            var newSet = new HashSet<int>(newList);
            if (newList.Count != newSet.Count) return "Ungültiger Zug.";
            foreach (var t in oldSet) if (!newSet.Contains(t)) return "Steine vom Tisch dürfen nicht zurück auf die Bank.";
            var rackSet = new HashSet<int>(rack);
            foreach (var t in newList) if (!oldSet.Contains(t)) { if (!rackSet.Contains(t)) return "Unbekannter Stein."; placed.Add(t); }
            if (placed.Count == 0) return "Lege mindestens einen Stein - oder ziehe einen.";
            var segs = Board.Segments(newCells);
            foreach (var s in segs)
                if (!Meld.Check(s.Tiles).Valid)
                    return s.Tiles.Length < 3 ? "Jede Kombination braucht mindestens 3 Steine." : "Auf dem Tisch liegt eine ungültige Kombination.";
            if (!melded)
            {
                var oldKeys = Board.Segments(oldCells).Select(s => Key(s.Tiles)).ToList();
                var ps = new HashSet<int>(placed);
                foreach (var s in segs)
                {
                    bool any = s.Tiles.Any(ps.Contains), all = s.Tiles.All(ps.Contains);
                    if (any && !all) return "Vor der Erstauslage darfst du nicht an Tisch-Kombinationen anlegen.";
                    if (!any) { if (!oldKeys.Remove(Key(s.Tiles))) return "Vor der Erstauslage darf der Tisch nicht umgebaut werden."; }
                    else meldPoints += Meld.Check(s.Tiles).Points;
                }
                if (meldPoints < InitialMeld) return "Erstauslage braucht mindestens 30 Punkte (aktuell " + meldPoints + ").";
            }
            return null;
        }

        public static int NewSetPoints(int[] oldCells, int[] newCells)
        {
            var oldSet = new HashSet<int>(oldCells.Where(x => x >= 0)); int p = 0;
            foreach (var s in Board.Segments(newCells))
                if (s.Tiles.All(t => !oldSet.Contains(t))) { var m = Meld.Check(s.Tiles); if (m.Valid) p += m.Points; }
            return p;
        }

        static string Key(int[] t) => string.Join(",", t.OrderBy(x => x));
    }
}
