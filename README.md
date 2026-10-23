# WebDeployer

Piccolo programma Windows portabile (un solo `.exe`, niente installazione) per pubblicare siti React + Vite, per esempio quelli generati da Lovable e pushati su GitHub.

## Cosa fa
- Clona il repository Git del sito e lo tiene aggiornato
- **Preview**: avvia `npm run dev` e apre il sito nel browser predefinito
- **Build**: esegue `npm run build` (con `git pull` prima, se vuoi)
- **Carica via FTP/FTPS** la cartella `dist`, anche in automatico dopo la build (**Auto deploy**):
  - invia solo i file cambiati dall'ultimo caricamento (l'elenco è in `projects\<nome>.deploy.json`)
  - se vuoi, elimina dal server i file delle build precedenti, ma solo quelli caricati da WebDeployer
  - la password FTP non viene mai salvata su disco

Stato dei lavori e idee future: [ROADMAP.md](ROADMAP.md).

## Requisiti
- Windows 10/11 (usa .NET Framework 4.8, già incluso in Windows)
- [Git per Windows](https://git-scm.com/download/win)
- [Node.js](https://nodejs.org/) (include npm)

## Uso
Avvia `WebDeployer.exe`. Le impostazioni (`settings.json`) e i progetti clonati (`projects\`) vengono salvati nella stessa cartella dell'exe.

## Compilare
Serve il [.NET SDK](https://dotnet.microsoft.com/download) (8 o successivo). Poi:

```bat
build.cmd
```

L'exe viene creato in `publish\WebDeployer.exe`.
