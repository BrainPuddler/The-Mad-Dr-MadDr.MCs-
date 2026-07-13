using System.Collections.Generic;

namespace MadDr.RosterClient
{
    /// <summary>
    /// The full result of a roster fetch, bundled for local-disk caching:
    /// the Menagerie plus every creature it references, plus when this
    /// snapshot was actually fetched. This is what the Unity client
    /// writes to Application.persistentDataPath on a successful live
    /// fetch and reads back when offline -- "most bulletproof possible...
    /// local and from internet as backup" means the offline path is a
    /// real, previously-verified roster, not a guess.
    ///
    /// No signature re-verification happens here or on the Unity side:
    /// docs/07 says genome signatures are "verified by match servers,"
    /// not by clients -- a client (this dev-mode fetch path included)
    /// was never meant to hold the verification key. The signature
    /// travels through the cache unchanged for whenever a real match
    /// server is built to consume it.
    /// </summary>
    public sealed class RosterCache
    {
        public string AccountId { get; }
        public MenagerieDto Menagerie { get; }
        public StoredGenomeDto[] Creatures { get; }

        /// <summary>ISO-8601 UTC timestamp of when this snapshot was
        /// fetched -- stamped by the Unity caller (DateTime.UtcNow),
        /// never derived here, so this package stays free of
        /// wall-clock reads.</summary>
        public string FetchedAtUtc { get; }

        public RosterCache(string accountId, MenagerieDto menagerie, StoredGenomeDto[] creatures, string fetchedAtUtc)
        {
            AccountId = accountId;
            Menagerie = menagerie;
            Creatures = creatures;
            FetchedAtUtc = fetchedAtUtc;
        }

        public static RosterCache FromJson(JsonValue v)
        {
            var creaturesArr = v.Field("creatures").AsArray();
            var creatures = new StoredGenomeDto[creaturesArr.Count];
            for (var i = 0; i < creaturesArr.Count; i++) creatures[i] = StoredGenomeDto.FromJson(creaturesArr[i]);

            return new RosterCache(
                v.Field("accountId").AsString(),
                MenagerieDto.FromJson(v.Field("menagerie")),
                creatures,
                v.Field("fetchedAtUtc").AsString());
        }

        public JsonValue ToJson()
        {
            var creaturesJson = new List<JsonValue>(Creatures.Length);
            foreach (var c in Creatures) creaturesJson.Add(c.ToJson());

            var obj = new Dictionary<string, JsonValue>();
            obj["accountId"] = JsonValue.Of(AccountId);
            obj["menagerie"] = Menagerie.ToJson();
            obj["creatures"] = JsonValue.Of(creaturesJson);
            obj["fetchedAtUtc"] = JsonValue.Of(FetchedAtUtc);
            return JsonValue.Of(obj);
        }
    }
}
