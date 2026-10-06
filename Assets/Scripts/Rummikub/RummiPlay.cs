using System.Collections.Generic;
using System.Linq;
using RommeCup.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RommeCup.Rummikub
{
    public class RummiPlay
    {
        const string M = "RummiPlay";
        readonly RummiModule mod;
        BoardView Bv => mod.Board;
        public Msg St;
        public int Seat;
        public bool Active;
        int[] work = Board.Empty();
        int[] rack = Board.Empty(40);
        readonly HashSet<int> hint = new HashSet<int>();
        int drag = -1, dragIdx, actor = -1;
        Area dragFrom;
        bool deal;

        public RummiPlay(RummiModule m) { mod = m; }

        public static bool Help { get => PlayerPrefs.GetInt("help", 1) == 1; set => PlayerPrefs.SetInt("help", value ? 1 : 0); }

        public void Begin(int seat) { Seat = seat; St = null; work = Board.Empty(); rack = Board.Empty(40); hint.Clear(); drag = -1; Active = true; Bv.SetRackRows(2); Log.Var(M, "seat", seat); }
        public void End() { Active = false; St = null; drag = -1; }

        public bool MyTurn => St != null && St.t == "state" && St.current == Seat;
        public bool Melded => St != null && St.players[Seat].melded;
        public bool IsPlaced(int id) => St != null && System.Array.IndexOf(St.cells, id) < 0 && System.Array.IndexOf(work, id) >= 0;
        public int PlacedCount => St == null ? 0 : work.Count(t => t >= 0) - St.cells.Count(t => t >= 0);
        public int MeldPoints => St == null ? 0 : Rules.NewSetPoints(St.cells, work);
        bool TableChanged => St != null && !work.SequenceEqual(St.cells);

        public void OnState(Msg m)
        {
            var prev = St;
            St = m; drag = -1;
            deal = prev == null || prev.round != m.round;
            actor = prev != null && !deal ? prev.current : -1;
            work = (int[])m.cells.Clone();
            if (deal) rack = Board.Empty(40);
            Reconcile(); hint.Clear();
            if (MyTurn && (prev == null || prev.current != Seat || prev.t != "state")) { Sfx.Play("turn", .8f); mod.Screens.Toast(deal ? "Du beginnst - lege aus oder ziehe einen Stein." : "Du bist am Zug."); }
            Refresh();
            deal = false;
            if (m.t == "round") mod.OnRoundEnd(m);
            else mod.Screens.HideRound();
        }

        public void OnPreview(Msg m)
        {
            if (MyTurn || St == null || m.cells == null) return;
            work = (int[])m.cells.Clone();
            actor = St.current;
            Refresh();
        }

        void Reconcile()
        {
            var mine = new HashSet<int>(St.rack);
            int n = St.rack.Length, rows = n <= 36 ? 2 : n <= 56 ? 3 : 4;
            if (rows * Board.Cols != rack.Length)
            {
                var nr = Board.Empty(rows * Board.Cols);
                for (int i = 0; i < Mathf.Min(rack.Length, nr.Length); i++) nr[i] = rack[i];
                rack = nr;
                Bv.SetRackRows(rows);
            }
            for (int i = 0; i < rack.Length; i++) if (rack[i] >= 0 && (!mine.Contains(rack[i]) || System.Array.IndexOf(work, rack[i]) >= 0)) rack[i] = -1;
            var present = new HashSet<int>(rack.Where(x => x >= 0));
            foreach (var id in St.rack) if (!present.Contains(id) && System.Array.IndexOf(work, id) < 0) PutFree(id);
        }

        void PutFree(int id)
        {
            for (int i = 0; i < rack.Length; i++) if (rack[i] < 0) { rack[i] = id; return; }
            Log.W(M, "rack full");
        }

        int Rel(int seat) => (seat - Seat + St.players.Length) % St.players.Length;

        LayoutInfo Info()
        {
            int n = St.players.Length;
            var opp = new int[n - 1];
            int previewed = MyTurn ? 0 : work.Count(t => t >= 0) - St.cells.Count(t => t >= 0);
            for (int r = 1; r < n; r++)
            {
                int s = (Seat + r) % n;
                opp[r - 1] = St.players[s].tiles - (s == St.current && previewed > 0 ? previewed : 0);
            }
            return new LayoutInfo { Pool = St.pool, Opp = opp, ActorRel = actor >= 0 && actor != Seat ? Rel(actor) : -1, Deal = deal };
        }

        public void Refresh()
        {
            if (St == null) return;
            Bv.Layout(work, rack, Info());
            var glow = new Dictionary<int, Color>();
            if (MyTurn)
            {
                var red = new Color(.42f, .04f, .03f); var green = new Color(.03f, .26f, .09f); var warm = new Color(.22f, .16f, .05f);
                foreach (var s in Board.Segments(work))
                {
                    bool placed = s.Tiles.Any(IsPlaced), valid = Meld.Check(s.Tiles).Valid;
                    foreach (var t in s.Tiles)
                    {
                        if (Help && !valid && (placed || TableChanged)) glow[t] = red;
                        else if (IsPlaced(t)) glow[t] = Help && valid ? green : warm;
                    }
                }
            }
            Bv.SetGlow(glow, hint);
            mod.Screens.UpdateHud(this);
        }

        public void Update()
        {
            if (!Active || St == null) return;
            bool down, held, up; Vector2 pos; int fid = -1;
            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0); pos = t.position; fid = t.fingerId;
                down = t.phase == TouchPhase.Began; up = t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled; held = !down && !up;
            }
            else { pos = Input.mousePosition; down = Input.GetMouseButtonDown(0); up = Input.GetMouseButtonUp(0); held = Input.GetMouseButton(0); }
            if (down && drag < 0)
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(fid)) return;
                if (mod.Screens.ModalOpen) return;
                var a = Bv.CellAtScreen(pos, out var idx);
                if (a == Area.None) return;
                var arr = a == Area.Table ? work : rack;
                int id = arr[idx];
                if (id < 0 || !CanPick(id, a)) { if (id >= 0 && a == Area.Table) mod.Screens.Toast(MyTurn ? "Vor der Erstauslage darfst du den Tisch nicht umbauen." : "Warte, bis du am Zug bist."); return; }
                drag = id; dragFrom = a; dragIdx = idx; arr[idx] = -1;
                hint.Clear();
                Sfx.Play("tpick", .7f);
            }
            if (drag >= 0 && (held || down) && Bv.Hit(pos, out var hw)) Bv.Drag(drag, hw);
            if (drag >= 0 && up) Drop(pos);
        }

        bool CanPick(int id, Area a) => a == Area.Rack || (MyTurn && (Melded || IsPlaced(id)));

        void Drop(Vector2 pos)
        {
            int id = drag; drag = -1;
            Bv.EndDrag(id);
            bool ok = false;
            var to = Bv.CellAtScreen(pos, out var idx);
            if (to == Area.Table && !MyTurn) mod.Screens.Toast("Du bist nicht am Zug.");
            else if (to == Area.Rack && dragFrom == Area.Table && St.cells.Contains(id)) mod.Screens.Toast("Steine vom Tisch bleiben auf dem Tisch.");
            else if (to != Area.None) ok = Insert(to == Area.Table ? work : rack, idx, id, to);
            if (!ok) { (dragFrom == Area.Table ? work : rack)[dragIdx] = id; Sfx.Play("error", .35f); }
            if (ok && MyTurn && (to == Area.Table || dragFrom == Area.Table)) mod.Send(new Msg { t = "preview", cells = work });
            Refresh();
        }

        bool Insert(int[] arr, int idx, int id, Area a)
        {
            if (arr[idx] < 0) { arr[idx] = id; return true; }
            int row = idx / Board.Cols, end = idx;
            while (end < (row + 1) * Board.Cols && arr[end] >= 0) end++;
            if (end >= (row + 1) * Board.Cols) return false;
            for (int i = idx; i < end; i++) if (!CanPick(arr[i], a)) return false;
            for (int i = end; i > idx; i--) arr[i] = arr[i - 1];
            arr[idx] = id;
            return true;
        }

        public void ResetTurn()
        {
            if (St == null) return;
            work = (int[])St.cells.Clone();
            Reconcile(); hint.Clear();
            if (MyTurn) mod.Send(new Msg { t = "preview", cells = work });
            Refresh();
        }

        public void Commit()
        {
            if (!MyTurn) return;
            var err = Rules.Validate(St.cells, work, St.rack, Melded, out _, out _);
            if (err != null) { mod.Screens.Toast(err); Sfx.Play("error"); return; }
            mod.Send(new Msg { t = "commit", cells = (int[])work.Clone() });
        }

        public void Draw()
        {
            if (!MyTurn) return;
            if (TableChanged) { work = (int[])St.cells.Clone(); Reconcile(); }
            mod.Send(new Msg { t = "draw" });
        }

        public void Sort(bool byColor)
        {
            var ids = rack.Where(x => x >= 0).OrderBy(x => Tiles.IsJoker(x) ? 1 : 0)
                .ThenBy(x => byColor ? (int)Tiles.Color(x) : Tiles.Number(x)).ThenBy(x => byColor ? Tiles.Number(x) : (int)Tiles.Color(x)).ToList();
            var groups = ids.Select(x => Tiles.IsJoker(x) ? -1 : byColor ? (int)Tiles.Color(x) : Tiles.Number(x)).Distinct().Count();
            bool gaps = ids.Count + groups - 1 <= rack.Length;
            for (int i = 0; i < rack.Length; i++) rack[i] = -1;
            int k = 0, last = int.MinValue;
            foreach (var id in ids)
            {
                int g = Tiles.IsJoker(id) ? -1 : byColor ? (int)Tiles.Color(id) : Tiles.Number(id);
                if (gaps && last != int.MinValue && g != last && k % Board.Cols != 0) k++;
                if (k >= rack.Length) k = System.Array.IndexOf(rack, -1);
                rack[k++] = id; last = g;
            }
            Sfx.Play("rack", .8f);
            Refresh();
        }

        public void Hint()
        {
            hint.Clear();
            var mine = rack.Where(x => x >= 0).ToList();
            if (!MyTurn) { mod.Screens.Toast("Tipps gibt es, wenn du am Zug bist."); return; }
            int need = Melded ? 0 : Mathf.Max(1, Rules.InitialMeld - MeldPoints);
            var sets = SetFinder.Best(mine, need);
            if (sets.Count > 0) { foreach (var t in sets[0]) hint.Add(t); mod.Screens.Toast(Melded ? "Diese Steine bilden eine Kombination." : "Damit schaffst du die Erstauslage (30 Punkte)."); }
            else if (Melded && SetFinder.TryLayOff((int[])work.Clone(), mine, out var lay)) { hint.Add(lay); mod.Screens.Toast("Diesen Stein kannst du an eine Kombination anlegen."); }
            else mod.Screens.Toast(Melded ? "Kein einfacher Zug - ziehe einen Stein oder baue den Tisch um." : "Noch keine 30 Punkte möglich - ziehe einen Stein.");
            Refresh();
        }
    }
}
