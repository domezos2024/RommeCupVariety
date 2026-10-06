using System.Collections;
using System.Collections.Generic;
using System.Linq;
using RommeCup.App;
using RommeCup.Core;
using UnityEngine;

namespace RommeCup.Karten
{
    public enum CTag { None, Hand, Meld, Staged, Stock, Discard }

    public class CView
    {
        public int Key, Face = -1, Back = -1, Idx = -1, Card = -1;
        public CTag Tag;
        public GameObject Go;
        public MeshRenderer R;
        public Vector3 Pos;
        public Quaternion Rot = Quaternion.identity;
        public float Scale = 1, Delay;
        public bool Pulse, Flying;
        public Color Glow, Applied = new Color(-1, 0, 0);
    }

    public class TableDto
    {
        public int[] Hand = new int[0], Disc = new int[0];
        public List<int[]> Melds = new List<int[]>(), Staged = new List<int[]>();
        public HashSet<int> Sel = new HashSet<int>(), Hint = new HashSet<int>();
        public int Pool, DCount, Me, Current, Actor = -1, N;
        public PInfo[] Players;
        public bool PulseStock, PulseDisc, SelValid, Deal;
    }

    public class CardTable
    {
        const string M = "CardTable";
        public const float CW = 1f, CD = 1.4f, TH = .008f, HandZ = -5.1f, StageZ = -2.9f, PileZ = 5.0f, TopZ = 7.3f, SideX = 10.6f, MeldTop = 3.1f, HandScale = 2.05f, MeldScale = 1.55f, PileScale = 1.22f, CardStep = .0062f;
        static readonly Quaternion Down = Quaternion.Euler(0, 0, 180);
        static readonly Vector3 StockPos = new Vector3(-2.1f, 0, PileZ), DiscPos = new Vector3(1.9f, 0, PileZ);
        public Camera Cam => Env.Cam;
        public bool Ready, Building;
        GameObject root, stockBlock, discBlock;
        Mesh mesh;
        readonly Material[] faces = new Material[55];
        Material edge;
        readonly Dictionary<int, CView> live = new Dictionary<int, CView>();
        readonly Stack<CView> pool = new Stack<CView>();
        readonly Dictionary<GameObject, CView> byGo = new Dictionary<GameObject, CView>();
        readonly TextMesh[] labels = new TextMesh[4];
        readonly MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        float time;
        int fitN = -1;
        bool backdrop;
        Vector3 camPos, focus;
        Quaternion camRot;
        float camDist;

        public static int BackOf(int card) => card < 0 ? CardArt.BackRed : card < 52 || (card >= 104 && card < 107) ? CardArt.BackRed : CardArt.BackBlue;

        public IEnumerator Build()
        {
            if (Ready) yield break;
            if (Building) { while (!Ready) yield return null; yield break; }
            Building = true;
            mesh = CardMesh.Build(CW, CD, TH, .07f);
            var n = CardArt.PaperNormal();
            for (int i = 0; i < 53; i++)
            {
                faces[i] = Mats.Emissive(Color.white, 0, .42f, CardArt.Face(i), n, .25f);
                if (i % 4 == 3) yield return null;
            }
            faces[CardArt.BackRed] = Mats.Emissive(Color.white, 0, .48f, CardArt.CardBack(false), n, .25f);
            faces[CardArt.BackBlue] = Mats.Emissive(Color.white, 0, .48f, CardArt.CardBack(true), n, .25f);
            edge = Mats.Emissive(new Color(.95f, .93f, .9f), 0, .3f);
            yield return null;
            BuildScene();
            Ready = true; Building = false;
            Log.I(M, "built");
        }

        void BuildScene()
        {
            root = new GameObject("CardTable");
            const float w = 30f, d = 19.4f, cz = 1.15f;
            Env.BuildTable(root.transform, new Vector3(0, 0, cz), w, d, new Rect(-27, -16, 54, 34));
            Fx.Dust(root.transform, new Vector3(0, 3.2f, cz), new Vector3(30, 5, 20));
            var stripe = Mats.Lit(Color.white, 0, .3f, CardArt.EdgeStripes());
            stripe.mainTextureScale = new Vector2(1, 6);
            stockBlock = Env.Cube(root.transform, "StockBlock", StockPos, Vector3.one, stripe);
            discBlock = Env.Cube(root.transform, "DiscBlock", DiscPos, Vector3.one, stripe);
            root.SetActive(false);
        }

        public void SetVisible(bool v)
        {
            if (root) root.SetActive(v);
            if (!v) { Clear(); fitN = -1; }
        }

        public void Backdrop()
        {
            Clear();
            if (!root) return;
            root.SetActive(true);
            var d = new TableDto { N = 1, Pool = 70, DCount = 6, Disc = new[] { 22, 61, 9 }, Players = new PInfo[0] };
            d.Melds.Add(new[] { 13 * 0 + 6, 13 * 0 + 7, 13 * 0 + 8, 13 * 0 + 9 });
            d.Melds.Add(new[] { 11, 13 + 11, 26 + 11 });
            d.Melds.Add(new[] { 26 + 0, 26 + 1, 104, 26 + 3 });
            Layout(d);
            backdrop = true;
            var pts = new[] { new Vector3(-15, 0, -8), new Vector3(15, 0, -8), new Vector3(-15, 0, 10), new Vector3(15, 0, 10) };
            Env.Fit(pts, 40, new Vector2(.02f, .98f), new Vector2(.05f, .95f), out camPos, out camRot, out focus, out camDist);
            fitN = -1;
        }

        public void Clear()
        {
            foreach (var v in live.Values) Release(v);
            live.Clear();
            foreach (var l in labels) if (l) l.gameObject.SetActive(false);
        }

        void Release(CView v) { v.Go.SetActive(false); pool.Push(v); }

        CView Get(int key, int face, int back, Vector3 spawn, Quaternion spawnRot, float delay)
        {
            if (!live.TryGetValue(key, out var v))
            {
                if (pool.Count > 0) v = pool.Pop();
                else
                {
                    var go = new GameObject("Card", typeof(MeshFilter), typeof(MeshRenderer));
                    go.transform.SetParent(root.transform);
                    go.GetComponent<MeshFilter>().sharedMesh = mesh;
                    var bc = go.AddComponent<BoxCollider>(); bc.center = new Vector3(0, .02f, 0); bc.size = new Vector3(CW, .06f, CD);
                    v = new CView { Go = go, R = go.GetComponent<MeshRenderer>() };
                    byGo[go] = v;
                }
                v.Key = key; v.Face = -1; v.Back = -1; v.Applied = new Color(-1, 0, 0); v.Delay = delay; v.Flying = true;
                v.Go.transform.SetPositionAndRotation(spawn, spawnRot); v.Go.transform.localScale = Vector3.one * PileScale;
                v.Go.SetActive(true);
                live[key] = v;
            }
            if (v.Face != face || v.Back != back) { v.Face = face; v.Back = back; v.R.sharedMaterials = new[] { faces[face], faces[back], edge }; }
            return v;
        }

        static int Slot(int rel, int n) => n == 2 ? 1 : n == 3 ? (rel == 1 ? 0 : 2) : rel == 1 ? 0 : rel == 2 ? 1 : 2;

        static Vector3 SeatPos(int slot) => slot == 1 ? new Vector3(0, 0, TopZ) : new Vector3(slot == 0 ? -SideX : SideX, 0, 1.3f);

        Vector3 SpawnFor(TableDto d, out Quaternion rot)
        {
            rot = Down;
            if (d.Actor < 0 || d.N < 2) return StockPos + Vector3.up * .5f;
            int rel = (d.Actor - d.Me + d.N) % d.N;
            if (rel == 0) { rot = Quaternion.Euler(-20, 0, 0); return new Vector3(0, .5f, HandZ); }
            return SeatPos(Slot(rel, d.N)) + Vector3.up * .5f;
        }

        void Put(HashSet<int> used, TableDto d, int key, int face, int back, Vector3 pos, Quaternion rot, CTag tag, int idx, int card, Color glow, bool pulse, float scale, bool fromActor, float delay = 0)
        {
            used.Add(key);
            Vector3 sp; Quaternion sr;
            if (fromActor) sp = SpawnFor(d, out sr); else { sp = StockPos + Vector3.up * (.05f + d.Pool * CardStep * PileScale); sr = Down; }
            var v = Get(key, face, back, sp, sr, delay);
            v.Pos = pos; v.Rot = rot; v.Tag = tag; v.Idx = idx; v.Card = card; v.Glow = glow; v.Pulse = pulse; v.Scale = scale;
        }

        static float J(int id, int salt, float amp) => (Noise.Hash(id, salt) - .5f) * 2 * amp;

        public void Layout(TableDto d)
        {
            if (!Ready) return;
            backdrop = false;
            if (d.Deal) Clear();
            var used = new HashSet<int>();
            var none = Color.black;
            int n = d.Hand.Length;
            float step = n <= 1 ? 0 : Mathf.Min(2.9f, 42f / (n - 1)), R = 24f;
            var warm = new Color(.25f, .18f, .03f); var green = new Color(.04f, .26f, .1f);
            for (int i = 0; i < n; i++)
            {
                int c = d.Hand[i]; bool sel = d.Sel.Contains(c);
                float th = (i - (n - 1) / 2f) * step * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Sin(th), 0, Mathf.Cos(th));
                var pos = new Vector3(0, 0, HandZ - R) + dir * (R + (sel ? .55f : 0)) + Vector3.up * (.05f + .012f * i + (sel ? .3f : 0));
                var rot = Quaternion.Euler(0, th * Mathf.Rad2Deg, 0) * Quaternion.Euler(sel ? -22 : -30, 0, 0);
                Put(used, d, c, Cards.Face(c), BackOf(c), pos, rot, CTag.Hand, i, c, sel ? (d.SelValid ? green : warm) : none, d.Hint.Contains(c), HandScale, false, d.Deal ? .3f + i * (d.N) * .07f : 0);
            }
            PlaceGroups(used, d, d.Staged, StageZ, CTag.Staged, 1.08f, green);
            float ms = 1;
            foreach (var s in new[] { MeldScale, 1.35f, 1.15f, .98f, .84f, .7f })
            {
                ms = s;
                if (Rows(d.Melds, 16.5f, s) * 1.8f * s <= 7f) break;
            }
            PlaceGroups(used, d, d.Melds, MeldTop, CTag.Meld, ms, none, 16.5f, true);
            float dh = Mathf.Max(0, d.DCount - d.Disc.Length) * CardStep * PileScale;
            discBlock.SetActive(dh > 0);
            if (dh > 0) { discBlock.transform.position = DiscPos + Vector3.up * dh / 2; discBlock.transform.localScale = new Vector3(CW * PileScale * .97f, dh, CD * PileScale * .97f); }
            int m = d.Disc.Length;
            for (int i = 0; i < m; i++)
            {
                int c = d.Disc[i]; bool top = i == m - 1;
                var pos = DiscPos + new Vector3(J(c, 1, .16f), dh + TH * PileScale * (i + 1) + .002f, J(c, 2, .16f));
                Put(used, d, c, Cards.Face(c), BackOf(c), pos, Quaternion.Euler(0, J(c, 3, 11), 0), top ? CTag.Discard : CTag.None, -1, c, none, top && d.PulseDisc, PileScale, true);
            }
            float sh = Mathf.Max(0, d.Pool - 1) * CardStep * PileScale;
            stockBlock.SetActive(d.Pool > 1);
            if (d.Pool > 1) { stockBlock.transform.position = StockPos + Vector3.up * sh / 2; stockBlock.transform.localScale = new Vector3(CW * PileScale * .97f, sh, CD * PileScale * .97f); }
            if (d.Pool > 0) Put(used, d, 2000 + d.Pool % 2, CardArt.BackRed, CardArt.BackRed, StockPos + Vector3.up * (sh + TH * PileScale), Down * Quaternion.Euler(0, J(d.Pool, 4, 1.5f), 0), CTag.Stock, -1, -1, none, d.PulseStock, PileScale, false);
            for (int r = 1; r < d.N; r++)
            {
                int seat = (d.Me + r) % d.N, slot = Slot(r, d.N), cnt = d.Players[seat].tiles;
                float osp = cnt <= 1 ? 0 : Mathf.Min(.42f, 6.5f / (cnt - 1));
                var basePos = SeatPos(slot); var frame = Quaternion.Euler(0, slot == 1 ? 180 : slot == 0 ? 90 : -90, 0);
                for (int i = 0; i < cnt; i++)
                {
                    float o = (i - (cnt - 1) / 2f) * osp;
                    var local = new Vector3(o, .012f + .004f * i, -o * o * .02f);
                    int key = 1000 + r * 100 + i;
                    Put(used, d, key, CardArt.BackRed, Noise.Hash(key, d.Players[seat].score + 31) < .5f ? CardArt.BackRed : CardArt.BackBlue, basePos + frame * local, frame * Quaternion.Euler(0, o * 3f + J(key, 5, 1.5f), 180), CTag.None, -1, -1, none, false, 1.1f, false, d.Deal ? .3f + (i * d.N + r) * .07f : 0);
                }
                Label(r, slot, d.Players[seat].name + "  ·  " + cnt, seat == d.Current);
            }
            for (int r = Mathf.Max(1, d.N); r < 4; r++) if (labels[r]) labels[r].gameObject.SetActive(false);
            foreach (var key in live.Keys.Where(x => !used.Contains(x)).ToList()) { Release(live[key]); live.Remove(key); }
            if (fitN != d.N) { fitN = d.N; Fit(d.N); }
            if (d.Deal) Sfx.Play("riffle", .8f);
        }

        static int Rows(List<int[]> groups, float width, float s)
        {
            int rows = groups.Count == 0 ? 0 : 1; float x = 0;
            foreach (var g in groups)
            {
                float w = (CW + .42f * (g.Length - 1)) * s;
                if (x > 0 && x + w > width) { rows++; x = 0; }
                x += w + .8f * s;
            }
            return rows;
        }

        void PlaceGroups(HashSet<int> used, TableDto d, List<int[]> groups, float z0, CTag tag, float s, Color glow, float width = 20, bool wrap = false)
        {
            var rows = new List<List<int>>(); var cur = new List<int>(); float x = 0;
            for (int gi = 0; gi < groups.Count; gi++)
            {
                float w = (CW + .42f * (groups[gi].Length - 1)) * s;
                if (wrap && x > 0 && x + w > width) { rows.Add(cur); cur = new List<int>(); x = 0; }
                cur.Add(gi); x += w + .8f * s;
            }
            if (cur.Count > 0) rows.Add(cur);
            for (int r = 0; r < rows.Count; r++)
            {
                float tw = rows[r].Sum(gi => (CW + .42f * (groups[gi].Length - 1)) * s) + .8f * s * (rows[r].Count - 1);
                float px = -tw / 2;
                foreach (var gi in rows[r])
                {
                    var g = groups[gi];
                    for (int i = 0; i < g.Length; i++)
                        Put(used, d, g[i], Cards.Face(g[i]), BackOf(g[i]), new Vector3(px + (CW * s) / 2 + .42f * s * i + J(g[i], 6, .02f), .004f + TH * s * 1.2f * i, z0 - r * 1.8f * s + J(g[i], 7, .03f)), Quaternion.Euler(0, J(g[i], 8, 1.4f), 0), tag, gi, g[i], glow, false, s, true);
                    px += (CW + .42f * (g.Length - 1)) * s + .8f * s;
                }
            }
        }

        void Label(int r, int slot, string text, bool cur)
        {
            if (!labels[r])
            {
                var go = new GameObject("Label" + r);
                go.transform.SetParent(root.transform);
                var tm = go.AddComponent<TextMesh>();
                tm.font = UiKit.Font; tm.fontSize = 60; tm.characterSize = .06f; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.fontStyle = FontStyle.Bold;
                go.GetComponent<MeshRenderer>().sharedMaterial = UiKit.Font.material;
                go.transform.rotation = Quaternion.Euler(90, 0, 0);
                labels[r] = tm;
            }
            var l = labels[r];
            l.gameObject.SetActive(true);
            l.text = text; l.color = cur ? Theme.Brass : new Color(.92f, .88f, .8f, .8f);
            l.transform.position = slot == 1 ? new Vector3(0, .02f, TopZ + 1.5f) : new Vector3(slot == 0 ? -SideX : SideX, .02f, 5.4f);
        }

        void Fit(int n)
        {
            float hx = n >= 3 ? SideX + 1.6f : 8.6f, zt = (n == 2 || n == 4) ? TopZ + 2f : PileZ + 1.6f, zb = HandZ - 1.5f;
            var pts = new[] { new Vector3(-hx, .3f, zt), new Vector3(hx, .3f, zt), new Vector3(-hx, 0, zb), new Vector3(hx, 0, zb), new Vector3(-9, .4f, HandZ), new Vector3(9, .4f, HandZ) };
            Env.Fit(pts, 62, new Vector2(.01f, .835f), new Vector2(.01f, .83f), out camPos, out camRot, out focus, out camDist);
        }

        public CView Pick(Vector2 screen)
        {
            if (!Ready || !root || !root.activeSelf) return null;
            Physics.SyncTransforms();
            if (Physics.Raycast(Cam.ScreenPointToRay(screen), out var hit, 200f) && byGo.TryGetValue(hit.collider.gameObject, out var v) && v.Go.activeSelf) return v;
            return null;
        }

        public void Update(float dt)
        {
            if (!Ready || !root || !root.activeSelf) return;
            time += dt;
            float k = 1 - Mathf.Exp(-dt * 10), pulse = .5f + .5f * Mathf.Sin(time * 5);
            int landed = 0, launched = 0;
            foreach (var v in live.Values)
            {
                if (v.Delay > 0) { v.Delay -= dt; if (v.Delay <= 0) launched++; continue; }
                var t = v.Go.transform;
                var p = t.position;
                float hd = new Vector2(p.x - v.Pos.x, p.z - v.Pos.z).magnitude;
                if (hd > .5f) { if (!v.Flying && hd > 2) launched++; v.Flying = true; }
                p = Vector3.Lerp(p, v.Pos + Vector3.up * Mathf.Min(hd * .12f, .9f), k);
                t.position = p;
                t.rotation = Quaternion.Slerp(t.rotation, v.Rot, k);
                if (Mathf.Abs(t.localScale.x - v.Scale) > .001f) t.localScale = Vector3.Lerp(t.localScale, Vector3.one * v.Scale, k);
                if (v.Flying && (p - v.Pos).sqrMagnitude < .002f) { v.Flying = false; t.position = v.Pos; landed++; }
                var e = v.Pulse ? Color.Lerp(new Color(.06f, .05f, .02f), new Color(.3f, .24f, .1f), pulse) : v.Glow;
                if (e != v.Applied) { mpb.SetColor("_EmissionColor", e); v.R.SetPropertyBlock(mpb); v.Applied = e; }
            }
            if (launched > 0) Sfx.Play("cslide", .55f, .05f);
            if (landed > 0) Sfx.Play("card", Mathf.Min(1, .45f + .1f * landed), .03f);
            if (backdrop)
            {
                var rot = Quaternion.Euler(40 + Mathf.Sin(time * .05f) * 3, Mathf.Sin(time * .06f) * 14, 0);
                Cam.transform.SetPositionAndRotation(focus - rot * Vector3.forward * camDist, rot);
            }
            else Cam.transform.SetPositionAndRotation(camPos + new Vector3(Mathf.Sin(time * .23f) * .05f, Mathf.Sin(time * .31f) * .03f, Mathf.Sin(time * .17f) * .03f), camRot);
        }
    }
}
