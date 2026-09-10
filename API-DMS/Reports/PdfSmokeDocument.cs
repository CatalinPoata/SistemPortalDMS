using System.Net;
using System.Text;

namespace API_DMS.Reports
{
    public static class PdfSmokeDocument
    {
        public static string CreateHtml()
        {
            var rows = new StringBuilder();

            for (var number = 1; number <= 80; number++)
            {
                var subject = number == 80
                    ? "ULTIMUL RÂND - Șș Țț Ăă Ââ Îî."
                    : "Înregistrare cerere: adeverință, ștampilă și înștiințare.";

                rows.Append($"""
                <tr>
                    <td class="number">{number}</td>
                    <td>{WebUtility.HtmlEncode("Ștefan Țurcanu")}</td>
                    <td>{WebUtility.HtmlEncode(subject)}</td>
                </tr>
                """);
            }

            return $$"""
            <!DOCTYPE html>
            <html lang="ro">
            <head>
                <meta charset="utf-8">

                <meta http-equiv="Content-Security-Policy"
                      content="default-src 'none'; style-src 'unsafe-inline'">

                <title>Verificare PDF</title>

                <style>
                    @page {
                        size: A4 landscape;
                        margin: 15mm 12mm 18mm;
                    }

                    body {
                        margin: 0;
                        color: #0f172a;
                        font: 10pt "DejaVu Sans", sans-serif;
                    }

                    h1 {
                        font-size: 18pt;
                    }

                    table {
                        width: 100%;
                        border-collapse: collapse;
                        table-layout: fixed;
                    }

                    thead {
                        display: table-header-group;
                    }

                    tr {
                        break-inside: avoid;
                    }

                    th, td {
                        padding: 8px;
                        border: 1px solid #94a3b8;
                        vertical-align: top;
                        text-align: left;
                        overflow-wrap: anywhere;
                    }

                    th {
                        background: #e2e8f0;
                    }

                    .number {
                        text-align: center;
                    }
                </style>
            </head>
            <body>
                <h1>Verificare PDF - diacritice românești</h1>

                <p>Date fictive pentru verificarea infrastructurii.</p>

                <p>Caractere verificate: Șș Țț Ăă Ââ Îî.</p>

                <table>
                    <colgroup>
                        <col style="width:10%">
                        <col style="width:30%">
                        <col style="width:60%">
                    </colgroup>

                    <thead>
                        <tr>
                            <th class="number">Nr.</th>
                            <th>Solicitant</th>
                            <th>Obiectul lucrării și observații</th>
                        </tr>
                    </thead>

                    <tbody>{{rows}}</tbody>
                </table>
            </body>
            </html>
            """;
        }
    }
}
