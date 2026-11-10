# Handoff – gdje smo stali

Stanje na dan 2026-11-09. Zadnji commit je `8da6a94` (auto combat, quick combat, casualties, info cards). Sve ispod je **necommitano** u radnoj kopiji: 39 izmijenjenih datoteka i 3 nove (`PanelRevisionSeeder.cs`, `MissionPanelView.cs`, `Tools/UiArt/revise_panels.py`).

## Što je napravljeno (necommitano)

**Revizija panela** (`Tools/UiArt/revise_panels.py` crta grafiku, a `PanelRevisionSeeder` slaže kontrole; oba su jednokratni alati)
- Desni panel na adventure ekranu je sužen na dvije trećine, pa je viewport mape veći. Maknuti su warhost i gumb za jahanje.
- Mission karta otvara novi `MissionPanel` s pričom i ciljevima. Zatvara se na CLOSE, ponovnim klikom na kartu ili Escapeom.
- S mission karte su iz grafike obrisani gornji rub warhost kutije i ukrasna linija s draguljem u sredini.
- Victory:
  - prvi korak prikazuje samo gubitke, centrirano i veće;
  - iskustvo je prebačeno na rewards ekran.
- Defeat ekran sad koristi victory okvir bez heroja, a tri izbora su u redu gumba.
- Dialogue box je u sredini ekrana i bez naslikanih portreta, a djeca prate veličinu boxa.
- Sitni popravci: portret, smjer gledanja jedinica, combat statistike unutar svog panela.

**Dev opcije**
- Speed mode (brže kretanje po mapi) je zadano uključen.
- F3 panel ima opciju "Restart battle button", zadano uključenu (PlayerPrefs `dev.battleRestart`).
  - Kad je opcija uključena, u kutu battle loga se vidi gumb RESTART BATTLE.
  - Gumb ponovno pokreće neodlučenu bitku s punom vojskom (`GameSession.RestartBattle`).

**Brzina kretanja u borbi**
- Brzine su u `AshenPass.asset`, u odjeljku *Presentation pacing*:
  - `Battle Move Hexes Per Second` = 2;
  - `Quick Battle Move Hexes Per Second` = 6.
- U Options je novi toggle "Quick battle animation", u istom redu s Mute i Quick combat.
  - Pamti se u PlayerPrefs (`options.quickBattleAnimation`, zadano isključen).
  - Kad je uključen, jedinice idu 6 heksa u sekundi.
- Stack koji se kreće udara tek kad stigne na cilj (`GameSession.PendingStriker`, `pendingHits`). Dok udarac ne padne, ostali stackovi stoje, a pauza iza akcije počinje tek nakon udarca.
- Speed mode **ne** ubrzava borbu.

## Automatska provjera (zadnje pokretanje)

Pokrenuto je u mirroru `D:/WORK/Projects/HoMA_Verify`, jer je Unity bio otvoren:
- EditMode: 171/171.
- Smoke: 152 PASS, 0 FAIL.

`Combat.prefab` i `Options.prefab` su seedani u mirroru i kopirani natrag u projekt.

## Testirati ručno prije commita

1. **Unity reimport:**
   - prebaci se u Unity, pričekaj reimport;
   - provjeri da u konzoli nema grešaka ni missing referenci (Combat i Options prefab).
2. **Adventure ekran:**
   - mapa je šira, a desni panel uži i bez ostataka warhosta;
   - na mission karti nema linije s draguljem i nema tamne mrlje na mjestu gdje je bila.
3. **Mission panel:** otvori ga klikom na kartu i zatvori na sva tri načina (CLOSE, ponovni klik, Escape).
4. **Dialogue:** box je centriran, tekst je unutar okvira i nema naslikanih portreta.
5. **Borba, normalna brzina:**
   - jedinice idu oko 2 heksa u sekundi;
   - udarac, broj štete i zvuk dolaze tek kad melee jedinica stigne do mete;
   - strijelci pucaju odmah;
   - jedinice gledaju prema neprijatelju.
6. **Quick battle animation:**
   - uključi ga u Options, pa provjeri da su jedinice 6 heksa u sekundi;
   - restartaj igru i provjeri da je opcija i dalje uključena.
7. **Quick combat** i dalje preskače bojište i ide ravno na victory ili defeat.
8. **Restart battle:**
   - gumb je vidljiv usred bitke i bitka kreće ispočetka s punom vojskom;
   - kad ugasiš opciju u F3, gumb nestane;
   - nakon kraja bitke gumb nije aktivan.
9. **Victory, Rewards i Defeat:**
   - Victory prvo prikazuje gubitke, zatim rewards s iskustvom;
   - Defeat ima tri gumba u redu i retry radi.
10. **Osjećaj tempa:** bitke s puno kretanja su sad sporije. Odluči jesu li 2 heksa u sekundi dobra brzina ili treba promijeniti vrijednost u Inspectoru.

Ako je sve u redu, commitati. Jednokratni alati (`PanelRevisionSeeder`, `revise_panels.py`) ulaze u commit; dokumentirani su u README, `Docs/HOMA_ARCHITECTURE.md` i `Tools/README.md`.

## Kako provjeravati

- Prvo provjeri je li Unity otvoren: `Get-Process Unity` i `Temp/UnityLockfile`.
- Ako je zatvoren, u projektu pokreni `Tools/Build.ps1` pa `Tools/SmokeTest.ps1`.
- Ako je otvoren:
  1. robocopy `/MIR` mapa `Assets`, `Packages`, `ProjectSettings` i `Tools` u `D:/WORK/Projects/HoMA_Verify`;
  2. ako su mijenjani seederi, pokreni u batchmodeu `-executeMethod HeroesOfMetalAge.EditorTools.PanelRevisionSeeder.CreateMissingAssets`;
  3. ondje pokreni `Build.ps1` i `SmokeTest.ps1`;
  4. seedane prefabove kopiraj natrag u projekt.
- Rezultati su u `Reports/runtime-smoke.txt` (PASS/FAIL linije), `Reports/editmode-tests.xml` i u screenshotovima u `Reports/`.

## Napomene

- Smoke ovisi o slučajnosti: restart bitke troši random, pa je `DebugSpells()` premješten prije putovanja do bossa. Ako se mijenja redoslijed u smokeu, pripazi na manu u kasnijim bitkama.
- Options: staze klizača su naslikane u `OptionsFrame.png`, pa se klizači ne smiju pomicati. Nove toggle opcije idu u isti red ili zahtijevaju novu grafiku.

## Sljedeći koraci

- Ručni playtest iz liste gore, zatim commit.
- Zatim povratne informacije o tempu borbe i preglednosti UI-a (vidi kraj `Docs/STATUS.md`).
