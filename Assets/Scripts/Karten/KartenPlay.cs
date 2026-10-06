using System.Collections.Generic;
using System.Linq;
using RommeCup.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RommeCup.Karten
{
    public class KartenPlay
    {
        const string M = "KartenPlay";
        readonly KartenModule mod;
        public CMsg St;
        public int Seat;
        public bool Active;
        readonly List<int> order = new List<int>();
        readonly HashSet<int> sel = new HashSet<int>(), hint = new HashSet<int>();
        readonly List<List<int>> staged = new List<List<int>>();
        Vector2 downPos;
        bool tracking, deal;

        public KartenPlay(KartenModule m) { mod = m; }

        public static bool Help { get => PlayerPrefs.GetInt("khelp", 1) == 1; set => PlayerPrefs.SetInt("khelp", value ? 1 : 0); }

        public bool MyTurn => St != null && St.t == "state" && St.current == Seat;
        public bool MyDraw => MyTurn && St.phase == 0;
        public bool MyPlay => MyTurn && St.phase == 1;
        public bool Melded => St != null && St.players[Seat].melded;
        public int SelCount => sel.Count;
        public bool HasStaged => staged.Count > 0;
        public int StagedPoints => staged.Sum(g => Cards.Arrange(g)?.Points ?? 0);
        public int SelPoints => Cards.Arrange(sel.ToList())?.Points ?? 0;
        public bool SelValid => sel.Count >= 3 && Cards.Arrange(sel.ToList()) != null;

        List<List<int>> Table => CardHost.Unflatten(St.table);

        public void Begin(int seat) { Seat = seat; St = null; order.Clear(); sel.Clear(); hint.Clear(); staged.Clear(); Active = true; }

        public void End() { Active = false; St = null; mod.Table.Clear(); }

        public void OnState(CMsg m)
        {
            var prev = St;
            St = m; Seat = m.seat;
            bool newRound = prev == null || prev.round != m.round;
            order.RemoveAll(c => !m.hand.Contains(c));
            foreach (var c in m.hand) if (!order.Contains(c)) order.Add(c);
            if (newRound) { sel.Clear(); staged.Clear(); SortOrder(true); }
            sel.IntersectWith(m.hand);
            if (!MyPlay) staged.Clear(); else staged.RemoveAll(g => g.Any(c => !m.hand.Contains(c)));
            hint.Clear();
            deal = newRound;
            if (MyTurn && (prev == null || prev.current != Seat || prev.t != "state")) { Sfx.Play("turn", .8f); mod.Screens.Toast(newRound ? "Du beginnst - ziehe eine Karte." : "Du bist am Zug - ziehe eine Karte."); }
            Refresh();
            deal = false;
            if (m.t == "round") mod.OnRoundEnd(m); else mod.Screens.HideRound();
        }

        public void Refresh()
        {
            if (St == null) return;
            var hide = new HashSet<int>(staged.SelectMany(g => g));
            var d = new TableDto
            {
                Hand = order.Where(c => !hide.Contains(c)).ToArray(), Disc = St.disc, Pool = St.pool, DCount = St.dcount, Deal = deal, Me = Seat, Current = St.current, Actor = St.actor, N = St.players.Length, Players = St.players,
                Sel = sel, Hint = hint, PulseStock = MyDraw, PulseDisc = MyDraw && St.dcount > 0, SelValid = Help && SelValid
            };
            foreach (var g in Table) d.Melds.Add(g.ToArray());
            foreach (var g in staged) d.Staged.Add(Cards.Arrange(g)?.Cards.ToArray() ?? g.ToArray());
            mod.Table.Layout(d);
            mod.Screens.UpdateHud(this);
        }

        void SortOrder(bool byColor)
        {
            var s = order.OrderBy(c => Cards.IsJoker(c) ? 1 : 0).ThenBy(c => byColor ? Cards.Suit(c) : Cards.Rank(c)).ThenBy(c => byColor ? Cards.Rank(c) : Cards.Suit(c)).ThenBy(c => c).ToList();
            order.Clear(); order.AddRange(s);
        }

        public void Sort(bool byColor) { SortOrder(byColor); Sfx.Play("cslide", .6f); Refresh(); }

        public void Update()
        {
            if (!Active || St == null || mod.Screens.ModalOpen) { tracking = false; return; }
            bool down, up; Vector2 pos; int fid = -1;
            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0); pos = t.position; fid = t.fingerId;
                down = t.phase == TouchPhase.Began; up = t.phase == TouchPhase.Ended;
            }
            else { pos = Input.mousePosition; down = Input.GetMouseButtonDown(0); up = Input.GetMouseButtonUp(0); }
            if (down)
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(fid)) { tracking = false; return; }
                tracking = true; downPos = pos;
            }
            if (up && tracking)
            {
                tracking = false;
                if ((pos - downPos).sqrMagnitude < 2500) Tap(pos);
            }
        }

        void Tap(Vector2 pos)
        {
            var v = mod.Table.Pick(pos);
            if (v == null) return;
            switch (v.Tag)
            {
                case CTag.Hand:
                    if (!sel.Remove(v.Card)) sel.Add(v.Card);
                    hint.Clear(); Sfx.Play("cslide", .3f, .02f); Refresh();
                    break;
                case CTag.Stock: DrawStock(); break;
                case CTag.Discard: TakeDiscard(); break;
                case CTag.Staged:
                    staged.RemoveAt(v.Idx); Sfx.Play("cslide", .4f); Refresh();
                    break;
                case CTag.Meld: MeldTap(v); break;
            }
        }

        void Fail(string text) { mod.Screens.Toast(text); Sfx.Play("error", .5f); }

        void MeldTap(CView v)
        {
            if (!MyPlay) { Fail(MyTurn ? "Ziehe zuerst eine Karte." : "Du bist nicht am Zug."); return; }
            if (sel.Count == 0) { Fail("Wähle zuerst Karten aus deiner Hand."); return; }
            if (!Melded) { Fail("Erst die Erstauslage (" + Cards.InitialMeld + " Punkte) machen."); return; }
            var meld = Table[v.Idx];
            if (sel.Count == 1 && Cards.IsJoker(v.Card) && Cards.Swapped(meld, v.Card, sel.First()) != null)
            {
                mod.Send(new CMsg { t = "swap", meld = v.Idx, joker = v.Card, card = sel.First() });
                sel.Clear(); return;
            }
            if (Cards.Arrange(meld, sel) == null) { Fail("Das passt dort nicht an."); return; }
            mod.Send(new CMsg { t = "layoff", meld = v.Idx, cards = sel.ToArray() });
            sel.Clear();
        }

        public void DrawStock()
        {
            if (!MyDraw) { Fail(MyTurn ? "Du hast schon gezogen - lege eine Karte ab." : "Du bist nicht am Zug."); return; }
            mod.Send(new CMsg { t = "stock" });
        }

        public void TakeDiscard()
        {
            if (!MyDraw) { Fail(MyTurn ? "Du hast schon gezogen - lege eine Karte ab." : "Du bist nicht am Zug."); return; }
            mod.Send(new CMsg { t = "disc" });
        }

        public void Lay()
        {
            if (!MyPlay) return;
            if (!SelValid) { Fail("Keine gültige Kombination: Reihe (gleiche Farbe, fortlaufend) oder Gruppe (gleicher Wert, verschiedene Farben)."); return; }
            var g = sel.ToList(); sel.Clear();
            if (Melded) { mod.Send(new CMsg { t = "meld", cards = CardHost.Flatten(new[] { g }) }); return; }
            staged.Add(g); Refresh();
        }

        public void ConfirmInitial()
        {
            if (!MyPlay || staged.Count == 0) return;
            if (StagedPoints < Cards.InitialMeld) { Fail("Es fehlen noch " + (Cards.InitialMeld - StagedPoints) + " Punkte."); return; }
            var f = CardHost.Flatten(staged); staged.Clear();
            mod.Send(new CMsg { t = "meld", cards = f });
        }

        public void Discard()
        {
            if (!MyPlay) return;
            if (sel.Count != 1) { Fail("Wähle genau eine Karte zum Ablegen."); return; }
            int c = sel.First(); sel.Clear(); staged.Clear();
            mod.Send(new CMsg { t = "discard", card = c });
        }

        public void ResetTurn()
        {
            if (!MyPlay) return;
            sel.Clear(); staged.Clear();
            mod.Send(new CMsg { t = "undo" });
            Refresh();
        }

        public void Hint()
        {
            hint.Clear();
            if (!MyTurn) { Fail("Tipps gibt es, wenn du am Zug bist."); return; }
            var hand = St.hand.ToList();
            var table = Table;
            if (MyDraw)
            {
                if (St.dcount > 0)
                {
                    int top = St.disc[St.disc.Length - 1];
                    bool use = (Melded && table.Any(m => Cards.Arrange(m, new[] { top }) != null)) || CardAi.Best(hand.Append(top)).Points > CardAi.Best(hand).Points;
                    if (use) { hint.Add(top); mod.Screens.Toast("Die Ablagekarte passt zu deinen Karten."); }
                    else mod.Screens.Toast("Ziehe vom Stapel.");
                }
                Refresh(); return;
            }
            var cv = CardAi.Best(hand);
            if (!Melded)
            {
                if (cv.Points >= Cards.InitialMeld) { foreach (var c in cv.Groups.SelectMany(g => g)) hint.Add(c); mod.Screens.Toast("Damit schaffst du die Erstauslage (" + cv.Points + " Punkte)."); }
                else { hint.Add(CardAi.WorstCard(hand, St.took)); mod.Screens.Toast("Noch keine " + Cards.InitialMeld + " Punkte möglich - lege diese Karte ab."); }
            }
            else
            {
                var cand = hand.Where(c => !Cards.IsJoker(c) && table.Any(m => Cards.Arrange(m, new[] { c }) != null)).ToList();
                int lay = cand.Count > 0 ? cand[0] : -1;
                if (lay >= 0) { hint.Add(lay); mod.Screens.Toast("Diese Karte kannst du anlegen."); }
                else if (cv.Groups.Count > 0) { foreach (var c in cv.Groups[0]) hint.Add(c); mod.Screens.Toast("Diese Karten bilden eine Kombination."); }
                else { hint.Add(CardAi.WorstCard(hand, St.took)); mod.Screens.Toast("Kein Zug mehr - lege diese Karte ab."); }
            }
            Refresh();
        }
    }
}
