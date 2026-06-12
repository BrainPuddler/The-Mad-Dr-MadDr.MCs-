/** Bootstrap: wire an in-memory store + service to the HTTP app and listen.
 * Config from the environment; secrets must be set in any real deployment. */

import { createApp } from "./http.js";
import { MutatorService } from "./service.js";
import { InMemoryStore } from "./store.js";

const port = Number(process.env.PORT ?? 8787);
const signingKey = process.env.SIGNING_KEY ?? "dev-signing-key-change-me";
const internalKey = process.env.INTERNAL_KEY ?? "dev-internal-key-change-me";

if (process.env.NODE_ENV === "production" && (signingKey.startsWith("dev-") || internalKey.startsWith("dev-"))) {
  throw new Error("refusing to start in production with default dev keys");
}

const service = new MutatorService(new InMemoryStore(), { signingKey, internalKey });
const app = createApp(service);

app.listen(port, () => {
  console.log(`mutator-service listening on :${port} (store: in-memory)`);
});
