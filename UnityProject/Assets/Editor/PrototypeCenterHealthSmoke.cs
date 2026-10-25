using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Cursor = UnityEngine.Cursor;

namespace Ironfront.UnityPrototype.Editor
{
    public static class PrototypeCenterHealthSmoke
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly List<Behaviour> frozen = new List<Behaviour>();
        private static PrototypeRuntime captureGame;
        private static float oldHealth;
        private static bool requested;
        private static int preparedFrame, capturedFrame;
        private static double begun;
        private static string capturePath;
        private static EditorWindow captureView;
        private static bool oldMaximized;

        [MenuItem("IRONFRONT/Verify Center Health and Scoreboard Controls", true)]
        [MenuItem("IRONFRONT/Capture Center Health HUD", true)]
        private static bool CanRun() => EditorApplication.isPlaying;

        [MenuItem("IRONFRONT/Verify Center Health and Scoreboard Controls")]
        private static void Verify()
        {
            PrototypeRuntime game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
            bool opened = game != null && game.ScoreboardOpen;
            CursorLockMode oldLock = Cursor.lockState; bool oldCursor = Cursor.visible;
            try
            {
                Check(game != null && game.Player != null && game.MatchStarted && game.Scoreboard != null &&
                    game.Player.Alive && !game.Frontend.IsOpen && !game.PauseOpen && !game.Match.Winner.HasValue,
                    "actual active living battle exists; alive="+(game?.Player?.Alive ?? false)+" frontend="+(game?.Frontend?.IsOpen ?? false));
                foreach(Vector2 size in new[] {new Vector2(960,600),new Vector2(1280,720),new Vector2(1280,800),new Vector2(1024,768)})
                {
                    Rect panel=PrototypePlayerHealthHud.Panel(size.x,size.y);
                    Check(Mathf.Abs(panel.center.x-size.x*.5f)<.01f && Mathf.Abs(panel.width/size.x-.25f)<.001f &&
                        panel.yMin>size.y*.70f && panel.yMax<size.y-35f,"quarter-width health is bottom-centered and clear of controls");
                    Rect full=PrototypePlayerHealthHud.Bar(size.x,size.y),hurt=PrototypePlayerHealthHud.Bar(size.x,size.y,37f);
                    Check(full.height>=10f && Mathf.Abs(hurt.width/full.width-.37f)<.001f,"enlarged bar reflects injury proportionally");
                    Check(PrototypePlayerHealthHud.Bar(size.x,size.y,-1f).width==0f &&
                        PrototypePlayerHealthHud.Bar(size.x,size.y,float.NaN).width==0f,"invalid/negative health cannot invert the bar");
                    Check(!panel.Overlaps(new Rect(12f,size.y-109f,230f,61f)),"support-credit panel leaves health clear");
                    for(int i=0;i<6;i++)
                    {
                        Rect card=PrototypePlayerHealthHud.EquipmentCard(size.x,size.y,i);
                        Check(!panel.Overlaps(card) && card.xMin>0 && card.xMax<=size.x-10f && card.yMax<size.y-10f,
                            "all six engineer cards fit without covering health");
                        for(int j=0;j<i;j++) Check(!card.Overlaps(PrototypePlayerHealthHud.EquipmentCard(size.x,size.y,j)),"equipment cards are distinct");
                    }
                }
                game.SetScoreboard(false);
                float clock=Time.timeScale; var entry=game.Scores.Get(game.Player); int total=entry.Total,credits=entry.Credits;
                Check(game.SetScoreboard(true) && game.ScoreboardOpen && Cursor.lockState==CursorLockMode.None && Cursor.visible,
                    "actual opening permits reading and scrolling");
                Check(Time.timeScale==clock && !game.Player.CanUseHeldEquipment,"world keeps running while local equipment input is disabled");
                Check(entry.Total==total && entry.Credits==credits,"opening standings changes no points");
                Check(game.SetScoreboard(false) && !game.ScoreboardOpen && game.Player.CanUseHeldEquipment,
                    "actual closing restores gameplay permission; alive="+game.Player.Alive+" frontend="+game.Frontend.IsOpen+" cursor="+Cursor.lockState);
                if(!Application.isBatchMode && Application.isFocused)
                    Check(Cursor.lockState==CursorLockMode.Locked,"focused native GameView restores the actual locked cursor");
                game.SetScoreboard(true);
                Check(game.SetSupportMenu(true) && game.SupportMenuOpen && !game.ScoreboardOpen,"support and standings cannot compete for input");
                game.SetSupportMenu(false);
                Debug.Log("IRONFRONT center health and scoreboard controls PASS: actual open/close and cursor/input lifecycle, unchanged world clock/score, " +
                    "quarter-width centered injury bar, four display aspects and six equipment slots without overlaps");
            }
            catch(Exception error) {Debug.LogError("IRONFRONT center health and scoreboard controls FAIL: "+error);}
            finally {game?.SetSupportMenu(false);game?.SetScoreboard(opened);Cursor.lockState=oldLock;Cursor.visible=oldCursor;}
        }

        [MenuItem("IRONFRONT/Capture Center Health HUD")]
        private static void BeginCapture()
        {
            try
            {
                Check(!Application.isBatchMode,"real OnGUI capture requires the test-owned GUI Editor");
                captureGame=UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
                Check(captureGame!=null && captureGame.Player!=null && captureGame.Player.Alive,"living actual player exists");
                Type gameView=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                captureView=EditorWindow.GetWindow(gameView);captureView.Show();captureView.Focus();
                oldMaximized=captureView.maximized;captureView.maximized=true;
                oldHealth=captureGame.Player.Health;
                typeof(PrototypePlayer).GetField("<Health>k__BackingField",Private).SetValue(captureGame.Player,47f);
                captureGame.SetSupportMenu(false);captureGame.SetScoreboard(false);captureGame.Frontend.Close();
                Freeze(captureGame.Player);
                foreach(var bot in captureGame.Bots) Freeze(bot);
                foreach(var vehicle in captureGame.Vehicles) Freeze(vehicle as Behaviour);
                string folder=Path.GetFullPath("Builds/Verification054");Directory.CreateDirectory(folder);
                capturePath=Path.Combine(folder,"center-health-hud.png");if(File.Exists(capturePath)) File.Delete(capturePath);
                begun=EditorApplication.timeSinceStartup;preparedFrame=Time.frameCount;requested=false;
                EditorApplication.update-=CaptureFrame;EditorApplication.update+=CaptureFrame;
            }
            catch(Exception error) {End();Debug.LogError("IRONFRONT center health HUD capture FAIL: "+error);}
        }
        private static void CaptureFrame()
        {
            try
            {
                if(!EditorApplication.isPlaying) {End();return;}
                if(EditorApplication.timeSinceStartup-begun>25d) throw new InvalidOperationException("real GameView screenshot timed out");
                if(!requested)
                {
                    if(Time.frameCount<preparedFrame+3) return;
                    ScreenCapture.CaptureScreenshot(capturePath,1);capturedFrame=Time.frameCount;requested=true;return;
                }
                if(Time.frameCount<capturedFrame+2 || !File.Exists(capturePath) || new FileInfo(capturePath).Length<1000) return;
                byte[] png=File.ReadAllBytes(capturePath);
                int width=(png[16]<<24)|(png[17]<<16)|(png[18]<<8)|png[19];
                int height=(png[20]<<24)|(png[21]<<16)|(png[22]<<8)|png[23];
                Check(width>=480 && height>=300,"actual rendered HUD has usable native pixel dimensions");
                Debug.Log("IRONFRONT center health HUD capture PASS: actual GameView/OnGUI PNG at "+width+"x"+height+"; "+capturePath);
                End();
            }
            catch(Exception error) {End();Debug.LogError("IRONFRONT center health HUD capture FAIL: "+error);}
        }
        private static void Freeze(Behaviour actor) {if(actor!=null && actor.enabled) {frozen.Add(actor);actor.enabled=false;}}
        private static void End()
        {
            EditorApplication.update-=CaptureFrame;
            if(captureGame?.Player!=null) typeof(PrototypePlayer).GetField("<Health>k__BackingField",Private).SetValue(captureGame.Player,oldHealth);
            foreach(Behaviour actor in frozen) if(actor!=null) actor.enabled=true;
            if(captureView!=null) {captureView.maximized=oldMaximized;captureView=null;}
            frozen.Clear();captureGame=null;
        }
        private static void Check(bool value,string message) {if(!value) throw new InvalidOperationException(message);}
    }
}
