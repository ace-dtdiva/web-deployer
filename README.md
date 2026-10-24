# WebDeployer (Anita's Website Deployer)

Programma Windows portabile (un solo `.exe`, niente installazione) per pubblicare siti React + Vite,
per esempio quelli generati da Lovable e pushati su GitHub: li scarica, li fa vedere in anteprima,
li costruisce e li carica sull'hosting via FTP/FTPS.

## Cosa fa
- **Sorgente**: clona il repository Git del sito e lo tiene aggiornato (`git pull`)
- **Anteprima**: avvia `npm run dev` e apre il sito nel browser predefinito
- **Pubblica**: **Build e carica** fa tutto in un clic, oppure **Solo build** / **Solo FTP**
  - carica solo i file cambiati dall'ultimo caricamento
  - se vuoi, elimina dal server i file delle build precedenti, ma solo quelli caricati da WebDeployer
  - aggiunge il `.htaccess` che fa funzionare le pagine interne dei siti con React Router (hosting Apache)
  - la password FTP non viene mai salvata su disco
- Due modalità, selezionabili in alto a destra:
  - **Professional**: tutto, con log, versioni degli strumenti e scheda FTP
  - **Anita friendly**: solo le tre sezioni, con parole semplici ("Costruisci il sito", "Metti il sito online"),
    messaggi chiari in basso e una spiegazione se qualcosa va storto

## Requisiti
- Windows 10 o 11 (usa .NET Framework 4.8, già incluso in Windows)
- [Git per Windows](https://git-scm.com/download/win): include Git Credential Manager, che gestisce il login a GitHub per i repository privati
- [Node.js](https://nodejs.org/) versione LTS (include npm)

## Installazione
Nessuna: copia `WebDeployer.exe` in una cartella in cui puoi scrivere (per esempio `Documenti\WebDeployer`, non `Programmi`) e avvialo.
Accanto all'exe WebDeployer crea:

| File / cartella | Contenuto |
|---|---|
| `settings.json` | impostazioni (repository, dati FTP senza password, modalità) |
| `projects\<nome>\` | il sito clonato da GitHub |
| `projects\<nome>.deploy.json` | elenco dei file caricati sul server, per caricare solo quelli cambiati |
| `WebDeployer-errori.log` | dettagli degli errori imprevisti (solo se ce ne sono) |

Si può aprire una sola copia di WebDeployer per cartella; per gestire due siti diversi basta tenere due copie dell'exe in due cartelle.

## Prima configurazione (in Professional)
1. Scheda **Progetto**: incolla l'URL del repository GitHub e premi **Clona**. Se il repository è privato si apre il login di GitHub.
2. **Avvia anteprima** per controllare che il sito funzioni; **Stop** la chiude.
3. Scheda **FTP**: inserisci server, porta (di solito 21), utente e cartella remota del sito (es. `/public_html`),
   poi premi **Test connessione**. La password viene chiesta quando serve e resta in memoria finché l'app è aperta.
4. Torna su **Progetto** e premi **Build e carica**.

Da qui in poi si può passare ad **Anita friendly**: la modalità scelta viene ricordata.

## Uso quotidiano (Anita friendly)
- **Costruisci e metti online**: scarica le ultime modifiche da GitHub, costruisce il sito e lo carica sul server.
- **Guarda il sito sul PC**: apre l'anteprima nel browser; **Chiudi l'anteprima** la ferma.
- In basso compare cosa sta succedendo; se qualcosa va storto si apre una finestra che lo spiega
  (il "dettaglio tecnico" serve a chi deve aiutare).

## Come funziona il caricamento
- Per ogni server e cartella WebDeployer ricorda l'impronta (SHA-256) dei file caricati e invia solo quelli cambiati.
- Le pagine `.html` vengono caricate per ultime, così il sito online non punta mai a file non ancora arrivati.
- Se sul server manca `index.html` (es. hosting svuotato) ricarica tutto da solo; **Ricarica tutti i file** (scheda FTP) lo forza.
- **Elimina dal server i file delle build precedenti** (spenta di default) cancella solo file che WebDeployer stesso aveva caricato:
  gli altri file presenti sul server non vengono mai toccati.
- **Aggiungi .htaccess per React Router** (attiva di default): i siti Lovable hanno una sola pagina vera (`index.html`) e
  gli indirizzi interni come `/contatti` li gestisce React Router nel browser; aperti da un link o ricaricati, su Apache
  darebbero errore 404. Il `.htaccess` dice al server di rispondere con `index.html`. Regole:
  - se la build ha già un suo `.htaccess` (es. `public/.htaccess` nel progetto) si usa quello;
  - se sul server c'è già un `.htaccess` diverso (del provider o modificato a mano) non viene mai sovrascritto né cancellato:
    il log mostra le righe da aggiungere a mano;
  - non serve per gli hosting Nginx o IIS, che ignorano il `.htaccess`.

## Siti Lovable con il template nuovo (TanStack Start)
I progetti creati dai template recenti di Lovable non sono siti statici, e caricati così com'escono
dalla build darebbero una pagina vuota o senza foto. WebDeployer li riconosce da solo e ci pensa lui:

- **Le foto non stanno nel repository**: in `src\assets` ci sono solo dei puntatori (`*.asset.json`)
  a immagini che Lovable tiene sui suoi server. Prima della build WebDeployer scarica quelle che
  mancano in `public\__l5e\...`, dove Vite le prende e le copia nel sito; restano lì per le volte
  dopo, quindi si scarica solo quello che è cambiato. Servono anche all'anteprima, che senza di loro
  si vede bianca.
- **La build non produce pagine `.html`**: `npm run build` fa un programma che costruisce le pagine
  mentre il visitatore le chiede, e di norma lo fa per Cloudflare. WebDeployer costruisce con il
  preset `node`, avvia il sito sul PC per qualche secondo, si fa dare tutte le pagine e le salva
  (`/contatti` diventa `contatti\index.html`). Il risultato è un sito statico normale.
- **La cartella da caricare è `.output\public`** invece di `dist`: viene usata in automatico quando
  `dist` non esiste.

In Professional, sotto i pulsanti della sezione **Pubblica**, il selettore **Tipo di progetto** dice
come trattare il progetto: *Automatico* (consigliato) riconosce da sé se ci sono foto da scaricare e
se la build ha bisogno di generare le pagine, *Generico* pubblica la build così com'è, *Lovable* fa
comunque tutti e due i passaggi. Le due cose sono indipendenti: un sito Lovable del template vecchio
ha le foto da scaricare ma la build è già pronta.

## Problemi comuni
| Messaggio | Cosa fare |
|---|---|
| Il certificato FTPS del server non è valido | Spunta **Accetta certificati non validi** (scheda FTP) oppure usa l'indirizzo FTP indicato dal provider |
| Accesso rifiutato dal server (530) | Controlla utente e password; alcuni provider accettano solo FTPS: prova **Solo FTPS (TLS esplicito)** |
| La cartella remota non esiste sul server | Controlla il percorso nella scheda FTP (es. `/public_html`, `/htdocs`, `/www.miosito.it`) |
| Server non raggiungibile / timeout | Controlla indirizzo, porta e connessione |
| git pull fallito | Di solito ci sono file modificati a mano nella cartella del progetto: cancella `projects\<nome>` e premi di nuovo **Clona** |
| La pagina online dà errore 404 se ricaricata | Controlla che **Aggiungi .htaccess per React Router** sia attiva (scheda FTP). Se sul server c'era già un `.htaccess`, il log dice quali righe aggiungere; su hosting Nginx la regola va impostata nel pannello del provider |
| Errore imprevisto | I dettagli sono in `WebDeployer-errori.log`, accanto all'exe |

## Compilare
Serve il [.NET SDK](https://dotnet.microsoft.com/download) (8 o successivo). Poi:

```bat
build.cmd
```

L'exe viene creato in `publish\WebDeployer.exe`: un solo file, con FluentFTP incorporato.
`tools\make-icon.ps1` rigenera l'icona (`src\WebDeployer\Assets\app.ico`).

Struttura del codice (`src\WebDeployer`):
- `Core\`: Git, npm, FTP, impostazioni, avvio dei processi
- `Ui\`: tema e controlli grafici disegnati come nel design
- `MainForm.cs`: la logica della finestra; `MainForm.Layout.cs`: l'interfaccia e i testi delle due modalità

Cos'è cambiato fra una versione e l'altra: [CHANGELOG.md](CHANGELOG.md).
Stato dei lavori e idee future: [ROADMAP.md](ROADMAP.md).

## Componenti di terze parti
- [FluentFTP](https://github.com/robinrodricks/FluentFTP), © Robin Rodricks e contributori, licenza MIT: incorporato nell'exe.
