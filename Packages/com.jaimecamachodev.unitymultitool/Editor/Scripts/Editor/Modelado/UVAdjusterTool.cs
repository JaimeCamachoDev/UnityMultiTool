using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using JaimeCamachoDev.Multitool.UI;
using System.Collections.Generic;
using System.Linq;

namespace JaimeCamachoDev.Multitool.Modeling
{
    public static class UVAdjusterTool
    {
        // Variables estáticas para las filas y columnas
        private static int rows = 1;
        private static int columns = 1;

        // Variables estáticas para la posición en la cuadrícula
        private static int gridX = 0;
        private static int gridY = 0;

        // Lista estática para almacenar los Mesh Filters seleccionados
        private static readonly List<MeshFilter> selectedMeshFilters = new List<MeshFilter>();

        // Diccionario para almacenar las UV originales antes de cualquier modificación
        private static readonly Dictionary<MeshFilter, Vector2[]> originalUVs = new Dictionary<MeshFilter, Vector2[]>();

        // Construye la interfaz de la herramienta (UI Toolkit)
        public static VisualElement CreateGUI()
        {
            var root = new VisualElement();
            root.Add(new Label("UV Adjuster Tool") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 6 } });
            root.Add(new HelpBox("Selecciona en la escena uno o más objetos con MeshFilter. Esta herramienta modifica las UVs directamente sobre el Mesh asset compartido: afecta a todos los objetos que usen esa misma malla en el proyecto.", HelpBoxMessageType.Info));

            var contentContainer = new VisualElement { style = { marginTop = 6 } };
            root.Add(contentContainer);

            void RefreshContent()
            {
                contentContainer.Clear();

                selectedMeshFilters.Clear();
                foreach (GameObject go in Selection.gameObjects)
                {
                    MeshFilter filter = go != null ? go.GetComponent<MeshFilter>() : null;
                    if (filter != null)
                    {
                        selectedMeshFilters.Add(filter);
                    }
                }

                if (selectedMeshFilters.Count == 0)
                {
                    contentContainer.Add(new HelpBox("Selecciona uno o más objetos con MeshFilter en la escena para continuar.", HelpBoxMessageType.Warning));
                    return;
                }

                var selectionPanel = new MTUIPanel("Objetos seleccionados");
                foreach (MeshFilter filter in selectedMeshFilters)
                {
                    selectionPanel.Add(new MTUIInfoLabel("• " + filter.gameObject.name));
                }
                contentContainer.Add(selectionPanel);

                var statusContainer = new VisualElement { style = { marginTop = 6 } };

                MTUIActionButton adjustButton = null;
                MTUIActionButton undoButton = null;

                void RefreshStatus()
                {
                    statusContainer.Clear();

                    bool hasValidGrid = rows > 0 && columns > 0;
                    bool hasBackup = selectedMeshFilters.Any(f => f != null && originalUVs.ContainsKey(f));

                    if (!hasValidGrid)
                    {
                        statusContainer.Add(new HelpBox("Rows y Columns deben ser mayores que 0.", HelpBoxMessageType.Warning));
                    }

                    adjustButton.SetAvailable(hasValidGrid);
                    undoButton.SetAvailable(hasBackup);
                }

                // Input para las filas y columnas
                var rowsField = new IntegerField("Rows") { value = rows };
                rowsField.RegisterValueChangedCallback(evt => { rows = evt.newValue; RefreshStatus(); });
                contentContainer.Add(rowsField);

                var columnsField = new IntegerField("Columns") { value = columns };
                columnsField.RegisterValueChangedCallback(evt => { columns = evt.newValue; RefreshStatus(); });
                contentContainer.Add(columnsField);

                // Input para la posición en la cuadrícula
                var gridXField = new IntegerField("Grid X") { value = gridX };
                gridXField.RegisterValueChangedCallback(evt => gridX = evt.newValue);
                contentContainer.Add(gridXField);

                var gridYField = new IntegerField("Grid Y") { value = gridY };
                gridYField.RegisterValueChangedCallback(evt => gridY = evt.newValue);
                contentContainer.Add(gridYField);

                contentContainer.Add(statusContainer);

                // Botón para ajustar UVs
                adjustButton = new MTUIActionButton("Adjust UVs", () =>
                {
                    AdjustUVs();
                    RefreshStatus();
                });
                adjustButton.style.marginTop = 6;
                contentContainer.Add(adjustButton);

                // Botón para deshacer los cambios
                undoButton = new MTUIActionButton("Undo last change", () =>
                {
                    UndoUVChanges();
                    RefreshStatus();
                }, MTUIColors.NeutralBackground, MTUIColors.NeutralBorder, MTUIColors.NeutralText);
                contentContainer.Add(undoButton);

                RefreshStatus();
            }

            // Sigue la selección de la escena mientras la herramienta esté abierta
            root.RegisterCallback<AttachToPanelEvent>(_ => Selection.selectionChanged += RefreshContent);
            root.RegisterCallback<DetachFromPanelEvent>(_ => Selection.selectionChanged -= RefreshContent);

            RefreshContent();

            return root;
        }

        // Método para ajustar las UVs de las mallas seleccionadas
        private static void AdjustUVs()
        {
            if (selectedMeshFilters.Count == 0)
            {
                Debug.LogError("No Mesh Filters!");
                return;
            }

            if (rows <= 0 || columns <= 0)
            {
                Debug.LogError("Rows and colums must be greater than 0.");
                return;
            }

            foreach (var meshFilter in selectedMeshFilters)
            {
                if (meshFilter == null || meshFilter.sharedMesh == null)
                    continue;

                // Obtener la malla
                Mesh mesh = meshFilter.sharedMesh;
                Vector2[] uvs = mesh.uv;

                if (uvs.Length == 0)
                {
                    Debug.LogWarning($"'{mesh.name}' no tiene UVs; se omite.");
                    continue;
                }

                // Almacenar las UV originales antes de modificarlas
                if (!originalUVs.ContainsKey(meshFilter))
                {
                    originalUVs[meshFilter] = (Vector2[])uvs.Clone();
                }

                // Calcular el tamaño del cuadrado de UV
                float uvWidth = 1.0f / columns;
                float uvHeight = 1.0f / rows;

                // Calcular el offset basado en la posición de la cuadrícula
                Vector2 offset = new Vector2(gridX * uvWidth, gridY * uvHeight);

                // Ajustar las UVs
                for (int i = 0; i < uvs.Length; i++)
                {
                    uvs[i] = new Vector2(
                        uvs[i].x * uvWidth + offset.x,
                        uvs[i].y * uvHeight + offset.y
                    );
                }

                // Asignar las nuevas UVs a la malla
                Undo.RecordObject(mesh, "Adjust UVs");
                mesh.uv = uvs;

                // Marcar la malla como modificada y guardar los cambios
                EditorUtility.SetDirty(mesh);
                string path = AssetDatabase.GetAssetPath(mesh);
                if (!string.IsNullOrEmpty(path))
                {
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    Debug.Log("UVs adjusted and saved: " + mesh.name);
                }
                else
                {
                    Debug.LogError("Error saving.");
                }
            }
        }

        // Método para deshacer los cambios en las UVs
        private static void UndoUVChanges()
        {
            foreach (var meshFilter in selectedMeshFilters)
            {
                if (meshFilter == null || meshFilter.sharedMesh == null || !originalUVs.ContainsKey(meshFilter))
                    continue;

                // Obtener la malla y las UV originales
                Mesh mesh = meshFilter.sharedMesh;
                Undo.RecordObject(mesh, "Restore Original UVs");
                mesh.uv = originalUVs[meshFilter];

                // Marcar la malla como modificada y guardar los cambios
                EditorUtility.SetDirty(mesh);
                string path = AssetDatabase.GetAssetPath(mesh);
                if (!string.IsNullOrEmpty(path))
                {
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    Debug.Log("UVs restored: " + mesh.name);
                }
                else
                {
                    Debug.LogError("Error restoring.");
                }
            }
        }
    }
}
