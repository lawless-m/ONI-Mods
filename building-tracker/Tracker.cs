using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEngine;

namespace BuildingTrackerMod
{
    public class Tracker : KMonoBehaviour, ISim1000ms
    {
        public static Tracker Instance;

        // Flush every 60 sim-seconds (10 times per cycle)
        public const float FlushInterval = 60f;

        private readonly Dictionary<(int cellX, int cellY, string prefabId, string resource, string direction, string category), float> accumulator
            = new Dictionary<(int, int, string, string, string, string), float>();

        private float lastFlushTime = -1f;
        private int lastFullTerrainCycle = -1;
        private bool tablesCreated;
        private volatile bool flushInProgress;

        // Previous terrain state for delta computation
        private TerrainCell[] previousTerrain;

        protected override void OnSpawn()
        {
            base.OnSpawn();
            Instance = this;
            UnityEngine.Debug.Log("BuildingTrackerMod: Tracker spawned");
        }

        public void Record(KMonoBehaviour source, string resource, float amount, string direction, string category)
        {
            if (amount <= 0f)
                return;

            var bc = source.GetComponent<BuildingComplete>();
            if (bc == null)
                return;

            string prefabId = bc.Def.PrefabID;
            int cell = Grid.PosToCell(source.transform.position);
            Grid.CellToXY(cell, out int cx, out int cy);

            var key = (cx, cy, prefabId, resource, direction, category);
            if (accumulator.TryGetValue(key, out float existing))
                accumulator[key] = existing + amount;
            else
                accumulator[key] = amount;
        }

        public void Sim1000ms(float dt)
        {
            if (GameClock.Instance == null || flushInProgress)
                return;

            float simTime = GameClock.Instance.GetTime();
            if (lastFlushTime < 0f)
            {
                lastFlushTime = simTime;
                return;
            }

            if (simTime - lastFlushTime < FlushInterval)
                return;

            lastFlushTime = simTime;

            var snapshot = SnapshotData(simTime);
            if (snapshot == null)
                return;

            flushInProgress = true;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var sw = Stopwatch.StartNew();
                    WriteSnapshot(snapshot);
                    sw.Stop();
                    int terrainCount = snapshot.FullTerrain?.Length ?? snapshot.TerrainDeltas?.Count ?? 0;
                    string terrainType = snapshot.FullTerrain != null ? "full" : "delta";
                    UnityEngine.Debug.Log($"BuildingTrackerMod: Flush took {sw.ElapsedMilliseconds}ms ({snapshot.FlowData.Count} flows, {snapshot.Buildings.Count} buildings, {terrainCount} terrain [{terrainType}])");
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"BuildingTrackerMod: Flush failed: {e}");
                }
                finally
                {
                    flushInProgress = false;
                }
            });
        }

        private FlushSnapshot SnapshotData(float simTime)
        {
            int cycle = GameClock.Instance.GetCycle();
            string dbPath = GetDbPath();
            if (dbPath == null)
                return null;

            var snapshot = new FlushSnapshot
            {
                DbPath = dbPath,
                Cycle = cycle,
                SimTime = simTime,
                FlowData = new List<FlowRecord>(accumulator.Count),
            };

            // Copy accumulator
            foreach (var kvp in accumulator)
            {
                var k = kvp.Key;
                snapshot.FlowData.Add(new FlowRecord
                {
                    CellX = k.cellX, CellY = k.cellY,
                    PrefabId = k.prefabId, Resource = k.resource,
                    Amount = kvp.Value, Direction = k.direction,
                    Category = k.category
                });
            }
            accumulator.Clear();

            // Snapshot buildings
            var buildings = new List<BuildingRecord>();
            foreach (var bc in Components.BuildingCompletes.Items)
            {
                if (bc == null || bc.Def == null)
                    continue;
                int cell = Grid.PosToCell(bc.transform.position);
                Grid.CellToXY(cell, out int cx, out int cy);
                buildings.Add(new BuildingRecord
                {
                    PrefabId = bc.Def.PrefabID, Name = bc.Def.Name,
                    CellX = cx, CellY = cy,
                    Width = bc.Def.WidthInCells, Height = bc.Def.HeightInCells,
                    Revealed = Grid.Revealed[cell] ? 1 : 0
                });
            }
            snapshot.Buildings = buildings;

            // Snapshot terrain — full once per cycle, deltas every flush
            int count = Grid.CellCount;
            int width = Grid.WidthInCells;
            bool fullSnapshot = cycle != lastFullTerrainCycle || previousTerrain == null;

            var currentTerrain = new TerrainCell[count];
            for (int cell = 0; cell < count; cell++)
            {
                Element elem = Grid.Element[cell];
                currentTerrain[cell] = new TerrainCell
                {
                    ElementId = elem.id,
                    Mass = Grid.Mass[cell],
                    Temperature = Grid.Temperature[cell],
                    IsSolid = elem.IsSolid,
                    IsLiquid = elem.IsLiquid,
                    IsGas = elem.IsGas,
                    Revealed = Grid.Revealed[cell]
                };
            }

            if (fullSnapshot)
            {
                lastFullTerrainCycle = cycle;
                var terrain = new List<TerrainRecord>(count / 2);
                for (int cell = 0; cell < count; cell++)
                {
                    var c = currentTerrain[cell];
                    if (c.ElementId == SimHashes.Vacuum && c.Mass <= 0f)
                        continue;
                    terrain.Add(new TerrainRecord
                    {
                        CellX = cell % width,
                        CellY = cell / width,
                        Element = c.ElementId.ToString(),
                        Mass = c.Mass,
                        Temperature = c.Temperature,
                        State = c.IsSolid ? "solid" : c.IsLiquid ? "liquid" : c.IsGas ? "gas" : "vacuum",
                        Revealed = c.Revealed ? 1 : 0
                    });
                }
                snapshot.FullTerrain = terrain.ToArray();
            }
            else
            {
                var deltas = new List<TerrainRecord>();
                for (int cell = 0; cell < count; cell++)
                {
                    var cur = currentTerrain[cell];
                    var prev = previousTerrain[cell];
                    if (cur.ElementId == prev.ElementId
                        && cur.Mass == prev.Mass
                        && cur.Temperature == prev.Temperature
                        && cur.Revealed == prev.Revealed)
                        continue;
                    deltas.Add(new TerrainRecord
                    {
                        CellX = cell % width,
                        CellY = cell / width,
                        Element = cur.ElementId.ToString(),
                        Mass = cur.Mass,
                        Temperature = cur.Temperature,
                        State = cur.IsSolid ? "solid" : cur.IsLiquid ? "liquid" : cur.IsGas ? "gas" : "vacuum",
                        Revealed = cur.Revealed ? 1 : 0
                    });
                }
                snapshot.TerrainDeltas = deltas;
            }

            previousTerrain = currentTerrain;
            return snapshot;
        }

        private void WriteSnapshot(FlushSnapshot s)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(s.DbPath));

            int rc = Sqlite3.sqlite3_open(s.DbPath, out IntPtr db);
            Sqlite3.Check(db, rc);

            try
            {
                if (!tablesCreated)
                {
                    Sqlite3.Exec(db, @"
                        CREATE TABLE IF NOT EXISTS buildings (
                            cycle INTEGER NOT NULL,
                            sim_time REAL NOT NULL,
                            prefab_id TEXT NOT NULL,
                            name TEXT NOT NULL,
                            cell_x INTEGER NOT NULL,
                            cell_y INTEGER NOT NULL,
                            width INTEGER NOT NULL,
                            height INTEGER NOT NULL,
                            revealed INTEGER NOT NULL
                        );
                        CREATE TABLE IF NOT EXISTS resource_flows (
                            cycle INTEGER NOT NULL,
                            sim_time REAL NOT NULL,
                            prefab_id TEXT NOT NULL,
                            cell_x INTEGER NOT NULL,
                            cell_y INTEGER NOT NULL,
                            resource TEXT NOT NULL,
                            amount REAL NOT NULL,
                            direction TEXT NOT NULL,
                            category TEXT NOT NULL
                        );
                        CREATE TABLE IF NOT EXISTS terrain (
                            cycle INTEGER NOT NULL,
                            sim_time REAL NOT NULL,
                            cell_x INTEGER NOT NULL,
                            cell_y INTEGER NOT NULL,
                            element TEXT NOT NULL,
                            mass REAL NOT NULL,
                            temperature REAL NOT NULL,
                            state TEXT NOT NULL,
                            revealed INTEGER NOT NULL,
                            is_full INTEGER NOT NULL DEFAULT 0
                        );
                    ");
                    tablesCreated = true;
                }

                Sqlite3.Exec(db, "BEGIN TRANSACTION");

                // Resource flows
                rc = Sqlite3.sqlite3_prepare_v2(db,
                    "INSERT INTO resource_flows (cycle, sim_time, prefab_id, cell_x, cell_y, resource, amount, direction, category) VALUES (?1,?2,?3,?4,?5,?6,?7,?8,?9)",
                    -1, out IntPtr flowStmt, IntPtr.Zero);
                Sqlite3.Check(db, rc);

                foreach (var f in s.FlowData)
                {
                    Sqlite3.sqlite3_bind_int(flowStmt, 1, s.Cycle);
                    Sqlite3.sqlite3_bind_double(flowStmt, 2, s.SimTime);
                    Sqlite3.BindText(flowStmt, 3, f.PrefabId);
                    Sqlite3.sqlite3_bind_int(flowStmt, 4, f.CellX);
                    Sqlite3.sqlite3_bind_int(flowStmt, 5, f.CellY);
                    Sqlite3.BindText(flowStmt, 6, f.Resource);
                    Sqlite3.sqlite3_bind_double(flowStmt, 7, f.Amount);
                    Sqlite3.BindText(flowStmt, 8, f.Direction);
                    Sqlite3.BindText(flowStmt, 9, f.Category);
                    Sqlite3.sqlite3_step(flowStmt);
                    Sqlite3.sqlite3_reset(flowStmt);
                }
                Sqlite3.sqlite3_finalize(flowStmt);

                // Buildings
                {
                    rc = Sqlite3.sqlite3_prepare_v2(db,
                        "INSERT INTO buildings (cycle, sim_time, prefab_id, name, cell_x, cell_y, width, height, revealed) VALUES (?1,?2,?3,?4,?5,?6,?7,?8,?9)",
                        -1, out IntPtr bStmt, IntPtr.Zero);
                    Sqlite3.Check(db, rc);

                    foreach (var b in s.Buildings)
                    {
                        Sqlite3.sqlite3_bind_int(bStmt, 1, s.Cycle);
                        Sqlite3.sqlite3_bind_double(bStmt, 2, s.SimTime);
                        Sqlite3.BindText(bStmt, 3, b.PrefabId);
                        Sqlite3.BindText(bStmt, 4, b.Name);
                        Sqlite3.sqlite3_bind_int(bStmt, 5, b.CellX);
                        Sqlite3.sqlite3_bind_int(bStmt, 6, b.CellY);
                        Sqlite3.sqlite3_bind_int(bStmt, 7, b.Width);
                        Sqlite3.sqlite3_bind_int(bStmt, 8, b.Height);
                        Sqlite3.sqlite3_bind_int(bStmt, 9, b.Revealed);
                        Sqlite3.sqlite3_step(bStmt);
                        Sqlite3.sqlite3_reset(bStmt);
                    }
                    Sqlite3.sqlite3_finalize(bStmt);
                }

                // Terrain
                {
                    rc = Sqlite3.sqlite3_prepare_v2(db,
                        "INSERT INTO terrain (cycle, sim_time, cell_x, cell_y, element, mass, temperature, state, revealed, is_full) VALUES (?1,?2,?3,?4,?5,?6,?7,?8,?9,?10)",
                        -1, out IntPtr tStmt, IntPtr.Zero);
                    Sqlite3.Check(db, rc);

                    int isFull = s.FullTerrain != null ? 1 : 0;
                    var terrainRows = s.FullTerrain != null
                        ? (IEnumerable<TerrainRecord>)s.FullTerrain
                        : s.TerrainDeltas;

                    foreach (var t in terrainRows)
                    {
                        Sqlite3.sqlite3_bind_int(tStmt, 1, s.Cycle);
                        Sqlite3.sqlite3_bind_double(tStmt, 2, s.SimTime);
                        Sqlite3.sqlite3_bind_int(tStmt, 3, t.CellX);
                        Sqlite3.sqlite3_bind_int(tStmt, 4, t.CellY);
                        Sqlite3.BindText(tStmt, 5, t.Element);
                        Sqlite3.sqlite3_bind_double(tStmt, 6, t.Mass);
                        Sqlite3.sqlite3_bind_double(tStmt, 7, t.Temperature);
                        Sqlite3.BindText(tStmt, 8, t.State);
                        Sqlite3.sqlite3_bind_int(tStmt, 9, t.Revealed);
                        Sqlite3.sqlite3_bind_int(tStmt, 10, isFull);
                        Sqlite3.sqlite3_step(tStmt);
                        Sqlite3.sqlite3_reset(tStmt);
                    }
                    Sqlite3.sqlite3_finalize(tStmt);
                }

                Sqlite3.Exec(db, "COMMIT");
            }
            catch
            {
                try { Sqlite3.Exec(db, "ROLLBACK"); } catch { }
                throw;
            }
            finally
            {
                Sqlite3.sqlite3_close(db);
            }
        }

        private static string GetDbPath()
        {
            string saveDir = SaveLoader.GetActiveSaveFolder();
            if (string.IsNullOrEmpty(saveDir))
                saveDir = SaveLoader.GetSavePrefix();
            if (string.IsNullOrEmpty(saveDir))
                return null;
            return Path.Combine(saveDir, "BuildingTracker", "tracker.db");
        }

        // Snapshot data structures — plain data, no Unity references
        private class FlushSnapshot
        {
            public string DbPath;
            public int Cycle;
            public float SimTime;
            public List<FlowRecord> FlowData;
            public List<BuildingRecord> Buildings;
            public TerrainRecord[] FullTerrain;       // non-null on first flush of each cycle
            public List<TerrainRecord> TerrainDeltas;  // non-null on subsequent flushes
        }

        private struct FlowRecord
        {
            public int CellX, CellY;
            public string PrefabId, Resource, Direction, Category;
            public float Amount;
        }

        private struct BuildingRecord
        {
            public string PrefabId, Name;
            public int CellX, CellY, Width, Height, Revealed;
        }

        private struct TerrainRecord
        {
            public int CellX, CellY;
            public string Element, State;
            public float Mass, Temperature;
            public int Revealed;
        }

        // Compact struct for diffing — stays in memory, never written to db
        private struct TerrainCell
        {
            public SimHashes ElementId;
            public float Mass, Temperature;
            public bool IsSolid, IsLiquid, IsGas, Revealed;
        }
    }
}
