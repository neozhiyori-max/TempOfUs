namespace TempMod.Core;

/// <summary>役職の出現可否、人数上限、出現率を定義するホスト設定。</summary>
public sealed record RoleSpawnOption(RoleId Role, bool Enabled = true, int MaxCount = 1, int ChancePercent = 100);

public sealed class RoleAssignmentOptions
{
    /// <summary>ゲーム内でインポスター陣営として扱う人数上限。</summary>
    public int ImpostorCount { get; init; } = 2;
    /// <summary>クルー特殊役職を配布する人数上限。</summary>
    public int CrewRoleCount { get; init; } = 10;
    /// <summary>第三陣営特殊役職を配布する人数上限。</summary>
    public int NeutralRoleCount { get; init; } = 1;
    public bool EnableLovers { get; init; } = true;
    public IReadOnlyList<RoleSpawnOption> Roles { get; init; } = new[]
    {
        new RoleSpawnOption(RoleId.Sheriff), new RoleSpawnOption(RoleId.Doctor),
        new RoleSpawnOption(RoleId.MadScientist), new RoleSpawnOption(RoleId.Tracker),
        new RoleSpawnOption(RoleId.TimeTraveler), new RoleSpawnOption(RoleId.Seer),
        new RoleSpawnOption(RoleId.BarrierNic), new RoleSpawnOption(RoleId.LightWorker),
        new RoleSpawnOption(RoleId.Investigator), new RoleSpawnOption(RoleId.Mayor),
        new RoleSpawnOption(RoleId.Ninja), new RoleSpawnOption(RoleId.Warlock),
        new RoleSpawnOption(RoleId.Mafia), new RoleSpawnOption(RoleId.Puppeteer),
        new RoleSpawnOption(RoleId.Eraser), new RoleSpawnOption(RoleId.Undertaker),
        new RoleSpawnOption(RoleId.Jester), new RoleSpawnOption(RoleId.Jackal),
        new RoleSpawnOption(RoleId.Vampire),
    };
}

public sealed record RoleAssignment(
    IReadOnlyDictionary<byte, RoleId> PrimaryRoles,
    IReadOnlyDictionary<byte, IReadOnlyList<ModifierId>> Modifiers,
    IReadOnlyList<(byte First, byte Second)> LoversPairs);

/// <summary>
/// ベースのクルー／インポスター枠を先に確保し、その枠に合う特殊役職だけを重み付きで抽選する。
/// 役職がプレイヤー数を上回る場合は、自動的に一部を未選出にする。
/// </summary>
public static class RoleAssignmentPlanner
{
    public static RoleAssignment Create(IReadOnlyList<byte> playerIds, RoleAssignmentOptions options, Random random)
    {
        if (playerIds.Count < 1)
            throw new ArgumentException("役職割当には少なくとも1人のプレイヤーが必要です。", nameof(playerIds));
        if (playerIds.Distinct().Count() != playerIds.Count)
            throw new ArgumentException("プレイヤーIDは重複できません。", nameof(playerIds));

        var shuffledPlayers = playerIds.OrderBy(_ => random.Next()).ToList();
        var impostorCount = Math.Clamp(options.ImpostorCount, 1, Math.Max(1, playerIds.Count - 1));
        var impostorSlots = shuffledPlayers.Take(impostorCount).ToList();
        var crewSlots = shuffledPlayers.Skip(impostorCount).ToList();
        var result = playerIds.ToDictionary(id => id, _ => RoleId.Crewmate);
        foreach (var impostor in impostorSlots)
            result[impostor] = RoleId.Impostor;

        AssignFactionRoles(result, crewSlots, Faction.Crew, options.Roles, Math.Clamp(options.CrewRoleCount, 0, 15), random);
        AssignFactionRoles(result, impostorSlots, Faction.Impostor, options.Roles, Math.Clamp(options.ImpostorCount, 0, 15), random);

        // 第三陣営はクルー枠から、ホストが指定した上限まで配布する。
        var neutralCandidates = ExpandCandidates(options.Roles, Faction.Neutral);
        var neutralCount = Math.Min(Math.Min(Math.Clamp(options.NeutralRoleCount, 0, 15), neutralCandidates.Count), crewSlots.Count);
        foreach (var playerId in crewSlots.OrderBy(_ => random.Next()).Take(neutralCount))
        {
            if (!TryDrawByChance(neutralCandidates, random, out var role))
                continue;
            neutralCandidates.Remove(role);
            result[playerId] = role.Role;
        }

        var modifiers = playerIds.ToDictionary(id => id, _ => (IReadOnlyList<ModifierId>)Array.Empty<ModifierId>());
        var pairs = new List<(byte First, byte Second)>();
        if (options.EnableLovers && playerIds.Count >= 2)
        {
            var lovers = shuffledPlayers.OrderBy(_ => random.Next()).Take(2).ToArray();
            modifiers[lovers[0]] = new[] { ModifierId.Lovers };
            modifiers[lovers[1]] = new[] { ModifierId.Lovers };
            pairs.Add((lovers[0], lovers[1]));
        }

        return new RoleAssignment(result, modifiers, pairs);
    }

    private static void AssignFactionRoles(
        Dictionary<byte, RoleId> result,
        IReadOnlyList<byte> slots,
        Faction faction,
        IReadOnlyList<RoleSpawnOption> options,
        int maxAssignments,
        Random random)
    {
        var candidates = ExpandCandidates(options, faction);
        foreach (var playerId in slots.OrderBy(_ => random.Next()).Take(maxAssignments))
        {
            if (candidates.Count == 0)
                return;
            if (!TryDrawByChance(candidates, random, out var role))
            {
                // 有効なカスタム役職が候補に残る場合、役職なしで試合を始めない。
                // 出現率は抽選順位へ反映済みであり、候補が一つでもある限り必ず一つを割り当てる。
                role = candidates[random.Next(candidates.Count)];
            }
            candidates.Remove(role);
            result[playerId] = role.Role;
        }
    }

    private static List<RoleSpawnOption> ExpandCandidates(IEnumerable<RoleSpawnOption> options, Faction faction)
    {
        return options
            .Where(option => option.Enabled && option.ChancePercent > 0 && RoleCatalog.GetFaction(option.Role) == faction)
            .SelectMany(option => Enumerable.Repeat(option, Math.Clamp(option.MaxCount, 0, 15)))
            .ToList();
    }

    private static bool TryDrawByChance(IReadOnlyList<RoleSpawnOption> candidates, Random random, out RoleSpawnOption selected)
    {
        selected = default!;
        if (candidates.Count == 0)
            return false;

        var total = candidates.Sum(candidate => Math.Max(1, candidate.ChancePercent));
        var choice = random.Next(total);
        foreach (var candidate in candidates)
        {
            choice -= Math.Max(1, candidate.ChancePercent);
            if (choice < 0)
            {
                if (random.Next(100) >= Math.Clamp(candidate.ChancePercent, 0, 100))
                    return false;
                selected = candidate;
                return true;
            }
        }
        return false;
    }
}
