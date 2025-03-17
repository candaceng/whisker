using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class WhiskerManager : MonoBehaviour {
    [Header("File Paths")]
    public string paramNameCSV = "param_name";
    public string whiskerCoordFolder = "whisker_coordinates"; 
    public string whiskingFolder = "whisking_data";

    [Header("Whisker Settings")]
    public float colliderRadius = 0.1f;
    public Material whiskerMaterial;
    public int smoothness = 5; // Bezier subdivisions

    [Header("Animation Settings")]
    public float oscillationSpeed = 1f;
    public bool smoothInterpolation = true;
    
    private Dictionary<string, Whisker> whiskers = new Dictionary<string, Whisker>();
    private float animationTimer;

    private Dictionary<int, List<Vector3[]>> rightFrameData = new Dictionary<int, List<Vector3[]>>();
    private Dictionary<int, List<Vector3[]>> leftFrameData = new Dictionary<int, List<Vector3[]>>();

    List<string> whiskerNames = new List<string>();
    
    void Start() {
        whiskerNames = LoadWhiskerNames();
        LoadAllFrames();
        foreach (var entry in rightFrameData) {
            CreateWhisker(entry.Key, entry.Value); // Right whiskers (0-29)
        }
        foreach (var entry in leftFrameData) {
            CreateWhisker(entry.Key + 30, entry.Value); // Left whiskers (30-59)
        }
    }

    void LoadAllFrames() {
        for (int frameIdx = 0; frameIdx < 30; frameIdx++) {
            LoadWhiskingData($"right_whiskers_frame_{frameIdx}", rightFrameData);
            LoadWhiskingData($"left_whiskers_frame_{frameIdx}", leftFrameData);
        }

        Debug.Log($"Loaded {rightFrameData.Count} right frames and {leftFrameData.Count} left frames.");
    }

    void LoadWhiskingData(string fileName, Dictionary<int, List<Vector3[]>> targetDict) {
        TextAsset csvData = Resources.Load<TextAsset>($"{whiskingFolder}/{fileName}");

        if (csvData == null) {
            Debug.LogError($"Failed to load {fileName}.csv");
            return;
        }
        string[] lines = csvData.text.Split('\n');
        if (lines.Length < 2) return; 

        foreach (string line in lines) {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("Frame")) continue; // Skip headers

            string[] values = line.Split(',');
            if (values.Length != 6) continue; 

            int frameIndex = int.Parse(values[0]);
            int whiskerIndex = int.Parse(values[1]);
            int pointIndex = int.Parse(values[2]);
            float x = float.Parse(values[3]);
            float y = float.Parse(values[4]);
            float z = float.Parse(values[5]);

            if (!targetDict.ContainsKey(whiskerIndex)) {
                targetDict[whiskerIndex] = new List<Vector3[]>(30); // Initialize list for 30 frames
                for (int f = 0; f < 30; f++) {
                    targetDict[whiskerIndex].Add(new Vector3[100]); // Ensure each frame has 100 points
                }
            }

            // Assign value directly to `targetDict`
            targetDict[whiskerIndex][frameIndex][pointIndex] = new Vector3(x, z, y); // Swap Y/Z for Unity's coordinate system

        }
    }

    public Vector3[] GetWhiskerPositions(int frameIndex, int whiskerIndex, bool isRight) {
        Dictionary<int, List<Vector3[]>> targetDict = isRight ? rightFrameData : leftFrameData;
        
        if (targetDict.ContainsKey(whiskerIndex) && frameIndex < targetDict[whiskerIndex].Count) {
            return targetDict[whiskerIndex][frameIndex];
        }
        
        return new Vector3[100]; // Return empty array if data is missing
    }

    List<string> LoadWhiskerNames() {
        List<string> names = new List<string>();
        TextAsset csvData = Resources.Load<TextAsset>(paramNameCSV);
        string[] lines = csvData.text.Split('\n');

        foreach (string line in lines) {
            string trimmedLine = line.Trim();
            if (!string.IsNullOrEmpty(trimmedLine)) {
                names.Add(trimmedLine);
            }
        }

        return names;
    }
    void CreateWhisker(int whiskerIndex, List<Vector3[]> frames) {
        string whiskerName = whiskerNames[whiskerIndex];

        GameObject whiskerObj = new GameObject(whiskerName);
        whiskerObj.transform.parent = transform;

        Whisker whisker = whiskerObj.AddComponent<Whisker>();
        whisker.Initialize(frames, smoothness, whiskerMaterial);

        whiskers.Add(whiskerName, whisker);
    }

    void Update() {
        animationTimer += Time.deltaTime * oscillationSpeed;
        float progress = Mathf.PingPong(animationTimer, 1f);
        
        int frameIndex = Mathf.FloorToInt(progress * 30); // 30 fps
        float blend = (progress * 30) - frameIndex;
        
        foreach(var whisker in whiskers.Values) {
            int whiskerIndex = whiskerNames.IndexOf(whisker.name);
            whisker.UpdateFrame(frameIndex, blend, whiskerIndex);
            // if(smoothInterpolation) {
            //     whisker.UpdateFrame(frameIndex, blend, whiskerIndex);
            // } else {
            //     whisker.UpdateFrame(Mathf.RoundToInt(progress * 50), 0, 0);
            // }
        }
    }

    // OLD ROTATION METHOD
    // public Vector3 GetWhiskerRotation(int whiskerIndex, int frameIndex, bool isRight) {
    //     Dictionary<int, List<Vector3>> targetDict = isRight ? rightWhiskerAngles : leftWhiskerAngles;
    //     if (targetDict.ContainsKey(whiskerIndex) && frameIndex < targetDict[whiskerIndex].Count) {
    //         return targetDict[whiskerIndex][frameIndex];
    //     }
    //     return Vector3.zero; // Default if index is out of range
    // }

    // Load whisker names from param_name.csv
    
    // List<Vector3> LoadWhiskerPoints(string framePath) {
    //     List<Vector3> points = new List<Vector3>();
    //     TextAsset csvData = Resources.Load<TextAsset>(framePath);
        
    //     if (csvData == null) {
    //         Debug.LogError($"Failed to load {framePath}");
    //         return points;
    //     }

    //     string[] lines = csvData.text.Split('\n');

    //     foreach (string line in lines) {
    //         string trimmedLine = line.Trim();
    //         if (string.IsNullOrEmpty(trimmedLine)) continue;

    //         string[] values = trimmedLine.Split(',');
    //         if (values.Length < 3) {
    //             Debug.LogWarning($"Skipping invalid line: {trimmedLine}");
    //             continue;
    //         }

    //         // Parse XYZ coordinates with coordinate system conversion
    //         float x = float.Parse(values[0]);
    //         float y = float.Parse(values[1]);
    //         float z = float.Parse(values[2]);
    //         points.Add(new Vector3(x, z, y)); // Swap Y/Z for Unity's coordinate system
    //     }

    //     return points;
    // }

    // Dictionary<string, List<Vector3[]>> LoadAllFrames(List<string> names) {
    //     Dictionary<string, List<Vector3[]>> data = new Dictionary<string, List<Vector3[]>>();
        
    //     foreach(string name in names) {
    //         List<Vector3[]> frames = new List<Vector3[]>();
            
    //         for(int i = 0; i < 51; i++) {
    //             string framePath = $"{whiskerCoordFolder}/angle_{i}/{name}";
    //             List<Vector3> points = LoadWhiskerPoints(framePath);
    //             frames.Add(points.ToArray()); // Convert to array here
    //         }
            
    //         data.Add(name, frames);
    //     }
        
    //     return data;
    // }



}