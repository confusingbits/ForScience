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
        private Rect windowPosition = new Rect();
        private bool initStyle = false;

        // functionality
        private ModuleScienceContainer container = null;
        private bool autoTransfer = false;

        private void Start()
        {
            if (!initStyle) InitStyle();
            RenderingManager.AddToPostDrawQueue(0, OnDraw);
        }

        void Update()
        {
            if (autoTransfer & !FlightGlobals.ActiveVessel.isEVA) TransferScience();
        }

        void OnDraw()
        {
            if (HighLogic.LoadedScene == GameScenes.FLIGHT)
            {
                windowPosition = GUILayout.Window(104234, windowPosition, MainWindow, "For Science", windowStyle);
            }
        }

        public void TransferScience()
        {
            var containerList = FlightGlobals.ActiveVessel.FindPartModulesImplementing<ModuleScienceContainer>();
            var experimentList = FlightGlobals.ActiveVessel.FindPartModulesImplementing<ModuleScienceExperiment>().Cast<IScienceDataContainer>().ToList();
            if (container = null) container = containerList[0];
            containerList[0].StoreData(experimentList, true);
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
                autoTransfer = true;
            }
            else autoTransfer = false;
            GUILayout.EndHorizontal();


            GUI.DragWindow();
        }
    }
}






