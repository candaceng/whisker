using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System;
using UnityEngine.Rendering.Universal;
using System.Globalization;

public class VideoWhiskerRecorder : MonoBehaviour {
    public Camera rightEyeCamera;
    public Camera leftEyeCamera;

    public int captureW = 256;  
    public int captureH = 256;
    public int outW     = 64;   
    public int outH     = 64;
    RenderTexture hiRT;
    Texture2D outTex;

    public WhiskerManager whiskerManager;

    private int currentFrame = 0;
    private bool isRecording = false;
    private string savePath;
    public int frameCap = 51;

    public bool IsRecording => isRecording;
    public int CurrentFrame => currentFrame;

    private List<string> whiskerNames = new List<string>();
    private Dictionary<string, List<string>> whiskerContactsPerTrial = new Dictionary<string, List<string>>();

    void LateUpdate() {

        var allWhiskers = FindObjectsOfType<Whisker>();
        
        if (!isRecording || currentFrame >= frameCap) {
            if (isRecording) {
                StopRecording();
            }
            return;
        }

        if (whiskerNames == null || whiskerNames.Count == 0) {
            Debug.LogWarning("[Recorder] Skipping frame: whiskerNames not loaded.");
            return;
        }

        // saving images from mouse pov
        if (currentFrame == 0) {
            string leftPath  = System.IO.Path.Combine(savePath, $"frame_left_0000.png");
            string rightPath = System.IO.Path.Combine(savePath, $"frame_right_0000.png");
            CaptureEyeAndSave(leftEyeCamera,  leftPath);
            CaptureEyeAndSave(rightEyeCamera, rightPath);
            currentFrame++;

            // Reset
            RenderTexture.active = null;
            rightEyeCamera.targetTexture = null;
            leftEyeCamera.targetTexture = null;

        }

        // logging whisker contact csv files
        Dictionary<string, Whisker> whiskerMap = new Dictionary<string, Whisker>();
        foreach (var whisker in allWhiskers)
            whiskerMap[whisker.name] = whisker;

        foreach (string whiskerName in whiskerNames) {
            // string line = "0,0,0,0"; 
            string line = $"0,0"; 
            if (whiskerMap.TryGetValue(whiskerName, out Whisker w) && w != null) {
                bool contact = w.HasContact();
                // Vector3 pt = contact ? w.GetLastContactPoint() : Vector3.zero;
                // line = $"{(contact ? 1 : 0)},{pt.x},{pt.y},{pt.z}";

                float s = contact ? w.SContact : 0f;
                float theta = w.thetaWDeg;
                
                line = $"{s.ToString("F4", CultureInfo.InvariantCulture)},{theta:F4}";
            }

            if (!whiskerContactsPerTrial.ContainsKey(whiskerName))
                whiskerContactsPerTrial[whiskerName] = new List<string>();
            whiskerContactsPerTrial[whiskerName].Add(line);
        }

        foreach (var w in allWhiskers)
            w.ResetContactInfo();

        currentFrame++;

    }

    public void SetOutputPath(string path) {
        savePath = path;
        Directory.CreateDirectory(savePath);
    }

    public void StartRecording() {
        whiskerContactsPerTrial.Clear();

        if (isRecording) return;

        if (hiRT == null) {
            int samples = Mathf.Max(1, QualitySettings.antiAliasing); 
            hiRT = new RenderTexture(captureW, captureH, 24, RenderTextureFormat.ARGB32) {
                antiAliasing = samples,
                filterMode   = FilterMode.Bilinear
            };
            hiRT.Create();
        }
        if (outTex == null) {
            outTex = new Texture2D(outW, outH, TextureFormat.RGB24, false);
        }

        float aspect = (float)captureW / (float)captureH;
        rightEyeCamera.aspect = aspect;
        leftEyeCamera.aspect  = aspect;

        float perEyeHorizontalFov = 140f; 
        float vfov = Camera.HorizontalToVerticalFieldOfView(perEyeHorizontalFov, aspect);
        rightEyeCamera.fieldOfView = vfov;
        leftEyeCamera.fieldOfView  = vfov;

        rightEyeCamera.allowMSAA = true;
        leftEyeCamera.allowMSAA  = true;
        
        ConfigureEye(rightEyeCamera);
        ConfigureEye(leftEyeCamera);

        foreach (var w in FindObjectsOfType<Whisker>()) {
            w.targetPixelHeight = outH; 
            if (w.name.StartsWith("R"))      w.eyeCamera = rightEyeCamera;
            else if (w.name.StartsWith("L")) w.eyeCamera = leftEyeCamera;
            else                             w.eyeCamera = rightEyeCamera; 
        }

        isRecording = true;
        currentFrame = 0; 

        if (whiskerManager != null && (whiskerNames == null || whiskerNames.Count == 0)) {
            whiskerNames = whiskerManager.whiskerNames;
            Debug.Log($"[Recorder] Loaded {whiskerNames.Count} whisker names.");
        }
    }

    private void CaptureEyeAndSave(Camera eyeCam, string filePath) {
        eyeCam.targetTexture = hiRT;
        eyeCam.Render();                      

        RenderTexture tmp = RenderTexture.GetTemporary(outW, outH, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(hiRT, tmp);            

        RenderTexture.active = tmp;
        outTex.ReadPixels(new Rect(0,0,outW,outH), 0, 0);
        outTex.Apply();

        eyeCam.targetTexture = null;
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(tmp);

        System.IO.File.WriteAllBytes(filePath, outTex.EncodeToPNG());
    }

    private void ConfigureEye(Camera cam) {
        cam.forceIntoRenderTexture = true;
        cam.stereoTargetEye = StereoTargetEyeMask.None;
        cam.enabled = false; 

        var urp = cam.GetUniversalAdditionalCameraData();
        urp.renderPostProcessing = false;
        urp.antialiasing = AntialiasingMode.None; 
        urp.SetRenderer(0);                      
        urp.cameraStack.Clear();            

        // Don't render UI from this camera
        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0) cam.cullingMask &= ~(1 << uiLayer);
    }

    public void StopRecording() {
        
        if (!isRecording) return;

        Debug.Log($"StopRecording called at currentFrame={currentFrame}, dictCount={whiskerContactsPerTrial.Count}");

        isRecording = false;

        foreach (var kvp in whiskerContactsPerTrial) {
            string whiskerName = kvp.Key;
            string whiskerFile = Path.Combine(savePath, $"{whiskerName}.csv");
            File.WriteAllLines(whiskerFile, kvp.Value);
        }

    }

}
