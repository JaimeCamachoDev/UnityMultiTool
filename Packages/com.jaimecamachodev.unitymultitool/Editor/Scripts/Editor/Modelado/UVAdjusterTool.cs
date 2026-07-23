using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
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
            root.Add(new HelpBox("Esta herramienta modifica las UVs directamente sobre el Mesh asset compartido: afecta a todos los objetos que usen esa misma malla en el proyecto.", HelpBoxMessageType.Info));

            var statusContainer = new VisualElement { style = { marginTop = 6 } };

            MTUIActionButton adjustButton = null;
            MTUIActionButton undoButton = null;

            void RefreshStatus()
            {
                statusContainer.Clear();

                bool hasValidFilters = selectedMeshFilters.Any(f => f != null);
                bool hasValidGrid = rows > 0 && columns > 0;
                bool hasBackup = originalUVs.Count > 0;

                if (!hasValidFilters)
                {
                    statusContainer.Add(new HelpBox("Añade al menos un Mesh Filter para poder ajustar sus UVs.", HelpBoxMessageType.Info));
                }
                else if (!hasValidGrid)
                {
                    statusContainer.Add(new HelpBox("Rows y Columns deben ser mayores que 0.", HelpBoxMessageType.Warning));
                }

                adjustButton.SetAvailable(hasValidFilters && hasValidGrid);
                undoButton.SetAvailable(hasBackup);
            }

            // Input para las filas y columnas
            var rowsField = new IntegerField("Rows") { value = rows };
            rowsField.RegisterValueChangedCallback(evt => { rows = evt.newValue; RefreshStatus(); });
            root.Add(rowsField);

            var columnsField = new IntegerField("Columns") { value = columns };
            columnsField.RegisterValueChangedCallback(evt => { columns = evt.newValue; RefreshStatus(); });
            root.Add(columnsField);

            // Input para la posición en la cuadrícula
            var gridXField = new IntegerField("Grid X") { value = gridX };
            gridXField.RegisterValueChangedCallback(evt => gridX = evt.newValue);
            root.Add(gridXField);

            var gridYField = new IntegerField("Grid Y") { value = gridY };
            gridYField.RegisterValueChangedCallback(evt => gridY = evt.newValue);
            root.Add(gridYField);

            // Mostrar lista de Mesh Filters seleccionados
            root.Add(new Label("Select Mesh Filters") { style = { marginTop = 10 } });

            var listContainer = new VisualElement();

            void RefreshList()
            {
                listContainer.Clear();

                for (int i = 0; i < selectedMeshFilters.Count; i++)
                {
                    int index = i;
                    var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 2 } };

                    // Campo para seleccionar un Mesh Filter
                    var filterField = new ObjectField { objectType = typeof(MeshFilter), allowSceneObjects = true, value = selectedMeshFilters[index] };
                    filterField.style.flexGrow = 1;
                    filterField.RegisterValueChangedCallback(evt =>
                    {
                        selectedMeshFilters[index] = evt.newValue as MeshFilter;
                        RefreshStatus();
                    });
                    row.Add(filterField);

                    // Botón para eliminar el Mesh Filter de la lista
                    var removeButton = new MTUIActionButton("Remove", () =>
                    {
                        selectedMeshFilters.RemoveAt(index);
                        RefreshList();
                        RefreshStatus();
                    }, MTUIColors.NeutralBackground, MTUIColors.NeutralBorder, MTUIColors.NeutralText);
                    removeButton.style.width = 70;
                    row.Add(removeButton);

                    listContainer.Add(row);
                }
            }

            // Botón para agregar un nuevo Mesh Filter
            root.Add(new MTUIActionButton("Add Mesh Filter", () =>
            {
                selectedMeshFilters.Add(null);
                RefreshList();
                RefreshStatus();
            }));

            root.Add(listContainer);
            root.Add(statusContainer);

            // Botón para ajustar UVs
            adjustButton = new MTUIActionButton("Adjust UVs", () =>
            {
                AdjustUVs();
                RefreshStatus();
            });
            adjustButton.style.marginTop = 6;
            root.Add(adjustButton);

            // Botón para deshacer los cambios
            undoButton = new MTUIActionButton("Undo last change", () =>
            {
                UndoUVChanges();
                RefreshStatus();
            }, MTUIColors.NeutralBackground, MTUIColors.NeutralBorder, MTUIColors.NeutralText);
            root.Add(undoButton);

            RefreshList();
            RefreshStatus();

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
