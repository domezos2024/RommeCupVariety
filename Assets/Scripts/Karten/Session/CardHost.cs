using System;
using System.Collections.Generic;
using System.Linq;
using RommeCup.App;
using RommeCup.Core;
using RommeCup.Rummikub;
using UnityEngine;

namespace RommeCup.Karten
{
    [Serializable]
    public class CMsg : IRoundMsg
    {
        public string t, name, text;
        public int seat = -1, pool, dcount, current, round, rounds, winner = -1, phase, took = -1, actor = -1, mult = 1, meld = -1, card = -1, joker = -1;
        public bool over;
        public int[] hand, table, disc, cards;
        public PInfo[] players;
        PInfo[] IRoundMsg.Players => players;
        int IRoundMsg.Winner => winner;
        int IRoundMsg.Round => round;
        int IRoundMsg.Rounds => rounds;
        string IRoundMsg.Text => text;
        bool IRoundMsg.Over => over;
    }

    public class CardHost : ILobby
    {
        const string M = "CardHost";
        public class Seat { public string Name; public GameHost.Kind K; public int Conn = -1; }
        public readonly List<Seat> Seats = new List<Seat>();
        public CardGame G;
        public int Rounds { get; set; } = 3;
        public bool Started, Autopilot;
        readonly System.Random rng = new System.Random();
        float aiDelay = 1.5f;
        public int Count => Seats.Count;
        public string Name(int i) => Seats[i].Name;
        public bool Ai(int i) => Seats[i].K == GameHost.Kind.Ai;
        public Action<CMsg> LocalSink;
        public Action<int, string> RemoteSend;
        float aiTimer;
        int aiSteps;

        public int AddLocal(string n) { Seats.Add(new Seat { Name = n, K = GameHost.Kind.Local }); return Seats.Count - 1; }
        public int AddRemote(string n, int conn) { Seats.Add(new Seat { Name = Unique(n), K = GameHost.Kind.Remote, Conn = conn }); Log.I(M, "remote " + n + " conn=" + conn); return Seats.Count - 1; }
        public void AddAi() { if (Seats.Count < 4) Seats.Add(new Seat { Name = GameHost.AiNames.First(a => Seats.All(s => s.Name != a)), K = GameHost.Kind.Ai }); }
        public void RemoveAi() { var i = Seats.FindLastIndex(s => s.K == GameHost.Kind.Ai); if (i > 0) Seats.RemoveAt(i); }
        public int SeatOfConn(int conn) => Seats.FindIndex(s => s.K == GameHost.Kind.Remote && s.Conn == conn);
        string Unique(string n) { var b = string.IsNullOrWhiteSpace(n) ? "Gast" : n.Trim(); var r = b; for (int i = 2; Seats.Any(s => s.Name == r); i++) r = b + " " + i; return r; }

        public PInfo[] LobbyInfos() => Seats.Select(s => new PInfo { name = s.Name, ai = s.K == GameHost.Kind.Ai }).ToArray();

        public void BroadcastLobby()
        {
            for (int i = 0; i < Seats.Count; i++) Send(i, new CMsg { t = "lobby", seat = i, rounds = Rounds, players = LobbyInfos() });
        }

        public void DropConn(int conn)
        {
            int i = SeatOfConn(conn);
            if (i < 0) return;
            if (!Started) { Seats.RemoveAt(i); BroadcastLobby(); return; }
            Seats[i].K = GameHost.Kind.Ai; Seats[i].Conn = -1;
            G.P[i].Ai = true; G.P[i].Online = false;
            G.LastEvent = Seats[i].Name + " hat die Verbindung verloren - der Computer spielt weiter.";
            SendState();
        }

        public void StartMatch()
        {
            G = new CardGame { Rounds = Rounds };
            foreach (var s in Seats) G.P.Add(new CPlayer { Name = s.Name, Ai = s.K == GameHost.Kind.Ai });
            Started = true;
            Log.I(M, "match seats=" + Seats.Count + " rounds=" + Rounds);
            G.StartRound();
            SendState();
        }

        public void NextRound()
        {
            if (G == null || !G.RoundOver) return;
            if (G.MatchOver) { StartMatch(); return; }
            G.StartRound();
            SendState();
        }

        public static List<List<int>> Unflatten(int[] f)
        {
            var r = new List<List<int>>(); var cur = new List<int>();
            if (f == null) return r;
            foreach (var c in f) { if (c < 0) { if (cur.Count > 0) r.Add(cur); cur = new List<int>(); } else cur.Add(c); }
            if (cur.Count > 0) r.Add(cur);
            return r;
        }

        public static int[] Flatten(IEnumerable<IEnumerable<int>> g)
        {
            var l = new List<int>();
            foreach (var m in g) { l.AddRange(m); l.Add(-1); }
            return l.ToArray();
        }

        public void Receive(int seat, CMsg m)
        {
            if (G == null || m == null) return;
            Log.I(M, "seat=" + seat + " t=" + m.t);
            string err;
            switch (m.t)
            {
                case "stock": err = G.DrawStock(seat); break;
                case "disc": err = G.DrawDiscard(seat); break;
                case "meld": err = G.Meld(seat, Unflatten(m.cards)); break;
                case "layoff": err = G.LayOff(seat, m.meld, m.cards == null ? new List<int>() : m.cards.ToList()); break;
                case "swap": err = G.Swap(seat, m.meld, m.joker, m.card); break;
                case "discard": err = G.Discarding(seat, m.card); break;
                case "undo": err = G.Undo(seat); break;
                default: return;
            }
            if (err != null) Send(seat, new CMsg { t = "reject", text = err }); else SendState();
        }

        public void SendState() { for (int i = 0; i < Seats.Count; i++) Send(i, StateFor(i)); }

        CMsg StateFor(int i) => new CMsg
        {
            t = G.RoundOver ? "round" : "state", seat = i, hand = G.P[i].Hand.ToArray(), table = Flatten(G.Table), disc = G.Discard.Skip(Math.Max(0, G.Discard.Count - 3)).ToArray(),
            pool = G.Stock.Count, dcount = G.Discard.Count, current = G.Current, phase = G.Drawn ? 1 : 0, round = G.Round, rounds = G.Rounds, winner = G.Winner, over = G.MatchOver,
            text = G.LastEvent, took = G.Took, actor = G.Actor, mult = G.Mult,
            players = G.P.Select((p, k) => new PInfo { name = p.Name, tiles = p.Hand.Count, score = p.Score, delta = p.LastDelta, hand = G.HandValue(k), melded = p.Melded, ai = p.Ai, online = p.Online }).ToArray()
        };

        void Send(int seat, CMsg m)
        {
            var s = Seats[seat];
            if (s.K == GameHost.Kind.Local) LocalSink?.Invoke(m);
            else if (s.K == GameHost.Kind.Remote) RemoteSend?.Invoke(s.Conn, JsonUtility.ToJson(m));
        }

        public void Tick(float dt)
        {
            bool ai = Seats[G?.Current ?? 0].K == GameHost.Kind.Ai || (Autopilot && Seats[G?.Current ?? 0].K == GameHost.Kind.Local);
            if (!Started || G == null || G.RoundOver || !ai) { aiTimer = 0; return; }
            if (aiTimer == 0) aiDelay = (G.Drawn ? .8f : 1.3f) + (float)rng.NextDouble() * (G.Drawn ? .9f : 1.4f);
            aiTimer += dt;
            if (aiTimer < aiDelay) return;
            aiTimer = 0;
            int cur = G.Current;
            if (!G.Drawn) aiSteps = 0;
            string err = null;
            try
            {
                var a = CardAi.Next(G, cur, aiSteps++);
                err = CardAi.Apply(G, cur, a);
                if (err != null) Log.W(M, "ai " + a.K + ": " + err);
            }
            catch (Exception e) { Log.E(M, "ai " + e); err = "ex"; }
            if (err != null)
            {
                if (!G.Drawn) { if (G.DrawStock(cur) != null) G.DrawDiscard(cur); }
                else G.Discarding(cur, G.P[cur].Hand.OrderBy(c => Cards.IsJoker(c) ? 1 : 0).First(c => c != G.Took || G.P[cur].Hand.Count == 1));
            }
            SendState();
        }
    }
}
