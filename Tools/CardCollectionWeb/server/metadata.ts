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
      zh: "保留嘲讽与2防御；0.2.3 行动花费定为3。",
      en: "Heavy armor and Taunt; the 0.2.3 balance pass sets its oil cost to 3.",
    },
    proposedActionCost: 3,
  },
  minion_40b_main_battle_tank: {
    unitClass: "assault",
    role: { zh: "闪击抢线", en: "Blitz flanker" },
    preferredLine: "frontline",
    note: {
      zh: "标准闪击突击车，防御力降为0换取推进后切后排的机动。",
      en: "Charge flanks after advancing; the 0.2.3 pass trades its Defense down to 0.",
    },
    proposedActionCost: 1,
  },
  minion_40_main_battle_tank: {
    unitClass: "assault",
    role: { zh: "过牌重装", en: "Draw tank" },
    preferredLine: "frontline",
    note: {
      zh: "嘲讽与部署抽2；0.2.3 行动花费定为2。",
      en: "Taunt and Deploy draw 2; the 0.2.3 pass sets its oil cost to 2.",
    },
    proposedActionCost: 2,
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
    unitClass: "vanguard",
    role: { zh: "冲击重骑", en: "Impact rider" },
    preferredLine: "frontline",
    note: {
      zh: "0.2.3 改为先锋：4费1行动，4攻0防2生命，具有冲击时攻击力+2。",
      en: "0.2.3 makes it a Vanguard: 4 cost / 1 oil, 4 ATK / 0 DEF / 2 HP, +2 ATK while it has Impact.",
    },
    proposedActionCost: 1,
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
  // 0.2.3「早期战争」新增核心单位：.tres 已写入 UnitClass，此处补充战术定位与备注。
  minion_113th_garrison: {
    unitClass: "vanguard",
    role: { zh: "零油守备", en: "Free garrison" },
    preferredLine: "frontline",
    note: {
      zh: "1费0行动花费的守备营，廉价占住支援线并推进前线。",
      en: "A 1-cost, zero-oil garrison that cheaply holds a support slot and advances.",
    },
    proposedActionCost: 0,
  },
  minion_122nd_bugle_battalion: {
    unitClass: "vanguard",
    role: { zh: "守护号手", en: "Guard bugler" },
    preferredLine: "frontline",
    note: {
      zh: "自带守护的廉价先锋，与相邻单位互成防线。",
      en: "A cheap Vanguard with Guard that anchors an adjacent defensive pair.",
    },
    proposedActionCost: 1,
  },
  minion_232nd_vanguard_battalion: {
    unitClass: "vanguard",
    role: { zh: "闪击前压", en: "Charging spearhead" },
    preferredLine: "frontline",
    note: {
      zh: "闪击进场，部署即对一个敌方目标造成1点伤害。",
      en: "Charge entry plus Deploy: deal 1 damage to an enemy target.",
    },
    proposedActionCost: 1,
  },
  minion_type15a_plateau_tank: {
    unitClass: "assault",
    role: { zh: "高原突进", en: "Plateau breakthrough" },
    preferredLine: "frontline",
    note: {
      zh: "部署抽1；突击推进后可直接开火。",
      en: "Deploy draws 1; as an Assault it can advance and fire in one turn.",
    },
    proposedActionCost: 1,
  },
  minion_m10_booker: {
    unitClass: "assault",
    role: { zh: "闪击轻坦", en: "Charging light tank" },
    preferredLine: "frontline",
    note: {
      zh: "闪击；场上有友方先锋时行动花费-1。",
      en: "Charge; costs 1 less oil while a friendly Vanguard is on the board.",
    },
    proposedActionCost: 1,
  },
  minion_shadow_drone: {
    unitClass: "skirmisher",
    role: { zh: "闪击游击", en: "Charging skirmisher" },
    preferredLine: "flex",
    note: {
      zh: "闪击游击，可跨线骚扰但正常承受反击。",
      en: "Charging Skirmisher that harasses across rows while still taking retaliation.",
    },
    proposedActionCost: 1,
  },
  minion_hover_gunship_drone: {
    unitClass: "support",
    role: { zh: "守护炮艇", en: "Guard gunship" },
    preferredLine: "support",
    note: {
      zh: "支援线守护炮台，部署补一张「打击」。",
      en: "A Guard battery in the support row; Deploy adds a Strike to hand.",
    },
    proposedActionCost: 1,
  },
  minion_peregrine_drone: {
    unitClass: "skirmisher",
    role: { zh: "侦察游击", en: "Recon skirmisher" },
    preferredLine: "flex",
    note: {
      zh: "闪击与亡语抽1，2费换牌兼骚扰。",
      en: "Charge and a Deathrattle draw for a cheap two-cost harassment play.",
    },
    proposedActionCost: 1,
  },
  minion_gyrfalcon_heavy_drone: {
    unitClass: "skirmisher",
    role: { zh: "重装猎手", en: "Heavy hunter" },
    preferredLine: "flex",
    note: {
      zh: "伏击、闪击与冲击齐备，落地即可无伤先手。",
      en: "Ambush, Charge and Impact together allow a safe first strike right after deployment.",
    },
    proposedActionCost: 1,
  },
  minion_type34_grenade_mg_team: {
    unitClass: "support",
    role: { zh: "跨线压制", en: "Cross-row suppression" },
    preferredLine: "support",
    note: {
      zh: "无额外效果的跨线火力组，2行动花费持续输出。",
      en: "A plain global-range fire team that pays 2 oil for steady damage.",
    },
    proposedActionCost: 2,
  },
  minion_55th_orbital_corps: {
    unitClass: "vanguard",
    role: { zh: "轨道突击", en: "Orbital shock" },
    preferredLine: "frontline",
    note: {
      zh: "闪击与守护，部署把一个单位直接推进前线。",
      en: "Charge and Guard; Deploy moves a unit straight into the frontline.",
    },
    proposedActionCost: 1,
  },
  minion_t90m_late: {
    unitClass: "assault",
    role: { zh: "重装守线", en: "Armored line holder" },
    preferredLine: "frontline",
    note: {
      zh: "守护重坦，部署补一张「爆炸反应装甲」。",
      en: "A Guard heavy tank; Deploy adds Explosive Reactive Armor to hand.",
    },
    proposedActionCost: 2,
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
