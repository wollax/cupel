using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Wollax.Cupel.Tests;

public class SmokeTests
{
    [Test]
    public async Task Solution_Compiles()
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        await Assert.That(assemblies).IsNotNull();
    }
}
