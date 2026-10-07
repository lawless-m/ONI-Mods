using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using Klei.AI;
using UnityEngine;

namespace DupeTherapist
{
    public class TherapistBehaviour : MonoBehaviour
    {
        public static bool BlockInput { get; private set; }

        private TherapistServer server;
        private float refreshTimer;
        private float historyTimer;
        private const float RefreshInterval = 2f;
        private const float HistoryInterval = 30f; // snapshot every 30s game time

        private void Awake()
        {
            server = new TherapistServer();
            server.Start();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.T) &&
                (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
            {
                Application.OpenURL(server.Url);
            }

            if (!server.IsRunning) return;

            server.ApplyPendingChanges();

            refreshTimer -= Time.unscaledDeltaTime;
            if (refreshTimer <= 0)
            {
                refreshTimer = RefreshInterval;
                server.CachedJson = DupeSerializer.Serialize();
                server.CachedResourcesJson = ResourceSerializer.Serialize();
                server.CachedGeysersJson = GeyserSerializer.Serialize();
            }

            historyTimer -= Time.unscaledDeltaTime;
            if (historyTimer <= 0)
            {
                historyTimer = HistoryInterval;
                float cycle = GameClock.Instance != null ? GameClock.Instance.GetCycle() + GameClock.Instance.GetCurrentCycleAsPercentage() : 0;
                server.History.RecordSnapshot(cycle);
                server.ResHistory.RecordSnapshot(cycle);

                // Portrait rendering disabled for now
            }
        }

        private void OnDestroy()
        {
            server?.Stop();
        }
    }

    public class TherapistServer
    {
        private HttpListener listener;
        private Thread listenerThread;
        private volatile bool running;
        public volatile string CachedJson = "{}";
        public volatile string CachedResourcesJson = "{}";
        public volatile string CachedGeysersJson = "{}";
        private readonly ConcurrentQueue<PriorityChange> pendingChanges = new ConcurrentQueue<PriorityChange>();
        public readonly VitalHistory History = new VitalHistory();
        public readonly ResourceHistory ResHistory = new ResourceHistory();

        public bool IsRunning => running;
        public string Url => "http://localhost:8585/";
        private string webRoot;

        public void Start()
        {
            if (running) return;

            webRoot = Path.GetDirectoryName(
                System.Reflection.Assembly.GetExecutingAssembly().Location);

            listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:8585/");

            try
            {
                listener.Start();
            }
            catch (Exception ex)
            {
                Debug.Log($"DupeTherapist: Failed to start server: {ex.Message}");
                return;
            }

            running = true;
            listenerThread = new Thread(ListenLoop) { IsBackground = true };
            listenerThread.Start();
            Debug.Log("DupeTherapist: Server running at http://localhost:8585/");
        }

        public void Stop()
        {
            if (!running) return;
            running = false;
            try { listener?.Stop(); } catch { }
            try { listener?.Close(); } catch { }
            Debug.Log("DupeTherapist: Server stopped");
        }

        private void ListenLoop()
        {
            while (running)
            {
                try
                {
                    var ctx = listener.GetContext();
                    ThreadPool.QueueUserWorkItem(_ => HandleRequest(ctx));
                }
                catch (HttpListenerException) when (!running) { break; }
                catch (ObjectDisposedException) when (!running) { break; }
                catch (Exception ex)
                {
                    if (running)
                        Debug.Log($"DupeTherapist: {ex.Message}");
                }
            }
        }

        private void HandleRequest(HttpListenerContext ctx)
        {
            var path = ctx.Request.Url.AbsolutePath;
            try
            {
                if (path == "/" && ctx.Request.HttpMethod == "GET")
                    ServeFile(ctx.Response, "index.html", "text/html");
                else if (path == "/api/dupes" && ctx.Request.HttpMethod == "GET")
                    Respond(ctx.Response, 200, CachedJson, "application/json");
                else if (path == "/api/priorities" && ctx.Request.HttpMethod == "POST")
                    HandlePriorityPost(ctx);
                else if (path == "/api/history" && ctx.Request.HttpMethod == "GET")
                    Respond(ctx.Response, 200, History.ToJson(), "application/json");
                else if (path == "/api/resources" && ctx.Request.HttpMethod == "GET")
                    Respond(ctx.Response, 200, CachedResourcesJson, "application/json");
                else if (path == "/api/resource-history" && ctx.Request.HttpMethod == "GET")
                    Respond(ctx.Response, 200, ResHistory.ToJson(), "application/json");
                else if (path == "/api/geysers" && ctx.Request.HttpMethod == "GET")
                    Respond(ctx.Response, 200, CachedGeysersJson, "application/json");
                else if (path.StartsWith("/api/portrait") && ctx.Request.HttpMethod == "GET")
                    HandlePortrait(ctx);
                else if (path == "/api/rename" && ctx.Request.HttpMethod == "POST")
                    HandleRename(ctx);
                else
                    Respond(ctx.Response, 404, "Not found");
            }
            catch (Exception ex)
            {
                try { Respond(ctx.Response, 500, ex.Message); } catch { }
            }
        }

        private void ServeFile(HttpListenerResponse response, string filename, string contentType)
        {
            var path = Path.Combine(webRoot, "web", filename);
            if (File.Exists(path))
                Respond(response, 200, File.ReadAllText(path), contentType);
            else
                Respond(response, 404, $"{filename} not found at {path}");
        }

        private void HandlePriorityPost(HttpListenerContext ctx)
        {
            string body;
            using (var reader = new StreamReader(ctx.Request.InputStream))
                body = reader.ReadToEnd();

            var changes = PriorityChange.ParseBatch(body);
            foreach (var c in changes)
                pendingChanges.Enqueue(c);

            Respond(ctx.Response, 200,
                $"{{\"queued\":{changes.Count}}}", "application/json");
        }

        private readonly ConcurrentQueue<RenameRequest> pendingRenames = new ConcurrentQueue<RenameRequest>();

        private void HandlePortrait(HttpListenerContext ctx)
        {
            var query = ctx.Request.QueryString;
            var name = query["name"];
            if (string.IsNullOrEmpty(name))
            {
                Respond(ctx.Response, 400, "Missing name parameter");
                return;
            }

            var renderer = PortraitRenderer.Instance;
            var png = renderer?.GetPortrait(name);
            if (png != null)
            {
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "image/png";
                ctx.Response.ContentLength64 = png.Length;
                ctx.Response.OutputStream.Write(png, 0, png.Length);
                ctx.Response.Close();
            }
            else
            {
                Respond(ctx.Response, 404, "Portrait not ready");
            }
        }

        private void HandleRename(HttpListenerContext ctx)
        {
            string body;
            using (var reader = new StreamReader(ctx.Request.InputStream))
                body = reader.ReadToEnd();

            var oldName = PriorityChange.ExtractString(body, "oldName");
            var newName = PriorityChange.ExtractString(body, "newName");
            if (oldName == null || newName == null)
            {
                Respond(ctx.Response, 400, "{\"error\":\"missing oldName or newName\"}", "application/json");
                return;
            }

            pendingRenames.Enqueue(new RenameRequest { OldName = oldName, NewName = newName });
            Respond(ctx.Response, 200, "{\"ok\":true}", "application/json");
        }

        public void ApplyPendingChanges()
        {
            while (pendingChanges.TryDequeue(out var change))
                change.Apply();

            while (pendingRenames.TryDequeue(out var rename))
                rename.Apply();
        }

        private static void Respond(HttpListenerResponse response, int status,
            string body, string contentType = "text/plain")
        {
            response.StatusCode = status;
            response.ContentType = contentType + "; charset=utf-8";
            var bytes = Encoding.UTF8.GetBytes(body);
            response.ContentLength64 = bytes.Length;
            response.OutputStream.Write(bytes, 0, bytes.Length);
            response.Close();
        }
    }

    public class PriorityChange
    {
        public string DupeName;
        public string ChoreGroupId;
        public int Priority;

        public void Apply()
        {
            var dupes = Components.LiveMinionIdentities.Items;
            var identity = dupes?.FirstOrDefault(d => d.GetProperName() == DupeName);
            if (identity == null) return;

            var consumer = identity.gameObject.GetComponent<ChoreConsumer>();
            if (consumer == null) return;

            var cg = Db.Get().ChoreGroups.TryGet(ChoreGroupId);
            if (cg == null) return;

            consumer.SetPersonalPriority(cg, Priority);
        }

        public static List<PriorityChange> ParseBatch(string json)
        {
            var results = new List<PriorityChange>();
            json = json.Trim();
            if (json.StartsWith("[")) json = json.Substring(1);
            if (json.EndsWith("]")) json = json.Substring(0, json.Length - 1);
            if (string.IsNullOrWhiteSpace(json)) return results;

            foreach (var chunk in SplitObjects(json))
            {
                var c = new PriorityChange
                {
                    DupeName = ExtractString(chunk, "dupe"),
                    ChoreGroupId = ExtractString(chunk, "choreGroup"),
                    Priority = ExtractInt(chunk, "priority")
                };
                if (c.DupeName != null && c.ChoreGroupId != null)
                    results.Add(c);
            }
            return results;
        }

        private static IEnumerable<string> SplitObjects(string json)
        {
            int depth = 0;
            int start = 0;
            for (int i = 0; i < json.Length; i++)
            {
                if (json[i] == '{') depth++;
                else if (json[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        yield return json.Substring(start, i - start + 1);
                        start = i + 1;
                    }
                }
                else if (depth == 0 && json[i] == ',')
                    start = i + 1;
            }
        }

        public static string ExtractString(string json, string key)
        {
            var marker = $"\"{key}\":\"";
            int idx = json.IndexOf(marker, StringComparison.Ordinal);
            if (idx < 0) return null;
            idx += marker.Length;
            int end = json.IndexOf("\"", idx, StringComparison.Ordinal);
            return end < 0 ? null : json.Substring(idx, end - idx);
        }

        private static int ExtractInt(string json, string key)
        {
            var marker = $"\"{key}\":";
            int idx = json.IndexOf(marker, StringComparison.Ordinal);
            if (idx < 0) return 0;
            idx += marker.Length;
            var sb = new StringBuilder();
            while (idx < json.Length && (char.IsDigit(json[idx]) || json[idx] == '-'))
                sb.Append(json[idx++]);
            int.TryParse(sb.ToString(), out int val);
            return val;
        }
    }

    public class RenameRequest
    {
        public string OldName;
        public string NewName;

        public void Apply()
        {
            var dupes = Components.LiveMinionIdentities.Items;
            var identity = dupes?.FirstOrDefault(d => d.GetProperName() == OldName);
            if (identity == null) return;

            identity.SetName(NewName);
            Debug.Log($"DupeTherapist: Renamed '{OldName}' to '{NewName}'");

            // Invalidate portrait cache
            if (PortraitRenderer.Instance != null)
                PortraitRenderer.Instance.InvalidateCache();
        }
    }

    public static class DupeSerializer
    {
        public static string Serialize()
        {
            var sb = new StringBuilder(4096);

            // Chore groups
            var choreGroups = Db.Get().ChoreGroups.resources;
            sb.Append("{\"choreGroups\":[");
            for (int i = 0; i < choreGroups.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var cg = choreGroups[i];
                sb.Append("{\"id\":\"").Append(Esc(cg.Id))
                  .Append("\",\"name\":\"").Append(Esc(cg.Name))
                  .Append("\"}");
            }

            // Sicknesses available in the game
            sb.Append("],\"sicknesses\":[");
            try
            {
                var sicknesses = Db.Get().Sicknesses.resources;
                for (int i = 0; i < sicknesses.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append("{\"id\":\"").Append(Esc(sicknesses[i].Id))
                      .Append("\",\"name\":\"").Append(Esc(sicknesses[i].Name))
                      .Append("\"}");
                }
            }
            catch { }

            // Schedules
            sb.Append("],\"schedules\":[");
            try
            {
                var schedules = ScheduleManager.Instance.GetSchedules();
                for (int i = 0; i < schedules.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append("{\"name\":\"").Append(Esc(schedules[i].name)).Append("\"}");
                }
            }
            catch { }

            sb.Append("],\"dupes\":[");

            // Duplicants
            var dupes = Components.LiveMinionIdentities.Items;
            if (dupes != null)
            {
                for (int i = 0; i < dupes.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    SerializeDupe(sb, dupes[i], choreGroups);
                }
            }

            sb.Append("]}");
            return sb.ToString();
        }

        private static void SerializeDupe(StringBuilder sb, MinionIdentity identity,
            List<ChoreGroup> choreGroups)
        {
            var go = identity.gameObject;
            sb.Append("{\"name\":\"").Append(Esc(identity.GetProperName())).Append('"');

            // Traits
            sb.Append(",\"traits\":[");
            var traits = go.GetComponent<Traits>();
            if (traits != null)
            {
                var ids = traits.GetTraitIds();
                for (int i = 0; i < ids.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    string display = ids[i];
                    try
                    {
                        var t = Db.Get().traits.TryGet(ids[i]);
                        if (t != null) display = t.Name;
                    }
                    catch { }
                    sb.Append('"').Append(Esc(display)).Append('"');
                }
            }
            sb.Append(']');

            // Interests
            sb.Append(",\"interests\":[");
            var resume = go.GetComponent<MinionResume>();
            if (resume != null)
            {
                bool first = true;
                try
                {
                    var skillGroups = Db.Get().SkillGroups.resources;
                    foreach (var sg in skillGroups)
                    {
                        float apt;
                        if (resume.AptitudeBySkillGroup.TryGetValue(
                            new HashedString(sg.Id), out apt) && apt > 0)
                        {
                            if (!first) sb.Append(',');
                            sb.Append('"').Append(Esc(sg.Name)).Append('"');
                            first = false;
                        }
                    }
                }
                catch { }
            }
            sb.Append(']');

            // Learned skills
            sb.Append(",\"skills\":[");
            if (resume != null)
            {
                bool first = true;
                foreach (var kvp in resume.MasteryBySkillID)
                {
                    if (!kvp.Value) continue;
                    if (!first) sb.Append(',');
                    string display = kvp.Key;
                    try
                    {
                        var skill = Db.Get().Skills.TryGet(kvp.Key);
                        if (skill != null) display = skill.Name;
                    }
                    catch { }
                    sb.Append('"').Append(Esc(display)).Append('"');
                    first = false;
                }
            }
            sb.Append(']');

            // Disabled chore groups (from traits like Unconstructive, Yokel, etc.)
            sb.Append(",\"disabledGroups\":[");
            if (traits != null)
            {
                bool first = true;
                foreach (var cg in choreGroups)
                {
                    try
                    {
                        if (traits.IsChoreGroupDisabled(cg))
                        {
                            if (!first) sb.Append(',');
                            sb.Append('"').Append(Esc(cg.Id)).Append('"');
                            first = false;
                        }
                    }
                    catch { }
                }
            }
            sb.Append(']');

            // Core attributes (Strength, Athletics, etc.)
            sb.Append(",\"coreAttributes\":{");
            try
            {
                var dbAttrs = Db.Get().Attributes;
                var coreAttrs = new[] {
                    ("Strength", dbAttrs.Strength), ("Athletics", dbAttrs.Athletics),
                    ("Learning", dbAttrs.Learning), ("Cooking", dbAttrs.Cooking),
                    ("Caring", dbAttrs.Caring), ("Art", dbAttrs.Art),
                    ("Digging", dbAttrs.Digging), ("Construction", dbAttrs.Construction),
                    ("Machinery", dbAttrs.Machinery), ("Ranching", dbAttrs.Ranching),
                    ("Botanist", dbAttrs.Botanist),
                };
                for (int i = 0; i < coreAttrs.Length; i++)
                {
                    if (i > 0) sb.Append(',');
                    float val = 0;
                    try
                    {
                        var inst = go.GetAttributes().Get(coreAttrs[i].Item2);
                        if (inst != null) val = inst.GetTotalValue();
                    }
                    catch { }
                    sb.Append('"').Append(coreAttrs[i].Item1).Append("\":").Append(Mathf.RoundToInt(val));
                }
            }
            catch { }
            sb.Append('}');

            // Priorities per chore group
            var consumer = go.GetComponent<ChoreConsumer>();
            sb.Append(",\"priorities\":{");
            for (int i = 0; i < choreGroups.Count; i++)
            {
                if (i > 0) sb.Append(',');
                int prio = 0;
                try { if (consumer != null) prio = consumer.GetPersonalPriority(choreGroups[i]); }
                catch { }
                sb.Append('"').Append(Esc(choreGroups[i].Id)).Append("\":").Append(prio);
            }
            sb.Append('}');

            // Attribute values per chore group
            sb.Append(",\"attributes\":{");
            for (int i = 0; i < choreGroups.Count; i++)
            {
                if (i > 0) sb.Append(',');
                float val = 0;
                try
                {
                    var attr = choreGroups[i].attribute;
                    if (attr != null)
                    {
                        var inst = go.GetAttributes().Get(attr);
                        if (inst != null) val = inst.GetTotalValue();
                    }
                }
                catch { }
                sb.Append('"').Append(Esc(choreGroups[i].Id))
                  .Append("\":").Append(Mathf.RoundToInt(val));
            }
            sb.Append('}');

            // Vitals (amounts + attributes)
            sb.Append(",\"vitals\":{");
            var amounts = Db.Get().Amounts;
            AppendAmount(sb, "stress", amounts.Stress, go, true);
            AppendAmount(sb, "calories", amounts.Calories, go, false);
            AppendAmount(sb, "bladder", amounts.Bladder, go, false);
            AppendAmount(sb, "stamina", amounts.Stamina, go, false);
            AppendAmount(sb, "breath", amounts.Breath, go, false);
            AppendAmount(sb, "hitPoints", amounts.HitPoints, go, false);
            AppendAmount(sb, "immunity", amounts.ImmuneLevel, go, false);
            AppendAmount(sb, "decor", amounts.Decor, go, false);
            try { AppendAmount(sb, "rads", amounts.RadiationBalance, go, false); } catch { }

            // Body temperature (Kelvin → Celsius)
            try
            {
                var tempInst = amounts.Temperature.Lookup(go);
                if (tempInst != null)
                    sb.Append(",\"temperature\":").Append((tempInst.value - 273.15f).ToString("F1"));
                else
                    sb.Append(",\"temperature\":0");
            }
            catch { sb.Append(",\"temperature\":0"); }

            // Morale (attribute, not amount)
            try
            {
                var moraleAttr = Db.Get().Attributes.QualityOfLife.Lookup(go);
                var moraleExpAttr = Db.Get().Attributes.QualityOfLifeExpectation.Lookup(go);
                float morale = moraleAttr?.GetTotalValue() ?? 0;
                float moraleExp = moraleExpAttr?.GetTotalValue() ?? 0;
                sb.Append(",\"morale\":").Append(Mathf.RoundToInt(morale));
                sb.Append(",\"moraleExpectation\":").Append(Mathf.RoundToInt(moraleExp));
            }
            catch
            {
                sb.Append(",\"morale\":0,\"moraleExpectation\":0");
            }

            // Distance travelled
            try
            {
                var navigator = go.GetComponent<Navigator>();
                if (navigator != null)
                {
                    int total = 0;
                    foreach (var kvp in navigator.distanceTravelledByNavType)
                        total += kvp.Value;
                    sb.Append(",\"distanceTravelled\":").Append(total);
                }
                else
                    sb.Append(",\"distanceTravelled\":0");
            }
            catch { sb.Append(",\"distanceTravelled\":0"); }

            sb.Append('}');

            // Schedule
            try
            {
                var schedulable = go.GetComponent<Schedulable>();
                var schedule = schedulable?.GetSchedule();
                sb.Append(",\"schedule\":\"").Append(Esc(schedule?.name ?? "")).Append('"');
            }
            catch { sb.Append(",\"schedule\":\"\""); }

            // Dupe type (bionic vs regular)
            bool isBionic = false;
            try { isBionic = identity.model == GameTags.Minions.Models.Bionic; } catch { }
            sb.Append(",\"isBionic\":").Append(isBionic ? "true" : "false");

            // Bionic-specific vitals
            if (isBionic)
            {
                sb.Append(",\"bionicVitals\":{");
                var ba = Db.Get().Amounts;
                try { AppendAmount(sb, "battery", ba.BionicInternalBattery, go, true); } catch { sb.Append("\"battery\":0"); }
                try { AppendAmount(sb, "oil", ba.BionicOil, go, false); } catch { }
                try { AppendAmount(sb, "gunk", ba.BionicGunk, go, false); } catch { }
                try { AppendAmount(sb, "oxygenTank", ba.BionicOxygenTank, go, false); } catch { }
                sb.Append('}');
            }

            // Equipment (suits, outfits, tools)
            sb.Append(",\"equipment\":[");
            try
            {
                var equipment = identity.GetEquipment();
                if (equipment != null)
                {
                    bool first = true;
                    foreach (var slot in equipment.Slots)
                    {
                        var equippable = slot.assignable as Equippable;
                        if (equippable != null && equippable.isEquipped)
                        {
                            if (!first) sb.Append(',');
                            sb.Append("{\"slot\":\"").Append(Esc(equippable.def.Slot))
                              .Append("\",\"id\":\"").Append(Esc(equippable.def.Id))
                              .Append("\",\"name\":\"").Append(Esc(equippable.GetProperName()))
                              .Append("\"}");
                            first = false;
                        }
                    }
                }
            }
            catch { }
            sb.Append(']');

            // Disease exposure
            sb.Append(",\"diseaseExposure\":{");
            try
            {
                var germMonitor = go.GetSMI<GermExposureMonitor.Instance>();
                if (germMonitor != null)
                {
                    var sicknesses = Db.Get().Sicknesses.resources;
                    bool first = true;
                    foreach (var sickness in sicknesses)
                    {
                        if (!first) sb.Append(',');
                        var state = germMonitor.GetExposureState(sickness.Id);
                        sb.Append('"').Append(Esc(sickness.Id)).Append("\":\"")
                          .Append(state.ToString()).Append('"');
                        first = false;
                    }
                }
            }
            catch { }
            sb.Append('}');

            // Current sicknesses
            sb.Append(",\"sicknesses\":[");
            try
            {
                var minionSicknesses = go.GetSicknesses();
                if (minionSicknesses != null)
                {
                    bool first = true;
                    foreach (SicknessInstance si in minionSicknesses)
                    {
                        if (!first) sb.Append(',');
                        sb.Append('"').Append(Esc(si.Sickness.Name)).Append('"');
                        first = false;
                    }
                }
            }
            catch { }
            sb.Append(']');

            sb.Append('}');
        }

        private static void AppendAmount(StringBuilder sb, string key, Amount amount,
            GameObject go, bool isFirst)
        {
            if (!isFirst) sb.Append(',');
            try
            {
                var inst = amount.Lookup(go);
                sb.Append('"').Append(key).Append("\":")
                  .Append(inst != null ? Mathf.RoundToInt(inst.value).ToString() : "0");
            }
            catch { sb.Append('"').Append(key).Append("\":0"); }
        }

        public static string EscStatic(string s)
        {
            if (s == null) return "";
            return StripTags(s).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
        }

        private static string Esc(string s) => EscStatic(s);

        private static readonly System.Text.RegularExpressions.Regex TagRegex =
            new System.Text.RegularExpressions.Regex(@"<[^>]+>", System.Text.RegularExpressions.RegexOptions.Compiled);

        private static string StripTags(string s)
        {
            if (s == null || s.IndexOf('<') < 0) return s;
            return TagRegex.Replace(s, "");
        }
    }

    public static class ResourceSerializer
    {
        public static string Serialize()
        {
            var sb = new StringBuilder(8192);
            sb.Append("{\"worlds\":[");

            var cm = ClusterManager.Instance;
            if (cm != null)
            {
                // Build category lookup: resource tag -> (categoryName, unit)
                var catNames = new Dictionary<Tag, string>();
                var catUnits = new Dictionary<Tag, string>();
                MapCategories(catNames, catUnits, GameTags.MaterialCategories, "mass");
                MapCategories(catNames, catUnits, GameTags.CalorieCategories, "calories");
                MapCategories(catNames, catUnits, GameTags.UnitCategories, "units");

                // Collect world info
                var worldNames = new Dictionary<int, string>();
                var worldStart = new Dictionary<int, bool>();
                var moduleInteriors = new HashSet<int>();
                foreach (var world in cm.WorldContainers)
                {
                    try
                    {
                        if (world.IsModuleInterior) { moduleInteriors.Add(world.id); continue; }
                        string name;
                        try { name = world.GetProperName(); }
                        catch { name = world.gameObject.name; }
                        worldNames[world.id] = name;
                        try { worldStart[world.id] = world.IsStartWorld; } catch { worldStart[world.id] = false; }
                    }
                    catch { }
                }

                // Iterate ALL pickupables globally — bypasses WorldInventory
                // which doesn't update for non-active DLC worlds
                // worldId -> resourceTag -> totalMass
                var worldResources = new Dictionary<int, Dictionary<Tag, float>>();
                var pickupables = Components.Pickupables.Items;
                if (pickupables != null)
                {
                    foreach (var p in pickupables)
                    {
                        try
                        {
                            int worldId = p.GetMyWorldId();
                            if (moduleInteriors.Contains(worldId)) continue;
                            if (!worldNames.ContainsKey(worldId)) continue;

                            if (p.KPrefabID.HasTag(GameTags.StoredPrivate)) continue;

                            Tag resTag = p.KPrefabID.PrefabTag;
                            float mass = p.PrimaryElement.Mass;
                            if (mass <= 0) continue;

                            if (!worldResources.ContainsKey(worldId))
                                worldResources[worldId] = new Dictionary<Tag, float>();
                            var res = worldResources[worldId];
                            if (res.ContainsKey(resTag)) res[resTag] += mass;
                            else res[resTag] = mass;
                        }
                        catch { }
                    }
                }

                // Serialize each world
                bool firstWorld = true;
                foreach (var worldId in worldNames.Keys.OrderBy(id => id))
                {
                    if (!firstWorld) sb.Append(',');
                    firstWorld = false;

                    sb.Append("{\"id\":").Append(worldId);
                    sb.Append(",\"name\":\"").Append(Esc(worldNames[worldId])).Append('"');
                    sb.Append(",\"isStartWorld\":").Append(worldStart[worldId] ? "true" : "false");
                    sb.Append(",\"categories\":[");

                    if (worldResources.TryGetValue(worldId, out var resources))
                    {
                        // Group by category
                        var groups = new Dictionary<string, List<KeyValuePair<string, float>>>();
                        var groupUnits = new Dictionary<string, string>();
                        foreach (var kvp in resources)
                        {
                            string cat = "Other";
                            string unit = "mass";
                            if (catNames.TryGetValue(kvp.Key, out var cn))
                            { cat = cn; unit = catUnits[kvp.Key]; }

                            if (!groups.ContainsKey(cat))
                            { groups[cat] = new List<KeyValuePair<string, float>>(); groupUnits[cat] = unit; }

                            string resName;
                            try { resName = kvp.Key.ProperNameStripLink(); }
                            catch { resName = kvp.Key.Name; }
                            groups[cat].Add(new KeyValuePair<string, float>(resName, kvp.Value));
                        }

                        bool firstCat = true;
                        foreach (var cat in groups.OrderBy(kv => kv.Key))
                        {
                            if (!firstCat) sb.Append(',');
                            firstCat = false;
                            var items = cat.Value;
                            items.Sort((a, b) => b.Value.CompareTo(a.Value));
                            sb.Append("{\"name\":\"").Append(Esc(cat.Key)).Append('"');
                            sb.Append(",\"unit\":\"").Append(groupUnits[cat.Key]).Append('"');
                            sb.Append(",\"resources\":[");
                            for (int i = 0; i < items.Count; i++)
                            {
                                if (i > 0) sb.Append(',');
                                sb.Append("[\"").Append(Esc(items[i].Key))
                                  .Append("\",").Append(items[i].Value.ToString("F1")).Append(']');
                            }
                            sb.Append("]}");
                        }
                    }

                    sb.Append("]}");
                }
            }

            sb.Append("]}");
            return sb.ToString();
        }

        private static void MapCategories(Dictionary<Tag, string> catNames,
            Dictionary<Tag, string> catUnits, IEnumerable<Tag> categories, string unit)
        {
            foreach (var catTag in categories)
            {
                try
                {
                    string catName;
                    try { catName = catTag.ProperNameStripLink(); }
                    catch { catName = catTag.Name; }
                    var discovered = DiscoveredResources.Instance?.GetDiscoveredResourcesFromTag(catTag);
                    if (discovered != null)
                        foreach (var resTag in discovered)
                            if (!catNames.ContainsKey(resTag))
                            { catNames[resTag] = catName; catUnits[resTag] = unit; }
                }
                catch { }
            }
        }

        private static string Esc(string s) => DupeSerializer.EscStatic(s);
    }

    public static class GeyserSerializer
    {
        public static string Serialize()
        {
            var sb = new StringBuilder(4096);
            sb.Append("{\"geysers\":[");

            var geysers = UnityEngine.Object.FindObjectsOfType<Geyser>();
            if (geysers != null)
            {
                bool first = true;
                foreach (var g in geysers.OrderBy(g => g.GetComponent<KPrefabID>()?.GetProperName() ?? ""))
                {
                    try
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        SerializeGeyser(sb, g);
                    }
                    catch { }
                }
            }

            sb.Append("]}");
            return sb.ToString();
        }

        private static void SerializeGeyser(StringBuilder sb, Geyser g)
        {
            var config = g.configuration;
            var name = g.GetComponent<KPrefabID>()?.GetProperName() ?? g.gameObject.name;
            var studyable = g.GetComponent<Studyable>();
            var uncoverable = g.GetComponent<Uncoverable>();
            bool studied = studyable != null && studyable.Studied;
            bool uncovered = uncoverable == null || uncoverable.IsUncovered;

            int cell = Grid.PosToCell(g.transform.GetPosition());
            Grid.CellToXY(cell, out int x, out int y);
            int worldId = Grid.WorldIdx[cell];

            var element = ElementLoader.FindElementByHash(config.GetElement());
            string elementName = element?.name ?? config.GetElement().ToString();
            string elementState = "unknown";
            if (element != null)
            {
                if (element.IsLiquid) elementState = "liquid";
                else if (element.IsGas) elementState = "gas";
                else if (element.IsSolid) elementState = "solid";
            }

            sb.Append("{\"name\":\"").Append(Esc(name)).Append('"');
            sb.Append(",\"element\":\"").Append(Esc(elementName)).Append('"');
            sb.Append(",\"elementState\":\"").Append(elementState).Append('"');
            sb.Append(",\"studied\":").Append(studied ? "true" : "false");
            sb.Append(",\"uncovered\":").Append(uncovered ? "true" : "false");
            sb.Append(",\"x\":").Append(x).Append(",\"y\":").Append(y);
            sb.Append(",\"worldId\":").Append(worldId);

            try
            {
                var world = ClusterManager.Instance?.GetWorld(worldId);
                sb.Append(",\"worldName\":\"").Append(Esc(world?.GetProperName() ?? "")).Append('"');
            }
            catch { sb.Append(",\"worldName\":\"\""); }

            sb.Append(",\"temperature\":").Append((config.GetTemperature() - 273.15f).ToString("F1"));

            if (studied)
            {
                sb.Append(",\"emitRate\":").Append(config.GetEmitRate().ToString("F4"));
                sb.Append(",\"massPerCycle\":").Append(config.GetMassPerCycle().ToString("F1"));
                sb.Append(",\"onDuration\":").Append(config.GetOnDuration().ToString("F1"));
                sb.Append(",\"offDuration\":").Append(config.GetOffDuration().ToString("F1"));
                sb.Append(",\"iterationLength\":").Append(config.GetIterationLength().ToString("F1"));
                sb.Append(",\"iterationPercent\":").Append(config.GetIterationPercent().ToString("F4"));
                sb.Append(",\"yearOnDuration\":").Append(config.GetYearOnDuration().ToString("F1"));
                sb.Append(",\"yearOffDuration\":").Append(config.GetYearOffDuration().ToString("F1"));
                sb.Append(",\"yearLength\":").Append(config.GetYearLength().ToString("F1"));
                sb.Append(",\"yearPercent\":").Append(config.GetYearPercent().ToString("F4"));
                sb.Append(",\"averageEmission\":").Append(config.GetAverageEmission().ToString("F4"));
                sb.Append(",\"maxPressure\":").Append(config.GetMaxPressure().ToString("F1"));
            }

            sb.Append('}');
        }

        private static string Esc(string s) => DupeSerializer.EscStatic(s);
    }

    public class VitalHistory
    {
        private const int MaxSnapshots = 600; // ~5 hours at 30s intervals

        // dupeName -> vitalKey -> list of (cycle, value)
        private readonly Dictionary<string, Dictionary<string, List<DataPoint>>> data
            = new Dictionary<string, Dictionary<string, List<DataPoint>>>();

        private static readonly string[] TrackedVitals =
            { "stress", "calories", "bladder", "stamina", "breath",
              "hitPoints", "immunity", "decor", "rads", "temperature",
              "morale", "distanceTravelled" };

        public struct DataPoint
        {
            public float cycle;
            public float value;
        }

        public void RecordSnapshot(float cycle)
        {
            var dupes = Components.LiveMinionIdentities.Items;
            if (dupes == null) return;

            var amounts = Db.Get().Amounts;

            foreach (var identity in dupes)
            {
                var name = identity.GetProperName();
                var go = identity.gameObject;

                if (!data.ContainsKey(name))
                    data[name] = new Dictionary<string, List<DataPoint>>();

                var dupeData = data[name];

                Record(dupeData, "stress", cycle, amounts.Stress, go);
                Record(dupeData, "calories", cycle, amounts.Calories, go);
                Record(dupeData, "bladder", cycle, amounts.Bladder, go);
                Record(dupeData, "stamina", cycle, amounts.Stamina, go);
                Record(dupeData, "breath", cycle, amounts.Breath, go);
                Record(dupeData, "hitPoints", cycle, amounts.HitPoints, go);
                Record(dupeData, "immunity", cycle, amounts.ImmuneLevel, go);
                Record(dupeData, "decor", cycle, amounts.Decor, go);
                try { Record(dupeData, "rads", cycle, amounts.RadiationBalance, go); } catch { }

                // Temperature
                try
                {
                    var tempInst = amounts.Temperature.Lookup(go);
                    float temp = tempInst != null ? tempInst.value - 273.15f : 0;
                    AddPoint(dupeData, "temperature", cycle, temp);
                }
                catch { }

                // Morale
                try
                {
                    var moraleInst = Db.Get().Attributes.QualityOfLife.Lookup(go);
                    AddPoint(dupeData, "morale", cycle, moraleInst?.GetTotalValue() ?? 0);
                }
                catch { }

                // Distance
                try
                {
                    var nav = go.GetComponent<Navigator>();
                    if (nav != null)
                    {
                        int total = 0;
                        foreach (var kvp in nav.distanceTravelledByNavType)
                            total += kvp.Value;
                        AddPoint(dupeData, "distanceTravelled", cycle, total);
                    }
                }
                catch { }

                // Bionic vitals
                try
                {
                    if (identity.model == GameTags.Minions.Models.Bionic)
                    {
                        Record(dupeData, "battery", cycle, amounts.BionicInternalBattery, go);
                        Record(dupeData, "oil", cycle, amounts.BionicOil, go);
                        Record(dupeData, "gunk", cycle, amounts.BionicGunk, go);
                        Record(dupeData, "oxygenTank", cycle, amounts.BionicOxygenTank, go);
                    }
                }
                catch { }
            }
        }

        private void Record(Dictionary<string, List<DataPoint>> dupeData,
            string key, float cycle, Amount amount, GameObject go)
        {
            try
            {
                var inst = amount.Lookup(go);
                AddPoint(dupeData, key, cycle, inst?.value ?? 0);
            }
            catch { }
        }

        private void AddPoint(Dictionary<string, List<DataPoint>> dupeData,
            string key, float cycle, float value)
        {
            if (!dupeData.ContainsKey(key))
                dupeData[key] = new List<DataPoint>();

            var list = dupeData[key];
            list.Add(new DataPoint { cycle = cycle, value = value });

            if (list.Count > MaxSnapshots)
                list.RemoveRange(0, list.Count - MaxSnapshots);
        }

        public string ToJson()
        {
            var sb = new StringBuilder(8192);
            sb.Append('{');
            bool firstDupe = true;
            foreach (var dupeKvp in data)
            {
                if (!firstDupe) sb.Append(',');
                sb.Append('"').Append(DupeSerializer.EscStatic(dupeKvp.Key)).Append("\":{");

                bool firstVital = true;
                foreach (var vitalKvp in dupeKvp.Value)
                {
                    if (!firstVital) sb.Append(',');
                    sb.Append('"').Append(vitalKvp.Key).Append("\":[");

                    for (int i = 0; i < vitalKvp.Value.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        var pt = vitalKvp.Value[i];
                        sb.Append('[').Append(pt.cycle.ToString("F2"))
                          .Append(',').Append(pt.value.ToString("F1")).Append(']');
                    }
                    sb.Append(']');
                    firstVital = false;
                }
                sb.Append('}');
                firstDupe = false;
            }
            sb.Append('}');
            return sb.ToString();
        }
    }

    public class ResourceHistory
    {
        private const int MaxSnapshots = 600;

        // resourceName -> { unit, points[] }
        private readonly Dictionary<string, ResourceSeries> data
            = new Dictionary<string, ResourceSeries>();

        private class ResourceSeries
        {
            public string unit;
            public readonly List<VitalHistory.DataPoint> points = new List<VitalHistory.DataPoint>();
        }

        public void RecordSnapshot(float cycle)
        {
            // Build category lookup
            var catUnits = new Dictionary<Tag, string>();
            MapUnits(catUnits, GameTags.MaterialCategories, "mass");
            MapUnits(catUnits, GameTags.CalorieCategories, "calories");
            MapUnits(catUnits, GameTags.UnitCategories, "units");

            // Collect module interiors to skip
            var moduleInteriors = new HashSet<int>();
            var cm = ClusterManager.Instance;
            if (cm == null) return;
            foreach (var world in cm.WorldContainers)
            {
                try { if (world.IsModuleInterior) moduleInteriors.Add(world.id); }
                catch { }
            }

            // Sum totals per resource across all worlds
            var totals = new Dictionary<Tag, float>();
            var pickupables = Components.Pickupables.Items;
            if (pickupables == null) return;

            foreach (var p in pickupables)
            {
                try
                {
                    if (moduleInteriors.Contains(p.GetMyWorldId())) continue;
                    if (p.KPrefabID.HasTag(GameTags.StoredPrivate)) continue;
                    float mass = p.PrimaryElement.Mass;
                    if (mass <= 0) continue;
                    Tag tag = p.KPrefabID.PrefabTag;
                    if (totals.ContainsKey(tag)) totals[tag] += mass;
                    else totals[tag] = mass;
                }
                catch { }
            }

            foreach (var kvp in totals)
            {
                string name;
                try { name = kvp.Key.ProperNameStripLink(); }
                catch { name = kvp.Key.Name; }

                string unit = "mass";
                if (catUnits.TryGetValue(kvp.Key, out var u)) unit = u;

                if (!data.TryGetValue(name, out var series))
                {
                    series = new ResourceSeries { unit = unit };
                    data[name] = series;
                }

                series.points.Add(new VitalHistory.DataPoint { cycle = cycle, value = kvp.Value });
                if (series.points.Count > MaxSnapshots)
                    series.points.RemoveRange(0, series.points.Count - MaxSnapshots);
            }
        }

        private static void MapUnits(Dictionary<Tag, string> catUnits,
            IEnumerable<Tag> categories, string unit)
        {
            foreach (var catTag in categories)
            {
                try
                {
                    var discovered = DiscoveredResources.Instance?.GetDiscoveredResourcesFromTag(catTag);
                    if (discovered != null)
                        foreach (var resTag in discovered)
                            if (!catUnits.ContainsKey(resTag))
                                catUnits[resTag] = unit;
                }
                catch { }
            }
        }

        public string ToJson()
        {
            var sb = new StringBuilder(8192);
            sb.Append('{');
            bool first = true;
            foreach (var kvp in data.OrderBy(kv => kv.Key))
            {
                if (kvp.Value.points.Count < 2) continue;
                if (!first) sb.Append(',');
                first = false;
                sb.Append('"').Append(DupeSerializer.EscStatic(kvp.Key)).Append("\":{\"unit\":\"")
                  .Append(kvp.Value.unit).Append("\",\"points\":[");
                for (int i = 0; i < kvp.Value.points.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    var pt = kvp.Value.points[i];
                    sb.Append('[').Append(pt.cycle.ToString("F2"))
                      .Append(',').Append(pt.value.ToString("F1")).Append(']');
                }
                sb.Append("]}");
            }
            sb.Append('}');
            return sb.ToString();
        }
    }
}
