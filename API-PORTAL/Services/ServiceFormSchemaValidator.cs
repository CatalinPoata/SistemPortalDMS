using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace API_PORTAL.Services
{
    public sealed class ServiceFormSchemaValidator
    {
        private static readonly Regex KeyPattern = new(
            "^[a-z][a-z0-9_]{0,49}$",
            RegexOptions.CultureInvariant);

        private static readonly HashSet<string> FieldTypes =
        [
            "text", "textarea", "number", "date", "boolean",
            "select", "radio", "checkboxes", "email", "phone",
            "nationalId", "file", "heading", "paragraph"
        ];

        private static readonly HashSet<string> ValueFieldTypes =
        [
            "text", "textarea", "number", "date", "boolean",
            "select", "radio", "checkboxes", "email", "phone",
            "nationalId", "file"
        ];

        public IReadOnlyDictionary<string, string[]>
            ValidateForPublication(JsonElement schema)
        {
            var errors = new Dictionary<string, List<string>>();

            if (schema.ValueKind != JsonValueKind.Object)
            {
                AddError(
                    errors,
                    "formSchema",
                    "Schema formularului trebuie să fie un obiect JSON.");

                return ToReadOnly(errors);
            }

            if (!schema.TryGetProperty("sections", out var sections) ||
                sections.ValueKind != JsonValueKind.Array)
            {
                AddError(
                    errors,
                    "formSchema.sections",
                    "Schema trebuie să conțină lista sections.");

                return ToReadOnly(errors);
            }

            if (sections.GetArrayLength() == 0)
            {
                AddError(
                    errors,
                    "formSchema.sections",
                    "Formularul publicat trebuie să conțină cel puțin o secțiune.");
            }

            var sectionKeys = new HashSet<string>(
                StringComparer.Ordinal);
            var valueFieldKeys = new HashSet<string>(
                StringComparer.Ordinal);
            var visibleWhenReferences = new List<
                (string Path, string ReferencedField)>();

            var sectionIndex = 0;

            foreach (var section in sections.EnumerateArray())
            {
                var sectionPath = $"formSchema.sections[{sectionIndex}]";
                sectionIndex++;

                if (section.ValueKind != JsonValueKind.Object)
                {
                    AddError(
                        errors,
                        sectionPath,
                        "Secțiunea trebuie să fie un obiect JSON.");

                    continue;
                }

                var sectionKey = ReadKey(
                    section,
                    "key",
                    sectionPath,
                    errors);

                if (sectionKey is not null &&
                    !sectionKeys.Add(sectionKey))
                {
                    AddError(
                        errors,
                        $"{sectionPath}.key",
                        "Cheia secțiunii trebuie să fie unică.");
                }

                ReadRequiredText(
                    section,
                    "title",
                    sectionPath,
                    200,
                    errors);

                if (!section.TryGetProperty("fields", out var fields) ||
                    fields.ValueKind != JsonValueKind.Array)
                {
                    AddError(
                        errors,
                        $"{sectionPath}.fields",
                        "Secțiunea trebuie să conțină lista fields.");

                    continue;
                }

                if (fields.GetArrayLength() == 0)
                {
                    AddError(
                        errors,
                        $"{sectionPath}.fields",
                        "Secțiunea trebuie să conțină cel puțin un câmp.");
                }

                var fieldIndex = 0;

                foreach (var field in fields.EnumerateArray())
                {
                    var fieldPath =
                        $"{sectionPath}.fields[{fieldIndex}]";
                    fieldIndex++;

                    ValidateField(
                        field,
                        fieldPath,
                        valueFieldKeys,
                        visibleWhenReferences,
                        errors);
                }
            }

            foreach (var reference in visibleWhenReferences)
            {
                if (!valueFieldKeys.Contains(reference.ReferencedField))
                {
                    AddError(
                        errors,
                        $"{reference.Path}.field",
                        "Câmpul din visibleWhen nu există sau nu are valoare.");
                }
            }

            return ToReadOnly(errors);
        }

        private static void ValidateField(
            JsonElement field,
            string path,
            ISet<string> valueFieldKeys,
            ICollection<(string Path, string ReferencedField)>
                visibleWhenReferences,
            IDictionary<string, List<string>> errors)
        {
            if (field.ValueKind != JsonValueKind.Object)
            {
                AddError(
                    errors,
                    path,
                    "Câmpul trebuie să fie un obiect JSON.");

                return;
            }

            var key = ReadKey(field, "key", path, errors);
            var type = ReadRequiredText(field, "type", path, 30, errors);

            ReadRequiredText(field, "label", path, 200, errors);
            ValidateRequiredBoolean(field, "required", path, errors);
            ValidateVisibleWhen(
                field,
                path,
                key,
                visibleWhenReferences,
                errors);

            if (type is null || !FieldTypes.Contains(type))
            {
                if (type is not null)
                {
                    AddError(
                        errors,
                        $"{path}.type",
                        "Tipul câmpului nu este acceptat.");
                }

                return;
            }

            if (ValueFieldTypes.Contains(type) && key is not null &&
                !valueFieldKeys.Add(key))
            {
                AddError(
                    errors,
                    $"{path}.key",
                    "Cheia câmpului trebuie să fie unică în formular.");
            }

            switch (type)
            {
                case "text":
                case "textarea":
                    ValidatePositiveInteger(
                        field,
                        "maxLength",
                        path,
                        errors);
                    ValidatePositiveInteger(
                        field,
                        "minLength",
                        path,
                        errors);

                    if (type == "text")
                    {
                        ValidatePattern(field, path, errors);
                    }
                    break;

                case "number":
                    ValidateNumberRange(field, path, errors);
                    ValidateBoolean(field, "integer", path, errors);
                    break;

                case "date":
                    ValidateDateRange(field, path, errors);
                    ValidateBoolean(field, "notInFuture", path, errors);
                    break;

                case "select":
                case "radio":
                case "checkboxes":
                    ValidateOptions(field, path, errors);

                    if (type == "checkboxes")
                    {
                        ValidatePositiveInteger(
                            field,
                            "minSelected",
                            path,
                            errors,
                            allowZero: true);
                        ValidatePositiveInteger(
                            field,
                            "maxSelected",
                            path,
                            errors,
                            allowZero: true);
                    }

                    break;

                case "file":
                    ValidatePositiveInteger(
                        field,
                        "maxSizeMb",
                        path,
                        errors);
                    break;
            }
        }

        private static void ValidateVisibleWhen(
            JsonElement field,
            string path,
            string? key,
            ICollection<(string Path, string ReferencedField)>
                references,
            IDictionary<string, List<string>> errors)
        {
            if (!field.TryGetProperty("visibleWhen", out var visibleWhen) ||
                visibleWhen.ValueKind == JsonValueKind.Null)
            {
                return;
            }

            if (visibleWhen.ValueKind != JsonValueKind.Object)
            {
                AddError(
                    errors,
                    $"{path}.visibleWhen",
                    "visibleWhen trebuie să fie null sau un obiect.");

                return;
            }

            var reference = ReadRequiredText(
                visibleWhen,
                "field",
                $"{path}.visibleWhen",
                50,
                errors);

            if (!visibleWhen.TryGetProperty("equals", out var equals) ||
                equals.ValueKind is JsonValueKind.Array or
                    JsonValueKind.Object or
                    JsonValueKind.Undefined)
            {
                AddError(
                    errors,
                    $"{path}.visibleWhen.equals",
                    "visibleWhen.equals trebuie să fie o valoare scalară.");
            }

            if (reference is not null)
            {
                if (reference == key)
                {
                    AddError(
                        errors,
                        $"{path}.visibleWhen.field",
                        "Un câmp nu se poate condiționa de propria valoare.");
                }
                else
                {
                    references.Add(($"{path}.visibleWhen", reference));
                }
            }
        }

        private static void ValidateOptions(
            JsonElement field,
            string path,
            IDictionary<string, List<string>> errors)
        {
            if (!field.TryGetProperty("options", out var options) ||
                options.ValueKind != JsonValueKind.Array ||
                options.GetArrayLength() == 0)
            {
                AddError(
                    errors,
                    $"{path}.options",
                    "Câmpul trebuie să aibă cel puțin o opțiune.");

                return;
            }

            var values = new HashSet<string>(StringComparer.Ordinal);
            var optionIndex = 0;

            foreach (var option in options.EnumerateArray())
            {
                var optionPath = $"{path}.options[{optionIndex}]";
                optionIndex++;

                if (option.ValueKind != JsonValueKind.Object)
                {
                    AddError(
                        errors,
                        optionPath,
                        "Opțiunea trebuie să fie un obiect JSON.");

                    continue;
                }

                var value = ReadRequiredText(
                    option,
                    "value",
                    optionPath,
                    200,
                    errors);

                ReadRequiredText(
                    option,
                    "label",
                    optionPath,
                    200,
                    errors);

                if (value is not null && !values.Add(value))
                {
                    AddError(
                        errors,
                        $"{optionPath}.value",
                        "Valoarea opțiunii trebuie să fie unică.");
                }
            }
        }

        private static void ValidateNumberRange(
            JsonElement field,
            string path,
            IDictionary<string, List<string>> errors)
        {
            var minimum = ReadNumber(field, "min", path, errors);
            var maximum = ReadNumber(field, "max", path, errors);

            if (minimum.HasValue && maximum.HasValue &&
                minimum > maximum)
            {
                AddError(
                    errors,
                    path,
                    "Valoarea min nu poate fi mai mare decât max.");
            }
        }

        private static void ValidateDateRange(
            JsonElement field,
            string path,
            IDictionary<string, List<string>> errors)
        {
            var minimum = ReadDate(field, "minDate", path, errors);
            var maximum = ReadDate(field, "maxDate", path, errors);

            if (minimum.HasValue && maximum.HasValue &&
                minimum > maximum)
            {
                AddError(
                    errors,
                    path,
                    "minDate nu poate fi după maxDate.");
            }
        }

        private static void ValidatePositiveInteger(
            JsonElement value,
            string property,
            string path,
            IDictionary<string, List<string>> errors,
            bool allowZero = false)
        {
            if (!value.TryGetProperty(property, out var number) ||
                number.ValueKind == JsonValueKind.Null)
            {
                return;
            }

            if (!number.TryGetInt32(out var integer) ||
                (allowZero ? integer < 0 : integer <= 0))
            {
                AddError(
                    errors,
                    $"{path}.{property}",
                    allowZero
                        ? "Valoarea trebuie să fie un număr întreg pozitiv sau zero."
                        : "Valoarea trebuie să fie un număr întreg pozitiv.");
            }
        }

        private static void ValidatePattern(
            JsonElement field,
            string path,
            IDictionary<string, List<string>> errors)
        {
            if (!field.TryGetProperty("pattern", out var pattern) ||
                pattern.ValueKind == JsonValueKind.Null)
            {
                return;
            }

            if (pattern.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(pattern.GetString()))
            {
                AddError(
                    errors,
                    $"{path}.pattern",
                    "Expresia regulată trebuie să fie un șir nevid.");

                return;
            }

            try
            {
                _ = new Regex(
                    pattern.GetString()!,
                    RegexOptions.CultureInvariant,
                    TimeSpan.FromMilliseconds(100));
            }
            catch (ArgumentException)
            {
                AddError(
                    errors,
                    $"{path}.pattern",
                    "Expresia regulată nu este validă.");
            }
        }

        private static void ValidateBoolean(
            JsonElement value,
            string property,
            string path,
            IDictionary<string, List<string>> errors)
        {
            if (value.TryGetProperty(property, out var boolean) &&
                boolean.ValueKind is not JsonValueKind.True and
                    not JsonValueKind.False and
                    not JsonValueKind.Null)
            {
                AddError(
                    errors,
                    $"{path}.{property}",
                    "Valoarea trebuie să fie booleană.");
            }
        }

        private static void ValidateRequiredBoolean(
            JsonElement value,
            string property,
            string path,
            IDictionary<string, List<string>> errors)
        {
            if (!value.TryGetProperty(property, out var boolean) ||
                boolean.ValueKind is not JsonValueKind.True and
                    not JsonValueKind.False)
            {
                AddError(
                    errors,
                    $"{path}.{property}",
                    "Valoarea booleană este obligatorie.");
            }
        }

        private static decimal? ReadNumber(
            JsonElement value,
            string property,
            string path,
            IDictionary<string, List<string>> errors)
        {
            if (!value.TryGetProperty(property, out var number) ||
                number.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (!number.TryGetDecimal(out var result))
            {
                AddError(
                    errors,
                    $"{path}.{property}",
                    "Valoarea trebuie să fie numerică.");

                return null;
            }

            return result;
        }

        private static DateOnly? ReadDate(
            JsonElement value,
            string property,
            string path,
            IDictionary<string, List<string>> errors)
        {
            if (!value.TryGetProperty(property, out var date) ||
                date.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (date.ValueKind != JsonValueKind.String ||
                !DateOnly.TryParseExact(
                    date.GetString(),
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var result))
            {
                AddError(
                    errors,
                    $"{path}.{property}",
                    "Data trebuie să aibă formatul AAAA-LL-ZZ.");

                return null;
            }

            return result;
        }

        private static string? ReadKey(
            JsonElement value,
            string property,
            string path,
            IDictionary<string, List<string>> errors)
        {
            var key = ReadRequiredText(value, property, path, 50, errors);

            if (key is not null && !KeyPattern.IsMatch(key))
            {
                AddError(
                    errors,
                    $"{path}.{property}",
                    "Cheia poate conține litere mici, cifre și _ și trebuie să înceapă cu literă.");
            }

            return key;
        }

        private static string? ReadRequiredText(
            JsonElement value,
            string property,
            string path,
            int maximumLength,
            IDictionary<string, List<string>> errors)
        {
            if (!value.TryGetProperty(property, out var text) ||
                text.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(text.GetString()))
            {
                AddError(
                    errors,
                    $"{path}.{property}",
                    "Valoarea este obligatorie.");

                return null;
            }

            var result = text.GetString()!.Trim();

            if (result.Length > maximumLength)
            {
                AddError(
                    errors,
                    $"{path}.{property}",
                    $"Valoarea poate avea cel mult {maximumLength} caractere.");
            }

            return result;
        }

        private static void AddError(
            IDictionary<string, List<string>> errors,
            string key,
            string message)
        {
            if (!errors.TryGetValue(key, out var messages))
            {
                messages = [];
                errors[key] = messages;
            }

            messages.Add(message);
        }

        private static IReadOnlyDictionary<string, string[]> ToReadOnly(
            IReadOnlyDictionary<string, List<string>> errors)
        {
            return errors.ToDictionary(
                item => item.Key,
                item => item.Value.ToArray());
        }
    }
}
