using Ironwall.Dotnet.Libraries.Nats.Models;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Nats.Tests;

public class NatsSetupModelTests
{
    [Fact]
    public void EffectiveSubject_When3FieldsSet_ReturnsComposedSubject()
    {
        var model = new NatsSetupModel
        {
            DomainNats = "sensorway",
            GroupNats = "unit001",
            SubsystemNats = "gis",
            DefaultSubjectNats = "fallback.>"
        };

        Assert.Equal("sensorway.unit001.gis.>", model.EffectiveSubject);
    }

    [Fact]
    public void EffectiveSubject_WhenDomainEmpty_ReturnsDefaultSubject()
    {
        var model = new NatsSetupModel
        {
            DomainNats = "",
            GroupNats = "unit001",
            SubsystemNats = "gis",
            DefaultSubjectNats = "fallback.>"
        };

        Assert.Equal("fallback.>", model.EffectiveSubject);
    }

    [Fact]
    public void EffectiveSubject_WhenGroupEmpty_ReturnsDefaultSubject()
    {
        var model = new NatsSetupModel
        {
            DomainNats = "sensorway",
            GroupNats = null,
            SubsystemNats = "gis",
            DefaultSubjectNats = "fallback.>"
        };

        Assert.Equal("fallback.>", model.EffectiveSubject);
    }

    [Fact]
    public void EffectiveSubject_WhenSubsystemEmpty_ReturnsDefaultSubject()
    {
        var model = new NatsSetupModel
        {
            DomainNats = "sensorway",
            GroupNats = "unit001",
            SubsystemNats = "",
            DefaultSubjectNats = "fallback.>"
        };

        Assert.Equal("fallback.>", model.EffectiveSubject);
    }

    [Fact]
    public void EffectiveSubject_WhenAllNullAndNoDefault_ReturnsFallbackLiteral()
    {
        var model = new NatsSetupModel
        {
            DomainNats = null,
            GroupNats = null,
            SubsystemNats = null,
            DefaultSubjectNats = null
        };

        Assert.Equal("default.>", model.EffectiveSubject);
    }

    [Fact]
    public void CopyConstructor_CopiesAllNewFields()
    {
        var original = new NatsSetupModel
        {
            IpAddressNats = "10.0.0.1",
            PortNats = 4222,
            DomainNats = "sensorway",
            GroupNats = "unit002",
            SubsystemNats = "nvr",
            DefaultSubjectNats = "fallback.>",
            UsernameNats = "user",
            PasswordNats = "pass",
            ConnectionTimeoutNats = 3000
        };

        var copy = new NatsSetupModel(original);

        Assert.Equal(original.IpAddressNats, copy.IpAddressNats);
        Assert.Equal(original.PortNats, copy.PortNats);
        Assert.Equal(original.DomainNats, copy.DomainNats);
        Assert.Equal(original.GroupNats, copy.GroupNats);
        Assert.Equal(original.SubsystemNats, copy.SubsystemNats);
        Assert.Equal(original.DefaultSubjectNats, copy.DefaultSubjectNats);
        Assert.Equal(original.UsernameNats, copy.UsernameNats);
        Assert.Equal(original.PasswordNats, copy.PasswordNats);
        Assert.Equal(original.ConnectionTimeoutNats, copy.ConnectionTimeoutNats);
    }
}
