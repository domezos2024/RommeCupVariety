using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using RommeCup.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RommeCup.Rummikub
{
    public enum Area { None, Table, Rack }

    public class LayoutInfo
    {
        public int Pool;
        public int[] Opp = new int[0];
        public int ActorRel = -1;
        public bool Deal;
    }

    public class TileView
    {
        public enum R { None, Table, Rack, Pool, Opp }
        public int Id, Seat, Idx;
        public R Role;
        public GameObject Go;
        public Material Body;
        public Vector3 Target;
        public Quaternion Rot = Quaternion.identity;
        public float Scale = 1, Delay;
        public bool Shown, Dragged, Flying, Pulse;
        public Color Glow, Applied = new Color(-1, 0, 0);
    }

    public class BoardView
    {
        const string M = "BoardView";
        public const float PX = 1.08f, PZ = 1.5f, TableZ = 1.4f, TW = 1f, TD = 1.4f, TH = .3f, Tilt = 28f, RPZ = 1.2f, OppScale = .82f;
        public static float FeltW => Board.Cols * PX + .9f;
        public static float FeltD => Board.Rows * PZ + .8f;
        public static float TopEdge => TableZ + FeltD / 2;
        public static float RackZ0 => TableZ - FeltD / 2 - 1.5f;
        static float HeapX0 => -FeltW / 2 - 5.6f;
        static float HeapZ0 => RackZ0 - .4f;
        public Camera Cam => Env.Cam;
        public int RackRows { get; private set; } = 2;
        public bool Ready;
        readonly TileView[] tiles = new TileView[Tiles.Count];
        readonly Material[] faces = new Material[53];
        GameObject root, rackGo, oppGo;
        Mesh mesh;
        float time;
        int oppN = -1;
        bool backdrop;
        Vector3 camPos, focus;
        Quaternion camRot;
        float camDist;

        public IEnumerator Build(Action<float> progress)
        {
            root = new GameObject("Board");
            Env.BuildTable(root.transform, new Vector3(0, 0, TableZ), FeltW, FeltD, new Rect(-26, RackZ0 - 9, 52, 36));
            Fx.Dust(root.transform, new Vector3(-2, 3.2f, TableZ), new Vector3(30, 5, 20));
            mesh = TileMesh.Build(TW, TD, TH, .13f, .055f);
            for (int k = 0; k < 53; k++)
            {
                faces[k] = Mats.Lit(Color.white, 0, .58f, TileArt.Face(k), TileArt.Normal(k), 1f);
                progress?.Invoke((k + 1) / 60f);
                if (k % 3 == 2) yield return null;
            }
            for (int i = 0; i < Tiles.Count; i++)
            {
                var go = new GameObject("Tile" + i, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(root.transform);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                var body = Mats.Emissive(TileArt.Ivory * .96f, 0, .55f);
                go.GetComponent<MeshRenderer>().sharedMaterials = new[] { faces[Tiles.Kind(i)], body };
                go.SetActive(false);
                tiles[i] = new TileView { Id = i, Go = go, Body = body, Role = TileView.R.None };
            }
            progress?.Invoke(1);
            SetRackRows(2, true);
            Ready = true;
            Log.I(M, "built");
        }

        public void SetRackRows(int rows, bool force = false)
        {
            if (rows == RackRows && !force) return;
            RackRows = rows;
            if (rackGo) Object.Destroy(rackGo);
            rackGo = new GameObject("Rack");
            rackGo.transform.SetParent(root.transform);
            var plastic = Mats.Lit(new Color(.08f, .08f, .085f), 0, .66f);
            float w = Board.Cols * PX + .7f;
            for (int r = 0; r < rows; r++)
            {
                float z = RowZ(r), top = RowY(r) - .36f;
                Env.Cube(rackGo.transform, "Step" + r, new Vector3(0, top / 2, z), new Vector3(w, top, RPZ), plastic);
                Env.Cube(rackGo.transform, "Lip" + r, new Vector3(0, top + .07f, z - RPZ / 2 + .06f), new Vector3(w, .14f, .12f), plastic);
            }
            float zb = RowZ(0) + RPZ / 2 + .05f;
            Env.Cube(rackGo.transform, "Back", new Vector3(0, (RowY(0) + .1f) / 2, zb), new Vector3(w, RowY(0) + .1f, .1f), plastic);
            float z0 = RowZ(rows - 1) - RPZ / 2, len = zb - z0;
            Env.Cube(rackGo.transform, "EndL", new Vector3(-w / 2 - .06f, (RowY(0) + .1f) / 2, z0 + len / 2), new Vector3(.12f, RowY(0) + .1f, len), plastic);
            Env.Cube(rackGo.transform, "EndR", new Vector3(w / 2 + .06f, (RowY(0) + .1f) / 2, z0 + len / 2), new Vector3(.12f, RowY(0) + .1f, len), plastic);
            if (!backdrop) FitCamera();
        }

        float RowZ(int r) => RackZ0 - r * RPZ;
        float RowY(int r) => .42f + (RackRows - 1 - r) * .3f;

        void BuildOpp(int n)
        {
            if (n == oppN) return;
            oppN = n;
            if (oppGo) Object.Destroy(oppGo);
            oppGo = new GameObject("OppRacks");
            oppGo.transform.SetParent(root.transform);
            var plastic = Mats.Lit(new Color(.08f, .08f, .085f), 0, .66f);
            for (int s = 1; s <= n; s++)
            {
                OppSpan(s, n, out var cx, out var w);
                Env.Cube(oppGo.transform, "OppBase" + s, new Vector3(cx, .14f, TopEdge + 1.35f), new Vector3(w, .28f, 1.7f), plastic);
                Env.Cube(oppGo.transform, "OppStep" + s, new Vector3(cx, .32f, TopEdge + .95f), new Vector3(w, .2f, .8f), plastic);
            }
            FitCamera();
        }

        static void OppSpan(int s, int n, out float cx, out float w)
        {
            float span = FeltW - .4f, each = span / n;
            cx = -span / 2 + each * (s - .5f); w = each - .5f;
        }

        Vector3 OppPos(int s, int j, int count, out Quaternion rot)
        {
            OppSpan(s, Mathf.Max(1, oppN), out var cx, out var w);
            float step = TW * OppScale * 1.04f;
            int cap = Mathf.Max(1, Mathf.FloorToInt((w - .3f) / step));
            int tiers = count > cap ? 2 : 1, per = Mathf.CeilToInt(count / (float)tiers), tier = j / Mathf.Max(1, per), k = j % Mathf.Max(1, per);
            float sp = Mathf.Min(step, (w - .4f) / Mathf.Max(1, per));
            rot = Quaternion.Euler(65, 0, 0);
            float x = cx + (k - (per - 1) / 2f) * sp;
            return tier == 0 ? new Vector3(x, .3f, TopEdge + 1.55f) : new Vector3(x, .48f, TopEdge + .85f);
        }

        static Vector3 HeapPos(int i, out Quaternion rot)
        {
            const int cols = 4, rows = 11, per = cols * rows;
            int layer = i / per, j = i % per, c = j % cols, r = j / cols;
            float jx = (Noise.Hash(i, 1) - .5f) * .28f, jz = (Noise.Hash(i, 2) - .5f) * .3f, off = layer % 2 == 1 ? .55f : 0;
            float yaw = (Noise.Hash(i, 3) - .5f) * 34, tx = layer > 0 ? (Noise.Hash(i, 4) - .5f) * 5 : 0, tz = layer > 0 ? (Noise.Hash(i, 5) - .5f) * 5 : 0;
            rot = Quaternion.Euler(0, yaw, 0) * Quaternion.Euler(tx, 0, 180 + tz);
            return new Vector3(HeapX0 + c * 1.3f + off + jx, TH + layer * (TH + .02f), HeapZ0 + r * 1.5f + off * .7f + jz);
        }

        public static Vector3 TableCell(int i) => new Vector3((i % Board.Cols - (Board.Cols - 1) / 2f) * PX, 0, TableZ + ((Board.Rows - 1) / 2f - i / Board.Cols) * PZ);
        Vector3 RackCell(int i) => new Vector3((i % Board.Cols - (Board.Cols - 1) / 2f) * PX, RowY(i / Board.Cols), RowZ(i / Board.Cols));

        public Area CellAtScreen(Vector2 screen, out int idx)
        {
            idx = -1;
            var ray = Cam.ScreenPointToRay(screen);
            for (int r = 0; r < RackRows; r++)
            {
                if (!Plane(ray, RowY(r) + .2f, out var p)) continue;
                int c = Mathf.RoundToInt(p.x / PX + (Board.Cols - 1) / 2f);
                if (c < 0 || c >= Board.Cols || Mathf.Abs(p.z - RowZ(r)) > RPZ / 2) continue;
                idx = r * Board.Cols + c; return Area.Rack;
            }
            if (Plane(ray, TH / 2, out var w))
            {
                int c = Mathf.RoundToInt(w.x / PX + (Board.Cols - 1) / 2f), r = Mathf.RoundToInt((Board.Rows - 1) / 2f - (w.z - TableZ) / PZ);
                if (c >= 0 && c < Board.Cols && r >= 0 && r < Board.Rows) { idx = r * Board.Cols + c; return Area.Table; }
            }
            return Area.None;
        }

        static bool Plane(Ray ray, float y, out Vector3 w)
        {
            w = default;
            if (Mathf.Abs(ray.direction.y) < 1e-4f) return false;
            float t = (y - ray.origin.y) / ray.direction.y;
            if (t < 0) return false;
            w = ray.origin + ray.direction * t;
            return true;
        }

        public bool Hit(Vector2 screen, out Vector3 w) => Plane(Cam.ScreenPointToRay(screen), 0, out w);

        void Swap(TileView a, TileView b)
        {
            (a.Role, b.Role) = (b.Role, a.Role); (a.Seat, b.Seat) = (b.Seat, a.Seat); (a.Idx, b.Idx) = (b.Idx, a.Idx);
            var ta = a.Go.transform; var tb = b.Go.transform;
            var p = ta.position; var q = ta.rotation; var s = ta.localScale;
            ta.position = tb.position; ta.rotation = tb.rotation; ta.localScale = tb.localScale;
            tb.position = p; tb.rotation = q; tb.localScale = s;
            (a.Target, b.Target) = (b.Target, a.Target); (a.Rot, b.Rot) = (b.Rot, a.Rot); (a.Scale, b.Scale) = (b.Scale, a.Scale);
        }

        public void Layout(int[] table, int[] rack, LayoutInfo info)
        {
            if (!Ready) return;
            if (backdrop) { backdrop = false; FitCamera(); }
            BuildOpp(info.Opp.Length);
            var onTable = new bool[Tiles.Count]; var inRack = new bool[Tiles.Count];
            foreach (var id in table) if (id >= 0) onTable[id] = true;
            foreach (var id in rack) if (id >= 0) inRack[id] = true;
            if (info.Deal)
            {
                var order = tiles.OrderBy(t => Noise.Hash(t.Id, Time.frameCount)).ToList();
                for (int i = 0; i < order.Count; i++)
                {
                    var t = order[i];
                    t.Role = TileView.R.Pool; t.Idx = i; t.Delay = 0; t.Flying = false; t.Dragged = false;
                    t.Target = HeapPos(i, out t.Rot); t.Scale = 1;
                    t.Go.transform.position = t.Target; t.Go.transform.rotation = t.Rot; t.Go.transform.localScale = Vector3.one;
                    t.Shown = true; t.Go.SetActive(true);
                }
            }
            foreach (var t in tiles)
            {
                if (!(onTable[t.Id] || inRack[t.Id]) || (t.Role != TileView.R.Pool && t.Role != TileView.R.Opp)) continue;
                var want = inRack[t.Id] || info.ActorRel <= 0 ? TileView.R.Pool : TileView.R.Opp;
                if (t.Role == want && (want == TileView.R.Pool || t.Seat == info.ActorRel)) continue;
                var donor = tiles.Where(h => !onTable[h.Id] && !inRack[h.Id] && h.Role == want && (want == TileView.R.Pool || h.Seat == info.ActorRel)).OrderByDescending(h => h.Idx).FirstOrDefault();
                if (donor != null) Swap(t, donor);
            }
            for (int i = 0; i < rack.Length; i++)
            {
                if (rack[i] < 0) continue;
                var t = tiles[rack[i]];
                if (info.Deal) t.Delay = .35f + i % Board.Cols * Mathf.Max(1, info.Opp.Length + 1) * .05f + i / Board.Cols * .02f;
                t.Role = TileView.R.Rack; t.Target = RackCell(i); t.Rot = Quaternion.Euler(-Tilt, 0, 0); t.Scale = 1;
            }
            for (int i = 0; i < table.Length; i++)
            {
                if (table[i] < 0) continue;
                var t = tiles[table[i]];
                t.Role = TileView.R.Table; t.Rot = Quaternion.Euler(0, (Noise.Hash(t.Id, 7) - .5f) * 2.4f, 0); t.Scale = 1;
                t.Target = TableCell(i) + new Vector3((Noise.Hash(t.Id, 8) - .5f) * .04f, 0, (Noise.Hash(t.Id, 9) - .5f) * .04f);
            }
            var hidden = tiles.Where(t => !onTable[t.Id] && !inRack[t.Id]).ToList();
            var pool = hidden.Where(t => t.Role == TileView.R.Pool).OrderBy(t => t.Idx).ToList();
            int n = info.Opp.Length;
            var opp = new List<TileView>[n + 1];
            for (int s = 1; s <= n; s++) opp[s] = hidden.Where(t => t.Role == TileView.R.Opp && t.Seat == s).OrderBy(t => t.Idx).ToList();
            foreach (var t in hidden.Where(t => t.Role != TileView.R.Pool && !(t.Role == TileView.R.Opp && t.Seat >= 1 && t.Seat <= n)))
            {
                if (info.ActorRel >= 1 && info.ActorRel <= n) { t.Role = TileView.R.Opp; t.Seat = info.ActorRel; opp[info.ActorRel].Add(t); }
                else { t.Role = TileView.R.Pool; pool.Add(t); }
            }
            int next = pool.Count == 0 ? 0 : pool.Max(t => t.Idx) + 1;
            foreach (var t in pool) if (t.Role != TileView.R.Pool) { t.Role = TileView.R.Pool; }
            for (int s = 1; s <= n; s++)
                while (opp[s].Count > info.Opp[s - 1]) { var t = opp[s][opp[s].Count - 1]; opp[s].RemoveAt(opp[s].Count - 1); t.Role = TileView.R.Pool; t.Idx = next++; pool.Add(t); }
            int dealt = 0;
            for (int round = 0; round < 40; round++)
                for (int s = 1; s <= n; s++)
                {
                    if (opp[s].Count >= info.Opp[s - 1] || pool.Count == 0) continue;
                    var t = pool[pool.Count - 1]; pool.RemoveAt(pool.Count - 1);
                    t.Role = TileView.R.Opp; t.Seat = s; t.Idx = opp[s].Count; opp[s].Add(t);
                    if (info.Deal) t.Delay = .35f + (round * (n + 1) + s) * .05f;
                    dealt++;
                }
            pool.Sort((a, b) => a.Idx.CompareTo(b.Idx));
            if (pool.Count > 0 && pool[pool.Count - 1].Idx > pool.Count + 30) for (int i = 0; i < pool.Count; i++) pool[i].Idx = i;
            foreach (var t in pool) { t.Target = HeapPos(t.Idx, out var q); t.Rot = q; t.Scale = 1; }
            for (int s = 1; s <= n; s++)
                for (int j = 0; j < opp[s].Count; j++) { var t = opp[s][j]; t.Idx = j; t.Target = OppPos(s, j, opp[s].Count, out var q); t.Rot = q; t.Scale = OppScale; }
            foreach (var t in tiles)
            {
                if (!t.Shown) { t.Go.transform.position = t.Target; t.Go.transform.rotation = t.Rot; t.Go.transform.localScale = Vector3.one * t.Scale; }
                t.Shown = true; t.Go.SetActive(true);
                if (t.Role != TileView.R.Table && t.Role != TileView.R.Rack) t.Dragged = false;
            }
            if (info.Deal) Sfx.Play("tshuffle", .8f);
        }

        public void Backdrop()
        {
            if (!Ready) return;
            ShowScene();
            var cells = Board.Empty();
            int T(TileColor c, int v, int copy = 0) => copy * 52 + (int)c * 13 + v - 1;
            void Put(int row, int col, params int[] ids) => Board.Write(cells, row, col, ids);
            Put(1, 2, T(TileColor.Blau, 4), T(TileColor.Blau, 5), T(TileColor.Blau, 6), T(TileColor.Blau, 7));
            Put(1, 9, T(TileColor.Rot, 9), T(TileColor.Schwarz, 9), T(TileColor.Orange, 9));
            Put(3, 4, T(TileColor.Orange, 10), T(TileColor.Orange, 11), T(TileColor.Orange, 12), T(TileColor.Orange, 13));
            Put(3, 12, T(TileColor.Schwarz, 1), T(TileColor.Schwarz, 2), T(TileColor.Schwarz, 3));
            Put(5, 1, T(TileColor.Rot, 11), T(TileColor.Blau, 11), T(TileColor.Schwarz, 11), T(TileColor.Orange, 11));
            Put(5, 9, T(TileColor.Rot, 5), 104, T(TileColor.Rot, 7), T(TileColor.Rot, 8));
            int onTable = cells.Count(x => x >= 0);
            backdrop = false;
            Layout(cells, Board.Empty(RackRows * Board.Cols), new LayoutInfo { Pool = Tiles.Count - onTable, Opp = new int[0] });
            SetGlow(null, null);
            backdrop = true;
            var pts = Bounds(false);
            Env.Fit(pts, 40, new Vector2(.02f, .98f), new Vector2(.05f, .95f), out camPos, out camRot, out focus, out camDist);
        }

        public void SetGlow(Dictionary<int, Color> glow, HashSet<int> pulse)
        {
            foreach (var t in tiles) { t.Glow = glow != null && glow.TryGetValue(t.Id, out var c) ? c : Color.black; t.Pulse = pulse != null && pulse.Contains(t.Id); }
        }

        public void Drag(int id, Vector3 world)
        {
            var t = tiles[id]; t.Dragged = true; t.Delay = 0;
            var tr = t.Go.transform;
            tr.position = Vector3.Lerp(tr.position, world + new Vector3(0, .65f, .2f), .55f);
            tr.rotation = Quaternion.Slerp(tr.rotation, Quaternion.Euler(-10, 0, 0), .3f);
            tr.localScale = Vector3.one;
        }

        public void EndDrag(int id) { if (id >= 0) tiles[id].Dragged = false; }

        public Vector3 TilePos(int id) => tiles[id].Go.transform.position;

        public void HideAll() { foreach (var t in tiles) { t.Shown = false; t.Role = TileView.R.None; t.Go.SetActive(false); } }

        public void HideScene() { HideAll(); if (root) root.SetActive(false); }

        public void ShowScene() { if (root) root.SetActive(true); }

        Vector3[] Bounds(bool withRack)
        {
            float hw = FeltW / 2 + .3f, zt = TopEdge + .3f, zb = withRack ? RowZ(RackRows - 1) - RPZ / 2 - .2f : RackZ0 - 1;
            var l = new List<Vector3> { new Vector3(-hw, 0, zt), new Vector3(hw, 0, zt), new Vector3(-hw, 0, zb), new Vector3(hw, 0, zb), new Vector3(HeapX0 - .7f, 0, HeapZ0 - .8f), new Vector3(HeapX0 - .7f, .6f, HeapZ0 + 10 * 1.5f + .8f) };
            if (oppN > 0) { l.Add(new Vector3(-hw, 1.2f, TopEdge + 2.3f)); l.Add(new Vector3(hw, 1.2f, TopEdge + 2.3f)); }
            if (withRack) { l.Add(new Vector3(-hw, RowY(0) + .5f, zb)); l.Add(new Vector3(hw, RowY(0) + .5f, zb)); }
            return l.ToArray();
        }

        public void FitCamera()
        {
            if (!Cam || backdrop) return;
            Env.Fit(Bounds(true), 57, new Vector2(.008f, .838f), new Vector2(.008f, .835f), out camPos, out camRot, out focus, out camDist);
        }

        public void Update(float dt)
        {
            if (!root || !root.activeSelf || !Ready) return;
            time += dt;
            float k = 1 - Mathf.Exp(-dt * 10), pulse = .5f + .5f * Mathf.Sin(time * 5);
            int landed = 0;
            foreach (var t in tiles)
            {
                if (!t.Shown) continue;
                if (t.Delay > 0) { t.Delay -= dt; continue; }
                var tr = t.Go.transform;
                if (!t.Dragged)
                {
                    var p = tr.position; var tg = t.Target;
                    float hd = new Vector2(p.x - tg.x, p.z - tg.z).magnitude;
                    if (hd > .35f || Mathf.Abs(p.y - tg.y) > .35f) t.Flying = true;
                    p = Vector3.Lerp(p, tg + Vector3.up * Mathf.Min(hd * .22f, 1.6f), k);
                    tr.position = p;
                    tr.rotation = Quaternion.Slerp(tr.rotation, t.Rot, k);
                    if (Mathf.Abs(tr.localScale.x - t.Scale) > .001f) tr.localScale = Vector3.Lerp(tr.localScale, Vector3.one * t.Scale, k);
                    if (t.Flying && (p - tg).sqrMagnitude < .003f) { t.Flying = false; tr.position = tg; landed++; }
                }
                var e = t.Pulse ? Color.Lerp(t.Glow, new Color(.32f, .26f, .12f), pulse) : t.Glow;
                if (e != t.Applied) { t.Body.SetColor("_EmissionColor", e); t.Applied = e; }
            }
            if (landed > 0) Sfx.Play("tile", Mathf.Min(1, .4f + .12f * landed), .022f);
            if (backdrop)
            {
                float yaw = Mathf.Sin(time * .06f) * 14;
                var rot = Quaternion.Euler(40 + Mathf.Sin(time * .05f) * 3, yaw, 0);
                Cam.transform.SetPositionAndRotation(focus - rot * Vector3.forward * camDist, rot);
            }
            else Cam.transform.SetPositionAndRotation(camPos + new Vector3(Mathf.Sin(time * .23f) * .05f, Mathf.Sin(time * .31f) * .03f, Mathf.Sin(time * .17f) * .03f), camRot);
        }
    }

    public static class TileMesh
    {
        public static Mesh Build(float w, float d, float h, float r, float b, int seg = 6)
        {
            var outl = new List<Vector3>(); var dirs = new List<Vector3>();
            float hx = w / 2 - r, hz = d / 2 - r;
            var cc = new[] { new Vector2(hx, hz), new Vector2(-hx, hz), new Vector2(-hx, -hz), new Vector2(hx, -hz) };
            for (int c = 0; c < 4; c++)
                for (int i = 0; i <= seg; i++)
                {
                    float a = (c * 90 + 90f * i / seg) * Mathf.Deg2Rad;
                    dirs.Add(new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)));
                    outl.Add(new Vector3(cc[c].x, 0, cc[c].y));
                }
            int n = outl.Count;
            var v = new List<Vector3>(); var nm = new List<Vector3>(); var uv = new List<Vector2>();
            var t0 = new List<int>(); var t1 = new List<int>();
            Vector2 UV(Vector3 p) => new Vector2((p.x + w / 2) / w, (p.z + d / 2) / d);
            int ci = v.Count; v.Add(new Vector3(0, h, 0)); nm.Add(Vector3.up); uv.Add(new Vector2(.5f, .5f));
            int top = v.Count;
            for (int i = 0; i < n; i++) { var p = outl[i] + dirs[i] * (r - b); v.Add(p + Vector3.up * h); nm.Add(Vector3.up); uv.Add(UV(p)); }
            for (int i = 0; i < n; i++) { t0.Add(ci); t0.Add(top + (i + 1) % n); t0.Add(top + i); }
            void Ring(float rr, float y, float up, float side)
            {
                for (int i = 0; i < n; i++) { var p = outl[i] + dirs[i] * rr; v.Add(new Vector3(p.x, y, p.z)); nm.Add((Vector3.up * up + dirs[i] * side).normalized); uv.Add(UV(p)); }
            }
            void Quads(int a, int c)
            {
                for (int i = 0; i < n; i++) { int j = (i + 1) % n; t1.AddRange(new[] { a + i, a + j, c + j, a + i, c + j, c + i }); }
            }
            const int steps = 3;
            int prev = v.Count; Ring(r - b, h, 1, 0);
            for (int s = 1; s <= steps; s++)
            {
                float a = s / (float)steps * Mathf.PI / 2;
                int cur = v.Count; Ring(r - b + b * Mathf.Sin(a), h - b + b * Mathf.Cos(a), Mathf.Cos(a), Mathf.Sin(a));
                Quads(prev, cur); prev = cur;
            }
            int si = v.Count; Ring(r, h - b, 0, 1);
            int so = v.Count; Ring(r, b * .5f, 0, 1);
            Quads(si, so);
            int bo = v.Count; Ring(r - b * .5f, 0, -.7f, .7f);
            Quads(so, bo);
            int cb = v.Count; v.Add(Vector3.zero); nm.Add(Vector3.down); uv.Add(new Vector2(.5f, .5f));
            int bot = v.Count; Ring(r - b * .5f, 0, -1, 0);
            for (int i = 0; i < n; i++) { t1.Add(cb); t1.Add(bot + i); t1.Add(bot + (i + 1) % n); }
            var m = new Mesh { name = "Tile" };
            m.SetVertices(v); m.SetNormals(nm); m.SetUVs(0, uv);
            m.subMeshCount = 2; m.SetTriangles(t0, 0); m.SetTriangles(t1, 1);
            m.RecalculateBounds(); m.RecalculateTangents();
            return m;
        }
    }
}
