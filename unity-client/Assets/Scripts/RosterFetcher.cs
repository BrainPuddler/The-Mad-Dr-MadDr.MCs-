using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MadDr.RosterClient;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Fetches a player's Menagerie + creature genomes from mutator-service
/// (docs/07), with a local-disk cache fallback for when the service is
/// unreachable -- "most bulletproof possible... local and from internet
/// as backup." Live fetch is primary (freshest roster, matches docs/09's
/// server-authoritative posture -- the same reason match servers, not
/// clients, are the trusted fetch point there); the local cache is the
/// offline safety net, not the default path.
///
/// accountId here is docs/07's own documented dev-mode stand-in for real
/// auth (the x-account-id header -- real OAuth/JWT is the stated future
/// plan and slots in here without changing anything downstream, since
/// this script only ever sees an accountId string either way). The Lab
/// website (site/main.js) generates one random UUID per browser on first
/// visit; click its new "Account ID" header button to copy it, then
/// paste it into this component's Inspector field to see the same
/// Menagerie here.
///
/// All parsing goes through packages/roster-client (zero I/O, unit
/// tested against real captured server responses) -- this script is
/// deliberately thin: networking and file I/O only. Named RosterFetcher,
/// not RosterClient, specifically to not collide with the
/// MadDr.RosterClient namespace this file imports.
/// </summary>
public class RosterFetcher : MonoBehaviour
{
    [Tooltip("Where mutator-service is running. Defaults to the same deployed instance the Lab website (site/main.js MUTATOR_URL) uses, so a creature spawned there is fetchable here with no local server needed. Point this at http://localhost:8787 instead only if you're also running `npm start` in packages/mutator-service locally -- and note the Lab website itself is hardcoded to the deployed URL, so a locally-run Lab still needs its own MUTATOR_URL edit to match.")]
    public string baseUrl = "https://maddr-mutator.onrender.com";

    [Tooltip("Paste this from the Lab website's new \"Account ID\" header button to see the same monsters here.")]
    public string accountId = "";

    [Tooltip("Seconds before a request gives up and falls back to the local cache.")]
    public int timeoutSeconds = 8;

    /// <summary>Fired once a roster is available -- either freshly
    /// fetched (wasFromCache = false) or loaded from the local cache
    /// after a live-fetch failure (wasFromCache = true).</summary>
    public event Action<RosterCache, bool> OnRosterReady;

    /// <summary>Fired only when BOTH the live fetch and the local cache
    /// fail -- e.g. first run, offline, with nothing cached yet.</summary>
    public event Action<string> OnRosterFailed;

    private string CachePath
    {
        get { return Path.Combine(Application.persistentDataPath, "roster_cache_" + SafeFileName(accountId) + ".json"); }
    }

    public void FetchRoster()
    {
        if (string.IsNullOrEmpty(accountId))
        {
            OnRosterFailed?.Invoke("no accountId set -- copy one from the Lab website's Account ID button");
            return;
        }
        StartCoroutine(FetchRosterCoroutine());
    }

    private IEnumerator FetchRosterCoroutine()
    {
        var menagerieResult = new RequestResult();
        yield return Get(baseUrl + "/menagerie", menagerieResult);

        if (!menagerieResult.Success)
        {
            TryFallbackToCache("live fetch failed: " + menagerieResult.Error);
            yield break;
        }

        MenagerieDto menagerie;
        try
        {
            menagerie = MenagerieDto.FromJson(JsonValue.Parse(menagerieResult.Body));
        }
        catch (Exception e)
        {
            TryFallbackToCache("malformed menagerie response: " + e.Message);
            yield break;
        }

        var creatures = new List<StoredGenomeDto>();
        foreach (var id in menagerie.CreatureIds)
        {
            var creatureResult = new RequestResult();
            yield return Get(baseUrl + "/creature/" + UnityWebRequest.EscapeURL(id), creatureResult);
            if (!creatureResult.Success)
            {
                TryFallbackToCache("failed fetching creature " + id + ": " + creatureResult.Error);
                yield break;
            }
            try
            {
                creatures.Add(StoredGenomeDto.FromJson(JsonValue.Parse(creatureResult.Body)));
            }
            catch (Exception e)
            {
                TryFallbackToCache("malformed creature response for " + id + ": " + e.Message);
                yield break;
            }
        }

        var cache = new RosterCache(accountId, menagerie, creatures.ToArray(), DateTime.UtcNow.ToString("o"));
        WriteCache(cache);
        OnRosterReady?.Invoke(cache, false);
    }

    private IEnumerator Get(string url, RequestResult result)
    {
        using (var req = UnityWebRequest.Get(url))
        {
            req.timeout = timeoutSeconds;
            req.SetRequestHeader("x-account-id", accountId);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                result.Success = false;
                result.Error = req.error;
            }
            else
            {
                result.Success = true;
                result.Body = req.downloadHandler.text;
            }
        }
    }

    private void TryFallbackToCache(string reason)
    {
        var cache = ReadCache();
        if (cache != null)
        {
            Debug.LogWarning("RosterFetcher: " + reason + " -- using cached roster from " + cache.FetchedAtUtc);
            OnRosterReady?.Invoke(cache, true);
        }
        else
        {
            OnRosterFailed?.Invoke(reason + " (and no local cache exists yet)");
        }
    }

    private void WriteCache(RosterCache cache)
    {
        try
        {
            File.WriteAllText(CachePath, cache.ToJson().Serialize());
        }
        catch (Exception e)
        {
            // Cache-write failure never blocks the roster the player
            // already has in hand this run -- only next run's offline
            // fallback is degraded.
            Debug.LogWarning("RosterFetcher: failed writing local cache: " + e.Message);
        }
    }

    private RosterCache ReadCache()
    {
        try
        {
            if (!File.Exists(CachePath)) return null;
            var text = File.ReadAllText(CachePath);
            return RosterCache.FromJson(JsonValue.Parse(text));
        }
        catch (Exception e)
        {
            Debug.LogWarning("RosterFetcher: local cache unreadable: " + e.Message);
            return null;
        }
    }

    private static string SafeFileName(string s)
    {
        var sb = new StringBuilder();
        foreach (var c in s) sb.Append(char.IsLetterOrDigit(c) || c == '-' ? c : '_');
        return sb.ToString();
    }

    private sealed class RequestResult
    {
        public bool Success;
        public string Body = "";
        public string Error = "";
    }
}
