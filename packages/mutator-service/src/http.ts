/**
 * The HTTP surface (docs/07 API). Deliberately built on Node's built-in
 * http server with a tiny router and zero runtime dependencies -- this
 * service is "deliberately boring" (docs/07) and nothing here needs a
 * framework. A real deployment can sit this behind any gateway.
 *
 * Auth is stubbed: the `x-account-id` header stands in for the JWT subject
 * that OAuth/OIDC will provide (docs/07 auth). Internal endpoints check the
 * shared `x-internal-key` instead.
 */

import { createServer, type IncomingMessage, type ServerResponse } from "node:http";
import { ServiceError, badRequest, unauthorized } from "./errors.js";
import type { MutatorService } from "./service.js";

type Handler = (ctx: Ctx) => unknown | Promise<unknown>;
interface Route {
  method: string;
  pattern: RegExp;
  paramNames: string[];
  handler: Handler;
  internal?: boolean;
}
interface Ctx {
  accountId: string;
  params: Record<string, string>;
  query: URLSearchParams;
  body: any;
  req: IncomingMessage;
}

const MAX_BODY = 256 * 1024; // genomes are tiny; cap requests hard

function compile(path: string): { pattern: RegExp; paramNames: string[] } {
  const names: string[] = [];
  const rx = path.replace(/:[A-Za-z0-9_]+/g, (m) => {
    names.push(m.slice(1));
    return "([^/]+)";
  });
  return { pattern: new RegExp(`^${rx}$`), paramNames: names };
}

export function createApp(service: MutatorService): ReturnType<typeof createServer> {
  const routes: Route[] = [];
  const add = (method: string, path: string, handler: Handler, internal = false) => {
    const { pattern, paramNames } = compile(path);
    routes.push({ method, pattern, paramNames, handler, internal });
  };

  // mutating operations (idempotencyKey required in body)
  add("POST", "/spawn", (c) => service.spawn(c.accountId, c.body.idempotencyKey, c.body));
  add("POST", "/mutate", (c) =>
    service.mutate(c.accountId, c.body.idempotencyKey, {
      parentId: c.body.parentId,
      options: c.body.options,
    }),
  );
  add("POST", "/splice", (c) =>
    service.splice(c.accountId, c.body.idempotencyKey, {
      parentAId: c.body.parentAId,
      parentBId: c.body.parentBId,
      noise: c.body.noise,
    }),
  );
  add("POST", "/graft", (c) =>
    service.graft(c.accountId, c.body.idempotencyKey, {
      parentId: c.body.parentId,
      slot: c.body.slot,
      family: c.body.family,
      params: c.body.params,
    }),
  );
  add("POST", "/harvest/part", (c) =>
    service.harvestPart(c.accountId, c.body.idempotencyKey, {
      creatureId: c.body.creatureId,
      slot: c.body.slot,
    }),
  );
  add("POST", "/harvest/heart", (c) =>
    service.harvestHeart(c.accountId, c.body.idempotencyKey, { creatureId: c.body.creatureId }),
  );
  add("POST", "/sew/part", (c) =>
    service.sewPart(c.accountId, c.body.idempotencyKey, {
      creatureId: c.body.creatureId,
      slot: c.body.slot,
      itemId: c.body.itemId,
    }),
  );
  add("POST", "/sew/heart", (c) =>
    service.sewHeart(c.accountId, c.body.idempotencyKey, {
      creatureId: c.body.creatureId,
      itemId: c.body.itemId,
    }),
  );

  // reads
  add("GET", "/creatures", (c) =>
    service.listCreatures(c.accountId, c.query.get("cursor") ?? undefined, Number(c.query.get("limit") ?? 50)),
  );
  add("GET", "/creature/:id", (c) => service.getCreature(c.accountId, c.params.id!));
  add("GET", "/creature/:id/lineage", (c) => ({ ancestors: service.lineage(c.accountId, c.params.id!) }));
  add("GET", "/menagerie", (c) => service.getMenagerie(c.accountId));
  add("PUT", "/menagerie", (c) => service.setMenagerie(c.accountId, c.body.creatureIds ?? []));
  add("GET", "/wallet", (c) => service.getWallet(c.accountId));
  add("GET", "/tray", (c) => ({ items: service.listTray(c.accountId) }));
  add("GET", "/catalog", (c) => ({ families: service.getCatalog(c.accountId) }));

  // internal (match servers)
  add("GET", "/roster/:accountId", (c) =>
    ({ roster: service.roster(c.req.headers["x-internal-key"] as string, c.params.accountId!) }),
    true,
  );

  return createServer((req, res) => {
    void handle(req, res, routes, service);
  });
}

async function handle(
  req: IncomingMessage,
  res: ServerResponse,
  routes: Route[],
  _service: MutatorService,
): Promise<void> {
  try {
    const url = new URL(req.url ?? "/", "http://localhost");
    const route = routes.find(
      (r) => r.method === req.method && r.pattern.test(url.pathname),
    );
    if (!route) return send(res, 404, { error: "not_found", message: `no route ${req.method} ${url.pathname}` });

    const match = route.pattern.exec(url.pathname)!;
    const params: Record<string, string> = {};
    route.paramNames.forEach((n, i) => (params[n] = decodeURIComponent(match[i + 1]!)));

    let accountId = "";
    if (route.internal) {
      if (!req.headers["x-internal-key"]) throw unauthorized("missing internal key");
    } else {
      accountId = (req.headers["x-account-id"] as string) ?? "";
      if (!accountId) throw unauthorized("missing x-account-id");
    }

    const body = await readJson(req);
    const result = await route.handler({ accountId, params, query: url.searchParams, body, req });

    // operation records carry their client-facing payload in `result`
    const payload =
      result && typeof result === "object" && "result" in (result as any) && "serverSeed" in (result as any)
        ? (result as any).result
        : result;
    send(res, 200, payload);
  } catch (err) {
    if (err instanceof ServiceError) {
      return send(res, err.status, { error: err.code, message: err.message });
    }
    send(res, 500, { error: "internal", message: (err as Error).message });
  }
}

function readJson(req: IncomingMessage): Promise<any> {
  return new Promise((resolve, reject) => {
    if (req.method === "GET" || req.method === "HEAD") return resolve({});
    let size = 0;
    const chunks: Buffer[] = [];
    req.on("data", (c: Buffer) => {
      size += c.length;
      if (size > MAX_BODY) {
        reject(badRequest("request body too large"));
        req.destroy();
        return;
      }
      chunks.push(c);
    });
    req.on("end", () => {
      const raw = Buffer.concat(chunks).toString("utf8").trim();
      if (!raw) return resolve({});
      try {
        resolve(JSON.parse(raw));
      } catch {
        reject(badRequest("body is not valid JSON"));
      }
    });
    req.on("error", reject);
  });
}

function send(res: ServerResponse, status: number, payload: unknown): void {
  const json = JSON.stringify(payload ?? null);
  res.writeHead(status, { "content-type": "application/json", "content-length": Buffer.byteLength(json) });
  res.end(json);
}
