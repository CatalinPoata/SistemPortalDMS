Aplicația este împărțită în două zone care trebuie să poată funcționa separat. Portalul primește cereri și publică informații pentru cetățeni, iar DMS-ul se ocupă de registratură și de activitatea funcționarilor. Documentul cu cerințe stabilește deja această separare, folosirea celor două scheme PostgreSQL și comunicarea HTTP dintre componente.

Backend-ul folosește ASP.NET Core 9, EF Core cu Npgsql și PostgreSQL 16. Pentru interfețele web există trei proiecte Next.js separate, toate construite cu App Router, React și TypeScript. API-PORTAL este folosit atât de app-pub, cât și app-bo, iar API-DMS este folosit de app-dms.

Cerințele permiteau separarea front-end-urilor, dar nu o impuneau. În varianta actuală am ales trei aplicații distincte. Fiecare își are propria navigare, propriile pagini de autentificare și componentele de care are nevoie. Astfel este destul de simplu să vezi ce ajunge în interfața cetățeanului și ce aparține zonei administrative.

Există și un cost. O parte din cod se repetă, în special cel pentru autentificare și raportare. Dacă modifici o componentă comună, este posibil să trebuiască să faci aceeași schimbare în mai multe proiecte, fapt ce a dus la multe debugging-uri.

În backend am organizat codul în jurul funcțiilor aplicației. Controller-ele sunt grupate pe module, DTO-urile descriu contractele HTTP, iar configurațiile EF stabilesc structura tabelelor și constrângerile bazei de date.

Operațiile care au o responsabilitate clară au primit servicii separate. Aici intră stocarea fișierelor, validarea formularelor, alocarea numerelor de registru și trimiterea mesajelor între componente.

Controller-ele folosesc însă și DbContext direct. Nu am introdus un repository generic între controller și EF Core. În proiectul acesta, un astfel de strat ar fi ascuns tocmai lucrurile care trebuie prezentate, cum ar fi proiecțiile SQL, interogările și limitele unei tranzacții.

Dezavantajul este că unele controllere au ajuns destul de mari. Dacă proiectul ar continua să crească, următorul pas logic ar fi mutarea fluxurilor mai complexe în servicii de aplicație, dar pornind de la operații concrete, nu de la un strat generic pus peste tot.

Shared.Reporting este o bibliotecă separată deoarece generarea PDF-urilor și regulile comune de raportare sunt folosite de ambele domenii. Ambele API-uri fac referire la ea.

Datele nu sunt însă citite centralizat. Fiecare API își selectează singur datele pe care le deține și apoi le trimite către partea comună de raportare. Biblioteca nu primește o conexiune care poate citi ambele scheme. Am putut astfel reutiliza codul de randare fără a introduce o cale indirectă prin care Portalul să ajungă la datele DMS sau invers.

În Docker Compose este folosită aceeași instanță PostgreSQL și aceeași bază portal_dms_db, dar separ datele prin scheme și conturi diferite. Fiecare DbContext își păstrează și tabela __EFMigrationsHistory în schema lui.

Detaliul acesta devine important când sunt pornite API-urile separat. Fiecare serviciu vede doar istoricul propriilor migrații și nu încearcă să interpreteze migrațiile celuilalt.

Scriptul init.sql creează schemele, utilizatorii și extensia pg_trgm. Tabelele aplicației sunt create ulterior prin migrațiile EF.

init.sql rulează când este creat volumul PostgreSQL, nu la fiecare restart. Din acest motiv, dacă modificați o parolă în .env după ce baza a fost inițializată, utilizatorul PostgreSQL deja existent nu își schimbă automat parola.

În DMS, SearchPath include dms,public. Tabelele aplicației rămân în schema dms, dar operatorii gin_trgm_ops folosiți de indexurile trigram vin din extensia instalată în public.

Asta nu oferă DMS-ului acces la datele Portalului. Este doar o dependență tehnică necesară pentru indexurile de căutare.

Enumerările sunt salvate ca text și au constrângeri în baza de date pentru valorile permise. Dacă deschideți tabela direct în SQL, puteți citi imediat o valoare precum registered sau completed, fără a căuta corespondența unui număr.

Am păstrat totuși și constrângerile în PostgreSQL. Faptul că în cod există un enum C# nu împiedică un alt client SQL să încerce să introducă o valoare greșită.

Browserul ajunge la aplicații prin Caddy. Pentru fiecare dintre cele trei domenii locale, Caddy trimite rutele /api și /swagger către backend, iar restul cererilor merg către aplicația Next.js potrivită.

Astfel, front-end-ul poate apela direct rute precum /api/requests folosind originea curentă. Nu a trebuit să introduc în fiecare build o adresă publică diferită pentru API și nici să încerc să partajez același cookie între mai multe domenii.

În Portal, Caddy adaugă și antetul X-Portal-Frontend. Prin el, API-ul poate vedea dacă cererea a venit prin aplicația publică sau prin backoffice.

Antetul nu înlocuiește autorizarea. Dacă o operație cere rol de administrator, API-ul verifică în continuare rolul utilizatorului. X-Portal-Frontend oferă doar informație despre punctul prin care a intrat cererea.

Caddy are o adresă fixă în rețeaua internă și API-urile îl declară drept proxy cunoscut. Astfel, antetele care păstrează schema și adresa cererii originale sunt acceptate doar când vin prin intermediarul așteptat.

Backend-ul poate vedea astfel că utilizatorul a accesat aplicația prin HTTPS chiar dacă între Caddy și container conexiunea este HTTP.

În mediul local am păstrat și porturile HTTPS directe ale API-urilor. Acestea folosesc un fișier PFX separat. Configurația adaugă un pas la instalarea mediului, dar accesul direct la API rămâne util atunci când voiam să verific dacă problema vine din backend sau din proxy.

Certificatele interne generate de Caddy și starea lui sunt salvate în volume, deci nu dispar la un simplu restart al containerelor.

Autentificarea respectă cerințele pentru JWT, refresh token și roluri separate. O decizie importantă este locul în care ținem efectiv sesiunea în browser.

Access token-ul rămâne doar în memoria aplicației. Refresh token-ul este trimis printr-un cookie HttpOnly, Secure și SameSite=Strict.

Dacă este reîncărcată, access token-ul din memorie dispare. Aplicația face atunci un refresh și își reconstruiește sesiunea. Apare o cerere în plus la încărcare, dar nu trebuie păstrat JWT-ul în localStorage.

Clientul HTTP știe să lucreze cu rute locale și tratează diferit răspunsurile JSON și fișierele binare. Este important pentru endpoint-urile de export. Un PDF valid și un răspuns ProblemDetails nu trebuie procesate în același mod doar pentru că vin de la același endpoint.

Rotirea refresh token-ului devine mai complicată când este deschisă aplicația în două file.

De exemplu, ambele file pot vedea același cookie și pot încerca să facă refresh aproape simultan. Prima cerere rotește token-ul. A doua ajunge cu token-ul vechi, iar sistemul ar putea interpreta situația drept reutilizare nepermisă și ar invalida sesiunea.

Pentru a evita cazul acesta, aplicațiile folosesc Web Locks. Operațiile de autentificare sunt serializate între filele care folosesc aceeași origine.

Pentru sincronizare am folosit și BroadcastChannel. Prin canal nu trimit token-ul. O filă anunță doar că sesiunea s-a schimbat, iar celelalte își citesc din nou starea.

În implementarea actuală, lock-ul este păstrat și în jurul unor apeluri autentificate. Soluția simplifică ordinea operațiilor, dar are și un efect secundar: o cerere lentă poate ține pe loc alte request-uri care așteaptă același lock.

Blocarea se aplică la nivel de origine. Portalul public și backoffice-ul rulează pe origini diferite, deci sesiunile lor rămân separate.

Pe server există și o formă de coordonare în PostgreSQL.

PostgresRefreshSessionLock pornește o tranzacție și blochează rândul utilizatorului cu SELECT ... FOR UPDATE. Un lock păstrat doar în memoria procesului nu ar fi suficient dacă, la un moment dat, ar fi pornite două instanțe ale aceluiași API.

Lock-ul pe utilizator ajută și la coordonarea dintre resetarea parolei și refresh. Dacă parola este resetată, nu este dorit ca o cerere concurentă să reușească să creeze imediat după aceea un refresh token care trebuia de fapt invalidat.

Testele care verifică această situație folosesc PostgreSQL real. Pentru testele în care blocarea nu contează există și implementarea fără lock folosită împreună cu infrastructura InMemory.

Pentru CSRF este folosit mecanismul antiforgery din ASP.NET Core.

Clientul cere un token de la /api/security/csrf, apoi îl trimite în antetul X-CSRF-TOKEN la operațiile protejate care se bazează pe cookie.

La operația de refresh, token-ul este cerut în context anonim. Pentru revocarea unei sesiuni deja autentificate, este obținut pentru identitatea utilizatorului curent.

Diferența contează pentru că token-ul antiforgery este legat de contextul în care a fost emis.

Caddy adaugă și o politică CSP de bază. Aceasta limitează folosirea paginilor în frame-uri, obiectele, adresa de bază și destinația formularelor. Nu am tratat configurația ca pe o politică CSP complet restrictivă pentru toate scripturile aplicației.

TOTP era opțional în cerințe, dar l-am implementat în ambele API-uri pentru rolurile care îl pot utiliza.

Configurația TOTP este ținută într-o entitate separată, UserTotp, nu direct lângă restul câmpurilor obișnuite ale utilizatorului.

Secretul este criptat cu Data Protection. Challenge-ul de login are alt scop de securitate și expiră după cinci minute.

Dacă utilizatorul are TOTP activ, o parolă corectă nu creează imediat sesiunea. Serverul generează mai întâi challenge-ul, iar sesiunea este emisă doar după verificarea celui de-al doilea factor.

Sistemul salvează ultimul contor TOTP acceptat pentru a împiedica folosirea de două ori a aceluiași cod. Sunt generate și zece coduri de recuperare. Acestea sunt hash-uite și fiecare poate fi consumat o singură dată.

La configurare, secretul TOTP trebuie totuși trimis către browser pentru ca utilizatorul să îl poată adăuga într-o aplicație de autentificare. Din acest motiv ar fi incorect spus că secretul nu ajunge niciodată în browser.

Cheile Data Protection sunt persistate separat pentru cele două API-uri.

Ele sunt folosite pentru mai mult decât cookie-urile antiforgery. Protejează și secretele TOTP și challenge-urile generate în timpul autentificării.

Dacă păstrezi baza de date, dar pierzi cheile Data Protection, poți rămâne cu informații criptate pe care aplicația nu le mai poate citi.

Volumele permit repornirea containerelor fără pierderea acestor chei. Într-un sistem folosit în producție, backup-ul bazei de date și backup-ul părții de securitate ar trebui tratate împreună.

În testele automate sunt folosite chei efemere. În felul acesta, rezultatele testelor nu depind de profilul Windows sau de configurația computerului pe care sunt rulate.

Pentru integrarea dintre Portal și DMS este folosit un outbox, așa cum cer specificațiile. Nu am adăugat un broker separat de mesaje.

Un mesaj din outbox este pur și simplu un rând în baza de date și este salvat în aceeași tranzacție cu operația de business care îl produce.

Un BackgroundService caută mesajele care trebuie trimise o dată la două secunde. Ia loturi de câte zece, în ordinea în care au fost create, apoi le livrează prin clienți HTTP dedicați.

Pentru un proiect demonstrativ, soluția rămâne simplu de pornit și ușor de verificat. Mesajele pot fi văzute direct în baza de date și în ecranele administrative, fără a mai fi configurezat încă un serviciu separat.

Livrarea este de tip „cel puțin o dată”. Dacă DMS salvează cererea, dar răspunsul HTTP se pierde pe drum, Portalul nu știe că operația a reușit și o va trimite din nou.

Din acest motiv, receptorul trebuie să poată recunoaște retransmiterile.

Autentificarea dintre servicii folosește contractul stabilit în cerințe: un secret comun în antetul Bearer, timestamp și semnătură HMAC-SHA256.

Filtrul citește corpul brut al cererii și verifică semnătura calculată peste timestamp.body. După verificare, fluxul este repoziționat pentru ca ASP.NET Core să poată face deserializarea normală.

Semnăturile sunt comparate cu FixedTimeEquals, iar timestamp-ul are o toleranță de cinci minute.

Acest mecanism este separat de autentificarea JWT a utilizatorilor.

HMAC-ul spune că mesajul vine de la serviciul așteptat și că body-ul nu a fost modificat. Nu rezolvă însă singur problema retransmiterilor. Aceeași cerere validă poate fi trimisă de două ori în intervalul acceptat.

Pentru acest caz, DMS are tabela InboundRequest.

Ea păstrează endpoint-ul, cheia de idempotență, hash-ul corpului, statusul HTTP și răspunsul produs.

Dacă primește din nou aceeași cheie, sistemul verifică mai întâi hash-ul cererii. Dacă payload-ul este același, poate întoarce rezultatul salvat anterior.

Implementarea recitește răspunsul și după prima salvare.

Motivul este jsonb. PostgreSQL poate normaliza JSON-ul atunci când îl stochează. Două răspunsuri pot avea exact același conținut logic, dar să difere ca ordine a proprietăților sau ca spațiere.

Dacă folosim pentru ambele situații reprezentarea citită din baza de date, primul răspuns și răspunsul de replay au aceeași formă.

Hash-ul este calculat peste body-ul brut. Asta înseamnă că, pentru aceeași cheie de idempotență, clientul trebuie să retransmită exact același payload.

Pe traseul invers, Portalul păstrează eventId într-un inbox și salvează efectele callback-ului în aceeași tranzacție cu informația că evenimentul a fost procesat.

Statusul unei cereri, intrarea din cronologie și notificarea utilizatorului sunt lucruri separate în modelul de date, chiar dacă toate pot apărea după același eveniment. Astfel le putem afișa, interoga și administra separat.

Dacă DMS trimite un document de răspuns, Portalul păstrează metadatele și identificatorul documentului din DMS. Conținutul fizic rămâne în DMS.

Cele două sisteme corelează cererea prin external_id. Numărul de registru rămâne însă numărul oficial pe care îl vede cetățeanul.

Retry-ul folosește întârzieri de 5 secunde, 30 de secunde, 2 minute, 10 minute și apoi o oră.

După opt încercări nereușite, mesajul trece în Failed. Același lucru se întâmplă dacă eroarea este clasificată de client drept una pentru care nu are sens să mai încerce.

Acest comportament previne trimiterea continuă către un serviciu care nu răspunde. Are însă și un efect vizibil: dacă serviciul revine online, mesajul nu pleacă obligatoriu în aceeași secundă. Poate aștepta următoarea fereastră de retry.

Procesoarele actuale nu folosesc lease-uri și nici SKIP LOCKED pentru revendicarea unui lot.

Configurația presupune în acest moment câte o instanță pentru fiecare API. Dacă am porni mai mulți workeri pe același outbox, două instanțe ar putea selecta același mesaj.

Idempotența receptorului ne protejează de efectele duplicate, dar dacă am vrea să scalăm workerii ar trebui să adăugăm și coordonarea explicită a loturilor.

Numărul oficial de registru este alocat cu un INSERT ... ON CONFLICT DO UPDATE ... RETURNING, folosind perechea registru și an.

Contorul participă la tranzacție. Dacă operația care creează înregistrarea eșuează și tranzacția face rollback, revine și modificarea contorului.

Alegerea este importantă pentru cerința numerelor consecutive. O secvență PostgreSQL obișnuită poate consuma un număr chiar dacă tranzacția este anulată.

Numerotarea rămâne responsabilitatea DMS-ului inclusiv atunci când cererea a fost trimisă inițial din Portal.

La programări avem o altă situație de concurență. Incrementarea se face printr-un update condiționat de booked_count < capacity.

Dacă update-ul nu modifică niciun rând, rezervarea este refuzată. Verificarea locurilor disponibile și ocuparea locului se fac în aceeași comandă SQL, nu în două etape separate în memoria API-ului.

Formularele sunt definite prin JSON, conform documentului funcțional.

Avem două validări diferite. Prima verifică definiția formularului, înainte ca acesta să fie publicat. A doua validează valorile trimise efectiv de utilizator.

Prima împiedică publicarea unui formular care nu poate fi completat corect. A doua rămâne obligatorie pe server chiar dacă interfața web validează deja datele, deoarece un client poate trimite request-uri direct către API.

Cererea salvează și schema, plus versiunea formularului valabilă în momentul depunerii.

Editorul de formulare are și un detaliu pur de front-end care s-a dovedit important. Identificatorul tehnic al unui câmp nu trebuie să fie aceeași valoare cu cheia pe care utilizatorul o editează.

Dacă am folosi cheia editabilă drept React key, componenta input s-ar remonta la fiecare caracter introdus. Modelul salvat în baza de date ar putea fi corect, dar editarea ar deveni foarte neplăcută.

Pentru conținutul rich text, backoffice-ul folosește Tiptap.

Editorul oferă formatare structurată și integrare bună cu React fără să fie nevoie să implementăm manual operațiile peste contentEditable.

Serverul nu are încredere în HTML-ul venit din browser. Conținutul trece prin HtmlSanitizer, configurat cu o listă clară de taguri, atribute și scheme URL acceptate.

Am configurat editorul cât mai aproape de regulile de sanitizare. Altfel, utilizatorul ar putea selecta o formatare în interfață și să constate după salvare că serverul a eliminat-o.

Sanitizarea se face în continuare pe server, indiferent de client.

Conținutul HTML aprobat este afișat ulterior ca HTML acolo unde avem câmpuri rich text. Câmpurile obișnuite rămân texte simple.

Fișierele sunt păstrate în volume locale, iar informațiile despre ele sunt salvate relațional în PostgreSQL.

Serverul generează cheia fizică folosind anul, luna și un GUID. Numele trimis de utilizator nu decide locul în care fișierul este salvat.

La upload, fișierul ajunge mai întâi într-un director .tmp. În timpul copierii numărăm octeții și calculăm SHA-256.

După aceea verificăm tipul fișierului după conținut și abia apoi îl mutăm la destinația finală.

Limita este de 10MB pentru un fișier.

DMS face și o verificare mai devreme pe Content-Length, cu o marjă pentru structura multipart. Verificarea aceasta este doar o protecție suplimentară.

Content-Length descrie dimensiunea întregului request, nu dimensiunea exactă a fișierului. În plus, antetul poate lipsi. Din cauza asta, limita reală trebuie verificată în timp ce citim fluxul.

Sistemul de fișiere și PostgreSQL nu participă la aceeași tranzacție.

Codul șterge fișierele temporare sau finale atunci când apare o eroare pe care o poate gestiona. Dacă procesul este însă oprit brutal exact în momentul nepotrivit, poate rămâne un fișier fără metadatele corespunzătoare.

În cazul stocării locale, trebuie acceptată posibilitatea unei operații ulterioare de reconciliere. Am preferat să păstrăm această limitare vizibilă în loc să prezentăm stocarea pe disc ca fiind tranzacțională.

La download, API-ul verifică mai întâi dacă utilizatorul are voie să citească documentul. După verificare emite un URL semnat cu termen de expirare.

Există și diferența dintre adresele folosite intern și cele folosite de browser.

Când un container cheamă alt container, folosește numele serviciului din rețeaua Docker. Când construim un link pe care trebuie să îl deschidă utilizatorul, folosim domeniul public configurat.

Dacă trimiți browserului o adresă internă de container, ea nu va funcționa în afara rețelei Docker.

Rapoartele sunt definite prin JSON și lucrează doar cu seturi de date declarate.

Pentru generarea PDF-ului folosim Chromium prin Playwright. Construim HTML-ul din definiția raportului și escapăm valorile introduse în el.

Imaginile includ fonturile DejaVu, astfel încât diacriticele românești să fie randate corect.

Contextul Playwright folosit pentru raportare nu permite JavaScript în pagină, acces la rețea sau service workers. Un raport trebuie să conțină tot ce îi trebuie pentru export și să nu depindă de resurse externe.

Chromium este pornit pentru fiecare randare.

Serviciul de raportare este singleton și are un semafor care permite un singur export simultan pentru fiecare API. Dacă mai vine o cerere în timp ce browserul lucrează deja, aceasta este refuzată ca ocupată.

Nu păstrăm o coadă nelimitată de procese Chromium. Costul este timpul necesar pornirii browserului pentru fiecare export, dar consumul de resurse rămâne previzibil pentru dimensiunea proiectului.

Numerotarea paginilor este făcută prin footer-ul Chromium. Antetul tabelelor și marginile paginii sunt controlate din HTML și CSS.

Exportul CSV folosește aceleași coloane și rânduri selectate pentru raport. Valorile sunt formatate invariant, iar fișierul conține BOM UTF-8 pentru ca Excel să deschidă corect diacriticele.

Seturile de date și regulile care aparțin DMS rămân în DMS. Cele ale Portalului rămân în Portal. Faptul că randarea este într-o bibliotecă comună nu înseamnă că întregul motor de raportare este centralizat.

Listele sunt încărcate prin proiecții și paginare pe server.

Prin proiecție putem aduce, de exemplu, numele registrului sau numele serviciului odată cu rândul principal, fără să executăm câte o interogare SQL suplimentară pentru fiecare element afișat.

În DMS există testul N7, care numără comenzile SQL executate pentru lista pozițiilor.

Pentru endpoint-ul respectiv ne așteptăm la două selecții indiferent dacă pagina are 5, 20 sau 100 de elemente: una pentru numărul total și una pentru datele paginii.

Testul verifică exact acel endpoint. Nu înseamnă că toate listele din aplicație sunt automat protejate împotriva interogărilor inutile. Dacă apare o listă nouă, trebuie verificată separat.

Erorile folosesc un contract comun bazat pe ProblemDetails, filtre și middleware.

Front-end-ul poate astfel să afișeze mesajul și traceId fără să aibă logică separată pentru fiecare controller.

Aceeași structură poate fi folosită pentru o eroare de validare, pentru o problemă internă sau pentru un eșec de integrare.

Logurile din consolă sunt în format JSON și includ scope-uri.

Handler-ele HTTP propagă X-Trace-Id între servicii. Dacă un apel este pornit mai târziu de un worker de fundal, el poate primi însă un trace nou.

De aceea nu presupunem că toate retry-urile unei operații vor avea același identificator de tracing.

Pentru urmărirea unei cereri pe termen mai lung avem și ID-urile cererilor și evenimentelor. În mediul local nu rulează un serviciu separat care să colecteze și să vizualizeze trace-uri distribuite.

Testele folosesc două tipuri de infrastructură.

Testele HTTP înlocuiesc serviciul de e-mail și anumite apeluri externe. Ele sunt utile când vrem să verificăm răspunsurile API și regulile aplicației fără să pornim toate serviciile.

Pentru concurență, contoare și comportament relațional folosim PostgreSQL real.

Provider-ul InMemory nu reproduce tranzacțiile, blocările și toate constrângerile PostgreSQL. Un test de SELECT ... FOR UPDATE, de exemplu, nu ar spune mare lucru dacă ar rula doar peste InMemory.

În DMS, testul de acceptanță pentru PDF pornește Chromium și citește documentul rezultat cu PdfPig.

În Portal, unele teste de contract folosesc în schimb un randor fals.

Cele două tipuri de test verifică lucruri diferite. Faptul că toată suita este verde nu îți spune automat că ultima pagină a unui PDF arată bine sau că întregul traseu Portal -> DMS -> Portal funcționează corect când unul dintre servicii cade temporar.

Pentru astfel de lucruri rămâne utilă verificarea manuală prin mediul Docker.

În modul Development, folosit de Compose, aplicațiile aplică automat migrațiile și rulează seed-ul la pornire.

Pentru review este foarte practic. Dacă pornești proiectul pe un volum gol, primești direct o aplicație în care există conturi și date suficiente pentru a testa principalele fluxuri.

Seed-ul caută datele după ID-uri sau coduri cunoscute și adaugă ce lipsește. Nu resetează la fiecare restart modificările pe care le-ai făcut prin interfață.

Configurația aceasta este gândită pentru dezvoltare și demonstrație, nu trebuie tratată automat ca o configurație potrivită pentru producție.

Într-un mediu real aș separa aplicarea migrațiilor de pornirea API-ului. Ar trebui stabilit clar cum se fac backup-urile pentru volume și pentru cheile Data Protection, eliminate conturile demonstrative și revizuite toate porturile expuse.

În forma actuală, prioritatea a fost ca proiectul să poată fi pornit ușor de altcineva și ca mecanismele importante de consistență, concurență și securitate să poată fi văzute și testate direct în cod.