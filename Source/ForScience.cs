using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ForScience
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class ForScience : MonoBehaviour
    {
        //GUI
        private ApplicationLauncherButton FSAppButton = null;
        private Texture icon = null;

        //states
        Vessel stateVessel = null;
        CelestialBody stateBody = null;
        string stateBiome = null;
        ExperimentSituations stateSituation = 0;

        //thread control
        bool autoTransfer = true;
        List<ModuleScienceExperiment> completedExperiments = new List<ModuleScienceExperiment>();
        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();

        // to do list
        //
        // supress duplicate experiments from running - done
        // reset non-rerunnable experiments after deploying the experiment (you can just docking jeb from a veseel with bob and the experiments will be already be available) - done-ish, see next
        // timeout/handle experiment result notification windows automaticly, maybe suppress to toolbar window? - somewhat done, directly adding automaticly run experiments to avoid deplayexperiment issue
        // integrate science lab
        // move all data to one container when docked - done
        // allow a user specified container to hold data
        // transmit data from probes automaticly


        private void Awake()
        {
            // we have to do this here because apparently the app launcher events don't load at the right time, so we just check for ready here and then create the toolbar.

            if (ApplicationLauncher.Ready)
            {

                FSAppButton = ApplicationLauncher.Instance.AddModApplication(

                        toggleCollection,
                        toggleCollection,
                        null,
                        null,
                        null,
                        null,
                        ApplicationLauncher.AppScenes.FLIGHT,
                        GameDatabase.Instance.GetTexture("ForScience/Plugins/FS_active", false)
                   );
            }

        }

        private void Update()
        {
            // this is the primary logic that controls when to do what, so we aren't contstantly eating cpu
            if (FlightGlobals.ActiveVessel.FindPartModulesImplementing<ModuleScienceContainer>().Count() == 0) // Check if any science containers are on the vessel, if not, remove the app button
            {
                ApplicationLauncher.Instance.RemoveModApplication(FSAppButton);
            }
            else if (HighLogic.CurrentGame.Mode == Game.Modes.CAREER | HighLogic.CurrentGame.Mode == Game.Modes.SCIENCE_SANDBOX) // only modes with science mechanics will run
            {
                if (autoTransfer) // if we've enabled the app to run, on by default, the toolbar toggles this. Also, check to see if we are NOT eva (controlling a kerbal)
                {
                    TransferScience();// always move experiment data to science container, mostly for manual experiments
                    if (StatesHaveChanged()) // if we are in a new state, we will check and run experiments
                    {
                        RunScience();
                    }
                }
            }
        }

        private void OnDestroy() // we have to clear the toolbar button ondestroy or you get multiple toolbars when you switch vessel, also i'm sure its a good practice, or some salty programmer stuff
        {
            ApplicationLauncher.Instance.RemoveModApplication(FSAppButton);
            FSAppButton = null;
        }

        private void TransferScience()
        {
            if (ActiveContainer().GetActiveVesselDataCount() != ActiveContainer().GetScienceCount()) // only actually transfer if there is data to move
            {
#if DEBUG
                Debug.Log("[For Science] Tranfering science to container.");
#endif
                ActiveContainer().StoreData(GetExperimentList().Cast<IScienceDataContainer>().ToList(), true); // this is what actually moves the data to the active container
                List<ModuleScienceContainer> containerstotransfer = GetContainerList(); // a temporary list of our containers
                containerstotransfer.Remove(ActiveContainer()); // we need to remove the container we storing the data in because that would be wierd and buggy
                ActiveContainer().StoreData(containerstotransfer.Cast<IScienceDataContainer>().ToList(), true); // now we store all data from other containers
            }
        }

        private void RunScience()
        {
            if (GetExperimentList() == null) // hey, it can happen!
            {
#if DEBUG
                Debug.Log("[For Science] GetExperimentList() was null.");
#endif
            }
            else
            {
                foreach (ModuleScienceExperiment currentExperiment in GetExperimentList()) // loop through all the experiments onboard, checking each one for valid experiments to run
                {

#if DEBUG
                    Debug.Log("[For Science] Checking experiment: " + currentScienceSubject(currentExperiment.experiment).id);
#endif

                    if (ActiveContainer().HasData(newScienceData(currentExperiment))) // we have the same experiment data onboard, so we skip it
                    {
#if DEBUG
                        Debug.Log("[For Science] Skipping: We already have that data onboard.");
#endif
                    }
                    else if (!currentExperiment.rerunnable & !IsScientistOnBoard()) // no cheating goo and materials here
                    {
#if DEBUG
                        Debug.Log("[For Science] Skipping: Experiment is not repeatable.");
#endif
                    }
                    else if (!currentExperiment.experiment.IsAvailableWhile(currentSituation(), currentBody())) // this experiement isn't available here so we skip it
                    {
#if DEBUG
                        Debug.Log("[For Science] Skipping: Experiment is not available for this situation/atmosphere.");
#endif
                    }
                    else if (currentScienceValue(currentExperiment) == 0) // this experiment has no more value so we skip it
                    {
#if DEBUG
                        Debug.Log("[For Science] Skipping: No more science is available: ");
#endif
                    }
                    else
                    {
#if DEBUG
                        Debug.Log("[For Science] Running experiment: " + currentScienceSubject(currentExperiment.experiment).id);
#endif
                        ActiveContainer().AddData(newScienceData(currentExperiment)); //manually add data to avoid deployexperiment state issues
                    }

                }
            }
        }

        //lots of little helper functions to make the main code readable

        private float currentScienceValue(ModuleScienceExperiment currentExperiment)
        {
            return ResearchAndDevelopment.GetScienceValue(
                                    currentExperiment.experiment.baseValue * currentExperiment.experiment.dataScale,
                                    currentScienceSubject(currentExperiment.experiment));
        }

        private ScienceData newScienceData(ModuleScienceExperiment currentExperiment)
        {
            return new ScienceData(
                        currentExperiment.experiment.baseValue * currentScienceSubject(currentExperiment.experiment).dataScale,
                        currentExperiment.xmitDataScalar,
                        0f,
                        currentScienceSubject(currentExperiment.experiment).id,
                        currentScienceSubject(currentExperiment.experiment).title
                        );
        }

        private Vessel currentVessel()
        {
            return FlightGlobals.ActiveVessel;
        }

        private CelestialBody currentBody()
        {
            return FlightGlobals.ActiveVessel.mainBody;
        }

        private ExperimentSituations currentSituation()
        {
            return ScienceUtil.GetExperimentSituation(FlightGlobals.ActiveVessel);
        }

        public static string currentBiome()
        {
            if (FlightGlobals.ActiveVessel != null)
                if (FlightGlobals.ActiveVessel.mainBody.BiomeMap != null)
                    return !string.IsNullOrEmpty(FlightGlobals.ActiveVessel.landedAt)
                                    ? Vessel.GetLandedAtString(FlightGlobals.ActiveVessel.landedAt)
                                    : ScienceUtil.GetExperimentBiome(FlightGlobals.ActiveVessel.mainBody,
                                                FlightGlobals.ActiveVessel.latitude, FlightGlobals.ActiveVessel.longitude);

            return string.Empty;
        }

        //private string currentBiome() // we keep a running check on the vessels state to see if we've changed biomes.
        //{
        //    if (currentVessel().landedAt != string.Empty) // more string emptys for weird biomes
        //    {
        //        if (currentVessel().landedAt == "KSC_Pad_Grounds") return "LaunchPad"; // handle a bunch of special biomes around ksc
        //        else if (currentVessel().landedAt == "KSC_LaunchPad_Platform") return "LaunchPad";
        //        else if (currentVessel().landedAt == "KSC_Pad_Flag_Pole") return "LaunchPad";
        //        else if (currentVessel().landedAt == "KSC_Pad_Water_Tower") return "LaunchPad";
        //        else if (currentVessel().landedAt == "KSC_Pad_Tanks") return "LaunchPad";
        //        else if (currentVessel().landedAt == "KSC_Pad_Round_Tank") return "LaunchPad";
        //        else if (currentVessel().landedAt == "KSC_SPH_Grounds") return "SPH";
        //        else if (currentVessel().landedAt == "KSC_SPH_Round_Tank") return "SPHRoundTank";
        //        else if (currentVessel().landedAt == "KSC_SPH_Main_Building") return "SPHMainBuilding";
        //        else if (currentVessel().landedAt == "KSC_SPH_Water_Tower") return "SPHWaterTower";
        //        else if (currentVessel().landedAt == "KSC_SPH_Tanks") return "SPHTanks";
        //        else if (currentVessel().landedAt == "KSC_Crawlerway") return "Crawlerway";
        //        else if (currentVessel().landedAt == "KSC_Mission_Control_Grounds") return "MissionControl";
        //        else if (currentVessel().landedAt == "KSC_Mission_Control") return "MissionControl";
        //        else if (currentVessel().landedAt == "KSC_VAB_Grounds") return "VAB";
        //        else if (currentVessel().landedAt == "KSC_VAB_Round_Tank") return "VABRoundTank";
        //        else if (currentVessel().landedAt == "KSC_VAB_Pod_Memorial") return "VABPodMemorial";
        //        else if (currentVessel().landedAt == "KSC_VAB_Main_Building") return "VABMainBuilding";
        //        else if (currentVessel().landedAt == "KSC_VAB_Tanks") return "VABTanks";
        //        else if (currentVessel().landedAt == "KSC_Astronaut_Complex_Grounds") return "AstronautComplex"; // yes, it is messy
        //        else if (currentVessel().landedAt == "KSC_Astronaut_Complex") return "AstronautComplex";
        //        else if (currentVessel().landedAt == "KSC_Administration_Grounds") return "Administration";
        //        else if (currentVessel().landedAt == "KSC_Administration") return "Administration";
        //        else if (currentVessel().landedAt == "KSC_Flag_Pole") return "FlagPole";
        //        else if (currentVessel().landedAt == "KSC_R&D_Grounds") return "R&D";
        //        else if (currentVessel().landedAt == "KSC_R&D_Corner_Lab") return "R&DCornerLab";
        //        else if (currentVessel().landedAt == "KSC_R&D_Central_Building") return "R&DCentralBuilding";
        //        else if (currentVessel().landedAt == "KSC_R&D_Wind_Tunnel") return "R&DWindTunnel";
        //        else if (currentVessel().landedAt == "KSC_R&D_Main_Building") return "R&DMainBuilding";
        //        else if (currentVessel().landedAt == "KSC_R&D_Tanks") return "R&DTanks";
        //        else if (currentVessel().landedAt == "KSC_R&D_Observatory") return "R&DObservatory";
        //        else if (currentVessel().landedAt == "KSC_R&D_Side_Lab") return "R&DSideLab";
        //        else if (currentVessel().landedAt == "KSC_R&D_Observatory") return "R&DObservatory";
        //        else if (currentVessel().landedAt == "KSC_R&D_Small_Lab") return "R&DSmallLab";
        //        else if (currentVessel().landedAt == "KSC_Tracking_Station_Grounds") return "TrackingStation";
        //        else if (currentVessel().landedAt == "KSC_Tracking_Station_Hub") return "TrackingStationHub";
        //        else if (currentVessel().landedAt == "KSC_Tracking_Station_Dish_East") return "TrackingStationDishEast";
        //        else if (currentVessel().landedAt == "KSC_Tracking_Station_Dish_South") return "TrackingStationDishSouth";
        //        else if (currentVessel().landedAt == "KSC_Tracking_Station_Dish_North") return "TrackingStationDishNorth"; // very messy
        //        else return currentVessel().landedAt; // you're still here?
        //    }
        //    else return ScienceUtil.GetExperimentBiome(currentBody(), currentVessel().latitude, currentVessel().longitude); //else give up, err...return squad's *cough* highly accurate biome *cough*
        //}

        private ScienceSubject currentScienceSubject(ScienceExperiment experiment)
        {
            string fixBiome = string.Empty; // some biomes don't have 4th string, so we just put an empty in to compare strings later
            if (experiment.BiomeIsRelevantWhile(currentSituation())) fixBiome = currentBiome();// for those that do, we add it to the string
            return ResearchAndDevelopment.GetExperimentSubject(experiment, currentSituation(), currentBody(), fixBiome);//ikr!, we pretty much did all the work already, jeez
        }

        private ModuleScienceContainer ActiveContainer() // set the container to gather all science data inside, usualy this is the root command pod of the oldest vessel
        {
            return FlightGlobals.ActiveVessel.FindPartModulesImplementing<ModuleScienceContainer>().FirstOrDefault();
        }

        private List<ModuleScienceExperiment> GetExperimentList() // a list of all experiments
        {
            return FlightGlobals.ActiveVessel.FindPartModulesImplementing<ModuleScienceExperiment>();
        }

        private List<ModuleScienceContainer> GetContainerList() // a list of all science containers
        {
            return FlightGlobals.ActiveVessel.FindPartModulesImplementing<ModuleScienceContainer>(); // list of all experiments onboard
        }

        private bool StatesHaveChanged() // Track our vessel state, it is used for thread control to know when to fire off new experiments.
        {
            if (currentVessel() != stateVessel | currentSituation() != stateSituation | currentBody() != stateBody | currentBiome() != stateBiome)
            {
                stateVessel = currentVessel();
                stateBody = currentBody();
                stateSituation = currentSituation();
                stateBiome = currentBiome();
                stopwatch.Reset();
                stopwatch.Start();
            }

            if (stopwatch.ElapsedMilliseconds > 100) // trottling detection to kill transient states.
            {
                stopwatch.Reset();
#if DEBUG
                Debug.Log("[For Science] Vessel in new experiment state.");
#endif
                return true;
            }
            else return false;
        }

        private void toggleCollection() // This is our main toggle for the logic and changes the icon between green and red versions on the bar when it does so.
        {
            if (autoTransfer)
            {
                autoTransfer = false;
                icon = GameDatabase.Instance.GetTexture("ForScience/Plugins/FS_inactive", false); // change the red colored icon
                FSAppButton.SetTexture(icon);
            }
            else
            {
                autoTransfer = true; // FIRE EVERYTHING!
                icon = GameDatabase.Instance.GetTexture("ForScience/Plugins/FS_active", false); // change to the green colored icon
                FSAppButton.SetTexture(icon);
            }
        }

        private bool IsScientistOnBoard() // check if there is a scientist onboard so we can rerun things like goo or scijrs
        {
            var returnvalue = false;
            foreach (ProtoCrewMember kerbal in currentVessel().GetVesselCrew())
            {
                if (kerbal.experienceTrait.Title == "Scientist")
                {
                    returnvalue = true;
                }
                else returnvalue = false;
            }
            return returnvalue;
        }
    }
}






