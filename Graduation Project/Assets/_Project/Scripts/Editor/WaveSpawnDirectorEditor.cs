using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using RetroFPS.Combat;

namespace RetroFPS.Editor
{
    /// <summary>
    /// WaveSpawnDirector için oda özeti ve Validate paneli (oda izolasyonu, dalga, doğuş noktası ve bariyer kontrolleri).
    /// </summary>
    [CustomEditor(typeof(WaveSpawnDirector))]
    public class WaveSpawnDirectorEditor : UnityEditor.Editor
    {
        private readonly List<ValidationIssue> _issues = new List<ValidationIssue>(24);
        private bool _showRoom = true;
        private bool _showValidation = true;
        private bool _showWaves = false;
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

            WaveSpawnDirector director = (WaveSpawnDirector)target;

            if (_dirty && Event.current.type == EventType.Layout)
            {
                _issues.Clear();
                EncounterValidator.ValidateRoom(director, _issues);
                _dirty = false;
            }

            EditorGUILayout.Space(8);

            _showValidation = EditorGUILayout.BeginFoldoutHeaderGroup(_showValidation, "Oda Doğrulama (Validate)");
            if (_showValidation)
            {
                EncounterInspectorUtil.DrawIssues(_issues);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Yeniden Doğrula")) _dirty = true;

                    if (GUILayout.Button("Doğuş Noktalarını Bu Odaya Bağla"))
                    {
                        Undo.RecordObject(director, "Bind Room Spawn Points");
                        director.BindRoomSpawnPoints();
                        EditorUtility.SetDirty(director);
                        _dirty = true;
                    }
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            _showRoom = EditorGUILayout.BeginFoldoutHeaderGroup(_showRoom, "Oda Özeti");
            if (_showRoom)
            {
                DrawRoomSummary(director);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            _showWaves = EditorGUILayout.BeginFoldoutHeaderGroup(_showWaves, "Dalga Özeti");
            if (_showWaves)
            {
                EncounterInspectorUtil.DrawWaveSummary(director.EffectiveWaves);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private static void DrawRoomSummary(WaveSpawnDirector director)
        {
            string source = director.WaveSetAsset != null ? $"WaveSet: {director.WaveSetAsset.name}" : "Satır-içi dalga listesi";
            EditorGUILayout.LabelField("Oda", director.RoomName);
            EditorGUILayout.LabelField("Dalga Kaynağı", source);

            EncounterSpawnPoint[] points = director.PeekRoomSpawnPoints();
            int ground = 0, air = 0;
            for (int i = 0; i < points.Length; i++)
            {
                if (points[i] == null) continue;
                if (points[i].AllowGroundEnemies) ground++;
                if (points[i].AllowAirEnemies) air++;
            }
            EditorGUILayout.LabelField("Doğuş Noktaları", $"{points.Length} (zemin uyumlu {ground}, hava uyumlu {air})");

            EncounterArea area = director.EncounterArea != null ? director.EncounterArea : director.GetComponent<EncounterArea>();
            EditorGUILayout.LabelField("Alan", area != null ? area.name : "— (yok)");

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(area == null))
                {
                    if (GUILayout.Button("Alanı Seç")) Selection.activeObject = area;
                }
                using (new EditorGUI.DisabledScope(points.Length == 0))
                {
                    if (GUILayout.Button("Doğuş Noktalarını Seç"))
                    {
                        List<Object> objs = new List<Object>(points.Length);
                        for (int i = 0; i < points.Length; i++)
                        {
                            if (points[i] != null) objs.Add(points[i].gameObject);
                        }
                        Selection.objects = objs.ToArray();
                    }
                }
            }
        }
    }
}
