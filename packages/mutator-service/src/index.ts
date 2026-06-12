/**
 * @maddr/mutator-service -- the Mutator API (docs/07). Exposes the service,
 * store contract, and HTTP app for embedding or testing; `server.ts` is the
 * standalone entry point.
 */

export * from "./service.js";
export * from "./store.js";
export * from "./economy.js";
export * from "./errors.js";
export * from "./sign.js";
export { createApp } from "./http.js";
