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
            }

            historyTimer -= Time.unscaledDeltaTime;
            if (historyTimer <= 0)
            {
                historyTimer = HistoryInterval;
                float cycle = GameClock.Instance != null ? GameClock.Instance.GetCycle() + GameClock.Instance.GetCurrentCycleAsPercentage() : 0;
                server.History.RecordSnapshot(cycle);

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
        private readonly ConcurrentQueue<PriorityChange> pendingChanges = new ConcurrentQueue<PriorityChange>();
        public readonly VitalHistory History = new VitalHistory();

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
}
