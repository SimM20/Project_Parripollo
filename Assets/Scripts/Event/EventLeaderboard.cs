using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Leaderboard del evento: cada run cerrada (<see cref="EventRun.Finish"/>) se guarda en
/// <c>event_leaderboard.json</c> dentro de <see cref="Application.persistentDataPath"/> y se
/// sube a la tabla <c>event_leaderboard</c> de Supabase, la que lee la página del ranking.
///
/// El archivo local es la fuente de verdad y el respaldo si se corta internet: lo que no
/// subió queda marcado como pendiente y se reintenta cada <see cref="RetrySeconds"/>
/// mientras el juego esté abierto, y otra vez al próximo arranque. Cada entrada lleva su
/// propio id, así que un reintento de algo que ya había llegado no la duplica (409).
///
/// La clave es la publicable del proyecto (la misma que usa la página del ranking): solo permite insertar.
/// </summary>
public class EventLeaderboard : MonoBehaviour
{
    // Proyecto propio del ranking, separado del de la encuesta de la web (ranking-evento-config.js en Parrilla40-Web).
    private const string SupabaseUrl = "https://qbaktsjyetnyvmfegwxu.supabase.co";
    private const string SupabasePublicKey = "sb_publishable_cSxHPxODM1I6edbuNpSqdw_Lw3wLf0P";
    private const string Endpoint = SupabaseUrl + "/rest/v1/event_leaderboard";

    private const string FileName = "event_leaderboard.json";
    private const int TimeoutSeconds = 15;
    private const float RetrySeconds = 30f;

    [Serializable]
    public class Entry
    {
        public string id;
        public string player_name;
        public int total_earned;
        public string created_at;
        public bool uploaded;
    }

    [Serializable]
    private class Store
    {
        public List<Entry> entries = new List<Entry>();
    }

    // Lo que viaja a Supabase: created_at y uploaded son solo locales.
    [Serializable]
    private class Payload
    {
        public string id;
        public string player_name;
        public int total_earned;
    }

    private static Store store;
    private static EventLeaderboard runner;
    private Coroutine uploadRoutine;

    private static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    /// <summary>Todas las runs guardadas en esta máquina, subidas o no.</summary>
    public static IReadOnlyList<Entry> Entries => Load().entries;

    public static void Record(string playerName, float totalEarned, bool upload)
    {
        var entry = new Entry
        {
            id = Guid.NewGuid().ToString(),
            player_name = playerName,
            total_earned = Mathf.Max(0, Mathf.RoundToInt(totalEarned)),
            created_at = DateTime.UtcNow.ToString("o"),
            uploaded = false
        };

        Load().entries.Add(entry);
        Save();

        Debug.Log("[EventLeaderboard] Guardado: " + entry.player_name + " → $" + entry.total_earned);

        if (upload) UploadPending();
    }

    public static void UploadPending()
    {
        if (!HasPending()) return;

        if (runner == null)
        {
            var go = new GameObject("EventLeaderboard");
            DontDestroyOnLoad(go);
            runner = go.AddComponent<EventLeaderboard>();
        }

        if (runner.uploadRoutine == null)
            runner.uploadRoutine = runner.StartCoroutine(runner.UploadLoop());
    }

    private static bool HasPending() => Load().entries.Exists(e => !e.uploaded);

    // Lo que quedó sin subir la última vez (sin internet, juego cerrado a mitad de run).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void UploadLeftoversOnLaunch() => UploadPending();

    private IEnumerator UploadLoop()
    {
        while (true)
        {
            List<Entry> pending = Load().entries.FindAll(e => !e.uploaded);
            if (pending.Count == 0) break;

            bool allSent = true;
            foreach (Entry entry in pending)
            {
                bool sent = false;
                yield return Send(entry, ok => sent = ok);

                if (!sent)
                {
                    allSent = false;
                    break;
                }

                entry.uploaded = true;
                Save();
            }

            if (allSent) continue;

            // Realtime: la pausa pone timeScale en 0.
            yield return new WaitForSecondsRealtime(RetrySeconds);
        }

        uploadRoutine = null;
    }

    private static IEnumerator Send(Entry entry, Action<bool> done)
    {
        var payload = new Payload
        {
            id = entry.id,
            player_name = entry.player_name,
            total_earned = entry.total_earned
        };
        byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));

        using (var request = new UnityWebRequest(Endpoint, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("apikey", SupabasePublicKey);
            request.SetRequestHeader("Prefer", "return=minimal");
            request.timeout = TimeoutSeconds;

            yield return request.SendWebRequest();

            // 409 = ese id ya estaba: un reintento de algo que había llegado.
            bool ok = request.result == UnityWebRequest.Result.Success || request.responseCode == 409;

            if (ok)
                Debug.Log("[EventLeaderboard] Subido: " + entry.player_name + " → $" + entry.total_earned);
            else
                Debug.LogWarning(
                    "[EventLeaderboard] No se pudo subir (" + request.responseCode + " " + request.error +
                    "). Queda guardado y se reintenta. " + request.downloadHandler.text
                );

            done(ok);
        }
    }

    private static Store Load()
    {
        if (store != null) return store;

        store = new Store();
        if (!File.Exists(FilePath)) return store;

        try
        {
            Store loaded = JsonUtility.FromJson<Store>(File.ReadAllText(FilePath));
            if (loaded != null && loaded.entries != null) store = loaded;
        }
        catch (Exception e)
        {
            // Nunca pisar un archivo que no se pudo leer: ahí están los puntajes del evento.
            string backup = FilePath + "." + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".bak";
            File.Move(FilePath, backup);
            Debug.LogError("[EventLeaderboard] Archivo ilegible, movido a " + backup + ": " + e.Message);
        }

        return store;
    }

    private static void Save()
    {
        try
        {
            string temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(Load(), true));
            if (File.Exists(FilePath)) File.Delete(FilePath);
            File.Move(temp, FilePath);
        }
        catch (Exception e)
        {
            Debug.LogError("[EventLeaderboard] No se pudo guardar " + FilePath + ": " + e.Message);
        }
    }

    private void OnDestroy()
    {
        if (runner == this) runner = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        store = null;
        runner = null;
    }
}
