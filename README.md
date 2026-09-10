Sistemul are trei componente principale: portalul public, backoffice-ul în care configurați serviciile și aplicația de registratură. Pentru testare am pregătit un mediu local bazat pe Docker Compose. La pornire, acesta ridică infrastructura necesară și încarcă automat datele de test.

Pentru a porni proiectul, deschideți un terminal în directorul în care se află fișierul docker-compose.yml. Scripturile PowerShell sunt pregătite pentru PowerShell 7 pe Windows. Docker Desktop trebuie să ruleze containere Linux.

Aveți nevoie și de SDK-ul .NET 9. În global.json este setată versiunea 9.0.317, dar puteți folosi și o versiune mai nouă din aceeași serie. SDK-ul instalat local este folosit pentru certificate și pentru rularea testelor. Aplicațiile propriu-zise se compilează în containere, iar pentru simpla pornire a mediului nu este necesară instalarea Node.js.

Înainte să porniți containerele, verificați și porturile. Sistemul folosește porturile 80 și 443 pentru interfețele web, 5432 pentru PostgreSQL și 1025 plus 8025 pentru MailHog. API-urile folosesc 5001/5002 și 7116/7106, iar aplicațiile front-end folosesc porturile 3000-3002.

Dacă unul dintre aceste porturi este ocupat de altă aplicație, trebuie să îl eliberați înainte să porniți mediul.

Proxy-ul folosește intern rețeaua 172.30.50.0/24. Dacă lucrați prin VPN, verificați să nu folosească același interval de adrese.

Următorul lucru de pregătit este fișierul .env. În repository există deja .env.example, pe care îl puteți folosi drept punct de plecare. 


Dacă preferați, puteți copia manual .env.example în .env și apoi să înlocuiți valorile CHANGE_ME_....

Cheile JWT, cheia folosită pentru integrare și cheile pentru descărcări trebuie să aibă cel puțin 32 de caractere. Trebuie verificată și valoarea HTTPS_CERT_PASSWORD: aceasta trebuie să fie aceeași cu parola folosită pentru fișierul PFX.

Este important de reținut că schimbarea valorilor din .env nu modifică parolele utilizatorilor deja salvați într-o bază PostgreSQL inițializată. Cele două sunt separate. În același mod, valorile de configurare din .env nu reprezintă automat datele folosite pentru autentificarea în interfața web.

Puteți folosi și fișierul .env aflat deja în folderul principal al proiectului.

API-urile expun endpoint-uri HTTPS, astfel că înainte să porniți containerele trebuie să existe un certificat local. Următorul script citește configurația din .env și exportă certificatul în certs/aspnetapp.pfx:

```powershell
. .\scripts\Test.Common.ps1
Import-LocalEnvironment (Get-Location).Path
Assert-RequiredEnvironmentValue 'HTTPS_CERT_PASSWORD'

New-Item -ItemType Directory -Force certs | Out-Null
dotnet dev-certs https --trust
if ($LASTEXITCODE -ne 0) { throw 'Certificatul de dezvoltare nu a putut fi pregatit.' }

dotnet dev-certs https --export-path .\certs\aspnetapp.pfx --password $env:HTTPS_CERT_PASSWORD
if ($LASTEXITCODE -ne 0) { throw 'Exportul certificatului PFX a esuat.' }
```

După ce ați terminat configurarea, porniți mediul cu:

```powershell
docker compose up --build -d
```

Prima pornire poate dura puțin mai mult deoarece Docker trebuie să construiască imaginile și să inițializeze serviciile. Dacă primele request-uri dau eroare imediat după pornire, verificați logurile și încercați din nou după ce serviciile au terminat inițializarea.

Pentru accesul din browser mai există un pas legat de certificate.

Caddy este folosit ca reverse proxy și își generează propriile certificate. Din acest motiv, comanda dotnet dev-certs de mai sus nu elimină avertismentul de securitate pentru portal.localhost.

Pe Windows, puteți copia certificatul root generat de Caddy din container și apoi să îl importați în certificatele utilizatorului:

```powershell
docker compose cp proxy:/data/caddy/pki/authorities/local/root.crt ./certs/caddy-root.crt
if ($LASTEXITCODE -ne 0) { throw 'Certificatul Caddy nu a putut fi copiat.' }

Import-Certificate -FilePath .\certs\caddy-root.crt -CertStoreLocation Cert:\CurrentUser\Root
```

După import, închideți și redeschideți browserul.

Firefox își gestionează certificatele separat în anumite configurații. Dacă folosiți Firefox și browserul continuă să afișeze avertismentul, importați manual fișierul caddy-root.crt din managerul de certificate al browserului.

Folosiți aplicațiile prin HTTPS. Autentificarea se bazează pe cookie-uri care au flag-ul Secure și folosește și Web Locks, deci accesarea variantelor HTTP nu este scenariul normal de utilizare.

| Resursă accesată | Adresă |
| --- | --- |
| Portalul cetățeanului | https://portal.localhost |
| Backoffice-ul Portalului | https://bo.localhost |
| Registratura DMS | https://dms.localhost |
| Swagger Portal | https://portal.localhost/swagger/index.html |
| Swagger DMS | https://dms.localhost/swagger/index.html |
| Interfața locală MailHog | http://localhost:8025 |

În mod normal, sistemul de operare rezolvă automat domeniile .localhost. Dacă pe computerul dumneavoastră nu funcționează, adăugați următoarea linie în fișierul hosts:

127.0.0.1 portal.localhost bo.localhost dms.localhost

La prima inițializare sunt create automat câteva conturi de test:

| Interfață | E-mail | Parolă | Rol |
| --- | --- | --- | --- |
| Portal public | citizen@portal.local | Citizen123!ChangeMe | Citizen |
| Backoffice | admin@portal.local | Admin123!ChangeMe | Admin |
| DMS | admin@example.com | Admin123!ChangeMe | Admin |
| DMS | clerk@example.com | Clerk123!ChangeMe | Clerk |
| DMS | clerk2@example.com | Clerk123!ChangeMe | Clerk |

Parolele sunt vizibile intenționat, pentru ca proiectul să poată fi verificat fără pregătirea manuală a conturilor. Acestea sunt conturi destinate mediului local de test.

Portalul și DMS folosesc baze separate de utilizatori. De exemplu, faptul că aveți rol de administrator în Portal nu vă oferă automat acces de administrator în DMS.

Puteți testa și crearea unui cont nou, precum și resetarea parolei. Mesajele trimise de aplicație ajung în MailHog, unde puteți deschide direct linkurile generate.

Mediul vine și cu date care permit testarea aplicației fără configurare suplimentară. Sunt incluse servicii predefinite, formulare, un chestionar, intervale pentru programări și câteva date istorice. În DMS sunt disponibile registrele registru-intrari-iesiri și dovada-inregistrare.

Dacă doar reporniți containerele, datele rămân salvate.

Pentru a verifica comunicarea dintre Portal și DMS, cel mai simplu este să trimiteți efectiv o cerere și să urmăriți traseul acesteia prin aplicație.

Puteți folosi scenariul următor:

1. Intrați în backoffice și modificați un serviciu existent sau creați unul nou. Asociați serviciul unui registru DMS care este deschis.
2. Autentificați-vă în Portal cu utilizatorul Citizen și trimiteți o cerere. După trimitere, deschideți secțiunea „Cererile mele”. Numărul final de înregistrare apare după ce cererea ajunge în DMS.
3. Intrați în DMS cu un cont Clerk. Căutați lucrarea nouă și preluați-o. Adăugați o repartizare, apoi solicitați clarificări și completați un motiv concret.
4. Reveniți în Portal pe contul Citizen. Ar trebui să vedeți motivul cererii de clarificare. Răspundeți și atașați documentele cerute. Acțiunile vor apărea și în cronologia din DMS.
5. Întoarceți-vă în DMS, încărcați un document de răspuns și închideți lucrarea. Portalul trebuie să primească noul status, iar cetățeanul trebuie să poată descărca documentul oficial.

Puteți testa în același flux și generarea dovezii de înregistrare sau exportul rapoartelor PDF.

Există și un mecanism de reîncercare pentru cererile trimise între componente. Îl puteți verifica fără să modificați codul.

Mai întâi, deschideți în Portal un serviciu deja configurat și autentificați utilizatorul Citizen. Apoi opriți API-ul DMS:

```powershell
docker compose stop api-dms
```

Trimiteți acum o cerere din Portal și deschideți ecranul Outbox din backoffice. Mesajul ar trebui să rămână în așteptare, deoarece DMS nu este disponibil.

Porniți din nou componenta:

```powershell
docker compose start api-dms
```

După repornire, sistemul va încerca din nou să livreze cererea.

Reîncercările sunt programate la 5 secunde, 30 de secunde, 2 minute și 10 minute. După aceea, sistemul încearcă din nou o dată pe oră, până la un maxim de cereri de 8.

Statusul Pending înseamnă că mesajul încă are încercări de livrare programate. Dacă apare Failed, mesajul a ajuns la opt eșecuri consecutive sau a întâlnit o eroare considerată nerecuperabilă.

În cazul unui mesaj Failed, administratorul poate porni din nou trimiterea direct din backoffice.

Există o diferență importantă între acest mecanism și publicarea serviciilor. Dacă api-dms este oprit și încercați să publicați un serviciu nou din backoffice, publicarea va eșua. Verificarea registrului selectat se face printr-un apel HTTP sincron, deci nu trece prin mecanismul Outbox descris mai sus.

Testele automate pot fi rulate separat. Pentru ele nu sunt necesare interfețele web, proxy-ul sau MailHog.

Aveți nevoie de SDK-ul .NET, Docker și dotnet-ef.

Dacă nu aveți dotnet-ef, instalați versiunea folosită de proiect, adică 9.0.19.

Modulul PDF folosește Playwright pentru randare. Din acest motiv, pentru testele locale trebuie să aveți Chromium instalat prin Playwright.

Puteți pregăti mediul și apoi să rulați testele astfel:

```powershell
dotnet build .\API-DMS\API-DMS.csproj
if ($LASTEXITCODE -ne 0) { throw 'Build-ul API-DMS a esuat.' }
pwsh .\API-DMS\bin\Debug\net9.0\playwright.ps1 install chromium
if ($LASTEXITCODE -ne 0) { throw 'Instalarea Chromium a esuat.' }

.\scripts\test-dms.ps1
.\scripts\test-portal.ps1
```


Imaginile Docker destinate producției instalează browserul în timpul build-ului. Pașii de mai sus sunt necesari doar atunci când doriți să rulați dotnet test direct pe computerul local.

Scripturile de test folosesc configurația din .env. Ele pornesc doar PostgreSQL, apoi creează bazele portal_dms_test și portal_portal_test, instalează extensia pg_trgm și aplică migrațiile necesare.

Variabilele de conectare sunt construite automat de script.

Extensia folosită pentru căutare se află în schema public. Din acest motiv, conexiunea DMS include parametrul SearchPath=dms,public.

Dacă preferați Bash, sunt disponibile și variantele:


```bash
bash scripts/test-dms.sh
bash scripts/test-portal.sh
```

Pentru recrearea bazelor de test puteți folosi -ResetDatabase în PowerShell sau --reset în Bash.

Nu este nevoie să rulați resetarea la fiecare test. Este utilă mai ales când doriți să verificați migrațiile de la zero sau când baza de test a rămas într-o stare pe care nu mai doriți să o păstrați.

Pentru front-end aveți nevoie de Node.js 24.

În fiecare dintre directoarele app-pub, app-bo și app-dms, rulați:

```powershell
npm ci
npm run lint
npm run build
```

Aceste comenzi verifică partea web. Pentru logica de backend, folosiți scripturile .NET descrise mai sus.

Pentru lucrul de zi cu zi sunt utile și comenzile Docker Compose de bază.

docker compose stop oprește temporar containerele fără să le șteargă.

docker compose start pornește din nou containerele oprite.

docker compose up --build -d reconstruiește imaginile și pornește serviciile. Folosiți varianta cu --build atunci când există modificări care trebuie incluse într-o imagine nouă.

docker compose down șterge containerele și rețeaua creată pentru ele, dar păstrează volumele. Datele rămân astfel pe disc.

Mare grijă la următoarea comandă:

```powershell
docker compose down -v
```

Aceasta șterge și volumele. Sunt eliminate baza de date, fișierele încărcate fizic, certificatele generate de Caddy și cheile Data Protection.

După o astfel de resetare, setările 2FA generate anterior nu mai sunt valabile. Va trebui și să importați din nou certificatul root generat de Caddy.

Folosiți docker compose down -v doar atunci când doriți să refaceți complet mediul.

Fișierul .env și certificatul local aspnetapp.pfx nu sunt șterse de această comandă, deoarece se află pe computerul local și nu în volumele Docker eliminate.

Mai există un caz de care trebuie să țineți cont dacă folosiți o versiune mai veche a proiectului. Identificatorii unor migrații s-au schimbat în istoricul de dezvoltare.

Dacă doriți să păstrați datele unei baze create cu o versiune mai veche de cod, poate fi nevoie să aliniați manual ID-urile din tabela __EFMigrationsHistory.

Dacă datele locale nu sunt importante, varianta mai simplă este să recreați volumele și să porniți cu o bază curată.

Pentru depanare, începeți cu:

```powershell
docker compose ps
```

Apoi verificați logurile serviciului care nu pornește sau care returnează erori.

Dacă lipsește fișierul PFX sau parola certificatului este greșită, API-ul se va opri la pornire.

Dacă modificați parola PostgreSQL în .env după ce baza de date a fost deja inițializată, aplicația poate începe să primească erori de conexiune. Schimbarea variabilei din .env nu modifică automat parola deja configurată în baza existentă.

Dacă primiți HTTP 500 și răspunsul conține un traceId, căutați eroarea corespunzătoare în logurile API-ului. Swagger arată contractul răspunsului de eroare, dar nu afișează automat excepția internă produsă în backend.

Dacă descărcați un raport PDF și fișierul rezultat nu se deschide, verificați răspunsul HTTP primit de browser. În unele cazuri, serverul poate răspunde cu un JSON care descrie eroarea, iar browserul îl poate salva totuși cu extensia .pdf.

Arhitectura aplicației este descrisă mai detaliat în fișierul [ARHITECTURA.md](https://www.google.com/search?q=ARHITECTURA.md).

Lista funcționalităților cerute inițial se găsește în [documentul cu cerințe](https://www.google.com/search?q=Cerinte-PoC-Portal-Servicii-DMS.md).
