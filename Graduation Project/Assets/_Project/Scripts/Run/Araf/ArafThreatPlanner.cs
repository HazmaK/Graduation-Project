using System;
using RetroFPS.Run.Labyrinth;
using UnityEngine;

namespace RetroFPS.Run.Araf
{
    /// <summary>Plans deterministic threat cells using only caller-owned buffers.</summary>
    public static class ArafThreatPlanner
    {
        /// <summary>Picks distinct eligible corridors, maximizing minimum Manhattan grid distance to prior picks; ties use rng.</summary>
        public static int PickPatrolCells(LabyrinthLayout layout, Vector3 avoidLocal, float minDistance,
            RunRandom rng, GridPos[] results, int count)
        {
            if (layout == null || rng == null || results == null || count <= 0) return 0;
            int target = Math.Min(count, results.Length);
            float distanceSquared = Mathf.Max(0f, minDistance) * Mathf.Max(0f, minDistance);
            int picked = 0;
            while (picked < target)
            {
                GridPos best = default;
                int bestDistance = -1;
                int ties = 0;
                for (int y = 0; y < layout.Height; y++)
                for (int x = 0; x < layout.Width; x++)
                {
                    var cell = new GridPos(x, y);
                    if (!Eligible(layout, cell, avoidLocal, distanceSquared)) continue;
                    int nearest = int.MaxValue;
                    for (int i = 0; i < picked; i++) nearest = Math.Min(nearest, GridDistance(cell, results[i]));
                    if (nearest == 0) continue;
                    if (nearest > bestDistance)
                    {
                        best = cell;
                        bestDistance = nearest;
                        ties = 1;
                    }
                    else if (nearest == bestDistance && rng.Range(0, ++ties) == 0) best = cell;
                }
                if (bestDistance < 0) break;
                results[picked++] = best;
            }
            return picked;
        }

        /// <summary>Writes the first doorway's outside cell and follows open depth-minus-one edges to StartCell, limited by buffer capacity.</summary>
        public static int BuildReturnPath(LabyrinthLayout layout, int roomSlotIndex, GridPos[] buffer)
        {
            if (layout == null || buffer == null || buffer.Length == 0
                || (uint)roomSlotIndex >= (uint)layout.RoomCount) return 0;
            Doorway[] doors = layout.GetRoom(roomSlotIndex).Doorways;
            if (doors == null || doors.Length == 0) return 0;
            GridPos cell = Next(doors[0].Inside, doors[0].Dir);
            int length = 0;
            while (length < buffer.Length)
            {
                buffer[length++] = cell;
                if (Same(cell, layout.StartCell)) break;
                int depth = layout.GetDepth(cell.X, cell.Y);
                bool found = false;
                for (int dir = 0; dir < 4; dir++)
                {
                    if (layout.HasWall(cell.X, cell.Y, (LabyrinthDir)dir)) continue;
                    GridPos next = Next(cell, (LabyrinthDir)dir);
                    if (!InBounds(layout, next) || layout.GetDepth(next.X, next.Y) != depth - 1) continue;
                    cell = next;
                    found = true;
                    break;
                }
                if (!found) break;
            }
            return length;
        }

        /// <summary>Picks eligible path cells at least two Manhattan grid steps apart, preferring the 30–85% window before widening.</summary>
        public static int PickWaveCells(LabyrinthLayout layout, GridPos[] path, int pathLength,
            Vector3 avoidLocal, float minDistance, RunRandom rng, GridPos[] results, int count)
        {
            if (layout == null || path == null || results == null || rng == null || count <= 0 || pathLength <= 0) return 0;
            pathLength = Math.Min(pathLength, path.Length);
            int target = Math.Min(count, results.Length);
            int begin = Mathf.CeilToInt((pathLength - 1) * 0.3f);
            int end = Mathf.FloorToInt((pathLength - 1) * 0.85f);
            float distanceSquared = Mathf.Max(0f, minDistance) * Mathf.Max(0f, minDistance);
            int picked = 0;
            for (int pass = 0; pass < 2 && picked < target; pass++)
            {
                while (picked < target)
                {
                    int chosen = -1;
                    int candidates = 0;
                    for (int i = begin; i <= end; i++)
                    {
                        GridPos cell = path[i];
                        if (!Eligible(layout, cell, avoidLocal, distanceSquared)) continue;
                        bool separated = true;
                        for (int j = 0; j < picked; j++)
                            if (GridDistance(cell, results[j]) < 2) { separated = false; break; }
                        if (separated && rng.Range(0, ++candidates) == 0) chosen = i;
                    }
                    if (chosen < 0) break;
                    results[picked++] = path[chosen];
                }
                begin = 0;
                end = pathLength - 1;
            }
            return picked;
        }

        private static bool Eligible(LabyrinthLayout layout, GridPos cell, Vector3 avoidLocal, float distanceSquared)
            => InBounds(layout, cell) && !Same(cell, layout.StartCell)
                && layout.GetRoomIndex(cell.X, cell.Y) < 0
                && (layout.CellToLocal(cell.X, cell.Y) - avoidLocal).sqrMagnitude >= distanceSquared;

        private static bool Same(GridPos a, GridPos b) => a.X == b.X && a.Y == b.Y;
        private static int GridDistance(GridPos a, GridPos b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
        private static bool InBounds(LabyrinthLayout layout, GridPos cell)
            => (uint)cell.X < (uint)layout.Width && (uint)cell.Y < (uint)layout.Height;

        private static GridPos Next(GridPos cell, LabyrinthDir dir)
        {
            switch (dir)
            {
                case LabyrinthDir.North: return new GridPos(cell.X, cell.Y + 1);
                case LabyrinthDir.East: return new GridPos(cell.X + 1, cell.Y);
                case LabyrinthDir.South: return new GridPos(cell.X, cell.Y - 1);
                default: return new GridPos(cell.X - 1, cell.Y);
            }
        }
    }
}
