# Changelog

Le modifiche di WebDeployer, dalla versione più recente.

## 1.2.0 — 24 ottobre 2026

Supporto ai siti Lovable fatti con il template nuovo (TanStack Start), che prima non si riuscivano
a pubblicare: l'anteprima si vedeva bianca e la build non produceva nessuna pagina da caricare.

- **Le foto vengono scaricate prima della build.** Nei siti Lovable le immagini non stanno nel
  repository: in `src\assets` ci sono solo dei puntatori (`*.asset.json`) a file che stanno sui
  server di Lovable. Ora WebDeployer scarica in `public\__l5e\...` quelle che mancano, confrontando
  la dimensione dichiarata, e le ritrova già pronte le volte dopo. Servono anche all'anteprima.
- **Le pagine del sito vengono generate.** La build di questi progetti fa un programma che costruisce
  le pagine mentre il visitatore le chiede, e di norma lo fa per Cloudflare: su un hosting FTP non
  arriverebbe nessun `.html`. Ora WebDeployer costruisce con il preset `node`, avvia il sito sul PC
  per qualche secondo, parte dalla home seguendo i collegamenti interni e salva ogni indirizzo come
  pagina vera (`/contatti` diventa `contatti\index.html`).
- **Cartella di output automatica**: quando `dist` non esiste si usa `.output\public`.
- **Selettore «Tipo di progetto»** (solo in Professional): *Automatico* riconosce da sé cosa serve,
  *Generico* pubblica la build così com'è, *Lovable* forza foto e pagine.
- `NuGet.config` nel repository: senza origini NuGet configurate sulla macchina la compilazione
  falliva con NU1100 su FluentFTP.

## 1.1.0 — 23 ottobre 2026

- **`.htaccess` per React Router** (attivo di default): gli indirizzi interni dei siti a pagina
  singola, aperti da un link o ricaricati, rispondono con `index.html` invece che con un errore 404.
  Non sovrascrive mai un `.htaccess` già presente sul server: in quel caso il log dice cosa aggiungere
  a mano.

## 1.0.0 — 23 ottobre 2026

Prima versione completa: un solo `.exe` portabile, senza installazione.

- **Progetto**: clone del repository GitHub e `git pull` prima della build, con il login gestito da
  Git Credential Manager per i repository privati.
- **Anteprima**: `npm run dev` e apertura del sito nel browser, con Stop che chiude tutto.
- **Pubblica**: *Build e carica* in un clic, oppure *Solo build* / *Solo FTP*.
- **FTP/FTPS** con test della connessione: carica solo i file cambiati dall'ultimo caricamento
  (impronta SHA-256 per server e cartella), le pagine `.html` per ultime, e sa ricaricare tutto se
  sul server manca `index.html`. La password non viene mai salvata su disco.
- **Pulizia opzionale** dei file delle build precedenti, limitata a quelli caricati da WebDeployer.
- **Due modalità**: Professional, con log, versioni degli strumenti e scheda FTP, e Anita friendly,
  con parole semplici, tre sezioni e messaggi chiari in caso di errore.
- Icona dell'app, logo animato, una sola copia aperta per cartella e `WebDeployer-errori.log` per
  gli errori imprevisti.
