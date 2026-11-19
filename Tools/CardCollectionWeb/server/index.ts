import path from "node:path";
import express, { type ErrorRequestHandler } from "express";
import { APPLICATION_ROOT, loadCatalog, REPOSITORY_ROOT, type GameCatalog } from "./data.ts";
import { generateThemeDeck, seedFrom } from "./generator.ts";
import { RULES } from "../shared/rules.ts";
import { summarizePacks } from "../shared/packs.ts";
import { ResourceInputError, ResourceNotFoundError, ResourceTagService } from "./resource-service.ts";

const app = express();
const resources = new ResourceTagService(REPOSITORY_ROOT);
const port = Number(process.env.PORT ?? 8787);
app.disable("x-powered-by");
app.use(express.json({ limit: "128kb" }));
app.use((_request, response, next) => {
  response.setHeader("Cache-Control", "no-store");
  response.setHeader("Access-Control-Allow-Origin", process.env.NODE_ENV === "production" ? "same-origin" : "*");
  response.setHeader("Access-Control-Allow-Headers", "Content-Type");
  response.setHeader("Access-Control-Allow-Methods", "GET,POST,PUT,OPTIONS");
  next();
});
app.options("/api/*path", (_request, response) => response.sendStatus(204));

async function catalog(): Promise<GameCatalog> {
  return loadCatalog();
}

app.get("/api/health", (_request, response) => {
  response.json({ ok: true, service: "odyssey-card-collection", time: new Date().toISOString() });
});

app.get("/api/cards", async (_request, response, next) => {
  try {
    const data = await catalog();
    response.json({
      cards: data.cards,
      total: data.cards.length,
      generatedAt: data.generatedAt,
      repository: REPOSITORY_ROOT,
    });
  } catch (error) {
    next(error);
  }
});

app.get("/api/cards/:id", async (request, response, next) => {
  try {
    const data = await catalog();
    const card = data.cards.find((entry) => entry.id === request.params.id);
    if (!card) return response.status(404).json({ error: "CARD_NOT_FOUND", id: request.params.id });
    return response.json(card);
  } catch (error) {
    next(error);
  }
});

app.put("/api/cards/:id/tags", async (request, response, next) => {
  try {
    await resources.saveCard(request.params.id, request.body);
    const data = await catalog();
    const card = data.cards.find((entry) => entry.id === request.params.id);
    if (!card) return response.status(404).json({ error: "CARD_NOT_FOUND", id: request.params.id });
    return response.json(card);
  } catch (error) {
    next(error);
  }
});

/** 卡包清单：标准卡包 + 从 .tres 自动发现的自定义卡包，均带实时数量。 */
app.get("/api/packs", async (_request, response, next) => {
  try {
    const data = await catalog();
    const packs = summarizePacks(data.cards.map((card) => card.packId));
    return response.json({ packs, total: data.cards.length, generatedAt: data.generatedAt });
  } catch (error) {
    next(error);
  }
});

/** 批量归类：把选中的多张卡牌一次性写回目标 PackId，并返回最新 catalog。 */
app.post("/api/packs/assign", async (request, response, next) => {
  try {
    const result = await resources.assignPack(request.body?.cardIds, request.body?.packId);
    const data = await catalog();
    return response.json({
      cards: data.cards,
      total: data.cards.length,
      generatedAt: data.generatedAt,
      updated: result.updated,
      missing: result.missing,
      packId: result.packId,
    });
  } catch (error) {
    next(error);
  }
});

app.get("/api/heroes", async (_request, response, next) => {
  try {
    const data = await catalog();
    response.json({ heroes: data.heroes, weapons: data.weapons, total: data.heroes.length });
  } catch (error) {
    next(error);
  }
});

app.get("/api/relics", async (_request, response, next) => {
  try {
    const data = await catalog();
    response.json({ relics: data.relics, total: data.relics.length });
  } catch (error) {
    next(error);
  }
});

app.get("/api/themes", async (_request, response, next) => {
  try {
    const data = await catalog();
    response.json({ themes: data.themes, total: data.themes.length });
  } catch (error) {
    next(error);
  }
});

app.put("/api/themes/:id", async (request, response, next) => {
  try {
    await resources.saveTheme(request.params.id, request.body);
    const data = await catalog();
    const theme = data.themes.find((entry) => entry.id === request.params.id);
    if (!theme) return response.status(404).json({ error: "THEME_NOT_FOUND", id: request.params.id });
    return response.json(theme);
  } catch (error) {
    next(error);
  }
});

app.post("/api/themes/:id/generate", async (request, response, next) => {
  try {
    const data = await catalog();
    const theme = data.themes.find((entry) => entry.id === request.params.id);
    if (!theme) return response.status(404).json({ error: "THEME_NOT_FOUND", id: request.params.id });
    const seed = seedFrom(request.body?.seed);
    return response.json(generateThemeDeck(theme, data.cards, seed));
  } catch (error) {
    next(error);
  }
});

app.get("/api/rules", (_request, response) => response.json(RULES));
app.get("/api/validate", (_request, response) => response.json(resources.validate()));

if (process.env.NODE_ENV === "production") {
  const staticRoot = path.join(APPLICATION_ROOT, "dist", "client");
  const sendClient: express.RequestHandler = (_request, response) =>
    response.sendFile("index.html", { root: staticRoot });
  app.get("/", sendClient);
  app.use(express.static(staticRoot, { index: false, maxAge: "1h" }));
  app.get(/.*/, sendClient);
}

const errorHandler: ErrorRequestHandler = (error, _request, response, _next) => {
  const status = error instanceof ResourceInputError || error instanceof ResourceNotFoundError ? error.status : 500;
  if (status === 500) console.error("[CardCollectionWeb]", error);
  response.status(status).json({
    error: status === 500 ? "CATALOG_READ_FAILED" : "RESOURCE_WRITE_REJECTED",
    message: error instanceof Error ? error.message : String(error),
  });
};
app.use(errorHandler);

const server = app.listen(port, "127.0.0.1", () => {
  console.log(`[CardCollectionWeb] API http://127.0.0.1:${port}`);
  console.log(`[CardCollectionWeb] Repository ${REPOSITORY_ROOT}`);
  if (process.env.NODE_ENV === "production") console.log(`[CardCollectionWeb] Static ${APPLICATION_ROOT}`);
});

for (const signal of ["SIGINT", "SIGTERM"] as const) {
  process.on(signal, () => server.close(() => process.exit(0)));
}
