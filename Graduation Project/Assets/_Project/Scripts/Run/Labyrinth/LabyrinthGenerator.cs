using System;
using UnityEngine;

namespace RetroFPS.Run.Labyrinth
{
    /// <summary>Koşu akışından deterministik oda ve örgülü koridor topolojisi üretir.</summary>
    public static class LabyrinthGenerator
    {
        private const int PlacementAttempts = 200;

        /// <summary>Pozitif boyutlar ve geçerli olasılık/sınırlar ile bağlantılı bir labirent üretir.</summary>
        public static LabyrinthLayout Generate(LabyrinthSettings settings, RunRandom rng)
        {
            Validate(settings, rng);
            int width = settings.Width;
            int height = settings.Height;
            int cellCount = checked(width * height);
            int corner = rng.Range(0, 4);
            var start = new GridPos((corner & 1) == 0 ? 0 : width - 1,
                (corner & 2) == 0 ? 0 : height - 1);
            int startIndex = start.Y * width + start.X;
            var roomIndices = new int[cellCount];
            var walls = new byte[cellCount];
            var visited = new bool[cellCount];
            var work = new int[cellCount];
            for (int i = 0; i < cellCount; i++)
            {
                roomIndices[i] = -1;
                walls[i] = 15;
            }

            var roomMins = new GridPos[Math.Min(settings.RoomSlotCount, (cellCount - 1) / 4)];
            int roomCount = PlaceRooms(settings, rng, startIndex, roomIndices, roomMins, visited, work);
            Array.Clear(visited, 0, visited.Length);
            CarveMaze(width, height, startIndex, roomIndices, walls, rng, visited, work);

            var doorways = new Doorway[roomCount][];
            // Her 2x2 odanın sekiz olası dış kenarı vardır.
            var candidates = new int[8];
            for (int room = 0; room < roomCount; room++)
            {
                GridPos min = roomMins[room];
                int count = 0;
                for (int y = min.Y; y <= min.Y + 1; y++)
                for (int x = min.X; x <= min.X + 1; x++)
                {
                    int cell = y * width + x;
                    for (int dir = 0; dir < 4; dir++)
                    {
                        int next = Neighbour(cell, dir, width, height);
                        if (next < 0) continue;
                        if (roomIndices[next] == room) Open(walls, cell, next, dir);
                        else if (roomIndices[next] < 0) candidates[count++] = cell * 4 + dir;
                    }
                }
                rng.Shuffle(candidates, count);
                // İki kapı tercih edilir; oda içi kenarlar bu sınıra dahil değildir.
                int doorwayCount = Math.Min(count, Math.Min(2, settings.DoorwaysPerRoomMax));
                doorways[room] = new Doorway[doorwayCount];
                for (int i = 0; i < doorwayCount; i++)
                {
                    int cell = candidates[i] / 4;
                    int dir = candidates[i] % 4;
                    Open(walls, cell, Neighbour(cell, dir, width, height), dir);
                    var inside = new GridPos(cell % width, cell / width);
                    Vector3 direction = Direction(dir);
                    Vector3 centre = CellToLocal(inside.X, inside.Y, width, height, settings.CellSize);
                    doorways[room][i] = new Doorway(inside, (LabyrinthDir)dir,
                        centre + direction * (settings.CellSize / 2f), Quaternion.LookRotation(direction));
                }
            }

            Braid(width, height, roomIndices, walls, rng, settings.BraidChance, candidates);
            int[] depths = CalculateDepths(width, height, startIndex, walls, work);
            var rooms = new RoomSlotInfo[roomCount];
            for (int room = 0; room < roomCount; room++)
            {
                GridPos min = roomMins[room];
                int cell = min.Y * width + min.X;
                int depth = Math.Min(Math.Min(depths[cell], depths[cell + 1]),
                    Math.Min(depths[cell + width], depths[cell + width + 1]));
                Vector3 centre = CellToLocal(min.X, min.Y, width, height, settings.CellSize)
                    + new Vector3(settings.CellSize / 2f, 0f, settings.CellSize / 2f);
                rooms[room] = new RoomSlotInfo(room, min, depth, centre, doorways[room]);
            }
            return new LabyrinthLayout(width, height, settings.CellSize, start,
                walls, roomIndices, depths, rooms);
        }

        private static int PlaceRooms(LabyrinthSettings settings, RunRandom rng, int start,
            int[] indices, GridPos[] mins, bool[] visited, int[] work)
        {
            if (settings.Width < 2 || settings.Height < 2) return 0;
            int count = 0;
            while (count < mins.Length)
            {
                bool placed = false;
                for (int attempt = 0; attempt < PlacementAttempts; attempt++)
                {
                    var min = new GridPos(rng.Range(0, settings.Width - 1), rng.Range(0, settings.Height - 1));
                    int startX = start % settings.Width;
                    int startY = start / settings.Width;
                    if (startX >= min.X && startX <= min.X + 1 && startY >= min.Y && startY <= min.Y + 1)
                        continue;
                    bool fits = true;
                    for (int i = 0; i < count; i++)
                    {
                        // Aradaki boş hücre sayısı Chebyshev ayrımıyla ölçülür.
                        long separation = 2L + settings.MinRoomGap;
                        if (Math.Abs((long)min.X - mins[i].X) < separation
                            && Math.Abs((long)min.Y - mins[i].Y) < separation)
                        { fits = false; break; }
                    }
                    if (!fits) continue;
                    mins[count] = min;
                    SetRoom(indices, settings.Width, min, count);
                    // Ada oluşturan veya başka bir odanın son koridor kenarını kapatan
                    // aday geri alınır. Böylece tek kapı sınırında da bağlantı garantidir.
                    if (CorridorsConnected(indices, settings.Width, settings.Height, start,
                            indices.Length - 4 * (count + 1), visited, work)
                        && RoomsHaveCorridor(indices, settings.Width, settings.Height, mins, count + 1))
                    { placed = true; break; }
                    SetRoom(indices, settings.Width, min, -1);
                }
                if (!placed) break;
                count++;
            }
            return count;
        }

        private static void SetRoom(int[] indices, int width, GridPos min, int room)
        {
            int cell = min.Y * width + min.X;
            indices[cell] = indices[cell + 1] = indices[cell + width] = indices[cell + width + 1] = room;
        }

        private static bool CorridorsConnected(int[] indices, int width, int height, int start,
            int expected, bool[] visited, int[] queue)
        {
            Array.Clear(visited, 0, visited.Length);
            int head = 0;
            int tail = 1;
            queue[0] = start;
            visited[start] = true;
            while (head < tail)
            {
                int cell = queue[head++];
                for (int dir = 0; dir < 4; dir++)
                {
                    int next = Neighbour(cell, dir, width, height);
                    if (next < 0 || visited[next] || indices[next] >= 0) continue;
                    visited[next] = true;
                    queue[tail++] = next;
                }
            }
            return tail == expected;
        }

        private static bool RoomsHaveCorridor(int[] indices, int width, int height, GridPos[] mins, int count)
        {
            for (int room = 0; room < count; room++)
            {
                bool found = false;
                for (int y = mins[room].Y; y <= mins[room].Y + 1 && !found; y++)
                for (int x = mins[room].X; x <= mins[room].X + 1 && !found; x++)
                for (int dir = 0; dir < 4; dir++)
                {
                    int next = Neighbour(y * width + x, dir, width, height);
                    if (next >= 0 && indices[next] < 0) { found = true; break; }
                }
                if (!found) return false;
            }
            return true;
        }

        private static void CarveMaze(int width, int height, int start, int[] indices, byte[] walls,
            RunRandom rng, bool[] visited, int[] stack)
        {
            var directions = new int[4];
            int size = 1;
            stack[0] = start;
            visited[start] = true;
            while (size > 0)
            {
                int cell = stack[size - 1];
                int count = 0;
                for (int dir = 0; dir < 4; dir++)
                {
                    int next = Neighbour(cell, dir, width, height);
                    if (next >= 0 && indices[next] < 0 && !visited[next]) directions[count++] = dir;
                }
                if (count == 0) { size--; continue; }
                // Rastgele komşu seçimi, her adımda karıştırılmış komşu sırasına denktir.
                int chosen = directions[rng.Range(0, count)];
                int target = Neighbour(cell, chosen, width, height);
                Open(walls, cell, target, chosen);
                visited[target] = true;
                stack[size++] = target;
            }
        }

        private static void Braid(int width, int height, int[] indices, byte[] walls,
            RunRandom rng, float chance, int[] candidates)
        {
            for (int cell = 0; cell < walls.Length; cell++)
            {
                if (indices[cell] >= 0) continue;
                int openings = 0;
                int count = 0;
                for (int dir = 0; dir < 4; dir++)
                {
                    if ((walls[cell] & (1 << dir)) == 0) { openings++; continue; }
                    int next = Neighbour(cell, dir, width, height);
                    if (next >= 0 && indices[next] < 0) candidates[count++] = dir;
                }
                if (openings != 1 || count == 0 || !rng.Chance(chance)) continue;
                int chosen = candidates[rng.Range(0, count)];
                Open(walls, cell, Neighbour(cell, chosen, width, height), chosen);
            }
        }

        private static int[] CalculateDepths(int width, int height, int start, byte[] walls, int[] queue)
        {
            var depths = new int[walls.Length];
            for (int i = 0; i < depths.Length; i++) depths[i] = -1;
            int head = 0;
            int tail = 1;
            queue[0] = start;
            depths[start] = 0;
            while (head < tail)
            {
                int cell = queue[head++];
                for (int dir = 0; dir < 4; dir++)
                {
                    if ((walls[cell] & (1 << dir)) != 0) continue;
                    int next = Neighbour(cell, dir, width, height);
                    if (depths[next] >= 0) continue;
                    depths[next] = depths[cell] + 1;
                    queue[tail++] = next;
                }
            }
            return depths;
        }

        private static int Neighbour(int cell, int dir, int width, int height)
        {
            switch (dir)
            {
                case 0: return cell / width + 1 < height ? cell + width : -1;
                case 1: return cell % width + 1 < width ? cell + 1 : -1;
                case 2: return cell / width > 0 ? cell - width : -1;
                default: return cell % width > 0 ? cell - 1 : -1;
            }
        }

        private static void Open(byte[] walls, int cell, int next, int dir)
        {
            walls[cell] &= (byte)(15 ^ (1 << dir));
            walls[next] &= (byte)(15 ^ (1 << ((dir + 2) & 3)));
        }

        private static Vector3 Direction(int dir)
        {
            switch (dir)
            {
                case 0: return Vector3.forward;
                case 1: return Vector3.right;
                case 2: return Vector3.back;
                default: return Vector3.left;
            }
        }

        private static Vector3 CellToLocal(int x, int y, int width, int height, float cellSize)
            => new Vector3((x - (width - 1) / 2f) * cellSize, 0f, (y - (height - 1) / 2f) * cellSize);

        private static void Validate(LabyrinthSettings settings, RunRandom rng)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (settings.Width < 1) throw new ArgumentOutOfRangeException(nameof(settings.Width));
            if (settings.Height < 1) throw new ArgumentOutOfRangeException(nameof(settings.Height));
            if (!(settings.CellSize > 0f) || float.IsInfinity(settings.CellSize))
                throw new ArgumentOutOfRangeException(nameof(settings.CellSize));
            if (settings.RoomSlotCount < 0) throw new ArgumentOutOfRangeException(nameof(settings.RoomSlotCount));
            if (!(settings.BraidChance >= 0f && settings.BraidChance <= 1f))
                throw new ArgumentOutOfRangeException(nameof(settings.BraidChance));
            if (settings.MinRoomGap < 0) throw new ArgumentOutOfRangeException(nameof(settings.MinRoomGap));
            if (settings.DoorwaysPerRoomMax < 1)
                throw new ArgumentOutOfRangeException(nameof(settings.DoorwaysPerRoomMax));
        }
    }
}
