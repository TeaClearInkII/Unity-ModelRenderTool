// 茶清墨刂 & DeepSeek

using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

public class ModelRenderTool : EditorWindow
{
    // 渲染参数
    private GameObject targetObject;
    private Camera targetCamera;
    private BackgroundType bgType = BackgroundType.Color;
    private Color backgroundColor = new Color(0.5f, 0.5f, 0.5f, 0f);
    private Texture2D backgroundImage;
    private float cameraDistance = 1f;
    private Vector3 cameraRotation = new Vector3(335f, 315f, 0f);
    private Vector2Int resolution = new Vector2Int(512, 512);
    private bool orthographic = false;
    private float orthoSize = 2f;
    private float cameraFOV = 60f;
    private bool renderWholeScene = false;
    private string outputFolder = "Assets/Auto_Iron";
    
    // 自定义文件名
    private string customFileName = "";

    private bool useSceneCameraPose = false;
    private float savedCameraDistance;
    private Vector3 savedCameraRotation;
    private bool savedOrthographic;
    private float savedOrthoSize;
    private float savedCameraFOV;

    private PreviewRenderer preview;
    private Texture2D previewTexture;
    private Rect previewRect;

    // 滚动条位置
    private Vector2 scrollPosition;

    private enum BackgroundType { Color, Image }

    [MenuItem("Tools/模型渲染截图工具")]
    public static void ShowWindow() => GetWindow<ModelRenderTool>("模型渲染器 @茶清墨刂");

    private void OnEnable()
    {
        LoadSettings();
        SaveToolCameraParams();
        InitPreview();
    }

    private void OnDisable()
    {
        CleanupPreview();
        SaveSettings();
    }

    private void SaveToolCameraParams()
    {
        savedCameraDistance = cameraDistance;
        savedCameraRotation = cameraRotation;
        savedOrthographic = orthographic;
        savedOrthoSize = orthoSize;
        savedCameraFOV = cameraFOV;
    }

    private void RestoreToolCameraParams()
    {
        cameraDistance = savedCameraDistance;
        cameraRotation = savedCameraRotation;
        orthographic = savedOrthographic;
        orthoSize = savedOrthoSize;
        cameraFOV = savedCameraFOV;
    }

    private void InitPreview()
    {
        preview = new PreviewRenderer();
        RefreshPreview();
    }

    private void CleanupPreview()
    {
        if (preview != null)
        {
            preview.Cleanup();
            preview = null;
        }
        if (previewTexture != null)
            DestroyImmediate(previewTexture);
    }

    private void RefreshPreview()
    {
        if (preview == null) return;

        float targetAspect = (float)resolution.x / resolution.y;
        int previewWidth = (int)previewRect.width;
        int previewHeight = (int)previewRect.height;
        if (previewWidth > 0 && previewHeight > 0)
        {
            float rectAspect = (float)previewWidth / previewHeight;
            int renderWidth, renderHeight;
            if (rectAspect > targetAspect)
            {
                renderHeight = previewHeight;
                renderWidth = Mathf.RoundToInt(renderHeight * targetAspect);
            }
            else
            {
                renderWidth = previewWidth;
                renderHeight = Mathf.RoundToInt(renderWidth / targetAspect);
            }
            preview.SetResolution(renderWidth, renderHeight);
        }
        else
        {
            preview.SetResolution(256, 256);
        }

        bool ignore = renderWholeScene;

        if (useSceneCameraPose)
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null && sceneView.camera != null)
            {
                Camera cam = sceneView.camera;
                preview.SetSceneCamera(cam.transform.position, cam.transform.rotation,
                    cam.orthographic, cam.orthographicSize, cam.fieldOfView);
                preview.SetTarget(ignore ? null : targetObject);
            }
            else
            {
                UseToolCameraParams();
            }
        }
        else if (targetCamera != null)
        {
            preview.SetSceneCamera(targetCamera.transform.position, targetCamera.transform.rotation,
                targetCamera.orthographic, targetCamera.orthographicSize, targetCamera.fieldOfView);
            preview.SetTarget(ignore ? null : targetObject);
        }
        else
        {
            preview.SetTarget(ignore ? null : targetObject);
            preview.SetCamera(cameraDistance, cameraRotation, orthographic, orthoSize, cameraFOV);
        }

        preview.SetBackground(bgType, backgroundColor, backgroundImage);
        Texture2D tex = preview.Render();
        if (tex != null)
        {
            if (previewTexture != null) DestroyImmediate(previewTexture);
            previewTexture = tex;
        }
        else
        {
            if (previewTexture == null)
                previewTexture = new Texture2D(1, 1);
        }
        Repaint();
    }

    private void UseToolCameraParams()
    {
        // 备用逻辑，已在 RefreshPreview 中处理
    }

    private void OnGUI()
    {
        // 添加纵向滚动视图，确保内容过多时可以被滚动查看
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUI.BeginChangeCheck();

        EditorGUILayout.HelpBox(
            "场景中的物体（灯光、特效等）会影响渲染结果，建议在生成前隐藏不需要的物体。",
            MessageType.Warning);
        
        EditorGUILayout.Space();
        
        EditorGUILayout.LabelField("模型设置", EditorStyles.boldLabel);
        targetObject = (GameObject)EditorGUILayout.ObjectField("目标物体", targetObject, typeof(GameObject), true);
        
        renderWholeScene = EditorGUILayout.ToggleLeft("渲染整个场景（忽略目标物体）", renderWholeScene);
        
        if (renderWholeScene)
            EditorGUILayout.HelpBox("开启后，将渲染当前场景中的所有可见物体，与目标物体无关。", MessageType.Info);
        else
            EditorGUILayout.HelpBox("支持预制体或场景中的GameObject。留空时只渲染背景。", MessageType.Info);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("自定义相机对象", EditorStyles.boldLabel);
        targetCamera = (Camera)EditorGUILayout.ObjectField("目标相机（可选）", targetCamera, typeof(Camera), true);
        if (targetCamera != null)
            EditorGUILayout.HelpBox("如果指定了相机，将直接使用该相机的Transform和投影参数，忽略下方的相机控制。", MessageType.Info);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("相机控制", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        bool newUseScenePose = GUILayout.Toggle(useSceneCameraPose, " 使用当前Scene视图相机参数", GUILayout.Width(220));
        if (newUseScenePose != useSceneCameraPose)
        {
            if (newUseScenePose)
                SaveToolCameraParams();
            else
                RestoreToolCameraParams();
            useSceneCameraPose = newUseScenePose;
            RefreshPreview();
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        
        if (useSceneCameraPose)
        {
            EditorGUILayout.HelpBox("开启后，直接使用Scene视图相机的位置、旋转和投影参数。渲染内容仍由“渲染整个场景”复选框控制。", MessageType.Info);
        }
        
        EditorGUILayout.Space();
        
        EditorGUILayout.LabelField("工具相机参数", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledGroupScope(useSceneCameraPose || targetCamera != null))
        {
            EditorGUILayout.BeginHorizontal();
            cameraDistance = EditorGUILayout.FloatField("距离", cameraDistance);
            if (GUILayout.Button("重置", GUILayout.Width(50))) cameraDistance = 1f;
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            cameraRotation = EditorGUILayout.Vector3Field("旋转 (欧拉角)", cameraRotation);
            if (GUILayout.Button("重置", GUILayout.Width(50))) cameraRotation = new Vector3(335f, 315f, 0f);
            EditorGUILayout.EndHorizontal();
            
            orthographic = EditorGUILayout.Toggle("正交", orthographic);
            if (orthographic)
            {
                orthoSize = EditorGUILayout.FloatField("正交尺寸", orthoSize);
                EditorGUILayout.HelpBox("正交模式下，相机距离仅影响相机位置，画面大小由正交尺寸决定。", MessageType.None);
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                cameraFOV = EditorGUILayout.Slider("视野 (FOV)", cameraFOV, 10f, 120f);
                if (GUILayout.Button("重置", GUILayout.Width(50))) cameraFOV = 60f;
                EditorGUILayout.EndHorizontal();
            }
            
            if (GUILayout.Button("从Scene视图复制相机参数", GUILayout.Height(24)))
            {
                CopyFromSceneCamera();
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("背景设置", EditorStyles.boldLabel);
        bgType = (BackgroundType)EditorGUILayout.EnumPopup("背景类型", bgType);
        if (bgType == BackgroundType.Color)
        {
            backgroundColor = EditorGUILayout.ColorField("背景颜色", backgroundColor);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("预设:", GUILayout.Width(40));
            if (GUILayout.Button("白")) backgroundColor = Color.white;
            if (GUILayout.Button("灰")) backgroundColor = Color.gray;
            if (GUILayout.Button("黑")) backgroundColor = Color.black;
            if (GUILayout.Button("红")) backgroundColor = Color.red;
            if (GUILayout.Button("绿")) backgroundColor = Color.green;
            if (GUILayout.Button("蓝")) backgroundColor = Color.blue;
            if (GUILayout.Button("透明")) backgroundColor = Color.clear;
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            backgroundImage = (Texture2D)EditorGUILayout.ObjectField("背景图片", backgroundImage, typeof(Texture2D), false);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("输出设置", EditorStyles.boldLabel);
        resolution = EditorGUILayout.Vector2IntField("分辨率", resolution);
        outputFolder = EditorGUILayout.TextField("输出文件夹", outputFolder);
        if (GUILayout.Button("选择文件夹"))
        {
            string selected = EditorUtility.OpenFolderPanel("选择输出文件夹", Application.dataPath, "");
            if (!string.IsNullOrEmpty(selected))
            {
                if (selected.StartsWith(Application.dataPath))
                    outputFolder = "Assets" + selected.Substring(Application.dataPath.Length);
                else
                    EditorUtility.DisplayDialog("错误", "请在 Assets 目录下选择", "确定");
            }
        }

        // 文件名编辑区域
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("保存文件名");
        string autoName = GetAutoFileName();
        customFileName = EditorGUILayout.TextField(customFileName);
        if (GUILayout.Button("重置", GUILayout.Width(50)))
            customFileName = "";
        EditorGUILayout.EndHorizontal();
        if (string.IsNullOrEmpty(customFileName))
            EditorGUILayout.LabelField("将自动生成:", autoName + ".png");
        else
            EditorGUILayout.LabelField("将保存为:", customFileName + ".png");

        string fileName = GetOutputFileName();
        EditorGUILayout.LabelField("最终文件名", fileName + ".png");

        EditorGUILayout.Space();
        if (GUILayout.Button("保存图片", GUILayout.Height(30)))
            SaveImage();

        // 预览区域
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("实时预览", EditorStyles.boldLabel);
        Rect newPreviewRect = GUILayoutUtility.GetRect(256, 256, GUILayout.ExpandWidth(true), GUILayout.Height(256));
        bool rectChanged = (newPreviewRect.width != previewRect.width || newPreviewRect.height != previewRect.height);
        previewRect = newPreviewRect;
        
        if (rectChanged && previewRect.width > 0 && previewRect.height > 0)
            RefreshPreview();
        else if (EditorGUI.EndChangeCheck())
            RefreshPreview();

        if (previewTexture != null)
        {
            float texAspect = (float)previewTexture.width / previewTexture.height;
            float rectAspect = previewRect.width / previewRect.height;
            Rect drawRect = previewRect;
            if (texAspect > rectAspect)
            {
                float drawHeight = previewRect.width / texAspect;
                drawRect.y += (previewRect.height - drawHeight) * 0.5f;
                drawRect.height = drawHeight;
            }
            else
            {
                float drawWidth = previewRect.height * texAspect;
                drawRect.x += (previewRect.width - drawWidth) * 0.5f;
                drawRect.width = drawWidth;
            }
            GUI.DrawTexture(drawRect, previewTexture, ScaleMode.StretchToFill, false);
        }
        else
        {
            EditorGUI.DrawRect(previewRect, Color.gray);
            GUI.Label(previewRect, "预览生成中...", EditorStyles.centeredGreyMiniLabel);
        }

        EditorGUILayout.EndScrollView();
    }

    private void CopyFromSceneCamera()
    {
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView != null && sceneView.camera != null)
        {
            Camera cam = sceneView.camera;
            Vector3 camPos = cam.transform.position;
            cameraDistance = Vector3.Distance(camPos, Vector3.zero);
            Vector3 directionToOrigin = (Vector3.zero - camPos).normalized;
            Quaternion lookRot = Quaternion.LookRotation(directionToOrigin);
            cameraRotation = lookRot.eulerAngles;
            orthographic = cam.orthographic;
            orthoSize = cam.orthographicSize;
            cameraFOV = cam.fieldOfView;
            RefreshPreview();
            EditorUtility.DisplayDialog("完成", "已将Scene视图相机参数复制到工具相机。", "确定");
        }
        else
        {
            EditorUtility.DisplayDialog("错误", "未找到有效的Scene视图相机。", "确定");
        }
    }

    private string GetAutoFileName()
    {
        if (!renderWholeScene && targetObject != null)
            return targetObject.name;

        string fullPath = Path.Combine(Application.dataPath, outputFolder.Substring("Assets/".Length));
        if (!Directory.Exists(fullPath))
            return "Auto_Iron_1";

        var existing = Directory.GetFiles(fullPath, "Auto_Iron_*.png")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .Where(n => n.StartsWith("Auto_Iron_"))
            .Select(n => int.TryParse(n.Substring("Auto_Iron_".Length), out int num) ? num : 0)
            .ToList();
        int next = existing.Count == 0 ? 1 : existing.Max() + 1;
        return $"Auto_Iron_{next}";
    }

    private string GetOutputFileName()
    {
        string trimmed = customFileName.Trim();
        if (!string.IsNullOrEmpty(trimmed))
            return trimmed;
        return GetAutoFileName();
    }

    private string GetUniqueFileName(string baseNameWithoutExtension, string folderPath)
    {
        string candidate = baseNameWithoutExtension;
        int counter = 1;
        while (File.Exists(Path.Combine(folderPath, candidate + ".png")))
        {
            candidate = $"{baseNameWithoutExtension}_{counter}";
            counter++;
        }
        return candidate;
    }

    private void SaveImage()
    {
        if (preview == null) return;

        preview.SetResolution(resolution.x, resolution.y);
        bool ignore = renderWholeScene;

        if (useSceneCameraPose)
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null && sceneView.camera != null)
            {
                Camera cam = sceneView.camera;
                preview.SetSceneCamera(cam.transform.position, cam.transform.rotation,
                    cam.orthographic, cam.orthographicSize, cam.fieldOfView);
                preview.SetTarget(ignore ? null : targetObject);
            }
            else
            {
                EditorUtility.DisplayDialog("错误", "未找到有效的Scene视图相机。", "确定");
                return;
            }
        }
        else if (targetCamera != null)
        {
            preview.SetSceneCamera(targetCamera.transform.position, targetCamera.transform.rotation,
                targetCamera.orthographic, targetCamera.orthographicSize, targetCamera.fieldOfView);
            preview.SetTarget(ignore ? null : targetObject);
        }
        else
        {
            preview.SetTarget(ignore ? null : targetObject);
            preview.SetCamera(cameraDistance, cameraRotation, orthographic, orthoSize, cameraFOV);
        }

        preview.SetBackground(bgType, backgroundColor, backgroundImage);
        Texture2D finalTex = preview.Render();
        if (finalTex == null)
        {
            EditorUtility.DisplayDialog("错误", "渲染失败，请检查参数", "确定");
            return;
        }

        if (!Directory.Exists(outputFolder))
        {
            Directory.CreateDirectory(outputFolder);
            AssetDatabase.Refresh();
        }

        string folderFullPath = Path.Combine(Application.dataPath, outputFolder.Substring("Assets/".Length));
        string baseName = GetOutputFileName();
        string targetPath = Path.Combine(folderFullPath, baseName + ".png");

        // 检查是否存在同名文件
        if (File.Exists(targetPath))
        {
            int choice = EditorUtility.DisplayDialogComplex(
                "文件已存在",
                $"文件 '{baseName}.png' 已经存在，要如何处理？",
                "覆盖",
                "取消",
                "自动重命名 (加 _1)");

            switch (choice)
            {
                case 0: // 覆盖
                    break;
                case 1: // 取消
                    DestroyImmediate(finalTex);
                    return;
                case 2: // 自动重命名
                    baseName = GetUniqueFileName(baseName, folderFullPath);
                    targetPath = Path.Combine(folderFullPath, baseName + ".png");
                    break;
            }
        }

        File.WriteAllBytes(targetPath, finalTex.EncodeToPNG());
        DestroyImmediate(finalTex);

        AssetDatabase.Refresh();
        string relativePath = "Assets" + targetPath.Substring(Application.dataPath.Length);
        EditorUtility.DisplayDialog("完成", $"图片已保存：\n{relativePath}", "确定");
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Texture2D>(relativePath));
    }

    private void SaveSettings()
    {
        EditorPrefs.SetInt("ModelRenderTool_BgType", (int)bgType);
        EditorPrefs.SetString("ModelRenderTool_BgColor", ColorUtility.ToHtmlStringRGBA(backgroundColor));
        EditorPrefs.SetString("ModelRenderTool_CameraRot", $"{cameraRotation.x},{cameraRotation.y},{cameraRotation.z}");
        EditorPrefs.SetFloat("ModelRenderTool_CameraDist", cameraDistance);
        EditorPrefs.SetInt("ModelRenderTool_ResW", resolution.x);
        EditorPrefs.SetInt("ModelRenderTool_ResH", resolution.y);
        EditorPrefs.SetBool("ModelRenderTool_Ortho", orthographic);
        EditorPrefs.SetFloat("ModelRenderTool_OrthoSize", orthoSize);
        EditorPrefs.SetFloat("ModelRenderTool_CameraFOV", cameraFOV);
        EditorPrefs.SetString("ModelRenderTool_OutputFolder", outputFolder);
        EditorPrefs.SetBool("ModelRenderTool_UseScenePose", useSceneCameraPose);
        EditorPrefs.SetBool("ModelRenderTool_RenderWholeScene", renderWholeScene);
        EditorPrefs.SetString("ModelRenderTool_TargetCamera", targetCamera != null ? targetCamera.name : "");
        EditorPrefs.SetFloat("ModelRenderTool_SavedDist", savedCameraDistance);
        EditorPrefs.SetString("ModelRenderTool_SavedRot", $"{savedCameraRotation.x},{savedCameraRotation.y},{savedCameraRotation.z}");
        EditorPrefs.SetBool("ModelRenderTool_SavedOrtho", savedOrthographic);
        EditorPrefs.SetFloat("ModelRenderTool_SavedOrthoSize", savedOrthoSize);
        EditorPrefs.SetFloat("ModelRenderTool_SavedFOV", savedCameraFOV);
        EditorPrefs.SetString("ModelRenderTool_CustomFileName", customFileName);
        string imgPath = backgroundImage != null ? AssetDatabase.GetAssetPath(backgroundImage) : "";
        EditorPrefs.SetString("ModelRenderTool_BgImage", imgPath);
    }

    private void LoadSettings()
    {
        bgType = (BackgroundType)EditorPrefs.GetInt("ModelRenderTool_BgType", 0);
        string colorStr = EditorPrefs.GetString("ModelRenderTool_BgColor", "80808000");
        if (ColorUtility.TryParseHtmlString("#" + colorStr, out Color col))
            backgroundColor = col;
        else
            backgroundColor = new Color(0.5f, 0.5f, 0.5f, 0f);

        string rotStr = EditorPrefs.GetString("ModelRenderTool_CameraRot", "335,315,0");
        string[] rotParts = rotStr.Split(',');
        if (rotParts.Length == 3)
        {
            float.TryParse(rotParts[0], out cameraRotation.x);
            float.TryParse(rotParts[1], out cameraRotation.y);
            float.TryParse(rotParts[2], out cameraRotation.z);
        }

        cameraDistance = EditorPrefs.GetFloat("ModelRenderTool_CameraDist", 1f);
        resolution.x = EditorPrefs.GetInt("ModelRenderTool_ResW", 512);
        resolution.y = EditorPrefs.GetInt("ModelRenderTool_ResH", 512);
        orthographic = EditorPrefs.GetBool("ModelRenderTool_Ortho", false);
        orthoSize = EditorPrefs.GetFloat("ModelRenderTool_OrthoSize", 2f);
        cameraFOV = EditorPrefs.GetFloat("ModelRenderTool_CameraFOV", 60f);
        outputFolder = EditorPrefs.GetString("ModelRenderTool_OutputFolder", "Assets/Auto_Iron");
        useSceneCameraPose = EditorPrefs.GetBool("ModelRenderTool_UseScenePose", false);
        renderWholeScene = EditorPrefs.GetBool("ModelRenderTool_RenderWholeScene", false);
        customFileName = EditorPrefs.GetString("ModelRenderTool_CustomFileName", "");
        string targetCamName = EditorPrefs.GetString("ModelRenderTool_TargetCamera", "");
        if (!string.IsNullOrEmpty(targetCamName))
        {
            GameObject go = GameObject.Find(targetCamName);
            if (go != null) targetCamera = go.GetComponent<Camera>();
        }
        
        savedCameraDistance = EditorPrefs.GetFloat("ModelRenderTool_SavedDist", cameraDistance);
        string savedRotStr = EditorPrefs.GetString("ModelRenderTool_SavedRot", "335,315,0");
        string[] savedRotParts = savedRotStr.Split(',');
        if (savedRotParts.Length == 3)
        {
            float.TryParse(savedRotParts[0], out savedCameraRotation.x);
            float.TryParse(savedRotParts[1], out savedCameraRotation.y);
            float.TryParse(savedRotParts[2], out savedCameraRotation.z);
        }
        savedOrthographic = EditorPrefs.GetBool("ModelRenderTool_SavedOrtho", orthographic);
        savedOrthoSize = EditorPrefs.GetFloat("ModelRenderTool_SavedOrthoSize", orthoSize);
        savedCameraFOV = EditorPrefs.GetFloat("ModelRenderTool_SavedFOV", cameraFOV);

        string imgAssetPath = EditorPrefs.GetString("ModelRenderTool_BgImage", "");
        if (!string.IsNullOrEmpty(imgAssetPath))
            backgroundImage = AssetDatabase.LoadAssetAtPath<Texture2D>(imgAssetPath);
    }

    // ================= 内部预览渲染器（已移除新建场景功能） =================
    private class PreviewRenderer
    {
        private Camera renderCamera;
        private GameObject cameraGO;
        private GameObject lightGO;
        private GameObject instanceGO;
        private GameObject backgroundQuad;
        private Material bgMaterial;
        private RenderTexture rt;
        private int lastWidth, lastHeight;

        private bool useExternalCamera;
        private Vector3 extCamPos;
        private Quaternion extCamRot;
        private bool extCamOrtho;
        private float extCamOrthoSize;
        private float extCamFOV;

        private GameObject targetSource;
        private float distance;
        private Vector3 rotation;
        private bool ortho;
        private float orthoSize;
        private float fov;

        private BackgroundType bgType;
        private Color bgColor;
        private Texture2D bgImage;

        public void SetTarget(GameObject obj)
        {
            targetSource = obj;
        }
        public void SetBackground(BackgroundType type, Color color, Texture2D image)
        {
            bgType = type; bgColor = color; bgImage = image;
        }
        public void SetCamera(float dist, Vector3 rot, bool orth, float size, float fovVal)
        {
            useExternalCamera = false;
            distance = dist; rotation = rot; ortho = orth; orthoSize = size; fov = fovVal;
        }
        public void SetSceneCamera(Vector3 pos, Quaternion rot, bool orth, float orthSize, float fovVal)
        {
            useExternalCamera = true;
            extCamPos = pos; extCamRot = rot; extCamOrtho = orth;
            extCamOrthoSize = orthSize; extCamFOV = fovVal;
        }
        public void SetResolution(int width, int height) { lastWidth = width; lastHeight = height; }

        public Texture2D Render()
        {
            if (lastWidth <= 0 || lastHeight <= 0) return null;

            CreateResources();
            UpdateScene();

            if (renderCamera == null) return null;

            if (rt != null && (rt.width != lastWidth || rt.height != lastHeight))
            {
                rt.Release();
                Object.DestroyImmediate(rt);
                rt = null;
            }
            if (rt == null)
                rt = new RenderTexture(lastWidth, lastHeight, 24, RenderTextureFormat.ARGB32);

            renderCamera.targetTexture = rt;
            renderCamera.Render();

            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(lastWidth, lastHeight, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, lastWidth, lastHeight), 0, 0);
            tex.Apply();
            RenderTexture.active = null;

            return tex;
        }

        private void CreateResources()
        {
            if (cameraGO == null)
            {
                cameraGO = new GameObject("PreviewCamera");
                cameraGO.hideFlags = HideFlags.HideAndDontSave;
                renderCamera = cameraGO.AddComponent<Camera>();
                renderCamera.enabled = false;
                renderCamera.clearFlags = CameraClearFlags.SolidColor;
                renderCamera.nearClipPlane = 0.01f;
                renderCamera.farClipPlane = 100f;
            }

            if (lightGO == null)
            {
                lightGO = new GameObject("PreviewLights");
                lightGO.hideFlags = HideFlags.HideAndDontSave;
                Light mainLight = lightGO.AddComponent<Light>();
                mainLight.type = LightType.Directional;
                mainLight.transform.rotation = Quaternion.Euler(50, -30, 0);
                mainLight.intensity = 1f;
                GameObject fillGO = new GameObject("FillLight");
                fillGO.transform.parent = lightGO.transform;
                Light fillLight = fillGO.AddComponent<Light>();
                fillLight.type = LightType.Directional;
                fillLight.transform.rotation = Quaternion.Euler(-30, 45, 0);
                fillLight.intensity = 0.5f;
                fillLight.color = new Color(0.7f, 0.7f, 0.8f);
            }

            if (bgMaterial == null)
            {
                Shader shader = Shader.Find("Unlit/Texture");
                bgMaterial = new Material(shader);
                bgMaterial.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        private void UpdateScene()
        {
            if (renderCamera == null) return;

            if (instanceGO != null) Object.DestroyImmediate(instanceGO);
            if (backgroundQuad != null) Object.DestroyImmediate(backgroundQuad);

            if (targetSource != null)
            {
                bool isPrefab = PrefabUtility.GetPrefabAssetType(targetSource) != PrefabAssetType.NotAPrefab;
                if (isPrefab)
                    instanceGO = (GameObject)PrefabUtility.InstantiatePrefab(targetSource);
                else
                    instanceGO = Object.Instantiate(targetSource);
                if (instanceGO != null)
                {
                    instanceGO.hideFlags = HideFlags.HideAndDontSave;
                    instanceGO.transform.position = Vector3.zero;
                    instanceGO.transform.rotation = Quaternion.identity;
                }
            }

            if (useExternalCamera)
            {
                renderCamera.transform.position = extCamPos;
                renderCamera.transform.rotation = extCamRot;
                renderCamera.orthographic = extCamOrtho;
                if (extCamOrtho)
                    renderCamera.orthographicSize = extCamOrthoSize;
                else
                    renderCamera.fieldOfView = extCamFOV;
            }
            else
            {
                Vector3 center = Vector3.zero;
                if (instanceGO != null)
                    center = GetBounds(instanceGO).center;

                renderCamera.orthographic = ortho;
                if (ortho)
                    renderCamera.orthographicSize = orthoSize;
                else
                    renderCamera.fieldOfView = fov;

                Vector3 direction = Quaternion.Euler(rotation) * Vector3.back;
                Vector3 pos = center - direction.normalized * distance;
                renderCamera.transform.position = pos;
                renderCamera.transform.LookAt(center);
            }

            if (bgType == BackgroundType.Color)
            {
                renderCamera.backgroundColor = bgColor;
            }
            else
            {
                renderCamera.backgroundColor = Color.black;
                if (bgImage != null)
                {
                    backgroundQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    backgroundQuad.hideFlags = HideFlags.HideAndDontSave;
                    bgMaterial.mainTexture = bgImage;
                    MeshRenderer bgRenderer = backgroundQuad.GetComponent<MeshRenderer>();
                    bgRenderer.sharedMaterial = bgMaterial;
                    bgRenderer.sharedMaterial.renderQueue = 1000;

                    float bgDistance = 5f;
                    float camHeight, camWidth;
                    if (renderCamera.orthographic)
                    {
                        camHeight = 2f * renderCamera.orthographicSize;
                        camWidth = camHeight * renderCamera.aspect;
                    }
                    else
                    {
                        camHeight = 2f * bgDistance * Mathf.Tan(renderCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
                        camWidth = camHeight * renderCamera.aspect;
                    }

                    backgroundQuad.transform.position = renderCamera.transform.position + renderCamera.transform.forward * bgDistance;
                    backgroundQuad.transform.rotation = Quaternion.LookRotation(renderCamera.transform.forward);
                    backgroundQuad.transform.localScale = new Vector3(camWidth, camHeight, 1f);
                }
            }
        }

        private Bounds GetBounds(GameObject obj)
        {
            Bounds bounds = new Bounds(obj.transform.position, Vector3.zero);
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);
            }
            return bounds;
        }

        public void Cleanup()
        {
            if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
            if (cameraGO != null) Object.DestroyImmediate(cameraGO);
            if (lightGO != null) Object.DestroyImmediate(lightGO);
            if (instanceGO != null) Object.DestroyImmediate(instanceGO);
            if (backgroundQuad != null) Object.DestroyImmediate(backgroundQuad);
            if (bgMaterial != null) Object.DestroyImmediate(bgMaterial);
        }
    }
}
