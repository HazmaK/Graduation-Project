using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using RetroFPS.Combat;

namespace RetroFPS.Editor
{
    /// <summary>
    /// WaveSet asset'i için özet tablosu ve Validate paneli. Varsayılan Inspector'ın altına eklenir.
    /// </summary>
    [CustomEditor(typeof(WaveSet))]
    public class WaveSetEditor : UnityEditor.Editor
    {
        private readonly List<ValidationIssue> _issues = new List<ValidationIssue>(16);
        private bool _showSummary = true;
        private bool _showValidation = true;
        private bool _dirty = true;

        private void OnEnable()
        {
            Undo.undoRedoPerformed += MarkDirty;
            _dirty = true;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= MarkDirty;
        }

        private void MarkDirty()
        {
            _dirty = true;
            Repaint();
        }

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            if (EditorGUI.EndChangeCheck())
            {
                _dirty = true;
            }

            WaveSet set = (WaveSet)target;

            if (_dirty && Event.current.type == EventType.Layout)
            {
                _issues.Clear();
                EncounterValidator.ValidateWaves(set.Waves, _issues);
                _dirty = false;
            }

            EditorGUILayout.Space(8);
            _showValidation = EditorGUILayout.BeginFoldoutHeaderGroup(_showValidation, "Doğrulama (Validate)");
            if (_showValidation)
            {
                EncounterInspectorUtil.DrawIssues(_issues);
                if (GUILayout.Button("Yeniden Doğrula")) _dirty = true;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            _showSummary = EditorGUILayout.BeginFoldoutHeaderGroup(_showSummary, "Özet");
            if (_showSummary)
            {
                EncounterInspectorUtil.DrawWaveSummary(set.Waves);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
    }
}
