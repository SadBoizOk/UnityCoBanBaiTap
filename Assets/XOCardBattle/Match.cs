using System;
using System.Collections.Generic;
using System.Linq;

namespace XOCardBattle
{
    public enum Card
    {
        Swap, Freeze, RotateRight, RotateLeft, MirrorH, MirrorV, Helicopter,
        Thanos, Revive, Erase, Peek, Shuffle, Steal, Foresight, Discard,
        Nope, Cleanse, Silence, Lockdown, Reflect, Shield, UTurn, Spy,
        Recycle, Trade, FiftyFifty, Immortal, Mutual
    }

    public enum Outcome { Playing, X, O, Draw }

    public sealed class Cell
    {
        public int Piece; // 0 empty, 1 X, 2 O
        public bool Destroyed;
        public int FreezeUntil;
        public int ImmortalUntil;
    }

    public sealed class Play
    {
        public Card Card;
        public int Actor;
        public int A, B;
        public Play(Card card, int actor, int a = -1, int b = -1)
        { Card = card; Actor = actor; A = a; B = b; }
    }

    // No Unity dependencies: rules can be tested independently of the UI.
    public sealed class Match
    {
        public const int Size = 9, WinLength = 5;
        public readonly Cell[] Board = new Cell[Size * Size];
        public readonly List<Card>[] Hands = { new List<Card>(), new List<Card>() };
        public readonly List<Card> Deck = new List<Card>();
        public readonly List<Card> Discard = new List<Card>();
        public readonly List<Play> Stack = new List<Play>();
        public readonly List<Card> Choice = new List<Card>();
        public readonly string[] Notes = { "", "" };
        public readonly int[] Silence = new int[2], ShieldUntil = new int[2];
        public int Turn { get; private set; }
        public int Player { get; private set; }
        public int Priority { get; private set; }
        public int LockUntil { get; private set; }
        public bool Placed { get; private set; }
        public bool UsedCard { get; private set; }
        public int LastPlaced { get; private set; } = -1;
        public Outcome Result { get; private set; }
        public string Message { get; private set; } = "";
        public bool Locked => Turn < LockUntil;
        public bool Pending => Stack.Count > 0;
        public bool Choosing => Choice.Count > 0;
        readonly Random rng;
        readonly int initialCardCount;

        public Match(int seed = 0, int copies = 3, int startingHand = 5)
        {
            if (copies < 1 || startingHand < 0) throw new ArgumentOutOfRangeException();
            rng = new Random(seed);
            for (int i = 0; i < Board.Length; i++) Board[i] = new Cell();
            foreach (Card c in Enum.GetValues(typeof(Card)))
                for (int i = 0; i < copies; i++) Deck.Add(c);
            initialCardCount = Deck.Count;
            ShuffleList(Deck);
            for (int i = 0; i < startingHand; i++) { Draw(0); Draw(1); }
            Draw(0);
            Message = "X starts.";
        }

        public bool IsFrozen(int i) => !Locked && Board[i].FreezeUntil > Turn;
        public bool IsImmortal(int i) => !Locked && Board[i].ImmortalUntil > Turn;
        public bool IsDestroyed(int i) => !Locked && Board[i].Destroyed;
        public bool HasShield(int p) => !Locked && ShieldUntil[p] > Turn;
        public bool CanPlace(int i) => In(i) && Board[i].Piece == 0 && !IsDestroyed(i) && !IsFrozen(i);
        public int FreezeLeft(int i) => Math.Max(0, Board[i].FreezeUntil - Turn);
        public int ImmortalLeft(int i) => Math.Max(0, Board[i].ImmortalUntil - Turn);
        public int LockLeft => Math.Max(0, LockUntil - Turn);
        public int ShieldLeft(int p) => Math.Max(0, ShieldUntil[p] - Turn);
        public int TotalCards => Deck.Count + Discard.Count + Hands[0].Count + Hands[1].Count + Stack.Count + Choice.Count;
        public bool HasPlacement => Enumerable.Range(0, Board.Length).Any(CanPlace);
        public bool CanEnd => Result == Outcome.Playing && !Pending && !Choosing && (Placed || !HasPlacement);
        static bool In(int i) => i >= 0 && i < Size * Size;
        bool Own(int i, int p) => In(i) && !IsDestroyed(i) && Board[i].Piece == p + 1;
        bool Mutable(int i) => In(i) && !IsDestroyed(i) && !IsImmortal(i);
        bool Enemy(int i, int p) => Own(i, 1 - p);

        void ShuffleList<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            { int j = rng.Next(i + 1); T t = list[i]; list[i] = list[j]; list[j] = t; }
        }
        bool Take(out Card c)
        {
            if (Deck.Count == 0 && Discard.Count > 0)
            {
                Deck.AddRange(Discard); Discard.Clear(); ShuffleList(Deck);
            }
            if (Deck.Count == 0) { c = default(Card); return false; }
            int i = Deck.Count - 1; c = Deck[i]; Deck.RemoveAt(i); return true;
        }
        void Draw(int p) { Card c; if (Take(out c)) Hands[p].Add(c); }

        public bool Place(int i)
        {
            if (Result != Outcome.Playing || Pending || Choosing || Placed || !CanPlace(i)) return false;
            Board[i].Piece = Player + 1;
            Placed = true; LastPlaced = i;
            Message = Side(Player) + " placed.";
            CheckWin(); return true;
        }

        public bool EndTurn()
        {
            if (!CanEnd) return false;
            Advance(); return true;
        }

        void Advance()
        {
            if (Result != Outcome.Playing) return;
            // Silence counts the affected player's own completed turns; LOCKDOWN pauses
            // its effect, not its timer. Other timers count subsequent global turns.
            if (Silence[Player] > 0) Silence[Player]--;
            bool wasLocked = Locked;
            Turn++;
            if (wasLocked && !Locked)
                foreach (Cell c in Board) if (c.Destroyed) c.Piece = 0;
            CheckWin();
            if (Result != Outcome.Playing) return;
            Player = 1 - Player; Placed = false; UsedCard = false; LastPlaced = -1;
            Draw(Player);
            Message = Side(Player) + " turn.";
        }

        public static string Side(int p) => p == 0 ? "X" : "O";

        public static int TargetCount(Card c)
        {
            switch (c)
            {
                case Card.Swap: case Card.Mutual: return 2;
                case Card.Freeze: case Card.RotateRight: case Card.RotateLeft:
                case Card.MirrorH: case Card.MirrorV: case Card.Helicopter:
                case Card.Thanos: case Card.Revive: case Card.Erase: case Card.Cleanse:
                case Card.UTurn: case Card.Spy: case Card.Immortal: return 1;
                default: return 0;
            }
        }

        public static int RegionSize(Card c)
        {
            switch(c) {
                case Card.RotateRight: case Card.RotateLeft: return 2;
                case Card.MirrorH: case Card.MirrorV: case Card.Helicopter: return 3;
                default: return 0;
            }
        }
        IEnumerable<int> Region(int a, int n)
        {
            if (!In(a) || a / Size + n > Size || a % Size + n > Size) yield break;
            for (int r = 0; r < n; r++) for (int c = 0; c < n; c++) yield return a + r * Size + c;
        }
        bool ValidRegion(Card card, int a)
        {
            int n = RegionSize(card);
            int[] ids = Region(a,n).ToArray();
            return ids.Length == n*n && ids.All(i => !IsImmortal(i));
        }

        static bool Personal(Card c) => c == Card.Peek || c == Card.Steal || c == Card.Discard || c == Card.Silence;
        public bool CanStart(Card c)
        {
            if (Result != Outcome.Playing || Pending || Choosing || Locked || UsedCard || !Hands[Player].Contains(c)) return false;
            if (c == Card.Nope || c == Card.Reflect) return false;
            if (Silence[Player] > 0 && c != Card.Cleanse) return false;
            if (c == Card.UTurn && (!Placed || !Own(LastPlaced,Player) || !Mutable(LastPlaced))) return false;
            return true;
        }

        bool Valid(Play p, int actor)
        {
            int a=p.A, b=p.B, other=1-actor;
            switch(p.Card)
            {
                case Card.Swap: case Card.Mutual: return Own(a,actor) && Enemy(b,actor) && Mutable(a) && Mutable(b);
                case Card.Freeze: case Card.Thanos: return Mutable(a);
                case Card.RotateRight: case Card.RotateLeft: case Card.MirrorH: case Card.MirrorV: case Card.Helicopter:
                    return ValidRegion(p.Card,a);
                case Card.Revive: return In(a) && Board[a].Destroyed;
                case Card.Erase: return Mutable(a) && Board[a].Piece != 0;
                case Card.Peek: case Card.Steal: case Card.Discard: return Hands[other].Count > 0;
                case Card.Shuffle: case Card.Foresight: return Deck.Count > 0;
                case Card.Cleanse: return a == -1 ? Silence[actor] > 0 : In(a) && !IsDestroyed(a) && IsFrozen(a);
                case Card.Silence: return true;
                case Card.Lockdown: return true;
                case Card.Shield: return true;
                case Card.UTurn: return actor == Player && Placed && Own(LastPlaced,actor) && Mutable(LastPlaced) && CanPlace(a);
                case Card.Spy: return Enemy(a,actor) && Mutable(a);
                case Card.Recycle: return Discard.Any(c => c != Card.Recycle);
                case Card.Trade: return !HasShield(other);
                case Card.FiftyFifty: return Deck.Count + Discard.Count >= 2;
                case Card.Immortal: return Own(a,actor);
                default: return false;
            }
        }

        public bool ValidTarget(Card c, int a = -1, int b = -1)
            => CanStart(c) && Valid(new Play(c,Player,a,b),Player);

        public bool StartCard(Card c, int a = -1, int b = -1)
        {
            if (!ValidTarget(c,a,b)) { Message = "Invalid card or target."; return false; }
            Hands[Player].Remove(c);
            UsedCard = true;
            Stack.Add(new Play(c,Player,a,b)); Priority = 1-Player;
            Message = Side(Player) + " plays " + c + ".";
            // If no possible reaction, resolve without an unnecessary prompt.
            AutoResolve(); return true;
        }

        // Resolve active counters backwards. A live NOPE cancels exactly the previous
        // play; live REFLECT reverses a personal effect. No hidden information sampled yet.
        public void Preview(out bool canceled, out int actor)
        {
            actor = Stack[0].Actor;
            bool[] disabled = new bool[Stack.Count];
            for (int i = Stack.Count - 1; i > 0; i--)
            {
                if (disabled[i]) continue;
                if (Stack[i].Card == Card.Nope) disabled[i-1] = true;
                else if (Stack[i].Card == Card.Reflect) actor = 1-actor;
            }
            canceled = disabled[0];
        }

        bool Affects(Play p, int actor, int target)
        {
            if (Personal(p.Card) || p.Card == Card.Trade) return target != actor;
            if (p.Card == Card.Cleanse) return target != actor; // explicit exception
            switch(p.Card)
            {
                case Card.Swap: case Card.Mutual: return Own(p.A,target) || Own(p.B,target);
                case Card.Thanos: case Card.Erase: case Card.Freeze: case Card.Spy: return Own(p.A,target);
                case Card.RotateRight: case Card.RotateLeft: case Card.MirrorH: case Card.MirrorV: case Card.Helicopter:
                    return Region(p.A,RegionSize(p.Card)).Any(i => Own(i,target));
                default: return false;
            }
        }
        public bool CanReact(Card c)
        {
            if (!Pending || Locked || !Hands[Priority].Contains(c)) return false;
            Play root = Stack[0], top=Stack[Stack.Count-1];
            bool canceled; int actor; Preview(out canceled,out actor);
            if (c == Card.Nope)
                return Stack.Count > 1 ? top.Actor != Priority : Affects(root,actor,Priority);
            if (c == Card.Reflect)
                return Silence[Priority] == 0 && !canceled && Personal(root.Card) && actor != Priority;
            return false;
        }
        public bool React(Card c)
        {
            if (!CanReact(c)) return false;
            Hands[Priority].Remove(c); Stack.Add(new Play(c,Priority));
            Message = Side(Priority) + " plays " + c + ".";
            Priority = 1-Priority; AutoResolve(); return true;
        }
        void AutoResolve()
        {
            if (!CanReact(Card.Nope) && !CanReact(Card.Reflect)) Resolve();
        }
        public void PassReaction() { if (Pending) Resolve(); }

        void Resolve()
        {
            Play root = Stack[0];
            bool canceled; int actor; Preview(out canceled,out actor);
            bool shielded = (Personal(root.Card) || root.Card == Card.Trade) && HasShield(1-actor);
            if (canceled) Message = root.Card + " canceled.";
            else if (shielded) Message = root.Card + " blocked by shield.";
            else if (!Valid(root,actor)) Message = root.Card + " has no valid target.";
            else { Apply(root,actor); Message = root.Card + " resolved."; }
            foreach (Play p in Stack) Discard.Add(p.Card);
            Stack.Clear();
            CheckWin();
            if (root.Card == Card.Cleanse && Result == Outcome.Playing) Advance();
        }

        void Apply(Play p,int actor)
        {
            int a=p.A,b=p.B,other=1-actor;
            switch(p.Card)
            {
                case Card.Swap: int t=Board[a].Piece; Board[a].Piece=Board[b].Piece; Board[b].Piece=t; break;
                case Card.Freeze: Board[a].FreezeUntil=Math.Max(Turn+1,Board[a].FreezeUntil)+3; break;
                case Card.RotateRight: case Card.RotateLeft: case Card.MirrorH: case Card.MirrorV: case Card.Helicopter:
                    Transform(p.Card,a); break;
                case Card.Thanos: Board[a]=new Cell { Destroyed=true }; break;
                case Card.Revive: Board[a]=new Cell(); break;
                case Card.Erase: Board[a].Piece=0; break;
                case Card.Peek: Notes[actor]="Peek: "+Hands[other][rng.Next(Hands[other].Count)]; break;
                case Card.Shuffle: ShuffleList(Deck); Notes[0]=Notes[1]=""; break;
                case Card.Steal: { int i=rng.Next(Hands[other].Count); Hands[actor].Add(Hands[other][i]); Hands[other].RemoveAt(i); break; }
                case Card.Foresight: Notes[actor]="Top: "+string.Join(", ",Deck.AsEnumerable().Reverse().Take(3)); break;
                case Card.Discard: { int i=rng.Next(Hands[other].Count); Discard.Add(Hands[other][i]); Hands[other].RemoveAt(i); break; }
                case Card.Cleanse: if(a==-1) Silence[actor]=0; else Board[a].FreezeUntil=0; break;
                case Card.Silence: Silence[other]+=2; break;
                case Card.Lockdown: LockUntil=Turn+6; break;
                case Card.Shield: ShieldUntil[actor]=Turn+3; break;
                case Card.UTurn: Board[LastPlaced].Piece=0; Board[a].Piece=actor+1; LastPlaced=a; break;
                case Card.Spy: Board[a].Piece=actor+1; break;
                case Card.Recycle: { int[] ids=Enumerable.Range(0,Discard.Count).Where(i=>Discard[i]!=Card.Recycle).ToArray();
                    int i=ids[rng.Next(ids.Length)]; Hands[actor].Add(Discard[i]); Discard.RemoveAt(i); break; }
                case Card.Trade: { List<Card> temp=new List<Card>(Hands[actor]); Hands[actor].Clear();
                    Hands[actor].AddRange(Hands[other]); Hands[other].Clear(); Hands[other].AddRange(temp); break; }
                case Card.FiftyFifty: for(int i=0;i<2;i++){Card c;if(Take(out c)) Choice.Add(c);} break;
                case Card.Immortal: Board[a].ImmortalUntil=Turn+4; break;
                case Card.Mutual: Board[a].Piece=0; Board[b].Piece=0; break;
            }
        }

        void Transform(Card c,int a)
        {
            int n=RegionSize(c); int[] ids=Region(a,n).ToArray();
            List<Cell> old=ids.Select(i=>Board[i]).ToList();
            if(c==Card.Helicopter){ShuffleList(old);for(int i=0;i<ids.Length;i++) Board[ids[i]]=old[i];return;}
            for(int r=0;r<n;r++) for(int col=0;col<n;col++)
            {
                int rr=r, cc=col;
                if(c==Card.RotateRight){rr=col;cc=n-1-r;}
                else if(c==Card.RotateLeft){rr=n-1-col;cc=r;}
                else if(c==Card.MirrorH) cc=n-1-col;
                else if(c==Card.MirrorV) rr=n-1-r;
                Board[ids[rr*n+cc]]=old[r*n+col];
            }
        }

        public bool Choose(int i)
        {
            if(!Choosing || i<0 || i>=Choice.Count) return false;
            Hands[Player].Add(Choice[i]);
            for(int j=0;j<Choice.Count;j++) if(i!=j) Discard.Add(Choice[j]);
            Choice.Clear(); return true;
        }

        public Outcome CheckWin()
        {
            if(Result!=Outcome.Playing) return Result;
            bool x=false,o=false;
            int[,] dirs={{0,1},{1,0},{1,1},{1,-1}};
            for(int i=0;i<Board.Length;i++)
            {
                int piece=ActivePiece(i); if(piece==0) continue;
                int r=i/Size,c=i%Size;
                for(int d=0;d<4;d++)
                {
                    int dr=dirs[d,0],dc=dirs[d,1], count=1;
                    for(int j=1;j<WinLength;j++)
                    {
                        int rr=r+j*dr,cc=c+j*dc;
                        if(rr<0||rr>=Size||cc<0||cc>=Size||ActivePiece(rr*Size+cc)!=piece)break;
                        count++;
                    }
                    if(count>=WinLength){if(piece==1)x=true;else o=true;}
                }
            }
            if(x||o) Result=x&&o?Outcome.Draw:x?Outcome.X:Outcome.O;
            // No arbitrary full-board draw: cards can reopen positions. Prototype guard
            // for genuinely exhausted action space, and an explicit configurable policy later.
            if(Result==Outcome.Playing && !HasPlacement && !Locked &&
                !Board.Any(c=>c.FreezeUntil>Turn) && TotalCards==0) Result=Outcome.Draw;
            return Result;
        }
        int ActivePiece(int i) => IsDestroyed(i)||IsFrozen(i)?0:Board[i].Piece;

        public bool Invariants()
        {
            return TotalCards==initialCardCount && Board.Distinct().Count()==Board.Length &&
                Board.All(c=>c.Piece>=0&&c.Piece<=2&&(!c.Destroyed||Locked||c.Piece==0));
        }
    }
}
