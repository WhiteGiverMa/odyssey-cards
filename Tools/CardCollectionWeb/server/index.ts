import path from "node:path";
import express, { type ErrorRequestHandler } from "express";
import { APPLICATION_ROOT, loadCatalog, REPOSITORY_ROOT, type GameCatalog } from "./data.ts";
import { generateThemeDeck, seedFrom } from "./generator.ts";
import { RULES } from "../shared/rules.ts";

const app = express();
const port = Number(process.env.PORT ?? 8787);
app.disable("x-powered-by");
app.use(express.json({ limit: "128kb" }));
app.use((_request, response, next) => {
  response.setHeader("Cache-Control", "no-store");
  response.setHeader("Access-Control-Allow-Origin", process.env.NODE_ENV === "production" ? "same-origin" : "*");
  response.setHeader("Access-Control-Allow-Headers", "Content-Type");
  response.setHeader("Access-Control-Allow-Methods", "GET,POST,OPTIONS");
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

if (process.env.NODE_ENV === "production") {
  const staticRoot = path.join(APPLICATION_ROOT, "dist", "client");
  app.use(express.static(staticRoot, { index: false, maxAge: "1h" }));
  app.get(/.*/, (_request, response) => response.sendFile(path.join(staticRoot, "index.html")));
}

const errorHandler: ErrorRequestHandler = (error, _request, response, _next) => {
  console.error("[CardCollectionWeb]", error);
  response
    .status(500)
    .json({ error: "CATALOG_READ_FAILED", message: error instanceof Error ? error.message : String(error) });
};
app.use(errorHandler);

const server = app.listen(port, "127.0.0.1", () => {
  console.log(`[CardCollectionWeb] API http://127.0.0.1:${port}`);
  console.log(`[CardCollectionWeb] Repository ${REPOSITORY_ROOT}`);
});

for (const signal of ["SIGINT", "SIGTERM"] as const) {
  process.on(signal, () => server.close(() => process.exit(0)));
}
