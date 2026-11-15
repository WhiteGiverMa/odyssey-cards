import type { LocalText, TacticalProfile, UnitClass } from "../shared/types.ts";

interface UnitMapping {
  unitClass: UnitClass;
  role: LocalText;
  preferredLine: TacticalProfile["preferredLine"];
  note: LocalText;
  proposedActionCost?: number;
  guard?: boolean;
  intel?: boolean;
}

/** PRD §7.1 compatibility metadata. It never overwrites an explicit resource UnitClass. */
export const PRD_UNIT_MAPPINGS: Record<string, UnitMapping> = {
  minion_18th_regiment: {
    unitClass: "vanguard",
    role: { zh: "轻步掩护", en: "Light screen" },
    preferredLine: "frontline",
    note: { zh: "推荐改配守护；轮战回库持续占线。", en: "Guard conversion with Recycle for repeated line presence." },
    guard: true,
  },
  minion_what_the_dog_doing: {
    unitClass: "vanguard",
    role: { zh: "邻格复制防线", en: "Adjacent clone guard" },
    preferredLine: "support",
    note: {
      zh: "支援线相邻复制，组成轻步兵掩护阵。",
      en: "Clones into adjacent support slots to form a defensive screen.",
    },
    guard: true,
  },
  minion_40a_main_battle_tank: {
    unitClass: "assault",
    role: { zh: "重装嘲讽铁壁", en: "Heavy taunt wall" },
    preferredLine: "frontline",
    note: {
      zh: "保留嘲讽与2防御；建议降低行动油费。",
      en: "Heavy armor and Taunt, with a proposed oil-cost reduction.",
    },
    proposedActionCost: 1,
  },
  minion_40b_main_battle_tank: {
    unitClass: "assault",
    role: { zh: "闪击抢线", en: "Blitz flanker" },
    preferredLine: "frontline",
    note: { zh: "标准闪击突击车，可推进后切后排。", en: "Charge enables advance-and-fire tempo plays." },
  },
  minion_40_main_battle_tank: {
    unitClass: "assault",
    role: { zh: "过牌重装", en: "Draw tank" },
    preferredLine: "frontline",
    note: { zh: "嘲讽与战吼抽2；建议油费1。", en: "Taunt and Battlecry draw; proposed oil cost 1." },
    proposedActionCost: 1,
  },
  minion_tombstone: {
    unitClass: "assault",
    role: { zh: "破甲特攻", en: "Armor breaker" },
    preferredLine: "frontline",
    note: { zh: "对防御目标增伤，用于拆除敌方重坦。", en: "Bonus damage into defended targets breaks heavy lines." },
    proposedActionCost: 1,
  },
  minion_centurion: {
    unitClass: "vanguard",
    role: { zh: "全局护甲中枢", en: "Armor command" },
    preferredLine: "support",
    note: { zh: "蹲支援线保护HQ，并持续为前线刷甲。", en: "Supports the HQ and repeatedly armors the frontline." },
    proposedActionCost: 2,
  },
  minion_mech_lancer: {
    unitClass: "vanguard",
    role: { zh: "伏击反突击", en: "Ambush interceptor" },
    preferredLine: "frontline",
    note: { zh: "伏击与有甲加攻，负责前线反突击。", en: "Ambush and armor scaling punish frontal assaults." },
  },
  minion_centipede_siege: {
    unitClass: "support",
    role: { zh: "攻城火炮", en: "Siege artillery" },
    preferredLine: "support",
    note: { zh: "跨线免反击输出，保留伏击防突脸。", en: "Counter-free global fire with Ambush for close defense." },
    proposedActionCost: 1,
  },
  minion_centipede_aa: {
    unitClass: "support",
    role: { zh: "部署拦截", en: "Deployment interceptor" },
    preferredLine: "support",
    note: { zh: "跨线响应低费部署并强制互殴。", en: "Intercepts cheap enemy deployments across rows." },
  },
  minion_mingshan_walking_support: {
    unitClass: "support",
    role: { zh: "直伤响应炮台", en: "Spell-response battery" },
    preferredLine: "support",
    note: { zh: "闪击冲击，响应直伤法术跨线轰击。", en: "Charge and Impact augment spell-triggered bombardment." },
  },
  minion_knight_type1: {
    unitClass: "skirmisher",
    role: { zh: "零油空袭刺客", en: "Zero-oil raider" },
    preferredLine: "flex",
    note: { zh: "0油跨线，高机动冲击空袭。", en: "Zero-oil global strikes with Charge and Impact." },
  },
  minion_rayan_ge: {
    unitClass: "skirmisher",
    role: { zh: "烟幕游击", en: "Smoke skirmisher" },
    preferredLine: "flex",
    note: { zh: "闪击首攻为英雄附加烟幕。", en: "Charge attack applies Smokescreen to the hero." },
  },
  minion_detective_squad: {
    unitClass: "skirmisher",
    role: { zh: "侦察情报", en: "Reconnaissance" },
    preferredLine: "flex",
    note: {
      zh: "闪击侦察；PRD规划登场或首攻情报1。",
      en: "Charge recon; PRD plans Intel 1 on deploy or first attack.",
    },
    intel: true,
  },
  minion_lianshu_scout: {
    unitClass: "vanguard",
    role: { zh: "零费轮战侦察", en: "Recycling scout" },
    preferredLine: "flex",
    note: {
      zh: "闪击、亡语、轮战；可承担前期抢线。",
      en: "Zero-cost Charge, Deathrattle and Recycle contest the line.",
    },
  },
  minion_lianshu_transport: {
    unitClass: "vanguard",
    role: { zh: "机动补给", en: "Mobile logistics" },
    preferredLine: "flex",
    note: { zh: "抽2回2并在任意线回抽，最多2次。", en: "Draw and heal, then recycle from either row twice." },
  },
  minion_roach: {
    unitClass: "vanguard",
    role: { zh: "支援线增殖", en: "Support-line swarm" },
    preferredLine: "support",
    note: {
      zh: "仅在支援线空槽复制，不向前线溢出。",
      en: "Copies only into free support slots, never into frontline.",
    },
  },
  minion_slime: {
    unitClass: "vanguard",
    role: { zh: "零费闪击抢线", en: "Tempo scout" },
    preferredLine: "frontline",
    note: { zh: "0费0油闪击，提供基础前线争夺。", en: "Zero-cost Charge for early frontline pressure." },
  },
  minion_smart_stinky_egg: {
    unitClass: "support",
    role: { zh: "后排定时爆破", en: "Backline demolition" },
    preferredLine: "support",
    note: { zh: "留守支援线，亡语对敌方全体法术伤害。", en: "Backline threat with global spell damage on death." },
  },
};

export function nonUnitProfile(
  cardId: string,
  typeKey: string,
  mechanicTags: number,
  domainId: string,
): TacticalProfile {
  const has = (bit: number): boolean => (mechanicTags & bit) !== 0;
  if (cardId === "spell_elbow_strike")
    return {
      role: { zh: "前线近战指令", en: "Frontline melee order" },
      preferredLine: "frontline",
      range: "melee",
      note: {
        zh: "PRD 建议赋予近战贴身特色：优先指定敌方前线随从。",
        en: "The PRD proposes a close-range identity, primarily targeting enemy frontline units.",
      },
    };
  if (typeKey === "domain" || domainId || has(1024))
    return {
      role: { zh: "永久领域", en: "Permanent Domain" },
      preferredLine: "flex",
      range: "global",
      note: {
        zh: "永久 Power：Counter 由战斗事件消耗，不进行自动回合衰减。",
        en: "Permanent Power: counters are consumed by combat events, never by automatic turn decay.",
      },
    };
  if (typeKey === "status")
    return {
      role: { zh: "战斗状态", en: "Combat status" },
      preferredLine: "flex",
      range: "global",
      note: {
        zh: "战斗中生成的临时卡牌，不可加入永久构筑牌组。",
        en: "A temporary combat card that cannot enter a permanent constructed deck.",
      },
    };
  if (has(64))
    return {
      role: { zh: "支援线部署指令", en: "Support deployment order" },
      preferredLine: "support",
      range: "global",
      note: {
        zh: "召唤与复制严格进入所属方支援线；满4格时溢出消失，绝不自动进入前线。",
        en: "Summons and copies enter only their support row. Overflow is lost at four slots, never moved to frontline.",
      },
    };
  if (has(1) || has(2))
    return {
      role: { zh: "全图火力指令", en: "Global fire order" },
      preferredLine: "flex",
      range: "global",
      note: {
        zh: "除特别注明外，单体法术默认无视前线与阵线距离，仍遵循目标标签校验。",
        en: "Unless explicitly limited, targeted spells ignore frontline and row distance while retaining target-tag checks.",
      },
    };
  if (has(16) || has(32) || has(512) || has(16384))
    return {
      role: { zh: "资源与牌堆调度", en: "Resource & deck control" },
      preferredLine: "flex",
      range: "global",
      note: {
        zh: "通过抽牌、发现、弃牌或洗牌调整资源；不直接改变前线归属。",
        en: "Manipulates draw, discovery, discard or shuffle resources without directly changing frontline control.",
      },
    };
  if (has(4) || has(8) || has(128))
    return {
      role: { zh: "后勤续航指令", en: "Logistics & sustain" },
      preferredLine: "support",
      range: "global",
      note: {
        zh: "提供治疗、格挡或随从增益，可在支援线提前强化再推进前线。",
        en: "Healing, Block and buffs can prepare units in support before they advance.",
      },
    };
  return {
    role: { zh: "通用战术指令", en: "General tactical order" },
    preferredLine: "flex",
    range: "global",
    note: {
      zh: "法术目标默认不受前线阻挡；具体目标与效果以资源效果和本地化文本为准。",
      en: "Spell targets ignore frontline by default; resource effects and localized text define the exact behavior.",
    },
  };
}

export function profileFor(cardId: string): TacticalProfile | null {
  const mapping = PRD_UNIT_MAPPINGS[cardId];
  if (!mapping) return null;
  return {
    role: mapping.role,
    preferredLine: mapping.preferredLine,
    range: mapping.unitClass === "support" || mapping.unitClass === "skirmisher" ? "global" : "melee",
    note: mapping.note,
    proposedActionCost: mapping.proposedActionCost,
    proposedKeywords: [...(mapping.guard ? ["guard"] : []), ...(mapping.intel ? ["intel"] : [])],
  };
}
