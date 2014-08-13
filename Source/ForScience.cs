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
        private GUIStyle windowStyle, labelStyle, toggleStyle, textStyle;
        private Rect windowPosition = new Rect(0f, 200f, 0f, 0f);
        private bool initStyle = false;

        //states

        bool initState = false;
        Vessel stateVessel = null;
        CelestialBody stateBody = null;
        string stateBiome = null;
        ExperimentSituations stateSituation = 0;

        //current

        Vessel currentVessel = null;
        CelestialBody currentBody = null;
        string currentBiome = null;
        ExperimentSituations currentSituation = 0;
        List<ModuleScienceContainer> containerList = null;
        List<ModuleScienceExperiment> experimentList = null;
        ModuleScienceContainer container = null;

        //thread control

        bool runOnce = false;
        bool IsDataToCollect = false;
        bool autoTransfer = false;
        bool dataIsInContainer = false;

        private void Start()
        {
            if ((HighLogic.CurrentGame.Mode == Game.Modes.CAREER | HighLogic.CurrentGame.Mode == Game.Modes.SCIENCE_SANDBOX) & !initStyle) InitStyle();

            RenderingManager.AddToPostDrawQueue(0, OnDraw);
        }

        private void Update()
        {
            if (HighLogic.CurrentGame.Mode == Game.Modes.CAREER | HighLogic.CurrentGame.Mode == Game.Modes.SCIENCE_SANDBOX)
            {
                if (initState)
                {
                    UpdateCurrent();
                    if (!currentVessel.isEVA & autoTransfer)
                    {

                        if (IsDataToCollect) TransferScience();
                        else if (runOnce | !IsDataToCollect & (currentVessel != stateVessel | currentSituation != stateSituation | currentBody != stateBody | currentBiome != stateBiome))
                        {
                            Debug.Log("[For Science] Vessel in new experimental situation.");
                            RunScience();
                            UpdateStates();
                            runOnce = false;
                        }
                        else FindDataToTransfer();

                    }

                }
                else
                {
                    UpdateCurrent();
                    UpdateStates();
                    TransferScience();
                    initState = true;
                }
            }
        }

        private void OnDraw()
        {
            if (HighLogic.LoadedScene == GameScenes.FLIGHT & (HighLogic.CurrentGame.Mode == Game.Modes.CAREER | HighLogic.CurrentGame.Mode == Game.Modes.SCIENCE_SANDBOX))
            {
                windowPosition = GUILayout.Window(104234, windowPosition, MainWindow, "For Science", windowStyle);
            }
        }

        private void FindDataToTransfer()
        {
            foreach (ModuleScienceExperiment currentExperiementCollectData in experimentList)
            {
                if (currentExperiementCollectData.GetData().Count() == 1) IsDataToCollect = true;
            }
        }

        private void TransferScience()
        {
            containerList = currentVessel.FindPartModulesImplementing<ModuleScienceContainer>();
            experimentList = currentVessel.FindPartModulesImplementing<ModuleScienceExperiment>();

            if (container == null) container = containerList[0];

            Debug.Log("[For Science] Tranfering science to container.");
            container.StoreData(experimentList.Cast<IScienceDataContainer>().ToList(), true);

            IsDataToCollect = false;
        }

        private void RunScience()
        {
            foreach (ModuleScienceExperiment currentExperiment in experimentList)
            {
                var fixBiome = string.Empty;

                if (currentExperiment.experiment.BiomeIsRelevantWhile(currentSituation)) fixBiome = currentBiome;

                var currentScienceSubject = ResearchAndDevelopment.GetExperimentSubject(currentExperiment.experiment, currentSituation, currentBody, fixBiome);
                var currentScienceValue = ResearchAndDevelopment.GetScienceValue(currentExperiment.experiment.baseValue * currentExperiment.experiment.dataScale, currentScienceSubject);

                Debug.Log("[For Science] Checking experiment: " + currentScienceSubject.id);

                if (!currentExperiment.rerunnable)
                {
                    Debug.Log("[For Science] Skipping: Experiment is not repeatable.");
                }
                else if (!currentExperiment.experiment.IsAvailableWhile(currentSituation, currentBody))
                {
                    Debug.Log("[For Science] Skipping: Experiment is not available.");
                }
                else if (currentScienceValue < 1)
                {
                    Debug.Log("[For Science] Skipping: No more science is available.");
                }
                else
                {

                    foreach (ScienceData data in container.GetData())
                    {
                        if (currentScienceSubject.id.Contains(data.subjectID))
                        {
                            Debug.Log("[For Science]  Skipping: Found existing experiment data: " + data.subjectID);
                            dataIsInContainer = true;
                            break;
                        }

                        else
                        {
                            dataIsInContainer = false;
                            UpdateCurrent();
                        }
                    }

                    if (!dataIsInContainer)
                    {
                        Debug.Log("[For Science] Science available is " + currentScienceValue);
                        Debug.Log("[For Science] Running experiment: " + currentExperiment.experiment.id);
                        currentExperiment.DeployExperiment();
                        IsDataToCollect = true;
                    }

                }
            }
        }

        private void UpdateCurrent()
        {
            currentVessel = FlightGlobals.ActiveVessel;
            currentBody = currentVessel.mainBody;
            currentSituation = ScienceUtil.GetExperimentSituation(currentVessel);
            if (currentVessel.landedAt != string.Empty)
            {
                currentBiome = currentVessel.landedAt;
            }
            else currentBiome = ScienceUtil.GetExperimentBiome(currentBody, currentVessel.latitude, currentVessel.longitude);
        }

        private void UpdateStates()
        {
            stateVessel = currentVessel;
            stateBody = currentBody;
            stateSituation = currentSituation;
            stateBiome = currentBiome;
        }

        private void InitStyle()
        {
            labelStyle = new GUIStyle(HighLogic.Skin.label);
            labelStyle.stretchWidth = true;

            windowStyle = new GUIStyle(HighLogic.Skin.window);
            windowStyle.fixedWidth = 250f;

            toggleStyle = new GUIStyle(HighLogic.Skin.toggle);

            textStyle = new GUIStyle(HighLogic.Skin.label);

            initStyle = true;
        }

        private void MainWindow(int windowID)
        {
            GUILayout.BeginHorizontal();
            if (currentVessel.FindPartModulesImplementing<ModuleScienceContainer>().Count() == 0)
            {
                GUILayout.TextField("No science containers available.", textStyle);
            }
            else if (GUILayout.Toggle(autoTransfer, "Automatic data collection", toggleStyle))
            {
                autoTransfer = true;
            }
            else
            {
                autoTransfer = false;
                runOnce = true;
            }
            GUILayout.EndHorizontal();

            GUI.DragWindow();
        }
    }
}






