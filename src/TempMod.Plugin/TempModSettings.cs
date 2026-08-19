using BepInEx.Configuration;
using TempMod.Core;

namespace TempMod.Plugin;

/// <summary>
/// BepInEx設定ファイルの値を、ホスト用の役職抽選・能力設定へ変換する。
/// クライアントはホストが送る役職割当を受け取るため、試合の設定値はホスト側を正とする。
/// </summary>
internal sealed class TempModSettings
{
    private readonly ConfigFile _config;
    private readonly Dictionary<RoleId, ConfigEntry<bool>> _enabledRoles = new();
    private readonly Dictionary<RoleId, ConfigEntry<int>> _roleCounts = new();
    private readonly Dictionary<RoleId, ConfigEntry<int>> _roleChances = new();

    internal ConfigEntry<int> ImpostorCount { get; }
    internal ConfigEntry<int> CrewRoleCount { get; }
    internal ConfigEntry<int> NeutralRoleCount { get; }
    internal ConfigEntry<bool> EnableLovers { get; }
    internal ConfigEntry<int> SheriffKillLimit { get; }
    internal ConfigEntry<bool> SheriffCanKillNeutrals { get; }
    internal ConfigEntry<float> DoctorDeathTimeDisplaySeconds { get; }
    internal ConfigEntry<float> StandardKillCooldown { get; }
    internal ConfigEntry<float> NinjaKillCooldown { get; }
    internal ConfigEntry<int> MadGuesserShotsPerMeeting { get; }
    internal ConfigEntry<float> MadScientistDuration { get; }
    internal ConfigEntry<float> MadScientistCooldown { get; }
    internal ConfigEntry<float> TrackerDuration { get; }
    internal ConfigEntry<float> TrackerCooldown { get; }
    internal ConfigEntry<float> TimeTravelerSeconds { get; }
    internal ConfigEntry<float> TimeTravelerCooldown { get; }
    internal ConfigEntry<float> WarlockDuration { get; }
    internal ConfigEntry<float> WarlockCooldown { get; }
    internal ConfigEntry<float> PuppeteerDuration { get; }
    internal ConfigEntry<float> PuppeteerCooldown { get; }
    internal ConfigEntry<float> VampireDelay { get; }
    internal ConfigEntry<float> VampireCooldown { get; }
    internal ConfigEntry<float> JackalKillCooldown { get; }
    internal ConfigEntry<float> JackalSidekickCooldown { get; }
    internal ConfigEntry<bool> JackalSidekickPromotesOnDeath { get; }
    internal ConfigEntry<float> SpecialAbilityCooldown { get; }
    internal ConfigEntry<float> CleanerDuration { get; }
    internal ConfigEntry<float> BombDelay { get; }
    internal ConfigEntry<float> BombRadius { get; }
    internal ConfigEntry<float> MorphDuration { get; }
    internal ConfigEntry<float> BlackoutDuration { get; }
    internal ConfigEntry<float> PhantomDuration { get; }
    internal ConfigEntry<float> TrapDuration { get; }
    internal ConfigEntry<float> SilenceDuration { get; }
    internal ConfigEntry<float> AlchemyBodyStealthDuration { get; }
    internal ConfigEntry<RoleId> FreeplayPracticeRole { get; }
    internal ConfigEntry<RoleId> FreeplayDummyRole { get; }

    internal TempModSettings(ConfigFile config)
    {
        _config = config;
        ImpostorCount = config.Bind("陣営別人数", "インポスター人数", 2, "インポスター役職を配布する人数上限です。1～15。");
        CrewRoleCount = config.Bind("陣営別人数", "クルー人数", 10, "クルー役職を配布する人数上限です。0～15。");
        NeutralRoleCount = config.Bind("陣営別人数", "第三陣営人数", 1, "第三陣営役職を配布する人数上限です。0～15。");
        EnableLovers = config.Bind("第三陣営: ラバーズ", "ラバーズを有効化", true, "有効時、2人以上のゲームでランダムな2名をラバーズにします。");
        FreeplayPracticeRole = config.Bind("1人用フリープレイ検証", "自分の役職", RoleId.Undertaker, "フリープレイでローカルプレイヤーへ固定配布する役職です。オンライン・ローカルロビーには適用されません。");
        FreeplayDummyRole = config.Bind("1人用フリープレイ検証", "正規ダミーの役職", RoleId.Crewmate, "フリープレイでゲーム本体が管理する既存ダミーへ固定配布する役職です。ダミーの生成・複製は行いません。");

        SheriffKillLimit = config.Bind("能力: シェリフ", "キル回数上限", 1, "ゲーム中にシェリフが直接キルできる回数です。1～5回。");
        SheriffCanKillNeutrals = config.Bind("能力: シェリフ", "第三陣営をキル可能", true, "有効時、ジャッカル・ヴァンパイアなど第三陣営もキルできます。");
        DoctorDeathTimeDisplaySeconds = config.Bind("能力: ドクター", "死亡推定時刻の表示時間", 5f, "秒単位です。");
        StandardKillCooldown = config.Bind("能力: 共通", "標準キルクールダウン", 25f, "秒単位です。");
        NinjaKillCooldown = config.Bind("能力: ニンジャ", "キルクールダウン", 40f, "秒単位です。");
        MadGuesserShotsPerMeeting = config.Bind("能力: マッドゲッサー", "会議ごとの推測回数", 1, "会議中に推測できる最大回数です。1～5回。");
        MadScientistDuration = config.Bind("能力: マッドサイエンティスト", "バイタル表示時間", 5f, "秒単位です。");
        MadScientistCooldown = config.Bind("能力: マッドサイエンティスト", "クールダウン", 45f, "秒単位です。");
        TrackerDuration = config.Bind("能力: トラッカー", "追跡時間", 10f, "秒単位です。");
        TrackerCooldown = config.Bind("能力: トラッカー", "クールダウン", 30f, "秒単位です。");
        TimeTravelerSeconds = config.Bind("能力: タイムトラベラー", "巻戻し時間", 5f, "秒単位です。");
        TimeTravelerCooldown = config.Bind("能力: タイムトラベラー", "クールダウン", 35f, "秒単位です。");
        WarlockDuration = config.Bind("能力: ウォーロック", "呪い持続時間", 12f, "秒単位です。");
        WarlockCooldown = config.Bind("能力: ウォーロック", "クールダウン", 30f, "秒単位です。");
        PuppeteerDuration = config.Bind("能力: パペッティア", "操作時間", 5f, "秒単位です。");
        PuppeteerCooldown = config.Bind("能力: パペッティア", "クールダウン", 35f, "秒単位です。");
        VampireDelay = config.Bind("能力: ヴァンパイア", "噛みつき後の死亡遅延", 10f, "秒単位です。");
        VampireCooldown = config.Bind("能力: ヴァンパイア", "噛みつきクールダウン", 30f, "秒単位です。");
        JackalKillCooldown = config.Bind("能力: ジャッカル", "キルクールダウン", 30f, "秒単位です。");
        JackalSidekickCooldown = config.Bind("能力: ジャッカル", "サイドキック作成クールダウン", 30f, "秒単位です。ジャッカルのキルとは別に管理します。");
        JackalSidekickPromotesOnDeath = config.Bind("能力: ジャッカル", "親死亡時にサイドキックを昇格", true, "有効時、親ジャッカルが死亡するとそのサイドキックがジャッカルへ昇格します。");
        SpecialAbilityCooldown = config.Bind("能力: 追加役職共通", "基本クールダウン", 30f, "秒単位です。");
        CleanerDuration = config.Bind("能力: クリーナー", "清掃硬直時間", 3f, "秒単位です。");
        BombDelay = config.Bind("能力: ボマー", "爆発までの時間", 10f, "秒単位です。");
        BombRadius = config.Bind("能力: ボマー", "爆発範囲", 2.5f, "距離単位です。");
        MorphDuration = config.Bind("能力: モーフィング", "変身時間", 15f, "秒単位です。");
        BlackoutDuration = config.Bind("能力: ブラックアウト", "目隠し時間", 8f, "秒単位です。");
        PhantomDuration = config.Bind("能力: ファントム", "幽体化時間", 8f, "秒単位です。");
        TrapDuration = config.Bind("能力: トラッパー", "罠持続時間", 15f, "秒単位です。");
        SilenceDuration = config.Bind("能力: サイレンサー", "沈黙時間", 45f, "秒単位です。");
        AlchemyBodyStealthDuration = config.Bind("能力: アルケミスト", "死体透明時間", 12f, "秒単位です。");

        foreach (var role in SelectableRoles)
        {
            var definition = RoleCatalog.Get(role);
            var section = $"役職: {FactionLabel(definition.Faction)}";
            _enabledRoles[role] = config.Bind(section, $"{definition.DisplayName}を有効化", true, "ホストの役職抽選に含めます。");
            _roleCounts[role] = config.Bind(section, $"{definition.DisplayName}の人数", 1, "この役職の人数上限です。0～15。");
            _roleChances[role] = config.Bind(section, $"{definition.DisplayName}の出現率", 100, "この役職が候補になった際の出現率です。0～100%、10%刻み。");
        }
    }

    internal IReadOnlyList<LobbySettingRow> GetLobbyRows()
    {
        return SelectableRoles.Select(role =>
        {
            var definition = RoleCatalog.Get(role);
            return new LobbySettingRow(
                FactionLabel(definition.Faction),
                definition.DisplayName,
                _enabledRoles[role].Value,
                _roleCounts[role].Value,
                _roleChances[role].Value,
                role);
        }).ToArray();
    }

    internal RoleDetailRow[] GetRoleDetails(RoleId role) => role switch
    {
        RoleId.Sheriff => new[]
        {
            Detail(DetailSettingKey.SheriffKillLimit, "キル回数", $"{SheriffKillLimit.Value} 回"),
            Detail(DetailSettingKey.SheriffCanKillNeutrals, "第三陣営をキル", SheriffCanKillNeutrals.Value ? "可能" : "不可", isToggle: true),
        },
        RoleId.Doctor => new[] { Detail(DetailSettingKey.DoctorDisplaySeconds, "死亡推定時刻の表示", Seconds(DoctorDeathTimeDisplaySeconds.Value)) },
        RoleId.MadScientist => new[]
        {
            Detail(DetailSettingKey.MadScientistDuration, "バイタル表示時間", Seconds(MadScientistDuration.Value)),
            Detail(DetailSettingKey.MadScientistCooldown, "クールダウン", Seconds(MadScientistCooldown.Value)),
        },
        RoleId.Tracker => new[]
        {
            Detail(DetailSettingKey.TrackerDuration, "追跡時間", Seconds(TrackerDuration.Value)),
            Detail(DetailSettingKey.TrackerCooldown, "クールダウン", Seconds(TrackerCooldown.Value)),
        },
        RoleId.TimeTraveler => new[]
        {
            Detail(DetailSettingKey.TimeTravelerSeconds, "巻戻し時間", Seconds(TimeTravelerSeconds.Value)),
            Detail(DetailSettingKey.TimeTravelerCooldown, "クールダウン", Seconds(TimeTravelerCooldown.Value)),
        },
        RoleId.Ninja => new[] { Detail(DetailSettingKey.NinjaKillCooldown, "キルクールダウン", Seconds(NinjaKillCooldown.Value)) },
        RoleId.MadGuesser => new[] { Detail(DetailSettingKey.MadGuesserShotsPerMeeting, "会議ごとの推測回数", $"{MadGuesserShotsPerMeeting.Value} 回") },
        RoleId.Warlock => new[]
        {
            Detail(DetailSettingKey.WarlockDuration, "呪い持続時間", Seconds(WarlockDuration.Value)),
            Detail(DetailSettingKey.WarlockCooldown, "クールダウン", Seconds(WarlockCooldown.Value)),
        },
        RoleId.Puppeteer => new[]
        {
            Detail(DetailSettingKey.PuppeteerDuration, "操作時間", Seconds(PuppeteerDuration.Value)),
            Detail(DetailSettingKey.PuppeteerCooldown, "クールダウン", Seconds(PuppeteerCooldown.Value)),
        },
        RoleId.Vampire => new[]
        {
            Detail(DetailSettingKey.VampireDelay, "死亡までの時間", Seconds(VampireDelay.Value)),
            Detail(DetailSettingKey.VampireCooldown, "噛みつきクールダウン", Seconds(VampireCooldown.Value)),
        },
        RoleId.Jackal => new[]
        {
            Detail(DetailSettingKey.JackalKillCooldown, "キルクールダウン", Seconds(JackalKillCooldown.Value)),
            Detail(DetailSettingKey.JackalSidekickCooldown, "サイドキック作成クールダウン", Seconds(JackalSidekickCooldown.Value)),
            Detail(DetailSettingKey.JackalSidekickPromotesOnDeath, "親死亡時にサイドキックを昇格", JackalSidekickPromotesOnDeath.Value ? "有効" : "無効", isToggle: true),
        },
        RoleId.Cleaner => new[] { Detail(DetailSettingKey.CleanerDuration, "清掃硬直時間", Seconds(CleanerDuration.Value)) },
        RoleId.Bomber => new[]
        {
            Detail(DetailSettingKey.BombDelay, "爆発までの時間", Seconds(BombDelay.Value)),
            Detail(DetailSettingKey.BombRadius, "爆発範囲", $"{BombRadius.Value:0.0}"),
        },
        RoleId.Morphing => new[] { Detail(DetailSettingKey.MorphDuration, "変身時間", Seconds(MorphDuration.Value)) },
        RoleId.Blackout => new[] { Detail(DetailSettingKey.BlackoutDuration, "目隠し時間", Seconds(BlackoutDuration.Value)) },
        RoleId.Phantom or RoleId.Spectator => new[] { Detail(DetailSettingKey.PhantomDuration, "効果時間", Seconds(PhantomDuration.Value)) },
        RoleId.Trapper => new[] { Detail(DetailSettingKey.TrapDuration, "罠持続時間", Seconds(TrapDuration.Value)) },
        RoleId.Silencer => new[] { Detail(DetailSettingKey.SilenceDuration, "沈黙時間", Seconds(SilenceDuration.Value)) },
        RoleId.Alchemist => new[] { Detail(DetailSettingKey.AlchemyBodyStealthDuration, "死体透明時間", Seconds(AlchemyBodyStealthDuration.Value)) },
        _ => Array.Empty<RoleDetailRow>(),
    };

    internal void SetRoleEnabled(RoleId role, bool enabled)
    {
        _enabledRoles[role].Value = enabled;
        _config.Save();
    }

    internal void ToggleRole(RoleId role)
    {
        SetRoleEnabled(role, !_enabledRoles[role].Value);
    }

    internal void AdjustRoleCount(RoleId role, int delta)
    {
        _roleCounts[role].Value = Math.Clamp(_roleCounts[role].Value + delta, 0, 15);
        _config.Save();
    }

    internal int GetFactionCount(Faction faction) => faction switch
    {
        Faction.Crew => CrewRoleCount.Value,
        Faction.Impostor => ImpostorCount.Value,
        _ => NeutralRoleCount.Value,
    };

    internal void AdjustFactionCount(Faction faction, int delta)
    {
        switch (faction)
        {
            case Faction.Crew:
                CrewRoleCount.Value = Math.Clamp(CrewRoleCount.Value + delta, 0, 15);
                break;
            case Faction.Impostor:
                ImpostorCount.Value = Math.Clamp(ImpostorCount.Value + delta, 1, 15);
                break;
            default:
                NeutralRoleCount.Value = Math.Clamp(NeutralRoleCount.Value + delta, 0, 15);
                break;
        }
        _config.Save();
    }

    internal void AdjustRoleChance(RoleId role, int deltaSteps)
    {
        _roleChances[role].Value = Math.Clamp(_roleChances[role].Value + deltaSteps * 10, 0, 100);
        _config.Save();
    }

    internal void AdjustDetail(DetailSettingKey key, int delta)
    {
        switch (key)
        {
            case DetailSettingKey.SheriffKillLimit: SheriffKillLimit.Value = Math.Clamp(SheriffKillLimit.Value + delta, 1, 5); break;
            case DetailSettingKey.DoctorDisplaySeconds: DoctorDeathTimeDisplaySeconds.Value = AdjustSeconds(DoctorDeathTimeDisplaySeconds, delta); break;
            case DetailSettingKey.MadScientistDuration: MadScientistDuration.Value = AdjustSeconds(MadScientistDuration, delta); break;
            case DetailSettingKey.MadScientistCooldown: MadScientistCooldown.Value = AdjustSeconds(MadScientistCooldown, delta); break;
            case DetailSettingKey.TrackerDuration: TrackerDuration.Value = AdjustSeconds(TrackerDuration, delta); break;
            case DetailSettingKey.TrackerCooldown: TrackerCooldown.Value = AdjustSeconds(TrackerCooldown, delta); break;
            case DetailSettingKey.TimeTravelerSeconds: TimeTravelerSeconds.Value = AdjustSeconds(TimeTravelerSeconds, delta); break;
            case DetailSettingKey.TimeTravelerCooldown: TimeTravelerCooldown.Value = AdjustSeconds(TimeTravelerCooldown, delta); break;
            case DetailSettingKey.NinjaKillCooldown: NinjaKillCooldown.Value = AdjustSeconds(NinjaKillCooldown, delta); break;
            case DetailSettingKey.MadGuesserShotsPerMeeting: MadGuesserShotsPerMeeting.Value = Math.Clamp(MadGuesserShotsPerMeeting.Value + delta, 1, 5); break;
            case DetailSettingKey.WarlockDuration: WarlockDuration.Value = AdjustSeconds(WarlockDuration, delta); break;
            case DetailSettingKey.WarlockCooldown: WarlockCooldown.Value = AdjustSeconds(WarlockCooldown, delta); break;
            case DetailSettingKey.PuppeteerDuration: PuppeteerDuration.Value = AdjustSeconds(PuppeteerDuration, delta); break;
            case DetailSettingKey.PuppeteerCooldown: PuppeteerCooldown.Value = AdjustSeconds(PuppeteerCooldown, delta); break;
            case DetailSettingKey.VampireDelay: VampireDelay.Value = AdjustSeconds(VampireDelay, delta); break;
            case DetailSettingKey.VampireCooldown: VampireCooldown.Value = AdjustSeconds(VampireCooldown, delta); break;
            case DetailSettingKey.JackalKillCooldown: JackalKillCooldown.Value = AdjustSeconds(JackalKillCooldown, delta); break;
            case DetailSettingKey.JackalSidekickCooldown: JackalSidekickCooldown.Value = AdjustSeconds(JackalSidekickCooldown, delta); break;
            case DetailSettingKey.JackalSidekickPromotesOnDeath: JackalSidekickPromotesOnDeath.Value = !JackalSidekickPromotesOnDeath.Value; break;
            case DetailSettingKey.CleanerDuration: CleanerDuration.Value = AdjustSeconds(CleanerDuration, delta); break;
            case DetailSettingKey.BombDelay: BombDelay.Value = AdjustSeconds(BombDelay, delta); break;
            case DetailSettingKey.BombRadius: BombRadius.Value = Math.Clamp(BombRadius.Value + delta * .5f, .5f, 10f); break;
            case DetailSettingKey.MorphDuration: MorphDuration.Value = AdjustSeconds(MorphDuration, delta); break;
            case DetailSettingKey.BlackoutDuration: BlackoutDuration.Value = AdjustSeconds(BlackoutDuration, delta); break;
            case DetailSettingKey.PhantomDuration: PhantomDuration.Value = AdjustSeconds(PhantomDuration, delta); break;
            case DetailSettingKey.TrapDuration: TrapDuration.Value = AdjustSeconds(TrapDuration, delta); break;
            case DetailSettingKey.SilenceDuration: SilenceDuration.Value = AdjustSeconds(SilenceDuration, delta); break;
            case DetailSettingKey.AlchemyBodyStealthDuration: AlchemyBodyStealthDuration.Value = AdjustSeconds(AlchemyBodyStealthDuration, delta); break;
            case DetailSettingKey.SheriffCanKillNeutrals:
                SheriffCanKillNeutrals.Value = !SheriffCanKillNeutrals.Value;
                break;
        }
        _config.Save();
    }

    internal void ToggleLovers()
    {
        EnableLovers.Value = !EnableLovers.Value;
        _config.Save();
    }

    internal void AdjustFreeplayPracticeRole(int delta)
    {
        FreeplayPracticeRole.Value = CycleRole(FreeplayPracticeRole.Value, SelectableRoles, delta);
        _config.Save();
    }

    internal void AdjustFreeplayDummyRole(int delta)
    {
        FreeplayDummyRole.Value = CycleRole(FreeplayDummyRole.Value, FreeplayDummyRoles, delta);
        _config.Save();
    }

    private static RoleId CycleRole(RoleId current, IReadOnlyList<RoleId> roles, int delta)
    {
        var index = Array.IndexOf(roles.ToArray(), current);
        if (index < 0)
            index = 0;
        return roles[(index + delta % roles.Count + roles.Count) % roles.Count];
    }

    internal RoleAssignmentOptions CreateAssignmentOptions()
    {
        return new RoleAssignmentOptions
        {
            ImpostorCount = Math.Clamp(ImpostorCount.Value, 1, 15),
            CrewRoleCount = Math.Clamp(CrewRoleCount.Value, 0, 15),
            NeutralRoleCount = Math.Clamp(NeutralRoleCount.Value, 0, 15),
            EnableLovers = EnableLovers.Value,
            Roles = SelectableRoles.Select(role => new RoleSpawnOption(
                role,
                _enabledRoles[role].Value,
                Math.Clamp(_roleCounts[role].Value, 0, 15),
                Math.Clamp(_roleChances[role].Value, 0, 100))).ToArray(),
        };
    }

    internal RoleOptions CreateRoleOptions()
    {
        return new RoleOptions
        {
            SheriffKillLimit = Math.Clamp(SheriffKillLimit.Value, 1, 5),
            SheriffCanKillNeutrals = SheriffCanKillNeutrals.Value,
            StandardKillCooldown = Math.Max(0, StandardKillCooldown.Value),
            NinjaKillCooldown = Math.Max(0, NinjaKillCooldown.Value),
            MadGuesserShotsPerMeeting = Math.Clamp(MadGuesserShotsPerMeeting.Value, 1, 5),
            MadScientistDuration = Math.Max(0.5f, MadScientistDuration.Value),
            MadScientistCooldown = Math.Max(0, MadScientistCooldown.Value),
            TrackerDuration = Math.Max(0.5f, TrackerDuration.Value),
            TrackerCooldown = Math.Max(0, TrackerCooldown.Value),
            TimeTravelerSeconds = Math.Max(0.5f, TimeTravelerSeconds.Value),
            TimeTravelerCooldown = Math.Max(0, TimeTravelerCooldown.Value),
            WarlockDuration = Math.Max(0.5f, WarlockDuration.Value),
            WarlockCooldown = Math.Max(0, WarlockCooldown.Value),
            PuppeteerDuration = Math.Max(0.5f, PuppeteerDuration.Value),
            PuppeteerCooldown = Math.Max(0, PuppeteerCooldown.Value),
            VampireDelay = Math.Max(0.5f, VampireDelay.Value),
            VampireCooldown = Math.Max(0, VampireCooldown.Value),
            JackalKillCooldown = Math.Max(0, JackalKillCooldown.Value),
            JackalSidekickCooldown = Math.Max(0, JackalSidekickCooldown.Value),
            JackalSidekickPromotesOnJackalDeath = JackalSidekickPromotesOnDeath.Value,
            SpecialAbilityCooldown = Math.Max(0, SpecialAbilityCooldown.Value),
            CleanerDuration = Math.Max(0.5f, CleanerDuration.Value),
            BombDelay = Math.Max(0.5f, BombDelay.Value),
            BombRadius = Math.Max(0.5f, BombRadius.Value),
            MorphDuration = Math.Max(0.5f, MorphDuration.Value),
            BlackoutDuration = Math.Max(0.5f, BlackoutDuration.Value),
            PhantomDuration = Math.Max(0.5f, PhantomDuration.Value),
            TrapDuration = Math.Max(0.5f, TrapDuration.Value),
            SilenceDuration = Math.Max(0.5f, SilenceDuration.Value),
            AlchemyBodyStealthDuration = Math.Max(0.5f, AlchemyBodyStealthDuration.Value),
        };
    }

    private static RoleDetailRow Detail(DetailSettingKey key, string label, string value, bool isToggle = false) =>
        new(key, label, value, isToggle);

    private static float AdjustSeconds(ConfigEntry<float> entry, int delta) => Math.Clamp(entry.Value + delta, 1f, 120f);
    private static string Seconds(float value) => $"{value:0} 秒";

    private static string FactionLabel(Faction faction) => faction switch
    {
        Faction.Crew => "クルー",
        Faction.Impostor => "インポスター",
        _ => "第三陣営",
    };

    internal sealed record LobbySettingRow(
        string Category,
        string Label,
        bool Enabled,
        int Count,
        int ChancePercent,
        RoleId Role);

    internal sealed record RoleDetailRow(DetailSettingKey Key, string Label, string Value, bool IsToggle);

    internal enum DetailSettingKey
    {
        SheriffKillLimit,
        SheriffCanKillNeutrals,
        DoctorDisplaySeconds,
        MadScientistDuration,
        MadScientistCooldown,
        TrackerDuration,
        TrackerCooldown,
        TimeTravelerSeconds,
        TimeTravelerCooldown,
        NinjaKillCooldown,
        MadGuesserShotsPerMeeting,
        WarlockDuration,
        WarlockCooldown,
        PuppeteerDuration,
        PuppeteerCooldown,
        VampireDelay,
        VampireCooldown,
        JackalKillCooldown,
        JackalSidekickCooldown,
        JackalSidekickPromotesOnDeath,
        CleanerDuration,
        BombDelay,
        BombRadius,
        MorphDuration,
        BlackoutDuration,
        PhantomDuration,
        TrapDuration,
        SilenceDuration,
        AlchemyBodyStealthDuration,
    }

    internal static readonly RoleId[] FreeplayDummyRoles =
    {
        RoleId.Crewmate, RoleId.Impostor,
        RoleId.Sheriff, RoleId.Doctor, RoleId.MadScientist, RoleId.Tracker,
        RoleId.TimeTraveler, RoleId.Seer, RoleId.BarrierNic, RoleId.LightWorker,
        RoleId.Investigator, RoleId.Mayor, RoleId.Ninja, RoleId.Warlock,
        RoleId.Mafia, RoleId.Puppeteer, RoleId.Eraser, RoleId.Undertaker,
        RoleId.Cleaner, RoleId.MadGuesser, RoleId.Morphing, RoleId.Marionette,
        RoleId.Bomber, RoleId.Spy, RoleId.Trapper, RoleId.Blackout,
        RoleId.Phantom, RoleId.BountyHunter, RoleId.VampireLord, RoleId.Hacker,
        RoleId.Illusionist, RoleId.Silencer, RoleId.Gluttony, RoleId.TimeThief,
        RoleId.Deceptor, RoleId.Necromancer, RoleId.Witch, RoleId.Alchemist,
        RoleId.Jester, RoleId.Jackal, RoleId.Vampire, RoleId.God,
        RoleId.SchrodingerCat, RoleId.Zombie, RoleId.Apathy, RoleId.Advocate,
        RoleId.Clown, RoleId.Arsonist, RoleId.Terrorist, RoleId.Vulture,
        RoleId.Collector, RoleId.Guardian, RoleId.Fanatic, RoleId.Thief,
        RoleId.GhostHunter, RoleId.Bouncer, RoleId.Spectator, RoleId.Assassin,
    };

    internal static readonly RoleId[] SelectableRoles =
    {
        RoleId.Sheriff, RoleId.Doctor, RoleId.MadScientist, RoleId.Tracker,
        RoleId.TimeTraveler, RoleId.Seer, RoleId.BarrierNic, RoleId.LightWorker,
        RoleId.Investigator, RoleId.Mayor, RoleId.Ninja, RoleId.Warlock,
        RoleId.Mafia, RoleId.Puppeteer, RoleId.Eraser, RoleId.Undertaker,
        RoleId.Cleaner, RoleId.MadGuesser, RoleId.Morphing, RoleId.Marionette,
        RoleId.Bomber, RoleId.Spy, RoleId.Trapper, RoleId.Blackout,
        RoleId.Phantom, RoleId.BountyHunter, RoleId.VampireLord, RoleId.Hacker,
        RoleId.Illusionist, RoleId.Silencer, RoleId.Gluttony, RoleId.TimeThief,
        RoleId.Deceptor, RoleId.Necromancer, RoleId.Witch, RoleId.Alchemist,
        RoleId.Jester, RoleId.Jackal, RoleId.Vampire, RoleId.God,
        RoleId.SchrodingerCat, RoleId.Zombie, RoleId.Apathy, RoleId.Advocate,
        RoleId.Clown, RoleId.Arsonist, RoleId.Terrorist, RoleId.Vulture,
        RoleId.Collector, RoleId.Guardian, RoleId.Fanatic, RoleId.Thief,
        RoleId.GhostHunter, RoleId.Bouncer, RoleId.Spectator, RoleId.Assassin,
    };
}
