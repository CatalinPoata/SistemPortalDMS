using System.Globalization;
using System.Net.Mail;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace API_PORTAL.Services
{
    public sealed class ServiceFormValueValidator
    {
        private static readonly HashSet<string> ValueFieldTypes =
        [
            "text", "textarea", "number", "date", "boolean",
            "select", "radio", "checkboxes", "email", "phone",
            "nationalId", "file"
        ];

        private static readonly Regex PhoneCharacters = new(
            @"^\+?[0-9 ()-]+$",
            RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));

        private const string CnpWeights = "279146358279";

        public IReadOnlyDictionary<string, string[]> Validate(
            JsonElement schema,
            JsonElement values)
        {
            var errors = new Dictionary<string, List<string>>(
                StringComparer.Ordinal);

            if (values.ValueKind != JsonValueKind.Object)
            {
                AddError(
                    errors,
                    "values",
                    "Valorile formularului trebuie să fie un obiect JSON.");

                return ToReadOnly(errors);
            }

            var fields = ReadValueFields(schema);
            var knownKeys = fields
                .Select(field => field.GetProperty("key").GetString()!)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var property in values.EnumerateObject())
            {
                if (!knownKeys.Contains(property.Name))
                {
                    AddError(
                        errors,
                        property.Name,
                        "Câmpul nu există în schema formularului.");
                }
            }

            foreach (var field in fields)
            {
                ValidateField(field, values, errors);
            }

            return ToReadOnly(errors);
        }

        private static IReadOnlyList<JsonElement> ReadValueFields(
            JsonElement schema)
        {
            var result = new List<JsonElement>();

            if (schema.ValueKind != JsonValueKind.Object ||
                !schema.TryGetProperty("sections", out var sections) ||
                sections.ValueKind != JsonValueKind.Array)
            {
                return result;
            }

            foreach (var section in sections.EnumerateArray())
            {
                if (section.ValueKind != JsonValueKind.Object ||
                    !section.TryGetProperty("fields", out var fields) ||
                    fields.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var field in fields.EnumerateArray())
                {
                    if (field.ValueKind == JsonValueKind.Object &&
                        field.TryGetProperty("key", out var key) &&
                        key.ValueKind == JsonValueKind.String &&
                        field.TryGetProperty("type", out var type) &&
                        type.ValueKind == JsonValueKind.String &&
                        ValueFieldTypes.Contains(type.GetString()!))
                    {
                        result.Add(field);
                    }
                }
            }

            return result;
        }

        private static void ValidateField(
            JsonElement field,
            JsonElement values,
            IDictionary<string, List<string>> errors)
        {
            var key = field.GetProperty("key").GetString()!;

            if (!IsVisible(field, values))
            {
                return;
            }

            var required = field.TryGetProperty("required", out var requiredElement) &&
                requiredElement.ValueKind == JsonValueKind.True;
            var hasValue = values.TryGetProperty(key, out var value) &&
                value.ValueKind is not JsonValueKind.Null and
                    not JsonValueKind.Undefined;

            if (!hasValue)
            {
                if (required)
                {
                    AddError(errors, key, "Câmpul este obligatoriu.");
                }

                return;
            }

            var type = field.GetProperty("type").GetString()!;

            switch (type)
            {
                case "text":
                case "textarea":
                    ValidateText(field, value, key, required, errors);
                    break;

                case "email":
                    ValidateEmail(value, key, required, errors);
                    break;

                case "phone":
                    ValidatePhone(value, key, required, errors);
                    break;

                case "nationalId":
                    ValidateNationalId(value, key, required, errors);
                    break;

                case "number":
                    ValidateNumber(field, value, key, errors);
                    break;

                case "date":
                    ValidateDate(field, value, key, required, errors);
                    break;

                case "boolean":
                    ValidateBoolean(value, key, required, errors);
                    break;

                case "select":
                case "radio":
                    ValidateSingleOption(field, value, key, required, errors);
                    break;

                case "checkboxes":
                    ValidateMultipleOptions(field, value, key, required, errors);
                    break;

                case "file":
                    ValidateFileReference(value, key, errors);
                    break;
            }
        }

        private static bool IsVisible(
            JsonElement field,
            JsonElement values)
        {
            if (!field.TryGetProperty("visibleWhen", out var condition) ||
                condition.ValueKind == JsonValueKind.Null)
            {
                return true;
            }

            var controllingKey = condition.GetProperty("field").GetString()!;

            return values.TryGetProperty(controllingKey, out var actual) &&
                condition.TryGetProperty("equals", out var expected) &&
                ScalarEquals(actual, expected);
        }

        private static bool ScalarEquals(
            JsonElement actual,
            JsonElement expected)
        {
            if (actual.ValueKind != expected.ValueKind)
            {
                return false;
            }

            return actual.ValueKind switch
            {
                JsonValueKind.String => actual.GetString() == expected.GetString(),
                JsonValueKind.Number =>
                    actual.TryGetDecimal(out var actualNumber) &&
                    expected.TryGetDecimal(out var expectedNumber) &&
                    actualNumber == expectedNumber,
                JsonValueKind.True or JsonValueKind.False =>
                    actual.GetBoolean() == expected.GetBoolean(),
                JsonValueKind.Null => true,
                _ => false
            };
        }

        private static void ValidateText(
            JsonElement field,
            JsonElement value,
            string key,
            bool required,
            IDictionary<string, List<string>> errors)
        {
            if (!TryReadString(value, key, errors, out var text))
            {
                return;
            }

            if (required && string.IsNullOrWhiteSpace(text))
            {
                AddError(errors, key, "Câmpul este obligatoriu.");
                return;
            }

            if (TryReadInt(field, "minLength", out var minimum) &&
                text.Length < minimum)
            {
                AddError(
                    errors,
                    key,
                    $"Valoarea trebuie să aibă cel puțin {minimum} caractere.");
            }

            if (TryReadInt(field, "maxLength", out var maximum) &&
                text.Length > maximum)
            {
                AddError(
                    errors,
                    key,
                    $"Valoarea poate avea cel mult {maximum} caractere.");
            }

            if (field.TryGetProperty("pattern", out var pattern) &&
                pattern.ValueKind == JsonValueKind.String &&
                !Regex.IsMatch(
                    text,
                    pattern.GetString()!,
                    RegexOptions.CultureInvariant,
                    TimeSpan.FromMilliseconds(100)))
            {
                AddError(errors, key, "Valoarea nu respectă formatul cerut.");
            }
        }

        private static void ValidateEmail(
            JsonElement value,
            string key,
            bool required,
            IDictionary<string, List<string>> errors)
        {
            if (!TryReadString(value, key, errors, out var text))
            {
                return;
            }

            if (required && string.IsNullOrWhiteSpace(text))
            {
                AddError(errors, key, "Câmpul este obligatoriu.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(text) &&
                (!MailAddress.TryCreate(text, out var address) ||
                 !string.Equals(
                     address.Address,
                     text,
                     StringComparison.OrdinalIgnoreCase)))
            {
                AddError(errors, key, "Adresa de e-mail nu este validă.");
            }
        }

        private static void ValidatePhone(
            JsonElement value,
            string key,
            bool required,
            IDictionary<string, List<string>> errors)
        {
            if (!TryReadString(value, key, errors, out var text))
            {
                return;
            }

            if (required && string.IsNullOrWhiteSpace(text))
            {
                AddError(errors, key, "Câmpul este obligatoriu.");
                return;
            }

            var digitCount = text.Count(char.IsDigit);

            if (!string.IsNullOrWhiteSpace(text) &&
                (!PhoneCharacters.IsMatch(text) ||
                 digitCount is < 7 or > 15))
            {
                AddError(errors, key, "Numărul de telefon nu este valid.");
            }
        }

        private static void ValidateNationalId(
            JsonElement value,
            string key,
            bool required,
            IDictionary<string, List<string>> errors)
        {
            if (!TryReadString(value, key, errors, out var text))
            {
                return;
            }

            if (required && string.IsNullOrWhiteSpace(text))
            {
                AddError(errors, key, "Câmpul este obligatoriu.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(text) && !HasValidCnpChecksum(text))
            {
                AddError(errors, key, "CNP-ul nu este valid.");
            }
        }

        private static bool HasValidCnpChecksum(string value)
        {
            if (value.Length != 13 || value.Any(character => !char.IsDigit(character)))
            {
                return false;
            }

            var sum = 0;

            for (var index = 0; index < CnpWeights.Length; index++)
            {
                sum += (value[index] - '0') * (CnpWeights[index] - '0');
            }

            var expected = sum % 11;

            if (expected == 10)
            {
                expected = 1;
            }

            return value[12] - '0' == expected;
        }

        private static void ValidateNumber(
            JsonElement field,
            JsonElement value,
            string key,
            IDictionary<string, List<string>> errors)
        {
            if (value.ValueKind != JsonValueKind.Number ||
                !value.TryGetDecimal(out var number))
            {
                AddError(errors, key, "Valoarea trebuie să fie numerică.");
                return;
            }

            if (TryReadDecimal(field, "min", out var minimum) &&
                number < minimum)
            {
                AddError(errors, key, $"Valoarea minimă este {minimum}.");
            }

            if (TryReadDecimal(field, "max", out var maximum) &&
                number > maximum)
            {
                AddError(errors, key, $"Valoarea maximă este {maximum}.");
            }

            if (field.TryGetProperty("integer", out var integer) &&
                integer.ValueKind == JsonValueKind.True &&
                decimal.Truncate(number) != number)
            {
                AddError(errors, key, "Valoarea trebuie să fie un număr întreg.");
            }
        }

        private static void ValidateDate(
            JsonElement field,
            JsonElement value,
            string key,
            bool required,
            IDictionary<string, List<string>> errors)
        {
            if (!TryReadString(value, key, errors, out var text))
            {
                return;
            }

            if (required && string.IsNullOrWhiteSpace(text))
            {
                AddError(errors, key, "Câmpul este obligatoriu.");
                return;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            if (!DateOnly.TryParseExact(
                    text,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var date))
            {
                AddError(errors, key, "Data trebuie să aibă formatul AAAA-LL-ZZ.");
                return;
            }

            if (TryReadDate(field, "minDate", out var minimum) &&
                date < minimum)
            {
                AddError(errors, key, $"Data minimă este {minimum:yyyy-MM-dd}.");
            }

            if (TryReadDate(field, "maxDate", out var maximum) &&
                date > maximum)
            {
                AddError(errors, key, $"Data maximă este {maximum:yyyy-MM-dd}.");
            }

            if (field.TryGetProperty("notInFuture", out var notInFuture) &&
                notInFuture.ValueKind == JsonValueKind.True &&
                date > DateOnly.FromDateTime(DateTime.UtcNow))
            {
                AddError(errors, key, "Data nu poate fi în viitor.");
            }
        }

        private static void ValidateBoolean(
            JsonElement value,
            string key,
            bool required,
            IDictionary<string, List<string>> errors)
        {
            if (value.ValueKind is not JsonValueKind.True and
                not JsonValueKind.False)
            {
                AddError(errors, key, "Valoarea trebuie să fie booleană.");
            }
            else if (required && !value.GetBoolean())
            {
                AddError(errors, key, "Câmpul trebuie bifat.");
            }
        }

        private static void ValidateSingleOption(
            JsonElement field,
            JsonElement value,
            string key,
            bool required,
            IDictionary<string, List<string>> errors)
        {
            if (!TryReadString(value, key, errors, out var text))
            {
                return;
            }

            if (required && string.IsNullOrWhiteSpace(text))
            {
                AddError(errors, key, "Câmpul este obligatoriu.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(text) &&
                !ReadOptions(field).Contains(text))
            {
                AddError(errors, key, "Opțiunea selectată nu este validă.");
            }
        }

        private static void ValidateMultipleOptions(
            JsonElement field,
            JsonElement value,
            string key,
            bool required,
            IDictionary<string, List<string>> errors)
        {
            if (value.ValueKind != JsonValueKind.Array)
            {
                AddError(errors, key, "Valoarea trebuie să fie o listă de opțiuni.");
                return;
            }

            var selected = new List<string>();

            foreach (var item in value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String)
                {
                    AddError(errors, key, "Fiecare opțiune trebuie să fie un șir.");
                    return;
                }

                selected.Add(item.GetString()!);
            }

            if (selected.Count != selected.Distinct(StringComparer.Ordinal).Count())
            {
                AddError(errors, key, "Aceeași opțiune nu poate fi selectată de două ori.");
            }

            if (required && selected.Count == 0)
            {
                AddError(errors, key, "Selectează cel puțin o opțiune.");
            }

            if (TryReadInt(field, "minSelected", out var minimum) &&
                selected.Count < minimum)
            {
                AddError(errors, key, $"Selectează cel puțin {minimum} opțiuni.");
            }

            if (TryReadInt(field, "maxSelected", out var maximum) &&
                selected.Count > maximum)
            {
                AddError(errors, key, $"Poți selecta cel mult {maximum} opțiuni.");
            }

            var options = ReadOptions(field);

            if (selected.Any(item => !options.Contains(item)))
            {
                AddError(errors, key, "Lista conține o opțiune invalidă.");
            }
        }

        private static void ValidateFileReference(
            JsonElement value,
            string key,
            IDictionary<string, List<string>> errors)
        {
            if (value.ValueKind != JsonValueKind.Object ||
                !value.TryGetProperty("fileId", out var fileId) ||
                fileId.ValueKind != JsonValueKind.String ||
                !Guid.TryParse(fileId.GetString(), out _))
            {
                AddError(
                    errors,
                    key,
                    "Fișierul trebuie să conțină un fileId valid.");
            }
        }

        private static HashSet<string> ReadOptions(JsonElement field)
        {
            if (!field.TryGetProperty("options", out var options) ||
                options.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return options
                .EnumerateArray()
                .Where(option =>
                    option.ValueKind == JsonValueKind.Object &&
                    option.TryGetProperty("value", out var value) &&
                    value.ValueKind == JsonValueKind.String)
                .Select(option => option.GetProperty("value").GetString()!)
                .ToHashSet(StringComparer.Ordinal);
        }

        private static bool TryReadString(
            JsonElement value,
            string key,
            IDictionary<string, List<string>> errors,
            out string text)
        {
            if (value.ValueKind != JsonValueKind.String)
            {
                AddError(errors, key, "Valoarea trebuie să fie un șir.");
                text = string.Empty;
                return false;
            }

            text = value.GetString()!;
            return true;
        }

        private static bool TryReadInt(
            JsonElement value,
            string property,
            out int result)
        {
            result = 0;

            return value.TryGetProperty(property, out var element) &&
                element.TryGetInt32(out result);
        }

        private static bool TryReadDecimal(
            JsonElement value,
            string property,
            out decimal result)
        {
            result = 0;

            return value.TryGetProperty(property, out var element) &&
                element.TryGetDecimal(out result);
        }

        private static bool TryReadDate(
            JsonElement value,
            string property,
            out DateOnly result)
        {
            result = default;

            return value.TryGetProperty(property, out var element) &&
                element.ValueKind == JsonValueKind.String &&
                DateOnly.TryParseExact(
                    element.GetString(),
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out result);
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
