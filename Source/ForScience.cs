using KSP.UI.Screens;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ForScience
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    class ForScience : MonoBehaviour
    {
        //GUI
        ApplicationLauncherButton FSAppButton = null;

        //states
        Vessel stateVessel = null;
        CelestialBody stateBody = null;
        string stateBiome = null;
        ExperimentSituations stateSituation = 0;

        //thread control
        bool autoTransfer = true;
        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();

        // to do list
        //
        // integrate science lab
        // allow a user specified container to hold data
        // transmit data from probes automaticly

        void OnGUI()
        {
            // we have to do this here because apparently the app launcher events don't load at the right time, so we just check for ready here and then create the toolbar.

            if (ApplicationLauncher.Ready && FSAppButton == null)
            {

                FSAppButton = ApplicationLauncher.Instance.AddModApplication(

                       onTrue: toggleCollection,
                       onFalse: toggleCollection,
                       onHover: null,
                       onHoverOut: null,
                       onEnable: null,
                       onDisable: null,
                       visibleInScenes: ApplicationLauncher.AppScenes.FLIGHT,
                       texture: GameDatabase.Instance.GetTexture("ForScience/Icons/FS_active", false)
                   );
            }

        }

        void FixedUpdate() // running in physics update so that the vessel is always in a valid state to check for science.
        {
            // this is the primary logic that controls when to do what, so we aren't contstantly eating cpu
            if (FlightGlobals.ActiveVessel.FindPartModulesImplementing<ModuleScienceContainer>().Count() == 0) // Check if any science containers are on the vessel, if not, remove the app button
            {
                ApplicationLauncher.Instance.RemoveModApplication(FSAppButton);
            }
            else if (HighLogic.CurrentGame.Mode == Game.Modes.CAREER | HighLogic.CurrentGame.Mode == Game.Modes.SCIENCE_SANDBOX) // only modes with science mechanics will run
            {
                if (autoTransfer) // if we've enabled the app to run, on by default, the toolbar button toggles this.
                {
                    TransferScience();// always move experiment data to science container, mostly for manual experiments
                    if (StatesHaveChanged()) // if we are in a new state, we will check and run experiments
                    {
                        RunScience();
                    }
                }
            }

        }

        void OnDestroy() // we have to clear the toolbar button ondestroy or you get multiple toolbars when you switch vessel, also i'm sure its a good practice, or some salty programmer stuff
        {
            ApplicationLauncher.Instance.RemoveModApplication(FSAppButton);
            FSAppButton = null;
        }

        void TransferScience() // automaticlly find, transer and consolidate science data on the vessel
        {
            if (ActiveContainer().GetActiveVesselDataCount() != ActiveContainer().GetScienceCount()) // only actually transfer if there is data to move
            {

                Debug.Log("[ForScience!] Transfering science to container.");

                ActiveContainer().StoreData(GetExperimentList().Cast<IScienceDataContainer>().ToList(), true); // this is what actually moves the data to the active container
                List<ModuleScienceContainer> containerstotransfer = GetContainerList(); // a temporary list of our containers
                containerstotransfer.Remove(ActiveContainer()); // we need to remove the container we storing the data in because that would be wierd and buggy
                ActiveContainer().StoreData(containerstotransfer.Cast<IScienceDataContainer>().ToList(), true); // now we store all data from other containers
            }
        }

        void RunScience() // this is primary business logic for finding and running valid experiments
        {
            if (GetExperimentList() == null) // hey, it can happen!
            {
                Debug.Log("[ForScience!] GetExperimentList() was null.");
            }
            else
            {
                foreach (ModuleScienceExperiment currentExperiment in GetExperimentList()) // loop through all the experiments onboard, checking each one for valid experiments to run
                {


                    Debug.Log("[ForScience!] Checking experiment: " + currentScienceSubject(currentExperiment.experiment).id);

                    if (ActiveContainer().HasData(newScienceData(currentExperiment))) // we have the same experiment data onboard, so we skip it
                    {

                        Debug.Log("[ForScience!] Skipping: We already have that data onboard.");

                    }
                    else if (!currentExperiment.rerunnable && !IsScientistOnBoard()) // no cheating goo and materials here
                    {

                        Debug.Log("[ForScience!] Skipping: Experiment is not repeatable.");

                    }
                    else if (!currentExperiment.experiment.IsAvailableWhile(currentSituation(), currentBody())) // this experiement isn't available here so we skip it
                    {

                        Debug.Log("[ForScience!] Skipping: Experiment is not available for this situation/atmosphere.");

                    }
                    else if (currentScienceValue(currentExperiment) < 0.1) // this experiment has no more value so we skip it
                    {

                        Debug.Log("[ForScience!] Skipping: No more science is available: ");

                    }
                    else
                    {
                        Debug.Log("[ForScience!] Running experiment: " + currentScienceSubject(currentExperiment.experiment).id);

                        ActiveContainer().AddData(newScienceData(currentExperiment)); //manually add data to avoid deployexperiment state issues
                    }

                }
            }
        }

        //lots of little helper functions to make the main code readable
        float currentScienceValue(ModuleScienceExperiment currentExperiment)
        {
            return ResearchAndDevelopment.GetScienceValue(
                                    currentExperiment.experiment.baseValue * currentExperiment.experiment.dataScale,
                                    currentScienceSubject(currentExperiment.experiment));
        }

        ScienceData newScienceData(ModuleScienceExperiment currentExperiment)
        {
            return new ScienceData(
                       amount: currentExperiment.experiment.baseValue * currentScienceSubject(currentExperiment.experiment).dataScale,
                       xmitValue: currentExperiment.xmitDataScalar,
                       labBoost: 0f,
                       id: currentScienceSubject(currentExperiment.experiment).id,
                       dataName: currentScienceSubject(currentExperiment.experiment).title
                       );
        }

        Vessel currentVessel()
        {
            return FlightGlobals.ActiveVessel;
        }

        CelestialBody currentBody()
        {
            return FlightGlobals.ActiveVessel.mainBody;
        }

        ExperimentSituations currentSituation()
        {
            return ScienceUtil.GetExperimentSituation(FlightGlobals.ActiveVessel);
        }

        string currentBiome()
        {
            if (FlightGlobals.ActiveVessel != null)
                if (FlightGlobals.ActiveVessel.mainBody.BiomeMap != null)
                    return !string.IsNullOrEmpty(FlightGlobals.ActiveVessel.landedAt)
                                    ? Vessel.GetLandedAtString(FlightGlobals.ActiveVessel.landedAt)
                                    : ScienceUtil.GetExperimentBiome(FlightGlobals.ActiveVessel.mainBody,
                                                FlightGlobals.ActiveVessel.latitude, FlightGlobals.ActiveVessel.longitude);

            return string.Empty;
        }

        ScienceSubject currentScienceSubject(ScienceExperiment experiment)
        {
            string fixBiome = string.Empty; // some biomes don't have 4th string, so we just put an empty in to compare strings later
            if (experiment.BiomeIsRelevantWhile(currentSituation())) fixBiome = currentBiome();// for those that do, we add it to the string
            return ResearchAndDevelopment.GetExperimentSubject(experiment, currentSituation(), currentBody(), fixBiome);//ikr!, we pretty much did all the work already, jeez
        }

        ModuleScienceContainer ActiveContainer() // set the container to gather all science data inside, usualy this is the root command pod of the oldest vessel
        {
            return FlightGlobals.ActiveVessel.FindPartModulesImplementing<ModuleScienceContainer>().FirstOrDefault();
        }

        List<ModuleScienceExperiment> GetExperimentList() // a list of all experiments
        {
            return FlightGlobals.ActiveVessel.FindPartModulesImplementing<ModuleScienceExperiment>();
        }

        List<ModuleScienceContainer> GetContainerList() // a list of all science containers
        {
            return FlightGlobals.ActiveVessel.FindPartModulesImplementing<ModuleScienceContainer>(); // list of all experiments onboard
        }

        bool StatesHaveChanged() // Track our vessel state, it is used for thread control to know when to fire off new experiments.
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

            if (stopwatch.ElapsedMilliseconds > 100) // throttling detection to kill transient states.
            {
                stopwatch.Reset();

                Debug.Log("[ForScience!] Vessel in new experiment state.");

                return true;
            }
            else return false;
        }

        void toggleCollection() // This is our main toggle for the logic and changes the icon between green and red versions on the bar when it does so.
        {
            if (autoTransfer)
            {
                autoTransfer = false;
                FSAppButton.SetTexture(GameDatabase.Instance.GetTexture("ForScience/Icons/FS_inactive", false)); // change to the red colored icon
            }
            else
            {
                autoTransfer = true; // FIRE EVERYTHING!
                FSAppButton.SetTexture(GameDatabase.Instance.GetTexture("ForScience/Icons/FS_active", false)); // change to the green colored icon
            }
        }

        bool IsScientistOnBoard() // check if there is a scientist onboard so we can rerun things like goo or scijrs
        {
            foreach (ProtoCrewMember kerbal in currentVessel().GetVesselCrew())
            {
                if (kerbal.experienceTrait.Title == "Scientist") return true;
            }
            return false;
        }
    }
}