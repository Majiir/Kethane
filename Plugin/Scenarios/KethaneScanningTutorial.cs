using Kethane.GeodesicGrid;
using Kethane.PartModules;
using System;
using System.Linq;
using UnityEngine;

namespace Kethane.Scenarios
{
    public class KethaneScanningTutorial : TutorialScenario
    {
        protected override void OnAssetSetup()
        {
            instructorPrefabName = "Instructor_Gene";
        }

        private const string lockName = "KethaneScanningTutorialLock";

        private float surfaceCoverage = 0;
        private GUIStyle boldStyle = null;

        protected override void OnTutorialSetup()
        {
            TutorialPage introduction, grid, detector, sounds, colors, timewarp, conclusion;

            instructor.CharacterName = "Majiir";

            #region Introduction

            introduction = new TutorialPage("introduction");
            introduction.windowTitle = "Kethane Scanning Tutorial";

            introduction.OnEnter = s =>
            {
                instructor.StopRepeatingEmote();
            };

            introduction.OnDrawContent = () =>
            {
                GUILayout.Label("Hi there! Today we're going to use satellites to scan for Kethane, a valuable resource found all around the Kerbal system. Kethane can be processed into rocket fuel or burned in special engines, but we'll have to find it first.\n\nWe'll be controlling a Mun satellite together from here at KSC. It's been equipped with a Kethane survey unit and solar panels for power. Let's get started!");

                if (GUILayout.Button("Next")) { Tutorial.GoToNextPage(); }
            };

            Tutorial.AddPage(introduction);

            #endregion

            #region Grid

            grid = new TutorialPage("grid");
            grid.windowTitle = "Kethane Scanning Tutorial";

            grid.OnEnter = s =>
            {
                MapView.EnterMapView();
                MapOverlay.ShowOverlay = true;
                MapOverlay.SelectedResource = "Kethane";
                instructor.PlayEmote(instructor.anim_idle_lookAround);
            };

            grid.OnDrawContent = () =>
            {
                GUILayout.Label("Here we are in the map view. The cellular grid around the Mun will display resource scan data once we've collected it.\n\nYou can hide the grid or switch to other resources (if you have other resource mods installed) using the green window. (It might be hidden under this one.) The grid will appear around whatever planet or moon you're focused on, so you can view scan data from anywhere in the system.\n\nTake a moment to get comfortable with the grid controls and we'll continue when you're ready.");

                if (GUILayout.Button("Next")) { Tutorial.GoToNextPage(); }
            };

            grid.OnLeave = s =>
            {
                MapView.ExitMapView();
            };

            Tutorial.AddPage(grid);

            #endregion

            #region Detector

            detector = new TutorialPage("detector");
            detector.windowTitle = "Kethane Scanning Tutorial";

            detector.OnEnter = s =>
            {
                instructor.PlayEmote(instructor.anim_idle_wonder);
                InputLockManager.RemoveControlLock(lockName + "_actions");
            };

            detector.OnDrawContent = () =>
            {
                GUILayout.Label("Now, let's get scanning! There's a Kethane detector mounted on the front of the satellite. Right-click it and click \"Activate Detector\" to begin scanning.");
            };

            detector.OnFixedUpdate = () =>
            {
                if (FlightGlobals.ActiveVessel.Parts.SelectMany(p => p.Modules.OfType<KethaneDetector>()).Any(d => d.IsDetecting))
                {
                    Tutorial.GoToNextPage();
                }
            };

            detector.OnLeave = s =>
            {
                InputLockManager.SetControlLock(ControlTypes.ACTIONS_SHIP, lockName + "_actions");
                MapView.ExitMapView();
            };

            Tutorial.AddPage(detector);

            #endregion

            #region Sounds

            sounds = new TutorialPage("sounds");
            sounds.windowTitle = "Kethane Scanning Tutorial";

            sounds.OnEnter = s =>
            {
                instructor.PlayEmote(instructor.anim_true_thumbsUp);
                InputLockManager.RemoveControlLock(lockName + "_map");
            };

            sounds.OnDrawContent = () =>
            {
                GUILayout.Label("Excellent! The detector has turned toward the surface of the Mun, scanning for underground Kethane deposits. You'll occasionally hear beeping or blipping noises as the detector passes over cells on the grid. A louder tone indicates the presence of Kethane directly underneath the satellite.\n\nWhen you're ready, open the map view and we'll take a look at the results.");
            };

            sounds.OnFixedUpdate = () =>
            {
                if (MapView.MapIsEnabled)
                {
                    Tutorial.GoToNextPage();
                }
            };

            sounds.OnLeave = s =>
            {
                InputLockManager.SetControlLock(ControlTypes.MAP, lockName + "_map");
            };

            Tutorial.AddPage(sounds);

            #endregion

            #region Colors

            colors = new TutorialPage("colors");
            colors.windowTitle = "Kethane Scanning Tutorial";

            colors.OnEnter = s =>
            {
                instructor.PlayEmote(instructor.anim_idle_lookAround);
            };

            colors.OnDrawContent = () =>
            {
                GUILayout.Label("Now that we're scanning, you can see that some cells on the grid have changed color. Green cells indicate the presence of Kethane, and light gray cells have been scanned but nothing was found underneath.\n\nFor detailed information, you can hover your mouse over a cell on the grid. When you hover over a resource deposit, the quantity available for mining will also be displayed.");

                if (GUILayout.Button("Next")) { Tutorial.GoToNextPage(); }
            };

            Tutorial.AddPage(colors);

            #endregion

            #region Timewarp

            timewarp = new TutorialPage("timewarp");
            timewarp.windowTitle = "Kethane Scanning Tutorial";

            timewarp.OnEnter = s =>
            {
                instructor.PlayEmote(instructor.anim_idle_sigh);
                InputLockManager.RemoveControlLock(lockName + "_timewarp");
            };

            timewarp.OnDrawContent = () =>
            {
                if (boldStyle == null)
                {
                    boldStyle = new GUIStyle(GUI.skin.label);
                    boldStyle.fontStyle = FontStyle.Bold;
                }

                GUILayout.Label("Scanning the Mun is going to take a while at this rate. Luckily, we can send our satellite into time warp to finish the job faster. Note that while warping, the detector will lose some data, but overall it will still work faster.\n\nUse time warp to get 2% of the Mun scanned. Watch your battery levels as you approach the dark side. Detectors use a lot of power!");
                GUILayout.Label(String.Format("Surface Coverage: {0:P2}", surfaceCoverage), boldStyle);
            };

            timewarp.OnUpdate = () =>
            {
                surfaceCoverage = (float)Cell.AtLevel(MapOverlay.GridLevel).Count(c => KethaneData.Current["Kethane"][FlightGlobals.currentMainBody].IsCellScanned(c)) / Cell.CountAtLevel(MapOverlay.GridLevel);
                if (surfaceCoverage >= 0.02f)
                {
                    Tutorial.GoToNextPage();
                }
            };

            Tutorial.AddPage(timewarp);

            #endregion

            #region Conclusion

            conclusion = new TutorialPage("conclusion");
            conclusion.windowTitle = "Kethane Scanning Tutorial";

            conclusion.OnEnter = s =>
            {
                instructor.PlayEmoteRepeating(instructor.anim_true_smileB, 2);
                InputLockManager.RemoveControlLock(lockName + "_map");
            };

            conclusion.OnDrawContent = () =>
            {
                GUILayout.Label("Nice work! That wraps up our scanning session. Now try building a satellite of your own, or we can move onto using drills to extract Kethane from the ground. Thanks for stopping by!");

                if (GUILayout.Button("Finish")) { GameObject.Destroy(this); }
            };

            Tutorial.AddPage(conclusion);

            #endregion

            InputLockManager.SetControlLock(ControlTypes.ACTIONS_SHIP, lockName + "_actions");
            InputLockManager.SetControlLock(ControlTypes.ALL_SHIP_CONTROLS & ~ControlTypes.ACTIONS_SHIP, lockName + "_controls");
            InputLockManager.SetControlLock(ControlTypes.MAP, lockName + "_map");
            InputLockManager.SetControlLock(ControlTypes.TIMEWARP, lockName + "_timewarp");

            Tutorial.StartTutorial(introduction);
        }

        public void OnDestroy()
        {
            InputLockManager.RemoveControlLock(lockName + "_actions");
            InputLockManager.RemoveControlLock(lockName + "_controls");
            InputLockManager.RemoveControlLock(lockName + "_map");
            InputLockManager.RemoveControlLock(lockName + "_timewarp");
        }
    }
}
