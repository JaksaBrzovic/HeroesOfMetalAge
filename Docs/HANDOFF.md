# Handoff – gdje smo stali

Stanje na dan 2026-11-10. Zadnji commit na `main` je `b6762f1` i pushan je na `origin/main`. Radni direktorij je čist.

## Stanje projekta

- Igriv prototip 0.9 (Unity 6000.3.26f1, URP 2D, Full HD) s jednom misijom, Ashen Pass. Sadržaj je opisan u `Docs/STATUS.md`, a arhitektura u `Docs/HOMA_ARCHITECTURE.md`.
- Projekt je trenutno **na čekanju**. Ne radi se aktivno nova značajka. Kasnije bi mogao postati potezna igra u stilu Heroes 3, pa postojeća pravila i sadržaj ne treba mijenjati bez dogovora.
- Zadnja veća izmjena (`2e2f911`) obuhvaća:
  - reviziju panela: uži desni panel, mission panel, victory, rewards, defeat i dialogue box;
  - dev gumb Restart battle;
  - Quick battle animation;
  - brzine kretanja u borbi u `AshenPass.asset`, u odjeljku *Presentation pacing*.
- Automatske provjere su prošle prije tog commita: EditMode 171/171, smoke 152 PASS i 0 FAIL. Nije potvrđeno da je obavljen ručni playtest tih izmjena.

## Sljedeći koraci

1. **Ručni playtest izmjena iz `2e2f911`.** Provjeriti:
   - reimport u Unityju bez grešaka i missing referenci;
   - adventure ekran i mission panel (zatvaranje na CLOSE, ponovnim klikom na kartu i Escapeom);
   - centriran dialogue box;
   - tempo borbe: zadano 2 heksa u sekundi, a uz Quick battle animation 6;
   - da quick combat i dalje preskače bojište;
   - Restart battle (gumb vidljiv samo usred bitke, gasi se u F3);
   - redoslijed Victory → Rewards i tri gumba na Defeat ekranu.
2. **Povratne informacije o tempu borbe i preglednosti UI-a**, te ostale točke s kraja `Docs/STATUS.md`: gallop timing, osjećaj opasnosti, jačina udaraca, ducking glasova, upotrebljivost regrutacije.
3. Tek nakon toga zamijeniti placeholder audio i art, a da misija i dalje radi od početka do kraja.

Novi smjer razvoja dogovoriti s korisnikom prije implementacije.

## Kako provjeravati

- Prvo provjeri je li Unity otvoren: `Get-Process Unity` i `Temp/UnityLockfile`.
- Ako je zatvoren, u projektu pokreni `Tools/Build.ps1`, pa `Tools/SmokeTest.ps1`.
- Ako je otvoren:
  1. robocopy `/MIR` mapa `Assets`, `Packages`, `ProjectSettings` i `Tools` u `D:/WORK/Projects/HoMA_Verify`;
  2. ondje pokreni `Build.ps1` i `SmokeTest.ps1`;
  3. ako je seeder mijenjao prefabove, kopiraj ih natrag u projekt.
- Rezultati su u `Reports/runtime-smoke.txt` (PASS/FAIL linije), `Reports/editmode-tests.xml` i u screenshotovima u `Reports/`. `Reports/` se ne commita.

## Napomene

- Smoke ovisi o slučajnosti. Restart bitke troši random, zato je `DebugSpells()` prije putovanja do bossa. Ako mijenjaš redoslijed u smokeu, pripazi na manu u kasnijim bitkama.
- Staze klizača u Options naslikane su u `OptionsFrame.png`, pa se klizači ne smiju pomicati. Nove toggle opcije idu u postojeći red ili traže novu grafiku.
- Jednokratni alati za generiranje (`PanelRevisionSeeder`, `Tools/UiArt/revise_panels.py` i drugi) opisani su u `README.md`, `Docs/HOMA_ARCHITECTURE.md` i `Tools/README.md`. Projekt radi i bez njih.
