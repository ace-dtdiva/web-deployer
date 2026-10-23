# Roadmap

## Piano di sviluppo
Ogni step lascia l'app compilabile e funzionante, con un commit dedicato.

- [x] 0. Setup: repo, scheletro dell'app (schede Progetto/FTP, pannello log), script di build
- [x] 1. Progetto singolo: URL del repo e impostazioni salvate in `settings.json` accanto all'exe
- [x] 2. Git: controllo di Git/Node/npm, clone e aggiornamento (git pull)
- [x] 3. Preview: `npm install` quando serve, `npm run dev`, apertura del browser, Stop
- [x] 4. Build: `npm run build`, esito e cartella di output
- [x] 5. Scheda FTP: dati di connessione (FTP/FTPS), password chiesta e mai salvata, Test connessione
- [x] 6. Upload manuale della build, con avanzamento e annullamento
- [x] 7. Auto deploy: pulsante "Build e carica" (build e upload in un clic)
- [x] 8. Upload incrementale (solo i file cambiati) con pulizia opzionale dei file obsoleti
- [x] Grafica del design "WebDeployer – redesign" con logo animato e modalità Anita friendly / Professional
- [x] 9. Rifinitura: icona, protezioni e messaggi d'errore, README, versione 1.0.0
- [x] `.htaccess` per le app con React Router su hosting Apache: aggiunto in automatico, mai sovrascritto se sul server ce n'è già uno (1.1.0)

## Miglioramenti futuri
- Più progetti salvati (elenco), ognuno con repo, cartella e profilo FTP
- Salvataggio della password FTP cifrata (Windows DPAPI)
- Interfaccia in inglese
- Cartella dei progetti clonati selezionabile (ora è `projects\` accanto all'exe)
- Node e Git portabili scaricati dall'app se mancano
- SFTP
- Variabili `.env` per progetto
- Storico dei deploy
