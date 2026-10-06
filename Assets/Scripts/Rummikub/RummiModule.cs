using System.Collections.Generic;
using RommeCup.App;
using RommeCup.Core;
using RommeCup.Net;
using UnityEngine;

namespace RommeCup.Rummikub
{
    public class RummiModule : IGameModule, ISession
    {
        const string M = "RummiModule";
        public string Title => "Rummikub";
        public string Subtitle => "Das Legespiel mit 106 Steinen";
        public string Tagline => "2 bis 4 Spieler  ·  106 Steine  ·  Erstauslage 30 Punkte";
        public string Unit => "Steine";
        public string[] Rules => RummiScreens.RulePages;
        public bool Available => true;

        public readonly GameApp App;
        public readonly BoardView Board = new BoardView();
        public readonly RummiPlay Play;
        public readonly RummiScreens Screens;
        public GameHost Host;
        public bool IsHost { get; private set; }
        public bool InGame, Autopilot;
        BtLink bt;
        int hostConn = -1;
        readonly Queue<Msg> inbox = new Queue<Msg>();
        public List<KeyValuePair<string, string>> Devices { get; } = new List<KeyValuePair<string, string>>();
        public ILobby Lobby => Host;
        public bool Help { get => RummiPlay.Help; set => RummiPlay.Help = value; }
        public int MySeat => Play.Seat;
        public bool RoundShowing => Play.St != null && Play.St.t == "round";
        public void RefreshPlay() => Play.Refresh();

        public RummiModule(GameApp app)
        {
            App = app;
            Play = new RummiPlay(this);
            Screens = new RummiScreens(this);
        }

        public void ShowBackdrop() { Shutdown(); Play.End(); Board.Backdrop(); }

        public void Close() => ShowBackdrop();

        public void Open() { ShowBackdrop(); Screens.ShowModeSelect(); }

        public bool Back()
        {
            if (Screens.CloseModal()) return true;
            if (InGame) { Screens.ConfirmLeave(); return true; }
            if (Screens.OnModeSelect) return false;
            Leave();
            return true;
        }

        public void Update(float dt)
        {
            bt?.Poll();
            Host?.Tick(dt);
            while (inbox.Count > 0) Handle(inbox.Dequeue());
            Play.Update();
            Board.Update(dt);
        }

        GameHost NewHost(int rounds) => new GameHost { Rounds = rounds, Autopilot = Autopilot, LocalSink = m => inbox.Enqueue(m), RemoteSend = (c, s) => bt?.Send(c, s) };

        public void StartSolo(int ai, int rounds)
        {
            Shutdown();
            IsHost = true;
            Host = NewHost(rounds);
            Host.AddLocal(GameApp.PlayerName);
            for (int i = 0; i < ai; i++) Host.AddAi();
            Play.Begin(0);
            Host.StartMatch();
            Log.I(M, "solo ai=" + ai + " rounds=" + rounds);
        }

        public void HostBt()
        {
            Shutdown();
            IsHost = true;
            bt = NewBt();
            Screens.ShowBusy("Bluetooth wird vorbereitet ...");
            App.StartCoroutine(bt.Prepare((ok, err) =>
            {
                if (!ok) { Shutdown(); Screens.ShowError(err); return; }
                Host = NewHost(3);
                Host.AddLocal(GameApp.PlayerName);
                bt.StartHost(3);
                bt.MakeDiscoverable(300);
                Screens.ShowHostLobby();
            }));
        }

        public void JoinBt()
        {
            Shutdown();
            IsHost = false;
            bt = NewBt();
            Screens.ShowBusy("Bluetooth wird vorbereitet ...");
            App.StartCoroutine(bt.Prepare((ok, err) =>
            {
                if (!ok) { Shutdown(); Screens.ShowError(err); return; }
                Devices.Clear();
                Screens.ShowJoin("Suche nach Spielen ...");
                bt.StartDiscovery();
            }));
        }

        public void Rescan() { Devices.Clear(); bt?.StartDiscovery(); Screens.ShowJoin("Suche nach Spielen ..."); }

        public void ConnectTo(string addr, string name)
        {
            Screens.ShowBusy("Verbinde mit " + name + " ...");
            bt?.Connect(addr);
        }

        BtLink NewBt()
        {
            var b = new BtLink();
            b.Connected += (id, name) =>
            {
                Log.I(M, "connected " + id + " " + name);
                if (!IsHost) { hostConn = id; b.Send(id, JsonUtility.ToJson(new Msg { t = "hello", name = GameApp.PlayerName })); Screens.ShowBusy("Verbunden - warte auf den Gastgeber ..."); }
            };
            b.Message += OnBtMessage;
            b.Disconnected += id =>
            {
                Log.I(M, "disconnected " + id);
                if (IsHost) { Host?.DropConn(id); if (Host != null && !Host.Started) Screens.ShowHostLobby(); }
                else if (id == hostConn) { Leave(); Screens.ShowError("Die Verbindung zum Gastgeber wurde getrennt."); }
            };
            b.Found += (addr, name) =>
            {
                if (Devices.Exists(d => d.Key == addr)) return;
                Devices.Add(new KeyValuePair<string, string>(addr, name));
                if (!InGame && hostConn < 0) Screens.ShowJoin("Tippe auf das Gerät des Gastgebers:");
            };
            b.DiscoveryDone += () => { if (!InGame && hostConn < 0) Screens.ShowJoin(Devices.Count == 0 ? "Keine Geräte gefunden." : "Suche beendet - tippe auf das Gerät des Gastgebers:"); };
            b.ConnectFailed += (addr, e) => { Screens.ShowJoin("Verbindung fehlgeschlagen. Hat das Gerät 'Spiel eröffnen' gewählt?"); };
            b.Error += e => { Log.W(M, e); Screens.Toast(e); };
            return b;
        }

        void OnBtMessage(int id, string line)
        {
            Msg m;
            try { m = JsonUtility.FromJson<Msg>(line); } catch { Log.W(M, "bad json"); return; }
            if (m == null) return;
            if (!IsHost) { inbox.Enqueue(m); return; }
            if (Host == null) return;
            if (m.t == "hello")
            {
                if (Host.Started || Host.Seats.Count >= 4) { bt.Send(id, JsonUtility.ToJson(new Msg { t = "full", text = "Das Spiel ist voll oder läuft bereits." })); bt.Close(id); return; }
                int seat = Host.AddRemote(m.name, id);
                bt.Send(id, JsonUtility.ToJson(new Msg { t = "welcome", seat = seat }));
                Host.BroadcastLobby();
                Sfx.Play("turn", .6f);
                Screens.ShowHostLobby();
                return;
            }
            int s = Host.SeatOfConn(id);
            if (s >= 0) Host.Receive(s, m);
        }

        public void StartHostedMatch()
        {
            if (Host == null || Host.Seats.Count < 2) { Screens.Toast("Mindestens 2 Spieler nötig."); return; }
            bt?.StopAccepting();
            Play.Begin(0);
            Host.StartMatch();
        }

        public void Send(Msg m)
        {
            if (IsHost) Host?.Receive(Play.Seat, m);
            else if (hostConn >= 0) bt?.Send(hostConn, JsonUtility.ToJson(m));
        }

        void Handle(Msg m)
        {
            switch (m.t)
            {
                case "welcome": Play.Seat = m.seat; break;
                case "lobby": if (!IsHost) Screens.ShowClientLobby(m.players, m.rounds); break;
                case "full": Leave(); Screens.ShowError(m.text); break;
                case "state":
                case "round":
                    if (!InGame) EnterGame(m.seat);
                    Play.OnState(m);
                    break;
                case "preview": Play.OnPreview(m); break;
                case "reject": Screens.Toast(m.text); Sfx.Play("error"); break;
            }
        }

        void EnterGame(int seat)
        {
            InGame = true;
            if (!Play.Active) Play.Begin(seat);
            Play.Seat = seat;
            Screens.ShowHud();
            Log.I(M, "enter game seat=" + seat);
        }

        public void OnRoundEnd(Msg m)
        {
            bool iWon = m.winner == Play.Seat;
            if (m.over)
            {
                int best = 0;
                for (int i = 1; i < m.players.Length; i++) if (m.players[i].score > m.players[best].score) best = i;
                Sfx.Play("applause", best == Play.Seat ? .9f : .45f);
                Screens.ShowMatchEnd(m, best);
            }
            else
            {
                if (iWon) Sfx.Play("applause_small", .7f); else Sfx.Play("turn", .5f);
                Screens.ShowRoundEnd(m);
            }
        }

        public void NextRound() { if (IsHost) Host?.NextRound(); }

        public void Leave()
        {
            Log.I(M, "leave");
            InGame = false;
            Play.End();
            Shutdown();
            Board.Backdrop();
            Screens.ShowModeSelect();
        }

        void Shutdown()
        {
            bt?.Shutdown(); bt = null;
            Host = null; hostConn = -1; inbox.Clear(); InGame = false;
        }
    }
}
