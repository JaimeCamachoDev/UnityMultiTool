using UnityEditor;
using UnityEngine;

public static class VZOptizoneMenu
{
    [MenuItem("VZ Optizone/Crear Estructura de Objetos")]
    private static void CrearEstructuraDeObjetos()
    {
        // Crear el objeto raíz "Enviroment"
        GameObject enviroment = new GameObject("Enviroment");
        // Crear hijos de "Enviroment"
        new GameObject("Static").transform.parent = enviroment.transform;
        new GameObject("Dynamic").transform.parent = enviroment.transform;
        new GameObject("InteractiveObjects").transform.parent = enviroment.transform;

        // Crear el objeto raíz "Characters"
        GameObject characters = new GameObject("Characters");
        // Crear hijos de "Characters"
        new GameObject("NPCs").transform.parent = characters.transform;
        new GameObject("Animals").transform.parent = characters.transform;

        // Crear el objeto raíz "UI"
        new GameObject("UI");

        // Crear el objeto raíz "DELETE"
        new GameObject("DELETE");

        // Crear el objeto raíz "OtherProgramingStuff"
        new GameObject("OtherProgramingStuff");

        // Seleccionar el primer objeto creado en la jerarquía
        Selection.activeGameObject = enviroment;
    }
}
