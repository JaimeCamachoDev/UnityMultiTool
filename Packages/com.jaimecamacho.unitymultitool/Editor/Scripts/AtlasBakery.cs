#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEngine;

[ExecuteAlways]
public class AtlasBakery : MonoBehaviour
{
    List<Material> skinnedMeshMaterials = new List<Material>();
    List<int> skinnedMeshMaterialsMultiplier = new List<int>();
    //Private constants
    private const int MAX_VERTICES_FOR_16BITS_MESH = 50000; //NOT change this

    //Private variables
    private Vector3 thisOriginalPosition = Vector3.zero;
    private Vector3 thisOriginalRotation = Vector3.zero;
    private Vector3 thisOriginalScale = Vector3.one;

    //Enums of script
    public enum LogTypeOf
    {
        Assert,
        Error,
        Exception,
        Log,
        Warning
    }
    public enum MergeMethod
    {
        OneMeshPerMaterial,
        AllInOne,
        JustMaterialColors,
        OnlyAnima2dMeshes
    }
    public enum AtlasSize
    {
        Pixels32x32,
        Pixels64x64,
        Pixels128x128,
        Pixels256x256,
        Pixels512x512,
        Pixels1024x1024,
        Pixels2048x2048,
        Pixels4096x4096,
        Pixels8192x8192
    }    
    public enum MergeTiledTextures
    {
        SkipAll,
        ImprovedMode,
        LegacyMode
    }

    //Classes of script
    [Serializable]
    public class LogOfMerge
    {
        public string content;
        public LogTypeOf logType;

        public LogOfMerge(string content, LogTypeOf logType)
        {
            this.content = content;
            this.logType = logType;
        }
    }
    public enum MipMapEdgesSize
    {
        Pixels0x0,
        Pixels16x16,
        Pixels32x32,
        Pixels64x64,
        Pixels128x128,
        Pixels256x256,
        Pixels512x512,
        Pixels1024x1024,
    }
    public enum AtlasPadding
    {
        Pixels0x0,
        Pixels2x2,
        Pixels4x4,
        Pixels8x8,
        Pixels16x16,
    }
    public string saveFolder = "Assets";
    [Serializable]
    public class AllInOneParams
    {
        public Material materialToUse;
        public AtlasSize atlasResolution = AtlasSize.Pixels2048x2048;
        [HideInInspector] public MipMapEdgesSize mipMapEdgesSize = MipMapEdgesSize.Pixels0x0;
        [HideInInspector] public AtlasPadding atlasPadding = AtlasPadding.Pixels0x0;
        [HideInInspector] public MergeTiledTextures mergeTiledTextures = MergeTiledTextures.LegacyMode;
        [HideInInspector] public bool mergeOnlyEqualsRootBones = false;
        public bool useColorAlpha = true;
        public string ColorAlphaPropertyToFind = "_MainTex";
        public string ColorAlphaPropertyToInsert = "_MainTex";
        public bool RMAESupport = true;
        public string RMAEPropertyToFind = "_MAS";
        public string RMAEPropertyToInsert = "_MAS";
        public bool NormalMapSupport = true;
        public string NormalMapPropertyToFind = "_Normal";
        public string NormalMapPropertyToInsert = "_Normal";
        public bool aux1 = false;
        public string aux1PropertyToFind = "_Aux1";
        public string aux1PropertyToInsert = "_Aux1";
        public bool aux2Support = false;
        public string aux2PropertyFind = "_Aux2";
        public string aux2PropertyToInsert = "_Aux2";
        public bool aux3Support = false;
        public string aux3PropertyToFind = "_Aux3";
        public string aux3PropertyToInsert = "_Aux3";
        public bool aux4Support = false;
        public string aux4PropertyToFind = "_Aux4";
        public string aux4PropertyToInsert = "_Aux4";
        public bool aux5MapSupport = false;
        public string aux5PropertyToFind = "_Aux5";
        public string aux5PropertyToInsert = "_Aux5";
        public bool aux6Support = false;
        public string aux6PropertyToFind = "_Aux6";
        public string aux6PropertyToInsert = "_Aux6";
        [HideInInspector]public bool pinkNormalMapsFix = true;
    }

    //Important private variables from Script (Filled after a merge been done)
    ///<summary>[WARNING] Do not change the value of this variable. This is a variable used for internal tool operations.</summary> 
    [HideInInspector]
    public GameObject[] resultMergeOriginalGameObjects = null;
    ///<summary>[WARNING] Do not change the value of this variable. This is a variable used for internal tool operations.</summary> 
    [HideInInspector]
    public GameObject resultMergeGameObject = null;
    ///<summary>[WARNING] Do not change the value of this variable. This is a variable used for internal tool operations.</summary> 
    [HideInInspector]
    public string resultMergeTextStats = "";
    ///<summary>[WARNING] Do not change the value of this variable. This is a variable used for internal tool operations.</summary> 
    [HideInInspector]
    public List<LogOfMerge> resultMergeLogs = new List<LogOfMerge>();
    ///<summary>[WARNING] Do not change the value of this variable. This is a variable used for internal tool operations.</summary> 
    [HideInInspector]
    public List<string> resultMergeAssetsSaved = new List<string>();

    //Variables of Script (Merge)
    [HideInInspector]
    public MergeMethod mergeMethod;
    //[HideInInspector]
    public AllInOneParams allInOneParams = new AllInOneParams();
    [HideInInspector]
    public bool autoManagePosition = true;
    [HideInInspector]
    public bool compatibilityMode = true;
    [HideInInspector]
    public string nameOfThisMerge = "Combined Meshes";

    //Variables of Script (GameObject to ignore)
    [HideInInspector]
    public List<GameObject> gameObjectsToIgnore = new List<GameObject>();

    //Variables of script (Combine in editor)
    [HideInInspector]
    public bool saveDataInAssets = true;
    [HideInInspector]
    public bool savePrefabOfMerge = false;
    [HideInInspector]
    public string nameOfPrefabOfMerge = "";

    private void OnEnable()
    {
        nameOfThisMerge = gameObject.name;
    }
    [CustomEditor(typeof(AtlasBakery))]
    public class AtlasInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            //Start the undo event support, draw default inspector and monitor of changes
            AtlasBakery script = (AtlasBakery)target;
            GameObject[] gameObjectsToMerge = script.GetAllItemsForCombine(false, true);
            MeshRenderer[] skinnedMeshesToMerge = script.GetAllSkinnedMeshsValidatedToCombine(gameObjectsToMerge);
            if (GUILayout.Button("Recalculate textures sizes", GUILayout.Height(20)))
            {
                script.skinnedMeshMaterials.Clear();
                script.skinnedMeshMaterialsMultiplier.Clear();
                for (int i = 0; i < skinnedMeshesToMerge.Length; i++)
                {
                    MeshRenderer item = skinnedMeshesToMerge[i];
                    foreach (Material mat in item.sharedMaterials)
                    {
                        script.skinnedMeshMaterials.Add(mat);
                        script.skinnedMeshMaterialsMultiplier.Add(1024);
                    }
                }
            }
            // Diccionario para almacenar el tamaño de atlas por cada nombre de material
            Dictionary<string, int> materialSizeMapping = new Dictionary<string, int>();

            for (int i = 0; i < script.skinnedMeshMaterials.Count; i++)
            {
                Material item = script.skinnedMeshMaterials[i];
                string materialName = item.name;

                // Solo mostramos el campo de texto una vez por material de mismo nombre
                if (!materialSizeMapping.ContainsKey(materialName))
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(materialName + " material. Set desired square px:");

                    // Inicializamos el tamaño con el valor actual o 1024 como valor predeterminado
                    if (!materialSizeMapping.ContainsKey(materialName))
                    {
                        materialSizeMapping[materialName] = script.skinnedMeshMaterialsMultiplier[i];
                    }

                    // Asignamos el nuevo valor ingresado por el usuario al diccionario
                    materialSizeMapping[materialName] = int.Parse(EditorGUILayout.TextField(materialSizeMapping[materialName].ToString()));

                    GUILayout.EndHorizontal();
                }

                // Asignamos el mismo valor a todos los materiales con el mismo nombre
                script.skinnedMeshMaterialsMultiplier[i] = materialSizeMapping[materialName];
            }
            GUILayout.Space(20);
            if (GUILayout.Button("Apply texture size", GUILayout.Height(20)))
            {
                for (int i = 0; i < script.skinnedMeshMaterials.Count; i++)
                {
                    string[] textureProperties = script.skinnedMeshMaterials[i].GetTexturePropertyNames();

                    foreach (string property in textureProperties)
                    {
                        Texture texture = script.skinnedMeshMaterials[i].GetTexture(property);
                        if (texture != null && texture is Texture2D)
                        {
                            // Obtenemos la ruta de la textura
                            string imgAssetPath = AssetDatabase.GetAssetPath(texture);

                            // Obtenemos el TextureImporter asociado a la textura
                            TextureImporter textureImporter = (TextureImporter)AssetImporter.GetAtPath(imgAssetPath);

                            if (textureImporter != null)
                            {
                                // Cambiar el tamaño máximo de la textura
                                int newSize = script.skinnedMeshMaterialsMultiplier[i]; // Tamaño deseado para la textura
                                textureImporter.maxTextureSize = newSize;

                                // Guardar y aplicar los cambios
                                AssetDatabase.WriteImportSettingsIfDirty(imgAssetPath);
                                AssetDatabase.ImportAsset(imgAssetPath, ImportAssetOptions.ForceUpdate);

                                Debug.Log($"Texture resized: {texture.name} to max size: {newSize}px");
                            }
                        }
                    }
                }
                script.DoCombineMeshs_AllInOne();
            }
        }
    }
#if UNITY_EDITOR
    [ContextMenu("DoCombine")]
    private void DoCombineMeshs_AllInOne()
    {
        mergeMethod = MergeMethod.AllInOne;
        //Reset position, rotation and scale and store it (to avoid problems with matrix or blendshapes positioning for example)
        if (autoManagePosition == true)
        {
            thisOriginalPosition = this.gameObject.transform.position;
            thisOriginalRotation = this.gameObject.transform.eulerAngles;
            thisOriginalScale = this.gameObject.transform.localScale;
            this.gameObject.transform.position = Vector3.zero;
            this.gameObject.transform.eulerAngles = Vector3.zero;
            this.gameObject.transform.localScale = Vector3.one;
        }

        //Try to merge. If occurs error, stop merge
        //Clear Logs Of Merge
        resultMergeLogs.Clear();

        //Validate all variables
        ValidateAllVariables();

        
        //Start of Stats
        int mergedMeshes = 0;
        int drawCallReduction = 0;
        int originalUvLenght = 0;

        //Get all GameObjects to merge
        GameObject[] gameObjectsToMerge = GetAllItemsForCombine(false, true);

        //Get all Skinned Mesh Renderers to merge
        MeshRenderer[] skinnedMeshesToMerge = GetAllSkinnedMeshsValidatedToCombine(gameObjectsToMerge);

        //Verify if is provided a material to use
        if (allInOneParams.materialToUse == null)
        {
            return;
        }

        //Stop the merge if not have meshes to merge
        if (skinnedMeshesToMerge == null || skinnedMeshesToMerge.Length < 1)
        {
            return;
        }

        //------------------------------- START OF MERGE CODE --------------------------------

        //Prepare the storage
        List<CombineInstance> combinesToMerge = new List<CombineInstance>();
        List<TexturesSubMeshes> texturesAndSubMeshes = new List<TexturesSubMeshes>();

        //Prepare the progress bar to read mesh progress (It is used only in editor to show on progress bar)
        int totalSubMeshsInAllSkinnedMeshes = 0;
        foreach (MeshRenderer meshRenderer in skinnedMeshesToMerge)
            totalSubMeshsInAllSkinnedMeshes += meshRenderer.GetComponent<MeshFilter>().sharedMesh.subMeshCount;
        int totalSkinnedMeshesVerifiedAtHere = 0;

        //Obtains the data of each merge
        int totalVerticesVerifiedAtHere = 0;
        int aux = 0;
        foreach (MeshRenderer meshRender in skinnedMeshesToMerge)
        {
            //Get the data of merge for each submesh of this mesh
            for (int i = 0; i < meshRender.GetComponent<MeshFilter>().sharedMesh.subMeshCount; i++)
            {
                //Show progress bar
                float progressOfThisMeshRead = ((float)totalSkinnedMeshesVerifiedAtHere) / ((float)totalSubMeshsInAllSkinnedMeshes + 1);

                //Configure the Combine Instances for each submesh or mesh
                CombineInstance combineInstance = new CombineInstance();
                combineInstance.mesh = meshRender.GetComponent<MeshFilter>().sharedMesh;
                combineInstance.subMeshIndex = i;
                combineInstance.transform = meshRender.transform.localToWorldMatrix;
                combinesToMerge.Add(combineInstance);

                //Get the entire UV map of this submesh
                Vector2[] uvMapOfThisSubMesh = combineInstance.mesh.SMCGetSubmesh(i).uv;

                //Check if UV of this mesh uses a tiled texture (first, get the bounds values of UV of this mesh)
                TexturesSubMeshes.UvBounds boundDataOfUv = GetBoundValuesOfSubMeshUv(uvMapOfThisSubMesh);
                //If merge of tiled meshs is legacy, force all textures to be a normal textures, to rest of merge run as normal textures
                if (allInOneParams.mergeTiledTextures == MergeTiledTextures.LegacyMode)
                {
                    boundDataOfUv.majorX = 1.0f;
                    boundDataOfUv.majorY = 1.0f;
                    boundDataOfUv.minorX = 0.0f;
                    boundDataOfUv.minorY = 0.0f;
                }
                boundDataOfUv.RoundBoundsValuesAndCalculateSpaceNeededToTiling(); //<- This is necessary to avoid calcs problemns with float precision of Unity

                //If UV of this mesh, use a tiled texture, create another item to storage the data for only this submesh
                if (isTiledTexture(boundDataOfUv) == true)
                {
                    //Create another texture and respective submeshes to store it
                    TexturesSubMeshes thisTextureAndSubMesh = new TexturesSubMeshes();

                    //Calculate and get original resolution of main texture of this material
                    Texture2D mainTextureOfThisMaterial = (Texture2D)meshRender.sharedMaterials[i].GetTexture(allInOneParams.ColorAlphaPropertyToFind);
                    Vector2Int mainTextureSize = Vector2Int.zero;
                    Vector2Int mainTextureSizeWithEdges = Vector2Int.zero;
                    if (mainTextureOfThisMaterial == null)
                        mainTextureSize = new Vector2Int(64, 64);
                    if (mainTextureOfThisMaterial != null)
                        mainTextureSize = new Vector2Int(mainTextureOfThisMaterial.width, mainTextureOfThisMaterial.height);
                    mainTextureSizeWithEdges = new Vector2Int(mainTextureSize.x + (GetEdgesSizeForTextures() * 2), mainTextureSize.y + (GetEdgesSizeForTextures() * 2));

                    mainTextureSizeWithEdges = new Vector2Int((int)(skinnedMeshMaterialsMultiplier[aux]), (int)(skinnedMeshMaterialsMultiplier[aux]));
                    //Fill this class
                    thisTextureAndSubMesh.material = meshRender.sharedMaterials[i];
                    thisTextureAndSubMesh.isTiledTexture = true;
                    thisTextureAndSubMesh.mainTextureResolution = new Vector2Int(256, 256);// mainTextureSize;
                    thisTextureAndSubMesh.mainTextureResolutionWithEdges = new Vector2Int(256, 256);// mainTextureSizeWithEdges;
                    thisTextureAndSubMesh.mainTexture = GetValidatedCopyOfTexture(thisTextureAndSubMesh.material, allInOneParams.ColorAlphaPropertyToFind, thisTextureAndSubMesh.mainTextureResolutionWithEdges.x, thisTextureAndSubMesh.mainTextureResolutionWithEdges.y, boundDataOfUv, TextureType.MainTexture, true, progressOfThisMeshRead);
                    if (allInOneParams.RMAESupport == true)
                        thisTextureAndSubMesh.metallicMap = GetValidatedCopyOfTexture(thisTextureAndSubMesh.material, allInOneParams.RMAEPropertyToFind, thisTextureAndSubMesh.mainTextureResolutionWithEdges.x, thisTextureAndSubMesh.mainTextureResolutionWithEdges.y, boundDataOfUv, TextureType.MetallicMap, true, progressOfThisMeshRead);
                    if (allInOneParams.NormalMapSupport == true)
                        thisTextureAndSubMesh.specularMap = GetValidatedCopyOfTexture(thisTextureAndSubMesh.material, allInOneParams.NormalMapPropertyToFind, thisTextureAndSubMesh.mainTextureResolutionWithEdges.x, thisTextureAndSubMesh.mainTextureResolutionWithEdges.y, boundDataOfUv, TextureType.SpecularMap, true, progressOfThisMeshRead);
                    if (allInOneParams.aux1 == true)
                        thisTextureAndSubMesh.normalMap = GetValidatedCopyOfTexture(thisTextureAndSubMesh.material, allInOneParams.aux1PropertyToFind, thisTextureAndSubMesh.mainTextureResolutionWithEdges.x, thisTextureAndSubMesh.mainTextureResolutionWithEdges.y, boundDataOfUv, TextureType.NormalMap, true, progressOfThisMeshRead);
                    if (allInOneParams.aux2Support == true)
                        thisTextureAndSubMesh.normalMap2 = GetValidatedCopyOfTexture(thisTextureAndSubMesh.material, allInOneParams.aux2PropertyFind, thisTextureAndSubMesh.mainTextureResolutionWithEdges.x, thisTextureAndSubMesh.mainTextureResolutionWithEdges.y, boundDataOfUv, TextureType.NormalMap, true, progressOfThisMeshRead);
                    if (allInOneParams.aux3Support == true)
                        thisTextureAndSubMesh.heightMap = GetValidatedCopyOfTexture(thisTextureAndSubMesh.material, allInOneParams.aux3PropertyToFind, thisTextureAndSubMesh.mainTextureResolutionWithEdges.x, thisTextureAndSubMesh.mainTextureResolutionWithEdges.y, boundDataOfUv, TextureType.HeightMap, true, progressOfThisMeshRead);
                    if (allInOneParams.aux4Support == true)
                        thisTextureAndSubMesh.occlusionMap = GetValidatedCopyOfTexture(thisTextureAndSubMesh.material, allInOneParams.aux4PropertyToFind, thisTextureAndSubMesh.mainTextureResolutionWithEdges.x, thisTextureAndSubMesh.mainTextureResolutionWithEdges.y, boundDataOfUv, TextureType.OcclusionMap, true, progressOfThisMeshRead);
                    if (allInOneParams.aux5MapSupport == true)
                        thisTextureAndSubMesh.detailMap = GetValidatedCopyOfTexture(thisTextureAndSubMesh.material, allInOneParams.aux5PropertyToFind, thisTextureAndSubMesh.mainTextureResolutionWithEdges.x, thisTextureAndSubMesh.mainTextureResolutionWithEdges.y, boundDataOfUv, TextureType.DetailMap, true, progressOfThisMeshRead);
                    if (allInOneParams.aux6Support == true)
                        thisTextureAndSubMesh.detailMask = GetValidatedCopyOfTexture(thisTextureAndSubMesh.material, allInOneParams.aux6PropertyToFind, thisTextureAndSubMesh.mainTextureResolutionWithEdges.x, thisTextureAndSubMesh.mainTextureResolutionWithEdges.y, boundDataOfUv, TextureType.DetailMask, true, progressOfThisMeshRead);

                    //Create this mesh data. get all UV values from this submesh
                    TexturesSubMeshes.UserSubMeshes userSubMesh = new TexturesSubMeshes.UserSubMeshes();
                    userSubMesh.uvBoundsOfThisSubMesh = boundDataOfUv;
                    userSubMesh.startOfUvVerticesInIndex = totalVerticesVerifiedAtHere;
                    userSubMesh.originalUvVertices = new Vector2[uvMapOfThisSubMesh.Length];
                    for (int v = 0; v < userSubMesh.originalUvVertices.Length; v++)
                        userSubMesh.originalUvVertices[v] = uvMapOfThisSubMesh[v];
                    thisTextureAndSubMesh.userSubMeshes.Add(userSubMesh);

                    //Save the created class
                    texturesAndSubMeshes.Add(thisTextureAndSubMesh);
                }

                //If UV of this mesh, use a normal texture
                if (isTiledTexture(boundDataOfUv) == false)
                {
                    //Try to find a texture and respective submeshes that already is created that is using this texture
                    TexturesSubMeshes textureOfThisSubMesh = GetTheTextureSubMeshesOfMaterial(meshRender.sharedMaterials[i], texturesAndSubMeshes);

                    //If not found
                    if (textureOfThisSubMesh == null)
                    {
                        //Create another texture and respective submeshes to store it
                        TexturesSubMeshes thisTextureAndSubMesh = new TexturesSubMeshes();


                        //Calculate and get original resolution of main texture of this material
                        Texture2D mainTextureOfThisMaterial = (Texture2D)meshRender.sharedMaterials[i].GetTexture("_MainTex");//allInOneParams.mainTexturePropertyToFind);
                        Vector2Int mainTextureSize = Vector2Int.zero;
                        Vector2Int mainTextureSizeWithEdges = Vector2Int.zero;
                        if (mainTextureOfThisMaterial == null)
                            mainTextureSize = new Vector2Int(64, 64);
                        if (mainTextureOfThisMaterial != null)
                            mainTextureSize = new Vector2Int(mainTextureOfThisMaterial.width, mainTextureOfThisMaterial.height);
                        mainTextureSizeWithEdges = new Vector2Int(mainTextureSize.x + (GetEdgesSizeForTextures() * 2), mainTextureSize.y + (GetEdgesSizeForTextures() * 2));
                        
                        //Fill this class
                        thisTextureAndSubMesh.material = meshRender.sharedMaterials[i];
                        thisTextureAndSubMesh.isTiledTexture = false;
                        thisTextureAndSubMesh.mainTextureResolution = mainTextureSize;
                        thisTextureAndSubMesh.mainTextureResolutionWithEdges = mainTextureSizeWithEdges;
                        thisTextureAndSubMesh.mainTexture = GetValidatedCopyOfTexture(thisTextureAndSubMesh.material, "_MainTex", thisTextureAndSubMesh.mainTextureResolutionWithEdges.x, thisTextureAndSubMesh.mainTextureResolutionWithEdges.y, boundDataOfUv, TextureType.MainTexture, true, progressOfThisMeshRead);
                        if (allInOneParams.RMAESupport == true)
                            thisTextureAndSubMesh.metallicMap = GetValidatedCopyOfTexture(thisTextureAndSubMesh.material, allInOneParams.RMAEPropertyToFind, thisTextureAndSubMesh.mainTextureResolutionWithEdges.x, thisTextureAndSubMesh.mainTextureResolutionWithEdges.y, boundDataOfUv, TextureType.MetallicMap, true, progressOfThisMeshRead);
                        if (allInOneParams.NormalMapSupport == true)
                            thisTextureAndSubMesh.specularMap = GetValidatedCopyOfTexture(thisTextureAndSubMesh.material, allInOneParams.NormalMapPropertyToFind, thisTextureAndSubMesh.mainTextureResolutionWithEdges.x, thisTextureAndSubMesh.mainTextureResolutionWithEdges.y, boundDataOfUv, TextureType.SpecularMap, true, progressOfThisMeshRead);
                        if (allInOneParams.aux1 == true)
                            thisTextureAndSubMesh.normalMap = GetValidatedCopyOfTexture(thisTextureAndSubMesh.material, allInOneParams.aux1PropertyToFind, thisTextureAndSubMesh.mainTextureResolutionWithEdges.x, thisTextureAndSubMesh.mainTextureResolutionWithEdges.y, boundDataOfUv, TextureType.NormalMap, true, progressOfThisMeshRead);
                        if (allInOneParams.aux2Support == true)
                            thisTextureAndSubMesh.normalMap2 = GetValidatedCopyOfTexture(thisTextureAndSubMesh.material, allInOneParams.aux2PropertyFind, thisTextureAndSubMesh.mainTextureResolutionWithEdges.x, thisTextureAndSubMesh.mainTextureResolutionWithEdges.y, boundDataOfUv, TextureType.NormalMap, true, progressOfThisMeshRead);
                        if (allInOneParams.aux3Support == true)
                            thisTextureAndSubMesh.heightMap = GetValidatedCopyOfTexture(thisTextureAndSubMesh.material, allInOneParams.aux3PropertyToFind, thisTextureAndSubMesh.mainTextureResolutionWithEdges.x, thisTextureAndSubMesh.mainTextureResolutionWithEdges.y, boundDataOfUv, TextureType.HeightMap, true, progressOfThisMeshRead);
                        if (allInOneParams.aux4Support == true)
                            thisTextureAndSubMesh.occlusionMap = GetValidatedCopyOfTexture(thisTextureAndSubMesh.material, allInOneParams.aux4PropertyToFind, thisTextureAndSubMesh.mainTextureResolutionWithEdges.x, thisTextureAndSubMesh.mainTextureResolutionWithEdges.y, boundDataOfUv, TextureType.OcclusionMap, true, progressOfThisMeshRead);
                        if (allInOneParams.aux5MapSupport == true)
                            thisTextureAndSubMesh.detailMap = GetValidatedCopyOfTexture(thisTextureAndSubMesh.material, allInOneParams.aux5PropertyToFind, thisTextureAndSubMesh.mainTextureResolutionWithEdges.x, thisTextureAndSubMesh.mainTextureResolutionWithEdges.y, boundDataOfUv, TextureType.DetailMap, true, progressOfThisMeshRead);
                        if (allInOneParams.aux6Support == true)
                            thisTextureAndSubMesh.detailMask = GetValidatedCopyOfTexture(thisTextureAndSubMesh.material, allInOneParams.aux6PropertyToFind, thisTextureAndSubMesh.mainTextureResolutionWithEdges.x, thisTextureAndSubMesh.mainTextureResolutionWithEdges.y, boundDataOfUv, TextureType.DetailMask, true, progressOfThisMeshRead);

                        //Create this mesh data. get all UV values from this submesh
                        TexturesSubMeshes.UserSubMeshes userSubMesh = new TexturesSubMeshes.UserSubMeshes();
                        userSubMesh.uvBoundsOfThisSubMesh = boundDataOfUv;
                        userSubMesh.startOfUvVerticesInIndex = totalVerticesVerifiedAtHere;
                        userSubMesh.originalUvVertices = new Vector2[uvMapOfThisSubMesh.Length];
                        for (int v = 0; v < userSubMesh.originalUvVertices.Length; v++)
                            userSubMesh.originalUvVertices[v] = uvMapOfThisSubMesh[v];
                        thisTextureAndSubMesh.userSubMeshes.Add(userSubMesh);

                        //Save the created class
                        texturesAndSubMeshes.Add(thisTextureAndSubMesh);
                    }

                    //If found
                    if (textureOfThisSubMesh != null)
                    {
                        //Create this mesh data and add to textures that already exists. get all UV values from this submesh
                        TexturesSubMeshes.UserSubMeshes userSubMesh = new TexturesSubMeshes.UserSubMeshes();
                        userSubMesh.uvBoundsOfThisSubMesh = boundDataOfUv;
                        userSubMesh.startOfUvVerticesInIndex = totalVerticesVerifiedAtHere;
                        userSubMesh.originalUvVertices = new Vector2[uvMapOfThisSubMesh.Length];
                        for (int v = 0; v < userSubMesh.originalUvVertices.Length; v++)
                            userSubMesh.originalUvVertices[v] = uvMapOfThisSubMesh[v];
                        textureOfThisSubMesh.userSubMeshes.Add(userSubMesh);
                    }
                }

                //Increment stats
                mergedMeshes += 1;
                drawCallReduction += 1;
                originalUvLenght += uvMapOfThisSubMesh.Length;

                //Add the total vertices verified
                totalVerticesVerifiedAtHere += uvMapOfThisSubMesh.Length;

                //Update the value of progress bar of readed meshes
                totalSkinnedMeshesVerifiedAtHere += 1;
            }
            aux++;
        }

        //Combine all submeshes into one mesh with submeshes with all materials
        Mesh finalMesh = new Mesh();
        finalMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        finalMesh.name = "Combined Meshes (All In One)";
        finalMesh.CombineMeshes(combinesToMerge.ToArray(), true, true);

        //Do recalculations where is desired
        finalMesh.RecalculateBounds();

        //Create the holder GameObject
        resultMergeGameObject = new GameObject(nameOfThisMerge);
        resultMergeGameObject.transform.SetParent(this.gameObject.transform);
        MeshRenderer smrender = resultMergeGameObject.AddComponent<MeshRenderer>();
        smrender.gameObject.AddComponent<MeshFilter>();
        smrender.GetComponent<MeshFilter>().sharedMesh = finalMesh;
        smrender.sharedMaterials = new Material[] { GetValidatedCopyOfMaterial(allInOneParams.materialToUse, true, true) };
        smrender.sharedMaterials[0].name = "Combined Materials (All In One)";


        //Create all atlas using all collected textures
        AtlasData atlasGenerated = CreateAllAtlas(texturesAndSubMeshes, GetAtlasMaxResolution(), GetAtlasPadding(), true);


        //If the UV map of this mesh is inexistent
        if (smrender.GetComponent<MeshFilter>().sharedMesh.uv.Length == 0)
        {
            return;
        }

        //Process each submesh UV data and create a new entire UV map for combined mesh
        Vector2[] newUvMapForCombinedMesh = new Vector2[smrender.GetComponent<MeshFilter>().sharedMesh.uv.Length];
        foreach (TexturesSubMeshes thisTexture in texturesAndSubMeshes)
        {
            //Convert all vertices of all submeshes of this texture, to positive, if is a tiled texture
            if (thisTexture.isTiledTexture == true)
                thisTexture.ConvertAllSubMeshsVerticesToPositive();

            //Process each submesh registered as user of this texture
            foreach (TexturesSubMeshes.UserSubMeshes submesh in thisTexture.userSubMeshes)
            {
                //If this is a normal texture, not is a tiled texture (merge with the basic UV mapping algorthm)
                if (thisTexture.isTiledTexture == false)
                {
                    //Change all vertex of UV to positive, where vertex position is major than 1 or minor than 0, because the entire UV will resized to fit in your respective texture in atlas
                    for (int i = 0; i < submesh.originalUvVertices.Length; i++)
                    {
                        if (submesh.originalUvVertices[i].x < 0)
                            submesh.originalUvVertices[i].x = submesh.originalUvVertices[i].x * -1;
                        if (submesh.originalUvVertices[i].y < 0)
                            submesh.originalUvVertices[i].y = submesh.originalUvVertices[i].y * -1;
                    }

                    //Calculates the highest point of the UV map of each mesh, for know how to reduces to fit in texture atlas, checks which is the largest coordinate found in the list of UV vertices, in X or Y and stores it
                    Vector2 highestVertexCoordinatesForThisSubmesh = Vector2.zero;
                    for (int i = 0; i < submesh.originalUvVertices.Length; i++)
                        highestVertexCoordinatesForThisSubmesh = new Vector2(Mathf.Max(submesh.originalUvVertices[i].x, highestVertexCoordinatesForThisSubmesh.x), Mathf.Max(submesh.originalUvVertices[i].y, highestVertexCoordinatesForThisSubmesh.y));

                    //Calculate the percentage that the edge of this texture uses, calculates the size of the uv for each texture, to ignore the edges
                    Vector2 percentEdgeUsageOfCurrentTexture = thisTexture.GetEdgesPercentUsageOfThisTextures();

                    //Get index of this main texture submesh in atlas rects
                    int mainTextureIndexInAtlas = atlasGenerated.GetRectIndexOfThatMainTexture(thisTexture.mainTexture);

                    //Process all uv vertices of this submesh
                    for (int i = 0; i < submesh.originalUvVertices.Length; i++)
                    {
                        //Create the vertice
                        Vector2 thisVertex = Vector2.zero;
                        // ISRA ISRA ISRA
                        //If the UV map of this mesh is not larger than the texture
                        if (highestVertexCoordinatesForThisSubmesh.x <= 1)
                            thisVertex.x = Mathf.Lerp(atlasGenerated.atlasRects[mainTextureIndexInAtlas].xMin, atlasGenerated.atlasRects[mainTextureIndexInAtlas].xMax, Mathf.Lerp(percentEdgeUsageOfCurrentTexture.x, 1 - percentEdgeUsageOfCurrentTexture.x, submesh.originalUvVertices[i].x));
                        if (highestVertexCoordinatesForThisSubmesh.y <= 1)
                            thisVertex.y = Mathf.Lerp(atlasGenerated.atlasRects[mainTextureIndexInAtlas].yMin, atlasGenerated.atlasRects[mainTextureIndexInAtlas].yMax, Mathf.Lerp(percentEdgeUsageOfCurrentTexture.y, 1 - percentEdgeUsageOfCurrentTexture.y, submesh.originalUvVertices[i].y));

                        //If the UV map is larger than the texture
                        if (highestVertexCoordinatesForThisSubmesh.x > 1)
                            thisVertex.x = Mathf.Lerp(atlasGenerated.atlasRects[mainTextureIndexInAtlas].xMin, atlasGenerated.atlasRects[mainTextureIndexInAtlas].xMax, Mathf.Lerp(percentEdgeUsageOfCurrentTexture.x, 1 - percentEdgeUsageOfCurrentTexture.x, submesh.originalUvVertices[i].x / highestVertexCoordinatesForThisSubmesh.x));
                        if (highestVertexCoordinatesForThisSubmesh.y > 1)
                            thisVertex.y = Mathf.Lerp(atlasGenerated.atlasRects[mainTextureIndexInAtlas].yMin, atlasGenerated.atlasRects[mainTextureIndexInAtlas].yMax, Mathf.Lerp(percentEdgeUsageOfCurrentTexture.y, 1 - percentEdgeUsageOfCurrentTexture.y, submesh.originalUvVertices[i].y / highestVertexCoordinatesForThisSubmesh.y));

                        //Save this vertice edited in uv map of combined mesh
                        newUvMapForCombinedMesh[i + submesh.startOfUvVerticesInIndex] = thisVertex;
                    }
                }
            }
        }

        //Apply the new UV map merged using modification of all UV vertex of each submesh
        smrender.GetComponent<MeshFilter>().sharedMesh.uv = newUvMapForCombinedMesh;

        //Apply all atlas too
        ApplyAtlasInPropertyOfMaterial(smrender.sharedMaterials[0], allInOneParams.ColorAlphaPropertyToInsert, atlasGenerated.mainTextureAtlas);
        if (allInOneParams.RMAESupport == true)
            ApplyAtlasInPropertyOfMaterial(smrender.sharedMaterials[0], allInOneParams.RMAEPropertyToInsert, atlasGenerated.metallicMapAtlas);
        if (allInOneParams.NormalMapSupport == true)
            ApplyAtlasInPropertyOfMaterial(smrender.sharedMaterials[0], allInOneParams.NormalMapPropertyToInsert, atlasGenerated.specularMapAtlas);
        if (allInOneParams.aux1 == true)
            ApplyAtlasInPropertyOfMaterial(smrender.sharedMaterials[0], allInOneParams.aux1PropertyToInsert, atlasGenerated.normalMapAtlas);
        if (allInOneParams.aux2Support == true)
            ApplyAtlasInPropertyOfMaterial(smrender.sharedMaterials[0], allInOneParams.aux2PropertyToInsert, atlasGenerated.normalMap2Atlas);
        if (allInOneParams.aux3Support == true)
            ApplyAtlasInPropertyOfMaterial(smrender.sharedMaterials[0], allInOneParams.aux3PropertyToInsert, atlasGenerated.heightMapAtlas);
        if (allInOneParams.aux4Support == true)
            ApplyAtlasInPropertyOfMaterial(smrender.sharedMaterials[0], allInOneParams.aux4PropertyToInsert, atlasGenerated.occlusionMapAtlas);
        if (allInOneParams.aux5MapSupport == true)
            ApplyAtlasInPropertyOfMaterial(smrender.sharedMaterials[0], allInOneParams.aux5PropertyToInsert, atlasGenerated.detailMapAtlas);
        if (allInOneParams.aux6Support == true)
            ApplyAtlasInPropertyOfMaterial(smrender.sharedMaterials[0], allInOneParams.aux6PropertyToInsert, atlasGenerated.detailMaskAtlas);

        //------------------------------- END OF MERGE CODE --------------------------------

        //Save the original GameObjects
        resultMergeOriginalGameObjects = gameObjectsToMerge;

        //Disable all original GameObjects that are merged
        foreach (MeshRenderer originalRender in skinnedMeshesToMerge)
        {
            originalRender.gameObject.SetActive(false);
        }

        //Save data as asset
        if (saveDataInAssets == true)
            SaveAssetAsFile("GEO", smrender.GetComponent<MeshFilter>().sharedMesh, this.gameObject.name, "asset", false);
        if (saveDataInAssets == true)
            SaveAssetAsFile("MATS", atlasGenerated.mainTextureAtlas, this.gameObject.name + " (MainTexture)", "asset", true);
        if (saveDataInAssets == true && allInOneParams.RMAESupport == true)
            SaveAssetAsFile("MATS", atlasGenerated.metallicMapAtlas, this.gameObject.name + " (MetallicMap)", "asset", true);
        if (saveDataInAssets == true && allInOneParams.NormalMapSupport == true)
            SaveAssetAsFile("MATS", atlasGenerated.specularMapAtlas, this.gameObject.name + " (SpecularMap)", "asset", true);
        if (saveDataInAssets == true && allInOneParams.aux1 == true)
            SaveAssetAsFile("MATS", atlasGenerated.normalMapAtlas, this.gameObject.name + " (NormalMap)", "asset", true);
        if (saveDataInAssets == true && allInOneParams.aux2Support == true)
            SaveAssetAsFile("MATS", atlasGenerated.normalMap2Atlas, this.gameObject.name + " (NormalMap2x)", "asset", true);
        if (saveDataInAssets == true && allInOneParams.aux3Support == true)
            SaveAssetAsFile("MATS", atlasGenerated.heightMapAtlas, this.gameObject.name + " (HeightMap)", "asset", true);
        if (saveDataInAssets == true && allInOneParams.aux4Support == true)
            SaveAssetAsFile("MATS", atlasGenerated.occlusionMapAtlas, this.gameObject.name + " (OcclusionMap)", "asset", true);
        if (saveDataInAssets == true && allInOneParams.aux5MapSupport == true)
            SaveAssetAsFile("MATS", atlasGenerated.detailMapAtlas, this.gameObject.name + " (DetailMap)", "asset", true);
        if (saveDataInAssets == true && allInOneParams.aux6Support == true)
            SaveAssetAsFile("MATS", atlasGenerated.detailMaskAtlas, this.gameObject.name + " (DetailMask)", "asset", true);
        if (saveDataInAssets == true)
            SaveAssetAsFile("MATS", smrender.sharedMaterials[0], this.gameObject.name, "mat", false);

        //Show alert e log
        if (Application.isPlaying == false)
            Debug.Log("The merging of the meshes was completed successfully!");

        //Restore original position, rotation and scale
        if (autoManagePosition == true)
        {
            this.gameObject.transform.position = thisOriginalPosition;
            this.gameObject.transform.eulerAngles = thisOriginalRotation;
            this.gameObject.transform.localScale = thisOriginalScale;
        }
    }
    private Material GetValidatedCopyOfMaterial(Material targetMaterial, bool copyPropertiesOfTargetMaterial, bool clearAllTextures)
    {
        //Return a copy of target material
        Material material = new Material(targetMaterial.shader);

        //Copy all propertyies, if is desired
        if (copyPropertiesOfTargetMaterial == true)
            material.CopyPropertiesFromMaterial(targetMaterial);

        //Clear all textures, is is desired
        if (clearAllTextures == true)
        {
            if (material.HasProperty("_MainTex") == true)
                material.SetTexture("_MainTex", null);

            if (material.HasProperty("_MAS") == true)
                material.SetTexture("_MAS", null);

            if (material.HasProperty("_Normal") == true)
                material.SetTexture("_Normal", null);
        }

        return material;
    }
    
    private int GetAtlasPadding()
    {
        //If is All In One
        if (mergeMethod == MergeMethod.AllInOne)
        {
            switch (allInOneParams.atlasPadding)
            {
                case AtlasPadding.Pixels0x0:
                    return 0;
                case AtlasPadding.Pixels2x2:
                    return 2;
                case AtlasPadding.Pixels4x4:
                    return 4;
                case AtlasPadding.Pixels8x8:
                    return 8;
                case AtlasPadding.Pixels16x16:
                    return 16;
            }
        }

        //Return the max resolution
        return 0;
    }
    private int GetAtlasMaxResolution()
    {
        //If is All In One
        if (mergeMethod == MergeMethod.AllInOne)
        {
            switch (allInOneParams.atlasResolution)
            {
                case AtlasSize.Pixels32x32:
                    return 32;
                case AtlasSize.Pixels64x64:
                    return 64;
                case AtlasSize.Pixels128x128:
                    return 128;
                case AtlasSize.Pixels256x256:
                    return 256;
                case AtlasSize.Pixels512x512:
                    return 512;
                case AtlasSize.Pixels1024x1024:
                    return 1024;
                case AtlasSize.Pixels2048x2048:
                    return 2048;
                case AtlasSize.Pixels4096x4096:
                    return 4096;
                case AtlasSize.Pixels8192x8192:
                    return 8192;
            }
        }

        //Return the max resolution
        return 16;
    }
    private AtlasData CreateAllAtlas(List<TexturesSubMeshes> copyiedTextures, int maxResolution, int paddingBetweenTextures, bool showProgress)
    {
        //Create a atlas
        AtlasData atlasData = new AtlasData();
        List<Texture2D> texturesToUse = new List<Texture2D>();

        texturesToUse.Clear();
        foreach (TexturesSubMeshes item in copyiedTextures)
        {
            texturesToUse.Add(item.mainTexture);
        }
        atlasData.originalMainTexturesUsedAndOrdenedAccordingToAtlasRect = texturesToUse.ToArray();
        atlasData.atlasRects = atlasData.mainTextureAtlas.PackTextures(texturesToUse.ToArray(), paddingBetweenTextures, maxResolution);

        //Create the metallic atlas if is desired
        if (allInOneParams.RMAESupport == true)
        {
            texturesToUse.Clear();
            foreach (TexturesSubMeshes item in copyiedTextures)
                texturesToUse.Add(item.metallicMap);
            atlasData.metallicMapAtlas.PackTextures(texturesToUse.ToArray(), paddingBetweenTextures, maxResolution);
        }

        //Create the specullar atlas if is desired
        if (allInOneParams.NormalMapSupport == true)
        {
            texturesToUse.Clear();
            foreach (TexturesSubMeshes item in copyiedTextures)
                texturesToUse.Add(item.specularMap);
            atlasData.specularMapAtlas.PackTextures(texturesToUse.ToArray(), paddingBetweenTextures, maxResolution);
        }

        //Create the normal atlas if is desired
        if (allInOneParams.aux1 == true)
        {
            texturesToUse.Clear();
            foreach (TexturesSubMeshes item in copyiedTextures)
                texturesToUse.Add(item.normalMap);
            atlasData.normalMapAtlas.PackTextures(texturesToUse.ToArray(), paddingBetweenTextures, maxResolution);
        }

        //Create the normal 2 atlas if is desired
        if (allInOneParams.aux2Support == true)
        {
            texturesToUse.Clear();
            foreach (TexturesSubMeshes item in copyiedTextures)
                texturesToUse.Add(item.normalMap2);
            atlasData.normalMap2Atlas.PackTextures(texturesToUse.ToArray(), paddingBetweenTextures, maxResolution);
        }

        //Create the height atlas if is desired
        if (allInOneParams.aux3Support == true)
        {
            texturesToUse.Clear();
            foreach (TexturesSubMeshes item in copyiedTextures)
                texturesToUse.Add(item.heightMap);
            atlasData.heightMapAtlas.PackTextures(texturesToUse.ToArray(), paddingBetweenTextures, maxResolution);
        }

        //Create the occlusion atlas if is desired
        if (allInOneParams.aux4Support == true)
        {
            texturesToUse.Clear();
            foreach (TexturesSubMeshes item in copyiedTextures)
                texturesToUse.Add(item.occlusionMap);
            atlasData.occlusionMapAtlas.PackTextures(texturesToUse.ToArray(), paddingBetweenTextures, maxResolution);
        }

        //Create the detail atlas if is desired
        if (allInOneParams.aux5MapSupport == true)
        {
            texturesToUse.Clear();
            foreach (TexturesSubMeshes item in copyiedTextures)
                texturesToUse.Add(item.detailMap);
            atlasData.detailMapAtlas.PackTextures(texturesToUse.ToArray(), paddingBetweenTextures, maxResolution);
        }

        //Create the detail mask if is desired
        if (allInOneParams.aux6Support == true)
        {
            texturesToUse.Clear();
            foreach (TexturesSubMeshes item in copyiedTextures)
                texturesToUse.Add(item.detailMask);
            atlasData.detailMaskAtlas.PackTextures(texturesToUse.ToArray(), paddingBetweenTextures, maxResolution);
        }

        //Return the object
        return atlasData;
    }
    private MeshRenderer[] GetAllSkinnedMeshsValidatedToCombine(GameObject[] gameObjectsToCombine)
    {
        //Prepare the storage
        List<MeshRenderer> meshRenderers = new List<MeshRenderer>();

        //Get skinned mesh renderers in all GameObjects to combine
        foreach (GameObject obj in gameObjectsToCombine)
        {
            //Get the Skinned Mesh of this GameObject
            MeshRenderer meshRender = obj.GetComponent<MeshRenderer>();

            if (meshRender != null)
            {
                //Verify if msh renderer is disabled
                if (meshRender.enabled == false)
                {
                    continue;
                }

                //Verify if the sharedmesh is null
                if (meshRender.GetComponent<MeshFilter>().sharedMesh == null)
                {
                    continue;
                }

                //Verify if shared materials is null
                if (meshRender.sharedMaterials == null)
                {
                    continue;
                }

                //Verify if not have materials
                if (meshRender.sharedMaterials.Length == 0)
                {
                    continue;
                }

                //Verify if quantity of shared materials is different of submeshes
                if (meshRender.sharedMaterials.Length != meshRender.GetComponent<MeshFilter>().sharedMesh.subMeshCount)
                {
                    continue;
                }

                //Verify if exists null materials in this mesh
                bool foundNullMaterials = false;
                foreach (Material mat in meshRender.sharedMaterials)
                {
                    if (mat == null)
                        foundNullMaterials = true;
                }
                if (foundNullMaterials == true)
                {
                    continue;
                }

                //If the method of merge is "All In One" and "Merge All UV Sizes" is disabled, remove the mesh if the UV is greater than 1 or minor than 0
                if (mergeMethod == MergeMethod.AllInOne && allInOneParams.mergeTiledTextures == MergeTiledTextures.SkipAll)
                {
                    bool haveUvVerticesMajorThanOne = false;
                    foreach (Vector2 vertex in meshRender.GetComponent<MeshFilter>().sharedMesh.uv)
                    {
                        //Check if vertex is major than 1
                        if (vertex.x > 1.0f || vertex.y > 1.0f)
                        {
                            haveUvVerticesMajorThanOne = true;
                        }
                        //Check if vertex is major than 0
                        if (vertex.x < 0.0f || vertex.y < 0.0f)
                        {
                            haveUvVerticesMajorThanOne = true;
                        }
                    }
                    if (haveUvVerticesMajorThanOne == true)
                    {
                        continue;
                    }
                }

                //Add to list of valid Skinned Meshs, if can add
                meshRenderers.Add(meshRender);
            }
        }

        //Return all Skinned Meshes
        return meshRenderers.ToArray();
    }
    private GameObject[] GetAllItemsForCombine(bool includeItemsRegisteredToBeIgnored, bool launchLogs)
    {
        //Prepare the variable
        List<GameObject> itemsForCombineStart = new List<GameObject>();

        //Get all items for combine
        if (mergeMethod != MergeMethod.OnlyAnima2dMeshes)
        {
            MeshRenderer[] renderers = this.gameObject.GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer renderer in renderers)
            {
                itemsForCombineStart.Add(renderer.gameObject);
            }
        }

        //Return all itens, if is desired
        if (includeItemsRegisteredToBeIgnored == true)
        {
            return itemsForCombineStart.ToArray();
        }

        //Create the final list of items to combine
        List<GameObject> itemsForCombineFinal = new List<GameObject>();

        //Remove all GameObjects registered to be ignored
        for (int i = 0; i < itemsForCombineStart.Count; i++)
        {
            bool isToIgnore = false;
            foreach (GameObject obj in gameObjectsToIgnore)
            {
                if (obj == itemsForCombineStart[i])
                {
                    isToIgnore = true;
                }
            }
            if (isToIgnore == true)
            {
                continue;
            }
            itemsForCombineFinal.Add(itemsForCombineStart[i]);
        }

        return itemsForCombineFinal.ToArray();
    }
    private TexturesSubMeshes.UvBounds GetBoundValuesOfSubMeshUv(Vector2[] subMeshUv)
    {
        //Create the data size
        TexturesSubMeshes.UvBounds uvBounds = new TexturesSubMeshes.UvBounds();

        //Prepare the arrays
        float[] xAxis = new float[subMeshUv.Length];
        float[] yAxis = new float[subMeshUv.Length];

        //Fill all
        for (int i = 0; i < subMeshUv.Length; i++)
        {
            xAxis[i] = subMeshUv[i].x;
            yAxis[i] = subMeshUv[i].y;
        }

        //Return the data size
        uvBounds.majorX = Mathf.Max(xAxis);
        uvBounds.majorY = Mathf.Max(yAxis);
        uvBounds.minorX = Mathf.Min(xAxis);
        uvBounds.minorY = Mathf.Min(yAxis);
        return uvBounds;
    }
    private Texture2D GetValidatedCopyOfTexture(Material materialToFindTexture, string propertyToFindTexture, int widthOfCorrespondentMainTexture, int heightOfCorrespondentMainTexture, TexturesSubMeshes.UvBounds boundsUvValues, TextureType textureType, bool showProgress, float progress)
    {
        //-------------------------------------------- Create a refereference to target texture
        //Try to get the texture of material
        Texture2D targetTexture = null;
        materialToFindTexture.EnableKeyword(propertyToFindTexture);

        //If found the property of texture
        if (materialToFindTexture.HasProperty(propertyToFindTexture) == true && materialToFindTexture.GetTexture(propertyToFindTexture) != null)
            targetTexture = (Texture2D)materialToFindTexture.GetTexture(propertyToFindTexture);

        //If not found the property of texture
        if (materialToFindTexture.HasProperty(propertyToFindTexture) == false || materialToFindTexture.GetTexture(propertyToFindTexture) == null)
        {
            //Get the default and neutral color for this texture
            ColorData defaultColor = GetDefaultAndNeutralColorForThisTexture(textureType);
            //Create a fake texture blank
            targetTexture = new Texture2D(widthOfCorrespondentMainTexture, heightOfCorrespondentMainTexture);
            //Create blank pixels
            Color[] colors = new Color[widthOfCorrespondentMainTexture * heightOfCorrespondentMainTexture];
            for (int i = 0; i < colors.Length; i++)
                colors[i] = defaultColor.color;
            //Apply all pixels in void texture
            targetTexture.SetPixels(0, 0, widthOfCorrespondentMainTexture, heightOfCorrespondentMainTexture, colors, 0);
        }

        //-------------------------------------------- Start the creation of copyied texture
        //Prepare the storage for this texture that will be copyied
        Texture2D thisTexture = null;

        //If the texture is readable
        try
        {
            //-------------------------------------------- Calculate the size of copyied texture
            //Get desired edges size for each texture of atlas
            int edgesSize = GetEdgesSizeForTextures();

            //Calculate a preview of the total and final size of texture...
            int texWidth = 0;
            int texHeight = 0;
            int maxSizeOfTextures = 16384;
            bool overcameTheLimitationOf16k = false;
            //If is a normal texture
            if (isTiledTexture(boundsUvValues) == false)
            {
                texWidth = edgesSize + targetTexture.width + edgesSize;
                texHeight = edgesSize + targetTexture.height + edgesSize;
            }
            //If is a tiled texture
            if (isTiledTexture(boundsUvValues) == true)
            {
                texWidth = edgesSize + UvBoundToPixels(boundsUvValues.spaceMinorX, targetTexture.width) + targetTexture.width + UvBoundToPixels(boundsUvValues.spaceMajorX, targetTexture.width) + edgesSize;
                texHeight = edgesSize + UvBoundToPixels(boundsUvValues.spaceMinorY, targetTexture.height) + targetTexture.height + UvBoundToPixels(boundsUvValues.spaceMajorY, targetTexture.height) + edgesSize;
            }
            //Verify if the size of texture, as overcamed the limitation of 16384 pixels of Unity
            if (texWidth >= maxSizeOfTextures || texHeight >= maxSizeOfTextures)
                overcameTheLimitationOf16k = true;
            //If overcamed the limitation of texture sizes of unity, create a texture with the size of target texture
            if (overcameTheLimitationOf16k == true)
            {
                //Get the default and neutral color for this texture
                ColorData defaultColor = GetDefaultAndNeutralColorForThisTexture(textureType);
                texWidth = targetTexture.width;
                texHeight = targetTexture.height;
            }
            //Create the texture with size calculated above
            thisTexture = new Texture2D(texWidth, texHeight, TextureFormat.ARGB32, false, false);

            //-------------------------------------------- Copy all original pixels from target texture reference
            //Copy all pixels of the target texture
            Color32[] targetTexturePixels = targetTexture.GetPixels32(0);
            //If pink normal maps fix is enabled. If this is a normal map, try to get colors using different decoding (if have a compression format that uses different channels to store colors)
            if (allInOneParams.pinkNormalMapsFix == true && textureType == TextureType.NormalMap && targetTexture.format == TextureFormat.DXT5)
                for (int i = 0; i < targetTexturePixels.Length; i++)
                {
                    Color c = targetTexturePixels[i];
                    c.r = c.a * 2 - 1;  //red<-alpha (x<-w)
                    c.g = c.g * 2 - 1; //green is always the same (y)
                    Vector2 xy = new Vector2(c.r, c.g); //this is the xy vector
                    c.b = Mathf.Sqrt(1 - Mathf.Clamp01(Vector2.Dot(xy, xy))); //recalculate the blue channel (z)
                    targetTexturePixels[i] = new Color(c.r * 0.5f + 0.5f, c.g * 0.5f + 0.5f, c.b * 0.5f + 0.5f); //back to 0-1 range
                }

            //-------------------------------------------- Create a simple texture if the size of this copy texture has exceeded the limitation
            //Apply the copyied pixels to this texture, if is a texture that overcamed the limitation of pixels
            if (overcameTheLimitationOf16k == true)
            {
                //Get the default color of this type of texture
                ColorData defaultColor = GetDefaultAndNeutralColorForThisTexture(textureType);
                //Create blank pixels
                Color[] colors = new Color[targetTexture.width * targetTexture.height];
                for (int i = 0; i < colors.Length; i++)
                    colors[i] = defaultColor.color;
                //Apply all pixels in void texture
                thisTexture.SetPixels(0, 0, targetTexture.width, targetTexture.height, colors, 0);
            }
            //-------------------------------------------- Create a copy of target texture, if this copy of texture is a normal texture without tiling
            //Apply the copyied pixels to this texture if is normal texture
            if (isTiledTexture(boundsUvValues) == false && overcameTheLimitationOf16k == false)
                thisTexture.SetPixels32(edgesSize, edgesSize, targetTexture.width, targetTexture.height, targetTexturePixels, 0);
            //-------------------------------------------- Create a copy of target texture with support to tiling, if this copy texture not exceed the limitation size of unity
            //Apply the copyied pixels to this texture if is a tiled texture, start the simulated texture tiles
            if (isTiledTexture(boundsUvValues) == true && overcameTheLimitationOf16k == false)
            {
                //Prepare the vars
                Color[] tempColorBlock = null;

                //Add the left border
                tempColorBlock = targetTexture.GetPixels(
                    targetTexture.width - (int)((float)targetTexture.width * UvBoundSplitted(boundsUvValues.minorX)[1]), 0,
                    (int)((float)targetTexture.width * UvBoundSplitted(boundsUvValues.minorX)[1]), targetTexture.height, 0);
                for (int i = 0; i < UvBoundSplitted(boundsUvValues.spaceMinorY)[0] + UvBoundSplitted(boundsUvValues.spaceMajorY)[0] + 1; i++)
                    thisTexture.SetPixels(
                        edgesSize, edgesSize + UvBoundToPixels(UvBoundSplitted(boundsUvValues.spaceMinorY)[1], targetTexture.height) + (i * targetTexture.height),
                        UvBoundToPixels(UvBoundSplitted(boundsUvValues.spaceMinorX)[1], targetTexture.width), targetTexture.height, tempColorBlock, 0);

                //Fill the texture with repeated original textures
                tempColorBlock = targetTexture.GetPixels(0, 0, targetTexture.width, targetTexture.height, 0);
                for (int x = 0; x < UvBoundSplitted(boundsUvValues.spaceMinorX)[0] + UvBoundSplitted(boundsUvValues.spaceMajorX)[0] + 1; x++)
                    for (int y = 0; y < UvBoundSplitted(boundsUvValues.spaceMinorY)[0] + UvBoundSplitted(boundsUvValues.spaceMajorY)[0] + 1; y++)
                        thisTexture.SetPixels(
                            edgesSize + (int)((float)targetTexture.width * UvBoundSplitted(boundsUvValues.spaceMinorX)[1]) + (x * targetTexture.width),
                            edgesSize + (int)((float)targetTexture.height * UvBoundSplitted(boundsUvValues.spaceMinorY)[1]) + (y * targetTexture.height),
                            targetTexture.width, targetTexture.height, tempColorBlock, 0);

                //Add the right border
                tempColorBlock = targetTexture.GetPixels(0, 0, (int)((float)targetTexture.width * UvBoundSplitted(boundsUvValues.majorX)[1]), targetTexture.height, 0);
                for (int i = 0; i < UvBoundSplitted(boundsUvValues.spaceMinorY)[0] + UvBoundSplitted(boundsUvValues.spaceMajorY)[0] + 1; i++)
                    thisTexture.SetPixels(
                        edgesSize + (int)((float)targetTexture.width * UvBoundSplitted(boundsUvValues.spaceMinorX)[1]) + (((int)UvBoundSplitted(boundsUvValues.spaceMinorX)[0] + (int)UvBoundSplitted(boundsUvValues.spaceMajorX)[0] + 1) * targetTexture.width),
                        edgesSize + UvBoundToPixels(UvBoundSplitted(boundsUvValues.spaceMinorY)[1], targetTexture.height) + (i * targetTexture.height),
                        UvBoundToPixels(UvBoundSplitted(boundsUvValues.spaceMajorX)[1], targetTexture.width), targetTexture.height, tempColorBlock, 0);

                //Add the bottom border
                tempColorBlock = targetTexture.GetPixels(
                    0, targetTexture.height - (int)((float)targetTexture.height * UvBoundSplitted(boundsUvValues.minorY)[1]),
                    targetTexture.width, (int)((float)targetTexture.height * UvBoundSplitted(boundsUvValues.minorY)[1]), 0);
                for (int i = 0; i < UvBoundSplitted(boundsUvValues.spaceMinorX)[0] + UvBoundSplitted(boundsUvValues.spaceMajorX)[0] + 1; i++)
                    thisTexture.SetPixels(
                        edgesSize + UvBoundToPixels(UvBoundSplitted(boundsUvValues.spaceMinorX)[1], targetTexture.width) + (i * targetTexture.width), edgesSize,
                        targetTexture.width, UvBoundToPixels(UvBoundSplitted(boundsUvValues.spaceMinorY)[1], targetTexture.height), tempColorBlock, 0);

                //Add the top border
                tempColorBlock = targetTexture.GetPixels(0, 0, targetTexture.width, (int)((float)targetTexture.height * UvBoundSplitted(boundsUvValues.majorY)[1]), 0);
                for (int i = 0; i < UvBoundSplitted(boundsUvValues.spaceMinorX)[0] + UvBoundSplitted(boundsUvValues.spaceMajorX)[0] + 1; i++)
                    thisTexture.SetPixels(
                        edgesSize + UvBoundToPixels(UvBoundSplitted(boundsUvValues.spaceMinorX)[1], targetTexture.width) + (i * targetTexture.width),
                        edgesSize + (int)((float)targetTexture.height * UvBoundSplitted(boundsUvValues.spaceMinorY)[1]) + (((int)UvBoundSplitted(boundsUvValues.spaceMinorY)[0] + (int)UvBoundSplitted(boundsUvValues.spaceMajorY)[0] + 1) * targetTexture.height),
                        targetTexture.width, UvBoundToPixels(UvBoundSplitted(boundsUvValues.spaceMajorY)[1], targetTexture.height), tempColorBlock, 0);

                //Add the bottom left corner
                tempColorBlock = targetTexture.GetPixels(
                    targetTexture.width - (int)((float)targetTexture.width * UvBoundSplitted(boundsUvValues.minorX)[1]),
                    targetTexture.height - (int)((float)targetTexture.height * UvBoundSplitted(boundsUvValues.minorY)[1]),
                    (int)((float)targetTexture.width * UvBoundSplitted(boundsUvValues.minorX)[1]),
                    (int)((float)targetTexture.height * UvBoundSplitted(boundsUvValues.minorY)[1]),
                    0);
                thisTexture.SetPixels(edgesSize, edgesSize, (int)((float)targetTexture.width * UvBoundSplitted(boundsUvValues.minorX)[1]), (int)((float)targetTexture.height * UvBoundSplitted(boundsUvValues.minorY)[1]), tempColorBlock, 0);

                //Add the bottom right corner
                tempColorBlock = targetTexture.GetPixels(
                    0,
                    targetTexture.height - (int)((float)targetTexture.height * UvBoundSplitted(boundsUvValues.minorY)[1]),
                    (int)((float)targetTexture.width * UvBoundSplitted(boundsUvValues.majorX)[1]),
                    (int)((float)targetTexture.height * UvBoundSplitted(boundsUvValues.minorY)[1]),
                    0);
                thisTexture.SetPixels(
                    thisTexture.width - edgesSize - (int)((float)targetTexture.width * UvBoundSplitted(boundsUvValues.majorX)[1]), edgesSize,
                    (int)((float)targetTexture.width * UvBoundSplitted(boundsUvValues.majorX)[1]), (int)((float)targetTexture.height * UvBoundSplitted(boundsUvValues.minorY)[1]), tempColorBlock, 0);

                //Add the top left corner
                tempColorBlock = targetTexture.GetPixels(
                    targetTexture.width - (int)((float)targetTexture.width * UvBoundSplitted(boundsUvValues.minorX)[1]),
                    0,
                    (int)((float)targetTexture.width * UvBoundSplitted(boundsUvValues.minorX)[1]),
                    (int)((float)targetTexture.height * UvBoundSplitted(boundsUvValues.majorY)[1]),
                    0);
                thisTexture.SetPixels(
                    edgesSize, thisTexture.height - edgesSize - (int)((float)targetTexture.height * UvBoundSplitted(boundsUvValues.majorY)[1]),
                    (int)((float)targetTexture.width * UvBoundSplitted(boundsUvValues.minorX)[1]), (int)((float)targetTexture.height * UvBoundSplitted(boundsUvValues.majorY)[1]), tempColorBlock, 0);

                //Add the top right corner
                tempColorBlock = targetTexture.GetPixels(
                    0,
                    0,
                    (int)((float)targetTexture.width * UvBoundSplitted(boundsUvValues.majorX)[1]),
                    (int)((float)targetTexture.height * UvBoundSplitted(boundsUvValues.majorY)[1]),
                    0);
                thisTexture.SetPixels(
                    thisTexture.width - edgesSize - (int)((float)targetTexture.width * UvBoundSplitted(boundsUvValues.majorX)[1]), thisTexture.height - edgesSize - (int)((float)targetTexture.height * UvBoundSplitted(boundsUvValues.majorY)[1]),
                    (int)((float)targetTexture.width * UvBoundSplitted(boundsUvValues.majorX)[1]), (int)((float)targetTexture.height * UvBoundSplitted(boundsUvValues.majorY)[1]), tempColorBlock, 0);
            }

            //-------------------------------------------- Create the edges of copy texture, to support mip maps
            //If the edges size is minor than target texture size, uses the "SetPixels and GetPixels" to guarantee a faster copy
            if (edgesSize <= targetTexture.width && edgesSize <= targetTexture.height && overcameTheLimitationOf16k == false)
            {
                //Prepare the var
                Color[] copyiedPixels = null;

                //Copy right border to left of current texture
                copyiedPixels = thisTexture.GetPixels(thisTexture.width - edgesSize - edgesSize, 0, edgesSize, thisTexture.height, 0);
                thisTexture.SetPixels(0, 0, edgesSize, thisTexture.height, copyiedPixels, 0);

                //Copy left(original) border to right of current texture
                copyiedPixels = thisTexture.GetPixels(edgesSize, 0, edgesSize, thisTexture.height, 0);
                thisTexture.SetPixels(thisTexture.width - edgesSize, 0, edgesSize, thisTexture.height, copyiedPixels, 0);

                //Copy bottom (original) border to top of current texture
                copyiedPixels = thisTexture.GetPixels(0, edgesSize, thisTexture.width, edgesSize, 0);
                thisTexture.SetPixels(0, thisTexture.height - edgesSize, thisTexture.width, edgesSize, copyiedPixels, 0);

                //Copy top (original) border to bottom of current texture
                copyiedPixels = thisTexture.GetPixels(0, thisTexture.height - edgesSize - edgesSize, thisTexture.width, edgesSize, 0);
                thisTexture.SetPixels(0, 0, thisTexture.width, edgesSize, copyiedPixels, 0);
            }

            //If the edges size is major than target texture size, uses the "SetPixel and GetPixel" to repeat copy of pixels in target texture
            if (edgesSize > targetTexture.width || edgesSize > targetTexture.height && overcameTheLimitationOf16k == false)
            {
                //Copy right (original) border to left of current texture
                for (int x = 0; x < edgesSize; x++)
                    for (int y = 0; y < thisTexture.height; y++)
                        thisTexture.SetPixel(x, y, targetTexture.GetPixel((targetTexture.width - edgesSize - edgesSize) + x, y));

                //Copy left(original) border to right of current texture
                for (int x = thisTexture.width - edgesSize; x < thisTexture.width; x++)
                    for (int y = 0; y < thisTexture.height; y++)
                        thisTexture.SetPixel(x, y, targetTexture.GetPixel(targetTexture.width - x, y));

                //Copy bottom (original) border to top of current texture
                for (int x = 0; x < thisTexture.width; x++)
                    for (int y = 0; y < edgesSize; y++)
                        thisTexture.SetPixel(x, y, targetTexture.GetPixel(x, (targetTexture.width - edgesSize) + y));

                //Copy top (original) border to bottom of current texture
                for (int x = 0; x < thisTexture.width; x++)
                    for (int y = thisTexture.height - edgesSize; y < thisTexture.height; y++)
                        thisTexture.SetPixel(x, y, targetTexture.GetPixel(x, edgesSize - (targetTexture.height - y)));
            }
        }
        //If the texture is not readable
        catch (UnityException e)
        {
            if (e.Message.StartsWith("Texture '" + targetTexture.name + "' is not readable"))
            {
                //Get the default and neutral color for this texture
                ColorData defaultColor = GetDefaultAndNeutralColorForThisTexture(textureType);

                //Create the texture
                thisTexture = new Texture2D(widthOfCorrespondentMainTexture, heightOfCorrespondentMainTexture, TextureFormat.ARGB32, false, false);

                //Create blank pixels
                Color[] colors = new Color[widthOfCorrespondentMainTexture * heightOfCorrespondentMainTexture];
                for (int i = 0; i < colors.Length; i++)
                    colors[i] = defaultColor.color;

                //Apply all pixels in void texture
                thisTexture.SetPixels(0, 0, widthOfCorrespondentMainTexture, heightOfCorrespondentMainTexture, colors, 0);
            }
        }

        //-------------------------------------------- Calculate the use of edges of this texture, in percent
        //Only calculate if is the main texture, because main texture is more important and is the base texture for all uv mapping and calcs
        if (textureType == TextureType.MainTexture)
        {
            boundsUvValues.edgesUseX = (float)GetEdgesSizeForTextures() / (float)thisTexture.width;
            boundsUvValues.edgesUseY = (float)GetEdgesSizeForTextures() / (float)thisTexture.height;
        }

        //-------------------------------------------- Finally, resize the copy texture to mantain size equal to targe texture with edges
        //If this texture have the size differente of correspondent main texture size, resize it to be equal to main texture 
        if (thisTexture.width != widthOfCorrespondentMainTexture || thisTexture.height != heightOfCorrespondentMainTexture)
            SMCTextureResizer.Bilinear(thisTexture, widthOfCorrespondentMainTexture, heightOfCorrespondentMainTexture);

        //Return the texture 
        return thisTexture;
    }
    private float[] UvBoundSplitted(float uvSize)
    {
        //Convert to positive
        if (uvSize < 0.0f)
            uvSize = uvSize * -1.0f;
        //Result
        float[] result = new float[2];
        //Split
        string[] str = uvSize.ToString().Split(',');
        //Get result
        result[0] = float.Parse(str[0]);
        result[1] = 0.0f;
        if (str.Length > 1)
            result[1] = float.Parse("0," + str[1]);
        return result;
    }
    private TexturesSubMeshes GetTheTextureSubMeshesOfMaterial(Material material, List<TexturesSubMeshes> listOfTexturesAndSubMeshes)
    {
        //Run a loop to return the texture and respective submeshes that use this material
        foreach (TexturesSubMeshes item in listOfTexturesAndSubMeshes)
            if (item.material == material && item.isTiledTexture == false)
                return item;

        //If not found a item with this material, return null
        return null;
    }
    private int UvBoundToPixels(float uvSize, int textureSize)
    {
        return (int)(uvSize * (float)textureSize);
    }
    private bool isTiledTexture(TexturesSubMeshes.UvBounds bounds)
    {
        //Return if the bounds is major than one
        if (bounds.minorX < 0 || bounds.minorY < 0 || bounds.majorX > 1 || bounds.majorY > 1)
            return true;
        if (bounds.minorX >= 0 && bounds.minorY >= 0 && bounds.majorX <= 1 && bounds.majorY <= 1)
            return false;
        return false;
    }
    private int GetEdgesSizeForTextures()
    {
        //If is All In One
        if (mergeMethod == MergeMethod.AllInOne)
        {
            switch (allInOneParams.mipMapEdgesSize)
            {
                case MipMapEdgesSize.Pixels0x0:
                    return 0;
                case MipMapEdgesSize.Pixels16x16:
                    return 16;
                case MipMapEdgesSize.Pixels32x32:
                    return 32;
                case MipMapEdgesSize.Pixels64x64:
                    return 64;
                case MipMapEdgesSize.Pixels128x128:
                    return 128;
                case MipMapEdgesSize.Pixels256x256:
                    return 256;
                case MipMapEdgesSize.Pixels512x512:
                    return 512;
                case MipMapEdgesSize.Pixels1024x1024:
                    return 1024;
            }
        }

        //Return the max resolution
        return 2;
    }
    
    private void ApplyAtlasInPropertyOfMaterial(Material targetMaterial, string propertyToInsertTexture, Texture2D atlasTexture)
    {
        //If found the property
        if (targetMaterial.HasProperty(propertyToInsertTexture) == true)
        {
            //Try to enable this different keyword
            if (targetMaterial.IsKeywordEnabled(propertyToInsertTexture) == false)
                targetMaterial.EnableKeyword(propertyToInsertTexture);

            //Apply the texture
            targetMaterial.SetTexture(propertyToInsertTexture, atlasTexture);

            //Try to enable this different keyword
            if (targetMaterial.IsKeywordEnabled(propertyToInsertTexture) == false)
                targetMaterial.EnableKeyword(propertyToInsertTexture);

            //Forces enable all keyword, where is necessary
            if (propertyToInsertTexture == "_MetallicGlossMap" && targetMaterial.IsKeywordEnabled("_METALLICGLOSSMAP") == false && allInOneParams.RMAESupport == true)
                targetMaterial.EnableKeyword("_METALLICGLOSSMAP");

            if (propertyToInsertTexture == "_SpecGlossMap" && targetMaterial.IsKeywordEnabled("_SPECGLOSSMAP") == false && allInOneParams.NormalMapSupport == true)
                targetMaterial.EnableKeyword("_SPECGLOSSMAP");

            if (propertyToInsertTexture == "_BumpMap" && targetMaterial.IsKeywordEnabled("_NORMALMAP") == false && allInOneParams.aux1 == true)
                targetMaterial.EnableKeyword("_NORMALMAP");

            if (propertyToInsertTexture == "_ParallaxMap" && targetMaterial.IsKeywordEnabled("_PARALLAXMAP") == false && allInOneParams.aux3Support == true)
                targetMaterial.EnableKeyword("_PARALLAXMAP");

            if (propertyToInsertTexture == "_OcclusionMap" && targetMaterial.IsKeywordEnabled("_OcclusionMap") == false && allInOneParams.aux4Support == true)
                targetMaterial.EnableKeyword("_OcclusionMap");

            if (propertyToInsertTexture == "_DetailAlbedoMap" && targetMaterial.IsKeywordEnabled("_DETAIL_MULX2") == false && allInOneParams.aux5MapSupport == true)
                targetMaterial.EnableKeyword("_DETAIL_MULX2");

            if (propertyToInsertTexture == "_DetailNormalMap" && targetMaterial.IsKeywordEnabled("_DETAIL_MULX2") == false && allInOneParams.aux2Support == true)
                targetMaterial.EnableKeyword("_DETAIL_MULX2");
        }
    }
    private void SaveAssetAsFile(string folderNameToSave, UnityEngine.Object assetToSave, string fileName, string fileExtension, bool isPNG)
    {
#if UNITY_EDITOR
        // Si el juego está ejecutándose, cancelar guardado
        if (Application.isPlaying == true)
        {
            return;
        }

        // Construir el directorio completo
        string fullPath = saveFolder + "/" + folderNameToSave;

        // Crear las carpetas si no existen
        CreateFolderIfNotExists(fullPath);

        // Guardar el archivo
        string fileDirectory = fullPath + "/" + fileName + "." + fileExtension;

        if (isPNG)
        {
            // Convertir la textura en PNG y guardar
            if (assetToSave is Texture2D texture)
            {
                byte[] pngData = texture.EncodeToPNG();
                if (pngData != null)
                {
                    fileDirectory = fullPath + "/" + fileName + ".png";
                    // Escribir el archivo PNG en la ubicación deseada
                    File.WriteAllBytes(fileDirectory, pngData);
                    Debug.Log("Texture saved as PNG: " + fileDirectory);
                }
                else
                {
                    Debug.LogError("Failed to convert texture to PNG.");
                }
            }
            else
            {
                Debug.LogError("The asset is not a Texture2D. Cannot save as PNG.");
            }
        }
        else
        {
            // Guardar el asset de la forma predeterminada
            AssetDatabase.CreateAsset(assetToSave, fileDirectory);
        }

        resultMergeAssetsSaved.Add(fileDirectory);

        // Guardar todos los datos y recargar
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
#endif
    }

    private void CreateFolderIfNotExists(string fullPath)
    {
#if UNITY_EDITOR
        // Dividir la ruta en partes usando '/' como separador
        string[] folders = fullPath.Split('/');
        string currentPath = folders[0]; // Comienza con "Assets"

        // Iterar a través de cada carpeta en la ruta
        for (int i = 1; i < folders.Length; i++)
        {
            string folderToCheck = currentPath + "/" + folders[i];

            // Verificar si la carpeta ya existe
            if (!AssetDatabase.IsValidFolder(folderToCheck))
            {
                // Crear la carpeta si no existe
                AssetDatabase.CreateFolder(currentPath, folders[i]);
            }

            // Actualizar el path actual
            currentPath = folderToCheck;
        }
#endif
    }
    private void ValidateAllVariables()
    {
        //Validate all variables to avoid problems with merge

        //On merge with editor
        if (savePrefabOfMerge == true)
            saveDataInAssets = true;


        //If the merge name is empty, set as default
        if (String.IsNullOrEmpty(nameOfThisMerge) == true)
            nameOfThisMerge = "Combined Meshes";

        allInOneParams.ColorAlphaPropertyToFind = "_MainTex";
        allInOneParams.ColorAlphaPropertyToInsert = "_MainTex";
    }
#endif
    private class AtlasData
    {
        //This class store a atlas data
        public Texture2D mainTextureAtlas = new Texture2D(16, 16);
        public Texture2D metallicMapAtlas = new Texture2D(16, 16);
        public Texture2D specularMapAtlas = new Texture2D(16, 16);
        public Texture2D normalMapAtlas = new Texture2D(16, 16);
        public Texture2D normalMap2Atlas = new Texture2D(16, 16);
        public Texture2D heightMapAtlas = new Texture2D(16, 16);
        public Texture2D occlusionMapAtlas = new Texture2D(16, 16);
        public Texture2D detailMapAtlas = new Texture2D(16, 16);
        public Texture2D detailMaskAtlas = new Texture2D(16, 16);
        public Rect[] atlasRects = new Rect[0];
        public Texture2D[] originalMainTexturesUsedAndOrdenedAccordingToAtlasRect = new Texture2D[0];

        //Return the respective id of rect that the informed texture is posicioned
        public int GetRectIndexOfThatMainTexture(Texture2D texture)
        {
            //Prepare the storage
            int index = -1;

            foreach (Texture2D tex in originalMainTexturesUsedAndOrdenedAccordingToAtlasRect)
            {
                //Increase de index in onee
                index += 1;

                //If the texture informed is equal to original texture used, break this loop and return the respective index
                if (tex == texture)
                    break;
            }

            //Return the data
            return index;
        }
    }
    private enum TextureType
    {
        //This enum stores type of texture
        MainTexture,
        MetallicMap,
        SpecularMap,
        NormalMap,
        HeightMap,
        OcclusionMap,
        DetailMap,
        DetailMask
    }
    private class TexturesSubMeshes
    {
        public class UvBounds
        {
            //This class stores a data of size of a submesh uv, data like major value of x and y, etc
            public float majorX = 0;
            public float majorY = 0;
            public float minorX = 0;
            public float minorY = 0;
            public float spaceMinorX = 0;
            public float spaceMajorX = 0;
            public float spaceMinorY = 0;
            public float spaceMajorY = 0;
            public float edgesUseX = 0.0f;
            public float edgesUseY = 0.0f;

            public float Round(float value, int places)
            {
                return float.Parse(value.ToString("F" + places.ToString()));
            }

            public void RoundBoundsValuesAndCalculateSpaceNeededToTiling()
            {
                //Round all values
                majorX = Round(majorX, 4);
                majorY = Round(majorY, 4);
                minorX = Round(minorX, 4);
                minorY = Round(minorY, 4);

                //Calculate aditional space to left of texture
                if (minorX >= 0.0f)
                    spaceMinorX = 0.0f;
                if (minorX < 0.0f)
                    spaceMinorX = minorX * -1.0f;

                //Calculate aditional space to down of texture
                if (minorY >= 0.0f)
                    spaceMinorY = 0.0f;
                if (minorY < 0.0f)
                    spaceMinorY = minorY * -1.0f;

                //Calculate aditional space to up of texture
                if (majorY >= 1.0f)
                    spaceMajorY = majorY - 1.0f;

                //Calculate aditional space to right of texture
                if (majorX >= 1.0f)
                    spaceMajorX = majorX - 1.0f;
            }
        }

        public class UserSubMeshes
        {
            //This class stores data of a submesh that uses this texture
            public UvBounds uvBoundsOfThisSubMesh = new UvBounds();
            public int startOfUvVerticesInIndex = 0;
            public Vector2[] originalUvVertices = null;
        }

        //This class stores textures and all submeshes data that uses this texture. If is tilled texture, this is repeated and used only by one submesh
        public Material material;
        public Texture2D mainTexture;
        public Texture2D metallicMap;
        public Texture2D specularMap;
        public Texture2D normalMap;
        public Texture2D normalMap2;
        public Texture2D heightMap;
        public Texture2D occlusionMap;
        public Texture2D detailMap;
        public Texture2D detailMask;
        public bool isTiledTexture = false;
        public Vector2Int mainTextureResolution;
        public Vector2Int mainTextureResolutionWithEdges;
        public List<UserSubMeshes> userSubMeshes = new List<UserSubMeshes>();

        //Return the edges percent usage, getting from 0 submesh of this texture
        public Vector2 GetEdgesPercentUsageOfThisTextures()
        {
            return new Vector2(userSubMeshes[0].uvBoundsOfThisSubMesh.edgesUseX, userSubMeshes[0].uvBoundsOfThisSubMesh.edgesUseY);
        }

        //Convert all vertices of all submeshes to positive values
        public void ConvertAllSubMeshsVerticesToPositive()
        {
            //Convert all vertices for each submesh
            foreach (UserSubMeshes submesh in userSubMeshes)
            {
                //Calculate all minor values of vertices of this submehs
                float[] xAxis = new float[submesh.originalUvVertices.Length];
                float[] yAxis = new float[submesh.originalUvVertices.Length];
                for (int i = 0; i < submesh.originalUvVertices.Length; i++)
                {
                    xAxis[i] = submesh.originalUvVertices[i].x;
                    yAxis[i] = submesh.originalUvVertices[i].y;
                }
                Vector2 minorValues = new Vector2(Mathf.Min(xAxis), Mathf.Min(yAxis));

                //Modify all values of all vertices to positive
                for (int i = 0; i < submesh.originalUvVertices.Length; i++)
                {
                    //Get original value
                    Vector2 originalValue = submesh.originalUvVertices[i];

                    //Create the modifyied value
                    Vector2 newValue = Vector2.zero;

                    //Modify the value
                    if (originalValue.x >= 0.0f)
                        newValue.x = originalValue.x + ((minorValues.x < 0.0f) ? (minorValues.x * -1) : 0);
                    if (originalValue.y >= 0.0f)
                        newValue.y = originalValue.y + ((minorValues.y < 0.0f) ? (minorValues.y * -1) : 0);

                    //Convert all negative values to positive, and invert the values, to invert negative texture maping to positive
                    if (originalValue.x < 0.0f)
                        newValue.x = (minorValues.x * -1) - (originalValue.x * -1);
                    if (originalValue.y < 0.0f)
                        newValue.y = (minorValues.y * -1) - (originalValue.y * -1);

                    //Apply the new value
                    submesh.originalUvVertices[i] = newValue;
                }
            }
        }
    }
    private class ColorData
    {
        //This class stores a color and your respective name
        public string colorName;
        public Color color;

        public ColorData(string colorName, Color color)
        {
            this.colorName = colorName;
            this.color = color;
        }
    }
    private ColorData GetDefaultAndNeutralColorForThisTexture(TextureType textureType)
    {
        //Return the neutral color for texture type
        switch (textureType)
        {
            case TextureType.MainTexture:
                return new ColorData("RED", Color.red);
            case TextureType.MetallicMap:
                return new ColorData("BLACK", Color.black);
            case TextureType.SpecularMap:
                return new ColorData("BLACK", Color.black);
            case TextureType.NormalMap:
                return new ColorData("PURPLE", new Color(128.0f / 255.0f, 128.0f / 255.0f, 255.0f / 255.0f, 255.0f / 255.0f));
            case TextureType.HeightMap:
                return new ColorData("BLACK", Color.black);
            case TextureType.OcclusionMap:
                return new ColorData("WHITE", Color.white);
            case TextureType.DetailMap:
                return new ColorData("GRAY", Color.gray);
            case TextureType.DetailMask:
                return new ColorData("WHITE", Color.white);
        }
        return new ColorData("RED", Color.red);
    }
}
public class SMCTextureResizer
{
    public class ThreadData
    {
        public int start;
        public int end;
        public ThreadData(int s, int e)
        {
            start = s;
            end = e;
        }
    }

    private static Color[] texColors;
    private static Color[] newColors;
    private static int w;
    private static float ratioX;
    private static float ratioY;
    private static int w2;
    private static int finishCount;
    private static Mutex mutex;

    public static void Point(Texture2D tex, int newWidth, int newHeight)
    {
        ThreadedScale(tex, newWidth, newHeight, false);
    }

    public static void Bilinear(Texture2D tex, int newWidth, int newHeight)
    {
        ThreadedScale(tex, newWidth, newHeight, true);
    }

    private static void ThreadedScale(Texture2D tex, int newWidth, int newHeight, bool useBilinear)
    {
        texColors = tex.GetPixels();
        newColors = new Color[newWidth * newHeight];
        if (useBilinear)
        {
            ratioX = 1.0f / ((float)newWidth / (tex.width - 1));
            ratioY = 1.0f / ((float)newHeight / (tex.height - 1));
        }
        else
        {
            ratioX = ((float)tex.width) / newWidth;
            ratioY = ((float)tex.height) / newHeight;
        }
        w = tex.width;
        w2 = newWidth;
        var cores = Mathf.Min(SystemInfo.processorCount, newHeight);
        var slice = newHeight / cores;

        finishCount = 0;
        if (mutex == null)
        {
            mutex = new Mutex(false);
        }
        if (cores > 1)
        {
            int i = 0;
            ThreadData threadData;
            for (i = 0; i < cores - 1; i++)
            {
                threadData = new ThreadData(slice * i, slice * (i + 1));
                ParameterizedThreadStart ts = useBilinear ? new ParameterizedThreadStart(BilinearScale) : new ParameterizedThreadStart(PointScale);
                Thread thread = new Thread(ts);
                thread.Start(threadData);
            }
            threadData = new ThreadData(slice * i, newHeight);
            if (useBilinear)
            {
                BilinearScale(threadData);
            }
            else
            {
                PointScale(threadData);
            }
            while (finishCount < cores)
            {
                Thread.Sleep(1);
            }
        }
        else
        {
            ThreadData threadData = new ThreadData(0, newHeight);
            if (useBilinear)
            {
                BilinearScale(threadData);
            }
            else
            {
                PointScale(threadData);
            }
        }

        tex.Reinitialize(newWidth, newHeight);
        tex.SetPixels(newColors);
        tex.Apply();

        texColors = null;
        newColors = null;
    }

    public static void BilinearScale(System.Object obj)
    {
        ThreadData threadData = (ThreadData)obj;
        for (var y = threadData.start; y < threadData.end; y++)
        {
            int yFloor = (int)Mathf.Floor(y * ratioY);
            var y1 = yFloor * w;
            var y2 = (yFloor + 1) * w;
            var yw = y * w2;

            for (var x = 0; x < w2; x++)
            {
                int xFloor = (int)Mathf.Floor(x * ratioX);
                var xLerp = x * ratioX - xFloor;
                newColors[yw + x] = ColorLerpUnclamped(ColorLerpUnclamped(texColors[y1 + xFloor], texColors[y1 + xFloor + 1], xLerp), ColorLerpUnclamped(texColors[y2 + xFloor], texColors[y2 + xFloor + 1], xLerp), y * ratioY - yFloor);
            }
        }

        mutex.WaitOne();
        finishCount++;
        mutex.ReleaseMutex();
    }

    public static void PointScale(System.Object obj)
    {
        ThreadData threadData = (ThreadData)obj;
        for (var y = threadData.start; y < threadData.end; y++)
        {
            var thisY = (int)(ratioY * y) * w;
            var yw = y * w2;
            for (var x = 0; x < w2; x++)
            {
                newColors[yw + x] = texColors[(int)(thisY + ratioX * x)];
            }
        }

        mutex.WaitOne();
        finishCount++;
        mutex.ReleaseMutex();
    }

    private static Color ColorLerpUnclamped(Color c1, Color c2, float value)
    {
        return new Color(c1.r + (c2.r - c1.r) * value, c1.g + (c2.g - c1.g) * value, c1.b + (c2.b - c1.b) * value, c1.a + (c2.a - c1.a) * value);
    }
}

public static class SMCMeshClassExtension
{
    /*
     * This is an extension class, which adds extra functions to the Mesh class. For example, counting vertices for each submesh.
     */

    public class Vertices
    {
        List<Vector3> verts = null;
        List<Vector2> uv1 = null;
        List<Vector2> uv2 = null;
        List<Vector2> uv3 = null;
        List<Vector2> uv4 = null;
        List<Vector3> normals = null;
        List<Vector4> tangents = null;
        List<Color32> colors = null;
        List<BoneWeight> boneWeights = null;

        public Vertices()
        {
            verts = new List<Vector3>();
        }

        public Vertices(Mesh aMesh)
        {
            verts = CreateList(aMesh.vertices);
            uv1 = CreateList(aMesh.uv);
            uv2 = CreateList(aMesh.uv2);
            uv3 = CreateList(aMesh.uv3);
            uv4 = CreateList(aMesh.uv4);
            normals = CreateList(aMesh.normals);
            tangents = CreateList(aMesh.tangents);
            colors = CreateList(aMesh.colors32);
            boneWeights = CreateList(aMesh.boneWeights);
        }

        private List<T> CreateList<T>(T[] aSource)
        {
            if (aSource == null || aSource.Length == 0)
                return null;
            return new List<T>(aSource);
        }

        private void Copy<T>(ref List<T> aDest, List<T> aSource, int aIndex)
        {
            if (aSource == null)
                return;
            if (aDest == null)
                aDest = new List<T>();
            aDest.Add(aSource[aIndex]);
        }

        public int Add(Vertices aOther, int aIndex)
        {
            int i = verts.Count;
            Copy(ref verts, aOther.verts, aIndex);
            Copy(ref uv1, aOther.uv1, aIndex);
            Copy(ref uv2, aOther.uv2, aIndex);
            Copy(ref uv3, aOther.uv3, aIndex);
            Copy(ref uv4, aOther.uv4, aIndex);
            Copy(ref normals, aOther.normals, aIndex);
            Copy(ref tangents, aOther.tangents, aIndex);
            Copy(ref colors, aOther.colors, aIndex);
            Copy(ref boneWeights, aOther.boneWeights, aIndex);
            return i;
        }

        public void AssignTo(Mesh aTarget)
        {
            //Removes the limitation of 65k vertices, in case Unity supports.
            if (verts.Count > 65535)
                aTarget.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            aTarget.SetVertices(verts);
            if (uv1 != null) aTarget.SetUVs(0, uv1);
            if (uv2 != null) aTarget.SetUVs(1, uv2);
            if (uv3 != null) aTarget.SetUVs(2, uv3);
            if (uv4 != null) aTarget.SetUVs(3, uv4);
            if (normals != null) aTarget.SetNormals(normals);
            if (tangents != null) aTarget.SetTangents(tangents);
            if (colors != null) aTarget.SetColors(colors);
            if (boneWeights != null) aTarget.boneWeights = boneWeights.ToArray();
        }
    }

    //Return count of vertices for submesh
    public static Mesh SMCGetSubmesh(this Mesh aMesh, int aSubMeshIndex)
    {
        if (aSubMeshIndex < 0 || aSubMeshIndex >= aMesh.subMeshCount)
            return null;
        int[] indices = aMesh.GetTriangles(aSubMeshIndex);
        Vertices source = new Vertices(aMesh);
        Vertices dest = new Vertices();
        Dictionary<int, int> map = new Dictionary<int, int>();
        int[] newIndices = new int[indices.Length];
        for (int i = 0; i < indices.Length; i++)
        {
            int o = indices[i];
            int n;
            if (!map.TryGetValue(o, out n))
            {
                n = dest.Add(source, o);
                map.Add(o, n);
            }
            newIndices[i] = n;
        }
        Mesh m = new Mesh();
        dest.AssignTo(m);
        m.triangles = newIndices;
        return m;
    }
}
#endif