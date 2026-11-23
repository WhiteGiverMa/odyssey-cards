import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import { cp, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import path from "node:path";

const root = process.cwd();
const repository = path.resolve(root, "..", "..");
const fixtureRoot = await mkdtemp(path.join(tmpdir(), "odyssey-browser-qa-"));
await cp(path.join(repository, "Resources"), path.join(fixtureRoot, "Resources"), { recursive: true });
await cp(path.join(repository, "Scripts"), path.join(fixtureRoot, "Scripts"), { recursive: true });
await cp(path.join(repository, "OdysseyCards.csproj"), path.join(fixtureRoot, "OdysseyCards.csproj"));
await cp(path.join(repository, "project.godot"), path.join(fixtureRoot, "project.godot"));
const fixtureCard = path.join(fixtureRoot, "Resources", "Cards", "Spell_HeavyStrike.tres");
const originalHeavy = await readFile(fixtureCard, "utf8");

const server = spawn(process.execPath, ["dist/server/server/index.js"], {
  cwd: root,
  env: { ...process.env, NODE_ENV: "production", PORT: "8899", ODYSSEY_REPOSITORY_ROOT: fixtureRoot },
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
    "--remote-debugging-port=9333",
    "--remote-allow-origins=*",
    `--user-data-dir=${path.join(fixtureRoot, "chrome-profile")}`,
    "--window-size=1600,1000",
    "about:blank",
  ],
  { stdio: "ignore" },
);
let socket;

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
async function waitFor(url, attempts = 80) {
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
  await waitFor("http://127.0.0.1:8899/api/health");
  const pages = await (await waitFor("http://127.0.0.1:9333/json/list")).json();
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
    if (message.method === "Runtime.exceptionThrown") console.error("BROWSER", JSON.stringify(message.params));
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
    if (result.exceptionDetails)
      throw new Error(
        result.exceptionDetails.exception?.description ?? result.exceptionDetails.text ?? "Browser evaluation failed",
      );
    return result.result?.value;
  };
  const navigate = async (hash) => {
    await send("Page.navigate", { url: `http://127.0.0.1:8899/${hash}` });
    await sleep(1700);
  };
  const screenshot = async (name) => {
    const capture = await send("Page.captureScreenshot", { format: "png", captureBeyondViewport: false });
    await writeFile(path.join(root, "dist", name), Buffer.from(capture.data, "base64"));
  };
  const clickCard = async (name) => {
    const clicked = await evaluate(
      `(() => { const card = [...document.querySelectorAll('.game-card')].find((entry) => entry.textContent.includes(${JSON.stringify(name)})); if (!card) return false; card.click(); return true; })()`,
    );
    assert.equal(clicked, true, `Card not found: ${name}`);
    await sleep(250);
  };

  await send("Page.enable");
  await send("Runtime.enable");
  await navigate("#collection");
  if ((await evaluate("document.querySelectorAll('.main-nav button').length")) !== 4)
    throw new Error(`Collection did not load: ${await evaluate("location.href + ' | ' + document.body.innerText")}`);
  assert.equal(await evaluate("document.querySelectorAll('.main-nav button').length"), 4);
  assert.equal(await evaluate("document.querySelectorAll('.game-card').length"), 81);
  assert.equal(await evaluate("Boolean(document.querySelector('.deck-panel'))"), true);
  assert.match(await evaluate("document.querySelector('.collection-title h1').textContent"), /收藏与构筑/);
  await screenshot("qa-collection.png");

  await navigate("#builder");
  assert.equal(await evaluate("location.hash"), "#collection");
  assert.equal(await evaluate("Boolean(document.querySelector('.deck-panel'))"), true);

  await clickCard("沉重打击");
  assert.equal(
    await evaluate("document.querySelectorAll('.card-full-text [data-term=\"vulnerable\"]').length > 0"),
    true,
  );
  assert.equal(
    await evaluate("document.querySelectorAll('.card-full-text [data-term=\"disabled\"]').length > 0"),
    true,
  );
  assert.equal(await evaluate("Boolean(document.querySelector('.inline-tag-editor'))"), true);
  const anchor = await evaluate(
    "(() => { const r = document.querySelector('.card-full-text [data-term=\"vulnerable\"]').getBoundingClientRect(); return { x: r.x + r.width / 2, y: r.y + r.height / 2 }; })()",
  );
  await send("Input.dispatchMouseEvent", { type: "mouseMoved", x: anchor.x, y: anchor.y });
  await sleep(250);
  assert.match(await evaluate("document.querySelector('.game-popover')?.textContent ?? ''"), /1\.5/);
  await screenshot("qa-inspector-hyperlinks.png");

  // Actual PUT happens only against the system-temporary repository copy.
  await evaluate(
    "[...document.querySelectorAll('.inline-tag-editor .inline-field')][0].querySelectorAll('button')[[...document.querySelectorAll('.inline-tag-editor .inline-field')][0].querySelectorAll('button').length - 1].click()",
  );
  assert.equal(await evaluate("document.querySelector('.inline-editor-heading .primary-button').disabled"), false);
  await evaluate("document.querySelector('.inline-editor-heading .primary-button').click()");
  await sleep(800);
  const savedHeavy = await readFile(fixtureCard, "utf8");
  assert.match(savedHeavy, /Keywords = Array\[int\]\(\[12\]\)/);
  assert.ok(savedHeavy.includes("Cost = 7"));
  assert.ok(savedHeavy.includes('Description = "对一个目标造成6点伤害，使其获得3层易伤，失能3回合。"'));
  assert.ok(!savedHeavy.includes("UnitClass ="));
  assert.notEqual(savedHeavy, originalHeavy);

  const denied = await fetch("http://127.0.0.1:8899/api/cards/spell_heavy_strike/tags", {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ cost: 99 }),
  });
  assert.equal(denied.status, 400);
  assert.equal(await readFile(fixtureCard, "utf8"), savedHeavy);
  const themes = await (await fetch("http://127.0.0.1:8899/api/themes")).json();
  const themePath = path.join(fixtureRoot, "Resources", "Themes", "ThemeProfile_Ayame.tres");
  const themeBefore = await readFile(themePath, "utf8");
  const ayame = themes.themes.find((theme) => theme.id === "ayame");
  const themePut = await fetch("http://127.0.0.1:8899/api/themes/ayame", {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      tagWeights: ayame.tagWeights,
      keywordWeights: ayame.keywordWeights,
      coreCardIds: ayame.coreCardIds,
    }),
  });
  assert.equal(themePut.status, 200);
  assert.equal(await readFile(themePath, "utf8"), themeBefore);

  await evaluate("document.querySelector('.card-full-text [data-term=\"vulnerable\"]').click()");
  await sleep(450);
  assert.equal(await evaluate("location.hash"), "#wiki/term/vulnerable");
  assert.equal(
    await evaluate("document.querySelector('#term-vulnerable').classList.contains('anchor-highlight')"),
    true,
  );
  assert.equal(await evaluate("document.querySelectorAll('.glossary-entry').length"), 40);

  await evaluate("location.hash = '#collection'");
  await sleep(300);
  await clickCard("飞远");
  assert.equal(await evaluate("document.querySelectorAll('[data-card-link=\"spell_ukemi\"]').length > 0"), true);
  await evaluate("document.querySelector('.card-full-text [data-card-link=\"spell_ukemi\"]').click()");
  await sleep(200);
  assert.match(await evaluate("document.querySelector('#inspector-title').textContent"), /受身/);
  await evaluate("document.querySelector('.inspector-topbar .icon-button').click()");

  await evaluate("document.querySelector('.toast button')?.click()");
  await evaluate(
    "[...document.querySelectorAll('.main-nav button')].find((button) => button.textContent.includes('工坊')).click()",
  );
  await sleep(250);
  assert.equal(await evaluate("document.querySelectorAll('.matrix-row').length"), 81);
  await evaluate("document.querySelector('.validate-button').click()");
  await sleep(400);
  assert.equal(await evaluate("document.querySelector('.validation-report').classList.contains('ok')"), true);
  assert.match(
    await evaluate("document.querySelector('.validation-report').textContent"),
    /81 CARDS \/ 3 THEMES · 0 ERRORS/,
  );
  await screenshot("qa-workshop.png");

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

  console.log(
    JSON.stringify(
      {
        ok: true,
        navigation: ["collection", "workshop", "codex", "wiki"],
        cardsRendered: 81,
        builderRedirect: true,
        glossaryEntries: 41,
        hyperlinksAndPopover: true,
        relatedCardNavigation: true,
        temporaryRoundTripWrite: true,
        balanceWriteRejected: true,
        themeApiIdempotent: true,
        validation: "81 cards / 3 themes / 0 errors",
        screenshots: ["qa-collection.png", "qa-inspector-hyperlinks.png", "qa-workshop.png"],
      },
      null,
      2,
    ),
  );
} finally {
  if (socket && socket.readyState === WebSocket.OPEN) socket.close();
  if (chrome.exitCode === null) {
    const stopped = new Promise((resolve) => chrome.once("exit", resolve));
    chrome.kill();
    await stopped;
  }
  if (server.exitCode === null) {
    const stopped = new Promise((resolve) => server.once("exit", resolve));
    server.kill();
    await stopped;
  }
  await rm(fixtureRoot, { recursive: true, force: true, maxRetries: 8, retryDelay: 200 });
}
