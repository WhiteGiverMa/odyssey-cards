import { REPOSITORY_ROOT } from "../server/data.ts";
import { ResourceTagService } from "../server/resource-service.ts";

const [command = "help", argument] = process.argv.slice(2);
const service = new ResourceTagService(REPOSITORY_ROOT);

function usage(): void {
  console.log(`星图档案 · 标签与主题 CLI

用法：
  npm run tag -- list                    列出全部卡牌与主题摘要
  npm run tag -- dump <cardId>           JSON 转储单卡资源字段
  npm run tag -- dump theme:<heroId>     JSON 转储单主题资源字段
  npm run tag -- validate                校验未知位、越界关键词、兵种与悬空引用

当前仓库：${REPOSITORY_ROOT}`);
}

try {
  if (command === "list") {
    const cards = service.listCards();
    const themes = service.listThemes();
    for (const card of cards) {
      const unit = card.unitClass === null ? "-" : String(card.unitClass);
      console.log(
        `${String(card.id).padEnd(34)} type=${card.type} class=${unit} pack=${String(card.packId).padEnd(20)} tags=${card.mechanicTags} keywords=${JSON.stringify(card.keywords)}`,
      );
    }
    for (const theme of themes)
      console.log(
        `theme:${String(theme.id).padEnd(27)} core=${(theme.coreCardIds as string[]).length} file=${theme.fileName}`,
      );
    console.log(`\n${cards.length} cards · ${themes.length} themes`);
  } else if (command === "dump") {
    if (!argument) throw new Error("dump 需要 cardId 或 theme:<heroId>。");
    const result = service.dump(argument);
    if (!result) throw new Error(`找不到资源：${argument}`);
    console.log(JSON.stringify(result, null, 2));
  } else if (command === "validate") {
    const report = service.validate();
    console.log(JSON.stringify(report, null, 2));
    if (!report.ok) process.exitCode = 1;
  } else if (command === "help" || command === "--help" || command === "-h") {
    usage();
  } else {
    usage();
    throw new Error(`未知子命令：${command}`);
  }
} catch (error) {
  console.error(`[tag] ${error instanceof Error ? error.message : String(error)}`);
  process.exitCode = 1;
}
