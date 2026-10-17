using System.Globalization;
using HireYar.Application.Billing;
using HireYar.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HireYar.Tests;

public sealed class RegistrationTrialOptionsTests
{
    private static ServiceProvider Provider(int days, string credit)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RegistrationTrial:DurationDays"] = days.ToString(CultureInfo.InvariantCulture),
            ["RegistrationTrial:CreditAmountRial"] = credit,
            ["AI:Provider"] = "Disabled"
        }).Build();
        return new ServiceCollection().AddInfrastructure(config).BuildServiceProvider();
    }

    [Theory]
    [InlineData(2, "0.5")]
    [InlineData(1, "0.000001")]
    [InlineData(3650, "999999999999.999999")]
    public void Configured_values_bind_without_rounding(int days, string credit)
    {
        using var provider = Provider(days, credit);
        var options = provider.GetRequiredService<IOptions<RegistrationTrialOptions>>().Value;
        Assert.Equal(days, options.DurationDays);
        Assert.Equal(decimal.Parse(credit, CultureInfo.InvariantCulture), options.CreditAmountRial);
    }

    [Theory]
    [InlineData(0, "1")]
    [InlineData(-1, "1")]
    [InlineData(3651, "1")]
    [InlineData(3, "0")]
    [InlineData(3, "-0.5")]
    [InlineData(3, "0.0000001")]
    [InlineData(3, "1000000000000")]
    public void Invalid_configuration_is_rejected(int days, string credit)
    {
        using var provider = Provider(days, credit);
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<RegistrationTrialOptions>>().Value);
    }
}
