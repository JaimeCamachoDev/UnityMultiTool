using JaimeCamachoDev.Multitool.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace JaimeCamachoDev.Multitool.Misc
{
    public static class SceneStructureCreatorTool
    {
        public static VisualElement CreateGUI()
        {
            var root = new VisualElement();
            root.Add(new Label("Crear Estructura de Escena") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 6 } });
            root.Add(new HelpBox(
                "Crea una jerarquía de organización estándar en la escena actual: Enviroment (Static, Dynamic, InteractiveObjects), " +
                "Characters (NPCs, Animals), UI, DELETE y OtherProgramingStuff.",
                HelpBoxMessageType.Info));

            var panel = new MTUIPanel("Acción") { style = { marginTop = 6 } };

            var createButton = new MTUIActionButton("Crear Estructura de Objetos", CrearEstructuraDeObjetos);
            createButton.style.marginTop = 4;
            panel.Add(createButton);

            root.Add(panel);
            return root;
        }

        private static void CrearEstructuraDeObjetos()
        {
            GameObject enviroment = CreateRoot("Enviroment");
            CreateChild(enviroment, "Static");
            CreateChild(enviroment, "Dynamic");
            CreateChild(enviroment, "InteractiveObjects");

            GameObject characters = CreateRoot("Characters");
            CreateChild(characters, "NPCs");
            CreateChild(characters, "Animals");

            CreateRoot("UI");
            CreateRoot("DELETE");
            CreateRoot("OtherProgramingStuff");

            Selection.activeGameObject = enviroment;
        }

        private static GameObject CreateRoot(string name)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Crear Estructura de Escena");
            return go;
        }

        private static void CreateChild(GameObject parent, string name)
        {
            var child = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(child, "Crear Estructura de Escena");
            child.transform.parent = parent.transform;
        }
    }
}
