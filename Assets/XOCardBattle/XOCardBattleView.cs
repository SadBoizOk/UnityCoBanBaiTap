using System;
using System.Linq;
using UnityEngine;

namespace XOCardBattle
{
    // Local two-player prototype. No sprites, prefabs, input packages or scene links required.
    public sealed class XOCardBattleView : MonoBehaviour
    {
        public int seed = 12345;
        public int copiesPerCard = 3;
        public int startingHand = 5;
        Match game;
        Card? selected;
        int first = -1, visiblePlayer = -1;
        bool reveal;
        Vector2 scroll;
        static readonly string[] Names = {
            "SWAP","FREEZE","ROTATE R","ROTATE L","MIRROR H","MIRROR V","HELICOPTER",
            "THANOS","REVIVE","ERASE","PEEK","SHUFFLE","STEAL","FORESIGHT","DISCARD",
            "NOPE","CLEANSE","SILENCE","LOCKDOWN","REFLECT","SHIELD","U-TURN","SPY",
            "RECYCLE","TRADE","50/50","IMMORTAL","MUTUAL"
        };
        static readonly string[] Info = {
            "Swap one of your marks with an opponent mark.",
            "Freeze a cell for 3 turns; stacks; marks do not count.",
            "Rotate a 2x2 region clockwise.","Rotate a 2x2 region counterclockwise.",
            "Mirror a 3x3 region left/right.","Mirror a 3x3 region top/bottom.",
            "Shuffle all states in a 3x3 region.",
            "Destroy a cell and its contents.","Restore a destroyed cell, empty.","Erase a mark, not its cell.",
            "See one random opponent card.","Shuffle the shared draw pile.","Steal one random opponent card.",
            "See up to 3 top cards.","Discard one random opponent card.",
            "Cancel a pending effect targeting your side; may counter Cleanse.",
            "Clear Silence on yourself or Freeze on a cell; ends your turn.",
            "Block active cards for 2 personal turns; stacks; Nope/Cleanse allowed.",
            "5 turns of pure XO; existing states are suspended, timers continue.",
            "Redirect a personal effect at its caster.",
            "Block personal effects for 2 turns, not board effects.",
            "Move the mark placed this turn to another legal empty cell.",
            "Convert an enemy mark to yours.","Recover a random used card, except Recycle.",
            "Exchange both hands.","Draw two, keep one.","Protect your mark for 3 turns.",
            "Erase one of your marks and one enemy mark."
        };
        void Awake(){ ResetMatch(); }
        void ResetMatch()
        {
            game = new Match(seed,Mathf.Max(1,copiesPerCard),Mathf.Max(0,startingHand));
            selected=null; first=-1; visiblePlayer=-1; reveal=false; scroll=Vector2.zero;
        }
        static string Name(Card c) => Names[(int)c];
        void ClearSelection(){selected=null;first=-1;}
        int Viewer => game.Pending ? game.Priority : game.Player;

        void OnGUI()
        {
            if(game==null)return;
            if(visiblePlayer!=Viewer){visiblePlayer=Viewer;reveal=false;ClearSelection();scroll=Vector2.zero;}
            float scale=Mathf.Min(Screen.width/1200f,Screen.height/750f);
            if(scale<=0)return;
            Matrix4x4 old=GUI.matrix;
            GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1200*scale)/2,(Screen.height-750*scale)/2,0),Quaternion.identity,new Vector3(scale,scale,1));
            GUI.Box(new Rect(0,0,1200,750),"");
            GUI.Label(new Rect(24,12,500,25),"XO CARD BATTLE — 9 x 9 / five in a row");
            GUI.Label(new Rect(24,40,650,25),"Turn "+(game.Turn+1)+" | "+Match.Side(game.Player)+" | "+game.Message);
            GUI.Label(new Rect(24,66,1100,25),"Deck "+game.Deck.Count+" | Discard "+game.Discard.Count+" | X hand "+game.Hands[0].Count+" | O hand "+game.Hands[1].Count+
                " | Silence X/O "+game.Silence[0]+"/"+game.Silence[1]+" | Shield X/O "+game.ShieldLeft(0)+"/"+game.ShieldLeft(1)+" | Lock "+game.LockLeft);
            if(GUI.Button(new Rect(1030,12,145,32),"Restart")){ResetMatch();GUI.matrix=old;return;}
            DrawBoard();
            DrawPanel();
            GUI.Label(new Rect(24,650,540,28),"Cell: X/O mark | F freeze | I immortal | # destroyed");
            GUI.Label(new Rect(24,678,540,28),"Region cards: select the TOP LEFT cell of the region.");
            GUI.Label(new Rect(24,706,540,28),"Hot-seat: hand stays hidden until the next player reveals it.");
            GUI.matrix=old;
        }
        void DrawBoard()
        {
            const float cell=57;
            for(int i=0;i<game.Board.Length;i++)
            {
                Cell c=game.Board[i];
                bool destroyed=game.IsDestroyed(i), frozen=game.IsFrozen(i), immortal=game.IsImmortal(i);
                string label=destroyed?"#":c.Piece==1?"X":c.Piece==2?"O":"";
                if(frozen)label+="\nF"+game.FreezeLeft(i);
                if(immortal)label+=" I"+game.ImmortalLeft(i);
                Color previous=GUI.backgroundColor;
                GUI.backgroundColor= i==first ? new Color(1f,.85f,.35f) : destroyed ? Color.gray : frozen ? new Color(.5f,.8f,1f) : c.Piece==1 ? new Color(.55f,.75f,1f) : c.Piece==2 ? new Color(1f,.6f,.6f) : Color.white;
                bool old=GUI.enabled;
                GUI.enabled=old && reveal && !game.Pending && !game.Choosing && game.Result==Outcome.Playing;
                if(GUI.Button(new Rect(24+i%Match.Size*cell,106+i/Match.Size*cell,cell-2,cell-2),label)) OnCell(i);
                GUI.enabled=old;GUI.backgroundColor=previous;
            }
        }
        void OnCell(int i)
        {
            if(!selected.HasValue){game.Place(i);return;}
            Card c=selected.Value;
            if(Match.TargetCount(c)==2)
            {
                if(first<0)
                {
                    if(game.Board[i].Piece==game.Player+1 && !game.IsDestroyed(i) && !game.IsImmortal(i))first=i;
                    return;
                }
                if(game.StartCard(c,first,i))ClearSelection();
            }
            else if(game.StartCard(c,i))ClearSelection();
        }
        void DrawPanel()
        {
            if(game.Result!=Outcome.Playing)
            {
                GUI.Label(new Rect(565,110,610,45),"GAME OVER: "+game.Result);
                return;
            }
            if(!reveal)
            {
                GUI.Label(new Rect(565,110,610,35),"Pass the device to Player "+Match.Side(Viewer)+".");
                if(GUI.Button(new Rect(565,155,610,55),"Reveal my hand"))reveal=true;
                return;
            }
            if(game.Pending)
            {
                Play root=game.Stack[0];
                GUI.Label(new Rect(565,106,610,25),"Reaction: "+Match.Side(root.Actor)+" played "+Name(root.Card)+".");
                GUI.Label(new Rect(565,132,610,25),"Targets: "+(root.A<0?"—":(root.A/9+1)+","+(root.A%9+1))+" | stack: "+string.Join(" > ",game.Stack.Select(p=>Name(p.Card))));
                bool old=GUI.enabled;
                GUI.enabled=game.CanReact(Card.Nope);
                if(GUI.Button(new Rect(565,168,295,48),"NOPE"))game.React(Card.Nope);
                GUI.enabled=game.CanReact(Card.Reflect);
                if(GUI.Button(new Rect(875,168,300,48),"REFLECT"))game.React(Card.Reflect);
                GUI.enabled=old;
                if(GUI.Button(new Rect(565,230,610,48),"Pass / resolve"))game.PassReaction();
                return;
            }
            if(game.Choosing)
            {
                GUI.Label(new Rect(565,106,610,30),"50/50: choose one card to keep.");
                Card[] choice=game.Choice.ToArray();
                for(int i=0;i<choice.Length;i++)if(GUI.Button(new Rect(565,150+i*70,610,60),Name(choice[i])+"\n"+Info[(int)choice[i]])){game.Choose(i);break;}
                return;
            }
            GUI.Label(new Rect(565,106,610,24),"PLAYER "+Match.Side(game.Player)+" — "+(game.Placed?"mark used":"mark available")+" / "+(game.UsedCard?"card used":"card available"));
            GUI.Label(new Rect(565,132,610,26),"Private observation (may be outdated): "+game.Notes[game.Player]);
            if(selected.HasValue)
            {
                GUI.Label(new Rect(565,162,610,24),"Selected: "+Name(selected.Value)+" | "+(first<0?"Choose target":"Choose enemy target"));
                GUI.Label(new Rect(565,185,610,24),Info[(int)selected.Value]);
                if(GUI.Button(new Rect(1035,210,140,30),"Cancel"))ClearSelection();
                if(selected==Card.Cleanse && GUI.Button(new Rect(565,210,300,30),"Cleanse myself"))
                {if(game.StartCard(Card.Cleanse))ClearSelection();}
            }
            else GUI.Label(new Rect(565,162,610,24),"Select a card or click an empty cell to place your mark.");
            Card[] hand=game.Hands[game.Player].ToArray();
            int rows=(hand.Length+1)/2;
            scroll=GUI.BeginScrollView(new Rect(565,250,610,355),scroll,new Rect(0,0,588,Mathf.Max(350,rows*70)));
            for(int i=0;i<hand.Length;i++)
            {
                Card c=hand[i];bool old=GUI.enabled;
                GUI.enabled=old&&game.CanStart(c);
                if(GUI.Button(new Rect(i%2*294,i/2*70,288,64),Name(c)+"\n"+Info[(int)c]))
                {
                    first=-1;
                    if(Match.TargetCount(c)==0){if(game.StartCard(c))ClearSelection();}
                    else selected=c;
                    GUI.enabled=old;break;
                }
                GUI.enabled=old;
            }
            GUI.EndScrollView();
            bool enabled=GUI.enabled;GUI.enabled=enabled&&game.CanEnd;
            if(GUI.Button(new Rect(565,620,610,54),game.HasPlacement?"End turn":"Pass / end turn"))
            {game.EndTurn();ClearSelection();}
            GUI.enabled=enabled;
        }
    }
}
