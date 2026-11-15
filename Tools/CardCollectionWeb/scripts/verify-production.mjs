import { spawn } from "node:child_process";
import { execFile } from "node:child_process";
import path from "node:path";
import { promisify } from "node:util";

const execute = promisify(execFile);
const root = process.cwd();
const child = spawn(process.execPath, ["dist/server/server/index.js"], {
  cwd: root,
  env: { ...process.env, NODE_ENV: "production", PORT: "8787" },
  stdio: ["ignore", "pipe", "pipe"],
});
child.stdout.on("data", (chunk) => process.stdout.write(chunk));
child.stderr.on("data", (chunk) => process.stderr.write(chunk));

try {
  let ready = false;
  for (let attempt = 0; attempt < 50; attempt++) {
    if (child.exitCode !== null) throw new Error(`Production server exited before readiness (${child.exitCode})`);
    try {
      const response = await fetch("http://127.0.0.1:8787/api/health");
      if (response.ok) {
        ready = true;
        break;
      }
    } catch {
      /* Server is still starting. */
    }
    await new Promise((resolve) => setTimeout(resolve, 100));
  }
  if (!ready) throw new Error("Production server readiness timeout");
  process.env.API_BASE = "http://127.0.0.1:8787";
  process.env.CHECK_CLIENT = "1";
  await import("./smoke.mjs");
  if (process.env.CAPTURE_MOBILE === "1") {
    const chrome = process.env.CHROME_PATH ?? "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe";
    await execute(
      chrome,
      [
        "--headless=new",
        "--disable-gpu",
        "--hide-scrollbars",
        "--window-size=390,844",
        "--virtual-time-budget=6000",
        `--screenshot=${path.join(root, "dist", "qa-mobile-final.png")}`,
        "http://127.0.0.1:8787/#collection",
      ],
      { timeout: 30000 },
    );
  }
} finally {
  if (child.exitCode === null) {
    const stopped = new Promise((resolve) => child.once("exit", resolve));
    child.kill();
    await stopped;
  }
}
