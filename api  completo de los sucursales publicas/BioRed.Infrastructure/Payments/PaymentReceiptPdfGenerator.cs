using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BioRed.Infrastructure.Payments;

internal sealed class PaymentReceiptPdfGenerator
{
    private const string PrimaryColor = "#075985";
    private const string LightColor = "#E0F2FE";
    private const string BorderColor = "#CBD5E1";

    public PaymentReceiptPdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Generate(PaymentReceiptDocumentData data) =>
        Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(38);
                page.DefaultTextStyle(style =>
                    style.FontSize(10).FontColor(Colors.Grey.Darken3));

                page.Header().Column(column =>
                {
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(left =>
                        {
                            left.Item().Text(data.CompanyName)
                                .FontSize(20).Bold().FontColor(PrimaryColor);
                            left.Item().Text(data.BranchName).FontSize(11);
                            left.Item().Text(data.BranchAddress).FontSize(9).FontColor(Colors.Grey.Darken1);
                        });

                        row.ConstantItem(210).AlignRight().Column(right =>
                        {
                            right.Item().AlignRight().Text("COMPROBANTE DE PAGO")
                                .FontSize(15).Bold().FontColor(PrimaryColor);
                            right.Item().AlignRight().Text(data.ReceiptNumber).SemiBold();
                            right.Item().AlignRight().Text(
                                data.IssuedAtUtc.ToString("dd/MM/yyyy HH:mm 'UTC'", CultureInfo.InvariantCulture));
                        });
                    });

                    column.Item().PaddingTop(12).BorderBottom(2).BorderColor(PrimaryColor);
                });

                page.Content().PaddingTop(18).Column(column =>
                {
                    column.Spacing(14);

                    column.Item().Background(LightColor).Padding(12).Row(row =>
                    {
                        row.RelativeItem().Column(left =>
                        {
                            left.Item().Text("CLIENTE").FontSize(9).Bold().FontColor(PrimaryColor);
                            left.Item().Text(data.ClientName).SemiBold();
                            left.Item().Text($"Código: {data.ClientCode}");
                            if (!string.IsNullOrWhiteSpace(data.ClientTaxId))
                            {
                                left.Item().Text($"CUI/NIT: {data.ClientTaxId}");
                            }
                        });

                        row.RelativeItem().Column(right =>
                        {
                            right.Item().Text("PAGO").FontSize(9).Bold().FontColor(PrimaryColor);
                            right.Item().Text($"Pedido: {data.OrderCode}").SemiBold();
                            right.Item().Text($"Método: {data.PaymentTypeCode}");
                            right.Item().Text($"Proveedor: {data.Provider}");
                            right.Item().Text(
                                $"Fecha: {data.PaidAtUtc:dd/MM/yyyy HH:mm} UTC");
                        });
                    });

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(5);
                            columns.ConstantColumn(55);
                            columns.ConstantColumn(85);
                            columns.ConstantColumn(90);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCell).Text("Producto").Bold();
                            header.Cell().Element(HeaderCell).AlignCenter().Text("Cant.").Bold();
                            header.Cell().Element(HeaderCell).AlignRight().Text("Precio").Bold();
                            header.Cell().Element(HeaderCell).AlignRight().Text("Total").Bold();
                        });

                        foreach (var item in data.Items)
                        {
                            table.Cell().Element(BodyCell).Column(cell =>
                            {
                                cell.Item().Text(item.ProductName).SemiBold();
                                cell.Item().Text(item.ProductCode).FontSize(8).FontColor(Colors.Grey.Darken1);
                            });
                            table.Cell().Element(BodyCell).AlignCenter().Text(item.Quantity.ToString(CultureInfo.InvariantCulture));
                            table.Cell().Element(BodyCell).AlignRight().Text(Money(item.UnitPrice, data.Currency));
                            table.Cell().Element(BodyCell).AlignRight().Text(Money(item.LineTotal, data.Currency)).SemiBold();
                        }
                    });

                    column.Item().AlignRight().Width(260).Column(totals =>
                    {
                        totals.Spacing(5);
                        totals.Item().Row(row =>
                        {
                            row.RelativeItem().Text("Subtotal");
                            row.ConstantItem(105).AlignRight().Text(Money(data.Subtotal, data.Currency));
                        });
                        totals.Item().Row(row =>
                        {
                            row.RelativeItem().Text("Envío");
                            row.ConstantItem(105).AlignRight().Text(Money(data.DeliveryCost, data.Currency));
                        });
                        totals.Item().Row(row =>
                        {
                            row.RelativeItem().Text("Descuento");
                            row.ConstantItem(105).AlignRight().Text($"- {Money(data.Discount, data.Currency)}");
                        });
                        totals.Item().PaddingTop(5).BorderTop(1).BorderColor(BorderColor).Row(row =>
                        {
                            row.RelativeItem().Text("TOTAL PAGADO").Bold().FontColor(PrimaryColor);
                            row.ConstantItem(105).AlignRight().Text(Money(data.PaidAmount, data.Currency))
                                .FontSize(13).Bold().FontColor(PrimaryColor);
                        });

                        if (data.RefundedAmount > 0m)
                        {
                            totals.Item().Row(row =>
                            {
                                row.RelativeItem().Text("Reembolsado").FontColor(Colors.Orange.Darken2);
                                row.ConstantItem(105).AlignRight()
                                    .Text(Money(data.RefundedAmount, data.Currency))
                                    .FontColor(Colors.Orange.Darken2);
                            });
                        }
                    });

                    column.Item().PaddingTop(4).Column(details =>
                    {
                        details.Item().Text($"Identificador de pago: {data.PaymentId}").FontSize(9);
                        if (!string.IsNullOrWhiteSpace(data.ProviderReference))
                        {
                            details.Item().Text($"Referencia del proveedor: {data.ProviderReference}").FontSize(9);
                        }
                    });

                    column.Item().Background(Colors.Grey.Lighten4).Padding(10)
                        .Text("Este documento confirma la recepción del pago. No sustituye la factura fiscal emitida para el pedido.")
                        .FontSize(9).FontColor(Colors.Grey.Darken1);
                });

                page.Footer()
                    .DefaultTextStyle(style =>
                        style.FontSize(8).FontColor(Colors.Grey.Darken1))
                    .AlignCenter()
                    .Text(text =>
                    {
                        text.Span("BioRed · Comprobante generado electrónicamente · Página ");
                        text.CurrentPageNumber();
                    });
            });
        }).GeneratePdf();

    private static string Money(decimal amount, string currency) =>
        $"{currency} {amount.ToString("N2", CultureInfo.InvariantCulture)}";

    private static IContainer HeaderCell(IContainer container) =>
        container.Background(PrimaryColor).PaddingVertical(7).PaddingHorizontal(6)
            .DefaultTextStyle(style => style.FontColor(Colors.White));

    private static IContainer BodyCell(IContainer container) =>
        container.BorderBottom(1).BorderColor(BorderColor).PaddingVertical(7).PaddingHorizontal(6);
}
