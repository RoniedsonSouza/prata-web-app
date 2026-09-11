using AwesomeAssertions;
using Prata.Infrastructure.Briefing;

namespace Prata.Application.Tests.Briefing;

public sealed class DirectionSheetRendererTests
{
    [Fact]
    public void Render_embute_imagem_B8_quando_informada()
    {
        // PNG 1x1 minimo
        var png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="
        );

        var renderer = new DirectionSheetRenderer();
        var pdf = renderer.Render(
            new DirectionSheetModel(
                Guid.NewGuid(),
                "Ensaio",
                DateOnly.FromDateTime(DateTime.UtcNow.Date),
                [new DirectionSheetItem("B8", "Referencia", "sorriso", 1), new DirectionSheetItem("B4", "Conforto", "2", 1)],
                WarmUpAlert: true,
                AttentionNotes: null,
                ReferenceImageB8: png,
                ReferenceCaptionB8: "porque gosto"
            )
        );

        pdf.Length.Should().BeGreaterThan(500);
        // PDF com imagem embutida tipicamente contem stream /Image
        var text = System.Text.Encoding.ASCII.GetString(pdf);
        text.Should().Contain("/Subtype /Image");
    }
}
