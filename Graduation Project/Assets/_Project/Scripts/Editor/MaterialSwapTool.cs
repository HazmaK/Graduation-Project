using UnityEditor;
using UnityEngine;

namespace RetroFPS.EditorTools
{
    /// <summary>
    /// Replaces one material with another on the selected scene objects (and their children).
    /// Unlike dragging a material onto the Inspector, this never touches the material assets themselves.
    /// Menu: RetroFPS/Material Swap Tool.
    /// </summary>
    public sealed class MaterialSwapTool : EditorWindow
    {
        [SerializeField] private Material fromMaterial;
        [SerializeField] private Material toMaterial;
        [SerializeField] private bool replaceAllSlots;

        [MenuItem("RetroFPS/Material Swap Tool")]
        private static void Open() => GetWindow<MaterialSwapTool>("Material Swap");

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Hierarchy'de duvarları seç, sonra Replace'e bas. Sadece seçili objeler ve çocukları etkilenir; materyal asset'leri değişmez.", MessageType.Info);
            fromMaterial = (Material)EditorGUILayout.ObjectField("Eski (From)", fromMaterial, typeof(Material), false);
            toMaterial = (Material)EditorGUILayout.ObjectField("Yeni (To)", toMaterial, typeof(Material), false);
            replaceAllSlots = EditorGUILayout.Toggle("Tüm slotları değiştir", replaceAllSlots);

            using (new EditorGUI.DisabledScope(toMaterial == null || (!replaceAllSlots && fromMaterial == null)))
            {
                if (GUILayout.Button("Replace in Selection")) Replace();
            }
        }

        private void Replace()
        {
            GameObject[] roots = Selection.gameObjects;
            int changed = 0;
            for (int i = 0; i < roots.Length; i++)
            {
                Renderer[] renderers = roots[i].GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < renderers.Length; r++)
                {
                    Renderer rend = renderers[r];
                    Material[] mats = rend.sharedMaterials;
                    bool dirty = false;
                    for (int m = 0; m < mats.Length; m++)
                    {
                        if (replaceAllSlots || mats[m] == fromMaterial)
                        {
                            if (mats[m] == toMaterial) continue;
                            mats[m] = toMaterial;
                            dirty = true;
                        }
                    }
                    if (!dirty) continue;
                    Undo.RecordObject(rend, "Material Swap");
                    rend.sharedMaterials = mats;
                    EditorUtility.SetDirty(rend);
                    changed++;
                }
            }
            Debug.Log($"[MaterialSwap] {changed} renderer güncellendi.");
        }
    }
}
