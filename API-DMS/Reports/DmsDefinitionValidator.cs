using Shared.Reporting;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace API_DMS.Reports
{
    public sealed class DmsReportDefinitionValidator
    {
        private static readonly HashSet<string> Alignments =
            new(StringComparer.Ordinal)
            {
            "left", "center", "right"
            };

        private static readonly HashSet<string> SortDirections =
            new(StringComparer.Ordinal)
            {
            "asc", "desc"
            };

        private static readonly HashSet<string> Aggregations =
            new(StringComparer.Ordinal)
            {
            "count", "sum", "avg"
            };

        private static readonly Regex ParameterNamePattern =
            new(
                "^[A-Za-z][A-Za-z0-9_]{0,49}$",
                RegexOptions.CultureInvariant);

        private static readonly Regex PlaceholderPattern =
            new(
                "\\{([A-Za-z][A-Za-z0-9_]*)\\}",
                RegexOptions.CultureInvariant);

        public IReadOnlyDictionary<string, string[]> Validate(
            string requestedDatasetKey,
            JsonElement definitionJson)
        {
            var errors = new Dictionary<string, List<string>>(
                StringComparer.Ordinal);

            void Add(string key, string message)
            {
                if (!errors.TryGetValue(key, out var messages))
                {
                    messages = [];
                    errors[key] = messages;
                }

                messages.Add(message);
            }

            ReportDefinitionDocument definition;

            try
            {
                definition = ReportJson.Parse(definitionJson);
            }
            catch (JsonException)
            {
                Add(
                    "definition",
                    "Definiția raportului nu respectă structura admisă.");

                return Finish(errors);
            }

            var dataset = DmsReportDatasets.Find(requestedDatasetKey);

            if (dataset is null)
            {
                Add(
                    "datasetKey",
                    "Setul de date nu este disponibil în API-DMS.");

                return Finish(errors);
            }

            if (!string.Equals(
                    definition.DatasetKey,
                    requestedDatasetKey,
                    StringComparison.Ordinal))
            {
                Add(
                    "definition.datasetKey",
                    "Setul de date din definiție trebuie să coincidă cu DatasetKey.");
            }

            if (definition.RenderMode is not ("table" or "record"))
            {
                Add(
                    "definition.renderMode",
                    "RenderMode trebuie să fie table sau record.");
            }

            var parameters = definition.Parameters ?? [];
            var parameterNames = new HashSet<string>(
                StringComparer.Ordinal);

            var allowedParameters = dataset.Parameters.ToDictionary(
                parameter => parameter.Name,
                StringComparer.Ordinal);

            for (var index = 0; index < parameters.Count; index++)
            {
                var parameter = parameters[index];
                var path = $"definition.parameters[{index}]";

                if (!ParameterNamePattern.IsMatch(parameter.Name))
                {
                    Add(
                        $"{path}.name",
                        "Numele parametrului este invalid.");
                }
                else if (!parameterNames.Add(parameter.Name))
                {
                    Add(
                        $"{path}.name",
                        "Numele parametrului trebuie să fie unic.");
                }

                if (string.IsNullOrWhiteSpace(parameter.Label) ||
                    parameter.Label.Length > 200)
                {
                    Add(
                        $"{path}.label",
                        "Eticheta parametrului este obligatorie și are maximum 200 caractere.");
                }

                if (!allowedParameters.TryGetValue(
                    parameter.Name,
                    out var allowedParameter))
                {
                    Add(
                        $"{path}.name",
                        "Parametrul nu poate fi aplicat acestui set de date.");
                }
                else
                {
                    if (!string.Equals(
                            parameter.Type,
                            allowedParameter.Type,
                            StringComparison.Ordinal))
                    {
                        Add(
                            $"{path}.type",
                            "Tipul parametrului nu corespunde contractului setului de date.");
                    }

                    if (!string.Equals(
                            parameter.Source,
                            allowedParameter.Source,
                            StringComparison.Ordinal))
                    {
                        Add(
                            $"{path}.source",
                            "Sursa parametrului nu corespunde contractului setului de date.");
                    }
                }
            }

            var fields = dataset.Fields.ToDictionary(
                field => field.Key,
                StringComparer.Ordinal);

            var columns = definition.Columns ?? [];

            if (columns.Count == 0)
            {
                Add(
                    "definition.columns",
                    "Raportul trebuie să conțină cel puțin o coloană.");
            }

            var selectedFields = new HashSet<string>(
                StringComparer.Ordinal);

            var totalWidth = 0;

            for (var index = 0; index < columns.Count; index++)
            {
                var column = columns[index];
                var path = $"definition.columns[{index}]";

                if (!fields.TryGetValue(column.Field, out var field))
                {
                    Add(
                        $"{path}.field",
                        "Câmpul nu este disponibil în setul de date ales.");
                }
                else
                {
                    if (!selectedFields.Add(column.Field))
                    {
                        Add(
                            $"{path}.field",
                            "Același câmp nu poate apărea de două ori.");
                    }

                    if (!string.Equals(
                            column.Type,
                            field.Type,
                            StringComparison.Ordinal))
                    {
                        Add(
                            $"{path}.type",
                            "Tipul coloanei trebuie să coincidă cu tipul câmpului.");
                    }
                }

                if (string.IsNullOrWhiteSpace(column.Label) ||
                    column.Label.Length > 200)
                {
                    Add(
                        $"{path}.label",
                        "Eticheta coloanei este obligatorie și are maximum 200 caractere.");
                }

                if (!string.IsNullOrWhiteSpace(column.Align) &&
                    !Alignments.Contains(column.Align))
                {
                    Add(
                        $"{path}.align",
                        "Alinierea trebuie să fie left, center sau right.");
                }

                if (definition.RenderMode == "table")
                {
                    if (column.WidthPct is < 1 or > 100)
                    {
                        Add(
                            $"{path}.widthPct",
                            "Lățimea trebuie să fie între 1 și 100.");
                    }

                    totalWidth += column.WidthPct;
                }
            }

            if (definition.RenderMode == "table" &&
                columns.Count > 0 &&
                totalWidth != 100)
            {
                Add(
                    "definition.columns",
                    "Suma lățimilor coloanelor trebuie să fie exact 100 %.");
            }

            ValidateSort(
                definition.Sort ?? [],
                fields,
                Add);

            ValidateGroup(
                definition.GroupBy,
                fields,
                Add);

            ValidateTotals(
                definition.Totals ?? [],
                fields,
                Add);

            if (definition.Layout is null)
            {
                Add(
                    "definition.layout",
                    "Layout este obligatoriu.");
            }
            else
            {
                ValidateLayout(
                    definition.Layout,
                    parameterNames,
                    Add);
            }

            return Finish(errors);
        }

        private static void ValidateSort(
            IReadOnlyList<ReportSortDefinition> sort,
            IReadOnlyDictionary<string, ReportFieldDescriptor> fields,
            Action<string, string> add)
        {
            foreach (var (item, index) in sort.Select(
                (item, index) => (item, index)))
            {
                var path = $"definition.sort[{index}]";

                if (!fields.ContainsKey(item.Field))
                {
                    add(
                        $"{path}.field",
                        "Câmpul de sortare nu există în setul de date.");
                }

                if (!SortDirections.Contains(item.Dir))
                {
                    add(
                        $"{path}.dir",
                        "Direcția de sortare trebuie să fie asc sau desc.");
                }
            }
        }

        private static void ValidateGroup(
            ReportGroupDefinition? group,
            IReadOnlyDictionary<string, ReportFieldDescriptor> fields,
            Action<string, string> add)
        {
            if (group is not null &&
                !fields.ContainsKey(group.Field))
            {
                add(
                    "definition.groupBy.field",
                    "Câmpul de grupare nu există în setul de date.");
            }
        }

        private static void ValidateTotals(
            IReadOnlyList<ReportTotalDefinition> totals,
            IReadOnlyDictionary<string, ReportFieldDescriptor> fields,
            Action<string, string> add)
        {
            foreach (var (item, index) in totals.Select(
                (item, index) => (item, index)))
            {
                var path = $"definition.totals[{index}]";

                if (!fields.TryGetValue(item.Field, out var field))
                {
                    add(
                        $"{path}.field",
                        "Câmpul totalului nu există în setul de date.");

                    continue;
                }

                if (!Aggregations.Contains(item.Agg))
                {
                    add(
                        $"{path}.agg",
                        "Agregarea trebuie să fie count, sum sau avg.");

                    continue;
                }

                if (item.Agg is "sum" or "avg" && !field.IsNumeric)
                {
                    add(
                        $"{path}.field",
                        "sum și avg se aplică numai câmpurilor numerice.");
                }
            }
        }

        private static void ValidateLayout(
            ReportLayoutDefinition layout,
            IReadOnlySet<string> parameterNames,
            Action<string, string> add)
        {
            if (layout.Orientation is not ("portrait" or "landscape"))
            {
                add(
                    "definition.layout.orientation",
                    "Orientarea trebuie să fie portrait sau landscape.");
            }

            if (string.IsNullOrWhiteSpace(layout.Title) ||
                layout.Title.Length > 200)
            {
                add(
                    "definition.layout.title",
                    "Titlul este obligatoriu și are maximum 200 caractere.");
            }

            if (layout.Subtitle?.Length > 500)
            {
                add(
                    "definition.layout.subtitle",
                    "Subtitlul are maximum 500 caractere.");
            }

            ValidatePlaceholders(
                layout.Title,
                "definition.layout.title",
                parameterNames,
                add);

            ValidatePlaceholders(
                layout.Subtitle,
                "definition.layout.subtitle",
                parameterNames,
                add);
        }

        private static void ValidatePlaceholders(
            string? text,
            string path,
            IReadOnlySet<string> parameterNames,
            Action<string, string> add)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            foreach (Match match in PlaceholderPattern.Matches(text))
            {
                var parameterName = match.Groups[1].Value;

                if (!parameterNames.Contains(parameterName))
                {
                    add(
                        path,
                        $"Placeholder-ul {{{parameterName}}} nu corespunde unui parametru declarat.");
                }
            }
        }

        private static IReadOnlyDictionary<string, string[]> Finish(
            IReadOnlyDictionary<string, List<string>> errors)
        {
            return errors.ToDictionary(
                item => item.Key,
                item => item.Value.ToArray(),
                StringComparer.Ordinal);
        }
    }
}
