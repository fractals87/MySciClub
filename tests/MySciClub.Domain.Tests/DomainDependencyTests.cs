using System.Reflection;
using Xunit;

namespace MySciClub.Domain.Tests;

public sealed class DomainDependencyTests
{
    [Fact]
    public void DomainAssembly_DoesNotReferenceOtherApplicationProjectsOrInfrastructure()
    {
        var references = Assembly.Load("MySciClub.Domain")
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .Where(name => name is not null)
            .ToArray();

        Assert.DoesNotContain(references, name => name!.StartsWith("MySciClub."));
        Assert.DoesNotContain(references, name => name!.StartsWith("Microsoft.EntityFrameworkCore"));
        Assert.DoesNotContain(references, name => name!.StartsWith("Microsoft.AspNetCore"));
    }
}
