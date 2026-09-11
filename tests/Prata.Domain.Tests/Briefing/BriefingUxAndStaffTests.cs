using AwesomeAssertions;
using Prata.Application.Briefing;
using Prata.Domain.Sales;

namespace Prata.Domain.Tests.Briefing;

public sealed class BriefingUxAndStaffTests
{
    [Fact]
    public void B2_nao_sei_sugere_teste_das_duas_selfies()
    {
        BriefingUxHints.SuggestSelfieSideTest("B2", """{"option":"NAO_SEI"}""").Should().BeTrue();
        BriefingUxHints.SuggestSelfieSideTest("B2", """{"option":"ESQ"}""").Should().BeFalse();
        BriefingUxHints.SuggestSelfieSideTest("B1", """{"option":"NAO_SEI"}""").Should().BeFalse();
    }

    [Fact]
    public void RN_BRF_030_staff_so_ve_sensivel_se_designado()
    {
        var order = Order
            .Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)), DateTimeOffset.UtcNow)
            .Value;

        var staff = Guid.NewGuid();
        order.PodeVerBriefingSensivel(staff, isOwner: false).Should().BeFalse();
        order.DesignarStaffSensivel(staff).IsSuccess.Should().BeTrue();
        order.PodeVerBriefingSensivel(staff, isOwner: false).Should().BeTrue();
        order.PodeVerBriefingSensivel(Guid.NewGuid(), isOwner: false).Should().BeFalse();
        order.PodeVerBriefingSensivel(Guid.NewGuid(), isOwner: true).Should().BeTrue();

        order.RemoverStaffSensivel(staff).IsSuccess.Should().BeTrue();
        order.PodeVerBriefingSensivel(staff, isOwner: false).Should().BeFalse();
    }
}
