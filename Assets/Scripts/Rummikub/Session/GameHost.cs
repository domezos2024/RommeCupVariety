using System;
using System.Collections.Generic;
using System.Linq;
using RommeCup.App;
using RommeCup.Core;
using UnityEngine;

namespace RommeCup.Rummikub
{
    [Serializable]
    public class Msg : IRoundMsg
    {
        public string t, name, text;
        public int seat = -1, pool, current, round, rounds, winner = -1;
        public bool blocked, over;
        public int[] cells, rack;
        public PInfo[] players;
        PInfo[] IRoundMsg.Players => players;
        int IRoundMsg.Winner => winner;
        int IRoundMsg.Round => round;
        int IRoundMsg.Rounds => rounds;
        string IRoundMsg.Text => text;
        bool IRoundMsg.Over => over;
    }

    public class GameHost : ILobby
    {
        const string M = "GameHost";
        public enum Kind { Local, Remote, Ai }
        public class Seat { public string Name; public Kind K; public int Conn = -1; }

        public static readonly string[] AiNames = { "Helga", "Bernd", "Sabine" };
        public readonly List<Seat> Seats = new List<Seat>();
        public GameState S;
        public int Rounds { get; set; } = 3;
        public bool Started, Autopilot;
        public Action<Msg> LocalSink;
        public Action<int, string> RemoteSend;
        readonly System.Random rng = new System.Random();
        float aiTimer, aiDelay = 2;

        public int Count => Seats.Count;
        public string Name(int i) => Seats[i].Name;
        public bool Ai(int i) => Seats[i].K == Kind.Ai;
        public int AddLocal(string n) { Seats.Add(new Seat { Name = n, K = Kind.Local }); return Seats.Count - 1; }
        public int AddRemote(string n, int conn) { Seats.Add(new Seat { Name = Unique(n), K = Kind.Remote, Conn = conn }); Log.I(M, "remote " + n + " conn=" + conn); return Seats.Count - 1; }
        public void AddAi() { if (Seats.Count < 4) Seats.Add(new Seat { Name = AiNames.First(a => Seats.All(s => s.Name != a)), K = Kind.Ai }); }
        public void RemoveAi() { var i = Seats.FindLastIndex(s => s.K == Kind.Ai); if (i > 0) Seats.RemoveAt(i); }
        public int SeatOfConn(int conn) => Seats.FindIndex(s => s.K == Kind.Remote && s.Conn == conn);
        string Unique(string n) { var b = string.IsNullOrWhiteSpace(n) ? "Gast" : n.Trim(); var r = b; for (int i = 2; Seats.Any(s => s.Name == r); i++) r = b + " " + i; return r; }

        public PInfo[] LobbyInfos() => Seats.Select(s => new PInfo { name = s.Name, ai = s.K == Kind.Ai }).ToArray();

        public void BroadcastLobby()
        {
            for (int i = 0; i < Seats.Count; i++) Send(i, new Msg { t = "lobby", seat = i, rounds = Rounds, players = LobbyInfos() });
        }

        public void DropConn(int conn)
        {
            int i = SeatOfConn(conn);
            if (i < 0) return;
            if (!Started) { Seats.RemoveAt(i); BroadcastLobby(); return; }
            Seats[i].K = Kind.Ai; Seats[i].Conn = -1;
            S.P[i].Ai = true; S.P[i].Online = false;
            S.LastEvent = Seats[i].Name + " hat die Verbindung verloren - der Computer spielt weiter.";
            SendState();
        }

        public void StartMatch()
        {
            S = new GameState { Rounds = Rounds };
            foreach (var s in Seats) S.P.Add(new Player { Name = s.Name, Ai = s.K == Kind.Ai });
            Started = true;
            Log.I(M, "match seats=" + Seats.Count + " rounds=" + Rounds);
            S.StartRound();
            SendState();
        }

        public void NextRound()
        {
            if (S == null || !S.RoundOver) return;
            if (S.MatchOver) { StartMatch(); return; }
            S.StartRound();
            SendState();
        }

        public void Receive(int seat, Msg m)
        {
            if (S == null || m == null) return;
            Log.I(M, "seat=" + seat + " t=" + m.t);
            switch (m.t)
            {
                case "commit":
                    var err = S.Commit(seat, m.cells);
                    if (err != null) Send(seat, new Msg { t = "reject", text = err }); else SendState();
                    break;
                case "draw":
                    if (seat == S.Current) { S.Draw(seat); SendState(); }
                    break;
                case "preview":
                    if (seat != S.Current) break;
                    for (int i = 0; i < Seats.Count; i++) if (i != seat) Send(i, new Msg { t = "preview", seat = seat, cells = m.cells });
                    break;
            }
        }

        public void SendState() { for (int i = 0; i < Seats.Count; i++) Send(i, StateFor(i)); }

        Msg StateFor(int i) => new Msg
        {
            t = S.RoundOver ? "round" : "state", seat = i, cells = (int[])S.Table.Clone(), rack = S.P[i].Rack.ToArray(), pool = S.Pool.Count,
            current = S.Current, round = S.Round, rounds = S.Rounds, winner = S.Winner, blocked = S.Blocked, over = S.MatchOver, text = S.LastEvent,
            players = S.P.Select((p, k) => new PInfo { name = p.Name, tiles = p.Rack.Count, score = p.Score, delta = p.LastDelta, hand = S.HandValue(k), melded = p.Melded, ai = p.Ai, online = p.Online }).ToArray()
        };

        void Send(int seat, Msg m)
        {
            var s = Seats[seat];
            if (s.K == Kind.Local) LocalSink?.Invoke(m);
            else if (s.K == Kind.Remote) RemoteSend?.Invoke(s.Conn, JsonUtility.ToJson(m));
        }

        bool AiTurn => Seats[S.Current].K == Kind.Ai || (Autopilot && Seats[S.Current].K == Kind.Local);

        public void Tick(float dt)
        {
            if (!Started || S == null || S.RoundOver || !AiTurn) { aiTimer = 0; return; }
            if (aiTimer == 0) aiDelay = 1.6f + (float)rng.NextDouble() * 1.8f + S.Table.Count(t => t >= 0) * .012f;
            aiTimer += dt;
            if (aiTimer < aiDelay) return;
            aiTimer = 0;
            int cur = S.Current;
            int[] plan = null;
            try { plan = AiPlayer.Plan(S, cur); } catch (Exception e) { Log.E(M, "ai " + e); }
            if (plan == null || S.Commit(cur, plan) != null) S.Draw(cur);
            SendState();
        }
    }
}
