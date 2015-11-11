﻿// Project:         Daggerfall Tools For Unity
// Copyright:       Copyright (C) 2009-2015 Daggerfall Workshop
// Web Site:        http://www.dfworkshop.net
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Source Code:     https://github.com/Interkarma/daggerfall-unity
// Original Author: Gavin Clayton (interkarma@dfworkshop.net)
// Contributors:    Nystul
// 
// Notes:
//

using UnityEngine;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallConnect.Utility;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.Player;
using DaggerfallWorkshop.Game.Entity;

namespace DaggerfallWorkshop.Game.UserInterfaceWindows
{
    /// <summary>
    /// Implements indoor and dungeon automap window.
    /// </summary>
    public class DaggerfallAutomapWindow : DaggerfallPopupWindow
    {
        const float scrollLeftRightSpeed = 1.0f;
        const float scrollForwardBackwardSpeed = 1.0f;
        const float scrollUpDownSpeed = 1.0f;
        const float rotateSpeed = 10.0f;
        const float zoomSpeed = 1.0f;

        const float cameraHeightViewFromTop = 30.0f;
        const float cameraHeightView3D = 8.0f;
        const float cameraBackwardDistance = 20.0f;        


        const string nativeImgName = "AMAP00I0.IMG";
        const string nativeImgNameGrid3D = "AMAP01I0.IMG";

        enum AutomapViewMode { View2D = 0, View3D = 1};
        AutomapViewMode automapViewMode = AutomapViewMode.View2D;

        Panel panelAutomap = null;

        Texture2D nativeTexture;
        Texture2D nativeTextureGrid2D;
        Texture2D nativeTextureGrid3D;

        Color[] pixelsGrid2D;
        Color[] pixelsGrid3D;

        HUDCompass compass;

        Camera cameraAutomap = null;
        RenderTexture renderTextureAutomap = null;
        Texture2D textureAutomap = null;


        int renderTextureAutomapWidth;
        int renderTextureAutomapHeight;
        int renderTextureAutomapDepth = 16;

        Vector3 backupCameraPositionViewFromTop;
        Quaternion backupCameraRotationViewFromTop;
        Vector3 backupCameraPositionView3D;
        Quaternion backupCameraRotationView3D;

        Vector3 biasFromInitialPositionViewFromTop;
        Vector3 biasFromInitialPositionView3D;

        public DaggerfallAutomapWindow(IUserInterfaceManager uiManager)
            : base(uiManager)
        {
        }

        protected override void Setup()
        {
            ImgFile imgFile = null;
            DFBitmap bitmap = null;

            // Load native texture
            imgFile = new ImgFile(Path.Combine(DaggerfallUnity.Instance.Arena2Path, nativeImgName), FileUsage.UseMemory, false);
            imgFile.LoadPalette(Path.Combine(DaggerfallUnity.Instance.Arena2Path, imgFile.PaletteName));
            bitmap = imgFile.GetDFBitmap();
            nativeTexture = new Texture2D(bitmap.Width, bitmap.Height, TextureFormat.ARGB32, false);
            nativeTexture.SetPixels32(imgFile.GetColor32(bitmap, 0));
            nativeTexture.Apply(false, false); // make readable
            nativeTexture.filterMode = DaggerfallUI.Instance.GlobalFilterMode;
            if (!nativeTexture)
                throw new Exception("DaggerfallAutomapWindow: Could not load native texture (AMAP00I0.IMG).");
            
            // Load alternative Grid Icon (3D View Grid graphics)
            imgFile = new ImgFile(Path.Combine(DaggerfallUnity.Instance.Arena2Path, nativeImgNameGrid3D), FileUsage.UseMemory, false);
            imgFile.LoadPalette(Path.Combine(DaggerfallUnity.Instance.Arena2Path, imgFile.PaletteName));
            bitmap = imgFile.GetDFBitmap();
            nativeTextureGrid3D = new Texture2D(bitmap.Width, bitmap.Height, TextureFormat.ARGB32, false);
            nativeTextureGrid3D.SetPixels32(imgFile.GetColor32(bitmap, 0));
            nativeTextureGrid3D.Apply(false, false); // make readable
            nativeTextureGrid3D.filterMode = DaggerfallUI.Instance.GlobalFilterMode;
            if (!nativeTextureGrid3D)
                throw new Exception("DaggerfallAutomapWindow: Could not load native texture (AMAP01I0.IMG).");
            pixelsGrid3D = nativeTextureGrid3D.GetPixels(0, 0, 27, 19);

            // Cut out 2D View Grid graphics from background image
            nativeTextureGrid2D = new Texture2D(27, 19, TextureFormat.ARGB32, false);
            nativeTextureGrid2D.filterMode = FilterMode.Point;
            pixelsGrid2D = nativeTexture.GetPixels(78, nativeTexture.height - 171 - 19, 27, 19);
            nativeTextureGrid2D.SetPixels(pixelsGrid2D);
            
            // Always dim background
            ParentPanel.BackgroundColor = ScreenDimColor;

            // Setup native panel background
            NativePanel.BackgroundTexture = nativeTexture;

            NativePanel.OnMouseScrollUp += NativePanel_OnMouseScrollUp;
            NativePanel.OnMouseScrollDown += NativePanel_OnMouseScrollDown;

            // Grid button (toggle 2D <-> 3D view)
            Button gridButton = DaggerfallUI.AddButton(new Rect(78, 171, 27, 19), NativePanel);
            gridButton.OnMouseClick += GridButton_OnMouseClick;

            // forward button
            Button forwardButton = DaggerfallUI.AddButton(new Rect(105, 171, 21, 19), NativePanel);
            forwardButton.OnMouseDown += ForwardButton_OnMouseDown;

            // backward button
            Button backwardButton = DaggerfallUI.AddButton(new Rect(126, 171, 21, 19), NativePanel);
            backwardButton.OnMouseDown += BackwardButton_OnMouseDown;

            // left button
            Button leftButton = DaggerfallUI.AddButton(new Rect(149, 171, 21, 19), NativePanel);
            leftButton.OnMouseDown += LeftButton_OnMouseDown;
            
            // right button
            Button rightButton = DaggerfallUI.AddButton(new Rect(170, 171, 21, 19), NativePanel);
            rightButton.OnMouseDown += RightButton_OnMouseDown;

            // rotate left button
            Button rotateLeftButton = DaggerfallUI.AddButton(new Rect(193, 171, 21, 19), NativePanel);
            rotateLeftButton.OnMouseDown += RotateLeftButton_OnMouseDown;

            // rotate right button
            Button rotateRightButton = DaggerfallUI.AddButton(new Rect(214, 171, 21, 19), NativePanel);
            rotateRightButton.OnMouseDown += RotateRightButton_OnMouseDown;

            // up button
            Button upButton = DaggerfallUI.AddButton(new Rect(237, 171, 21, 19), NativePanel);
            upButton.OnMouseDown += UpButton_OnMouseDown;

            // down button
            Button downButton = DaggerfallUI.AddButton(new Rect(258, 171, 21, 19), NativePanel);
            downButton.OnMouseDown += DownButton_OnMouseDown;

            // Exit button
            Button exitButton = DaggerfallUI.AddButton(new Rect(281, 171, 28, 19), NativePanel);
            exitButton.OnMouseClick += ExitButton_OnMouseClick;

            if (!cameraAutomap)
            {
                GameObject gameObjectCameraAutomap = new GameObject("cameraAutomap");
                cameraAutomap = gameObjectCameraAutomap.AddComponent<Camera>();
                cameraAutomap.clearFlags = CameraClearFlags.SolidColor;
                cameraAutomap.cullingMask = 1;
                //cameraAutomap.depth = 10;
                cameraAutomap.renderingPath = RenderingPath.DeferredLighting;
                cameraAutomap.nearClipPlane = 0.7f;
                cameraAutomap.farClipPlane = 1000.0f;
                cameraAutomap.fieldOfView = 45.0f;
                //cameraAutomap.orthographic = true;

                //cameraAutomap.transform.position = GameObject.Find("PlayerAdvanced").transform.position;
                //cameraAutomap.transform.rotation = GameObject.Find("PlayerAdvanced").transform.rotation;
            }

            renderTextureAutomapWidth = ParentPanel.InteriorWidth - 19;
            renderTextureAutomapHeight = ParentPanel.InteriorHeight - 98;

            if (!renderTextureAutomap)
                renderTextureAutomap = new RenderTexture(renderTextureAutomapWidth, renderTextureAutomapHeight, renderTextureAutomapDepth);
            cameraAutomap.targetTexture = renderTextureAutomap;

            Rect position = new Rect(9f, 2f, (float)renderTextureAutomapWidth, (float)renderTextureAutomapHeight);
            if (panelAutomap == null)
                panelAutomap = DaggerfallUI.AddPanel(position, ParentPanel);

            if (!textureAutomap)
                textureAutomap = new Texture2D(renderTextureAutomap.width, renderTextureAutomap.height, TextureFormat.ARGB32, false);

            resetCameraPosition();
            resetBiasFromInitialPosition();
            updateAutoMapView();

            NativePanel.Update(); // needed so that NativePanel.LocalScale holds correct value
            
            compass = new HUDCompass(cameraAutomap);
            Vector2 scale = NativePanel.LocalScale;
            compass.CompassBoxRect = new Rect(3f * scale.x, 172f * scale.y, 76f * scale.x, 17f * scale.y);
            compass.Scale = NativePanel.LocalScale;
            NativePanel.Components.Add(compass);
        }

        
        public override void OnPush()
        {
            if (IsSetup)
            {
                if (cameraAutomap)
                {
                    resetCameraPosition();
                    resetBiasFromInitialPosition();
                    updateAutoMapView();
                }              
            }
        }

        #region Private Methods

        private void resetCameraPosition()
        {
            // get initial values for camera transform for view from top
            resetCameraTransformViewFromTop();
            saveCameraTransformViewFromTop();
            
            // get initial values for camera transform for 3D view
            resetCameraTransformView3D();
            saveCameraTransformView3D();

            // then set camera transform according to grid-button (view mode) setting
            switch (automapViewMode)
            {
                case AutomapViewMode.View2D:
                    resetCameraTransformViewFromTop();
                    break;
                case AutomapViewMode.View3D:
                    resetCameraTransformView3D();
                    break;
                default:
                    break;
            }
        }

        private void resetBiasFromInitialPosition()
        {
            biasFromInitialPositionViewFromTop = Vector3.zero;
            biasFromInitialPositionView3D = Vector3.forward;
        }

        private void shiftBiasFromInitialPosition(Vector3 translation)
        {
            switch (automapViewMode)
            {
                case AutomapViewMode.View2D:
                    biasFromInitialPositionViewFromTop += translation;
                    break;
                case AutomapViewMode.View3D:
                    biasFromInitialPositionView3D += translation;
                    break;
                default:
                    break;
            }
        }

        private void resetCameraTransformViewFromTop()
        {
            cameraAutomap.transform.position = Camera.main.transform.position + Vector3.up * cameraHeightViewFromTop;
            cameraAutomap.transform.LookAt(Camera.main.transform.position);            
        }

        private void resetCameraTransformView3D()
        {
            Vector3 viewDirectionInXZ = Vector3.forward; // new Vector3(Camera.main.transform.forward.x, 0.0f, Camera.main.transform.forward.z);
            cameraAutomap.transform.position = Camera.main.transform.position - viewDirectionInXZ * cameraBackwardDistance + Vector3.up * cameraHeightView3D;
            //cameraAutomap.transform.rotation = Camera.main.transform.rotation;
            cameraAutomap.transform.LookAt(Camera.main.transform.position);
        }

        private void saveCameraTransformViewFromTop()
        {
            backupCameraPositionViewFromTop = cameraAutomap.transform.position;
            backupCameraRotationViewFromTop = cameraAutomap.transform.rotation;
        }

        private void saveCameraTransformView3D()
        {
            backupCameraPositionView3D = cameraAutomap.transform.position;
            backupCameraRotationView3D = cameraAutomap.transform.rotation;
        }

        private void restoreOldCameraTransformViewFromTop()
        {
            cameraAutomap.transform.position = backupCameraPositionViewFromTop;
            cameraAutomap.transform.rotation = backupCameraRotationViewFromTop;
        }

        private void restoreOldCameraTransformView3D()
        {
            cameraAutomap.transform.position = backupCameraPositionView3D;
            cameraAutomap.transform.rotation = backupCameraRotationView3D;
        }

        private void updateAutoMapView()
        {
            if ((!cameraAutomap) || (!renderTextureAutomap))
                return;

            cameraAutomap.Render();

            RenderTexture.active = renderTextureAutomap;
            textureAutomap.ReadPixels(new Rect(0, 0, renderTextureAutomap.width, renderTextureAutomap.height), 0, 0);
            textureAutomap.Apply(false);

            panelAutomap.BackgroundTexture = textureAutomap;
        }


        #endregion

        #region Event Handlers

        private void NativePanel_OnMouseScrollUp()
        {
            Vector3 translation = cameraAutomap.transform.forward * zoomSpeed;
            cameraAutomap.transform.position += translation;
            updateAutoMapView();
        }

        private void NativePanel_OnMouseScrollDown()
        {
            Vector3 translation = -cameraAutomap.transform.forward * zoomSpeed;
            cameraAutomap.transform.position += translation;
            updateAutoMapView();
        }

        private void GridButton_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            int numberOfViewModes = Enum.GetNames(typeof(AutomapViewMode)).Length;
            automapViewMode++;
            if ((int)automapViewMode > numberOfViewModes - 1) // first mode is mode 0 -> so use numberOfViewModes-1 for comparison
                automapViewMode = 0;
            switch(automapViewMode)
            {
                case AutomapViewMode.View2D:
                    // update grid graphics
                    nativeTexture.SetPixels(78, nativeTexture.height - 171 - 19, 27, 19, pixelsGrid2D);
                    nativeTexture.Apply(false);
                    saveCameraTransformView3D();
                    restoreOldCameraTransformViewFromTop();
                    updateAutoMapView();
                    break;
                case AutomapViewMode.View3D:
                    // update grid graphics
                    nativeTexture.SetPixels(78, nativeTexture.height - 171 - 19, 27, 19, pixelsGrid3D);
                    nativeTexture.Apply(false);
                    saveCameraTransformViewFromTop();
                    restoreOldCameraTransformView3D();
                    updateAutoMapView();
                    break;
                default:
                    break;
            }
        }

        private void ForwardButton_OnMouseDown(BaseScreenComponent sender, Vector2 position)
        {
            Vector3 translation;
            switch (automapViewMode)
            {
                case AutomapViewMode.View2D:
                    translation = -cameraAutomap.transform.up * scrollForwardBackwardSpeed;
                    break;
                case AutomapViewMode.View3D:
                    translation = -cameraAutomap.transform.forward * scrollForwardBackwardSpeed;
                    translation.y = 0.0f; // comment this out for movement along camera optical axis
                    break;
                default:
                    translation = Vector3.zero;
                    break;
            }            
            cameraAutomap.transform.position += translation;
            shiftBiasFromInitialPosition(translation);
            updateAutoMapView();
        }

        private void BackwardButton_OnMouseDown(BaseScreenComponent sender, Vector2 position)
        {
            Vector3 translation;
            switch (automapViewMode)
            {
                case AutomapViewMode.View2D:
                    translation = cameraAutomap.transform.up * scrollForwardBackwardSpeed;
                    break;
                case AutomapViewMode.View3D:
                    translation = cameraAutomap.transform.forward * scrollForwardBackwardSpeed;
                    translation.y = 0.0f; // comment this out for movement along camera optical axis
                    break;
                default:
                    translation = Vector3.zero;
                    break;
            }            
            cameraAutomap.transform.position += translation;
            shiftBiasFromInitialPosition(translation);
            updateAutoMapView();
        }

        private void LeftButton_OnMouseDown(BaseScreenComponent sender, Vector2 position)
        {
            Vector3 translation = cameraAutomap.transform.right * scrollLeftRightSpeed;
            translation.y = 0.0f; // comment this out for movement perpendicular to camera optical axis and up vector
            cameraAutomap.transform.position += translation;
            shiftBiasFromInitialPosition(translation);
            updateAutoMapView();
        }

        private void RightButton_OnMouseDown(BaseScreenComponent sender, Vector2 position)
        {
            Vector3 translation = -cameraAutomap.transform.right * scrollLeftRightSpeed;
            translation.y = 0.0f; // comment this out for movement perpendicular to camera optical axis and up vector
            cameraAutomap.transform.position += translation;
            shiftBiasFromInitialPosition(translation);
            updateAutoMapView();
        }

        private void RotateLeftButton_OnMouseDown(BaseScreenComponent sender, Vector2 position)
        {
            Vector3 biasFromInitialPosition;
            switch (automapViewMode)
            {
                case AutomapViewMode.View2D:
                    biasFromInitialPosition = biasFromInitialPositionViewFromTop;
                    break;
                case AutomapViewMode.View3D:
                    biasFromInitialPosition = biasFromInitialPositionView3D;
                    break;
                default:
                    biasFromInitialPosition = Vector3.zero;
                    break;
            }
            cameraAutomap.transform.RotateAround(Camera.main.transform.position + biasFromInitialPosition, -Vector3.up, -rotateSpeed);
            //cameraAutomap.transform.RotateAround(Camera.main.transform.position, Vector3.up, -rotateSpeed);
            updateAutoMapView();
        }

        private void RotateRightButton_OnMouseDown(BaseScreenComponent sender, Vector2 position)
        {
            Vector3 biasFromInitialPosition;
            switch (automapViewMode)
            {
                case AutomapViewMode.View2D:
                    biasFromInitialPosition = biasFromInitialPositionViewFromTop;
                    break;
                case AutomapViewMode.View3D:
                    biasFromInitialPosition = biasFromInitialPositionView3D;
                    break;
                default:
                    biasFromInitialPosition = Vector3.zero;
                    break;
            }
            cameraAutomap.transform.RotateAround(Camera.main.transform.position + biasFromInitialPosition, -Vector3.up, +rotateSpeed);
            //cameraAutomap.transform.RotateAround(Camera.main.transform.position, Vector3.up, +rotateSpeed);
            updateAutoMapView();
        }

        private void UpButton_OnMouseDown(BaseScreenComponent sender, Vector2 position)
        {
            cameraAutomap.transform.position += Vector3.up * scrollUpDownSpeed;
            updateAutoMapView();
        }

        private void DownButton_OnMouseDown(BaseScreenComponent sender, Vector2 position)
        {
            cameraAutomap.transform.position += Vector3.down * scrollUpDownSpeed;
            updateAutoMapView();
        }

        private void ExitButton_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            CloseWindow();
        }

        #endregion
    }
}