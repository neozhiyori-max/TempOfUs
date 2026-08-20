using TempMod.Core;

var tests = new (string Name, Action Execute)[]
{
    ("クリーン基盤ではカスタム役職を保持しない", NoCustomRolesRemain),
    ("役職再実装世代を明示する", RebuildGenerationIsDeclared),
};

var failed = 0;
foreach (var test in tests)
{
    try
    {
        test.Execute();
        Console.WriteLine($"PASS: {test.Name}");
    }
    catch (Exception exception)
    {
        failed++;
        Console.WriteLine($"FAIL: {test.Name} — {exception.Message}");
    }
}

return failed == 0 ? 0 : 1;

static void NoCustomRolesRemain()
{
    Assert(!RoleRebuildFoundation.HasCustomRoles);
}

static void RebuildGenerationIsDeclared()
{
    Assert(RoleRebuildFoundation.Generation == "role-rebuild-1");
}

static void Assert(bool condition)
{
    if (!condition)
        throw new InvalidOperationException("期待した条件を満たしませんでした。");
}
