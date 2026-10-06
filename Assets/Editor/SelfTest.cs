using System.IO;
using System.Linq;
using RommeCup.Rummikub;
using UnityEditor;
using UnityEngine;

namespace RommeCup.EditorTools
{
    public static class SelfTest
    {
        static int fails;

        static void Check(bool ok, string what) { if (!ok) { fails++; Debug.LogError("[RC-TEST] FAIL " + what); } }

        static int T(TileColor c, int n) => (int)c * 13 + n - 1;

        static int C(int suit, int rank, int deck = 0) => deck * 52 + suit * 13 + rank;

        static void CardTests()
        {
            const int J = 104, J2 = 105;
            Check(Karten.Cards.Arrange(new[] { C(0, 6), C(1, 6), C(2, 6) })?.Kind == Karten.MeldKind.Set, "k set3");
            Check(Karten.Cards.Arrange(new[] { C(0, 6), C(0, 6, 1), C(2, 6) }) == null, "k set dup suit");
            Check(Karten.Cards.Arrange(new[] { C(0, 6), C(1, 6), C(2, 6), C(3, 6), J }) == null, "k set5");
            Check(Karten.Cards.Arrange(new[] { C(0, 3), C(0, 4), C(0, 5) })?.Points == 15, "k run pts");
            Check(Karten.Cards.Arrange(new[] { C(1, 0), C(1, 1), C(1, 2) })?.Points == 6, "k ace low");
            Check(Karten.Cards.Arrange(new[] { C(1, 11), C(1, 12), C(1, 0) })?.Points == 31, "k ace high");
            Check(Karten.Cards.Arrange(new[] { C(1, 12), C(1, 0), C(1, 1) }) == null, "k no wrap");
            Check(Karten.Cards.Arrange(new[] { C(2, 4), J, C(2, 6) })?.Points == 18, "k joker gap");
            Check(Karten.Cards.Arrange(new[] { C(2, 4), J, J2 }) == null, "k two jokers");
            Check(Karten.Cards.Arrange(new[] { C(2, 3), C(2, 4), C(2, 5), J })?.Points == 22, "k joker end pts");
            Check(Karten.Cards.Swapped(new System.Collections.Generic.List<int> { C(2, 4), J, C(2, 6) }, J, C(2, 5)) != null, "k swap ok");
            Check(Karten.Cards.Swapped(new System.Collections.Generic.List<int> { C(2, 4), J, C(2, 6) }, J, C(2, 8)) == null, "k swap bad");
            Check(Karten.Cards.Arrange(new[] { C(0, 5), C(0, 6), C(0, 7), C(0, 8), C(0, 9), C(0, 10), C(0, 11), C(0, 12), C(0, 0), C(0, 1) }) == null, "k wrap long");
            int rounds = 0, wins = 0, moves = 0;
            for (int g = 0; g < 30; g++)
            {
                var s = new Karten.CardGame { Rounds = 2 };
                for (int p = 0; p < 2 + g % 3; p++) s.P.Add(new Karten.CPlayer { Name = "AI" + p, Ai = true });
                for (int r = 0; r < 2; r++)
                {
                    s.StartRound();
                    int guard = 0;
                    while (!s.RoundOver && guard++ < 3000)
                    {
                        int cur = s.Current, step = 0;
                        while (!s.RoundOver && s.Current == cur && step < 60)
                        {
                            var a = Karten.CardAi.Next(s, cur, step++);
                            var err = Karten.CardAi.Apply(s, cur, a);
                            if (err != null) { Check(false, "k ai action " + a.K + ": " + err); if (!s.Drawn) s.DrawStock(cur); else s.Discarding(cur, s.P[cur].Hand[0]); }
                            moves++;
                            int total = s.Stock.Count + s.Discard.Count + s.P.Sum(p => p.Hand.Count) + s.Table.Sum(m => m.Count);
                            Check(total == Karten.Cards.Count, "k conservation " + total);
                            Check(s.Table.All(m => Karten.Cards.Arrange(m) != null), "k table valid");
                        }
                    }
                    Check(s.RoundOver, "k round finished");
                    Check(s.P.Sum(p => p.LastDelta) == 0, "k zero-sum");
                    rounds++; if (s.P[s.Winner].Hand.Count == 0) wins++;
                    if (r == 0 && !s.MatchOver) { }
                }
            }
            Debug.Log("[RC-TEST] cards rounds=" + rounds + " wins=" + wins + " moves=" + moves);
            var sheet = new Texture2D(Karten.CardArt.W * 14, Karten.CardArt.H * 4, TextureFormat.RGBA32, false);
            for (int k = 0; k < 54; k++)
            {
                var f = k == 53 ? Karten.CardArt.CardBack(false) : Karten.CardArt.Face(k);
                var rt = RenderTexture.GetTemporary(Karten.CardArt.W, Karten.CardArt.H);
                Graphics.Blit(f, rt);
                RenderTexture.active = rt;
                var tmp = new Texture2D(Karten.CardArt.W, Karten.CardArt.H, TextureFormat.RGBA32, false);
                tmp.ReadPixels(new Rect(0, 0, Karten.CardArt.W, Karten.CardArt.H), 0, 0); tmp.Apply();
                RenderTexture.active = null; RenderTexture.ReleaseTemporary(rt);
                int col = k >= 52 ? 13 : k % 13, row = k == 52 ? 1 : k == 53 ? 0 : 3 - k / 13;
                sheet.SetPixels(col * Karten.CardArt.W, row * Karten.CardArt.H, Karten.CardArt.W, Karten.CardArt.H, tmp.GetPixels());
            }
            sheet.Apply();
            Directory.CreateDirectory("Builds");
            File.WriteAllBytes("Builds/cards_preview.png", sheet.EncodeToPNG());
        }

        [MenuItem("RommeCup/Run Self Test")]
        public static void Run()
        {
            fails = 0;
            const int J = 104;
            Check(Meld.Check(new[] { T(TileColor.Schwarz, 7), T(TileColor.Rot, 7), T(TileColor.Blau, 7) }).Kind == MeldKind.Group, "group3");
            Check(!Meld.Check(new[] { T(TileColor.Schwarz, 7), T(TileColor.Schwarz, 7) + 52, T(TileColor.Blau, 7) }).Valid, "group dup color");
            Check(Meld.Check(new[] { T(TileColor.Orange, 4), T(TileColor.Orange, 5), T(TileColor.Orange, 6) }).Points == 15, "run points");
            Check(Meld.Check(new[] { J, T(TileColor.Schwarz, 5), T(TileColor.Schwarz, 6) }).Points == 15, "joker front as laid");
            Check(Meld.Check(new[] { T(TileColor.Schwarz, 12), T(TileColor.Schwarz, 13), J }).Points == 36, "joker shifted to front");
            Check(!Meld.Check(new[] { T(TileColor.Schwarz, 12), T(TileColor.Schwarz, 13), T(TileColor.Schwarz, 1) }).Valid, "no wrap");
            Check(Meld.Check(new[] { T(TileColor.Blau, 9), T(TileColor.Blau, 7), T(TileColor.Blau, 8) }).Valid, "unsorted run");
            Check(!Meld.Check(new[] { T(TileColor.Blau, 9), T(TileColor.Rot, 10), T(TileColor.Blau, 11) }).Valid, "mixed run");
            var empty = Board.Empty();
            var c1 = Board.Empty(); Board.Write(c1, 0, 0, new[] { T(TileColor.Schwarz, 10), T(TileColor.Rot, 10), T(TileColor.Blau, 10) });
            var rack = new[] { T(TileColor.Schwarz, 10), T(TileColor.Rot, 10), T(TileColor.Blau, 10), 5 };
            Check(Rules.Validate(empty, c1, rack, false, out _, out var mp) == null && mp == 30, "initial meld 30");
            var c2 = Board.Empty(); Board.Write(c2, 0, 0, new[] { T(TileColor.Schwarz, 9), T(TileColor.Rot, 9), T(TileColor.Blau, 9) });
            Check(Rules.Validate(empty, c2, new[] { T(TileColor.Schwarz, 9), T(TileColor.Rot, 9), T(TileColor.Blau, 9) }, false, out _, out _) != null, "initial meld 27 rejected");

            int rounds = 0, moves = 0, blocked = 0;
            for (int g = 0; g < 25; g++)
            {
                var s = new GameState { Rounds = 3 };
                for (int p = 0; p < 2 + g % 3; p++) s.P.Add(new Player { Name = "AI" + p, Ai = true });
                for (int r = 0; r < 3; r++)
                {
                    s.StartRound();
                    for (int guard = 0; guard < 3000 && !s.RoundOver; guard++)
                    {
                        int cur = s.Current;
                        var plan = AiPlayer.Plan(s, cur);
                        if (plan == null || s.Commit(cur, plan) != null) { if (plan != null) Check(false, "ai plan rejected: " + Rules.Validate(s.Table, plan, s.P[cur].Rack, s.P[cur].Melded, out _, out _)); s.Draw(cur); }
                        moves++;
                        int total = s.Pool.Count + s.P.Sum(p => p.Rack.Count) + s.Table.Count(t => t >= 0);
                        Check(total == Tiles.Count, "tile conservation " + total);
                        Check(Board.Segments(s.Table).All(x => Meld.Check(x.Tiles).Valid), "table valid");
                    }
                    Check(s.RoundOver, "round finished");
                    Check(s.P.Sum(p => p.LastDelta) == 0, "zero-sum scoring");
                    rounds++; if (s.Blocked) blocked++;
                }
            }
            Debug.Log("[RC-TEST] simulated rounds=" + rounds + " moves=" + moves + " blocked=" + blocked);

            CardTests();

            var sheet = new Texture2D(TileArt.W * 14, TileArt.H * 4, TextureFormat.RGBA32, false);
            for (int k = 0; k < 53; k++)
            {
                var f = TileArt.Face(k);
                var rt = RenderTexture.GetTemporary(TileArt.W, TileArt.H);
                Graphics.Blit(f, rt);
                RenderTexture.active = rt;
                var tmp = new Texture2D(TileArt.W, TileArt.H, TextureFormat.RGBA32, false);
                tmp.ReadPixels(new Rect(0, 0, TileArt.W, TileArt.H), 0, 0); tmp.Apply();
                RenderTexture.active = null; RenderTexture.ReleaseTemporary(rt);
                int col = k == 52 ? 13 : k % 13, row = k == 52 ? 0 : 3 - k / 13;
                sheet.SetPixels(col * TileArt.W, row * TileArt.H, TileArt.W, TileArt.H, tmp.GetPixels());
            }
            sheet.Apply();
            Directory.CreateDirectory("Builds");
            File.WriteAllBytes("Builds/tiles_preview.png", sheet.EncodeToPNG());
            File.WriteAllBytes("Builds/icon_preview.png", TileArt.Icon(256).EncodeToPNG());
            Debug.Log("[RC-TEST] RESULT fails=" + fails);
            if (Application.isBatchMode) EditorApplication.Exit(fails == 0 ? 0 : 1);
        }
    }
}
