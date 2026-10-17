using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

// One snapshot owns journey data. A transaction writes and flushes a temporary file,
// then atomically replaces the primary. PlayerPrefs remains the settings/legacy store.
public static class JourneyStore
{
    public const int SchemaVersion = 2;
    [Serializable] class Entry { public string key, value; }
    [Serializable] class Snapshot { public int version; public string campaign; public List<Entry> entries; }
    [Serializable] class Envelope { public string payload, checksum; }
    static Dictionary<string, string> values;
    static string campaign;
    static string diskCampaign;
    static bool dirty, inTransaction;
    public static string Error { get; private set; }
    public static string Recovery { get; private set; }
    public static bool InTransaction => inTransaction;
    public static string Campaign { get { Ensure(); return campaign; } }

    public static string DirectoryPath
    {
        get
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-greyspire-save-dir") return Path.GetFullPath(args[i + 1]);
#if UNITY_EDITOR
            // Every existing command-line test is isolated, including across domain reloads.
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-executeMethod")
                    return Path.GetFullPath(Path.Combine("Logs", "test-saves", args[i + 1] + "-" + System.Diagnostics.Process.GetCurrentProcess().Id));
#endif
            return Path.Combine(Application.persistentDataPath, "journey", Application.isEditor ? "editor" : "player");
        }
    }
    public static string FilePath => Path.Combine(DirectoryPath, "journey.json");
    public static string BackupPath => FilePath + ".bak";
    public static string CampaignPath => FilePath + ".campaign";

    static string Hash(string value)
    {
        using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(value)));
    }

    static Snapshot Read(string path)
    {
        var envelope = JsonUtility.FromJson<Envelope>(File.ReadAllText(path, Encoding.UTF8));
        if (envelope == null || string.IsNullOrEmpty(envelope.payload) || envelope.checksum != Hash(envelope.payload))
            throw new InvalidDataException("Save checksum failed: " + path);
        var snapshot = JsonUtility.FromJson<Snapshot>(envelope.payload);
        if (snapshot != null && (snapshot.version < 1 || snapshot.version > SchemaVersion))
            throw new NotSupportedException("Unsupported journey save version " + snapshot.version + ": " + path);
        if (snapshot == null || string.IsNullOrEmpty(snapshot.campaign) || snapshot.entries == null)
            throw new InvalidDataException("Incomplete journey save: " + path);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in snapshot.entries)
            if (entry == null || string.IsNullOrEmpty(entry.key) || entry.value == null || !keys.Add(entry.key))
                throw new InvalidDataException("Invalid journey entry: " + path);
        return snapshot;
    }

    public static void Ensure()
    {
        if (values != null) return;
        try
        {
            Snapshot snapshot = null;
            if (File.Exists(FilePath))
            {
                try { snapshot = Read(FilePath); }
                catch (InvalidDataException) { if (!File.Exists(BackupPath)) throw; }
                catch (ArgumentException) { if (!File.Exists(BackupPath)) throw; }
            }
            if (snapshot == null && File.Exists(BackupPath))
            {
                snapshot = Read(BackupPath);
                if (!File.Exists(CampaignPath) || File.ReadAllText(CampaignPath, Encoding.UTF8) != snapshot.campaign)
                    throw new InvalidDataException("Backup belongs to an unconfirmed campaign; existing files have been kept.");
                Recovery = "Your journey was recovered from its backup.";
                // Preserve the damaged original for inspection; repair without rotating it into the backup.
                if (File.Exists(FilePath)) File.Copy(FilePath, FilePath + ".damaged", true);
                AtomicWrite(FilePath, File.ReadAllBytes(BackupPath), null);
            }
            if (snapshot == null && File.Exists(CampaignPath))
                throw new InvalidDataException("Committed journey files are missing; legacy progress will not be reimported.");
            // A valid primary is authoritative, including after an interrupted reset.
            // The marker fences off older-campaign backups when the primary is damaged.
            diskCampaign = File.Exists(CampaignPath) ? File.ReadAllText(CampaignPath, Encoding.UTF8) : null;
            if (snapshot != null && diskCampaign != snapshot.campaign)
            {
                AtomicWrite(CampaignPath, Encoding.UTF8.GetBytes(snapshot.campaign), null);
                diskCampaign = snapshot.campaign;
            }
            values = new Dictionary<string, string>(StringComparer.Ordinal);
            dirty = false;
            campaign = snapshot == null ? Guid.NewGuid().ToString("N") : snapshot.campaign;
            if (snapshot != null) foreach (var entry in snapshot.entries) values.Add(entry.key, entry.value);
            else
            {
                Save.ImportLegacy();
                dirty = true;
            }
            if (GetInt("seedPolicy") > ProductionRandom.PolicyVersion)
                throw new NotSupportedException("Future random-stream policy; journey files have been kept.");
            PilotProgress.Validate();
            ChapterProgress.Validate();
            if (snapshot != null && snapshot.version == SchemaVersion && !Save.ObjectivesMigrated)
                throw new NotSupportedException("Schema 2 objective records are missing or unsupported; existing files have been kept.");
            if (snapshot != null && snapshot.version == 1)
            {
                // Keep the exact pre-upgrade file independently of the rotating
                // recovery backup. Payload hash makes retries and later upgrades
                // preserve their corresponding originals without path input.
                string digest = Hash(File.ReadAllText(FilePath, Encoding.UTF8)).Replace('/', '_').Replace('+', '-').TrimEnd('=');
                string archive = FilePath + ".v1." + digest + ".pre-objectives";
                if (!File.Exists(archive)) AtomicWrite(archive, File.ReadAllBytes(FilePath), null);
            }
            // v1 remains readable. Upgrade its objective identities atomically;
            // old builds reject v2 instead of misreading newly reordered content.
            if (snapshot == null || snapshot.version < SchemaVersion || !Save.ObjectivesMigrated)
            {
                Transaction(() => { Save.MigrateObjectives(); dirty = true; return true; }, true);
            }
            Error = null;
        }
        catch (Exception e)
        {
            values = null;
            Error = "Your journey could not be loaded. Existing save files have been kept. " + e.Message;
            throw;
        }
    }

    public static bool HasKey(string key) { Ensure(); return values.ContainsKey(key); }
    public static string GetString(string key, string fallback = "") { Ensure(); return values.TryGetValue(key, out string value) ? value : fallback; }
    public static int GetInt(string key, int fallback = 0) => int.TryParse(GetString(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : fallback;
    public static float GetFloat(string key, float fallback = 0f) => float.TryParse(GetString(key), NumberStyles.Float, CultureInfo.InvariantCulture, out float n) ? n : fallback;
    public static void SetString(string key, string value)
    {
        Ensure();
        if (values.TryGetValue(key, out string old) && old == value) return;
        values[key] = value;
        dirty = true;
#if UNITY_EDITOR
        AfterMutationForTest?.Invoke(key);
#endif
    }
    public static void SetInt(string key, int value) => SetString(key, value.ToString(CultureInfo.InvariantCulture));
    public static void SetFloat(string key, float value) => SetString(key, value.ToString("R", CultureInfo.InvariantCulture));

    public static T Transaction<T>(Func<T> command, bool flushBackup = false)
    {
        Ensure();
        if (inTransaction) return command();
        var before = new Dictionary<string, string>(values, StringComparer.Ordinal);
        string beforeCampaign = campaign;
        bool beforeDirty = dirty;
        inTransaction = true;
        try
        {
            T result = command();
            WriteSnapshot(flushBackup);
            // Publish only committed mutations, never a nested or failed write.
            if (PlaytestLog.Active != null)
                foreach (var entry in values)
                    if (!before.TryGetValue(entry.Key, out string old) || old != entry.Value)
                        PlaytestLog.Emit("save_commit", note: entry.Key + "=" + entry.Value);
            return result;
        }
        catch (Exception e)
        {
            values = before;
            campaign = beforeCampaign;
            dirty = beforeDirty;
            Error = "Progress could not be saved. The last committed journey is safe. " + e.Message;
            throw;
        }
        finally { inTransaction = false; }
    }
    public static void Transaction(Action command) => Transaction(() => { command(); return true; });

    public static void Flush(bool flushBackup = true)
    {
        Ensure();
        if (inTransaction) return;
        try { WriteSnapshot(flushBackup); }
        catch (Exception e) { Error = "Progress could not be saved. " + e.Message; throw; }
    }

    static void WriteSnapshot(bool flushBackup = false)
    {
        if (!dirty)
        {
            if (flushBackup && File.Exists(FilePath)) RefreshBackup(File.ReadAllBytes(FilePath));
            return;
        }
        var snapshot = new Snapshot { version = SchemaVersion, campaign = campaign, entries = new List<Entry>() };
        foreach (var pair in values) snapshot.entries.Add(new Entry { key = pair.Key, value = pair.Value });
        snapshot.entries.Sort((a, b) => string.CompareOrdinal(a.key, b.key));
        string payload = JsonUtility.ToJson(snapshot);
        byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Envelope { payload = payload, checksum = Hash(payload) }));
        Directory.CreateDirectory(DirectoryPath);
        if (diskCampaign != campaign)
        {
            // Fence the candidate campaign before swapping the primary. If the swap
            // fails, the still-valid old primary repairs this marker on the next load.
            AtomicWrite(CampaignPath, Encoding.UTF8.GetBytes(campaign), null);
            diskCampaign = campaign;
        }
        AtomicWrite(FilePath, bytes, BackupPath);
        // Successful replacement is the commit point. A failed backup refresh cannot roll it back.
        dirty = false;
        Error = null;
        // File.Replace rotates the previous complete primary into the backup.
        // Explicit checkpoints/migration/new-game refresh both copies; frequent
        // reward transactions need only the primary's disk barrier.
        if (flushBackup) RefreshBackup(bytes);
    }

    static void RefreshBackup(byte[] bytes)
    {
        try { AtomicWrite(BackupPath, bytes, null); }
        catch (IOException e) { Debug.LogWarning("Journey committed; backup refresh failed: " + e.Message); }
        catch (UnauthorizedAccessException e) { Debug.LogWarning("Journey committed; backup refresh failed: " + e.Message); }
    }

    static void AtomicWrite(string target, byte[] bytes, string backup)
    {
        string temporary = target + ".tmp";
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.None))
        {
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(true);
        }
#if UNITY_EDITOR
        BeforeReplaceForTest?.Invoke(target);
#endif
        if (File.Exists(target)) File.Replace(temporary, target, backup);
        else File.Move(temporary, target);
    }

    public static void NewJourney()
    {
        Transaction(() =>
        {
            values.Clear();
            campaign = Guid.NewGuid().ToString("N");
            Save.InitializeObjectives();
            dirty = true;
            return true;
        }, true);
    }

#if UNITY_EDITOR
    public static Action<string> BeforeReplaceForTest;
    public static Action<string> AfterMutationForTest;
    public static void ReloadForTest() { values = null; diskCampaign = null; dirty = inTransaction = false; Error = Recovery = null; }
#endif
}
