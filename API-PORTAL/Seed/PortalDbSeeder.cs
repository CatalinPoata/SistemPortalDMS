using API_PORTAL.Data;
using API_PORTAL.Entities;
using API_PORTAL.Entities.Base;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using API_PORTAL.Storage;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace API_PORTAL.Seed
{
    public static class PortalDbSeeder
    {
        public static async Task SeedAsync(
            IServiceProvider services,
            CancellationToken cancellationToken = default)
        {
            var db = services.GetRequiredService<PortalDbContext>();
            var passwordHasher =
                services.GetRequiredService<IPasswordHasher<User>>();

            await AddUserIfMissing(
                db,
                passwordHasher,
                "admin@portal.local",
                "Portal Admin",
                Role.Admin,
                "Admin123!ChangeMe",
                cancellationToken);

            await AddUserIfMissing(
                db,
                passwordHasher,
                "citizen@portal.local",
                "Portal Citizen",
                Role.Citizen,
                "Citizen123!ChangeMe",
                cancellationToken);

            await SeedServicesAsync(db, cancellationToken);
            await SeedArticlesAsync(db, cancellationToken);
            await SeedSurveyAsync(db, cancellationToken);
            await SeedAppointmentTypeAsync(db, cancellationToken);
            await SeedSubmissionsAsync(db, cancellationToken);
            await SeedPublicRegistryAsync(services, db, cancellationToken);
        }

        private static async Task SeedServicesAsync(
            PortalDbContext db,
            CancellationToken cancellationToken)
        {
            await AddServiceIfMissing(
                db,
                "depunere-solicitare",
                "Depunere solicitare",
                "Depune o solicitare electronică.",
                "REG-GEN",
                """
                {"sections":[{"key":"solicitant","title":"Date solicitant","fields":[{"key":"nume","label":"Nume complet","type":"text","required":true,"maxLength":200},{"key":"email","label":"E-mail","type":"email","required":true}]}]}
                """,
                true,
                3,
                1,
                cancellationToken);

            await AddServiceIfMissing(
                db,
                "cerere-informatie",
                "Cerere de informații",
                "Solicită informații de interes public.",
                "REG-GEN",
                """
                {"sections":[{"key":"cerere","title":"Cerere","fields":[{"key":"subiect","label":"Subiect","type":"text","required":true,"maxLength":500},{"key":"detalii","label":"Detalii","type":"textarea","required":true,"maxLength":5000}]}]}
                """,
                false,
                0,
                2,
                cancellationToken);

            await AddServiceIfMissing(
                db,
                "solicitare-certificat",
                "Solicitare certificat",
                "Solicită eliberarea unui certificat.",
                "REG-OUT",
                """
                {"sections":[{"key":"document","title":"Document solicitat","fields":[{"key":"tip","label":"Tip certificat","type":"select","required":true,"options":["Fiscal","Urbanism","Altele"]},{"key":"observatii","label":"Observații","type":"textarea","required":false,"maxLength":2000}]}]}
                """,
                true,
                2,
                3,
                cancellationToken);
        }

        private static async Task AddServiceIfMissing(
            PortalDbContext db,
            string code,
            string title,
            string description,
            string registryTypeCode,
            string schema,
            bool requiresAttachment,
            int maxAttachments,
            int displayOrder,
            CancellationToken cancellationToken)
        {
            if (await db.ServiceDefinitions.AnyAsync(
                    item => item.code == code,
                    cancellationToken))
            {
                return;
            }

            db.ServiceDefinitions.Add(new ServiceDefinition
            {
                id = Guid.NewGuid(),
                code = code,
                title = title,
                short_description = description,
                description = description,
                registry_type_code = registryTypeCode,
                form_schema = JsonDocument.Parse(schema),
                requires_attachment = requiresAttachment,
                max_attachments = maxAttachments,
                is_published = true,
                display_order = displayOrder
            });

            await db.SaveChangesAsync(cancellationToken);
        }

        private static async Task SeedArticlesAsync(
            PortalDbContext db,
            CancellationToken cancellationToken)
        {
            await AddArticleIfMissing(
                db,
                "program-lucru",
                "Programul de lucru cu publicul",
                "Informații despre program și modalități de contact.",
                "<p>Programul de lucru este de luni până vineri, între 08:00 și 16:00.</p>",
                cancellationToken);

            await AddArticleIfMissing(
                db,
                "depunere-online",
                "Cum depui o cerere online",
                "Pașii necesari pentru depunerea unei cereri.",
                "<p>Alege serviciul, completează formularul și urmărește starea cererii din contul tău.</p>",
                cancellationToken);
        }

        private static async Task AddArticleIfMissing(
            PortalDbContext db,
            string slug,
            string title,
            string summary,
            string body,
            CancellationToken cancellationToken)
        {
            if (await db.Articles.AnyAsync(
                    item => item.slug == slug,
                    cancellationToken))
            {
                return;
            }

            db.Articles.Add(new Article
            {
                id = Guid.NewGuid(),
                slug = slug,
                title = title,
                summary = summary,
                body = body,
                is_published = true,
                published_at = DateTimeOffset.UtcNow
            });

            await db.SaveChangesAsync(cancellationToken);
        }

        private static async Task SeedSurveyAsync(
            PortalDbContext db,
            CancellationToken cancellationToken)
        {
            var survey = await db.Surveys.SingleOrDefaultAsync(
                item => item.code == "satisfactie-portal",
                cancellationToken);

            if (survey is null)
            {
                survey = new Survey
                {
                    id = Guid.NewGuid(),
                    code = "satisfactie-portal",
                    title = "Satisfacția utilizatorilor portalului",
                    description = "Ajută-ne să îmbunătățim serviciile online.",
                    starts_at = DateTimeOffset.UtcNow.AddDays(-1),
                    ends_at = DateTimeOffset.UtcNow.AddDays(30),
                    allow_anonymous = true,
                    show_results = true,
                    is_published = true
                };

                db.Surveys.Add(survey);
                await db.SaveChangesAsync(cancellationToken);
            }

            if (!await db.SurveyQuestions.AnyAsync(
                    item => item.survey_id == survey.id,
                    cancellationToken))
            {
                db.SurveyQuestions.AddRange(
                    new SurveyQuestion
                    {
                        id = Guid.NewGuid(),
                        survey_id = survey.id,
                        key = "rating",
                        text = "Cât de mulțumit ești de portal?",
                        type = SurveyQuestionType.Rating,
                        is_required = true,
                        display_order = 1
                    },
                    new SurveyQuestion
                    {
                        id = Guid.NewGuid(),
                        survey_id = survey.id,
                        key = "feedback",
                        text = "Ce am putea îmbunătăți?",
                        type = SurveyQuestionType.FreeText,
                        is_required = false,
                        display_order = 2
                    });

                await db.SaveChangesAsync(cancellationToken);
            }

            if (!await db.SurveyResponses.AnyAsync(
                    item => item.survey_id == survey.id,
                    cancellationToken))
            {
                var citizen = await db.Users.SingleAsync(
                    item => item.email == "citizen@portal.local",
                    cancellationToken);

                db.SurveyResponses.Add(new SurveyResponse
                {
                    id = Guid.NewGuid(),
                    survey_id = survey.id,
                    user_id = citizen.id,
                    answers = JsonDocument.Parse("{\"rating\":5,\"feedback\":\"Foarte util.\"}"),
                    submitted_at = DateTimeOffset.UtcNow.AddHours(-2)
                });

                await db.SaveChangesAsync(cancellationToken);
            }
        }

        private static async Task SeedAppointmentTypeAsync(
            PortalDbContext db,
            CancellationToken cancellationToken)
        {
            var appointmentType = await db.AppointmentTypes.SingleOrDefaultAsync(
                item => item.code == "audienta-generala",
                cancellationToken);

            if (appointmentType is null)
            {
                appointmentType = new AppointmentType
                {
                    id = Guid.NewGuid(),
                    code = "audienta-generala",
                    name = "Audiență generală",
                    description = "Programare pentru audiență cu un reprezentant al instituției.",
                    location = "Sediul instituției",
                    duration_minutes = 30,
                    requires_confirmation = true,
                    max_days_ahead = 30,
                    is_active = true
                };

                db.AppointmentTypes.Add(appointmentType);
                await db.SaveChangesAsync(cancellationToken);
            }

            var now = DateTimeOffset.UtcNow;
            if (!await db.AppointmentSlots.AnyAsync(
                    item => item.appointment_type_id == appointmentType.id &&
                        item.starts_at > now,
                    cancellationToken))
            {
                var timeZone = TimeZoneInfo.FindSystemTimeZoneById(
                    "Europe/Bucharest");
                var tomorrow = TimeZoneInfo.ConvertTime(now, timeZone)
                    .Date.AddDays(1);
                db.AppointmentSlots.AddRange(
                    NewSlot(appointmentType.id,
                        RomanianLocalTimeToUtc(tomorrow.AddHours(9), timeZone)),
                    NewSlot(appointmentType.id,
                        RomanianLocalTimeToUtc(tomorrow.AddHours(10), timeZone)),
                    NewSlot(appointmentType.id,
                        RomanianLocalTimeToUtc(tomorrow.AddHours(11), timeZone), 2));

                await db.SaveChangesAsync(cancellationToken);
            }
        }

        private static DateTimeOffset RomanianLocalTimeToUtc(
            DateTime localTime,
            TimeZoneInfo timeZone)
        {
            var unspecifiedLocalTime = DateTime.SpecifyKind(
                localTime,
                DateTimeKind.Unspecified);
            return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(
                unspecifiedLocalTime,
                timeZone));
        }

        private static async Task SeedSubmissionsAsync(
            PortalDbContext db,
            CancellationToken cancellationToken)
        {
            if (await db.Submissions.AnyAsync(cancellationToken))
            {
                return;
            }

            var citizen = await db.Users.SingleAsync(
                item => item.email == "citizen@portal.local",
                cancellationToken);
            var informationService = await db.ServiceDefinitions.SingleAsync(
                item => item.code == "cerere-informatie",
                cancellationToken);
            var certificateService = await db.ServiceDefinitions.SingleAsync(
                item => item.code == "solicitare-certificat",
                cancellationToken);
            var generalService = await db.ServiceDefinitions.SingleAsync(
                item => item.code == "depunere-solicitare",
                cancellationToken);

            var baseTime = DateTimeOffset.UtcNow.AddDays(-5);
            var registered = NewSubmission(
                informationService,
                citizen,
                SubmissionStatus.Registered,
                baseTime,
                "Înregistrată în registratură.",
                "12/2026");
            var infoRequested = NewSubmission(
                certificateService,
                citizen,
                SubmissionStatus.InfoRequested,
                baseTime.AddDays(1),
                "Este necesară o clarificare privind documentul solicitat.",
                "13/2026");
            var completed = NewSubmission(
                generalService,
                citizen,
                SubmissionStatus.Completed,
                baseTime.AddDays(2),
                "Cererea a fost soluționată.",
                "14/2026");

            db.Submissions.AddRange(registered, infoRequested, completed);
            db.SubmissionEvents.AddRange(
                NewSubmissionEvent(registered, baseTime.AddHours(-1), SubmissionEventType.Submitted,
                    "Cererea a fost depusă."),
                NewSubmissionEvent(registered, baseTime, SubmissionEventType.Registered,
                    "Cererea a fost înregistrată în registratură."),
                NewSubmissionEvent(infoRequested, baseTime.AddHours(23), SubmissionEventType.Submitted,
                    "Cererea a fost depusă."),
                NewSubmissionEvent(infoRequested, baseTime.AddDays(1), SubmissionEventType.InfoRequested,
                    "Au fost solicitate clarificări."),
                NewSubmissionEvent(completed, baseTime.AddDays(1).AddHours(23), SubmissionEventType.Submitted,
                    "Cererea a fost depusă."),
                NewSubmissionEvent(completed, baseTime.AddDays(2), SubmissionEventType.Completed,
                    "Cererea a fost finalizată."));
            await db.SaveChangesAsync(cancellationToken);
        }

        private static Submission NewSubmission(
            ServiceDefinition service,
            User citizen,
            SubmissionStatus status,
            DateTimeOffset submittedAt,
            string statusDetails,
            string registryNumber)
        {
            return new Submission
            {
                id = Guid.NewGuid(),
                external_id = Guid.NewGuid(),
                service_id = service.id,
                user_id = citizen.id,
                schema_version = service.schema_version,
                form_snapshot = JsonDocument.Parse(
                    service.form_schema.RootElement.GetRawText()),
                values = JsonDocument.Parse("{}"),
                status = status,
                status_details = statusDetails,
                submitted_at = submittedAt,
                registered_at = submittedAt.AddHours(2),
                registry_display_number = registryNumber
            };
        }

        private static SubmissionEvent NewSubmissionEvent(
            Submission submission,
            DateTimeOffset occurredAt,
            SubmissionEventType type,
            string message)
        {
            return new SubmissionEvent
            {
                id = Guid.NewGuid(),
                submission_id = submission.id,
                occurred_at = occurredAt,
                type = type,
                message = message
            };
        }

        private static AppointmentSlot NewSlot(
            Guid appointmentTypeId,
            DateTimeOffset startsAt,
            int capacity = 1)
        {
            return new AppointmentSlot
            {
                id = Guid.NewGuid(),
                appointment_type_id = appointmentTypeId,
                starts_at = startsAt,
                ends_at = startsAt.AddMinutes(30),
                capacity = capacity,
                booked_count = 0,
                is_blocked = false
            };
        }

        private static async Task SeedPublicRegistryAsync(
            IServiceProvider services,
            PortalDbContext db,
            CancellationToken cancellationToken)
        {
            var registry = await db.PublicRegistrations.SingleOrDefaultAsync(
                item => item.code == "hotarari-locale",
                cancellationToken);

            if (registry is null)
            {
                registry = new PublicRegistry
                {
                    id = Guid.NewGuid(),
                    code = "hotarari-locale",
                    name = "Registrul hotărârilor locale",
                    description = "Hotărâri publicate pentru consultare publică.",
                    is_published = true,
                    display_order = 1
                };

                db.PublicRegistrations.Add(registry);
                await db.SaveChangesAsync(cancellationToken);
            }

            var entry = await db.PublicRegistryEntries.SingleOrDefaultAsync(
                item => item.registry_id == registry.id && item.position_number == "HCL 1/2026",
                cancellationToken);

            if (entry is null)
            {
                entry = new PublicRegistryEntry
                {
                    id = Guid.NewGuid(),
                    registry_id = registry.id,
                    position_number = "HCL 1/2026",
                    title = "Aprobarea bugetului local",
                    entry_date = DateOnly.FromDateTime(DateTime.UtcNow.Date),
                    description = "Hotărâre publicată pentru consultare.",
                    is_published = true
                };

                db.PublicRegistryEntries.Add(entry);
                await db.SaveChangesAsync(cancellationToken);
            }

            const string storageKey = "public-registry/seed/hcl-1-2026.pdf";
            var pdf = CreateSeedPdf();
            var environment = services.GetRequiredService<IHostEnvironment>();
            var options = services
                .GetRequiredService<IOptions<SubmissionFileStorageOptions>>()
                .Value;
            var root = Path.IsPathRooted(options.RootPath)
                ? options.RootPath
                : Path.Combine(environment.ContentRootPath, options.RootPath);
            var path = Path.Combine(
                root,
                storageKey.Replace('/', Path.DirectorySeparatorChar));

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(path, pdf, cancellationToken);

            if (!await db.PublicRegistryDocuments.AnyAsync(
                    item => item.entry_id == entry.id,
                    cancellationToken))
            {
                db.PublicRegistryDocuments.Add(new PublicRegistryDocument
                {
                    id = Guid.NewGuid(),
                    entry_id = entry.id,
                    storage_key = storageKey,
                    original_name = "hotarare-buget-local.pdf",
                    content_type = "application/pdf",
                    size_bytes = pdf.LongLength,
                    sha256 = Convert.ToHexString(SHA256.HashData(pdf)).ToLowerInvariant(),
                    display_order = 1
                });

                await db.SaveChangesAsync(cancellationToken);
            }
        }

        private static byte[] CreateSeedPdf()
        {
            const string stream =
                "BT\n/F1 18 Tf\n72 720 Td\n(Hotarare demonstrativa) Tj\nET\n";

            var objects = new[]
            {
                "<< /Type /Catalog /Pages 2 0 R >>",
                "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
                $"<< /Length {Encoding.ASCII.GetByteCount(stream)} >>\nstream\n{stream}endstream",
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
            };

            var document = new StringBuilder("%PDF-1.4\n");
            var offsets = new List<int> { 0 };

            for (var index = 0; index < objects.Length; index++)
            {
                offsets.Add(Encoding.ASCII.GetByteCount(document.ToString()));
                document.Append(index + 1)
                    .Append(" 0 obj\n")
                    .Append(objects[index])
                    .Append("\nendobj\n");
            }

            var xrefOffset = Encoding.ASCII.GetByteCount(document.ToString());
            document.Append("xref\n0 ")
                .Append(objects.Length + 1)
                .Append("\n0000000000 65535 f \n");

            for (var index = 1; index < offsets.Count; index++)
            {
                document.Append(offsets[index].ToString("D10"))
                    .Append(" 00000 n \n");
            }

            document.Append("trailer\n<< /Size ")
                .Append(objects.Length + 1)
                .Append(" /Root 1 0 R >>\nstartxref\n")
                .Append(xrefOffset)
                .Append("\n%%EOF\n");

            return Encoding.ASCII.GetBytes(document.ToString());
        }

        private static async Task AddUserIfMissing(
            PortalDbContext db,
            IPasswordHasher<User> passwordHasher,
            string email,
            string fullName,
            Role role,
            string password,
            CancellationToken cancellationToken)
        {
            var normalizedEmail = email.ToLowerInvariant();

            var exists = await db.Users.AnyAsync(
                item => item.email == normalizedEmail,
                cancellationToken);

            if (exists)
            {
                return;
            }

            var user = new User
            {
                id = Guid.NewGuid(),
                email = normalizedEmail,
                full_name = fullName,
                role = role,
                email_confirmed = true,
                is_active = true
            };

            user.password_hash =
                passwordHasher.HashPassword(user, password);

            db.Users.Add(user);

            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
