# Cerințe — Portal de Servicii Publice + Sistem de Management al Documentelor

**Versiune:** 3.0

---

## 1. Obiectiv

Se cere realizarea unui sistem compus din trei aplicații care funcționează ca un tot unitar:

- un **sistem de management al documentelor (DMS)**, care ține evidența intrărilor și ieșirilor de documente ale unei instituții, organizate pe registre, cu documente atașate;
- un **backoffice al portalului de servicii**, în care se configurează serviciile publice, formularele, articolele, chestionarele, programările și registrele publicate;
- un **portal public**, în care cetățeanul se autentifică, depune cereri, completează chestionare, solicită programări și consultă informațiile publicate.

Cererea depusă pe portal ajunge în DMS, primește număr de înregistrare, este prelucrată de un funcționar, iar rezultatul se întoarce în contul cetățeanului.

### 1.1. Capacități obligatorii

| # | Capacitate |
|---|---|
| C1 | Înregistrarea documentelor în registre, cu documente atașate și metadatele lor |
| C2 | Definirea formularelor în backoffice și publicarea lor în zona publică |
| C3 | Publicarea articolelor în portal |
| C4 | Definirea chestionarelor în backoffice și publicarea lor în zona publică |
| C5 | Solicitarea de programări în portal, cu definirea și confirmarea lor din backoffice |
| C6 | Publicarea de registre de documente în portal |
| C7 | Integrarea portal ↔ DMS: cererea pleacă, este prelucrată și se întoarce în contul cetățeanului |
| C8 | Rapoarte definite ca date, cu previzualizare, designer și export PDF |

---

## 2. Componentele sistemului

| Cod | Aplicație | Tip | Utilizatori |
|---|---|---|---|
| **APP-DMS** | Management documente / registratură | Aplicație web privată | `Clerk`, `Admin` |
| **APP-BO** | Backoffice portal servicii | Aplicație web privată | `Admin` |
| **APP-PUB** | Portal servicii — zona publică | Aplicație web publică | `Citizen`, vizitator neautentificat |

| Cod | Serviciu backend | Schemă de bază de date |
|---|---|---|
| **API-DMS** | API-ul registraturii | `dms` |
| **API-PORTAL** | API-ul portalului (public + backoffice) | `portal` |

APP-BO și APP-PUB pot fi două zone ale aceleiași aplicații front-end sau două aplicații separate, dar separarea de acces trebuie să fie reală: backoffice-ul nu este accesibil unui utilizator cu rol `Citizen`.

---

## 3. Stack tehnologic

| Strat | Tehnologie |
|---|---|
| Bază de date | **PostgreSQL 16+**, o instanță, două scheme (`portal`, `dms`) |
| Backend | **ASP.NET Core 8/9** + **Entity Framework Core** (Npgsql), migrații versionate în repo |
| Frontend | **React 18+** cu **Next.js 14+** (App Router), TypeScript |
| Stocare fișiere | Volum local, prin abstracție `IFileStore` |
| Rulare | **Docker Compose** — o singură comandă ridică întregul sistem, cu date de demonstrație |

**Reguli arhitecturale:**

1. Fiecare serviciu accesează numai propria schemă. Nu există chei străine între `portal` și `dms`, nu există interogări între scheme, iar șirul de conexiune al unui serviciu nu apare în configurația celuilalt.
2. Se folosesc doi utilizatori de bază de date cu drepturi disjuncte: `portal_app` are drepturi doar pe schema `portal`, `dms_app` doar pe `dms`.
3. Comunicarea între servicii se face exclusiv HTTP + JSON, autentificată conform §10.1.

---

## 4. Convenții generale

### 4.1. Tipuri de date

| Aspect | Regulă |
|---|---|
| **Chei primare** | **`uuid` (GUID)** pentru toate entitățile, generat de aplicație. Singura excepție: contorul de numerotare, care are cheie compusă (§8.2). |
| Chei de corelare între servicii | `uuid` |
| Momente în timp | `timestamptz`, stocate în **UTC**; pe fir **ISO 8601 UTC** |
| Date calendaristice fără oră | `date` |
| Sume | `numeric(14,2)` |
| Text scurt | `varchar(n)`, cu `n` declarat explicit |
| Text lung / HTML | `text` |
| Structuri variabile | **`jsonb`** |
| Enumerări | `varchar` cu constrângere `CHECK`, corespondent unui `enum` în cod |
| Numere de ordine vizibile (număr de înregistrare, poziție în registru) | `bigint` — sunt date de business, nu chei |

Formatarea de afișare (`zz.ll.aaaa`, separatori) aparține stratului de prezentare: front-end sau randorul de raport. Persistența și API-ul lucrează cu valori neformatate și ISO 8601 UTC.

### 4.2. API

- Rute REST sub `/api`, la plural, cu cratimă: `/api/registry-entries`, `/api/service-definitions`, `/api/appointments`.
- Erorile se întorc ca **`ProblemDetails`** (RFC 7807), cu `type`, `title`, `status`, `detail`, `traceId` și, pentru validări, `errors` (dicționar câmp → listă de mesaje).
- Coduri: `400` cerere malformată sau câmp `AUTO` trimis de client, `401` neautentificat, `403` neautorizat, `404` inexistent, `409` conflict, `422` regulă de business încălcată.
- Listările sunt paginate: `?page=1&pageSize=25&sort=field:asc`, răspuns `{ items, page, pageSize, total }`, cu limită maximă a lui `pageSize` impusă pe server.
- Toate răspunsurile poartă `traceId`, corelabil cu logurile.

### 4.3. Validare

Validarea de pe server este singura autoritate. Orice regulă aplicată în front-end există și pe server. Un `POST` direct către API, cu corp invalid, este respins cu `422`.

---

## 5. Roluri

| Rol | Acces |
|---|---|
| `Citizen` | APP-PUB: cont propriu, depunere cereri, chestionare, programări, vizualizarea propriilor date. Nu vede datele altor utilizatori. |
| `Clerk` | APP-DMS: registre, poziții de registratură, documente, repartizări, rapoarte. |
| `Admin` | Tot ce are `Clerk`, plus APP-BO: servicii, formulare, articole, chestionare, programări, registre publice, definiții de rapoarte, monitorizarea integrării. |

Verificarea rolului se face pe server, la fiecare endpoint. Ascunderea unui element în interfață nu constituie autorizare.

---

## 6. Autentificare

Fiecare serviciu are propriii utilizatori și emite propriul token, semnat cu propriul secret.

1. Parolele se stochează cu funcție de derivare lentă (ASP.NET Core Identity cu implicitul său, sau `PBKDF2` / `bcrypt` / `Argon2`, cu sare per utilizator).
2. Autentificare pe bază de **JWT** cu durată scurtă (10–15 minute), plus **refresh token** rotativ, stocat în baza de date și revocabil.
3. Refresh token-ul se transmite prin cookie `HttpOnly`, `Secure`, `SameSite=Strict`.
4. Înregistrare cont cetățean cu e-mail și parolă, urmată de confirmarea adresei printr-un token cu expirare. Token-ul se generează și se persistă; expedierea prin e-mail este opțională, iar în lipsa ei linkul se expune într-un ecran de dezvoltare.
5. Resetare parolă prin token cu expirare, de unică folosință.
6. Politică minimă de parolă (lungime ≥ 10, cel puțin trei clase de caractere), aplicată pe server.
7. Limitarea încercărilor de autentificare: 5 eșecuri consecutive → blocare temporară 15 minute.
8. Mesajele de eroare nu dezvăluie dacă adresa există.

**Opțional:** autentificare în doi pași cu TOTP pentru rolurile `Clerk` și `Admin`.

---

## 7. Fluxuri

Toate fluxurile sistemului, descrise sumar. Detaliile de model apar în §8, iar cele de contract în §10.

### F1 — Depunerea și soluționarea unei cereri (portal → DMS → portal)

1. Cetățeanul autentificat alege un serviciu publicat și completează formularul randat din schema serviciului, atașând fișiere dacă serviciul o cere.
2. La trimitere, portalul validează pe server, creează cererea cu un `external_id` nou și starea `Submitted`, copiază schema formularului ca instantaneu și scrie un mesaj în outbox — totul într-o singură tranzacție. Utilizatorul primește confirmarea imediat.
3. Un lucrător de fundal trimite cererea către DMS, cu antet de idempotență.
4. DMS-ul alocă numărul de înregistrare din contorul registrului, creează poziția de registratură, descarcă documentele prin canalul intern și persistă răspunsul — într-o singură tranzacție.
5. Portalul primește numărul, trece cererea în `Registered`, adaugă un eveniment în cronologie și o notificare.
6. Funcționarul preia poziția în DMS (`InReview`), o repartizează unui utilizator sau compartiment și o lucrează.
7. Dacă are nevoie de completări, trece poziția în `InfoRequested`. Cetățeanul încarcă fișierele suplimentare din ecranul cererii, portalul le trimite către DMS ca atașări ulterioare, iar poziția revine în `InReview`.
8. La finalizare, funcționarul încarcă documentul de răspuns și trece poziția în `Completed`. Poziția poate fi și respinsă, cu motiv obligatoriu.
9. Fiecare schimbare de stare produce un mesaj de outbox în DMS, livrat portalului; portalul îl deduplică și actualizează cererea, cronologia și notificările.
10. Cetățeanul deschide cererea și descarcă răspunsul: portalul verifică apartenența cererii la utilizator, cere DMS-ului un URL semnat de scurtă durată și redirecționează browserul.
11. Cetățeanul poate retrage cererea cât timp aceasta nu a intrat în lucru.

### F2 — Înregistrarea directă în registratură

1. Funcționarul alege registrul și direcția (intrare sau ieșire).
2. Completează datele poziției: obiectul lucrării, emitentul sau destinatarul, numărul și data documentului sursă, compartimentul, termenul.
3. La salvare, sistemul alocă numărul din contorul registrului pentru anul curent și înregistrează poziția.
4. Funcționarul atașează documentele, fiecare cu metadatele lui: tip de document, dată, emitent, observații.
5. Poziția parcurge aceleași stări și aceleași acțiuni ca o poziție venită din portal.

### F3 — Definirea și publicarea unui formular

1. Administratorul creează un serviciu public: cod, titlu, descriere, registrul din DMS în care se vor înregistra cererile.
2. Construiește formularul în editorul din backoffice: secțiuni, câmpuri, tipuri de câmp, validări, ordine, vizibilitate condiționată.
3. Previzualizează formularul exact așa cum îl va vedea cetățeanul.
4. Publică serviciul. Schema se validează structural la publicare.
5. Serviciul apare imediat în portalul public, iar formularul se randează generic din schemă, fără modificare de cod și fără redeploy.
6. Modificarea ulterioară a schemei incrementează versiunea și nu afectează cererile deja depuse, care se randează din instantaneul propriu.

### F4 — Publicarea unui articol

1. Administratorul redactează articolul în backoffice, cu editor WYSIWYG.
2. Conținutul HTML se sanitizează la salvare.
3. Articolul se publică, opțional cu dată de publicare programată în viitor.
4. Articolul apare în portalul public, în listă și pe pagina proprie, accesibilă după `slug`.

### F5 — Chestionar

1. Administratorul creează chestionarul în backoffice: titlu, descriere, perioadă de disponibilitate, dacă permite răspuns anonim, dacă afișează rezultatele.
2. Adaugă întrebările: tipul (alegere unică, alegere multiplă, evaluare pe scală, text liber), variantele de răspuns, obligativitatea și ordinea.
3. Publică chestionarul; acesta devine vizibil în portalul public în perioada declarată.
4. Cetățeanul completează chestionarul. Un utilizator autentificat poate răspunde o singură dată la același chestionar.
5. Administratorul consultă rezultatele agregate în backoffice; dacă chestionarul o permite, agregatul se afișează și în portalul public după completare.

### F6 — Programare

1. Administratorul definește tipurile de programare în backoffice: denumire, descriere, durată, locație, dacă necesită confirmare.
2. Generează intervalele disponibile pentru un tip de programare, pe o perioadă și un orar declarate, cu capacitate per interval.
3. Cetățeanul alege tipul de programare, vede intervalele libere și solicită o programare, cu observații opționale.
4. Sistemul verifică la rezervare că intervalul mai are capacitate; rezervarea se face atomic, astfel încât două solicitări simultane să nu depășească capacitatea.
5. Dacă tipul de programare necesită confirmare, programarea rămâne în starea `Requested`. Administratorul o confirmă sau o respinge, cu motiv la respingere. Altfel, se confirmă automat.
6. Cetățeanul vede starea programării în contul său și o poate anula până la începerea intervalului. Anularea eliberează capacitatea.
7. Administratorul poate marca programarea ca onorată sau neprezentată.

### F7 — Registru public de documente

1. Administratorul creează un registru public în backoffice: cod, denumire, descriere.
2. Adaugă poziții în registru: număr de poziție, titlu, dată, descriere, și atașează unul sau mai multe documente.
3. Publică registrul și pozițiile.
4. În portalul public, orice vizitator — inclusiv neautentificat — consultă registrul, caută în el după titlu și interval de date și descarcă documentele publicate.

### F8 — Raportare

1. Administratorul creează o definiție de raport: alege setul de date, selectează și ordonează coloanele, le stabilește eticheta, alinierea, lățimea și formatul, definește parametrii, gruparea și totalurile.
2. Previzualizează raportul pe date reale, direct în designer.
3. Salvează definiția, cu incrementarea versiunii.
4. Utilizatorul rulează raportul dintr-o listă, completează parametrii, vede previzualizarea paginată și exportă în PDF.

---

## 8. Model de date

Coloana **Oblig.**: `DA` = obligatoriu, completat de client; `NU` = opțional; `AUTO` = completat exclusiv de server.

> Câmpurile marcate `AUTO` nu se acceptă în corpul cererilor `POST`/`PUT`. Prezența lor → `400`.
>
> Toate cheile primare sunt `uuid`. Toate entitățile au `created_at` și, unde se modifică, `updated_at`, ambele `AUTO`.

### 8.1. Identitate

`User` și `RefreshToken` există fizic în ambele scheme, populate independent: schema `portal` conține cetățenii și administratorii de portal, schema `dms` conține funcționarii și administratorii. Cetățeanul nu are corespondent în schema `dms` — datele lui ajung denormalizate în câmpurile `applicant_*` ale poziției de registratură.

#### `User`

| Câmp | Tip | Oblig. | Reguli și comportament |
|---|---|---|---|
| `id` | uuid | AUTO | |
| `email` | text (256) | DA | Unic în schema sa, normalizat (lowercase, trim), cu index unic |
| `password_hash` | text (512) | AUTO | |
| `role` | enum | DA | `Citizen` \| `Clerk` \| `Admin`. Implicit `Citizen` la auto-înregistrare. |
| `full_name` | text (200) | DA | |
| `national_id` | text (13) | NU | CNP/CUI, validat ca format dacă e completat; se afișează mascat acolo unde nu e strict necesar |
| `phone` | text (30) | NU | |
| `address` | text (500) | NU | Adresă pe un singur câmp |
| `email_confirmed` | boolean | AUTO | Implicit `false`. Cât timp e `false`, depunerea de cereri și solicitarea de programări se resping cu `422`. |
| `is_active` | boolean | DA | Implicit `true`. La dezactivare autentificarea eșuează, datele existente rămân. |
| `failed_login_count` | numeric (int) | AUTO | |
| `lockout_end` | dată+oră (UTC) | AUTO | |

#### `RefreshToken`

| Câmp | Tip | Oblig. | Reguli |
|---|---|---|---|
| `id` | uuid | AUTO | |
| `user_id` | referință → `User` | AUTO | |
| `token_hash` | text (128) | AUTO | Se stochează hash-ul, nu token-ul |
| `expires_at` | dată+oră (UTC) | AUTO | |
| `revoked_at` | dată+oră (UTC) | AUTO | Se completează la rotire sau la deconectare |
| `replaced_by_id` | referință → `RefreshToken` | AUTO | Lanț de rotire; reutilizarea unui token revocat revocă tot lanțul |

---

### 8.2. Schema `dms` — registratură

#### `RegistryType` — nomenclatorul de registre

| Câmp | Tip | Oblig. | Reguli și comportament |
|---|---|---|---|
| `id` | uuid | AUTO | |
| `code` | text (30) | DA | Unic. Cod stabil, folosit în integrare. Nemodificabil după prima înregistrare. |
| `name` | text (200) | DA | |
| `direction` | enum | DA | `In` \| `Out` \| `Both`. Determină ce tip de poziții acceptă. |
| `start_number` | numeric (bigint) | DA | Implicit `1`. Prima poziție a fiecărui an pornește de aici. |
| `default_deadline_days` | numeric (int) | DA | Implicit `30`. Sursa termenului implicit al pozițiilor, în zile calendaristice. |
| `is_closed` | boolean | DA | Implicit `false`. Registru închis → orice tentativă de înregistrare eșuează cu `422`. |

Ștergerea unui registru care are poziții este interzisă (`409`); se folosește `is_closed`.

#### `DocumentKind` — nomenclatorul tipurilor de document

| Câmp | Tip | Oblig. | Reguli |
|---|---|---|---|
| `id` | uuid | AUTO | |
| `code` | text (30) | DA | Unic (ex. `CERERE`, `ADEVERINTA`, `ANEXA`, `RASPUNS`) |
| `name` | text (150) | DA | |
| `is_active` | boolean | DA | Implicit `true` |

#### `RegistryNumberCounter` — contorul de numerotare

| Câmp | Tip | Oblig. | Reguli |
|---|---|---|---|
| `registry_type_id` | referință → `RegistryType` | AUTO | Parte din cheia primară compusă |
| `year` | numeric (int) | AUTO | Parte din cheia primară compusă |
| `last_number` | numeric (bigint) | AUTO | Ultimul număr alocat |

Alocarea numărului se face printr-o singură instrucțiune atomică, executată în aceeași tranzacție cu inserarea poziției:

```sql
INSERT INTO dms.registry_number_counter (registry_type_id, year, last_number)
VALUES (@t, @y, (SELECT start_number FROM dms.registry_type WHERE id = @t))
ON CONFLICT (registry_type_id, year)
DO UPDATE SET last_number = registry_number_counter.last_number + 1
RETURNING last_number;
```

Implementarea prin `SELECT MAX(number) + 1` cu reîncercare la violarea constrângerii unice nu se acceptă.

#### `RegistryEntry` — poziția de registratură

| Câmp | Tip | Oblig. | Reguli și comportament |
|---|---|---|---|
| `id` | uuid | AUTO | Identificator intern, distinct de numărul de registratură |
| `external_id` | uuid | NU | Cheia de corelare cu portalul. Unic când e completat. Gol pentru pozițiile create direct în DMS. |
| `registry_type_id` | referință → `RegistryType` | DA | Nemodificabil după creare (`422`) |
| `year` | numeric (int) | AUTO | Derivat din `registered_at` |
| `number` | numeric (bigint) | AUTO | Alocat de contor. Nemodificabil. |
| `display_number` | text | — | Calculat, nestocat: `"{number}/{year}"` |
| `direction` | enum | DA | `In` \| `Out`. Pozițiile provenite din portal au întotdeauna `In`. |
| `registered_at` | dată+oră (UTC) | AUTO | Momentul înregistrării, dat de server. Nemodificabil. |
| `submitted_at` | dată+oră (UTC) | NU | Momentul depunerii la portal |
| `subject` | text (1000) | DA | Obiectul lucrării |
| `applicant_name` | text (200) | DA | Emitent la intrare, destinatar la ieșire |
| `applicant_national_id` | text (13) | NU | |
| `applicant_email` | text (256) | NU | Validat ca adresă de e-mail dacă e completat |
| `applicant_phone` | text (30) | NU | |
| `applicant_address` | text (500) | NU | |
| `source_doc_number` | text (60) | NU | Numărul propriu al documentului emitentului |
| `source_doc_date` | dată | NU | Nu poate fi în viitor |
| `department_id` | referință → `Department` | NU | Compartimentul repartizat |
| `service_code` | text (50) | NU | Codul serviciului din portal, când poziția provine din portal |
| `form_values` | jsonb | NU | Valorile formularului, copiate din portal ca instantaneu inert: se afișează și se listează, dar nu se validează și nu se interpretează în DMS |
| `deadline` | dată | AUTO | La creare: `registered_at` + `RegistryType.default_deadline_days`. Ulterior modificabil. |
| `status` | enum | AUTO | Vezi §9. Implicit `Registered`. Se modifică exclusiv prin acțiunile de tranziție. |
| `status_note` | text (1000) | NU | Motivul ultimei schimbări de stare; obligatoriu la `Rejected`, `InfoRequested` și `Cancelled` |
| `created_by_user_id` | referință → `User` | AUTO | Din contextul de autentificare; pentru pozițiile venite din portal, contul tehnic de integrare |

**Constrângeri:**
- `UNIQUE (registry_type_id, year, number)`;
- `UNIQUE (external_id)` acolo unde `external_id` este completat;
- indici pe `(registry_type_id, year, registered_at)`, `(status)`, `(department_id)`;
- index pentru căutare pe fragment în `subject` și `applicant_name`.

Anularea se face prin tranziția în starea `Cancelled`. Numărul rămâne consumat și nu se reutilizează.

#### `RegistryDocument` — document atașat unei poziții

| Câmp | Tip | Oblig. | Reguli |
|---|---|---|---|
| `id` | uuid | AUTO | |
| `entry_id` | referință → `RegistryEntry` | DA | |
| `external_file_id` | uuid | NU | Identificatorul fișierului la portal. Unic când e completat; cheia de idempotență pentru atașările ulterioare. |
| `direction` | enum | DA | `In` (document depus) \| `Out` (răspunsul instituției) |
| `document_kind_id` | referință → `DocumentKind` | DA | Tipul documentului |
| `document_date` | dată | NU | Data documentului, poate diferi de data încărcării |
| `issuer` | text (200) | NU | Emitentul documentului |
| `note` | text (500) | NU | Observații |
| `storage_key` | text (200) | AUTO | Cheie opacă în stocare, care nu conține numele original și nu este ghicibilă |
| `original_name` | text (255) | DA | Numele afișat |
| `content_type` | text (120) | AUTO | Determinat pe server după conținut și validat față de o listă albă (PDF, JPEG, PNG, DOCX, ODT) |
| `size_bytes` | numeric (bigint) | AUTO | Limită impusă pe server, implicit 10 MB per fișier |
| `sha256` | text (64) | AUTO | Calculat la încărcare |
| `uploaded_by_user_id` | referință → `User` | AUTO | |

Tipul fișierului se determină după conținut, nu după extensie și nu după antetul trimis de client.

#### `Department` — compartiment

| Câmp | Tip | Oblig. | Reguli |
|---|---|---|---|
| `id` | uuid | AUTO | |
| `code` | text (20) | DA | Unic |
| `name` | text (200) | DA | |
| `manager_user_id` | referință → `User` | NU | |
| `is_active` | boolean | DA | Implicit `true` |

#### `Task` — repartizare / rezoluție

| Câmp | Tip | Oblig. | Reguli |
|---|---|---|---|
| `id` | uuid | AUTO | |
| `entry_id` | referință → `RegistryEntry` | DA | |
| `assignee_user_id` | referință → `User` | NU | Dacă e completat, utilizatorul trebuie să aibă rol `Clerk` sau `Admin` (`422` altfel) |
| `department_id` | referință → `Department` | NU | Repartizare către compartiment |
| `created_by_user_id` | referință → `User` | AUTO | |
| `title` | text (200) | DA | |
| `instructions` | text lung | NU | Rezoluția |
| `due_date` | dată | NU | Nu poate fi anterioară datei curente la creare |
| `status` | enum | AUTO | `Open` \| `Done` \| `Cancelled`. Implicit `Open`. |
| `resolution_note` | text (1000) | NU | Obligatoriu la trecerea în `Done` |
| `completed_at` | dată+oră (UTC) | AUTO | Completat automat la `Done` |

`CHECK (assignee_user_id IS NOT NULL OR department_id IS NOT NULL)`.

#### `EntryEvent` — istoricul poziției

| Câmp | Tip | Oblig. | Reguli |
|---|---|---|---|
| `id` | uuid | AUTO | |
| `entry_id` | referință → `RegistryEntry` | AUTO | |
| `occurred_at` | dată+oră (UTC) | AUTO | |
| `actor_user_id` | referință → `User` | AUTO | Gol pentru evenimente de sistem |
| `type` | enum | AUTO | `Created`, `StatusChanged`, `Assigned`, `DocumentAdded`, `TaskCompleted` |
| `message` | text (1000) | AUTO | |
| `payload` | jsonb | AUTO | Detalii, ex. `{"from":"Registered","to":"InReview"}` |

Evenimentul se scrie în aceeași tranzacție cu modificarea pe care o descrie.

---

### 8.3. Schema `portal` — servicii și cereri

#### `ServiceDefinition` — serviciul public publicat

| Câmp | Tip | Oblig. | Reguli și comportament |
|---|---|---|---|
| `id` | uuid | AUTO | |
| `code` | text (50) | DA | Unic, folosit în URL și în integrare. Doar litere mici, cifre și `-`. |
| `title` | text (200) | DA | |
| `short_description` | text (500) | NU | Afișată în lista de servicii |
| `description` | **html** (`text`) | NU | Sanitizat pe server, cu listă albă de etichete, înainte de stocare |
| `registry_type_code` | text (30) | DA | Codul registrului din DMS. Validat la publicare printr-un apel către API-DMS. |
| `form_schema` | **jsonb** | DA | Schema formularului, conform §11 |
| `schema_version` | numeric (int) | AUTO | Pornește de la `1`, se incrementează la fiecare salvare care modifică `form_schema` |
| `requires_attachment` | boolean | DA | Implicit `false` |
| `max_attachments` | numeric (int) | DA | Implicit `3`; `0` = fără atașamente |
| `is_published` | boolean | DA | Implicit `false`. Serviciul nepublicat nu apare în portal și respinge depunerile cu `404`. |
| `display_order` | numeric (int) | DA | Implicit `0` |

Schema se validează structural la publicare; publicarea unui serviciu cu schemă invalidă → `422`, cu lista erorilor.

#### `Submission` — cererea depusă

| Câmp | Tip | Oblig. | Reguli și comportament |
|---|---|---|---|
| `id` | uuid | AUTO | |
| `external_id` | uuid | AUTO | Generat la depunere. Unic. Singura cheie de corelare cu DMS-ul. |
| `service_id` | referință → `ServiceDefinition` | DA | |
| `schema_version` | numeric (int) | AUTO | Copiat din serviciu la momentul depunerii; nu se actualizează ulterior |
| `form_snapshot` | jsonb | AUTO | Copia schemei de la momentul depunerii, care garantează randarea corectă a cererilor vechi |
| `user_id` | referință → `User` | AUTO | Din contextul de autentificare |
| `values` | jsonb | DA | Valorile completate, conform §11.5 |
| `status` | enum | AUTO | Vezi §9. Implicit `Submitted`. |
| `status_details` | text (1000) | AUTO | Text afișat cetățeanului |
| `submitted_at` | dată+oră (UTC) | AUTO | |
| `registry_number` | numeric (bigint) | AUTO | Completat din răspunsul DMS |
| `registry_year` | numeric (int) | AUTO | |
| `registry_display_number` | text (30) | AUTO | Ex. `„1247/2026”` |
| `registered_at` | dată+oră (UTC) | AUTO | |
| `dms_entry_id` | uuid | AUTO | Identificatorul poziției în DMS, folosit doar pentru apelurile de integrare |

Câmpurile `registry_*` sunt informație afișabilă. Corelarea se face exclusiv pe `external_id`.

#### `SubmissionFile`

| Câmp | Tip | Oblig. | Reguli |
|---|---|---|---|
| `id` | uuid | AUTO | Identificatorul trimis către DMS ca `fileId` |
| `submission_id` | referință → `Submission` | AUTO | |
| `field_key` | text (60) | NU | Cheia câmpului de tip `file` căruia îi corespunde; gol pentru atașamentele generale și documentele generate |
| `kind` | enum | AUTO | `Application` (document generat) \| `Attachment` (încărcat de cetățean) \| `Response` (referință către documentul de răspuns din DMS) |
| `storage_key` | text (200) | AUTO | Pentru `Response`, conține identificatorul documentului din DMS |
| `original_name` | text (255) | DA | |
| `content_type` | text (120) | AUTO | Listă albă, verificată după conținut |
| `size_bytes` | numeric (bigint) | AUTO | |
| `sha256` | text (64) | AUTO | |

#### `SubmissionEvent` — cronologia afișată cetățeanului

| Câmp | Tip | Oblig. | Reguli |
|---|---|---|---|
| `id` | uuid | AUTO | |
| `submission_id` | referință → `Submission` | AUTO | |
| `occurred_at` | dată+oră (UTC) | AUTO | |
| `type` | enum | AUTO | `Submitted`, `Registered`, `StatusChanged`, `InfoRequested`, `FileAdded`, `Completed`, `Rejected`, `Cancelled` |
| `message` | text (1000) | AUTO | Formulat pentru cetățean, în limba română |
| `file_id` | referință → `SubmissionFile` | AUTO | |

#### `Notification`

| Câmp | Tip | Oblig. | Reguli |
|---|---|---|---|
| `id` | uuid | AUTO | |
| `user_id` | referință → `User` | AUTO | |
| `subject` | text (200) | AUTO | |
| `body` | text lung | AUTO | |
| `link_url` | text (500) | AUTO | Ruta către obiectul vizat (cerere, programare) |
| `read_at` | dată+oră (UTC) | AUTO | Marcat de utilizator |
| `sent_at` | dată+oră (UTC) | AUTO | Completat doar dacă expedierea este implementată |

---

### 8.4. Schema `portal` — conținut publicat

#### `Article`

| Câmp | Tip | Oblig. | Reguli |
|---|---|---|---|
| `id` | uuid | AUTO | |
| `slug` | text (160) | DA | Unic; propus automat din titlu, editabil; doar litere mici, cifre, `-` |
| `title` | text (250) | DA | |
| `summary` | text (500) | NU | |
| `body` | **html** (`text`) | DA | Editor WYSIWYG în backoffice; sanitizare pe server, fără `<script>`, fără atribute `on*`, fără `javascript:` |
| `published_at` | dată+oră (UTC) | NU | Articolele cu dată în viitor nu apar public |
| `is_published` | boolean | DA | Implicit `false` |

#### `PublicRegistry` — registru de documente publicat

| Câmp | Tip | Oblig. | Reguli |
|---|---|---|---|
| `id` | uuid | AUTO | |
| `code` | text (40) | DA | Unic, folosit în URL |
| `name` | text (200) | DA | Ex. „Registrul hotărârilor” |
| `description` | text (1000) | NU | |
| `is_published` | boolean | DA | Implicit `false` |
| `display_order` | numeric (int) | DA | Implicit `0` |

#### `PublicRegistryEntry` — poziție într-un registru publicat

| Câmp | Tip | Oblig. | Reguli |
|---|---|---|---|
| `id` | uuid | AUTO | |
| `registry_id` | referință → `PublicRegistry` | DA | |
| `position_number` | text (40) | DA | Numărul poziției, ex. `„HCL 45”`. Unic în cadrul registrului. |
| `title` | text (500) | DA | |
| `entry_date` | dată | DA | Data actului |
| `description` | text (2000) | NU | |
| `is_published` | boolean | DA | Implicit `false`. Poziția nepublicată nu apare public, chiar dacă registrul e publicat. |

#### `PublicRegistryDocument`

| Câmp | Tip | Oblig. | Reguli |
|---|---|---|---|
| `id` | uuid | AUTO | |
| `entry_id` | referință → `PublicRegistryEntry` | DA | |
| `storage_key` | text (200) | AUTO | |
| `original_name` | text (255) | DA | |
| `content_type` | text (120) | AUTO | Listă albă, verificată după conținut |
| `size_bytes` | numeric (bigint) | AUTO | |
| `sha256` | text (64) | AUTO | |
| `display_order` | numeric (int) | DA | Implicit `0` |

Documentele dintr-un registru publicat se descarcă public, fără autentificare, dar tot prin URL semnat, generat la cerere.

---

### 8.5. Schema `portal` — chestionare

#### `Survey`

| Câmp | Tip | Oblig. | Reguli |
|---|---|---|---|
| `id` | uuid | AUTO | |
| `code` | text (50) | DA | Unic, folosit în URL |
| `title` | text (250) | DA | |
| `description` | **html** (`text`) | NU | Sanitizat pe server |
| `starts_at` | dată+oră (UTC) | NU | Înainte de acest moment, chestionarul nu acceptă răspunsuri |
| `ends_at` | dată+oră (UTC) | NU | După acest moment, chestionarul nu acceptă răspunsuri (`422`) și devine doar consultabil |
| `allow_anonymous` | boolean | DA | Implicit `false`. Când e `true`, se poate răspunde fără autentificare. |
| `show_results` | boolean | DA | Implicit `false`. Când e `true`, agregatul se afișează respondentului după trimitere. |
| `is_published` | boolean | DA | Implicit `false` |

`CHECK (ends_at IS NULL OR starts_at IS NULL OR ends_at > starts_at)`.

#### `SurveyQuestion`

| Câmp | Tip | Oblig. | Reguli |
|---|---|---|---|
| `id` | uuid | AUTO | |
| `survey_id` | referință → `Survey` | DA | |
| `key` | text (60) | DA | Stabil, unic în chestionar, imutabil; cheia sub care se stochează răspunsul |
| `text` | text (1000) | DA | Textul întrebării |
| `type` | enum | DA | `SingleChoice` \| `MultiChoice` \| `Rating` \| `FreeText` |
| `options` | jsonb | NU | Obligatoriu pentru `SingleChoice` și `MultiChoice`: `[{ "value": "...", "label": "..." }]`. Pentru `Rating`: `{ "min": 1, "max": 5, "minLabel": "...", "maxLabel": "..." }`. |
| `is_required` | boolean | DA | Implicit `false` |
| `display_order` | numeric (int) | DA | |

Modificarea întrebărilor unui chestionar care are deja răspunsuri este permisă doar pentru textul afișat; adăugarea, ștergerea sau schimbarea tipului se resping cu `422`.

#### `SurveyResponse`

| Câmp | Tip | Oblig. | Reguli |
|---|---|---|---|
| `id` | uuid | AUTO | |
| `survey_id` | referință → `Survey` | AUTO | |
| `user_id` | referință → `User` | AUTO | Gol pentru răspunsurile anonime |
| `answers` | jsonb | DA | `{ "cheie_intrebare": valoare }`. Formă: șir pentru `SingleChoice` și `FreeText`, listă de șiruri pentru `MultiChoice`, număr pentru `Rating`. |
| `submitted_at` | dată+oră (UTC) | AUTO | |

**Constrângere:** `UNIQUE (survey_id, user_id)` acolo unde `user_id` este completat — un utilizator autentificat răspunde o singură dată la același chestionar. Răspunsurile necunoscute sau lipsa unui răspuns obligatoriu se resping cu `422`.

---

### 8.6. Schema `portal` — programări

#### `AppointmentType`

| Câmp | Tip | Oblig. | Reguli |
|---|---|---|---|
| `id` | uuid | AUTO | |
| `code` | text (40) | DA | Unic |
| `name` | text (200) | DA | |
| `description` | text (1000) | NU | |
| `location` | text (250) | NU | Ghișeul sau sala unde are loc |
| `duration_minutes` | numeric (int) | DA | Durata unui interval, folosită la generarea intervalelor |
| `requires_confirmation` | boolean | DA | Implicit `true`. Când e `false`, programarea se confirmă automat la solicitare. |
| `max_days_ahead` | numeric (int) | DA | Implicit `30`. Cât de departe în viitor se poate solicita o programare. |
| `is_active` | boolean | DA | Implicit `true` |

#### `AppointmentSlot` — interval disponibil

| Câmp | Tip | Oblig. | Reguli |
|---|---|---|---|
| `id` | uuid | AUTO | |
| `appointment_type_id` | referință → `AppointmentType` | DA | |
| `starts_at` | dată+oră (UTC) | DA | |
| `ends_at` | dată+oră (UTC) | DA | `CHECK (ends_at > starts_at)` |
| `capacity` | numeric (int) | DA | Implicit `1`. Numărul de programări acceptate în interval. |
| `booked_count` | numeric (int) | AUTO | Implicit `0`. Se incrementează la rezervare, se decrementează la anulare sau respingere. |
| `is_blocked` | boolean | DA | Implicit `false`. Intervalul blocat nu mai apare ca disponibil, dar programările existente rămân. |

**Constrângeri:** `UNIQUE (appointment_type_id, starts_at)`; `CHECK (booked_count >= 0 AND booked_count <= capacity)`.

Rezervarea incrementează `booked_count` printr-un `UPDATE` condiționat, atomic, executat în aceeași tranzacție cu inserarea programării:

```sql
UPDATE portal.appointment_slot
   SET booked_count = booked_count + 1
 WHERE id = @slotId AND is_blocked = false AND booked_count < capacity
RETURNING booked_count;
```

Dacă instrucțiunea nu întoarce niciun rând, intervalul este plin sau blocat, iar solicitarea se respinge cu `409`. Nu se acceptă implementarea prin citirea prealabilă a lui `booked_count` urmată de o scriere separată.

#### `Appointment`

| Câmp | Tip | Oblig. | Reguli |
|---|---|---|---|
| `id` | uuid | AUTO | |
| `slot_id` | referință → `AppointmentSlot` | DA | |
| `user_id` | referință → `User` | AUTO | Din contextul de autentificare |
| `status` | enum | AUTO | `Requested` \| `Confirmed` \| `Rejected` \| `Cancelled` \| `Completed` \| `NoShow`. Implicit `Requested`, sau `Confirmed` dacă tipul nu necesită confirmare. |
| `notes` | text (1000) | NU | Observațiile solicitantului |
| `decision_note` | text (1000) | NU | Motivul deciziei; obligatoriu la `Rejected` |
| `decided_by_user_id` | referință → `User` | AUTO | |
| `decided_at` | dată+oră (UTC) | AUTO | |
| `reference_code` | text (20) | AUTO | Cod scurt afișat solicitantului, unic |

**Tranziții permise:**

```
Requested → Confirmed | Rejected | Cancelled
Confirmed → Completed | NoShow | Cancelled
Rejected  → (finală)
Cancelled → (finală)
Completed → (finală)
NoShow    → (finală)
```

Cetățeanul poate anula până la începerea intervalului; după acest moment, anularea se respinge cu `422`. Anularea și respingerea eliberează capacitatea intervalului, în aceeași tranzacție cu schimbarea stării. `UNIQUE (slot_id, user_id)` pentru programările active împiedică rezervarea dublă a aceluiași interval de către același utilizator.

---

### 8.7. Integrare

Câte un set în fiecare schemă, după rol.

#### `OutboxMessage`

| Câmp | Tip | Oblig. | Reguli |
|---|---|---|---|
| `id` | uuid | AUTO | |
| `aggregate_type` / `aggregate_id` | text (60) / uuid | AUTO | |
| `event_type` | text (80) | AUTO | Ex. `submission.registered` |
| `payload` | jsonb | AUTO | Corpul exact care se va trimite. Nu conține valori volatile: URL-uri semnate, momente de expirare. |
| `status` | enum | AUTO | `Pending` \| `Delivered` \| `Failed` |
| `attempts` | numeric (int) | AUTO | |
| `next_attempt_at` | dată+oră (UTC) | AUTO | |
| `last_error` | text (2000) | AUTO | |
| `delivered_at` | dată+oră (UTC) | AUTO | |

Mesajul se inserează în aceeași tranzacție cu modificarea de stare care l-a produs. Un lucrător de fundal îl livrează, cu retry exponențial (5 s, 30 s, 2 min, 10 min, 1 h; maximum 8 încercări), după care trece în `Failed`. Corpul trimis este exact `payload`, octet cu octet, la fiecare încercare.

#### `InboxEvent`

| Câmp | Tip | Oblig. | Reguli |
|---|---|---|---|
| `event_id` | uuid | DA | Cheie primară. Un `INSERT` care intră în conflict înseamnă „deja procesat” → `200` fără efect. |
| `source` | text (40) | AUTO | `dms` \| `portal` |
| `event_type` | text (80) | AUTO | |
| `payload` | jsonb | AUTO | |
| `processed_at` | dată+oră (UTC) | AUTO | |

#### `InboundRequest`

| Câmp | Tip | Oblig. | Reguli |
|---|---|---|---|
| `endpoint` | text (100) | AUTO | Ruta logică. Împreună cu cheia, formează cheia primară compusă. |
| `idempotency_key` | text (100) | AUTO | |
| `request_hash` | text (64) | AUTO | SHA-256 peste corpul brut, octet cu octet, exact cum a fost primit |
| `response_status` | numeric (int) | AUTO | Codul returnat la prima execuție |
| `response_body` | jsonb | AUTO | Răspunsul original |

---

### 8.8. Rapoarte — `ReportDefinition`

Entitatea există în ambele scheme. Fiecare serviciu deține definițiile rapoartelor peste propriile date și își expune propriul motor de rulare. Motorul se implementează o singură dată, ca bibliotecă partajată, și se instanțiază în fiecare API cu propria listă de seturi de date.

| Câmp | Tip | Oblig. | Reguli |
|---|---|---|---|
| `id` | uuid | AUTO | |
| `code` | text (50) | DA | Unic în schema sa, folosit în rută |
| `name` | text (200) | DA | |
| `dataset_key` | text (50) | DA | Trebuie să existe în lista de seturi de date a serviciului (§12.2) |
| `definition` | jsonb | DA | Structura din §12.1 |
| `version` | numeric (int) | AUTO | Se incrementează la fiecare salvare |
| `is_system` | boolean | DA | Rapoartele de sistem nu se pot șterge |
| `updated_by_user_id` | referință → `User` | AUTO | |

---

## 9. Stările cererii și ale poziției de registratură

Se folosește aceeași enumerare în ambele servicii.

| Stare | Semnificație pentru cetățean |
|---|---|
| `Submitted` | Cerere depusă, încă neînregistrată |
| `Registered` | Cerere înregistrată, are număr de înregistrare |
| `InReview` | Cerere în curs de soluționare |
| `InfoRequested` | Se solicită clarificări sau documente suplimentare |
| `Completed` | Cerere finalizată, răspunsul este disponibil |
| `Rejected` | Cerere respinsă, cu motiv |
| `Cancelled` | Cerere anulată |

**Tranziții permise:**

```
Submitted     → Registered | Cancelled
Registered    → InReview | Cancelled
InReview      → InfoRequested | Completed | Rejected
InfoRequested → InReview | Cancelled
Completed     → (finală)
Rejected      → (finală)
Cancelled     → (finală)
```

`Cancelled` se poate declanșa de cetățean, prin retragerea cererii în stările `Submitted`, `Registered` și `InfoRequested`, sau de funcționar, cu motiv obligatoriu.

Tranziția se validează pe server. Orice tranziție în afara tabelului → `422` și eveniment de audit. Trecerea în `Rejected`, `InfoRequested` sau `Cancelled` fără motiv completat → `422`. Trecerea în `Completed` fără document de răspuns atașat → `422`.

---

## 10. Contractul de integrare

### 10.1. Autentificare între servicii

Toate apelurile între API-PORTAL și API-DMS poartă:

```
Authorization: Bearer <secret partajat, din configurație>
X-Timestamp: <ISO 8601 UTC>
X-Signature: sha256=<HMAC-SHA256(secret, X-Timestamp + "." + corp brut)>
```

Serverul respinge cu `401` dacă semnătura nu corespunde sau dacă `X-Timestamp` iese dintr-o fereastră de ±5 minute. Credențialele serviciu-la-serviciu nu se transmit în șirul de interogare. Singurele token-uri admise în URL sunt token-urile semnate de descărcare, destinate browserului utilizatorului final.

### 10.2. Portal → DMS: înregistrarea cererii

```http
POST /api/registry-entries
Idempotency-Key: 8f2c1e6a-...
Content-Type: application/json

{
  "externalId": "8f2c1e6a-...",
  "registryTypeCode": "REG-GEN",
  "serviceCode": "certificat-urbanism",
  "direction": "In",
  "subject": "Cerere certificat de urbanism",
  "applicant": {
    "name": "Ionescu Maria", "nationalId": "2900101123456",
    "email": "maria@example.ro", "phone": "0722123456",
    "address": "Str. Republicii nr. 12, Craiova"
  },
  "submittedAt": "2026-08-07T09:14:22Z",
  "formValues": { "nume": "Ionescu Maria", "suprafata": 250 },
  "documents": [
    { "fileId": "3b91...", "name": "plan.pdf", "contentType": "application/pdf",
      "sizeBytes": 184322, "sha256": "9f2a...", "documentKindCode": "ANEXA" }
  ]
}
```

Răspuns `201`:

```json
{
  "entryId": "b7d4...",
  "registryTypeCode": "REG-GEN",
  "registryName": "Registru general intrări-ieșiri",
  "number": 1247,
  "year": 2026,
  "displayNumber": "1247/2026",
  "registeredAt": "2026-08-07T09:14:25Z",
  "status": "Registered"
}
```

`entryId` este identificatorul intern al poziției, iar `number` este numărul de registratură; sunt concepte distincte.

**Reguli de idempotență:**
- aceeași cheie și același `request_hash` → se returnează exact `response_status` și `response_body` persistate la prima execuție, fără a consuma un număr nou;
- aceeași cheie și corp diferit → `409`;
- cheia se interpretează în contextul rutei;
- alocarea numărului, inserarea poziției și salvarea răspunsului se fac într-o singură tranzacție.

### 10.3. DMS → Portal: evenimente de stare

```http
POST /api/callbacks/registry-events

{
  "eventId": "c4d2...",
  "entryId": "b7d4...",
  "externalId": "8f2c1e6a-...",
  "status": "Completed",
  "message": "Cererea a fost soluționată. Documentul de răspuns este disponibil.",
  "occurredAt": "2026-08-12T11:02:00Z",
  "responseDocument": {
    "documentId": "7a10...", "name": "raspuns.pdf",
    "contentType": "application/pdf", "sizeBytes": 220144, "sha256": "e11c..."
  }
}
```

Portalul răspunde `200 {"received": true}` și deduplică pe `eventId`. Orice alt cod determină reîncercarea. Metadatele complete sunt obligatorii, pentru ca portalul să poată crea `SubmissionFile` fără a avea conținutul.

### 10.4. Endpoint-uri de integrare

| Metodă | Rută | Sens | Rol |
|---|---|---|---|
| `POST` | `/api/registry-entries` | Portal → DMS | Înregistrarea cererii |
| `GET` | `/api/registry-entries/{entryId}` | Portal → DMS | Reconciliere și diagnoză |
| `POST` | `/api/registry-entries/{entryId}/documents` | Portal → DMS | Atașare ulterioară |
| `POST` | `/api/callbacks/registry-events` | DMS → Portal | Evenimente de stare |
| `POST` | `/api/internal/documents/{id}/download-url` | Portal → DMS | Obținerea unui URL semnat pentru documentul de răspuns |
| `GET` | `/api/internal/files/{fileId}/content` | DMS → Portal | Descărcarea conținutului unui fișier al cererii |
| `GET` | `/api/integration/outbox?status=Failed` | ambele | Alimentează ecranele de monitorizare |
| `POST` | `/api/integration/outbox/{id}/retry` | ambele | Reîncercare manuală |

Rutele `/api/internal/*` sunt accesibile exclusiv cu autentificarea serviciu-la-serviciu și nu se expun browserului.

### 10.5. Accesul la documente

Documentele nu se expun prin URL permanent cu identificator ghicibil. Descărcarea se face cu token semnat, cu expirare de 15 minute, emis la cerere, după verificarea autorizării. După expirare → `403`.

Pentru documentul de răspuns, aflat fizic în DMS, lanțul este: cetățean → portal, care verifică apartenența cererii → `POST /api/internal/documents/{id}/download-url` → DMS returnează URL-ul semnat → portalul redirecționează browserul.

Documentele din registrele publice se descarcă fără autentificare, dar tot prin URL semnat generat la cerere.

### 10.6. Transferul fișierelor între servicii

Fișierele nu se transmit în corpul cererilor de integrare și nu se trimit ca URL semnat în interiorul unui payload de outbox.

- Corpul cererii poartă doar metadate: `fileId`, `name`, `contentType`, `sizeBytes`, `sha256`, `documentKindCode`.
- Destinatarul descarcă conținutul separat, prin `GET /api/internal/files/{fileId}/content`, cu autentificarea din §10.1.
- Descărcarea se face înainte de a răspunde `201`, în cadrul aceleiași unități de lucru.

Un URL semnat inclus în `payload` ar expira înaintea ultimelor reîncercări din outbox, iar re-semnarea lui la fiecare încercare ar schimba corpul cererii și ar transforma o reîncercare legitimă într-un `409`.

---

## 11. Schema formularului

Formularul este date, nu cod. Nu se stochează HTML și nu se stochează JavaScript.

### 11.1. Structura

```jsonc
{
  "sections": [
    {
      "key": "solicitant",
      "title": "Date solicitant",
      "fields": [
        {
          "key": "nume",
          "label": "Nume și prenume",
          "type": "text",
          "required": true,
          "maxLength": 200,
          "placeholder": "",
          "helpText": "",
          "defaultValue": null,
          "visibleWhen": null
        }
      ]
    }
  ]
}
```

Versiunea schemei nu se stochează în JSON; sursa unică de adevăr este coloana `ServiceDefinition.schema_version`.

### 11.2. Tipuri de câmpuri

| `type` | Randare | Validări suportate |
|---|---|---|
| `text` | câmp text pe un rând | `required`, `minLength`, `maxLength`, `pattern` |
| `textarea` | câmp text multi-rând | `required`, `maxLength` |
| `number` | câmp numeric | `required`, `min`, `max`, `integer` |
| `date` | selector de dată | `required`, `minDate`, `maxDate`, `notInFuture` |
| `boolean` | bifă | `required` = „trebuie bifat” |
| `select` | listă derulantă | `required`, `options: [{value,label}]` |
| `radio` | butoane radio | ca `select` |
| `checkboxes` | selecție multiplă | `required`, `options`, `minSelected`, `maxSelected` |
| `email` | text cu validare de adresă | `required` |
| `phone` | text cu validare de telefon | `required` |
| `nationalId` | text cu validare CNP | `required`, verificarea cifrei de control |
| `file` | încărcare fișier | `required`, `accept`, `maxSizeMb` |

Opțional: `heading` și `paragraph`, elemente de prezentare fără valoare.

### 11.3. Reguli de comportament

1. Cheile de câmp sunt stabile și nu se reutilizează. Ștergerea unui câmp nu permite refolosirea cheii pentru alt câmp; se validează la salvarea schemei.
2. Aceeași validare se aplică pe server și în front-end. Sursa de adevăr este schema; front-end-ul o interpretează, serverul o reaplică. `POST` direct cu date invalide → `422`, cu `errors` pe chei de câmp.
3. Câmpurile necunoscute în `values` se resping cu `422`, nu se ignoră.
4. Randarea este generică: în portalul public nu există cod scris pentru un anumit serviciu. Adăugarea unui serviciu nou din backoffice produce un formular funcțional fără modificare de cod și fără redeploy.
5. Modificarea schemei nu afectează cererile deja depuse, care se randează din instantaneul propriu.

### 11.4. Vizibilitate condiționată

Un câmp poate avea `visibleWhen: { "field": "tip_solicitant", "equals": "persoana_juridica" }`. Când condiția este falsă, câmpul nu se afișează, iar validarea `required` nu se aplică — regulă respectată și pe server.

### 11.5. Reprezentarea valorilor

| Tip câmp | Formă în `values` |
|---|---|
| `text`, `textarea`, `email`, `phone`, `nationalId` | șir |
| `number` | număr |
| `date` | șir `"AAAA-LL-ZZ"` |
| `boolean` | `true` / `false` |
| `select`, `radio` | șirul `value` al opțiunii alese |
| `checkboxes` | listă de șiruri `value` |
| `file` | obiect `{ "fileId": "<uuid>" }`, care trebuie să corespundă unui `SubmissionFile` cu `field_key` egal cu cheia câmpului |

`maxSizeMb` și `accept` de la nivel de câmp se aplică fișierului acelui câmp. `requires_attachment` și `max_attachments` de la nivel de serviciu se aplică exclusiv atașamentelor generale, cele fără `field_key`. Cele două seturi nu se cumulează.

---

## 12. Rapoarte

Raportul este o definiție salvată în baza de date. Se livrează motorul de rulare, previzualizarea, exportul și un editor de definiție.

### 12.1. Structura definiției

```jsonc
{
  "renderMode": "table",                  // "table" (implicit) | "record"
  "datasetKey": "registry_entries",
  "parameters": [
    { "name": "registryTypeId", "type": "lookup", "source": "registry_types",
      "label": "Registru", "required": true },
    { "name": "dateFrom", "type": "date", "label": "De la data", "required": true },
    { "name": "dateTo",   "type": "date", "label": "Până la data", "required": true }
  ],
  "columns": [
    { "field": "number",         "label": "Nr.",        "type": "number", "align": "center", "widthPct": 7 },
    { "field": "registered_at",  "label": "Data",       "type": "date",   "align": "center", "widthPct": 10,
      "format": "dd.MM.yyyy" },
    { "field": "applicant_name", "label": "Solicitant", "type": "text",   "align": "left",   "widthPct": 22 },
    { "field": "subject",        "label": "Obiectul lucrării", "type": "text", "align": "left", "widthPct": 41 },
    { "field": "status",         "label": "Stare",      "type": "code",   "align": "center", "widthPct": 20 }
  ],
  "sort":    [{ "field": "number", "dir": "asc" }],
  "groupBy": null,
  "totals":  [{ "field": "number", "agg": "count" }],
  "layout":  { "orientation": "landscape", "title": "Registru intrări-ieșiri",
               "subtitle": "Perioada {dateFrom} – {dateTo}", "showPageNumbers": true }
}
```

Tipuri de parametru admise: `date`, `int`, `string`, `bool`, `lookup`.
Surse `lookup` admise: în API-DMS — `registry_types`, `departments`, `document_kinds`, `statuses`; în API-PORTAL — `service_definitions`, `appointment_types`, `statuses`.
În `title` și `subtitle` se acceptă exclusiv `{numeParametru}`, pentru parametri declarați în aceeași definiție; un placeholder nedeclarat invalidează definiția la salvare.

`format` descrie prezentarea valorii în raport și se aplică de randor.

### 12.2. Seturi de date

| Serviciu | Seturi disponibile |
|---|---|
| API-DMS | `registry_entries`, `tasks` |
| API-PORTAL | `submissions`, `appointments` |

Fiecare set declară explicit coloanele și tipurile lor. Parametrii se leagă ca parametri de comandă. Nu se acceptă SQL liber introdus din interfață și nu se construiește SQL prin concatenare de șiruri. Un raport nu interoghează schema celuilalt serviciu.

### 12.3. Previzualizare

`GET /api/report-definitions/{code}/preview?...` întoarce:

```json
{ "columns": [...], "rows": [...], "totals": {...},
  "meta": { "page": 1, "pageSize": 25, "total": 213 } }
```

Aceeași componentă front-end randează previzualizarea și pagina destinată exportului.

### 12.4. Editorul de definiție

Ecran care permite:

1. alegerea setului de date;
2. lista coloanelor disponibile, cu selectare prin bifă;
3. lista coloanelor selectate, reordonabilă prin drag & drop, cu editare inline pentru etichetă, aliniere, lățime și format;
4. definirea parametrilor;
5. gruparea pe un singur nivel și totaluri simple (`count`, `sum`, `avg`) pe coloane numerice;
6. previzualizare live pe date reale;
7. salvare, cu incrementarea versiunii.

Editorul nu oferă poziționare liberă pe canvas, benzi de raport, subrapoarte, expresii calculate sau grupări imbricate.

### 12.5. Reguli de aspect impuse de motor

Se aplică modului `table`:

1. Tipul coloanei determină alinierea implicită: `number`, `date`, `code` → centrat; `text` → stânga. Antetul moștenește alinierea coloanei sale.
2. Padding-ul celulelor de antet este identic cu cel al celulelor de detaliu.
3. Toate celulele de antet au aceeași aliniere verticală.
4. Antetul permite trecerea pe rând nou, fără trunchiere tăcută a titlului.
5. Datele se aliniază sus pe tot rândul, inclusiv valorile numerice.
6. Suma lățimilor coloanelor se validează la salvare: exact 100 %.

### 12.6. Export PDF

`POST /api/report-definitions/{code}/export` randează pe server aceeași pagină HTML ca previzualizarea, prin Chromium fără interfață (Playwright pentru .NET), cu:

- `@page` pentru dimensiune și margini;
- antet de tabel repetat pe fiecare pagină, prin `display: table-header-group`;
- numerotarea paginilor prin `footerTemplate`, cu clasele `pageNumber` și `totalPages`. Contoarele `page`/`pages` din CSS paged media nu sunt implementate de Chromium și nu se pot folosi.

Se acceptă și randarea prin QuestPDF, din aceeași definiție JSON; în acest caz, regulile de aspect din §12.5 se reimplementează integral în al doilea randor.

**Criteriu de acceptanță:** PDF generat pe date reale, cu diacritice românești corecte (ș, ț cu virgulă), antet repetat, orientare corectă, verificat inclusiv pe ultima pagină. Fonturile necesare se includ în imaginea de container.

Export CSV/XLSX din același set de date — opțional.

### 12.7. Rapoarte livrate

| Cod | Serviciu | `renderMode` | Conținut |
|---|---|---|---|
| `registru-intrari-iesiri` | API-DMS | `table` | Parametri: registru, interval de date; coloanele din §12.1; total = numărul de poziții |
| `dovada-inregistrare` | API-DMS | `record` | Document pe o singură poziție, în format etichetă:valoare; se folosește și ca dovadă generată la depunere |

În modul `record`, regula sumei de 100 % și §12.5 nu se aplică.

---

## 13. Cerințe pe aplicație

Prioritate: **Obligatoriu** / **Opțional**.

### 13.1. APP-PUB — portalul public

| # | Cerință | Prioritate |
|---|---|---|
| P1 | Pagină de pornire cu serviciile publicate, articolele publicate, chestionarele active și registrele publicate | Obligatoriu |
| P2 | Pagină de articol (`/articole/{slug}`), randând HTML sanitizat | Obligatoriu |
| P3 | Înregistrare cont, confirmarea adresei, autentificare, resetare parolă, deconectare | Obligatoriu |
| P4 | Pagina serviciului: descriere și formular randat generic din schemă | Obligatoriu |
| P5 | Depunere cerere: validare, atașamente, confirmare cu identificator afișat | Obligatoriu |
| P6 | „Cererile mele”: listă paginată, cu stare și număr de înregistrare | Obligatoriu |
| P7 | Detaliu cerere: valorile completate randate din instantaneu, cronologia, fișierele, descărcarea răspunsului | Obligatoriu |
| P8 | Încărcare de clarificări când cererea este în `InfoRequested` | Obligatoriu |
| P9 | Retragerea cererii, în stările permise | Obligatoriu |
| P10 | Lista chestionarelor active, completarea unui chestionar, afișarea rezultatelor agregate când chestionarul o permite | Obligatoriu |
| P11 | Lista tipurilor de programare, calendarul intervalelor libere, solicitarea unei programări | Obligatoriu |
| P12 | „Programările mele”: listă cu stare și cod de referință, anulare până la începerea intervalului | Obligatoriu |
| P13 | Lista registrelor publicate; consultarea unui registru cu căutare după titlu și interval de date; descărcarea documentelor, fără autentificare | Obligatoriu |
| P14 | Interfață utilizabilă pe mobil (390×844) și desktop (1280×800) | Obligatoriu |
| P15 | Formularul păstrează datele completate la eroare de validare | Obligatoriu |
| P16 | Descărcarea dovezii de înregistrare în PDF, prin motorul de rapoarte | Opțional |
| P17 | Listă de notificări în cont, cu marcare ca citite | Opțional |

### 13.2. APP-BO — backoffice-ul portalului

| # | Cerință | Prioritate |
|---|---|---|
| B1 | Autentificare, acces restricționat la rolul `Admin` | Obligatoriu |
| B2 | CRUD servicii publice, cu publicare și depublicare | Obligatoriu |
| B3 | Editor de formular: adăugare, ștergere și reordonare de secțiuni și câmpuri, alegerea tipului, setarea validărilor, previzualizare live | Obligatoriu |
| B4 | Avertizare la modificarea schemei unui serviciu cu cereri depuse; versiunea se incrementează la orice modificare a schemei | Obligatoriu |
| B5 | CRUD articole, cu editor WYSIWYG și sanitizare la salvare | Obligatoriu |
| B6 | CRUD chestionare și întrebări, cu publicare, perioadă de disponibilitate și previzualizare | Obligatoriu |
| B7 | Vizualizarea rezultatelor unui chestionar: agregate pe întrebare și lista răspunsurilor individuale | Obligatoriu |
| B8 | CRUD tipuri de programare | Obligatoriu |
| B9 | Generarea intervalelor de programare pe o perioadă și un orar declarate, cu capacitate; blocarea unui interval | Obligatoriu |
| B10 | Lista programărilor, cu filtre pe tip, stare și interval; confirmarea, respingerea cu motiv, marcarea ca onorată sau neprezentată | Obligatoriu |
| B11 | CRUD registre publice, poziții și documente atașate, cu publicare la ambele niveluri | Obligatoriu |
| B12 | Listă cereri, cu filtre pe serviciu, stare, interval de date și căutare pe solicitant | Obligatoriu |
| B13 | CRUD definiții de rapoarte și designer, peste seturile `submissions` și `appointments` | Obligatoriu |
| B14 | Ecran de monitorizare a outbox-ului portalului: mesaje `Pending` și `Failed`, ultima eroare, reîncercare manuală | Obligatoriu |
| B15 | Administrare utilizatori: listă, activare, dezactivare, schimbare rol | Opțional |

### 13.3. APP-DMS — registratura

| # | Cerință | Prioritate |
|---|---|---|
| D1 | Autentificare, acces pentru `Clerk` și `Admin` | Obligatoriu |
| D2 | CRUD registre și tipuri de document, cu închiderea registrelor; interzicerea ștergerii registrelor care au poziții | Obligatoriu |
| D3 | Listă poziții: filtre pe registru, an, interval de date, interval de numere, stare și compartiment; căutare pe obiect și solicitant; paginare pe server | Obligatoriu |
| D4 | Înregistrare manuală a unei poziții, cu alocarea numărului din contor | Obligatoriu |
| D5 | Detaliu poziție: date, valorile formularului, documentele de intrare și de ieșire cu metadatele lor, repartizări, cronologie | Obligatoriu |
| D6 | Acțiuni de tranziție: preluare, solicitare de clarificări, respingere cu motiv, finalizare cu document de răspuns, anulare cu motiv | Obligatoriu |
| D7 | Repartizare către utilizator și/sau compartiment, cu termen; marcarea ca rezolvată, cu notă | Obligatoriu |
| D8 | Încărcare și descărcare de documente, cu metadate (tip, dată, emitent, observații), validarea tipului după conținut și limită de dimensiune | Obligatoriu |
| D9 | Rapoarte: listă, previzualizare cu parametri, export PDF; designer peste seturile `registry_entries` și `tasks` | Obligatoriu |
| D10 | Ecran de monitorizare a outbox-ului DMS-ului, cu reîncercare manuală | Obligatoriu |
| D11 | CRUD compartimente | Opțional |
| D12 | Indicator vizual pentru pozițiile cu termen depășit | Opțional |

---

## 14. Cerințe non-funcționale

| # | Cerință | Prioritate |
|---|---|---|
| N1 | O singură comandă `docker compose up` ridică PostgreSQL, ambele API-uri, front-end-urile și stocarea; migrațiile se aplică automat | Obligatoriu |
| N2 | Seed idempotent, rulat la pornire: 2 registre, 4 tipuri de document, 3 servicii cu scheme de formular diferite, 2 articole, 1 chestionar cu răspunsuri, 1 tip de programare cu intervale, 1 registru public cu poziții și documente, 5 utilizatori acoperind toate rolurile, poziții de registratură pe 2 ani, cereri în stări diferite, 2 definiții de rapoarte | Obligatoriu |
| N3 | README cu scenariul de demonstrație și conturile de test | Obligatoriu |
| N4 | Configurația sensibilă exclusiv prin variabile de mediu; niciun secret în repo | Obligatoriu |
| N5 | Logare structurată, cu `traceId` propagat între servicii prin antet | Obligatoriu |
| N6 | Parametrizare SQL, sanitizare HTML, `Content-Security-Policy` de bază, protecție CSRF pe fluxurile cu cookie | Obligatoriu |
| N7 | Interogările de listă nu produc N+1 | Opțional |
| N8 | Documentație OpenAPI expusă pentru ambele API-uri | Opțional |
| N9 | Document scurt de arhitectură: decizii luate și motivele lor | Opțional |

---

## 15. Teste automate

| # | Test | Ce demonstrează |
|---|---|---|
| T1 | 50 de cereri paralele de înregistrare pe același registru și an → 50 de numere distincte, consecutive, fără duplicate și fără goluri | Corectitudinea contorului |
| T2 | Două cereri paralele cu aceeași cheie de idempotență și același corp → o singură poziție, un singur număr consumat, răspunsuri identice | Idempotență |
| T3 | Aceeași cheie de idempotență, corp diferit → `409` | |
| T4 | Același `eventId` de callback trimis de două ori → un singur efect, ambele răspunsuri `200` | Deduplicare |
| T5 | API-DMS oprit temporar în timpul depunerii → după repornire, cererea ajunge înregistrată o singură dată | Outbox și retry |
| T6 | Tranziție invalidă (`Completed` → `InReview`) → `422` și eveniment de audit | Mașina de stări |
| T7 | Finalizare fără document de răspuns → `422` | |
| T8 | `POST` direct cu payload care încalcă schema formularului (câmp obligatoriu lipsă, câmp necunoscut, lungime depășită) → `422`, cu erori pe chei de câmp | Validare pe server |
| T9 | Se modifică schema unui serviciu, apoi se randează o cerere depusă anterior → afișare corectă din instantaneu | Compatibilitate de schemă |
| T10 | N solicitări paralele pe un interval de programare cu capacitatea C → exact C confirmate, restul respinse cu `409`; `booked_count` nu depășește `capacity` | Rezervare atomică |
| T11 | Al doilea răspuns al aceluiași utilizator autentificat la același chestionar → `422` | Unicitatea răspunsului |
| T12 | `Citizen` cere datele altui utilizator → `403`; `Citizen` accesează o rută de backoffice → `403` | Autorizare |
| T13 | Descărcare de document după expirarea tokenului → `403` | |
| T14 | Server cu cultura `ro-RO` și fus `Europe/Bucharest` → momentele stocate și returnate rămân UTC corect | |
| T15 | Raportul `registru-intrari-iesiri` generat pe date fixe → număr de pagini și conținut așteptat, diacritice corecte | |

---

## 16. Etape de livrare

Fiecare etapă se încheie cu ceva rulabil și demonstrabil. Nu se trece mai departe cu criteriul de trecere neîndeplinit.

### Etapa 1 — Fundația

- Structura de soluție: cele două API-uri, front-end-ul, proiectele de teste
- `docker-compose` cu PostgreSQL, ambele API-uri, front-end și volum de fișiere
- Migrații inițiale, două scheme, doi utilizatori de bază de date cu drepturi disjuncte
- Autentificare completă și autorizare pe rol, în ambele servicii
- Seed minimal
- Un PDF generat de la un capăt la altul, cu diacritice, în containerul final

**Criteriu de trecere:** sistemul pornește pe o mașină curată, autentificarea funcționează în toate cele trei interfețe, un PDF se descarcă cu diacritice corecte.

### Etapa 2 — Registratura

- `RegistryType`, `DocumentKind`, `RegistryNumberCounter`, `RegistryEntry`, `RegistryDocument`, `Department`, `Task`, `EntryEvent`
- Contor atomic, constrângere unică — **T1**
- Registre, tipuri de document, listă poziții cu filtre și paginare, înregistrare manuală, detaliu poziție
- Încărcare și descărcare de documente cu metadate, prin URL semnat — **T13**
- Repartizări
- Mașina de stări validată pe server — **T6**, **T7**

**Criteriu de trecere:** se poate ține o registratură completă manual, fără portal.

### Etapa 3 — Servicii, formulare și conținut

- `ServiceDefinition`, `Submission`, `SubmissionFile`, `SubmissionEvent`, `Notification`, `Article`
- Validator de schemă și validator de valori față de schemă — **T8**
- Backoffice: servicii, editor de formular, articole
- Portal public: listă servicii, pagină serviciu, randare generică, depunere, „Cererile mele”, retragere
- Versionarea schemei și instantaneul — **T9**

**Criteriu de trecere:** se adaugă din backoffice un serviciu nou, cu formular nou, și acesta funcționează în portal fără modificare de cod.

### Etapa 4 — Integrarea

- `OutboxMessage`, `InboxEvent`, `InboundRequest`
- Contract HTTP complet, semnătură HMAC, idempotență
- Transferul fișierelor prin canalul intern, în ambele sensuri
- Lucrător de outbox cu retry; ecranele de monitorizare din ambele aplicații
- Callback cu deduplicare; generarea notificărilor
- Fluxul F1, capăt la capăt — **T2**, **T3**, **T4**, **T5**

**Criteriu de trecere:** fluxul F1 se execută integral, inclusiv cu DMS-ul oprit temporar în timpul depunerii.

### Etapa 5 — Chestionare, programări, registre publice

- `Survey`, `SurveyQuestion`, `SurveyResponse` — definire, publicare, completare, rezultate — **T11**
- `AppointmentType`, `AppointmentSlot`, `Appointment` — definire, generare de intervale, solicitare, confirmare, anulare — **T10**
- `PublicRegistry`, `PublicRegistryEntry`, `PublicRegistryDocument` — definire, publicare, consultare publică

**Criteriu de trecere:** fluxurile F5, F6 și F7 se execută integral, din backoffice în portalul public.

### Etapa 6 — Rapoarte

- `ReportDefinition` în ambele scheme, seturi de date declarate
- Motorul de rulare ca bibliotecă partajată, previzualizare paginată
- Designer de definiție în ambele aplicații private
- Export PDF cu regulile de aspect
- Cele două rapoarte livrate, inclusiv modul `record` — **T15**

**Criteriu de trecere:** se creează din interfață un raport nou, pe alt set de date, se previzualizează și se exportă, fără modificare de cod.

### Etapa 7 — Finisare

- Testele rămase: **T12**, **T14**
- Seed complet
- README și documentul de arhitectură
- Verificarea interfeței pe mobil și desktop
- Verificarea vizuală a PDF-urilor, inclusiv ultima pagină

---

## 17. Glosar

| Termen | Sens |
|---|---|
| **Poziție de registratură** | O intrare în registru: număr, an și registru, cu date și documente atașate |
| **Registru** | Nomenclator care grupează pozițiile și are propria numerotare pe an |
| **Registru public** | Colecție de documente publicate spre consultare în portal, fără legătură cu registratura internă |
| **Cerere** | Formularul completat și depus de cetățean pe portal |
| **Serviciu public** | Definiția publicată a unei cereri: descriere, schemă de formular și registru țintă |
| **Interval de programare** | Fereastră de timp cu capacitate, disponibilă pentru rezervare |
| **Outbox** | Tabel de evenimente de trimis, scris în aceeași tranzacție cu modificarea și livrat asincron |
| **Idempotență** | Repetarea aceleiași cereri produce același efect și același răspuns, o singură dată |
| **`external_id`** | Identificator generat de portal, unica cheie de corelare între cele două servicii |
| **Canal intern** | Rutele `/api/internal/*`, accesibile exclusiv serviciu-la-serviciu |
