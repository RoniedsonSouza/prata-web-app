using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Prata.Domain.Briefing;

namespace Prata.Infrastructure.Briefing;

public interface IDirectionSheetRenderer
{
    byte[] Render(DirectionSheetModel model);
}

public sealed record DirectionSheetModel(
    Guid OrderId,
    string ServiceName,
    DateOnly? IntendedDate,
    IReadOnlyList<DirectionSheetItem> Items,
    bool WarmUpAlert,
    string? AttentionNotes,
    byte[]? ReferenceImageB8 = null,
    string? ReferenceCaptionB8 = null
);

public sealed record DirectionSheetItem(string Code, string Label, string Value, int Priority);

/// <summary>
/// Ficha de direcao em uma pagina (RN-BRF-040). QuestPDF Community — ver ADR-0006 / CLAUDE.md.
/// </summary>
public sealed class DirectionSheetRenderer : IDirectionSheetRenderer
{
    static DirectionSheetRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Render(DirectionSheetModel model)
    {
        // Corte por prioridade para caber em uma pagina.
        var items = model.Items.OrderBy(i => i.Priority).Take(18).ToList();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Helvetica"));

                page.Header()
                    .Column(col =>
                    {
                        col.Item().Text("Ficha de direcao").SemiBold().FontSize(14);
                        col.Item().Text($"Pedido {model.OrderId:N} · {model.ServiceName}").FontSize(8).FontColor(Colors.Grey.Darken2);
                        if (model.IntendedDate is DateOnly d)
                            col.Item().Text($"Data pretendida: {d:dd/MM/yyyy}").FontSize(8);
                    });

                page.Content()
                    .PaddingVertical(8)
                    .Column(col =>
                    {
                        if (model.WarmUpAlert)
                        {
                            col.Item()
                                .Background(Colors.Orange.Lighten4)
                                .Padding(6)
                                .Text("Aquecimento: reservar ~20 min (B4 < 3).")
                                .SemiBold();
                        }

                        if (!string.IsNullOrWhiteSpace(model.AttentionNotes))
                        {
                            col.Item()
                                .PaddingTop(6)
                                .Background(Colors.Red.Lighten4)
                                .Padding(6)
                                .Text($"ATENCAO: {model.AttentionNotes}");
                        }

                        if (model.ReferenceImageB8 is { Length: > 0 } img)
                        {
                            col.Item().PaddingTop(6).Text("B8 · referencia").SemiBold();
                            if (!string.IsNullOrWhiteSpace(model.ReferenceCaptionB8))
                                col.Item().Text(model.ReferenceCaptionB8!).FontSize(8).FontColor(Colors.Grey.Darken1);

                            col.Item()
                                .PaddingTop(4)
                                .MaxHeight(160)
                                .AlignLeft()
                                .Width(140)
                                .Image(img)
                                .FitArea();
                        }

                        col.Item().PaddingTop(8).Text("Direcao").SemiBold();
                        foreach (var item in items.Where(i => i.Code != "B8"))
                        {
                            col.Item()
                                .PaddingVertical(2)
                                .Row(row =>
                                {
                                    row.ConstantItem(36).Text(item.Code).SemiBold();
                                    row.RelativeItem().Column(c =>
                                    {
                                        c.Item().Text(item.Label).FontSize(8).FontColor(Colors.Grey.Darken1);
                                        c.Item().Text(FormatValue(item));
                                    });
                                });
                        }
                    });

                page.Footer()
                    .AlignCenter()
                    .Text(t =>
                    {
                        t.Span("Gerada sob demanda · nao anexar em e-mail · ").FontSize(7).FontColor(Colors.Grey.Medium);
                        t.Span($"ficha-{model.OrderId:N}.pdf").FontSize(7);
                    });
            });
        });

        return document.GeneratePdf();
    }

    private static string FormatValue(DirectionSheetItem item)
    {
        if (item.Code is "B1" or "B4" or "B5")
        {
            if (int.TryParse(item.Value, out var scale))
            {
                scale = Math.Clamp(scale, 0, 5);
                return new string('●', scale) + new string('○', 5 - scale);
            }
        }

        return item.Value;
    }
}
