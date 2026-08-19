using TempMod.Core;

var tests = new (string Name, Action Run)[]
{
    ("シェリフは敵をキルできる", SheriffKillsEnemy),
    ("シェリフはクルーを誤射すると自爆する", SheriffMisfire),
    ("バリアは一回だけキルを防ぐ", BarrierBlocksKill),
    ("ヴァンパイアは時間差で死亡させる", VampireBite),
    ("ラバーズは後追いする", LoversDieTogether),
    ("市長は二票を持つ", MayorHasDoubleVote),
    ("シェリフのキル回数上限を守る", SheriffKillLimit),
    ("シェリフは設定で第三陣営をキルできない", SheriffCannotKillNeutralWhenDisabled),
    ("1人でも役職抽選できる", SinglePlayerAssignment),
    ("陣営別人数上限を守る", FactionRoleLimitsAreRespected),
    ("クリーナーは死体を清掃できる", CleanerRemovesBody),
    ("ボマーは時限爆弾で周囲を巻き込む", BomberExplodesNearbyPlayers),
    ("ジャッカルはクルーをサイドキックに勧誘できる", JackalRecruitsSidekick),
    ("ゾンビはクルーを子ゾンビに感染できる", ZombieInfectsCrew),
    ("ハゲタカの死体回収数は時間経過後も残る", VultureCollectionCountPersists),
    ("マッドゲッサーは会議中の正解で対象をキルできる", MadGuesserKillsCorrectRole),
    ("マッドゲッサーは会議中の誤答で自爆する", MadGuesserDiesOnWrongGuess),
    ("アドボケイトの買収は自分を二票、対象を零票にする", AdvocateBribeChangesVoteWeight),
    ("アルソニストは全員への注油後に点火で勝利する", ArsonistIgnitesAllDousedPlayers),
};

var failed = 0;
foreach (var (name, test) in tests)
{
    try
    {
        test();
        Console.WriteLine($"PASS: {name}");
    }
    catch (Exception exception)
    {
        failed++;
        Console.Error.WriteLine($"FAIL: {name} — {exception.Message}");
    }
}

return failed == 0 ? 0 : 1;

static (RoleEngine Engine, TestGateway Gateway) CreateEngine()
{
    var gateway = new TestGateway();
    var engine = new RoleEngine(gateway, new RoleOptions
    {
        StandardKillCooldown = 0,
        VampireCooldown = 0,
        VampireDelay = 2,
        KillDistance = 3,
        SpecialAbilityCooldown = 0,
        CleanerDuration = .1f,
        BombDelay = .1f,
        BombRadius = 3f,
    });
    for (byte playerId = 1; playerId <= 4; playerId++)
    {
        engine.RegisterPlayer(playerId, $"P{playerId}");
        engine.UpdatePosition(playerId, new Position(playerId, 0), 1);
    }
    return (engine, gateway);
}

static void SheriffKillsEnemy()
{
    var (engine, _) = CreateEngine();
    engine.AssignRole(1, RoleId.Sheriff);
    engine.AssignRole(2, RoleId.Impostor);
    Assert(engine.TryHandleAbility(new AbilityRequest(1, AbilityId.Kill, 2, null, 2), 2));
    Assert(!engine.Players[2].IsAlive);
    Assert(engine.Players[1].IsAlive);
}

static void SheriffMisfire()
{
    var (engine, _) = CreateEngine();
    engine.AssignRole(1, RoleId.Sheriff);
    engine.AssignRole(2, RoleId.Doctor);
    Assert(engine.TryHandleAbility(new AbilityRequest(1, AbilityId.Kill, 2, null, 2), 2));
    Assert(!engine.Players[1].IsAlive);
    Assert(engine.Players[2].IsAlive);
}

static void BarrierBlocksKill()
{
    var (engine, _) = CreateEngine();
    engine.AssignRole(1, RoleId.Impostor);
    engine.AssignRole(2, RoleId.BarrierNic);
    engine.Players[2].HasBarrier = true;
    Assert(engine.TryHandleAbility(new AbilityRequest(1, AbilityId.Kill, 2, null, 2), 2));
    Assert(engine.Players[2].IsAlive);
    Assert(!engine.Players[2].HasBarrier);
}

static void VampireBite()
{
    var (engine, _) = CreateEngine();
    engine.AssignRole(1, RoleId.Vampire);
    engine.AssignRole(2, RoleId.Crewmate);
    Assert(engine.TryHandleAbility(new AbilityRequest(1, AbilityId.Bite, 2, null, 2), 2));
    Assert(engine.Players[2].IsAlive);
    engine.Tick(4.1f);
    Assert(!engine.Players[2].IsAlive);
}

static void LoversDieTogether()
{
    var (engine, _) = CreateEngine();
    engine.AssignRole(1, RoleId.Impostor);
    engine.AssignRole(2, RoleId.Crewmate);
    engine.AssignRole(3, RoleId.Crewmate);
    engine.PairLovers(2, 3, 1);
    Assert(engine.TryHandleAbility(new AbilityRequest(1, AbilityId.Kill, 2, null, 2), 2));
    Assert(!engine.Players[2].IsAlive);
    Assert(!engine.Players[3].IsAlive);
}

static void MayorHasDoubleVote()
{
    var (engine, _) = CreateEngine();
    engine.AssignRole(1, RoleId.Mayor);
    Assert(engine.GetVoteWeight(1) == 2);
    Assert(engine.GetVoteWeight(2) == 1);
}

static void SheriffKillLimit()
{
    var gateway = new TestGateway();
    var engine = new RoleEngine(gateway, new RoleOptions { StandardKillCooldown = 0, SheriffKillLimit = 2, KillDistance = 3 });
    for (byte playerId = 1; playerId <= 4; playerId++)
    {
        engine.RegisterPlayer(playerId, $"P{playerId}");
        engine.UpdatePosition(playerId, new Position(playerId, 0), 1);
    }
    engine.AssignRole(1, RoleId.Sheriff);
    engine.AssignRole(2, RoleId.Impostor);
    engine.AssignRole(3, RoleId.Impostor);
    engine.AssignRole(4, RoleId.Impostor);
    Assert(engine.TryHandleAbility(new AbilityRequest(1, AbilityId.Kill, 2, null, 2), 2));
    Assert(engine.TryHandleAbility(new AbilityRequest(1, AbilityId.Kill, 3, null, 3), 3));
    Assert(!engine.TryHandleAbility(new AbilityRequest(1, AbilityId.Kill, 4, null, 4), 4));
}

static void SheriffCannotKillNeutralWhenDisabled()
{
    var gateway = new TestGateway();
    var engine = new RoleEngine(gateway, new RoleOptions { StandardKillCooldown = 0, SheriffKillLimit = 1, SheriffCanKillNeutrals = false, KillDistance = 3 });
    engine.RegisterPlayer(1, "Sheriff");
    engine.RegisterPlayer(2, "Neutral");
    engine.UpdatePosition(1, new Position(1, 0), 1);
    engine.UpdatePosition(2, new Position(2, 0), 1);
    engine.AssignRole(1, RoleId.Sheriff);
    engine.AssignRole(2, RoleId.Jackal);
    Assert(engine.TryHandleAbility(new AbilityRequest(1, AbilityId.Kill, 2, null, 2), 2));
    Assert(!engine.Players[1].IsAlive);
    Assert(engine.Players[2].IsAlive);
}

static void SinglePlayerAssignment()
{
    var assignment = RoleAssignmentPlanner.Create(new byte[] { 1 }, new RoleAssignmentOptions
    {
        ImpostorCount = 1,
        EnableLovers = true,
    }, new Random(1));
    Assert(assignment.PrimaryRoles.Count == 1);
    Assert(assignment.PrimaryRoles.ContainsKey(1));
    Assert(assignment.PrimaryRoles[1] != RoleId.Impostor);
    Assert(assignment.LoversPairs.Count == 0);
}

    static void FactionRoleLimitsAreRespected()
    {
        var assignment = RoleAssignmentPlanner.Create(new byte[] { 1, 2, 3, 4, 5, 6 }, new RoleAssignmentOptions
        {
            ImpostorCount = 1,
            CrewRoleCount = 1,
            NeutralRoleCount = 1,
            EnableLovers = false,
        }, new Random(42));

        var crewCustomCount = assignment.PrimaryRoles.Values.Count(role => RoleCatalog.GetFaction(role) == Faction.Crew && role != RoleId.Crewmate);
        var impostorCustomCount = assignment.PrimaryRoles.Values.Count(role => RoleCatalog.GetFaction(role) == Faction.Impostor && role != RoleId.Impostor);
        var neutralCount = assignment.PrimaryRoles.Values.Count(role => RoleCatalog.GetFaction(role) == Faction.Neutral);
        Assert(crewCustomCount <= 1);
        Assert(impostorCustomCount <= 1);
        Assert(neutralCount <= 1);
    }

static void CleanerRemovesBody()
{
    var (engine, _) = CreateEngine();
    engine.AssignRole(1, RoleId.Cleaner);
    engine.AssignRole(2, RoleId.Crewmate);
    engine.AssignRole(3, RoleId.Impostor);
    Assert(engine.TryHandleAbility(new AbilityRequest(3, AbilityId.Kill, 2, null, 2), 2));
    Assert(engine.Bodies.ContainsKey(2));
    Assert(engine.TryHandleAbility(new AbilityRequest(1, AbilityId.Clean, 2, null, 3), 3));
    engine.Tick(3.2f);
    Assert(!engine.Bodies.ContainsKey(2));
}

static void BomberExplodesNearbyPlayers()
{
    var (engine, _) = CreateEngine();
    engine.AssignRole(1, RoleId.Bomber);
    engine.AssignRole(2, RoleId.Crewmate);
    engine.AssignRole(3, RoleId.Crewmate);
    Assert(engine.TryHandleAbility(new AbilityRequest(1, AbilityId.PlantBomb, 2, null, 2), 2));
    engine.Tick(2.2f);
    Assert(!engine.Players[2].IsAlive);
    Assert(!engine.Players[3].IsAlive);
}

static void JackalRecruitsSidekick()
{
    var (engine, _) = CreateEngine();
    engine.AssignRole(1, RoleId.Jackal);
    engine.AssignRole(2, RoleId.Crewmate);
    Assert(engine.TryHandleAbility(new AbilityRequest(1, AbilityId.RecruitSidekick, 2, null, 2), 2));
    Assert(engine.Players[2].PrimaryRole == RoleId.Sidekick);
}

static void ZombieInfectsCrew()
{
    var (engine, _) = CreateEngine();
    engine.AssignRole(1, RoleId.Zombie);
    engine.AssignRole(2, RoleId.Crewmate);
    Assert(engine.TryHandleAbility(new AbilityRequest(1, AbilityId.InfectKill, 2, null, 2), 2));
    Assert(engine.Players[2].PrimaryRole == RoleId.ChildZombie);
}

static void VultureCollectionCountPersists()
{
    var (engine, _) = CreateEngine();
    engine.AssignRole(1, RoleId.Vulture);
    engine.AssignRole(2, RoleId.Crewmate);
    engine.AssignRole(3, RoleId.Impostor);
    Assert(engine.TryHandleAbility(new AbilityRequest(3, AbilityId.Kill, 2, null, 2), 2));
    Assert(engine.TryHandleAbility(new AbilityRequest(1, AbilityId.CollectBody, 2, null, 3), 3));
    engine.Tick(30);
    Assert(engine.Players[1].EffectCounts.TryGetValue(AbilityId.CollectBody, out var collected) && collected == 1);
}

static void MadGuesserKillsCorrectRole()
{
    var (engine, _) = CreateEngine();
    engine.AssignRole(1, RoleId.MadGuesser);
    engine.AssignRole(2, RoleId.Sheriff);
    engine.StartMeeting(2);
    Assert(engine.TryHandleAbility(new AbilityRequest(1, AbilityId.GuessRole, 2, new Position((float)RoleId.Sheriff, 0), 3), 3));
    Assert(!engine.Players[2].IsAlive);
    Assert(engine.Players[1].IsAlive);
}

static void MadGuesserDiesOnWrongGuess()
{
    var (engine, _) = CreateEngine();
    engine.AssignRole(1, RoleId.MadGuesser);
    engine.AssignRole(2, RoleId.Sheriff);
    engine.StartMeeting(2);
    Assert(engine.TryHandleAbility(new AbilityRequest(1, AbilityId.GuessRole, 2, new Position((float)RoleId.Doctor, 0), 3), 3));
    Assert(!engine.Players[1].IsAlive);
    Assert(engine.Players[2].IsAlive);
}

static void AdvocateBribeChangesVoteWeight()
{
    var (engine, _) = CreateEngine();
    engine.AssignRole(1, RoleId.Advocate);
    engine.AssignRole(2, RoleId.Crewmate);
    engine.StartMeeting(2);
    Assert(engine.TryHandleAbility(new AbilityRequest(1, AbilityId.Bribe, 2, null, 3), 3));
    Assert(engine.GetVoteWeight(1) == 2);
    Assert(engine.GetVoteWeight(2) == 0);
}

static void ArsonistIgnitesAllDousedPlayers()
{
    var (engine, gateway) = CreateEngine();
    engine.AssignRole(1, RoleId.Arsonist);
    engine.AssignRole(2, RoleId.Crewmate);
    engine.AssignRole(3, RoleId.Crewmate);
    Assert(engine.TryHandleAbility(new AbilityRequest(1, AbilityId.Douse, 2, null, 2), 2));
    Assert(engine.TryHandleAbility(new AbilityRequest(1, AbilityId.Douse, 3, null, 3), 3));
    Assert(engine.TryHandleAbility(new AbilityRequest(1, AbilityId.Douse, 4, null, 4), 4));
    Assert(engine.TryHandleAbility(new AbilityRequest(1, AbilityId.Ignite, null, null, 5), 5));
    Assert(!engine.Players[2].IsAlive && !engine.Players[3].IsAlive && !engine.Players[4].IsAlive);
    Assert(gateway.Events.Any(gameEvent => gameEvent.Kind == GameEventKind.Victory && gameEvent.Detail == VictoryKind.Arsonist.ToString()));
}

static void Assert(bool condition)

{
    if (!condition)
        throw new InvalidOperationException("期待した条件を満たしませんでした。");
}

sealed class TestGateway : IRoleGameGateway
{
    public List<GameEvent> Events { get; } = new();
    public bool IsWalkable(Position position) => true;
    public void Emit(GameEvent gameEvent) => Events.Add(gameEvent);
}
