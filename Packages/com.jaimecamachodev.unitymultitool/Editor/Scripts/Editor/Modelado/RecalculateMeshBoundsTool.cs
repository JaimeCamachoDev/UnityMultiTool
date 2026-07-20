using UnityEditor;
using UnityEngine;

namespace VZ_Optizone
{
    public static class RecalculateMeshBoundsTool
    {
        private static MeshFilter targetMeshFilter;
        private static Bounds editableBounds;
        private static bool boundsInitialized = false; // Evita el autocalculado inicial

        public static void DrawTool()
        {
            GUILayout.Label("Mesh Bounds Adjuster", EditorStyles.boldLabel);

            // Campo para seleccionar el MeshFilter
            MeshFilter newMeshFilter = (MeshFilter)EditorGUILayout.ObjectField("Target MeshFilter", targetMeshFilter, typeof(MeshFilter), true);

            // Si cambiamos de objeto, reinicializamos los bounds
            if (newMeshFilter != targetMeshFilter)
            {
                targetMeshFilter = newMeshFilter;
                if (targetMeshFilter != null && targetMeshFilter.sharedMesh != null)
                {
                    //editableBounds = targetMeshFilter.sharedMesh.bounds; // Inicializamos al seleccionar un objeto
                    boundsInitialized = true;
                }
                else
                {
                    boundsInitialized = false;
                }
            }

            if (targetMeshFilter == null)
            {
                EditorGUILayout.HelpBox("Drag a GameObject with a MeshFilter here.", MessageType.Info);
                return;
            }

            // **Mostrar y permitir la edición de los bounds actuales**
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

                // Aplicar los nuevos bounds
                mesh.bounds = editableBounds;

                // Forzar la actualización en el editor
                EditorUtility.SetDirty(targetMeshFilter);
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
            if (targetMeshFilter != null)
            {
                editableBounds = targetMeshFilter.sharedMesh.bounds;
                boundsInitialized = true;
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
