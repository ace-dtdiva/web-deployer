# WebDeployer

Piccolo programma Windows portabile (un solo `.exe`, niente installazione) per pubblicare siti React + Vite, per esempio quelli generati da Lovable e pushati su GitHub.

## Cosa fa
- Clona il repository Git del sito e lo tiene aggiornato
- **Preview**: avvia `npm run dev` e apre il sito nel browser predefinito
- **Build**: esegue `npm run build`
- Upload via FTP/FTPS, anche automatico dopo la build (in arrivo)

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
