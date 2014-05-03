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
        private GUIStyle windowStyle, labelStyle, toggleStyle;
        private Rect windowPosition = new Rect(0f, 200f, 0f, 0f);
        private bool initStyle = false;

        // states

        bool initState = false;
        Vessel stateVessel = null;
        CelestialBody stateBody = null;
        string stateBiome = null;
        ExperimentSituations stateSituation = 0;

        //global variables

        bool IsDataToCollect = false;
        bool autoTransfer = false;
        bool dataIsInContainer = false;
        Vessel currentVessel = null;
        CelestialBody currentBody = null;
        string currentBiome = null;
        ExperimentSituations currentSituation = 0;
        List<ModuleScienceContainer> containerList = null;
        List<ModuleScienceExperiment> experimentList = null;
        ModuleScienceContainer container = null;


        private void Start()
        {
            if (!initStyle) InitStyle();
            RenderingManager.AddToPostDrawQueue(0, OnDraw);

            if (!initState)
            {
                UpdateStates();
            }
        }

        private void Update()
        {
            UpdateCurrent();
            
            if (!currentVessel.isEVA & autoTransfer & IsDataToCollect) ManageScience();            

            if (!currentVessel.isEVA & autoTransfer & (currentVessel != stateVessel | currentSituation != stateSituation | currentBody != stateBody | currentBiome != stateBiome))
            {
                RunScience();
                UpdateStates();
            }
        }

        private void OnDraw()
        {
            if (HighLogic.LoadedScene == GameScenes.FLIGHT)
            {
                windowPosition = GUILayout.Window(104234, windowPosition, MainWindow, "For Science", windowStyle);
            }
        }

        private void ManageScience()
        {
            containerList = currentVessel.FindPartModulesImplementing<ModuleScienceContainer>();
            experimentList = currentVessel.FindPartModulesImplementing<ModuleScienceExperiment>();

            if (container == null) container = containerList[0];
            container.StoreData(experimentList.Cast<IScienceDataContainer>().ToList(), true);
        }

        private void RunScience()
        {
            foreach (ModuleScienceExperiment currentExperiment in experimentList)
            {
                if (currentExperiment.rerunnable & currentExperiment.experiment.IsAvailableWhile(currentSituation, currentBody))
                {
                    Debug.Log("checking experiment: " + currentExperiment.name);

                    dataIsInContainer = false;

                    foreach (ScienceData data in container.GetData())
                    {
                        if (data.subjectID == (currentExperiment.experimentID + "@" + currentBody.name + currentSituation + currentVessel.landedAt).Replace(" ", string.Empty))
                        {
                            //Debug.Log("experiment: " + (currentExperiment.experimentID + "@" + currentBody.name + currentSituation + v.landedAt).Replace(" ", string.Empty));
                            //Debug.Log("data:" + data.subjectID);
                            dataIsInContainer = true;
                        }
                        else if (data.subjectID == (currentExperiment.experimentID + "@" + currentBody.name + currentSituation + currentBiome).Replace(" ", string.Empty))
                        {
                            //Debug.Log("experiment: " + (currentExperiment.experimentID + "@" + currentBody.name + currentSituation + currentBiome).Replace(" ", string.Empty));
                            //Debug.Log("data:" + data.subjectID);
                            dataIsInContainer = true;
                        }
                        else if (data.subjectID == (currentExperiment.experimentID + "@" + currentBody.name + currentSituation).Replace(" ", string.Empty))
                        {
                            //Debug.Log("experiment: " + (currentExperiment.experimentID + "@" + currentBody.name + currentSituation).Replace(" ", string.Empty));
                            //Debug.Log("data:" + data.subjectID);
                            dataIsInContainer = true;
                        }
                    }
                    if (!dataIsInContainer & currentExperiment.GetScienceCount() == 0)
                    {
                        Debug.Log("Running experiment: " + currentExperiment.name);
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
            currentBiome = ScienceUtil.GetExperimentBiome(currentBody, currentVessel.latitude, currentVessel.longitude);
            currentSituation = ScienceUtil.GetExperimentSituation(currentVessel);            
        }

        private void UpdateStates()
        {
            stateVessel = FlightGlobals.ActiveVessel;
            stateBody = currentVessel.mainBody;
            stateBiome = ScienceUtil.GetExperimentBiome(currentBody, currentVessel.latitude, currentVessel.longitude);
            stateSituation = ScienceUtil.GetExperimentSituation(currentVessel);

            initState = true;
        }

        private void InitStyle()
        {
            labelStyle = new GUIStyle(HighLogic.Skin.label);
            labelStyle.stretchWidth = true;

            windowStyle = new GUIStyle(HighLogic.Skin.window);
            windowStyle.fixedWidth = 250f;

            toggleStyle = new GUIStyle(HighLogic.Skin.toggle);

            initStyle = true;
        }

        private void MainWindow(int windowID)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(autoTransfer, "Automatic data collection", toggleStyle))
            {
                if (!autoTransfer)
                {
                    ManageScience();
                    RunScience();
                }
                autoTransfer = true;
            }
            else autoTransfer = false;
            GUILayout.EndHorizontal();

            GUI.DragWindow();
        }
    }
}






