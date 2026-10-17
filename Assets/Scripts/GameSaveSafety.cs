using System;
using System.Collections.Generic;
using UnityEngine;

public partial class Game
{
    void ValidateJourneyContent()
    {
        if (MakeRegions().Length > 16 || Story.Rescues.Length > 32 || Story.Echoes.Length > 32)
            throw new InvalidOperationException("Journey v1 bit-set capacity exceeded; migrate before expanding content.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var mission in Missions)
            if (string.IsNullOrEmpty(mission.id) || !ids.Add(mission.id))
                throw new InvalidOperationException("Commission IDs must be unique and permanent.");
        ids.Clear();
        foreach (var region in RegionDefinitions)
            if (string.IsNullOrEmpty(region.id) || !ids.Add(region.id)) throw new InvalidOperationException("Region IDs must be unique and permanent.");
        ids.Clear();
        var rescueFloors = new HashSet<int>();
        foreach (var rescue in Story.Rescues)
        {
            if (string.IsNullOrEmpty(rescue.id) || !ids.Add(rescue.id)) throw new InvalidOperationException("Rescue IDs must be unique and permanent.");
            if (rescue.floor < 1 || rescue.floor > TowerFloors || !rescueFloors.Add(rescue.floor) || string.IsNullOrEmpty(rescue.farewell)
                || rescue.sightings == null || rescue.sightings.Length < 2) throw new InvalidOperationException("Rescue placement and both branches must be complete: " + rescue.id);
        }
        foreach (string id in Save.LegacyRescues)
            if (!ids.Contains(id)) throw new InvalidOperationException("Removing a core rescue requires an explicit pending-state migration: " + id);
        ids.Clear();
        foreach (var hero in Heroes.All)
            if (string.IsNullOrEmpty(hero.id) || !ids.Add(hero.id)) throw new InvalidOperationException("Hero IDs must be unique and permanent.");
        if (!Array.Exists(Heroes.All, hero => hero.id == "pip" && hero.cost == 0)) throw new InvalidOperationException("The free default hero must keep its stable identity.");
        foreach (var mission in Missions)
        {
            if (mission.regionId.Length > 0 && RegionIndex(mission.regionId) < 0) throw new InvalidOperationException("Unknown commission region: " + mission.id);
            if ((mission.job == Job.Chief || mission.job == Job.Recover) && string.IsNullOrEmpty(mission.targetId)) throw new InvalidOperationException("Named objective identity missing: " + mission.id);
            if (mission.job == Job.Explore && mission.count > RegionDefinitionCount) throw new InvalidOperationException("Exploration goal exceeds region content: " + mission.id);
        }
    }

    // Recover the entitlement before the player can enter the next expedition.
    bool ResumeRewards()
    {
        if (Save.PendingGuardian != null)
        {
            OpenHubPanel(0);
            OpenCards(() => SetState(GameState.Hub));
            return true;
        }
        for (int who = 0; who < Story.Rescues.Length; who++)
        {
            if (!Save.RescuePending(who)) continue;
            OpenHubPanel(0);
            ResumeRescue(who, () => SetState(GameState.Hub));
            return true;
        }
        if (PilotProgress.OfferPending) { OpenHubPanel(0); OpenCourierOffer(); return true; }
        return false;
    }

    // An interrupted rescue is safe at home. Its unresolved choice is asked there;
    // an already chosen branch can replay, with no second stat-point grant.
    void ResumeRescue(int who, Action then)
    {
        string id = Story.Rescues[who].id, token = Save.RescueToken(Story.Rescues[who].id);
        void Finish()
        {
            if (!Save.FinishRescue(id, token)) return;
            then();
        }
        void Branch(int choice) => Say(choice == 0 ? Story.GoHome(who) : Story.Saw(who), Finish);
        int saved = Save.RescueChoice(who);
        if (saved != 0) { Branch(saved - 1); return; }
        Say(Story.Rescues[who].rescue, () =>
            Ask("What do you ask of " + Story.Rescues[who].name + "?",
                "GO HOME", "Send them safely home to Brushwick.",
                "WHAT DID YOU SEE?", "Hear what is waiting on the floor above.",
                choice => { if (Save.ResolveRescue(id, choice, token)) Branch(choice); }));
    }

    int UnfinishedRescue()
    {
        for (int who = 0; who < Story.Rescues.Length; who++)
            if (Story.Rescues[who].floor <= Save.BestFloor && (Save.RescuedMask & (1 << who)) == 0) return who;
        return -1;
    }

    // Optional rescues cannot be stranded behind the permanent next-floor entry.
    // Revisit a liberated floor with its chests still governed by their receipts.
    void ReturnForRescue(int who)
    {
        if (who < 0 || who >= Story.Rescues.Length || Story.Rescues[who].floor > Save.BestFloor) return;
        Transition(() =>
        {
            Save.BeginExpedition(false);
            DefeatPending = false;
            int floor = Story.Rescues[who].floor;
            level = FloorCatalog.At(floor).theme;
            wave = FloorCatalog.At(floor).layout;
            Gloom = Save.GloomTier;
            InWilds = false;
            awakening = false;
            waveActive = clearPending = false;
            pending = 0;
            timers.Clear();
            numbers.Clear();
            Juice.Reset();
            ResetStyle();
            BuildFloor(level, wave);
            sat = satTarget = 1f;
            Player.NewRun();
            Player.weak = 1f;
            Player.gameObject.SetActive(true);
            Player.Revive(FloorStart, true);
            Player.transform.rotation = Quaternion.identity;
            Cam.Face(0f);
            Cam.Snap();
            hubPanel = 0;
            Pickup.AttractAll = false;
            music.SetTrack(level + 1);
            UnsealStairs();
            // This visit returns home rather than skipping progression in the active climb.
            if (stairSpot != null)
            {
                stairSpot.name = "The Way Home";
                stairSpot.prompt = "Return to Brushwick";
                stairSpot.act = LeaveTower;
            }
            SetState(GameState.Playing);
            Banner("UNFINISHED RESCUE", 3f, Story.Rescues[who].name + " is still waiting on Floor " + floor);
        });
    }
}
