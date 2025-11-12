using System.Collections;
using System.Collections.Generic;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Formats.Alembic.Importer;

namespace JaimeCamachoDev.Multitool.Animation
{
    internal enum TopologyType
    {
        Undefined,
        Analysing,
        Variable,
        Fixed
    }

    internal static class AlembicToVatTool
    {
        private static readonly List<string> DirectoryList = new List<string>();
        private static readonly object CoroutineOwner = new object();

        private static bool initialized;

        private static string exportPath = "Assets/ExportVAT/";
        private static bool nameFromAlembicPlayer = true;
        private static string exportFilename = "AlembicVAT";
        private static float startTime = 0f;
        private static float endTime = 10f;
        private static float sampleRate = 24f;
        private static bool storeCenterPositionInUv3;
        private static bool fromBlender;
        private static bool unlitMesh;
        private static bool compressNormal;
        private static TopologyType topologyState = TopologyType.Undefined;
        private static int directoryIndex;
        private static float progress;

        private static AlembicStreamPlayer alembicPlayer;
        private static Shader referenceShader;
        private static Shader unlitReferenceShader;

        private static Transform meshToBake;

        private static SerializedProperty timeProp;
        private static SerializedProperty startTimeProp;
        private static SerializedProperty endTimeProp;
        private static SerializedObject alembicObject;

        private static EditorCoroutine currentBaking;
        private static bool bakingInProgress;
        private static int maxTriangleCount;
        private static int minTriangleCount;

        public static void DrawTool()
        {
            EnsureInitialized();

            GUILayout.Label("Source", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            alembicPlayer = EditorGUILayout.ObjectField(
                "Alembic player",
                alembicPlayer,
                typeof(AlembicStreamPlayer),
                true) as AlembicStreamPlayer;
            if (EditorGUI.EndChangeCheck())
            {
                HandleAlembicPlayerChanged();
            }

            GUILayout.Space(10f);
            GUILayout.Label("Export", EditorStyles.boldLabel);

            RefreshExportDirectories();
            using (new EditorGUI.DisabledScope(DirectoryList.Count == 0))
            {
                if (DirectoryList.Count == 0)
                {
                    EditorGUILayout.HelpBox(
                        "No se encontraron carpetas Resources. Crea una para exportar los assets del VAT.",
                        MessageType.Info);
                }
                else
                {
                    directoryIndex = Mathf.Clamp(directoryIndex, 0, DirectoryList.Count - 1);
                    directoryIndex = EditorGUILayout.Popup("Export path", directoryIndex, DirectoryList.ToArray());
                    exportPath = DirectoryList[directoryIndex].Replace('\u2215', '/');
                }
            }

            nameFromAlembicPlayer = EditorGUILayout.Toggle("Name from alembic player", nameFromAlembicPlayer);
            using (new EditorGUI.DisabledScope(nameFromAlembicPlayer))
            {
                exportFilename = EditorGUILayout.TextField("Export filename", exportFilename);
            }

            GUILayout.Space(2f);
            EditorGUILayout.LabelField("Final path", $"{exportPath}/{exportFilename}_xxx.xxx");

            GUILayout.Space(10f);
            GUILayout.Label("Animation info", EditorStyles.boldLabel);

            if (topologyState != TopologyType.Undefined && topologyState != TopologyType.Analysing)
            {
                startTime = EditorGUILayout.FloatField("Start time", startTime);
                endTime = EditorGUILayout.FloatField("End time", endTime);
                sampleRate = EditorGUILayout.FloatField("Sample rate", sampleRate);
                if (!float.IsFinite(sampleRate) || Mathf.Approximately(sampleRate, 0f))
                {
                    sampleRate = 60f;
                }

                storeCenterPositionInUv3 = EditorGUILayout.Toggle("Store position in UV3", storeCenterPositionInUv3);
                fromBlender = EditorGUILayout.Toggle("Exported from Blender", fromBlender);
                unlitMesh = EditorGUILayout.Toggle("Unlit mesh", unlitMesh);

                if (!unlitMesh)
                {
                    compressNormal = EditorGUILayout.Toggle("Compress normal", compressNormal);
                }

                if (unlitMesh)
                {
                    unlitReferenceShader = EditorGUILayout.ObjectField(
                        "Unlit reference shader",
                        unlitReferenceShader,
                        typeof(Shader),
                        false) as Shader;
                }
                else
                {
                    referenceShader = EditorGUILayout.ObjectField(
                        "Lit reference shader",
                        referenceShader,
                        typeof(Shader),
                        false) as Shader;
                }
            }

            switch (topologyState)
            {
                case TopologyType.Undefined:
                    GUILayout.Label("Undefined topology");
                    break;
                case TopologyType.Analysing:
                    GUILayout.Label("Analysing topology ... please wait");
                    EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(), progress, "Analysing Alembic");
                    break;
                case TopologyType.Fixed:
                    GUILayout.Label("Fixed topology (morphing mesh)");
                    break;
                case TopologyType.Variable:
                    GUILayout.Label("Variable topology (mesh sequence)");
                    break;
            }

            GUILayout.Space(10f);

            using (new EditorGUI.DisabledScope(
                       topologyState == TopologyType.Undefined || topologyState == TopologyType.Analysing || DirectoryList.Count == 0))
            {
                if (bakingInProgress)
                {
                    if (GUILayout.Button("Cancel bake"))
                    {
                        CancelBake();
                    }
                }
                else
                {
                    if (GUILayout.Button("Bake mesh"))
                    {
                        BakeMesh();
                    }
                }
            }
        }

        public static void ResetState()
        {
            CancelBake();
            alembicPlayer = null;
            topologyState = TopologyType.Undefined;
            progress = 0f;
            meshToBake = null;
            alembicObject = null;
            timeProp = null;
            startTimeProp = null;
            endTimeProp = null;
        }

        private static void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            referenceShader = Resources.Load<Shader>("VAT_SRP_Lit_Material");
            unlitReferenceShader = Resources.Load<Shader>("VAT_SRP_Unlit_Material");

            if (referenceShader == null)
            {
                referenceShader = Resources.Load<Shader>("VAT_Legacy_Material");
            }

            if (unlitReferenceShader == null)
            {
                unlitReferenceShader = Resources.Load<Shader>("VAT_Legacy_Material");
            }

            DirectoryList.Clear();
            RefreshExportDirectories();
            initialized = true;
        }

        private static void RefreshExportDirectories()
        {
            DirectoryList.Clear();
            foreach (string folder in AssetDatabase.GetSubFolders("Assets"))
            {
                RecursiveSearchResources(folder);
            }

            if (DirectoryList.Count == 0)
            {
                exportPath = "Assets";
                directoryIndex = 0;
            }
            else
            {
                directoryIndex = Mathf.Clamp(directoryIndex, 0, DirectoryList.Count - 1);
                exportPath = DirectoryList[directoryIndex].Replace('\u2215', '/');
            }
        }

        private static void RecursiveSearchResources(string folder)
        {
            if (folder.ToLower().EndsWith("/resources"))
            {
                string modifiedFolder = folder.Replace('/', '\u2215');
                if (!DirectoryList.Contains(modifiedFolder))
                {
                    DirectoryList.Add(modifiedFolder);
                }
            }

            foreach (string subFolder in AssetDatabase.GetSubFolders(folder))
            {
                RecursiveSearchResources(subFolder);
            }
        }

        private static void HandleAlembicPlayerChanged()
        {
            Debug.Log("Object to bake has changed ... updating data");
            CancelBake();

            if (alembicPlayer == null)
            {
                topologyState = TopologyType.Undefined;
                return;
            }

            if (nameFromAlembicPlayer)
            {
                exportFilename = alembicPlayer.gameObject.name;
            }

            currentBaking = EditorCoroutineUtility.StartCoroutine(UpdateFromAlembic(), CoroutineOwner);
        }

        private static SerializedObject InitAlembic()
        {
            if (alembicPlayer == null)
            {
                Debug.LogError("Alembic player!");
                return null;
            }

            alembicObject = new SerializedObject(alembicPlayer);

            timeProp = alembicObject.FindProperty("currentTime");
            startTimeProp = alembicObject.FindProperty("startTime");
            endTimeProp = alembicObject.FindProperty("endTime");

            return alembicObject;
        }

        private static void BakeMesh()
        {
            Debug.Log("Start baking mesh!");
            bakingInProgress = true;
            currentBaking = EditorCoroutineUtility.StartCoroutine(ExportFrames(), CoroutineOwner);
        }

        private static IEnumerator UpdateFromAlembic()
        {
            Debug.Log("Get time from Alembic!");
            topologyState = TopologyType.Analysing;
            progress = 0.0f;

            SerializedObject alembic = InitAlembic();
            MeshFilter meshFilter = alembicPlayer != null ? alembicPlayer.gameObject.GetComponentInChildren<MeshFilter>() : null;
            meshToBake = meshFilter != null ? meshFilter.transform.parent : null;

            if (alembic != null)
            {
                maxTriangleCount = 0;
                minTriangleCount = 10000000;

                int framesCount = Mathf.RoundToInt((endTime - startTime) * sampleRate + 0.5f);

                for (int frame = 0; frame < framesCount; frame++)
                {
                    progress = (float)frame / framesCount;

                    float timing = startTime + frame / sampleRate;
                    timeProp.floatValue = timing;
                    alembicObject.ApplyModifiedProperties();
                    yield return null;

                    int triangleCount = 0;
                    if (meshToBake != null)
                    {
                        for (int i = 0; i < meshToBake.childCount; i++)
                        {
                            MeshFilter localMeshFilter = meshToBake.GetChild(i).GetComponent<MeshFilter>();

                            if (localMeshFilter == null && meshToBake.GetChild(i).childCount > 0)
                            {
                                localMeshFilter = meshToBake.GetChild(i).GetChild(0).GetComponent<MeshFilter>();
                            }

                            if (localMeshFilter != null && localMeshFilter.sharedMesh != null)
                            {
                                triangleCount += localMeshFilter.sharedMesh.triangles.Length / 3;
                            }
                        }
                    }

                    if (triangleCount > maxTriangleCount)
                    {
                        maxTriangleCount = triangleCount;
                    }

                    if (triangleCount < minTriangleCount)
                    {
                        minTriangleCount = triangleCount;
                    }
                }

                yield return null;
                startTime = 0.0f;
                sampleRate = 1.0f / Mathf.Max(0.0001f, startTimeProp.floatValue);
                endTime = endTimeProp.floatValue;
                topologyState = (maxTriangleCount == minTriangleCount) ? TopologyType.Fixed : TopologyType.Variable;
                progress = 1f;
            }
            else
            {
                topologyState = TopologyType.Undefined;
            }

            currentBaking = null;
        }

        private static void CancelBake()
        {
            if (currentBaking != null)
            {
                Debug.Log("Cancel current baking!");
                EditorCoroutineUtility.StopCoroutine(currentBaking);
                currentBaking = null;
            }

            bakingInProgress = false;
        }

        private static Vector2 GetUV(int xPos, int yPos, int xSize, int ySize)
        {
            Vector2 uv = new Vector2
            {
                x = (0.5f + xPos) / xSize,
                y = (0.5f + yPos) / ySize
            };

            return uv;
        }

        private static Vector2Int GetCoord(int xIndex, int yIndex, int xSize, int ySize, int columnSize)
        {
            Vector2Int uv = new Vector2Int();

            int columnIndex = yIndex / ySize;
            int verticalIndex = yIndex % ySize;

            uv.x = xIndex + columnIndex * columnSize;
            uv.y = verticalIndex;

            return uv;
        }

        private static IEnumerator ExportFrames()
        {
            Mesh bakedMesh = null;
            Vector3[] vertices = null;
            Vector2[] uv = null;
            Vector3[] uv3 = null;
            Vector3[] normals = null;
            Color[] colors = null;
            int[] triangles = null;
            int verticesCount = 0;
            int trianglesIndexCount = 0;

            string finalExportPath = exportPath + "/";

            SerializedObject alembic = InitAlembic();

            if (alembic == null)
            {
                bakingInProgress = false;
                yield break;
            }

            timeProp.floatValue = startTime;
            alembicObject.ApplyModifiedProperties();
            yield return null;

            bakedMesh = new Mesh
            {
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
            };

            if (meshToBake == null || meshToBake.childCount == 0)
            {
                Debug.LogError("Alembic hierarchy is empty. Ensure the alembic has mesh children.");
                bakingInProgress = false;
                yield break;
            }

            bool hasNormal = false;
            bool hasUVs = false;
            bool hasColors = false;

            if (topologyState == TopologyType.Variable)
            {
                hasNormal = true;
                verticesCount = maxTriangleCount * 3;
                trianglesIndexCount = maxTriangleCount * 3;
            }
            else
            {
                for (int i = 0; i < meshToBake.childCount; i++)
                {
                    MeshFilter localMeshFilter = meshToBake.GetChild(i).GetComponent<MeshFilter>();

                    if (localMeshFilter == null && meshToBake.GetChild(i).childCount > 0)
                    {
                        localMeshFilter = meshToBake.GetChild(i).GetChild(0).GetComponent<MeshFilter>();
                    }

                    if (localMeshFilter != null && localMeshFilter.sharedMesh != null)
                    {
                        verticesCount += localMeshFilter.sharedMesh.vertexCount;
                        trianglesIndexCount += localMeshFilter.sharedMesh.triangles.Length;

                        hasNormal |= localMeshFilter.sharedMesh.normals.Length > 0;
                        hasColors |= localMeshFilter.sharedMesh.colors.Length > 0;
                        hasUVs |= localMeshFilter.sharedMesh.uv.Length > 0;
                    }
                }
            }

            vertices = new Vector3[verticesCount];
            uv = new Vector2[verticesCount];
            uv3 = new Vector3[verticesCount];
            normals = new Vector3[verticesCount];
            colors = new Color[verticesCount];
            triangles = new int[trianglesIndexCount];

            int currentTrianglesIndex = 0;
            int verticesOffset = 0;

            if (topologyState == TopologyType.Variable)
            {
                for (int i = 0; i < verticesCount; i++)
                {
                    triangles[i] = i;
                    vertices[i] = Vector3.zero;
                    normals[i] = Vector3.up;
                }
            }
            else
            {
                for (int i = 0; i < meshToBake.childCount; i++)
                {
                    MeshFilter localMeshFilter = meshToBake.GetChild(i).GetComponent<MeshFilter>();

                    if (localMeshFilter == null && meshToBake.GetChild(i).childCount > 0)
                    {
                        localMeshFilter = meshToBake.GetChild(i).GetChild(0).GetComponent<MeshFilter>();
                    }

                    if (localMeshFilter != null && localMeshFilter.sharedMesh != null)
                    {
                        Vector3 center = Vector3.zero;

                        for (int j = 0; j < localMeshFilter.sharedMesh.vertexCount; j++)
                        {
                            if (hasUVs)
                            {
                                uv[j + verticesOffset] = localMeshFilter.sharedMesh.uv[j];
                            }

                            if (hasColors)
                            {
                                colors[j + verticesOffset] = localMeshFilter.sharedMesh.colors[j];
                            }

                            vertices[j + verticesOffset] = localMeshFilter.sharedMesh.vertices[j];
                            center += localMeshFilter.sharedMesh.vertices[j];
                        }

                        center /= Mathf.Max(1, localMeshFilter.sharedMesh.vertexCount);

                        if (storeCenterPositionInUv3)
                        {
                            for (int j = 0; j < localMeshFilter.sharedMesh.vertexCount; j++)
                            {
                                uv3[j + verticesOffset] = center;
                            }
                        }

                        for (int j = 0; j < localMeshFilter.sharedMesh.triangles.Length; j++)
                        {
                            triangles[currentTrianglesIndex++] = localMeshFilter.sharedMesh.triangles[j] + verticesOffset;
                        }

                        verticesOffset += localMeshFilter.sharedMesh.vertexCount;
                    }
                }
            }

            bakedMesh.vertices = vertices;
            if (hasUVs)
            {
                bakedMesh.uv = uv;
            }

            if (hasNormal)
            {
                bakedMesh.normals = normals;
            }

            if (hasColors)
            {
                bakedMesh.colors = colors;
            }

            bakedMesh.triangles = triangles;
            if (storeCenterPositionInUv3)
            {
                bakedMesh.SetUVs(2, uv3);
            }

            int[] textureSize = { 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192, 16384 };
            bakedMesh.RecalculateBounds();

            int columns = -1;
            int textureHeight = -1;
            int textureWidth = -1;

            int vertexCount = vertices.Length;
            int framesCount = Mathf.RoundToInt((endTime - startTime) * sampleRate + 0.5f);
            int adjustedFramesCount = framesCount + 2;

            Debug.Log("Frames count : " + framesCount);
            Debug.Log("Vertices count : " + vertexCount);

            bool exportVAT = true;

            columns = Mathf.CeilToInt(Mathf.Sqrt((float)vertexCount / adjustedFramesCount));
            Debug.Log("Initial columns : " + columns);
            int textureHeightAdjusted = Mathf.CeilToInt((float)vertexCount / columns);
            for (int i = 0; i < textureSize.Length; i++)
            {
                if (textureHeight == -1 && textureHeightAdjusted <= textureSize[i])
                {
                    textureHeight = textureSize[i];
                }
            }
            Debug.Log("Wanted height : " + textureHeightAdjusted + " - next POW 2 : " + textureHeight);
            if (textureHeight == -1)
            {
                Debug.LogError("Alembic too big to be encoded in VAT format ... too high");
                exportVAT = false;
            }

            if (exportVAT)
            {
                columns = Mathf.CeilToInt((float)vertexCount / textureHeight);

                Debug.Log("Adjusted columns : " + columns);
                for (int i = 0; i < textureSize.Length; i++)
                {
                    if (textureWidth == -1 && (adjustedFramesCount * columns) <= textureSize[i])
                    {
                        textureWidth = textureSize[i];
                    }
                }
                Debug.Log("Wanted width : " + (adjustedFramesCount * columns) + " - next POW 2 : " + textureWidth);

                if (textureWidth == -1)
                {
                    Debug.LogError("Alembic too big to be encoded in VAT format ... too wide");
                    exportVAT = false;
                }
            }

            Debug.Log("Delete older prefabs");
            AssetDatabase.DeleteAsset(finalExportPath + exportFilename + "_position.asset");
            AssetDatabase.DeleteAsset(finalExportPath + exportFilename + "_normal.asset");
            AssetDatabase.DeleteAsset(finalExportPath + exportFilename + "_mesh.asset");
            AssetDatabase.DeleteAsset(finalExportPath + exportFilename + "_material.mat");
            AssetDatabase.DeleteAsset(finalExportPath + exportFilename + "_prefab.prefab");

            if (exportVAT)
            {
                Bounds newBounds = new Bounds();
                Vector3 minBounds = new Vector3(1e9f, 1e9f, 1e9f);
                Vector3 maxBounds = new Vector3(-1e9f, -1e9f, -1e9f);

                Vector2[] uv2 = new Vector2[verticesCount];

                Debug.Log("Texture size : " + textureWidth + " x " + textureHeight + " Vertices : " + vertexCount + " Frames : " + framesCount);

                Texture2D positionTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBAHalf, false, true)
                {
                    wrapMode = TextureWrapMode.Clamp
                };
                if (topologyState == TopologyType.Variable)
                {
                    positionTexture.filterMode = FilterMode.Point;
                }

                Texture2D normalTexture = null;

                if (!unlitMesh)
                {
                    normalTexture = new Texture2D(
                        textureWidth,
                        textureHeight,
                        compressNormal ? TextureFormat.RGBA32 : TextureFormat.RGBAHalf,
                        false,
                        true)
                    {
                        wrapMode = TextureWrapMode.Clamp
                    };

                    if (topologyState == TopologyType.Variable)
                    {
                        normalTexture.filterMode = FilterMode.Point;
                    }
                }

                for (int frame = 0; frame < framesCount; frame++)
                {
                    float timing = startTime + frame / sampleRate;
                    Debug.Log("Encoding frame " + frame + " / " + framesCount + " (" + timing + ")");
                    timeProp.floatValue = timing;
                    alembicObject.ApplyModifiedProperties();
                    yield return null;

                    if (topologyState == TopologyType.Variable)
                    {
                        MeshFilter localMeshFilter = meshToBake.GetChild(0).GetComponent<MeshFilter>();

                        if (localMeshFilter == null && meshToBake.GetChild(0).childCount > 0)
                        {
                            localMeshFilter = meshToBake.GetChild(0).GetChild(0).GetComponent<MeshFilter>();
                        }

                        if (localMeshFilter != null && localMeshFilter.sharedMesh != null && localMeshFilter.sharedMesh.subMeshCount > 0)
                        {
                            List<Vector3> localVertices = new List<Vector3>();
                            localMeshFilter.sharedMesh.GetVertices(localVertices);
                            List<Vector3> localNormals = new List<Vector3>();
                            localMeshFilter.sharedMesh.GetNormals(localNormals);
                            int[] localIndex = localMeshFilter.sharedMesh.GetTriangles(0);

                            for (int targetIndex = 0; targetIndex < maxTriangleCount * 3; targetIndex++)
                            {
                                Vector2Int coordinates = GetCoord(frame, targetIndex, textureWidth, textureHeight, adjustedFramesCount);
                                Vector2Int coordinates0 = GetCoord(0, targetIndex, textureWidth, textureHeight, adjustedFramesCount);
                                Vector2 uvCoord = GetUV(coordinates0.x, coordinates0.y, textureWidth, textureHeight);

                                uv2[targetIndex] = uvCoord;

                                Vector3 newVertexPos = Vector3.zero;
                                Vector3 newVertexNrm = Vector3.up;

                                if (targetIndex < localIndex.Length)
                                {
                                    int vtxIndex = localIndex[targetIndex];
                                    newVertexPos = localVertices[vtxIndex];
                                    newVertexNrm = localNormals[vtxIndex];

                                    if (fromBlender)
                                    {
                                        newVertexPos = localMeshFilter.transform.TransformPoint(newVertexPos);
                                        newVertexNrm = localMeshFilter.transform.TransformDirection(newVertexNrm);
                                    }
                                }

                                minBounds = Vector3.Min(minBounds, newVertexPos);
                                maxBounds = Vector3.Max(maxBounds, newVertexPos);

                                positionTexture.SetPixel(coordinates.x, coordinates.y, new Color(newVertexPos.x, newVertexPos.y, newVertexPos.z, 1.0f));
                                if (!unlitMesh && normalTexture != null)
                                {
                                    newVertexNrm = newVertexNrm.normalized;
                                    newVertexNrm = newVertexNrm * 0.5f + Vector3.one * 0.5f;
                                    normalTexture.SetPixel(coordinates.x, coordinates.y, new Color(newVertexNrm.x, newVertexNrm.y, newVertexNrm.z, 1.0f));
                                }
                            }
                        }
                    }
                    else
                    {
                        Debug.Log("Doing animated solid meshes ");
                        verticesOffset = 0;
                        for (int i = 0; i < meshToBake.childCount; i++)
                        {
                            MeshFilter localMeshFilter = meshToBake.GetChild(i).GetComponent<MeshFilter>();
                            if (localMeshFilter == null && meshToBake.GetChild(i).childCount > 0)
                            {
                                localMeshFilter = meshToBake.GetChild(i).GetChild(0).GetComponent<MeshFilter>();
                            }

                            if (localMeshFilter != null && localMeshFilter.sharedMesh != null)
                            {
                                List<Vector3> localVertices = new List<Vector3>();
                                localMeshFilter.sharedMesh.GetVertices(localVertices);
                                List<Vector3> localNormals = new List<Vector3>();
                                localMeshFilter.sharedMesh.GetNormals(localNormals);

                                for (int j = 0; j < localVertices.Count; j++)
                                {
                                    int targetIndex = j + verticesOffset;

                                    Vector2Int coordinates = GetCoord(frame, targetIndex, textureWidth, textureHeight, adjustedFramesCount);
                                    Vector2Int coordinates0 = GetCoord(0, targetIndex, textureWidth, textureHeight, adjustedFramesCount);
                                    Vector2 uvCoord = GetUV(coordinates0.x, coordinates0.y, textureWidth, textureHeight);

                                    uv2[targetIndex] = uvCoord;

                                    Vector3 newVertexPos = localVertices[j];
                                    Vector3 newVertexNrm = localNormals.Count > j ? localNormals[j] : Vector3.up;

                                    if (fromBlender)
                                    {
                                        newVertexPos = localMeshFilter.transform.TransformPoint(newVertexPos);
                                        newVertexNrm = localMeshFilter.transform.TransformDirection(newVertexNrm);
                                    }

                                    Vector3 refVertexPos = vertices[targetIndex];
                                    newVertexPos -= refVertexPos;

                                    minBounds = Vector3.Min(minBounds, newVertexPos);
                                    maxBounds = Vector3.Max(maxBounds, newVertexPos);

                                    positionTexture.SetPixel(coordinates.x, coordinates.y, new Color(newVertexPos.x, newVertexPos.y, newVertexPos.z, 1.0f));
                                    if (!unlitMesh && normalTexture != null)
                                    {
                                        newVertexNrm = newVertexNrm.normalized;
                                        newVertexNrm = newVertexNrm * 0.5f + Vector3.one * 0.5f;
                                        normalTexture.SetPixel(coordinates.x, coordinates.y, new Color(newVertexNrm.x, newVertexNrm.y, newVertexNrm.z, 1.0f));
                                    }
                                }
                                verticesOffset += localVertices.Count;
                            }
                        }
                    }
                }

                newBounds.max = maxBounds;
                newBounds.min = minBounds;
                Debug.Log("Min bounds : " + minBounds.x + " , " + minBounds.y + " , " + minBounds.z);
                Debug.Log("Max bounds : " + maxBounds.x + " , " + maxBounds.y + " , " + maxBounds.z);

                bakedMesh.bounds = newBounds;

                positionTexture.Apply();
                if (!unlitMesh && normalTexture != null)
                {
                    normalTexture.Apply();
                }

                Debug.Log("Saving positions texture asset at " + finalExportPath + exportFilename + "_position.asset");
                AssetDatabase.CreateAsset(positionTexture, finalExportPath + exportFilename + "_position.asset");
                AssetDatabase.SaveAssets();
                if (!unlitMesh && normalTexture != null)
                {
                    Debug.Log("Saving normals texture asset at " + finalExportPath + exportFilename + "_normal.asset");
                    AssetDatabase.CreateAsset(normalTexture, finalExportPath + exportFilename + "_normal.asset");
                    AssetDatabase.SaveAssets();
                }

                bakedMesh.uv2 = uv2;
            }

            Debug.Log("Saving merged mesh asset at " + finalExportPath + exportFilename + "_mesh.asset");
            AssetDatabase.CreateAsset(bakedMesh, finalExportPath + exportFilename + "_mesh.asset");
            AssetDatabase.SaveAssets();
            yield return null;

            Debug.Log("Create prefab");

            Debug.Log("Saving material asset");
            Material newMaterial = new Material(unlitMesh ? unlitReferenceShader : referenceShader)
            {
                name = exportFilename + "_material"
            };
            newMaterial.SetFloat("_Framecount", framesCount);

            Texture2D resPosTexture = Resources.Load<Texture2D>(exportFilename + "_position");
            Texture2D resNormalTexture = Resources.Load<Texture2D>(exportFilename + "_normal");
            if (resPosTexture == null)
            {
                Debug.Log("Can't load position texture " + finalExportPath + exportFilename + "_position.asset");
            }
            if (resNormalTexture == null)
            {
                Debug.Log("Can't load position texture " + finalExportPath + exportFilename + "_normal.asset");
            }

            newMaterial.SetTexture("_VAT_positions", resPosTexture);
            newMaterial.SetTexture("_VAT_normals", resNormalTexture);

            AssetDatabase.CreateAsset(newMaterial, finalExportPath + exportFilename + "_material.mat");
            AssetDatabase.SaveAssets();

            GameObject newGameObject = new GameObject(exportFilename + "_Object");

            Mesh resMesh = Resources.Load<Mesh>(exportFilename + "_mesh");
            if (resMesh == null)
            {
                Debug.Log("Unable to reload created mesh");
            }

            Material resMaterial = Resources.Load<Material>(exportFilename + "_material");
            if (resMaterial == null)
            {
                Debug.Log("Unable to reload material");
            }

            MeshFilter meshFilter = newGameObject.AddComponent<MeshFilter>();
            meshFilter.mesh = resMesh;
            MeshRenderer meshRenderer = newGameObject.AddComponent<MeshRenderer>();
            meshRenderer.material = resMaterial;

            PrefabUtility.SaveAsPrefabAsset(newGameObject, finalExportPath + exportFilename + "_prefab.prefab");

            Object.DestroyImmediate(newGameObject);

            bakingInProgress = false;
            currentBaking = null;
        }
    }

    internal sealed class AlembicToVatWindow : EditorWindow
    {
        [MenuItem("Tools/JaimeCamachoDev/Multitool/Animation/Alembic to VAT")]
        public static void ShowWindow()
        {
            AlembicToVatWindow window = GetWindow<AlembicToVatWindow>("Alembic to VAT");
            window.minSize = new Vector2(420f, 520f);
        }

        private void OnDisable()
        {
            AlembicToVatTool.ResetState();
        }

        private void OnGUI()
        {
            AlembicToVatTool.DrawTool();
        }
    }
}
