using System.Numerics;

namespace TempMod.Core;

public enum Faction : byte
{
    Crew = 0,
    Impostor = 1,
    Neutral = 2,
}

public enum RoleId : byte
{
    Crewmate = 0,
    Impostor = 1,
    Sheriff,
    Doctor,
    MadScientist,
    Tracker,
    TimeTraveler,
    Seer,
    BarrierNic,
    LightWorker,
    Investigator,
    Mayor,
    Ninja,
    Warlock,
    Mafia,
    Puppeteer,
    Eraser,
    Undertaker,
    Jester,
    Jackal,
    Vampire,
    Cleaner,
    MadGuesser,
    Morphing,
    Marionette,
    Bomber,
    Spy,
    Trapper,
    Blackout,
    Phantom,
    BountyHunter,
    VampireLord,
    Hacker,
    Illusionist,
    Silencer,
    Gluttony,
    TimeThief,
    Deceptor,
    Necromancer,
    Witch,
    Alchemist,
    God,
    SchrodingerCat,
    Zombie,
    Apathy,
    Advocate,
    Clown,
    Arsonist,
    Terrorist,
    Vulture,
    Collector,
    Guardian,
    Fanatic,
    Thief,
    GhostHunter,
    Bouncer,
    Spectator,
    Assassin,
    Sidekick,
    ChildZombie,
}

public enum ModifierId : byte
{
    Lovers = 1,
}

public enum AbilityId : byte
{
    Kill = 1,
    OpenVitals,
    Track,
    TimeWarp,
    SpeakWithDead,
    GrantBarrier,
    Curse,
    Sabotage,
    Puppet,
    EraseKill,
    CarryBody,
    DropBody,
    Bite,
    Clean,
    GuessRole,
    CollectDna,
    Morph,
    MarionetteKill,
    PlantBomb,
    Wiretap,
    SetTrap,
    Blackout,
    Phase,
    CheckBounty,
    ReviveMinion,
    Hack,
    CreateIllusion,
    Silence,
    Devour,
    StealTime,
    DeceiveVote,
    AnimateBody,
    LinkCurse,
    AlchemyStealth,
    Omniscience,
    RecruitSidekick,
    AlignFaction,
    InfectKill,
    AbandonTasks,
    Bribe,
    ConfusionGas,
    Douse,
    Ignite,
    SelfDestruct,
    CollectBody,
    StealItem,
    AbsoluteDefense,
    FanaticWorship,
    StealSkin,
    CaptureGhost,
    ForceEject,
    Spectate,
    Assassinate,
}

public enum GameEventKind : byte
{
    AbilityAccepted = 1,
    AbilityRejected,
    PlayerDied,
    BarrierConsumed,
    CurseApplied,
    CurseTriggered,
    BiteApplied,
    TimeWarped,
    TrackingStarted,
    VitalsOpened,
    BarrierGranted,
    BodyCarried,
    BodyDropped,
    RoleErased,
    LoversPaired,
    LoversTriggered,
    Victory,
    BodyCleaned,
    BombPlanted,
    BombExploded,
    DnaCollected,
    MorphStarted,
    TrapPlaced,
    TrapTriggered,
    BlackoutStarted,
    PhaseStarted,
    BountyAssigned,
    SilenceApplied,
    BodyDevoured,
    BodyAnimated,
    WitchLinked,
    BodyHidden,
    RoleChanged,
}

public enum VictoryKind : byte
{
    None = 0,
    Jester,
    Lovers,
    Jackal,
    Vampire,
    Impostors,
    Crewmates,
    God,
    Zombie,
    Apathy,
    Advocate,
    Clown,
    Arsonist,
    Terrorist,
    Vulture,
    Collector,
    Guardian,
    Fanatic,
    Thief,
    GhostHunter,
    Bouncer,
    Spectator,
    Assassin,
}

public readonly record struct Position(float X, float Y)
{
    public static readonly Position Zero = new(0, 0);

    public float DistanceTo(Position other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    public Vector2 ToVector2() => new(X, Y);
}

public sealed record RoleDefinition(
    RoleId Id,
    string DisplayName,
    Faction Faction,
    bool CanDirectKill,
    bool IsKillerNeutral = false,
    bool IsModifier = false);

public sealed class RoleOptions
{
    public int SheriffKillLimit { get; init; } = 1;
    public bool SheriffCanKillNeutrals { get; init; } = true;
    public float StandardKillCooldown { get; init; } = 25f;
    public float NinjaKillCooldown { get; init; } = 40f;
    /// <summary>SuperNewRolesのEvilGuesserShotsPerMeetingに対応する、会議ごとの推測回数。</summary>
    public int MadGuesserShotsPerMeeting { get; init; } = 1;
    public float MadScientistDuration { get; init; } = 5f;
    public float MadScientistCooldown { get; init; } = 45f;
    public float TrackerDuration { get; init; } = 10f;
    public float TrackerCooldown { get; init; } = 30f;
    public float TimeTravelerSeconds { get; init; } = 5f;
    public float TimeTravelerCooldown { get; init; } = 35f;
    public float SeerDuration { get; init; } = 8f;
    public float SeerCooldown { get; init; } = 40f;
    public float BarrierCooldown { get; init; } = 35f;
    public float WarlockDuration { get; init; } = 12f;
    public float WarlockCooldown { get; init; } = 30f;
    public float PuppeteerDuration { get; init; } = 5f;
    public float PuppeteerCooldown { get; init; } = 35f;
    public float VampireDelay { get; init; } = 10f;
    public float VampireCooldown { get; init; } = 30f;
    public float JackalKillCooldown { get; init; } = 30f;
    /// <summary>SuperNewRolesのJackalAbilityに合わせた、サイドキック作成専用クールダウン。</summary>
    public float JackalSidekickCooldown { get; init; } = 30f;
    /// <summary>親ジャッカル死亡時に、存命のサイドキックをジャッカルへ昇格させる。</summary>
    public bool JackalSidekickPromotesOnJackalDeath { get; init; } = true;
    public float SpecialAbilityCooldown { get; init; } = 30f;
    public float CleanerDuration { get; init; } = 3f;
    public float BombDelay { get; init; } = 10f;
    public float BombRadius { get; init; } = 2.5f;
    public float MorphDuration { get; init; } = 15f;
    public float BlackoutDuration { get; init; } = 8f;
    public float PhantomDuration { get; init; } = 8f;
    public float TrapDuration { get; init; } = 15f;
    public float SilenceDuration { get; init; } = 45f;
    public float AlchemyBodyStealthDuration { get; init; } = 12f;
    public float InvestigatorTrailLifetime { get; init; } = 6f;
    public float FootprintInterval { get; init; } = .5f;
    public float PositionHistoryInterval { get; init; } = .25f;
    public float KillDistance { get; init; } = 2.0f;
    public float CurseDistance { get; init; } = 1.2f;
    public float UndertakerSpeedMultiplier { get; init; } = .7f;
    public bool VampireTimerPausesDuringMeeting { get; init; } = true;
}

public sealed class PlayerState
{
    public byte PlayerId { get; init; }
    public string PlayerName { get; init; } = string.Empty;
    public RoleId PrimaryRole { get; set; } = RoleId.Crewmate;
    public HashSet<ModifierId> Modifiers { get; } = new();
    public bool IsAlive { get; set; } = true;
    public Position Position { get; set; } = Position.Zero;
    public bool HasBarrier { get; set; }
    public bool IsCursed { get; set; }
    public float CurseExpiresAt { get; set; }
    public float BiteExpiresAt { get; set; }
    public byte? PuppetControllerId { get; set; }
    public float PuppetExpiresAt { get; set; }
    public byte? CarriedBodyOwnerId { get; set; }
    public bool RoleErasedOnDeath { get; set; }
    public float NextKillAt { get; set; }
    public int SheriffKillsRemaining { get; set; }
    /// <summary>現在の会議で消費したマッドゲッサーの推測回数。</summary>
    public int MadGuesserShotsThisMeeting { get; set; }
    public Dictionary<AbilityId, float> AbilityCooldowns { get; } = new();
    public Dictionary<AbilityId, float> EffectExpiresAt { get; } = new();
    public Dictionary<AbilityId, byte> EffectTargets { get; } = new();
    public Dictionary<AbilityId, int> EffectCounts { get; } = new();
    public byte? SecondaryEffectTargetId { get; set; }
    public float ImmobilizedUntil { get; set; }
    public Queue<PositionSample> PositionHistory { get; } = new();

    public bool HasModifier(ModifierId modifier) => Modifiers.Contains(modifier);
}

public readonly record struct PositionSample(float Time, Position Position);
public readonly record struct BodyState(byte OwnerId, Position Position, float DiedAt, bool IsCarried, bool RoleErased, float InvisibleUntil = 0f);
public sealed record ReplicatedPlayerState(
    byte PlayerId,
    string PlayerName,
    RoleId PrimaryRole,
    bool IsAlive,
    Position Position,
    bool HasBarrier,
    bool IsCursed,
    float CurseExpiresAt,
    float BiteExpiresAt,
    byte? PuppetControllerId,
    float PuppetExpiresAt,
    byte? CarriedBodyOwnerId,
    bool RoleErasedOnDeath,
    int SheriffKillsRemaining,
    IReadOnlyDictionary<AbilityId, float> AbilityCooldowns)
{
    /// <summary>現在の会議で消費したマッドゲッサーの推測回数。</summary>
    public int MadGuesserShotsThisMeeting { get; init; }

    // 既存の初期化子と互換な拡張プロパティをこの位置に保持する。
    public IReadOnlyDictionary<AbilityId, float> EffectExpiresAt { get; init; } = new Dictionary<AbilityId, float>();
    public IReadOnlyDictionary<AbilityId, byte> EffectTargets { get; init; } = new Dictionary<AbilityId, byte>();
    public IReadOnlyDictionary<AbilityId, int> EffectCounts { get; init; } = new Dictionary<AbilityId, int>();
    public byte? SecondaryEffectTargetId { get; init; }
    public float ImmobilizedUntil { get; init; }
}
public readonly record struct Footprint(byte OwnerId, Position Position, float CreatedAt);

public sealed record AbilityRequest(
    byte SenderId,
    AbilityId Ability,
    byte? TargetId,
    Position? RequestedPosition,
    float SentAt);

public sealed record GameEvent(
    GameEventKind Kind,
    float Time,
    byte? ActorId = null,
    byte? TargetId = null,
    string? Detail = null,
    Position? Position = null,
    bool Silent = false,
    IReadOnlyList<byte>? ParticipantIds = null);

public sealed record VictoryResult(VictoryKind Kind, IReadOnlyList<byte> WinnerIds)
{
    public static readonly VictoryResult None = new(VictoryKind.None, Array.Empty<byte>());
}

public interface IRoleGameGateway
{
    bool IsWalkable(Position position);
    void Emit(GameEvent gameEvent);
}

public static class RoleCatalog
{
    private static readonly IReadOnlyDictionary<RoleId, RoleDefinition> Definitions =
        new Dictionary<RoleId, RoleDefinition>
        {
            [RoleId.Crewmate] = new(RoleId.Crewmate, "クルー", Faction.Crew, false),
            [RoleId.Impostor] = new(RoleId.Impostor, "インポスター", Faction.Impostor, true),
            [RoleId.Sheriff] = new(RoleId.Sheriff, "シェリフ", Faction.Crew, true),
            [RoleId.Doctor] = new(RoleId.Doctor, "ドクター", Faction.Crew, false),
            [RoleId.MadScientist] = new(RoleId.MadScientist, "マッドサイエンティスト", Faction.Crew, false),
            [RoleId.Tracker] = new(RoleId.Tracker, "トラッカー", Faction.Crew, false),
            [RoleId.TimeTraveler] = new(RoleId.TimeTraveler, "タイムトラベラー", Faction.Crew, false),
            [RoleId.Seer] = new(RoleId.Seer, "シーア", Faction.Crew, false),
            [RoleId.BarrierNic] = new(RoleId.BarrierNic, "バリアニック", Faction.Crew, false),
            [RoleId.LightWorker] = new(RoleId.LightWorker, "ライトワーカー", Faction.Crew, false),
            [RoleId.Investigator] = new(RoleId.Investigator, "インベスティゲーター", Faction.Crew, false),
            [RoleId.Mayor] = new(RoleId.Mayor, "市長", Faction.Crew, false),
            [RoleId.Ninja] = new(RoleId.Ninja, "ニンジャ", Faction.Impostor, true),
            [RoleId.Warlock] = new(RoleId.Warlock, "ウォーロック", Faction.Impostor, true),
            [RoleId.Mafia] = new(RoleId.Mafia, "マフィア", Faction.Impostor, false),
            [RoleId.Puppeteer] = new(RoleId.Puppeteer, "パペッティア", Faction.Impostor, true),
            [RoleId.Eraser] = new(RoleId.Eraser, "イレイザー", Faction.Impostor, true),
            [RoleId.Undertaker] = new(RoleId.Undertaker, "アンダーテイカー", Faction.Impostor, true),
            [RoleId.Jester] = new(RoleId.Jester, "ジェスター", Faction.Neutral, false),
            [RoleId.Jackal] = new(RoleId.Jackal, "ジャッカル", Faction.Neutral, true, IsKillerNeutral: true),
            [RoleId.Vampire] = new(RoleId.Vampire, "ヴァンパイア", Faction.Neutral, true, IsKillerNeutral: true),
            [RoleId.Cleaner] = new(RoleId.Cleaner, "クリーナー", Faction.Impostor, true),
            [RoleId.MadGuesser] = new(RoleId.MadGuesser, "マッドゲッサー", Faction.Impostor, true),
            [RoleId.Morphing] = new(RoleId.Morphing, "モーフィング", Faction.Impostor, true),
            [RoleId.Marionette] = new(RoleId.Marionette, "マリオネット", Faction.Impostor, true),
            [RoleId.Bomber] = new(RoleId.Bomber, "ボマー", Faction.Impostor, false),
            [RoleId.Spy] = new(RoleId.Spy, "スパイ", Faction.Impostor, true),
            [RoleId.Trapper] = new(RoleId.Trapper, "トラッパー", Faction.Impostor, true),
            [RoleId.Blackout] = new(RoleId.Blackout, "ブラックアウト", Faction.Impostor, true),
            [RoleId.Phantom] = new(RoleId.Phantom, "ファントム", Faction.Impostor, true),
            [RoleId.BountyHunter] = new(RoleId.BountyHunter, "バウンティハンター", Faction.Impostor, true),
            [RoleId.VampireLord] = new(RoleId.VampireLord, "ヴァンパイアロード", Faction.Impostor, true),
            [RoleId.Hacker] = new(RoleId.Hacker, "ハッカー", Faction.Impostor, true),
            [RoleId.Illusionist] = new(RoleId.Illusionist, "イリュージョニスト", Faction.Impostor, true),
            [RoleId.Silencer] = new(RoleId.Silencer, "サイレンサー", Faction.Impostor, true),
            [RoleId.Gluttony] = new(RoleId.Gluttony, "グラトニー", Faction.Impostor, true),
            [RoleId.TimeThief] = new(RoleId.TimeThief, "タイムシーフ", Faction.Impostor, true),
            [RoleId.Deceptor] = new(RoleId.Deceptor, "ディセプター", Faction.Impostor, true),
            [RoleId.Necromancer] = new(RoleId.Necromancer, "ネクロマンサー", Faction.Impostor, true),
            [RoleId.Witch] = new(RoleId.Witch, "ウィッチ", Faction.Impostor, true),
            [RoleId.Alchemist] = new(RoleId.Alchemist, "アルケミスト", Faction.Impostor, true),
            [RoleId.God] = new(RoleId.God, "神（ゴッド）", Faction.Neutral, false),
            [RoleId.SchrodingerCat] = new(RoleId.SchrodingerCat, "シュレディンガーの猫", Faction.Neutral, false),
            [RoleId.Zombie] = new(RoleId.Zombie, "ゾンビ", Faction.Neutral, false, IsKillerNeutral: true),
            [RoleId.Apathy] = new(RoleId.Apathy, "アパシー", Faction.Neutral, false),
            [RoleId.Advocate] = new(RoleId.Advocate, "アドボケイト", Faction.Neutral, false),
            [RoleId.Clown] = new(RoleId.Clown, "ピエロ", Faction.Neutral, false),
            [RoleId.Arsonist] = new(RoleId.Arsonist, "アルソニスト", Faction.Neutral, false),
            [RoleId.Terrorist] = new(RoleId.Terrorist, "テロリスト", Faction.Neutral, false),
            [RoleId.Vulture] = new(RoleId.Vulture, "ハゲタカ", Faction.Neutral, false),
            [RoleId.Collector] = new(RoleId.Collector, "コレクター", Faction.Neutral, false),
            [RoleId.Guardian] = new(RoleId.Guardian, "ガーディアン", Faction.Neutral, false),
            [RoleId.Fanatic] = new(RoleId.Fanatic, "ファナティック", Faction.Neutral, false),
            [RoleId.Thief] = new(RoleId.Thief, "シーフ", Faction.Neutral, true, IsKillerNeutral: true),
            [RoleId.GhostHunter] = new(RoleId.GhostHunter, "ゴーストハンター", Faction.Neutral, false),
            [RoleId.Bouncer] = new(RoleId.Bouncer, "バウンサー", Faction.Neutral, false),
            [RoleId.Spectator] = new(RoleId.Spectator, "スペクテイター", Faction.Neutral, false),
            [RoleId.Assassin] = new(RoleId.Assassin, "アサシン", Faction.Neutral, false, IsKillerNeutral: true),
            [RoleId.Sidekick] = new(RoleId.Sidekick, "サイドキック", Faction.Neutral, true, IsKillerNeutral: true),
            [RoleId.ChildZombie] = new(RoleId.ChildZombie, "子ゾンビ", Faction.Neutral, false),
        };

    public static RoleDefinition Get(RoleId role) => Definitions[role];
    public static Faction GetFaction(RoleId role) => Get(role).Faction;
    public static bool IsKillerNeutral(RoleId role) => Get(role).IsKillerNeutral;
}
