using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace XOCardBattle
{
    public static class XOCardBattleSetup
    {
        const string ScenePath="Assets/XOCardBattle/XOCardBattle.unity";
        [MenuItem("Tools/XO Card Battle/Open or Create Scene")]
        public static void OpenScene()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)!=null)
            { EditorSceneManager.OpenScene(ScenePath); return; }
            Scene scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("Main Camera",typeof(Camera));
            camera.tag="MainCamera"; camera.transform.position=new Vector3(0,0,-10);
            camera.GetComponent<Camera>().clearFlags=CameraClearFlags.SolidColor;
            camera.GetComponent<Camera>().backgroundColor=new Color(.12f,.14f,.18f);
            new GameObject("XO Card Battle",typeof(XOCardBattleView));
            EditorSceneManager.SaveScene(scene,ScenePath);
            AssetDatabase.Refresh();
            Debug.Log("XO Card Battle scene ready. Press Play.");
        }

        static void Require(bool ok,string message)
        { if(!ok) throw new InvalidOperationException(message); }
        static Match NewMatch() { return new Match(123,3,0); }
        static void Give(Match m,int p,Card c)
        {
            if(m.Hands[p].Contains(c))return;
            Require(m.Deck.Remove(c),"Test card unavailable: "+c);
            m.Hands[p].Add(c);
        }
        static void Play(Match m,Card c,int a=-1,int b=-1)
        {
            Give(m,m.Player,c);
            Require(m.StartCard(c,a,b),"Failed to start "+c);
            if(m.Pending)m.PassReaction();
            Require(m.Invariants(),"Invariant failed: "+c);
        }
        static void Next(Match m)
        {
            if(!m.Placed && m.HasPlacement)
                Require(m.Place(Enumerable.Range(0,81).First(m.CanPlace)),"Could not place");
            Require(m.EndTurn(),"Could not end turn");
        }
        [MenuItem("Tools/XO Card Battle/Run Rules Smoke Tests")]
        public static void Tests()
        {
            Match m;
            // Horizontal, vertical, both diagonal directions; >=5.
            foreach(int step in new[]{1,9,10,8})
            {
                m=NewMatch();int start=step==8?8:0;
                for(int k=0;k<6;k++)m.Board[start+k*step].Piece=1;
                Require(m.CheckWin()==Outcome.X,"Direction "+step);
            }
            m=NewMatch();
            for(int i=0;i<5;i++){m.Board[i].Piece=1;m.Board[9+i].Piece=2;}
            Require(m.CheckWin()==Outcome.Draw,"Simultaneous wins");

            m=NewMatch();for(int i=0;i<5;i++)m.Board[i].Piece=1;
            m.Board[2].FreezeUntil=3;Require(m.CheckWin()==Outcome.Playing,"Frozen mark must not count");

            m=NewMatch();m.Board[10].Piece=1;m.Board[11].Piece=2;
            Play(m,Card.Swap,10,11);
            Require(m.Board[10].Piece==2&&m.Board[11].Piece==1,"Swap");

            m=NewMatch();m.Board[0].Piece=1;m.Board[0].FreezeUntil=4;m.Board[1].Destroyed=true;
            Play(m,Card.RotateRight,0);
            Require(m.Board[1].Piece==1&&m.Board[1].FreezeUntil==4&&m.Board[10].Destroyed,"Rotate states");

            m=NewMatch();Play(m,Card.Thanos,10);Require(m.IsDestroyed(10),"Destroy");Next(m);
            Play(m,Card.Revive,10);Require(m.CanPlace(10),"Revive");

            m=NewMatch();Play(m,Card.Freeze,10);Require(m.FreezeLeft(10)==3&&!m.CanPlace(10),"Freeze");Next(m);
            Play(m,Card.Freeze,10);Require(m.FreezeLeft(10)==6,"Freeze stacking");

            m=NewMatch();m.Board[20].Destroyed=true;Play(m,Card.Lockdown);
            Require(m.CanPlace(20)&&m.LockLeft==5,"Lockdown start");
            Require(m.Place(20),"Place in restored cell");Next(m);
            for(int i=0;i<5;i++)Next(m);
            Require(m.IsDestroyed(20)&&m.Board[20].Piece==0,"Lockdown restoration");

            m=NewMatch();Give(m,0,Card.Silence);Give(m,1,Card.Nope);Give(m,0,Card.Nope);
            Require(m.StartCard(Card.Silence)&&m.Pending,"Reaction opening");
            Require(m.React(Card.Nope)&&m.Pending,"First nope");
            Require(m.React(Card.Nope),"Counter nope");
            if(m.Pending)m.PassReaction();
            Require(m.Silence[1]==2&&m.Invariants(),"Double nope");

            m=NewMatch();Give(m,0,Card.Silence);Give(m,1,Card.Reflect);
            Require(m.StartCard(Card.Silence)&&m.Pending,"Reflect opening");
            Require(m.React(Card.Reflect),"Reflect");
            if(m.Pending)m.PassReaction();
            Require(m.Silence[0]==2&&m.Invariants(),"Reflect result");

            m=NewMatch();Play(m,Card.Shield);Next(m);Play(m,Card.Silence);
            Require(m.Silence[0]==0,"Shield block");

            m=NewMatch();m.Board[10].Piece=1;Play(m,Card.Immortal,10);Next(m);
            Give(m,1,Card.Erase);Require(!m.ValidTarget(Card.Erase,10),"Immortal blocks erase");
            Give(m,1,Card.RotateRight);Require(!m.ValidTarget(Card.RotateRight,0),"Immortal blocks region");

            m=NewMatch();Play(m,Card.Silence);Next(m);
            int turn=m.Turn;Play(m,Card.Cleanse);
            Require(m.Silence[1]==0&&m.Turn==turn+1,"Cleanse consumes turn");

            m=NewMatch();Require(m.Place(10),"UTurn setup");Play(m,Card.UTurn,11);
            Require(m.Board[10].Piece==0&&m.Board[11].Piece==1&&!m.Place(12),"UTurn cannot add mark");

            m=NewMatch();Play(m,Card.FiftyFifty);Require(m.Choosing&&m.Choice.Count==2,"50/50 draws");
            Require(m.Choose(1)&&m.Invariants(),"50/50 conservation");

            // Multiple active cards on the same turn, including repeat Freeze.
            m=NewMatch();Play(m,Card.Freeze,20);
            Play(m,Card.Freeze,20);
            Require(m.FreezeLeft(20)==6 && m.CardsPlayedThisTurn==2 && m.Turn==0,"Unlimited cards / same-turn stacking");
            Require(m.Place(10)&&!m.Place(11),"Still at most one mark per turn");
            Play(m,Card.Erase,10);
            Require(!m.Place(11)&&m.CardsPlayedThisTurn==3,"Erasing own mark does not grant another placement");
            Require(m.EndTurn()&&m.Player==1&&m.CardsPlayedThisTurn==0,"Manual end resets counters");

            m=NewMatch();Play(m,Card.Shield);
            Require(m.EndTurn()&&m.Turn==1,"Can end without placing a mark");

            m=NewMatch();Require(m.Place(10),"UTurn repeat setup");
            Play(m,Card.UTurn,11);Give(m,0,Card.UTurn);
            Require(!m.CanStart(Card.UTurn),"Only one UTurn per turn");

            m=NewMatch();Require(m.Place(10),"UTurn timing setup");
            Play(m,Card.Shield);Give(m,0,Card.UTurn);
            Require(!m.CanStart(Card.UTurn),"UTurn must immediately follow placement");

            m=NewMatch();Give(m,0,Card.Silence);Give(m,1,Card.Nope);
            Require(m.StartCard(Card.Silence)&&m.Pending&&!m.CanEnd,"Pending reaction blocks end turn");
            m.PassReaction();Require(m.CanEnd,"Can end after resolution");

            m=NewMatch();Play(m,Card.FiftyFifty);Require(!m.CanEnd,"Pending choice blocks end turn");
            Require(m.Choose(0)&&m.CanEnd,"Can end after choosing");

            m=NewMatch();Play(m,Card.Lockdown);Give(m,0,Card.Freeze);
            Require(!m.CanStart(Card.Freeze)&&m.CanEnd,"Lockdown blocks further cards");

            Debug.Log("XO Card Battle smoke tests passed.");
        }
    }
}
