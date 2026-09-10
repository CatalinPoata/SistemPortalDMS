using API_DMS.Data;
using API_DMS.Entities;
using API_DMS.Entities.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using System.Text.Json;
using Task = System.Threading.Tasks.Task;

namespace API_DMS.Seed
{
    public static class DmsDbSeeder
    {
        public static async Task SeedAsync(
            IServiceProvider services,
            CancellationToken cancellationToken = default)
        {
            var db = services
                .GetRequiredService<DmsDbContext>();

            var passwordHasher = services
                .GetRequiredService<IPasswordHasher<User>>();

            await SeedUserAsync(
                db,
                passwordHasher,
                "clerk@example.com",
                "Clerk Demo",
                Role.Clerk,
                "Clerk123!ChangeMe",
                cancellationToken);

            await SeedUserAsync(
                db,
                passwordHasher,
                "admin@example.com",
                "Admin Demo",
                Role.Admin,
                "Admin123!ChangeMe",
                cancellationToken);

            await SeedUserAsync(
                db,
                passwordHasher,
                "clerk2@example.com",
                "Clerk Demo 2",
                Role.Clerk,
                "Clerk123!ChangeMe",
                cancellationToken);

            await SeedDocumentKindAsync(
                db,
                "ANEXA",
                "Anexă la cerere",
                cancellationToken);

            await SeedDocumentKindAsync(
                db,
                "CERERE",
                "Cerere",
                cancellationToken);

            await SeedDocumentKindAsync(
                db,
                "RASPUNS",
                "Răspuns instituție",
                cancellationToken);

            await SeedDocumentKindAsync(
                db,
                "IDENTITATE",
                "Act de identitate",
                cancellationToken);

            var admin = await db.Users.SingleAsync(
                user => user.email == "admin@example.com",
                cancellationToken);

            var clerk = await db.Users.SingleAsync(
                user => user.email == "clerk@example.com",
                cancellationToken);

            var generalRegistry = await SeedRegistryTypeAsync(
                db,
                "REG-GEN",
                "Registru general intrări-ieșiri",
                RegistryDirection.Both,
                cancellationToken);

            var outgoingRegistry = await SeedRegistryTypeAsync(
                db,
                "REG-OUT",
                "Registru documente emise",
                RegistryDirection.Out,
                cancellationToken);

            var department = await SeedDepartmentAsync(
                db,
                "REGISTRATURA",
                "Compartiment registratură",
                clerk.id,
                cancellationToken);

            await SeedRegistryEntriesAsync(
                db,
                generalRegistry,
                outgoingRegistry,
                department,
                admin,
                clerk,
                cancellationToken);

            await SeedReportDefinitionAsync(
                db,
                admin,
                "registru-intrari-iesiri",
                "Registru intrări-ieșiri",
                "registry_entries",
                """
                {
                  "renderMode": "table",
                  "datasetKey": "registry_entries",
                  "parameters": [
                    {
                      "name": "registryTypeId",
                      "type": "lookup",
                      "source": "registry_types",
                      "label": "Registru",
                      "required": true
                    },
                    {
                      "name": "dateFrom",
                      "type": "date",
                      "label": "De la data",
                      "required": true
                    },
                    {
                      "name": "dateTo",
                      "type": "date",
                      "label": "Până la data",
                      "required": true
                    }
                  ],
                  "columns": [
                    {
                      "field": "number",
                      "label": "Nr.",
                      "type": "number",
                      "align": "center",
                      "widthPct": 7
                    },
                    {
                      "field": "registered_at",
                      "label": "Data",
                      "type": "date",
                      "align": "center",
                      "widthPct": 10,
                      "format": "dd.MM.yyyy"
                    },
                    {
                      "field": "applicant_name",
                      "label": "Solicitant",
                      "type": "text",
                      "align": "left",
                      "widthPct": 22
                    },
                    {
                      "field": "subject",
                      "label": "Obiectul lucrării",
                      "type": "text",
                      "align": "left",
                      "widthPct": 41
                    },
                    {
                      "field": "status",
                      "label": "Stare",
                      "type": "code",
                      "align": "center",
                      "widthPct": 20
                    }
                  ],
                  "sort": [
                    { "field": "number", "dir": "asc" }
                  ],
                  "groupBy": null,
                  "totals": [
                    { "field": "number", "agg": "count" }
                  ],
                  "layout": {
                    "orientation": "landscape",
                    "title": "Registru intrări-ieșiri",
                    "subtitle": "Perioada {dateFrom} – {dateTo}",
                    "showPageNumbers": true
                  }
                }
                """,
                cancellationToken);

            await SeedReportDefinitionAsync(
                db,
                admin,
                "dovada-inregistrare",
                "Dovadă de înregistrare",
                "registry_entries",
                """
                {
                  "renderMode": "record",
                  "datasetKey": "registry_entries",
                  "parameters": [
                    {
                      "name": "entryId",
                      "type": "string",
                      "label": "ID poziție",
                      "required": true
                    }
                  ],
                  "columns": [
                    {
                      "field": "display_number",
                      "label": "Număr de înregistrare",
                      "type": "code",
                      "align": "center"
                    },
                    {
                      "field": "registry_type_name",
                      "label": "Registru",
                      "type": "text",
                      "align": "left"
                    },
                    {
                      "field": "registered_at",
                      "label": "Data înregistrării",
                      "type": "date",
                      "align": "center",
                      "format": "dd.MM.yyyy"
                    },
                    {
                      "field": "direction",
                      "label": "Direcție",
                      "type": "code",
                      "align": "center"
                    },
                    {
                      "field": "applicant_name",
                      "label": "Solicitant",
                      "type": "text",
                      "align": "left"
                    },
                    {
                      "field": "subject",
                      "label": "Obiect",
                      "type": "text",
                      "align": "left"
                    },
                    {
                      "field": "deadline",
                      "label": "Termen",
                      "type": "date",
                      "align": "center",
                      "format": "dd.MM.yyyy"
                    },
                    {
                      "field": "status",
                      "label": "Stare",
                      "type": "code",
                      "align": "center"
                    }
                  ],
                  "sort": [],
                  "groupBy": null,
                  "totals": [],
                  "layout": {
                    "orientation": "portrait",
                    "title": "Dovadă de înregistrare",
                    "subtitle": "Poziția {entryId}",
                    "showPageNumbers": true
                  }
                }
                """,
                cancellationToken);
        }

        private static async Task<RegistryType> SeedRegistryTypeAsync(
            DmsDbContext db,
            string code,
            string name,
            RegistryDirection direction,
            CancellationToken cancellationToken)
        {
            var registry = await db.RegistryTypes.SingleOrDefaultAsync(
                item => item.code == code,
                cancellationToken);

            if (registry is not null)
            {
                return registry;
            }

            registry = new RegistryType
            {
                id = Guid.NewGuid(),
                code = code,
                name = name,
                direction = direction,
                start_number = 1,
                default_deadline_days = 30,
                is_closed = false
            };

            db.RegistryTypes.Add(registry);
            await db.SaveChangesAsync(cancellationToken);
            return registry;
        }

        private static async Task<Department> SeedDepartmentAsync(
            DmsDbContext db,
            string code,
            string name,
            Guid managerUserId,
            CancellationToken cancellationToken)
        {
            var department = await db.Departments.SingleOrDefaultAsync(
                item => item.code == code,
                cancellationToken);

            if (department is not null)
            {
                return department;
            }

            department = new Department
            {
                id = Guid.NewGuid(),
                code = code,
                name = name,
                manager_user_id = managerUserId,
                is_active = true
            };

            db.Departments.Add(department);
            await db.SaveChangesAsync(cancellationToken);
            return department;
        }

        private static async Task SeedRegistryEntriesAsync(
            DmsDbContext db,
            RegistryType generalRegistry,
            RegistryType outgoingRegistry,
            Department department,
            User admin,
            User clerk,
            CancellationToken cancellationToken)
        {
            var existingSubjects = await db.RegistryEntries
                .Where(item => item.subject.StartsWith("[SEED]"))
                .Select(item => item.subject)
                .ToListAsync(cancellationToken);

            var now = DateTimeOffset.UtcNow;
            var currentYear = now.Year;
            var previousYear = currentYear - 1;
            var entries = new List<RegistryEntry>();

            AddSeedEntryIfMissing(
                entries,
                existingSubjects,
                generalRegistry,
                department,
                admin,
                "[SEED] Solicitare înregistrată",
                currentYear,
                9001,
                EntryDirection.In,
                EntryStatus.Registered,
                now.AddDays(-2));

            AddSeedEntryIfMissing(
                entries,
                existingSubjects,
                generalRegistry,
                department,
                clerk,
                "[SEED] Cerere în analiză",
                currentYear,
                9002,
                EntryDirection.In,
                EntryStatus.InReview,
                now.AddDays(-5));

            AddSeedEntryIfMissing(
                entries,
                existingSubjects,
                generalRegistry,
                department,
                clerk,
                "[SEED] Cerere cu informații solicitate",
                previousYear,
                9001,
                EntryDirection.In,
                EntryStatus.InfoRequested,
                now.AddYears(-1).AddDays(-10));

            AddSeedEntryIfMissing(
                entries,
                existingSubjects,
                outgoingRegistry,
                department,
                admin,
                "[SEED] Răspuns finalizat",
                previousYear,
                9001,
                EntryDirection.Out,
                EntryStatus.Completed,
                now.AddYears(-1).AddDays(-20));

            if (entries.Count == 0)
            {
                return;
            }

            db.RegistryEntries.AddRange(entries);
            await db.SaveChangesAsync(cancellationToken);

            db.EntryEvents.AddRange(entries.Select(entry => new EntryEvent
            {
                id = Guid.NewGuid(),
                entry_id = entry.id,
                occurred_at = entry.registered_at,
                actor_user_id = entry.created_by_user_id,
                type = EventType.Created,
                message = "Poziție inițială de demonstrație.",
                payload = JsonDocument.Parse("{}")
            }));

            var openEntry = entries.FirstOrDefault(
                item => item.status is EntryStatus.Registered or EntryStatus.InReview);

            if (openEntry is not null)
            {
                db.Tasks.Add(new API_DMS.Entities.Task
                {
                    id = Guid.NewGuid(),
                    entry_id = openEntry.id,
                    assignee_user_id = clerk.id,
                    department_id = department.id,
                    created_by_user_id = admin.id,
                    title = "Verificare cerere demonstrativă",
                    instructions = "Analizează documentele și formulează un răspuns.",
                    due_date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
                    status = API_DMS.Entities.TaskStatus.Open
                });
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        private static void AddSeedEntryIfMissing(
            ICollection<RegistryEntry> entries,
            IReadOnlyCollection<string> existingSubjects,
            RegistryType registry,
            Department department,
            User actor,
            string subject,
            int year,
            long number,
            EntryDirection direction,
            EntryStatus status,
            DateTimeOffset registeredAt)
        {
            if (existingSubjects.Contains(subject))
            {
                return;
            }

            entries.Add(new RegistryEntry
            {
                id = Guid.NewGuid(),
                registry_type_id = registry.id,
                year = year,
                number = number,
                direction = direction,
                registered_at = registeredAt,
                submitted_at = registeredAt.AddMinutes(-5),
                subject = subject,
                applicant_name = "Cetățean demonstrativ",
                applicant_email = "citizen@portal.local",
                source_doc_number = $"SEED-{year}-{number}",
                source_doc_date = DateOnly.FromDateTime(registeredAt.UtcDateTime),
                department_id = department.id,
                service_code = "depunere-solicitare",
                form_values = JsonDocument.Parse("{\"nume\":\"Cetățean demonstrativ\"}"),
                deadline = DateOnly.FromDateTime(registeredAt.UtcDateTime.AddDays(30)),
                status = status,
                status_note = status == EntryStatus.InfoRequested
                    ? "Sunt necesare informații suplimentare."
                    : null,
                created_by_user_id = actor.id
            });
        }

        private static async Task SeedReportDefinitionAsync(
            DmsDbContext db,
            User admin,
            string code,
            string name,
            string datasetKey,
            string definition,
            CancellationToken cancellationToken)
        {
            var exists = await db.ReportDefinitions.AnyAsync(
                report => report.code == code,
                cancellationToken);

            if (exists)
            {
                return;
            }

            db.ReportDefinitions.Add(new ReportDefinition
            {
                id = Guid.NewGuid(),
                code = code,
                name = name,
                dataset_key = datasetKey,
                definition = JsonDocument.Parse(definition),
                version = 1,
                is_system = true,
                updated_by_user_id = admin.id
            });

            await db.SaveChangesAsync(cancellationToken);
        }

        private static async Task SeedDocumentKindAsync(
            DmsDbContext db,
            string code,
            string name,
            CancellationToken cancellationToken)
        {
            var exists = await db.DocumentKinds.AnyAsync(
                item => item.code == code,
                cancellationToken);

            if (exists)
            {
                return;
            }

            db.DocumentKinds.Add(new DocumentKind
            {
                id = Guid.NewGuid(),
                code = code,
                name = name,
                is_active = true
            });

            await db.SaveChangesAsync(cancellationToken);
        }

        private static async Task SeedUserAsync(
            DmsDbContext db,
            IPasswordHasher<User> passwordHasher,
            string email,
            string fullName,
            Role role,
            string password,
            CancellationToken cancellationToken)
        {
            var normalizedEmail =
                email.Trim().ToLowerInvariant();

            var exists = await db.Users.AnyAsync(
                user => user.email == normalizedEmail,
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
