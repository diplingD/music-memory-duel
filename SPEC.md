# Music Memory Duel — SPEC

> Izvor istine za implementaciju. Pravila igre, arhitektura, kontrakti, testovi i fazni plan.
> Identifikatori i kod su na engleskom, objašnjenja na srpskom.

---

## 1. Cilj projekta

Kompetitivna mrežna igra memorije melodije i ritma, 2–5 igrača u realnom vremenu.
Backend je **autoritativan**: on vodi tajmere, verifikuje poteze i obračunava poene.

**Cilj kao portfolio artefakt:** pokazati event-driven autoritativni game backend sa
determinističkom, potpuno testiranom game logikom — a ne CRUD aplikaciju sa React frontendom.

Tri stvari koje projekat mora da demonstrira:

1. **Deterministički game loop** izdvojen iz transporta (pure state machine + unit testovi).
2. **Concurrency rešena dizajnom** (single-writer per room), ne lock-ovima.
3. **Iskren threat model** — dokumentovano šta autoritativni server može, a šta ne može da spreči.

**Demo mora da radi za jednog čoveka.** Recruiter otvara link sam, bez protivnika.
Botovi su zato MVP feature, ne nice-to-have.

---

## 2. Tech stack

### V1 (MVP)

| Sloj | Tehnologija |
|---|---|
| Game logika | C# (.NET 9), bez ikakvih zavisnosti |
| Transport | ASP.NET Core + SignalR (WebSocket) |
| State | In-memory, jedan actor (Channel) po sobi |
| Frontend | React + TypeScript + Vite + Tailwind v4 |
| Zvuk | Tone.js |
| Testovi | xUnit (unit nad `Core/Game`) |
| Pakovanje | Jedan Docker image — ASP.NET servira i React build iz `wwwroot` |
| Deploy | Render (free tier); zahteva **tačno jednu instancu**, jer state živi u RAM-u |

**Bez baze, bez logina.** Nickname i sesija žive u RAM-u servera.

### V2

Vidi sekciju 12.

---

## 3. Domenski model

| Pojam | Značenje |
|---|---|
| **Room** | Soba sa kodom/linkom, 2–5 igrača, jedan aktivan meč |
| **Match** | Jedna partija u sobi, sastavljena od rundi |
| **Round** | Jedan ciklus: jedan kompozitor zadaje, svi rešavaju |
| **Composer** | Igrač koji u datoj rundi zadaje sekvencu |
| **Solver** | Svi ostali igrači u toj rundi |
| **Sequence** | Niz `NoteEvent` — visina tona + vreme napada |
| **Confirmation** | Kompozitorovo ponovno sviranje svoje sekvence u fazi rešavanja |

```csharp
public readonly record struct NoteEvent(string Pitch, int TMs);
// Pitch: "C4".."C5" (bele tipke), TMs: ms od prve note (prva nota je uvek 0)
```

**Skala:** 8 belih tipki `C4 D4 E4 F4 G4 A4 B4 C5`.
Tastatura: `1`–`8`. Miš/tap: 8 dugmića. Bez hromatike u V1.

---

## 4. Pravila igre

### 4.0 Tok jedne runde

```
Composing (kompozitor svira, ≤ 15s — svi ostali uživo čuju i vide svaku notu)
   ↓  kompozitor pritisne "Done" ili istekne 15s
Solving (svi igraju simultano — solveri ponavljaju, kompozitor potvrđuje)
   ↓  svi predali ILI istekao deadline
RoundResult (prikaz rezultata, 5s)
   ↓
sledeća runda ILI MatchOver
```

Fazni prelaz **uvek** nastaje iz eksplicitnog eventa (predaja ili istek deadline-a).
Nema `Tick` eventa i nema periodičnog tajmera — vidi 5.3.

**Odvojena `Playback` faza je izbačena iz dizajna.** Raniji nacrt je posle `Composing`-a imao
fazu u kojoj svi *ponovo* slušaju snimljenu sekvencu. Umesto toga se svaka nota broadcast-uje
**uživo** dok kompozitor svira (`NotePlayed`), pa `SequenceSubmitted` vodi **direktno** u
`Solving`. Fer bodovanje time nije ugroženo: poređenje uvek ide protiv **snimljene** sekvence sa
servera, nikad protiv onoga što je neko uživo čuo, pa mrežni jitter u broadcast-u utiče na
osećaj, ne na rezultat.

### 4.1 Konstante

```csharp
public static class GameConstants
{
    public const int ComposeMaxMs        = 15_000;
    public const double SolveMultiplier  =    2.0;
    public const int SolveMinMs          =  5_000;
    public const int SolveMaxMs          = 15_000;
    public const int ResultDisplayMs     =  5_000;

    public const double RhythmTolerance  =   0.40;

    public const int MinNotes            =      1;
    public const int MaxNotes            =    100;
    public const int MinNoteIntervalMs   =     50;  // human timing floor

    public const int MinPlayers          =      2;
    public const int RoundsPerPlayer     =      3;

    public const int RateLimitMaxCalls   =     70;
    public const int RateLimitWindowMs   = 10_000;
}
```

**Konstante koje je raniji nacrt imao, a implementacija ih nema** (i zašto):

| Konstanta | Zašto je nema |
|---|---|
| `PlaybackBufferMs` | `Playback` faza je izbačena — note se čuju uživo dok se komponuje (4.0) |
| `ComposerRhythmTolerance` | kompozitor i solveri dele isti `RhythmTolerance` (4.3) |
| `DuelTargetScore` | meč se završava jedinstvenim pravilom za sve veličine sobe (4.6) |
| `ReconnectGraceMs` | reconnect je implementiran u skraćenom obimu, bez trajnog uklanjanja igrača (4.7) |
| `MaxPlayers` | gornja granica veličine sobe još nije nametnuta u kodu — poznat gap, ne odluka |

### 4.2 Vremenski prozori

**Composing:** kompozitor ima najviše `ComposeMaxMs` (15s). Može završiti ranije.
`composerDurationMs` = vreme od prve do zadnje note (ne ceo prozor).

**Solving:** prozor zavisi od stvarne dužine sekvence.

```
solveMs = clamp(composerDurationMs * SolveMultiplier, SolveMinMs, SolveMaxMs)
```

| Kompozitor odsvirao | Solve prozor |
|---|---|
| 1 nota, 0 ms | 5 000 ms (floor) |
| 3 note u 3 000 ms | 6 000 ms |
| 6 nota u 5 500 ms | 11 000 ms |
| 12 nota u 9 500 ms | 15 000 ms (cap) |

Kompozitor i solveri dele **isti** prozor — sviraju simultano.
Runda se zatvara ranije ako svi predaju pre deadline-a.

> **Trenutno u kodu:** formula je zakomentarisana, a `solveMs` je fiksiran na `SolveMaxMs` —
> dev kompromis uveden radi lakšeg ručnog testiranja (uvek pun prozor). Otvorena stavka.

> Cap na 15s je namerno niži od teoretskog maksimuma (20s). Duge sekvence su zato
> agresivno teške — što je u redu, jer Dixit bodovanje (4.4) kažnjava nerešive sekvence.
> Ako se u testiranju pokaže da je previše, `SolveMaxMs` je jedna konstanta.

### 4.3 Poređenje sekvenci

Sekvenca je tačna kada se poklope **i visina tona i ritam**.

**Visina tona** se poredi strogo: isti broj nota, isti redosled, bez tolerancije.

**Ritam** se poredi preko intervala između nota, **normalizovanih na ukupno trajanje sekvence**.
Normalizacija čini poređenje nezavisnim od tempa — isti motiv odsviran 20% brže je ista
melodija. Bez nje bi igra bila frustrirajuća. Svaki normalizovani interval mora biti unutar
tolerancije od originalnog.

**Tolerancija:** jedinstvena `RhythmTolerance` (0.40) — **isti prag za solvere i za kompozitorovu
potvrdu**. Raniji nacrt je kompozitoru davao blažu toleranciju (0.45) da ne pada na sopstvenoj
sekvenci, ali je umesto toga podignut zajednički prag sa 0.30 na 0.40, pa odvojena konstanta
više ne postoji. Sekvenca od jedne note nema intervale, pa ritam trivijalno prolazi.

Uz to se računa i koliko je nota **od početka** tačno pogođeno; koristi se isključivo kao UI
feedback („6/9 nota") i nikada ne ulazi u bodovanje.

### 4.4 Bodovanje — 3+ igrača

Dva nezavisna obračuna: kompozitorov i solverov. Nema anuliranja runde.

| Uloga | Situacija | Poeni |
|---|---|---|
| Composer | potvrdio + **neki ali ne svi** solveri tačni | **+3** |
| Composer | potvrdio + **svi** solveri tačni (trivijalno) | **0** |
| Composer | potvrdio + **niko** nije tačan (nerešivo) | **0** |
| Composer | **nije potvrdio** | **−1** |
| Solver | tačno **i jedini tačan** | **+3** |
| Solver | tačno | **+2** |
| Solver | promašaj / timeout | **0** |

**Kritično:** kada kompozitor ne potvrdi, **solveri normalno boduju.**
Oni su odradili posao; kompozitorov `−1` je odvojena grana.

**Zašto je slobodan broj nota ispravan dizajn:** kompozitor sam bira težinu, a Dixit bodovanje
kažnjava oba ekstrema — 1 nota → svi pogode → `0`; 40 nota → niko ne pogodi → `0`, i još rizikuje
`−1` na svojoj potvrdi. Optimum je tačno na granici veštine protivnika.
**Zato u projektu NEMA adaptive difficulty algoritma** — igrači ga sami emuliraju.

### 4.5 Bodovanje — 1v1 (Duel)

Pri 2 igrača pravilo „neki ali ne svi" je degenerisano, pa važi posebna tabela.

| Protivnik | Kompozitor potvrdio | Composer | Opponent |
|---|---|---|---|
| tačno | ne | **−1** | **+3** |
| tačno | da | **0** | **+1** |
| promašio | da | **+1** | **0** |
| promašio | ne | **−1** | **0** |

**Zašto je kompozitorova nagrada u duelu mala (+1, a ne +3):** u 1v1 ne postoji Dixit brana
„niko nije pogodio → 0". Da kompozitor dobija +3 kad protivnik promaši, optimalna strategija bila
bi zadati najtežu moguću melodiju koju još uspevaš da potvrdiš — i igra bi se svela na to.
Sa +1 poeni pretežno teku ka **rešavanju**, a zadavanje je pozicija sa malim rizikom i malom
nagradom.

### 4.6 Završetak meča

**Jedno pravilo za sve veličine sobe:** `totalRounds = playerCountAtStart * RoundsPerPlayer`
(2 igrača = 6 rundi, 3 = 9, 5 = 15). Broj rundi se fiksira pri startu meča i **ne menja se** ako
igrač napusti sobu. Meč se uvek odigra do kraja — nema ranijeg završetka na osnovu rezultata,
na kraju se samo prikaže rang lista.

Raniji nacrt je za 1v1 predviđao trku do `DuelTargetScore` (10), sa proverom samo na kraju parne
runde da onaj ko prvi zadaje ne bi imao prednost. To je odbačeno: round-robin rotacija sama po
sebi garantuje da svako komponuje isti broj puta, pa posebna duel grana nije potrebna, a
`DuelTargetScore` više ne postoji.

Rotacija kompozitora: round-robin po redosledu ulaska u sobu. `Players` je fiksna lista — posle
`Lobby`-ja se niko ne pridružuje, a igrač koji se diskonektuje ostaje u listi (vidi 4.7).

### 4.7 Edge case-ovi

| Situacija | Ponašanje |
|---|---|
| Kompozitor se diskonektuje u `Composing` | Runda se anulira (0 za sve), red prelazi dalje |
| Kompozitor se diskonektuje u `Solving` | Tretira se kao „nije potvrdio" → `−1`; solveri boduju normalno |
| Solver se diskonektuje u `Solving` | Ako je predao, potez se računa; ako nije, `0` |
| Kompozitor ne pošalje sekvencu do isteka `Composing` | Runda se anulira, rotira se na sledećeg |
| Igrač se diskonektuje | Ostaje u listi označen kao „disconnected"; meč ide dalje, nedostajući odgovori su promašaj |
| Rejoin | Provera `(playerId, playerToken)` para, pa pun `RoomSnapshot` — igrač nastavlja sa svojim poenima |
| Kompozitor pošalje `0` nota | `SubmissionValidator` tiho odbacuje, runda padne na prirodan istek `Composing`-a |
| Kompozitor pošalje `> MaxNotes` nota | Isto — tiho odbijeno, važi istek `Composing` |
| Submission sa stranim `roundId` | Tiho se ignoriše (vidi 8.3) |
| Drugi submission u istoj rundi | Tiho se ignoriše (vidi 8.3) |

**Skraćeno u odnosu na raniji nacrt** (svesno, za MVP): nema trajnog uklanjanja igrača posle
grace perioda, nema prelaska u `MatchOver` kad broj povezanih padne ispod `MinPlayers`, i nema
persistencije preko pravog reload-a stranice. Pokriven je najčešći slučaj — mreža na trenutak
pukne, tab ostaje otvoren, SignalR sam uspostavi novu konekciju i klijent se automatski vrati
kroz `Rejoin`.

---

## 5. Arhitektura

### 5.1 Slojevi

```
server/Core/Game    PURE C# — state machine, pravila, poređenje, bodovanje. Bez SignalR-a, bez I/O.
server/             ASP.NET Core — Hub, RoomActor, Scheduler, EffectExecutor
server.tests/       xUnit
web/                React + TypeScript
```

Zavisnosti idu u jednom smeru: ostatak servera zna za `Core/Game`, a `Core/Game` ne zna da
SignalR postoji. Detalji strukture su u sekciji 10.

### 5.2 Pure state machine

**„Pure" nema veze sa bazom.** Znači da je to **čista funkcija**: za isti ulaz vraća uvek isti
izlaz i pri tome ne dira ništa izvan sebe. Sve što joj treba dobija kao parametar.

Konkretno, jedna funkcija oblika:

> `Reduce(trenutno stanje, šta se dogodilo, koliko je sada sati) → novo stanje + lista efekata`

Ono što ona **ne** sme da radi: čita bazu, šalje SignalR poruke, poziva `DateTime.UtcNow`,
loguje, pravi HTTP zahtev. Čak i vreme ulazi kao parametar — zato što funkcija koja sama zove
`DateTime.UtcNow` daje različit rezultat u dva poziva, pa nije deterministička i ne može se
testirati.

Funkcija **ne izvršava** ništa u spoljnom svetu. Ona samo vrati listu **efekata** — „pošalji ovu
poruku sobi", „pošalji ovo samo ovom igraču", „probudi me za 15 sekundi". Te efekte izvršava kod
izvan nje. Time ostaje čista, a sve što se dešava u spoljnom svetu je vidljivo kao podatak koji možeš
proveriti u testu.

**Šta se čuva u stanju:** trenutna faza, lista igrača (id, nick, tajni token, da li je konektovan,
poeni), indeks kompozitora, odigrano rundi, ukupno rundi, deadline tekuće faze i tekuća runda
(roundId, kompozitor, zadata sekvenca, odgovori po igraču).

**Faze:** `Lobby → Composing → Solving → RoundResult → (Composing | MatchOver)`

**Eventi** (jedino što može promeniti stanje):
`PlayerJoined`, `PlayerDisconnected`, `PlayerReconnected`, `MatchStartRequested`,
`SequenceSubmitted`, `NotePlayed`, `AnswerSubmitted`, `ComposeDeadlineReached`,
`SolveDeadlineReached`, `ResultDisplayFinished`.

**Efekti:** `PlayerListChangedEffect`, `MatchStartedEffect`, `NotePlayedEffect`,
`SolvingStartedEffect`, `RoundEndedEffect`, `MatchEndedEffect`, `RoomSnapshotEffect`,
`ErrorOccurred`, i `ScheduleEffect` (probudi me u trenutku X sa ovim eventom).

**Zakazana buđenja se ne otkazuju.** `CancelSchedule` ne postoji — jednom zakazan
`*DeadlineReached` uvek opali, pa svaki takav event nosi podatak o tome kojoj generaciji pripada
(`SolveDeadlineReached` nosi `RoundId`, `ComposeDeadlineReached` nosi `RoundsPlayed`) i
`Reducer` ga odbacuje ako se ne poklapa sa tekućim stanjem. Bez toga zakasneli deadline iz
prethodne runde zatvara tuđu, tekuću rundu.

**Šta se ovim dobija:** cela matrica bodovanja iz 4.4 i 4.5 se proverava pozivom jedne funkcije
u tri linije — bez pokrenutog servera i bez browsera. To važi i kad testove pišeš kasnije: ako
pravila stoje ovde, kad ih poželiš pokriti testovima to je posao od pola sata; ako stoje u Hub-u,
nije (vidi 5.5).

### 5.3 Actor per room — jedan red, bez lock-ova

Soba je kao **jedan šalter sa jednim redom**. Potez igrača, „isteklo je vreme", „igrač je pao sa
veze" — sve staje u isti red i obrađuje se jedan po jedan. Dva događaja fizički ne mogu dirati
stanje istovremeno, pa **nema nijednog `lock`-a**.

```csharp
public sealed class RoomActor : IAsyncDisposable
{
    private readonly Channel<GameEvent> _inbox = Channel.CreateUnbounded<GameEvent>();
    private GameState _state;                      // dira ga SAMO RunAsync loop

    public ValueTask PostAsync(GameEvent evt) => _inbox.Writer.WriteAsync(evt);

    private async Task RunAsync(CancellationToken ct)
    {
        await foreach (var evt in _inbox.Reader.ReadAllAsync(ct))
        {
            var (next, effects) = Reducer.Reduce(_state, evt, DateTime.UtcNow);
            _state = next;
            foreach (var e in effects) await _effects.ExecuteAsync(e, this, ct);
        }
    }
}
```

Bez ovoga: 5 igrača preda potez u istoj milisekundi + tajmer opali u istoj milisekundi = 6
thread-ova nad istim `Dictionary`, i bug koji se pojavi jednom u 200 mečeva i nikad se ne reprodukuje.

**Odnos prema mutexu / lock-u.** Cilj je isti — da samo jedan u datom trenutku dira stanje.
Mehanizam je drugačiji:

| | `lock` / mutex | red (Channel) |
|---|---|---|
| Ko čeka | 6 thread-ova stoji blokirano pred istim lock-om | nijedan — ubace poruku i odmah se vrate |
| Ko dira stanje | svaki thread, kad dobije lock | samo jedan loop, uvek isti |
| Rizik | zaboraviš `lock` na jednom mestu → bug; dva lock-a → deadlock | nemoguće zaboraviti — stanje je `private` i nedostupno izvan loop-a |
| Redosled | nedeterministički (ko prvi dobije lock) | tačno onaj kojim su poruke stigle |

Suština: sa lock-om **braniš** stanje od više thread-ova. Sa redom **nikad i nema** više
thread-ova nad stanjem, pa nema od čega da ga braniš. Kao razlika između kase koju čuva
obezbeđenje i kase do koje samo jedan radnik ima ključ.

Bonus koji lock ne daje: pošto je redosled poruka deterministički, meč se može reprodukovati
iz snimljenog niza eventova (V2 replay, 12.6).

`RoomRegistry` je `ConcurrentDictionary<string, RoomActor>` — jedini `Concurrent*` tip u projektu.

### 5.4 Deadline umesto tajmera

Server **ne** šalje `9... 8... 7...`. Server jednom pošalje apsolutni timestamp
(`solveDeadlineUtc`), a klijent sam crta countdown.

Server drži **jedno** buđenje po fazi (`ScheduleEffect`), koje ubacuje odgovarajući
`*DeadlineReached` event u inbox sobe.

Dobici: 1 poruka umesto 15, nikakav drift, i pri reconnect-u klijent odmah zna koliko ima vremena.

**Može li klijent da produži sebi vreme?** Ne. Server drži **isti** deadline i sam zatvara rundu
u svom trenutku; svaka predaja koja stigne posle toga se odbija. Klijent može da nacrta 60
sekundi na svom ekranu ako želi, ali countdown na klijentu je **isključivo prikaz** — nikada
izvor istine. Rezultat manipulacije je da igrač sam sebe zbuni i propusti rok.

Isto važi i za clock sync: klijent može lagati o svom RTT-u, ali time pomera samo brojku na
svom ekranu. Ovo je uopšte smisao autoritativnog servera — svaki broj koji klijent izračuna je
kozmetika, a jedini broj koji nešto znači je onaj koji server drži kod sebe.

**Clock sync (obavezno za fer simultanu fazu):** klijent pri ulasku pošalje 5× `Ping`,
za svaki izračuna `offset = serverNowMs + rtt/2 − clientNowMs`, uzme **medijanu** i njome
koriguje sve deadline-ove.

```ts
// web/src/services/clock.ts
export async function syncClock(conn: HubConnection): Promise<number> {
  const offsets: number[] = [];
  for (let i = 0; i < 5; i++) {
    const t0 = performance.now();
    const serverNowMs = await conn.invoke<number>("Ping");
    const rtt = performance.now() - t0;
    offsets.push(serverNowMs + rtt / 2 - Date.now());
  }
  return offsets.sort((a, b) => a - b)[2];   // medijana
}
```

### 5.5 Hub kao adapter

**Šta je Hub.** Hub je za SignalR ono što je Controller za Web API — klasa čije metode zove
spoljni svet. Razlika je samo u kanalu: Controller metodu poziva HTTP zahtev, a Hub metodu
poziva klijent preko otvorene WebSocket konekcije. Hub uz to može i **sam da zove klijenta**
(`Clients.Group(...)`), što Controller ne može.

**Zašto pravila ne smeju biti u njemu.** Hub metoda ima pristup stvarima koje postoje samo dok
je SignalR napravio taj objekat: `Context.ConnectionId`, `Clients`, `Groups`. To znači da tu
metodu **ne možeš pozvati iz testa** kao običnu funkciju — bez pokrenutog servera `Context` je
`null`, a `Clients.Group(...)` puca. Ako je bodovanje unutar te metode, jedini način da ga
proveriš je: podigni server, poveži tri prava SignalR klijenta, pošalji im poteze, pa čekaj
`Task.Delay(15000)` da istekne pravi tajmer i tek onda proveri rezultat. Jedan takav test traje
15 sekundi i ima 40 linija setup-a — a bodovanje ima 11 grana. Praktično to znači da će 2–3
grane ostati pogrešne i nikad se neće primetiti.

Rešenje: `GameHub` samo prevodi SignalR pozive u `GameEvent` i ubacuje ih u red sobe.
**Nula pravila igre.** Cilj: ~40 linija.

```csharp
public sealed class GameHub(RoomRegistry rooms) : Hub<IGameClient>
{
    public async Task SubmitAnswer(Guid roundId, NoteEvent[] notes)
    {
        var (roomCode, playerId) = Context.RequirePlayer();
        if (!SubmissionValidator.IsValid(notes)) return;   // tiho odbacivanje, bez greške klijentu
        await rooms.Get(roomCode).PostAsync(
            new AnswerSubmitted(playerId, roundId, [..notes]));
    }
}
```

### 5.6 Botovi — IZBAČENI IZ MVP-a

> **Nisu implementirani i ne rade se pre deploya.** Svesna odluka: demo se igra tako što jedna
> osoba otvori dva taba. Posledica je da „javni link koji jedna osoba može da igra protiv
> botova" iz sekcije 11 nije ispunjen u doslovnom smislu. Ostatak sekcije je zadržan kao nacrt
> ako se botovi ikad budu radili.

Bot bi koristio **isti** `GameEvent` interfejs kao ljudski igrač — kod koji ubacuje u isti kanal.
To je i dokaz da je arhitektura čista.

| Nivo | P(zapamti notu) | Timing jitter |
|---|---|---|
| Easy | 0.70 | ±18% |
| Medium | 0.85 | ±12% |
| Hard | 0.95 | ±8% |

Bot bi namerno **unosio jitter** da prođe proveru ljudskog timinga (8.1) — bot koji svira savršeno
bio bi odbijen kao cheater, što je dobar sanity test za samu proveru.

Bot odgovara posle realističnog kašnjenja (`0.6 × solveMs ± 20%`), preko `BotShouldAnswerEffect`.

---

## 6. SignalR kontrakti

### 6.1 Client → Server (Hub metode)

| Metoda | Parametri | Vraća |
|---|---|---|
| `CreateRoom` | `nick` | `{ roomCode, playerId, playerToken }` |
| `JoinRoom` | `roomCode, nick` | `{ playerId, playerToken }` |
| `Rejoin` | `roomCode, playerId, playerToken` | — (odgovor stiže kao `RoomState`) |
| `StartMatch` | — | — |
| `PlayNote` | `note` | — (uživo broadcast tokom `Composing`-a) |
| `SubmitSequence` | `notes[]` | — |
| `SubmitAnswer` | `roundId, notes[]` | — |
| `Ping` | — | `serverNowMs` (long) |

`StartMatch` je dostupan samo hostu (prvom igraču u sobi).

`SubmitSequence` nema `roundId` — runda tek nastaje iz njega, pa nema šta da se referencira;
zaštita od „ko prvi pošalje postaje kompozitor" je provera da je pošiljalac trenutni kompozitor
(inače `ErrorOccurred("not_your_turn")`). `SubmitAnswer` `roundId` ima, i koristi ga za
idempotentnost (8.3).

### 6.2 Server → Client (`IGameClient`)

```csharp
public interface IGameClient
{
    Task PlayerListChanged(PlayerDto[] players);
    Task MatchStarted(long composeDeadlineUnixMs, string composerId);
    Task NotePlayed(NoteEvent note);                // uživo, dok kompozitor svira
    Task SolvingStarted(Guid roundId, long solveDeadlineUnixMs);
    Task RoundEnded(RoundEndedDto dto);
    Task MatchEnded(StandingDto[] finalStandings);
    Task RoomState(RoomSnapshotDto dto);            // samo pri Rejoin-u
    Task ErrorOccurred(string code, string message);
}
```

```csharp
public record PlayerDto(string Id, string Nick, bool IsHost, bool IsConnected);

public record RoundEndedDto(
    Guid RoundId, string ComposerId, bool ComposerConfirmed,
    PlayerRoundResultDto[] Results, StandingDto[] Standings,
    long ResultDisplayDeadlineUnixMs);

public record PlayerRoundResultDto(
    string PlayerId, bool Submitted, bool Correct,
    double PrefixRatio, int PointsDelta, string Reason);

public record StandingDto(string PlayerId, string Nick, int Score, int Rank);

public record RoomSnapshotDto(
    Phase Phase, PlayerDto[] Players, string? ComposerId, Guid? RoundId,
    long? PhaseDeadlineUnixMs, StandingDto[] Standings);
```

**Deadline-ovi putuju kao Unix ms (`long`), ne kao `DateTime`** — klijent ih direktno poredi sa
`Date.now()` korigovanim za clock offset (5.4).

`RoomSnapshotDto` se šalje **samo pri `Rejoin`-u**, i to isključivo tom jednom igraču. Sadrži sve
što klijent treba da nacrta tačno stanje, uključujući preostalo vreme (preko apsolutnog
deadline-a). Namerno **ne** nosi detalje `RoundResult` faze — ko reconnect-uje baš u tih 5s vidi
prazan ekran dok ne stigne sledeći event.

`PlayerSubmitted` (signal „ovaj je predao") ne postoji — nije implementiran kao zaseban događaj.

### 6.3 TypeScript tipovi

Nacrt je predviđao da se `web/src/models/contracts.ts` **generiše** iz C#-a (NSwag ili TypeGen),
jer je ručno sinhronizovanje tipova zamka — jedan drift i debug-uješ satima.

**U praksi je fajl pisan ručno** — svesno odloženo, uz prihvaćen rizik od drift-a.

### 6.4 `Reason` kodovi

Za UI feedback u `PlayerRoundResultDto.Reason`:

```
correct | wrong_pitch | wrong_rhythm | wrong_length
timeout | not_submitted | implausible_timing | round_void
```

---

## 7. Frontend

```
web/src/
├── api/
│   └── gameHub.ts              HubConnection, autoReconnect, auto-Rejoin, tipovan wrapper
├── components/
│   ├── Keyboard.tsx            8 tipki, tastatura 1–8 + klik/tap
│   ├── Countdown.tsx           lokalni countdown iz apsolutnog deadline-a + clock offset
│   ├── PianoRoll.tsx           note na vremenskoj skali
│   ├── PhaseBanner.tsx         „Ti zadaješ" / „Ponovi"
│   ├── Scoreboard.tsx          finalna rang lista (MatchOver)
│   ├── RoundResult.tsx         po igraču: tačno/netačno, 6/9 nota, ±poeni
│   └── Lobby.tsx               nick, kod sobe / link, start
├── models/
│   └── contracts.ts            ručno pisano (vidi 6.3)
├── pages/
│   └── RoomPage.tsx            drži sve stanje faza kroz useState
├── services/
│   ├── synth.ts                Tone.js — playNote, playSequence
│   ├── clock.ts                clock offset sync (5.4)
│   ├── capture.ts              beleženje NoteEvent[] preko performance.now()
│   └── gameStore.ts            prazan — Zustand je zavisnost, ali se ne koristi
├── styles/
│   └── globals.css
└── utils/
    ├── pitches.ts              mapiranje tipki na tonove
    └── time.ts                 formatiranje, clamp, deadline → preostalo
```

**Ključne UX odluke:**

- Prva nota postavlja `t0`; svi `TMs` su relativni. Prva nota uvek `TMs = 0`.
- Vizualni + audio feedback na svaki pritisak (tipka svetli, nota zvuči).
- `Countdown` računa iz `deadline − (Date.now() + clockOffset)`. Nikad iz servera po sekundi.
- Tokom `Composing`-a svi **uživo** vide i čuju note dok ih kompozitor svira (`PianoRoll` + zvuk);
  `Keyboard` i dugme za predaju vidi **samo** kompozitor, ostali vide „X IS COMPOSING...".
- Tokom `Solving` **ne prikazuj** zadatu sekvencu (i ne loguj je u konzolu).
  Ne sprečava cheat (vidi 8), ali ne treba ga ni servirati.
- Autoplay policy: `Tone.start()` mora ići iz user gesture-a — jedan „Enter room" klik.

---

## 8. Anti-cheat i threat model

### 8.0 Šta autoritativni backend jeste rešio

Nemoguće je: dobiti poene bez validnog unosa, lagati o rezultatu, produžiti tajmer,
poslati potez posle deadline-a, bodovati u tuđe ime.

### 8.1 Provera ljudskog timinga (`HumanTiming`)

**Jedini mehanizam koji stvarno radi protiv replay napada.**

Presretnuta poruka ima *identične* `TMs` vrednosti kao original — jitter = 0. Čovek to
fizički ne može.

```csharp
public static bool IsHumanTiming(
    IReadOnlyList<NoteEvent> target,
    IReadOnlyList<NoteEvent> attempt)
{
    // 1) nijedan interval ispod ljudskog minimuma
    for (var i = 1; i < attempt.Count; i++)
        if (attempt[i].TMs - attempt[i - 1].TMs < GameConstants.MinNoteIntervalMs)
            return false;

    // 2) ne sme biti mašinski tačna kopija target timing-a
    if (attempt.Count >= 3 && attempt.Count == target.Count)
    {
        double totalDeviationMs = 0;
        for (var i = 0; i < attempt.Count; i++)
            totalDeviationMs += Math.Abs(attempt[i].TMs - target[i].TMs);

        var averageDeviationMs = totalDeviationMs / attempt.Count;
        if (averageDeviationMs < 5.0) return false;   // < 5ms prosečno odstupanje = bot/replay
    }
    return true;
}
```

Pokušaj koji padne na ovoj proveri boduje se kao promašaj sa `Reason = implausible_timing`
(sam string razloga je deo DTO ugovora prema frontendu i **ne** prati preimenovanje klase).

Provera se primenjuje podjednako na solverske pokušaje **i na kompozitorovu potvrdu** — pošto i
kompozitor mora fizički ponovo da odsvira melodiju u `Solving` fazi, i njegov pokušaj je podložan
istom napadu.

**Uključen ritam znatno jača ovaj check** — više timing podataka, više signala.
To je argument da ritam nije samo game feature nego i deo threat modela.

### 8.2 Rate limiting

**Ne pomaže protiv replay cheat-a.** Ostaje jer je 5 linija i štiti stabilnost:
zaštita od 5000 poruka/s koje pune memoriju i troše CPU.

Limit: **70 hub poziva / 10s po konekciji**, plus `MaxNotes` cap na payload.
Ne prodaji ga u README-u kao anti-cheat.

Zašto 70, a ne prvobitnih 30: limiter broji **sve** pozive iste konekcije zajedno, a `PlayNote`
se šalje za **svaku odsviranu notu** (uživo broadcast, 4.0). Sekvenca od 100 nota kroz 15s
lako pređe 30 poziva u prozoru, pa bi niži limit blokirao **legitimnu igru**. 70/10s i dalje
ostavlja ogromnu marginu u odnosu na pravi flood (~5000 poruka/s).

Implementaciona napomena: pravi ASP.NET Core *middleware* vidi samo inicijalni HTTP handshake
WebSocket konekcije, **ne** i svaki naredni poziv Hub metode — zato je limiter `IHubFilter`,
uprkos imenu fajla `RateLimitMiddleware.cs`. Samo brojanje je izdvojeno u čistu, testabilnu
`Utils/RateLimiter.cs` (`TryConsume(key, utcNow)`, vreme ulazi kao parametar — isti princip kao
`Reducer`).

### 8.3 Idempotentan submit vezan za `roundId`

Server prihvata **tačno jedan** submission po paru `(roundId, playerId)`; drugi se tiho ignoriše.
Rešava tri realne stvari:

1. **Mrežni retry** — SignalR pri reconnect-u može poslati poruku dvaput. Bez ovoga se poen duplira.
2. **Bot koji „pecka"** — bez ovoga pošalje 5 varijanti i računa da server prihvati najbolju.
3. **Zastarela poruka** — potez iz runde 4 stigne 3s kasno, u toku runde 5. `roundId` ne odgovara → odbaci.
   Bez ovoga dobijaš bugove koji izgledaju kao magija.

### 8.4 KNOWN LIMITATION — replay napad se ne može sprečiti

> Server **mora** poslati note klijentu da bi ih ovaj odsvirao. Znači odgovor se nalazi u
> memoriji svakog klijenta. Klijent može presretnuti `NotePlayed` poruke preko WebSocket-a,
> sklopiti ih u sekvencu i poslati isti niz nazad kao svoj odgovor — 100% tačno svaki put.
>
> **Ovo je fundamentalno nerešivo u ovom dizajnu**, ne propust implementacije.
> Ublažava se proverom ljudskog timinga (8.1), ali napadač koji doda realističan jitter prolazi.
>
> Jedino potpuno rešenje: server renderuje audio i strimuje ga, tako da note nikad ne
> stignu klijentu kao podatak. Odbačeno kao neproporcionalno za scope projekta
> (server-side audio rendering, bandwidth, latencija).

**Ovaj tekst ide u README.** Analiza vredi više na intervjuu od bilo koje implementirane feature.

---

## 9. Plan testiranja

### 9.1 Unit — bodovanje (`server.tests/ScoringTests.cs`)

Tabelarni testovi, po jedan case za svaki red iz 4.4 i 4.5:

```
Multi (3+ igrača):
  confirmed=true,  solvers=4, correct=2  ->  composer +3
  confirmed=true,  solvers=4, correct=4  ->  composer  0
  confirmed=true,  solvers=4, correct=0  ->  composer  0
  confirmed=false, solvers=4, correct=2  ->  composer -1
  confirmed=false, solvers=4, correct=2  ->  solveri normalno boduju   <- regresija za 4.4
  correct=true,  correctCount=1          ->  solver  +3
  correct=true,  correctCount=3          ->  solver  +2
  correct=false                          ->  solver   0

Duel (1v1):
  confirmed=false, opponentCorrect=true   -> (-1, +3)
  confirmed=true,  opponentCorrect=true   -> ( 0, +1)
  confirmed=true,  opponentCorrect=false  -> (+1,  0)
  confirmed=false, opponentCorrect=false  -> (-1,  0)
```

### 9.2 Unit — poređenje (`NoteComparerTests.cs`)

```
pitch: identičan -> true
pitch: zamenjen redosled -> false
pitch: različita dužina -> false
rhythm: isti motiv 25% brže -> true (normalizacija)
rhythm: isti motiv 25% sporije -> true
rhythm: "ta-ta-taaa" vs "ta-taaa-ta" -> false
rhythm: jitter 15% po intervalu -> true (unutar 0.40)
rhythm: udvostručen jedan interval -> false (60% posle normalizacije)
rhythm: 1 nota -> true (nema intervala)
prefix: 6 od 9 tačnih -> 0.667
human timing: savršena kopija target TMs -> false
human timing: interval 30ms -> false
human timing: ljudski jitter -> true
```

### 9.3 Unit — state machine (`ReducerTests.cs`)

```
Lobby + MatchStartRequested (1 igrač)  -> ostaje Lobby, error effect
Lobby + MatchStartRequested (2 igrača) -> Composing, ScheduleEffect, TotalRounds = 2 × RoundsPerPlayer
Composing + SequenceSubmitted (kompozitor) -> Solving, solveMs deadline
Composing + SequenceSubmitted (ne-kompozitor) -> ErrorOccurred("not_your_turn")
Composing + ComposeDeadlineReached (nema note) -> runda anulirana, sledeći kompozitor
Composing + ComposeDeadlineReached (stari RoundsPlayed) -> ignoriše se
Solving + AnswerSubmitted (svi predali) -> RoundResult odmah
Solving + AnswerSubmitted (isti igrač 2x) -> drugi se ignoriše
Solving + AnswerSubmitted (stari roundId) -> ignoriše se
Solving + SolveDeadlineReached -> RoundResult, nepredali dobijaju timeout
Solving + SolveDeadlineReached (stari roundId) -> ignoriše se
RoundResult + ResultDisplayFinished (nije zadnja runda) -> Composing, next composer
RoundResult + ResultDisplayFinished (zadnja runda) -> MatchOver
PlayerDisconnected (kompozitor, Composing) -> runda anulirana, rotacija
PlayerDisconnected (kompozitor, Composing, zadnja runda) -> MatchOver
PlayerReconnected (tačan token) -> IsConnected=true, poeni sačuvani, RoomSnapshotEffect
PlayerReconnected (pogrešan token) -> ignoriše se
```

**Ostale unit datoteke:** `SubmissionValidatorTests.cs` (granice broja nota, monotonost `TMs`),
`RateLimiterTests.cs` (fixed-window brojač), `HumanTimingTests.cs` (8.1).

### 9.4 Integration i 9.5 E2E — nisu rađeni

`WebApplicationFactory` integration testovi i Playwright E2E su ostali van MVP obima. Pokrivenost
se oslanja na unit testove nad `Core/Game` (gde su sva pravila) plus ručno testiranje u browseru.
Posledica: `GameHub`, `RoomActor` i `EffectExecutor` nemaju automatske testove — svesno prihvaćeno,
jer u njima nema pravila igre (5.5).

---

## 10. Struktura repozitorijuma

```
music-memory-duel/
├── SPEC.md
├── README.md                     ← GIF na vrhu + threat model iz 8.4
├── Dockerfile                    multi-stage: node build → dotnet publish → aspnet runtime
├── .dockerignore
├── .github/workflows/ci.yml      (još nije napravljen)
├── server/
│   ├── Program.cs
│   ├── Controllers/
│   │   └── HealthController.cs   jedini HTTP endpoint; sve ostalo ide preko Hub-a
│   ├── Hubs/
│   │   ├── GameHub.cs            adapter, nula pravila igre (5.5)
│   │   ├── IGameClient.cs        strongly-typed klijentski interfejs (6.2)
│   │   └── HubCallerContextExtensions.cs   RequirePlayer / TryGetPlayer
│   ├── Core/
│   │   ├── DTOs/                 sve iz 6.2
│   │   ├── Models/               GameState, Round, Player, Phase, NoteEvent
│   │   └── Game/                 ← PURE, bez SignalR-a i bez I/O (5.2)
│   │       ├── GameConstants.cs
│   │       ├── GameEvent.cs
│   │       ├── Effect.cs
│   │       ├── Reducer.cs
│   │       ├── Scoring.cs
│   │       ├── NoteComparer.cs
│   │       ├── HumanTiming.cs            (8.1)
│   │       └── PlayerRoundResult.cs
│   ├── Middleware/
│   │   ├── ExceptionMiddleware.cs
│   │   └── RateLimitMiddleware.cs        IHubFilter, ne pravi middleware (8.2)
│   ├── Services/
│   │   ├── RoomActor.cs          red po sobi (5.3)
│   │   ├── RoomRegistry.cs
│   │   ├── EffectExecutor.cs     izvršava efekte koje Reducer vrati
│   │   ├── Scheduler.cs          deadline buđenja (5.4)
│   │   └── BotPlayer.cs          prazan stub — botovi izbačeni iz MVP-a (5.6)
│   ├── Utils/
│   │   └── RateLimiter.cs        čist fixed-window brojač, testabilan (8.2)
│   └── Validators/
│       ├── SubmissionValidator.cs        MinNotes/MaxNotes, tMs monotono ne-opadajuće
│       └── NicknameValidator.cs          prazan stub — pravila nisu definisana
├── server.tests/                 xUnit (sekcija 9)
└── web/                          (sekcija 7)
```

Prazni folderi iz ranijeg nacrta (`Authorization/`, `Migrations/`, `Repository/`,
`Core/Interfaces/`) nisu napravljeni — dodaju se tek ako i kad stignu baza i auth (12.1, 12.3).

**Jedna napomena o `Core/Game/`.** To je jedini folder u kome ne sme biti `using
Microsoft.AspNetCore.SignalR`, `DateTime.UtcNow`, logovanja ni pristupa bazi. U ovoj strukturi
je to **dogovor**, ne nešto što kompajler tera. Ako želiš da ga kompajler tera, `Core/Game/` se
izdvoji u zaseban `.csproj` bez reference na ASP.NET — tada je fizički nemoguće pogrešiti. Oboje
radi; prvo je manje fajlova, drugo je sigurnije.

---

## 11. Fazni plan

Radi se **vertikalno**: u svakom koraku aplikacija je pokrenuta i nešto vidljivo se dodaje.
Testovi se pišu kad ti odgovara, a ne unaprijed — jedino što se ne odlaže je **gde** pravila
stoje (`Core/Game/`, vidi 5.5).

| Korak | Cilj | Radi se | Status |
|---|---|---|---|
| **1** | Dva browsera vide jedan drugog | Hub, `RoomRegistry`, `CreateRoom`/`JoinRoom`, lobby ekran, lista igrača | gotovo |
| **2** | Melodija stiže od jednog do drugog | `RoomActor` + red, `Reducer`, klavijatura, Tone.js, piano roll, uživo broadcast nota | gotovo |
| **3** | Runda se zatvara i boduje | deadline scheduler, Solving faza, `NoteComparer`, `Scoring`, ekran rezultata | gotovo |
| **4** | Meč se odigra do kraja | rotacija kompozitora, `MatchEnded`, tabela | gotovo |
| **5** | Demo radi za jednu osobu | botovi, `AddBot` u lobiju | **izbačeno iz MVP-a** |
| **6** | Otporno na stvarni svet | reconnect + `RoomSnapshot`, clock sync, human timing, rate limit, validatori | gotovo |
| **7** | Javno | README, Docker, CI, deploy | u toku — README i Docker gotovi, ostaju deploy i CI |

Testovi iz sekcije 9 se dodaju kad ti bude trebalo. Najkorisniji trenutak je odmah posle
koraka 3 — bodovanje ima 11 grana i to je jedino mesto gde greška ne pravi vidljiv bug nego
tiho pogrešan rezultat.

**Definition of done za MVP:**
- [x] Meč se odigra do kraja iz dva browsera
- [ ] Deployovan javni link — **ali bez botova**, pa se igra tako što jedna osoba otvori dva taba
- [x] Reconnect ne prekida meč
- [ ] README sa GIF-om i sekcijom 8.4 — README postoji, GIF dolazi posle deploya

---

## 12. V2 — Backlog

### 12.1 Baza (PostgreSQL + EF Core)

```
users            id, external_auth_id, nick, created_at
matches          id, room_code, started_at, ended_at, player_count, rhythm_enabled
match_players    match_id, user_id, final_score, final_rank
rounds           id, match_id, round_index, composer_id, target_notes (jsonb),
                 composer_duration_ms, composer_confirmed
submissions      round_id, user_id, notes (jsonb), correct, prefix_ratio,
                 points_delta, reason, received_at
leaderboard      user_id, rating, matches_played, wins, updated_at
```

Note se čuvaju kao `jsonb` — omogućava kasniju analizu odsviranih melodija.

### 12.2 Leaderboard

ELO (K=32, start 1200) nad `match_players`. Top 100 endpoint sa keširanjem.
Preporuka: ELO tek kada ima ≥5 odigranih mečeva, inače je tabela šum.

### 12.3 Auth

Clerk ili Supabase Auth (Google + email) → JWT → `AddAuthentication` na SignalR endpoint-u.
Anonimni gost mod ostaje — obavezno, da demo link i dalje radi bez registracije.

### 12.4 Logovanje i observability

- **Serilog** strukturirano logovanje, JSON sink.
- Svaki `GameEvent` se loguje sa `roomCode` i `roundId` kao strukturirana polja →
  ceo meč se rekonstruiše jednim filterom.
- `Microsoft.Extensions.Diagnostics.HealthChecks` na `/health`.
- Metrike: aktivne sobe, aktivne konekcije, rundi/min, prosečan `solveMs`,
  broj `implausible_timing` odbijanja.
- Opciono OpenTelemetry → Azure Monitor.

### 12.5 Redis — DA, ali samo za dve stvari

**Šta se dobija:** meč preživi restart backend-a; moguće je pokrenuti 2+ instance a da igrači
u istoj sobi ostanu povezani; deljeni keš za leaderboard.

**Šta se gubi:** ako živi state ide u Redis, dodaje se network round-trip na *svaki* potez u
igri koja se meri desetinkama sekunde — merljivo pogoršanje. Plus još jedan servis u deploy-u,
compose-u, CI-ju i monitoringu, serijalizacija stanja i verzionisanje šeme.

**Odluka:** živi `GameState` **ostaje u RAM-u**. Redis nosi isključivo:

1. **Periodični snapshot** (svaki `RoundResult`) za crash recovery.
2. **SignalR backplane** za scale-out, uz sticky routing po `roomCode`.

Ovaj trade-off se eksplicitno piše u README. Objašnjenje zašto Redis **nije** ispod
game loop-a vredi na intervjuu više od samog Redis-a u stack listi.

### 12.6 Match replay

Pošto je `Reduce` deterministički, čuvanje liste `GameEvent` daje replay besplatno.
Frontend reprodukuje meč iz event log-a, uz audio. Najbolji ROI od svih V2 feature-a.

### 12.7 Ostalo

- Spectator mod (join bez učešća).
- Hromatska skala + izbor instrumenta kao room setting.
- `rhythmEnabled` toggle u lobiju — ritam je u V1 uvek uključen, flag ne postoji.

---

## 13. Odbačeno (i zašto)

| Ideja | Zašto ne |
|---|---|
| Adaptive difficulty (rast broja nota po rundi) | Slobodan broj nota + Dixit bodovanje već rešavaju balans (4.4) |
| Parcijalni poeni za prefix | Bodovanje mora biti čitko; prefix ostaje samo UI feedback |
| Anuliranje runde kad kompozitor ne potvrdi | Solveri su odradili posao — boduju normalno, kompozitor sam nosi `−1` |
| Live state u Redis-u | Round-trip na svaki potez; vidi 12.5 |
| Server-side audio rendering kao anti-cheat | Neproporcionalno scope-u; vidi 8.4 |
| Periodični `Tick` event / server countdown | Deadline model je manje poruka i bez drift-a; vidi 5.4 |
