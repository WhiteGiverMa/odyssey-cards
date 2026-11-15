import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import { writeFile } from "node:fs/promises";
import path from "node:path";

const root = process.cwd();
const server = spawn(process.execPath, ["dist/server/server/index.js"], {
  cwd: root,
  env: { ...process.env, NODE_ENV: "production", PORT: "8787" },
  stdio: ["ignore", "pipe", "pipe"],
});
server.stdout.on("data", (chunk) => process.stdout.write(chunk));
server.stderr.on("data", (chunk) => process.stderr.write(chunk));
const chromePath = process.env.CHROME_PATH ?? "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe";
const chrome = spawn(
  chromePath,
  [
    "--headless=new",
    "--disable-gpu",
    "--hide-scrollbars",
    "--remote-debugging-port=9222",
    "--remote-allow-origins=*",
    `--user-data-dir=${path.join(root, "dist", "chrome-browser-smoke")}`,
    "--window-size=1600,1000",
    "about:blank",
  ],
  { stdio: "ignore" },
);
let socket;

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
async function waitFor(url, attempts = 60) {
  for (let index = 0; index < attempts; index++) {
    try {
      const response = await fetch(url);
      if (response.ok) return response;
    } catch {
      /* Startup. */
    }
    await sleep(100);
  }
  throw new Error(`Readiness timeout: ${url}`);
}

try {
  await waitFor("http://127.0.0.1:8787/api/health");
  const pages = await (await waitFor("http://127.0.0.1:9222/json/list")).json();
  const page = pages.find((entry) => entry.type === "page");
  assert.ok(page?.webSocketDebuggerUrl);
  socket = new WebSocket(page.webSocketDebuggerUrl);
  await new Promise((resolve, reject) => {
    socket.addEventListener("open", resolve, { once: true });
    socket.addEventListener("error", reject, { once: true });
  });
  let sequence = 0;
  const pending = new Map();
  socket.addEventListener("message", (event) => {
    const message = JSON.parse(String(event.data));
    if (!message.id) return;
    const handler = pending.get(message.id);
    if (!handler) return;
    pending.delete(message.id);
    message.error ? handler.reject(new Error(message.error.message)) : handler.resolve(message.result);
  });
  const send = (method, params = {}) =>
    new Promise((resolve, reject) => {
      const id = ++sequence;
      pending.set(id, { resolve, reject });
      socket.send(JSON.stringify({ id, method, params }));
    });
  const evaluate = async (expression) => {
    const result = await send("Runtime.evaluate", { expression, returnByValue: true, awaitPromise: true });
    if (result.exceptionDetails) throw new Error(result.exceptionDetails.text ?? "Browser evaluation failed");
    return result.result?.value;
  };
  const navigate = async (hash) => {
    await send("Page.navigate", { url: `http://127.0.0.1:8787/${hash}` });
    await sleep(1800);
  };
  await send("Page.enable");
  await send("Runtime.enable");
  await navigate("#collection");
  assert.equal(await evaluate("document.querySelectorAll('.game-card').length"), 61);
  await evaluate("document.querySelector('.game-card .card-add:not(:disabled)').click()");
  assert.match(await evaluate("document.querySelector('.deck-total').textContent"), /01/);
  await evaluate("document.querySelector('.game-card').click()");
  await sleep(300);
  assert.equal(await evaluate("Boolean(document.querySelector('.inspector'))"), true);
  assert.equal(
    await evaluate("document.querySelectorAll('.inspector-canvas, .inspector .artwork-canvas').length > 0"),
    true,
  );
  const screenshot = await send("Page.captureScreenshot", { format: "png", captureBeyondViewport: false });
  await writeFile(path.join(root, "dist", "qa-inspector.png"), Buffer.from(screenshot.data, "base64"));
  await evaluate("document.querySelector('.inspector-topbar .icon-button').click()");
  await send("Page.reload", { ignoreCache: true });
  await sleep(1800);
  assert.match(await evaluate("document.querySelector('.deck-total').textContent"), /01/);
  await evaluate(
    "[...document.querySelectorAll('.main-nav button')].find((button) => button.textContent.includes('英雄')).click()",
  );
  await sleep(250);
  assert.equal(await evaluate("document.querySelectorAll('.hero-record').length"), 4);
  assert.equal(await evaluate("document.querySelectorAll('.weapon-row').length"), 7);
  assert.equal(await evaluate("document.querySelectorAll('.relic-record').length"), 8);
  await evaluate(
    "[...document.querySelectorAll('.main-nav button')].find((button) => button.textContent.includes('三线')).click()",
  );
  await sleep(250);
  assert.equal(await evaluate("document.querySelectorAll('.board-slot').length"), 13);
  assert.match(await evaluate("document.querySelector('.reason-readout').textContent"), /可攻击/);
  await evaluate(
    "[...document.querySelectorAll('.scenario-bar button')].find((button) => button.textContent.includes('嘲讽')).click()",
  );
  await sleep(150);
  assert.match(await evaluate("document.querySelector('.reason-readout').textContent"), /嘲讽/);
  await evaluate("document.querySelector('.language-toggle').click()");
  await sleep(150);
  assert.equal(await evaluate("document.documentElement.lang"), "en");
  assert.match(await evaluate("document.querySelector('.page-title-block h1').textContent"), /Frontline Wiki/);
  await send("Browser.close").catch(() => undefined);
  console.log(
    JSON.stringify(
      {
        ok: true,
        cardsRendered: 61,
        inspector: true,
        deckPersistence: true,
        heroes: 4,
        weapons: 7,
        relics: 8,
        boardSlots: 13,
        scenarioSwitch: true,
        languageSwitch: true,
      },
      null,
      2,
    ),
  );
} finally {
  if (socket && socket.readyState === WebSocket.OPEN) socket.close();
  if (chrome.exitCode === null) chrome.kill();
  if (server.exitCode === null) {
    const stopped = new Promise((resolve) => server.once("exit", resolve));
    server.kill();
    await stopped;
  }
}
