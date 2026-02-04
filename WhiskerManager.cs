using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Collections;

public class WhiskerManager : MonoBehaviour {
    [Header("Animation")]
    public bool animate = true;          
    public int restingFrame = 0;        

    [Header("File Paths")]
    public string paramNameCSV = "param_name";
    public string whiskingFolder = "whisking_data";
    public GameObject ratHeadObject;

    [Header("Whisker Settings")]
    public Material whiskerMaterial;

    [Header("Animation Settings")]
    public float oscillationSpeed = 1f;
    public bool smoothInterpolation = true;
    
    private Dictionary<string, Whisker> whiskers = new Dictionary<string, Whisker>();
    private float animationTimer;

    private Dictionary<int, List<Vector3[]>> rightFrameData = new Dictionary<int, List<Vector3[]>>();
    private Dictionary<int, List<Vector3[]>> leftFrameData = new Dictionary<int, List<Vector3[]>>();

    public List<string> whiskerNames = new List<string>();

    IEnumerator Start()
    {
        whiskerNames = LoadWhiskerNames();
        LoadAllFrames();
        RebuildWhiskers();

        if (!animate)
            SetAllWhiskersToFrame(restingFrame);

        Debug.Log($"[WhiskerManager] names={whiskerNames?.Count ?? -1}");
        Debug.Log($"[WhiskerManager] whiskersDict={whiskers?.Count ?? -1}");

        while (FindObjectsOfType<CapsuleCollider>(true).Length == 0)
            yield return null;

        int w = LayerMask.NameToLayer("Whisker");
        if (w != -1) Physics.IgnoreLayerCollision(w, w, true);
    }


    void LoadAllFrames() {
        for (int frameIdx = 0; frameIdx < 51; frameIdx++) {
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
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("Frame")) continue;

            string[] values = line.Split(',');
            if (values.Length != 6) continue; 

            int frameIndex = int.Parse(values[0]);
            int whiskerIndex = int.Parse(values[1]);
            int pointIndex = int.Parse(values[2]);
            float x = float.Parse(values[3]);
            float y = float.Parse(values[4]);
            float z = float.Parse(values[5]);

            if (!targetDict.ContainsKey(whiskerIndex)) {
                targetDict[whiskerIndex] = new List<Vector3[]>(51);
                for (int f = 0; f < 51; f++) {
                    targetDict[whiskerIndex].Add(new Vector3[100]);
                }
            }

            targetDict[whiskerIndex][frameIndex][pointIndex] = new Vector3(x, z, y);
        }
    }

    public Vector3[] GetWhiskerPositions(int frameIndex, int whiskerIndex, bool isRight) {
        Dictionary<int, List<Vector3[]>> targetDict = isRight ? rightFrameData : leftFrameData;

        if (targetDict.ContainsKey(whiskerIndex) && frameIndex < targetDict[whiskerIndex].Count) {
            return targetDict[whiskerIndex][frameIndex];
        }

        return new Vector3[100];
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

    // for behavioural analysis
    public bool TryGetWhisker(string whiskerName, out Whisker whisker) {
        return whiskers.TryGetValue(whiskerName, out whisker);
    }

    void CreateWhisker(int whiskerIndex, List<Vector3[]> frames) {
        string whiskerName = whiskerNames[whiskerIndex];

        if (ratHeadObject == null) {
            Debug.LogError("ratHeadObject is not assigned!");
            return;
        }

        GameObject whiskerObj = new GameObject(whiskerName);
    
        whiskerObj.transform.SetParent(ratHeadObject.transform, false);
        whiskerObj.transform.localPosition = Vector3.zero;
        whiskerObj.transform.localRotation = Quaternion.identity;

        Whisker whisker = whiskerObj.AddComponent<Whisker>();
        whisker.Initialize(frames, whiskerMaterial);

        whiskers.Add(whiskerName, whisker);
    }


    public void RebuildWhiskers() {
        foreach (var w in whiskers.Values) {
            if (w != null) Destroy(w.gameObject);
        }
        whiskers.Clear();

        foreach (var entry in rightFrameData) {
            CreateWhisker(entry.Key, entry.Value);
        }

        foreach (var entry in leftFrameData) {
            CreateWhisker(entry.Key + 30, entry.Value);
        }

        Debug.Log($"Rebuilt {whiskers.Count} whiskers");
    }

    public void SetAllWhiskersToFrame(int frameIdx)
    {
        foreach (var whisker in whiskers.Values) {
            
            int whiskerIndex = whiskerNames.IndexOf(whisker.name);
            whisker.UpdateFrame(frameIdx, 0, whiskerIndex);
        }
    }

    void Update() {
        if (!animate) return;   

        animationTimer += Time.deltaTime * oscillationSpeed;
        float progress = Mathf.PingPong(animationTimer, 1f);

        int frameCap = 51;  // Use first half of whisk
        int frameIndex = Mathf.FloorToInt(progress * frameCap);
        float blend = (progress * frameCap) - frameIndex;

        foreach (var whisker in whiskers.Values) {
            int whiskerIndex = whiskerNames.IndexOf(whisker.name);
            whisker.UpdateFrame(frameIndex, blend, whiskerIndex);
        }
    }
}
