using TempMod.Core;

var tests = new (string Name, Action Run)[]
{
    ("シェリフは敵をキルできる", SheriffKillsEnemy),
    ("シェリフはクルーを誤射すると自爆する", SheriffMisfire),
    ("バリアは一回だけキルを防ぐ", BarrierBlocksKill),
    ("ヴァンパイアは時間差で死亡させる", VampireBite),
    ("ヴァンパイアの噛みつきタイマーは会議中に停止する", VampireBiteTimerPausesDuringMeeting),
    ("ラバーズは後追いする", LoversDieTogether),
    ("市長は二票を持つ", MayorHasDoubleVote),
    ("SNR版マフィアは他インポスター生存中にキルできず、最後に解放される", MafiaKillUnlocksOnlyAfterOtherImpostorsAreGone),
    ("シェリフのキル回数上限を守る", SheriffKillLimit),
    ("シェリフは設定で第三陣営をキルできない", SheriffCannotKillNeutralWhenDisabled),
    ("1人でも役職抽選できる", SinglePlayerAssignment),
    ("陣営別人数上限を守る", FactionRoleLimitsAreRespected),
    ("クリーナーは死体を清掃できる", CleanerRemovesBody),
    ("アンダーテイカーは死体を牽引・配置できる", UndertakerCarriesAndDropsBody),
    ("アンダーテイカーは会議開始時に牽引死体を配置する", UndertakerDropsBodyAtMeetingStart),
    ("アンダーテイカー死亡時に牽引死体を配置する", UndertakerDropsBodyOnCarrierDeath),
    ("ボマーは時限爆弾で周囲を巻き込む", BomberExplodesNearbyPlayers),
    ("ジャッカルはクルーをサイドキックに勧誘できる", JackalRecruitsSidekick),
    ("ジャッカル勧誘は役職変更イベントを通知する", JackalRecruitEmitsRoleChanged),
    ("ジャッカルはクルー以外を勧誘できない", JackalCannotRecruitNonCrew),
    ("ジャッカルが勧誘できるサイドキックは1人だけ", JackalCanRecruitOnlyOneSidekick),
    ("ジャッカル勧誘は専用クールダウンを使う", JackalRecruitUsesDedicatedCooldown),
    ("親ジャッカル死亡時にサイドキックが昇格する", SidekickPromotesWhenParentJackalDies),
    ("ジャッカルは別第三陣営キラーが残る間は勝利しない", JackalDoesNotWinWhileEnemyKillerLives),
    ("ジャッカルはサイドキックをキルできない", JackalCannotKillSidekick),
    ("サイドキックはジャッカルをキルできない", SidekickCannotKillJackal),
    ("サイドキックはジャッカル以外をキルできる", SidekickCanKillEnemy),
    ("会議終了後に会議状態が解除される", MeetingEndRestoresNormalState),
    ("会議追放されたジェスターは勝利する", ExiledJesterWins),
    ("全役職に定義が存在する", EveryRoleHasDefinition),
    ("全役職に説明が存在する", EveryRoleHasDescription),
    ("ゾンビはクルーを子ゾンビに感染できる", ZombieInfectsCrew),
    ("ハゲタカの死体回収数は時間経過後も残る", VultureCollectionCountPersists),
    ("マッドゲッサーは会議中の正解で対象をキルできる", MadGuesserKillsCorrectRole),
    ("マッドゲッサーは設定された会議内残弾まで推測できる", MadGuesserUsesConfiguredShotsPerMeeting),
    ("マッドゲッサーは会議内残弾を超えて推測できない", MadGuesserCannotExceedShotsPerMeeting),
    ("マッドゲッサーは次会議で推測残弾を回復する", MadGuesserShotsResetAtNextMeeting),
    ("マッドゲッサーは会議中の誤答で自爆する", MadGuesserDiesOnWrongGuess),
    ("アドボケイトの買収は自分を二票、対象を零票にする", AdvocateBribeChangesVoteWeight),
    ("アルソニストは全員への注油後に点火で勝利する", ArsonistIgnitesAllDousedPlayers),
    ("アルソニストの注油進捗は会議後も保持する", ArsonistDouseProgressPersistsThroughMeeting),
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

static void VampireBiteTimerPausesDuringMeeting()
{
    var (engine, _) = CreateEngine();
    engine.AssignRole(1, RoleId.Vampire);
    engine.AssignRole(2, RoleId.Crewmate);
    Assert(engine.TryHandleAbility(new AbilityRequest(1, AbilityId.Bite, 2, null, 2), 2));
    engine.StartMeeting(3);
    engine.Tick(6);
    Assert(engine.Players[2].IsAlive);
    engine.EndMeeting(null, 7, evaluateVictory: false);
    engine.Tick(7.9f);
    Assert(engine.Players[2].IsAlive);
    engine.Tick(8);
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

static void MafiaKillUnlocksOnlyAfterOtherImpostorsAreGone()
{
    var (engine, _) = CreateEngine();
    engine.AssignRole(1, RoleId.Mafia);
    engine.AssignRole(2, RoleId.Impostor);
    engine.AssignRole(3, RoleId.Crewmate);

    Assert(!engine.TryHandleAbility(new AbilityRequest(1, AbilityId.Kill, 3, null, 2), 2));
    Assert(engine.Players[3].IsAlive);

    // 上流のMafia.IsKillFlagと同じく、他の生存インポスターがいなくなった時点で解放される。
    engine.Players[2].IsAlive = false;
    Assert(engine.TryHandleAbility(new AbilityRequest(1, AbilityId.Kill, 3, null, 3), 3));
    Assert(!engine.Players[3].IsAlive);
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

static void UndertakerCarriesAndDropsBody()
{
    var (engine, _) = CreateEngine();
    engine.AssignRole(1, RoleId.Undertaker);
    engine.AssignRole(2, RoleId.Crewmate);
    engine.AssignRole(3, RoleId.Impostor);
    Assert(engine.TryHandleAbility(new AbilityRequest(3, AbilityId.Kill, 2, null, 2), 2));
    Assert(engine.TryHandleAbility(new AbilityRequest(1, AbilityId.CarryBody, 2, null, 3), 3));
    Assert(engine.Players[1].CarriedBodyOwnerId == 2);
    Assert(engine.Bodies[2].IsCarried);
    var destination = new Position(4, 5);
    Assert(engine.TryHandleAbility(new AbilityRequest(1, AbilityId.DropBody, null, destination, 4), 4));
    Assert(engine.Players[1].CarriedBodyOwnerId is null);
    Assert(!engine.Bodies[2].IsCarried);
    Assert(engine.Bodies[2].Position == destination);
}

static void UndertakerDropsBodyAtMeetingStart()
{
    var (engine, _) = CreateEngine();
    engine.AssignRole(1, RoleId.Undertaker);
    engine.AssignRole(2, RoleId.Crewmate);
    engine.AssignRole(3, RoleId.Impostor);
    Assert(engine.TryHandleAbility(new AbilityRequest(3, AbilityId.Kill, 2, null, 2), 2));
    Assert(engine.TryHandleAbility(new AbilityRequest(1, AbilityId.CarryBody, 2, null, 3), 3));
    engine.UpdatePosition(1, new Position(7, 8), 4);
    engine.StartMeeting(5);
    Assert(engine.Players[1].CarriedBodyOwnerId is null);
    Assert(!engine.Bodies[2].IsCarried);
    Assert(engine.Bodies[2].Position == new Position(7, 8));
}

static void UndertakerDropsBodyOnCarrierDeath()
{
    var (engine, _) = CreateEngine();
    engine.AssignRole(1, RoleId.Undertaker);
    engine.AssignRole(2, RoleId.Crewmate);
    // アンダーテイカーと敵対するジャッカルが運搬者をキルする状況を作る。
    engine.AssignRole(3, RoleId.Jackal);
    Assert(engine.TryHandleAbility(new AbilityRequest(3, AbilityId.Kill, 2, null, 2), 2));
    Assert(engine.TryHandleAbility(new AbilityRequest(1, AbilityId.CarryBody, 2, null, 3), 3));
    engine.UpdatePosition(1, new Position(6, 9), 4);
    engine.UpdatePosition(3, new Position(5, 9), 32);
    if (!engine.TryHandleAbility(new AbilityRequest(3, AbilityId.Kill, 1, null, 33), 33))
        throw new InvalidOperationException("ジャッカルが運搬者をキルできませんでした。");
    if (engine.Players[1].IsAlive)
        throw new InvalidOperationException("運搬者が死亡状態になりませんでした。");
    if (engine.Bodies[2].IsCarried)
        throw new InvalidOperationException("運搬者死亡後も死体が運搬中です。");
    if (engine.Bodies[2].Position != new Position(6, 9))
        throw new InvalidOperationException($"死体配置座標が不正です: {engine.Bodies[2].Position}");
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

static void JackalRecruitEmitsRoleChanged()
{
    var (engine, gateway) = CreateEngine();
    engine.AssignRole(1, RoleId.Jackal);
    engine.AssignRole(2, RoleId.Crewmate);
    Assert(engine.TryHandleAbility(new AbilityRequest(1, AbilityId.RecruitSidekick, 2, null, 2), 2));
    Assert(gateway.Events.Any(gameEvent => gameEvent.Kind == GameEventKind.RoleChanged && gameEvent.ActorId == 1 && gameEvent.TargetId == 2 && gameEvent.Detail == RoleId.Sidekick.ToString()));
}
static void JackalCannotRecruitNonCrew()
{
    var (engine, _) = CreateEngine();
    engine.AssignRole(1, RoleId.Jackal);
    engine.AssignRole(2, RoleId.Impostor);
    Assert(!engine.TryHandleAbility(new AbilityRequest(1, AbilityId.RecruitSidekick, 2, null, 2), 2));
    Assert(engine.Players[2].PrimaryRole == RoleId.Impostor);
}
static void JackalCanRecruitOnlyOneSidekick()
{
    var (engine, _) = CreateEngine();
    engine.AssignRole(1, RoleId.Jackal);
    engine.AssignRole(2, RoleId.Crewmate);
    engine.AssignRole(3, RoleId.Crewmate);
    Assert(engine.TryHandleAbility(new AbilityRequest(1, AbilityId.RecruitSidekick, 2, null, 2), 2));
    Assert(!engine.TryHandleAbility(new AbilityRequest(1, AbilityId.RecruitSidekick, 3, null, 3), 3));
    Assert(engine.Players[2].PrimaryRole == RoleId.Sidekick);
    Assert(engine.Players[3].PrimaryRole == RoleId.Crewmate);
}
static void JackalRecruitUsesDedicatedCooldown()
{
    var gateway = new TestGateway();
    var engine = new RoleEngine(gateway, new RoleOptions
    {
        StandardKillCooldown = 0,
        JackalKillCooldown = 0,
        JackalSidekickCooldown = 17,
        KillDistance = 3,
    });
    engine.RegisterPlayer(1, "Jackal");
    engine.RegisterPlayer(2, "Crew");
    engine.UpdatePosition(1, new Position(0, 0), 1);
    engine.UpdatePosition(2, new Position(1, 0), 1);
    engine.AssignRole(1, RoleId.Jackal);
    engine.AssignRole(2, RoleId.Crewmate);

    Assert(engine.TryHandleAbility(new AbilityRequest(1, AbilityId.RecruitSidekick, 2, null, 2), 2));
    Assert(engine.Players[1].AbilityCooldowns[AbilityId.RecruitSidekick] == 19);
    Assert(!engine.Players[1].AbilityCooldowns.ContainsKey(AbilityId.Kill));
}

static void SidekickPromotesWhenParentJackalDies()
{
    var (engine, gateway) = CreateEngine();
    engine.AssignRole(1, RoleId.Jackal);
    engine.AssignRole(2, RoleId.Crewmate);
    engine.AssignRole(3, RoleId.Impostor);
    Assert(engine.TryHandleAbility(new AbilityRequest(1, AbilityId.RecruitSidekick, 2, null, 2), 2));
    Assert(engine.Players[2].PrimaryRole == RoleId.Sidekick);

    Assert(engine.TryHandleAbility(new AbilityRequest(3, AbilityId.Kill, 1, null, 3), 3));
    Assert(!engine.Players[1].IsAlive);
    Assert(engine.Players[2].PrimaryRole == RoleId.Jackal);
    Assert(!engine.Players[2].EffectTargets.ContainsKey(AbilityId.RecruitSidekick));
    Assert(gateway.Events.Any(gameEvent => gameEvent.Kind == GameEventKind.RoleChanged && gameEvent.TargetId == 2 && gameEvent.Detail == "SidekickPromotedToJackal"));
}

static void JackalDoesNotWinWhileEnemyKillerLives()
{
    var (engine, gateway) = CreateEngine();
    engine.AssignRole(1, RoleId.Jackal);
    engine.AssignRole(2, RoleId.Crewmate);
    engine.AssignRole(3, RoleId.Vampire);

    Assert(engine.TryHandleAbility(new AbilityRequest(1, AbilityId.Kill, 2, null, 2), 2));
    Assert(!gateway.Events.Any(gameEvent => gameEvent.Kind == GameEventKind.Victory && gameEvent.Detail == VictoryKind.Jackal.ToString()));
    Assert(engine.TryHandleAbility(new AbilityRequest(1, AbilityId.Kill, 3, null, 33), 33));
    Assert(gateway.Events.Any(gameEvent => gameEvent.Kind == GameEventKind.Victory && gameEvent.Detail == VictoryKind.Jackal.ToString()));
}

static void JackalCannotKillSidekick()
{
    var (engine, _) = CreateEngine();
    engine.AssignRole(1, RoleId.Jackal);
    engine.AssignRole(2, RoleId.Crewmate);
    Assert(engine.TryHandleAbility(new AbilityRequest(1, AbilityId.RecruitSidekick, 2, null, 2), 2));
    Assert(!engine.TryHandleAbility(new AbilityRequest(1, AbilityId.Kill, 2, null, 3), 3));
    Assert(engine.Players[1].IsAlive && engine.Players[2].IsAlive);
}
static void SidekickCannotKillJackal()
{
    var (engine, _) = CreateEngine();
    engine.AssignRole(1, RoleId.Jackal);
    engine.AssignRole(2, RoleId.Crewmate);
    Assert(engine.TryHandleAbility(new AbilityRequest(1, AbilityId.RecruitSidekick, 2, null, 2), 2));
    Assert(!engine.TryHandleAbility(new AbilityRequest(2, AbilityId.Kill, 1, null, 3), 3));
    Assert(engine.Players[1].IsAlive && engine.Players[2].IsAlive);
}
static void SidekickCanKillEnemy()
{
    var (engine, _) = CreateEngine();
    engine.AssignRole(1, RoleId.Jackal);
    engine.AssignRole(2, RoleId.Crewmate);
    engine.AssignRole(3, RoleId.Crewmate);
    Assert(engine.TryHandleAbility(new AbilityRequest(1, AbilityId.RecruitSidekick, 2, null, 2), 2));
    Assert(engine.TryHandleAbility(new AbilityRequest(2, AbilityId.Kill, 3, null, 3), 3));
    Assert(!engine.Players[3].IsAlive);
}
static void MeetingEndRestoresNormalState()
{
    var (engine, _) = CreateEngine();
    engine.AssignRole(1, RoleId.Jackal);
    engine.AssignRole(2, RoleId.Crewmate);
    engine.StartMeeting(2);
    Assert(engine.IsMeetingActive);
    engine.EndMeeting(null, 5, vanillaExileAlreadyApplied: true, evaluateVictory: false);
    Assert(!engine.IsMeetingActive);
    Assert(engine.TryHandleAbility(new AbilityRequest(1, AbilityId.Kill, 2, null, 6), 6));
    Assert(!engine.Players[2].IsAlive);
}
static void ExiledJesterWins()
{
    var (engine, gateway) = CreateEngine();
    engine.AssignRole(1, RoleId.Jester);
    engine.AssignRole(2, RoleId.Crewmate);
    engine.StartMeeting(2);
    var result = engine.EndMeeting(1, 3);
    Assert(!engine.IsMeetingActive);
    Assert(!engine.Players[1].IsAlive);
    Assert(result.Kind == VictoryKind.Jester);
    Assert(gateway.Events.Any(gameEvent => gameEvent.Kind == GameEventKind.Victory && gameEvent.Detail == VictoryKind.Jester.ToString()));
}
static void EveryRoleHasDefinition()
{
    foreach (var role in Enum.GetValues<RoleId>())
    {
        var definition = RoleCatalog.Get(role);
        Assert(definition.Id == role);
        Assert(!string.IsNullOrWhiteSpace(definition.DisplayName));
    }
}
static void EveryRoleHasDescription()
{
    foreach (var role in Enum.GetValues<RoleId>())
        Assert(!string.IsNullOrWhiteSpace(RoleDescriptionCatalog.Get(role)));
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

static void MadGuesserUsesConfiguredShotsPerMeeting()
{
    var gateway = new TestGateway();
    var engine = new RoleEngine(gateway, new RoleOptions { MadGuesserShotsPerMeeting = 2, StandardKillCooldown = 0, KillDistance = 3 });
    for (byte playerId = 1; playerId <= 4; playerId++)
    {
        engine.RegisterPlayer(playerId, $"P{playerId}");
        engine.UpdatePosition(playerId, new Position(playerId, 0), 1);
    }
    engine.AssignRole(1, RoleId.MadGuesser);
    engine.AssignRole(2, RoleId.Sheriff);
    engine.AssignRole(3, RoleId.Doctor);
    engine.AssignRole(4, RoleId.Impostor);
    engine.StartMeeting(2);
    Assert(engine.TryHandleAbility(new AbilityRequest(1, AbilityId.GuessRole, 2, new Position((byte)RoleId.Sheriff, 0), 3), 3));
    Assert(engine.TryHandleAbility(new AbilityRequest(1, AbilityId.GuessRole, 3, new Position((byte)RoleId.Doctor, 0), 4), 4));
    Assert(engine.GetMadGuesserShotsRemaining(1) == 0);
    Assert(!engine.Players[2].IsAlive && !engine.Players[3].IsAlive);
}

static void MadGuesserCannotExceedShotsPerMeeting()
{
    var (engine, _) = CreateEngine();
    engine.AssignRole(1, RoleId.MadGuesser);
    engine.AssignRole(2, RoleId.Sheriff);
    engine.AssignRole(3, RoleId.Doctor);
    engine.StartMeeting(2);
    Assert(engine.TryHandleAbility(new AbilityRequest(1, AbilityId.GuessRole, 2, new Position((byte)RoleId.Sheriff, 0), 3), 3));
    Assert(!engine.TryHandleAbility(new AbilityRequest(1, AbilityId.GuessRole, 3, new Position((byte)RoleId.Doctor, 0), 4), 4));
    Assert(engine.Players[3].IsAlive);
}

static void MadGuesserShotsResetAtNextMeeting()
{
    var (engine, _) = CreateEngine();
    engine.AssignRole(1, RoleId.MadGuesser);
    engine.AssignRole(2, RoleId.Sheriff);
    engine.AssignRole(3, RoleId.Impostor);
    engine.StartMeeting(2);
    Assert(engine.TryHandleAbility(new AbilityRequest(1, AbilityId.GuessRole, 2, new Position((byte)RoleId.Sheriff, 0), 3), 3));
    Assert(engine.GetMadGuesserShotsRemaining(1) == 0);
    engine.EndMeeting(null, 4, evaluateVictory: false);
    engine.StartMeeting(5);
    Assert(engine.GetMadGuesserShotsRemaining(1) == 1);
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

static void ArsonistDouseProgressPersistsThroughMeeting()
{
    var (engine, gateway) = CreateEngine();
    engine.AssignRole(1, RoleId.Arsonist);
    engine.AssignRole(2, RoleId.Crewmate);
    engine.AssignRole(3, RoleId.Crewmate);
    Assert(engine.TryHandleAbility(new AbilityRequest(1, AbilityId.Douse, 2, null, 2), 2));
    Assert(engine.TryHandleAbility(new AbilityRequest(1, AbilityId.Douse, 3, null, 3), 3));
    Assert(engine.TryHandleAbility(new AbilityRequest(1, AbilityId.Douse, 4, null, 4), 4));
    engine.StartMeeting(5);
    engine.EndMeeting(null, 10, evaluateVictory: false);
    Assert(engine.TryHandleAbility(new AbilityRequest(1, AbilityId.Ignite, null, null, 11), 11));
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
