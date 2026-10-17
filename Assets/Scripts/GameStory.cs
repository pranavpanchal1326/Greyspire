using System;
using UnityEngine;

// The rescue of a drained Painter, told as a small interactive scene:
//   Timeline "Rescue"  ->  their lines  ->  a choice  ->  one of two branches  ->  Timeline "Depart"
// The Painter runs on an Animator Controller (Stone, Break, Idle, Talk, Nod, Walk); the
// Timelines take over the Animator while they play and hand it back when they end.
public partial class Game
{
    // A question with two answers, drawn by DrawStory.
    string askTitle, askA, askB, askHintA, askHintB;
    Action<int> askPick;

    bool cutscene;                  // a Timeline is playing: bars on, no dialogue box
    float cutsceneAt;
    StoryScene storyScene;
    Animator storyActor;
    string storyName;

    public bool Asking => askPick != null;
    public bool InCutscene => cutscene;
    public StoryScene Scene => storyScene;

    void Ask(string title, string a, string hintA, string b, string hintB, Action<int> pick)
    {
        dialog = null;
        askTitle = title; askA = a; askB = b; askHintA = hintA; askHintB = hintB;
        askPick = pick;
        SetState(GameState.Story);
    }

    public void Answer(int which)
    {
        if (which < 0 || which > 1) return;
        Action<int> pick = askPick;
        askPick = null;
        ToonKit.Sfx("click");
        pick?.Invoke(which);
    }

    // Every frame of the Story state, beside TickDialog.
    void TickStory()
    {
        if (askPick != null)
        {
            int n = GameInput.NumberPressed;
            if (n == 0 || n == 1) Answer(n);
        }
        else if (cutscene && storyScene != null && storyScene.Playing && Time.unscaledTime > cutsceneAt + 0.4f
            && GameInput.SkipTalkPressed) storyScene.Skip();

        // The Painter's mouth moves only while a line of theirs is on screen.
        if (storyActor != null)
            storyActor.SetBool("Talking", dialog != null && dialogIndex < dialog.Length && dialog[dialogIndex].who == storyName
                && dialogChars < dialog[dialogIndex].text.Length);
    }

    // The floor the scene stood on has gone (a new run, a fall): drop the scene with it.
    public void AbandonStory()
    {
        if (storyScene != null) storyScene.Close();
        storyScene = null;
        storyActor = null;
        askPick = null;
        cutscene = false;
    }

    void Cutscene(string timeline, Action then)
    {
        dialog = null;
        cutscene = true;
        cutsceneAt = Time.unscaledTime;
        SetState(GameState.Story);
        storyScene.Play(timeline, () =>
        {
            cutscene = false;
            then();
        });
    }

    void RescueScene(GameObject holder, Drained stone, Animator body, int who, Vector3 pos)
    {
        Lost lost = Story.Rescues[who];
        string token = Save.RescueToken(lost.id);
        Transform hero = Player.transform;
        Vector3 to = pos - hero.position;
        to.y = 0f;
        if (to.sqrMagnitude > 0.01f) hero.rotation = Quaternion.LookRotation(to);

        storyScene = StoryScene.Stage(holder.transform, body, hero.position);
        storyActor = body;
        storyName = lost.name;
        shotSubject = holder.transform;
        string first = lost.name.Split(' ')[0];

        storyScene.onSignal = signal =>
        {
            if (signal == "Thaw")
            {
                // The colour runs back in; underneath the Timeline the controller leaves Stone for Idle.
                stone.Thaw();
                body.SetTrigger("Thaw");
                storyScene.TurnActor(hero.position, 1.4f);
                Flash(UI.Gold, 0.4f);
                Fx.Ring(pos, UI.Gold, 0.4f, 6f, 0.7f);
            }
            else if (signal == "Go")
            {
                body.SetBool("Leaving", true);
                storyScene.WalkActor(new Vector3(-1f, 0f, -0.25f), 1.3f);
            }
        };

        int chosen = -1;
        void Leave() => Cutscene("Depart", () =>
        {
            if (!Save.FinishRescue(lost.id, token)) return;
            SetState(GameState.Playing);
            SystemMessage("PAINTER RESCUED", lost.name + " is going home to Brushwick.    +2 stat points");
            if (chosen >= 0) RescueAftermath(who, chosen, pos);
            Fx.Burst(holder.transform.position + Vector3.up, UI.Gold, 14, 6f);
            Fx.Ring(holder.transform.position, Color.white, 0.3f, 3f, 0.4f);
            holder.SetActive(false);
            storyActor = null;
            storyScene.Close();
            storyScene = null;
        });

        Cutscene("Rescue", () => Say(lost.rescue, () =>
            Ask("What do you ask of " + first + "?",
                "GO HOME", "Send them down to Brushwick\nat once, while the way is clear.",
                "WHAT DID YOU SEE?", "Hear what is waiting\non the floor above.",
                pick =>
                {
                    if (!Save.ResolveRescue(lost.id, pick, token)) return;
                    chosen = pick;
                    if (pick == 0) Say(Story.GoHome(who), Leave);
                    else Cutscene("Tell", () => Say(Story.Saw(who), Leave));
                })));
    }
}
