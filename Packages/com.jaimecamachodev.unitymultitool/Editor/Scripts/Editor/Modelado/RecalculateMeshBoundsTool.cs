using UnityEditor;
using UnityEngine;

namespace JaimeCamachoDev.Multitool.Modeling
{
    public static class RecalculateMeshBoundsTool
    {
        private static MeshFilter targetMeshFilter;
        private static Bounds editableBounds;

        public static void DrawTool()
        {
            GUILayout.Label("Mesh Bounds Adjuster", EditorStyles.boldLabel);

            // Campo para seleccionar el MeshFilter
            MeshFilter newMeshFilter = (MeshFilter)EditorGUILayout.ObjectField("Target MeshFilter", targetMeshFilter, typeof(MeshFilter), true);

            // Si cambiamos de objeto, reinicializamos los bounds con los del mesh actual
            if (newMeshFilter != targetMeshFilter)
            {
                targetMeshFilter = newMeshFilter;
                if (targetMeshFilter != null && targetMeshFilter.sharedMesh != null)
                {
                    editableBounds = targetMeshFilter.sharedMesh.bounds;
                }
            }

            if (targetMeshFilter == null)
            {
                EditorGUILayout.HelpBox("Drag a GameObject with a MeshFilter here.", MessageType.Info);
                return;
            }

            if (targetMeshFilter.sharedMesh == null)
            {
                EditorGUILayout.HelpBox("The selected GameObject's MeshFilter has no Mesh assigned.", MessageType.Warning);
                return;
            }

            // Mostrar y permitir la edición de los bounds actuales
            Vector3 newCenter = EditorGUILayout.Vector3Field("Bounds Center", editableBounds.center);
            Vector3 newSize = EditorGUILayout.Vector3Field("Bounds Size", editableBounds.size);

            // Solo actualizar si el usuario hace cambios
            if (newCenter != editableBounds.center || newSize != editableBounds.size)
            {
                editableBounds.center = newCenter;
                editableBounds.size = newSize;
            }

            // Botón para aplicar los cambios
            if (GUILayout.Button("Apply Bounds"))
            {
                ApplyBounds();
            }

            // Botón para resetear los bounds originales
            if (GUILayout.Button("Reset Bounds to last saved Mesh Bounds"))
            {
                ResetBounds();
            }
        }

        private static void ApplyBounds()
        {
            if (targetMeshFilter != null && targetMeshFilter.sharedMesh != null)
            {
                Mesh mesh = targetMeshFilter.sharedMesh;

                Undo.RecordObject(mesh, "Adjust Mesh Bounds");
                mesh.bounds = editableBounds;

                // El objeto realmente modificado es el Mesh, no el MeshFilter que lo referencia
                EditorUtility.SetDirty(mesh);
                if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(mesh)))
                {
                    AssetDatabase.SaveAssets();
                }

                Debug.Log("Bounds updated! New size: " + mesh.bounds.size);
            }
        }

        private static void ResetBounds()
        {
            if (targetMeshFilter != null && targetMeshFilter.sharedMesh != null)
            {
                editableBounds = targetMeshFilter.sharedMesh.bounds;
                Debug.Log("Bounds reset to original mesh bounds.");
            }
        }

        public static void SetTarget(MeshFilter meshFilter)
        {
            targetMeshFilter = meshFilter;
            if (targetMeshFilter != null && targetMeshFilter.sharedMesh != null)
            {
                editableBounds = targetMeshFilter.sharedMesh.bounds;
            }
        }

        // Dibuja el Bound en la SceneView
        public static void OnSceneGUI(SceneView sceneView)
        {
            if (targetMeshFilter != null && targetMeshFilter.sharedMesh != null)
            {
                Handles.color = Color.yellow;
                Handles.DrawWireCube(targetMeshFilter.transform.TransformPoint(editableBounds.center),
                                     targetMeshFilter.transform.TransformVector(editableBounds.size));
            }
        }

        // Habilita los Handles en la escena
        public static void EnableSceneView()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            SceneView.RepaintAll(); // Asegura la actualización en la vista
        }

        // Deshabilita los Handles en la escena
        public static void DisableSceneView()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.RepaintAll();
        }
    }
}
