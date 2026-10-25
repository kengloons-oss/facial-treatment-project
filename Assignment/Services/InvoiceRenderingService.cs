using QuestPDF.Fluent;
using QuestPDF.Helpers;

namespace Assignment.Services;

public class InvoiceRenderingService
{
    public InvoiceRenderingService()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    public byte[] GenerateInvoicePdf(Order order)
    {
        var document = Document.Create(Container =>
        {
            // first page
            Container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, QuestPDF.Infrastructure.Unit.Centimetre);

                // page header
                page.Header()
                    .Row(row =>
                    {
                        row.RelativeItem()
                            .Column(column =>
                            {
                                column.Item()
                                    .Text("OwO Meow Beauty")
                                    .ExtraBold()
                                    .FontSize(16);
                                column.Item()
                                    .Text("owomeowartgallery0101@gmail.com");
                            });

                        row.RelativeItem()
                            .ShowOnce()
                            .Text("Invoice")
                            .AlignRight()
                            .FontFamily("Arial")
                            .ExtraBlack()
                            .FontSize(30);
                    });

                // page content
                page.Content()
                    .Column(column =>
                    {
                        column.Item().Row(row =>
                        {
                            row.RelativeItem()
                                .Column(column2 =>
                                {
                                    column2.Item()
                                        .Text("Bill To:")
                                        .Bold();
                                    column2.Item()
                                        .Text(order.Member.Person.Name)
                                        .FontFamily("Arial")
                                        .FontSize(15)
                                        .Bold();
                                    column2.Item()
                                        .Text(order.Member.Person.Email);
                                    column2.Item()
                                        .Text("\n\n");
                                });

                            row.RelativeItem().Column(column2 =>
                            {
                                column2.Item()
                                    .Text($"Order #: {order.Code}")
                                    .AlignRight()
                                    .Bold();
                                column2.Item()
                                    .PaddingTop(2)
                                    .Text($"Date: {Helpers.FormatDate(order.Payment_date)}")
                                    .AlignRight();
                            });
                        });

                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(40); // No.
                                columns.RelativeColumn();   // Package Name
                                columns.ConstantColumn(50); // Quantity
                                columns.ConstantColumn(60); // Unit Price
                                columns.ConstantColumn(70); // Total Price
                            });

                            table.Header(header =>
                            {
                                header.Cell().Padding(4).Text("#").Bold();
                                header.Cell().Padding(4).Text("Package Name").Bold();
                                header.Cell().Padding(4).Text("Qty").AlignRight();
                                header.Cell().Padding(4).Text("Price (RM)").AlignRight();
                                header.Cell().Padding(4).Text("Total Price (RM)").AlignRight();

                                header.Cell()
                                    .ColumnSpan(5)
                                    .PaddingVertical(5)
                                    .BorderBottom(1)
                                    .BorderColor(Colors.Black);
                            });

                            for (var i = 0; i < order.OrderPackages.Count; i++)
                            {
                                var backgroundColor = i % 2 == 0 ?
                                    "#ffffff" :
                                    "#f0f0f0";

                                //var invoiceItem = invoiceItem.InvoiceItems[i];
                                table.Cell().Background(backgroundColor).Padding(4).Text((i + 1).ToString());
                                table.Cell().Background(backgroundColor).Padding(4).Text(order.OrderPackages[i].Package.Name);
                                table.Cell().Background(backgroundColor).Padding(4).Text("1").AlignRight();
                                table.Cell().Background(backgroundColor).Padding(4).Text(order.OrderPackages[i].Price.ToString("F2")).AlignRight();
                                table.Cell().Background(backgroundColor).Padding(4).Text(order.OrderPackages[i].Price.ToString("F2")).AlignRight();
                            }

                            table.Cell()
                                .ColumnSpan(5)
                                .PaddingVertical(5)
                                .BorderBottom(1)
                                .BorderColor(Colors.Black);

                            // subtotal
                            table.Cell()
                                .ColumnSpan(4)
                                .AlignRight()
                                .Text(txt =>
                                {
                                    txt.Span("Subtotal\t\t\t").Bold();
                                    txt.Span("RM ");
                                });

                            table.Cell().PaddingRight(4).Text(order.Subtotal.ToString("F2")).AlignRight();

                            // tax
                            table.Cell()
                                .ColumnSpan(4)
                                .AlignRight()
                                .Text(txt =>
                                {
                                    txt.Span("Tax (8%)\t\t\t").Bold();
                                    txt.Span("RM ");
                                });

                            table.Cell().PaddingRight(4).Text(order.Tax.ToString("F2")).AlignRight();

                            // discount
                            table.Cell()
                                .ColumnSpan(4)
                                .AlignRight()
                                .Text(txt =>
                                {
                                    txt.Span("Discount\t\t\t").Bold();
                                    txt.Span("RM ");
                                });

                            table.Cell().PaddingRight(4).Text(order.Discount.ToString("F2")).AlignRight();

                            // total amount
                            table.Cell()
                                .ColumnSpan(4)
                                .AlignRight()
                                .Text(txt =>
                                {
                                    txt.Span("Total Amount\t\t\t").Bold();
                                    txt.Span("RM ");
                                });

                            table.Cell().PaddingRight(4).Text(order.Amount.ToString("F2")).AlignRight();
                        });
                    });


                // page footer
                page.Footer()
                    .Column(column =>
                    {
                        column.Item()
                            .PaddingVertical(10)
                            .Text(text =>
                            {
                                text.Span("Page ");
                                text.CurrentPageNumber();
                                text.Span(" of ");
                                text.TotalPages();
                                text.AlignCenter();
                            });
                    });

            });
        });

        return document.GeneratePdf();
    }
}
