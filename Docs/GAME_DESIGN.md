# Heroes of Metal Age — Game Design Document

Verzija: 0.4 · Ažurirano: 12. listopada 2026. · Jezik dokumenta: hrvatski

Živi dokument dizajna cijele igre. Trenutno detaljno opisuje postojeći prototip i razdvaja ga od buduće vizije. Nepotvrđene odluke ostaju otvorene; ovaj dokument ne predstavlja tvrdnju da je cijela igra već definirana ili implementirana.

## 1. Status, izvori i održavanje

Oznake u dokumentu:

- **Implementirano:** pronađeno u lokalnom projektu, podacima ili kodu.
- **Dogovoreni smjer:** izražena korisnikova namjera, bez potpune implementacije.
- **Prijedlog:** preporuka za daljnji dizajn, još nije prihvaćena odluka.
- **Otvoreno:** treba definirati ili potvrditi.

Glavni projekt: `D:/WORK/Projects/Versioned/HeroesOfMetalAge`.
Vanjska radna zbirka arta: `C:/Users/jaksa/Documents/ChatGPT/MyVideoGame/assets`.

Izvori početne verzije: razgovor „Osmisli metalnu HOMM3 igru”, razgovori za mounted hero i air guitarist/roadie art, README, PROTOTYPE, AUDIO_DIRECTION, STATUS, ARCHITECTURE, authored baze te Domain/BattleEngine.cs. Provjera je pregled datoteka; u ovom koraku nije ponovljen gameplay test.

Podjela odgovornosti:

| Izvor | Što određuje |
|---|---|
| GAME_DESIGN.md | Namjeravano ponašanje, iskustvo i katalog sustava; pregled aktualnog dizajna |
| ScriptableObject baze | Stvarne aktualne vrijednosti balansa i reference na sadržaj |
| HOMA_ARCHITECTURE.md | Tehničke granice, ugovori i arhitekturne odluke projekta (opća Unity pravila nisu ponovljena) |
| AUDIO_DIRECTION.md | Detaljna zvučna produkcija i audio implementacija |
| PROTOTYPE.md | Kratki opseg prve misije i njeni kriteriji |
| STATUS.md | Isporučeno stanje, ograničenja i evidencija provjera |

Kod promjene pravila, sadržaja, balansa, UI toka, zvuka ili opsega ažurirati relevantan odjeljak ovog dokumenta u istom zadatku. Zabilježiti datum, izvor i stvarni status. Ako kod odstupa od namjere, evidentirati razliku umjesto tihog prepisivanja dizajna. Brojevi ispod su pregled trenutnih baza, ne druga lokacija za podešavanje balansa. Ne uvoditi buduće značajke kao implementirane. Dokument ne ažurira sam sebe tijekom ručnih izmjena izvan razvojnog zadatka; tada treba napraviti pregled razlika.

## 2. Vizija igre

**Dogovoreni smjer:** potezna fantasy strategija inspirirana Heroes of Might and Magic III, s heavy metal zvukom kao središtem korisničkog iskustva. Igrač vodi heroja i vojsku, putuje mapom, susreće skupine jedinica, vodi taktičke borbe i koristi gradove za razvoj snage.

Zvuk reagira na odluke: mirno promatranje svijeta, jahanje, prijateljski susret, prijetnju, početak borbe, mali ili veliki udarac, primljenu štetu i pobjedu. Metal je dio identiteta i povratne informacije cijele igre.

Stupovi dizajna:

1. Zvučno izražajno putovanje i borba.
2. Jasne strateške odluke o kretanju, vojsci, zlatu i opremi.
3. Taktička borba sa stackovima na heksovima.
4. Gradovi povezani s klanovima, ekonomijom i vlasništvom.
5. Herojski, mračni slikarski fantasy s metal motivima.

Implementirana platforma: Windows x64, single-player prototip, Unity 6000.3.26f1. Ciljana publika, konačne platforme, cijena, trajanje partije i opseg komercijalne igre: **otvoreno**.

## 3. Glavni ciklus igranja

Planiraj put → potroši dnevno kretanje → riješi susret → vodi borbu → primi nagrade → opremi heroja → regrutiraj i rasporedi vojsku → izazovi jaču vojsku.

Dan je strateški potez. Borba ima zasebne runde i poteze jedinica. Završetak dana obnavlja kretanje, a početak novog tjedna povećava gradske zalihe.

Prijedlog iskustva: svaka etapa treba donijeti čitljivu odluku i prepoznatljivu zvučnu promjenu. Ciljane minute do prvog susreta, prve borbe i završetka misije treba odrediti nakon playtesta.

## 4. Svijet, likovi i frakcije

Postojeći sadržaj:

| Entitet | Aktualna uloga |
|---|---|
| Kael Ironsong | Warlord frakcije Iron Oath, vođa bez vlastitog uporišta |
| Ashen Clan | Klan koji drži Ashen Hold i naplaćuje novačenje |
| Lord Varg | Imenovani klanski branitelj grada |
| Black Banner | Završna neprijateljska vojska prve misije |
| Ashen Pass | Prolaz između početnog područja i grada |

Šira povijest svijeta, motivacija sukoba, dijalozi, identitet ostalih klanova i kampanja: **otvoreno**. Ne pretpostavljati da Iron Oath ili Ashen Clan već imaju kompletan roster ili zasebna pravila frakcije.

## 5. Adventure mapa i vrijeme

**Implementirano (0.6):** naslikana karta Ashen Passa (3200 × 1920 px) podijeljena je na kvadratnu mrežu 50 × 30 ćelija po 64 px; taktička borba koristi zasebnu heksagonalnu mrežu. Heroj počinje na zapadnom kraju puta (2, 14), dan 1, sa 1600 bodova kretanja. Model kretanja slijedi klasične strateške igre (Heroes 3), a vrijednosti su u TerrainDatabase.asset:

| Teren | Cijena ulaska / ponašanje |
|---|---|
| Cesta (put) | 75 |
| Trava, zemlja | 100 |
| Pepeo, kamenjar | 125 |
| Pijesak | 150 |
| Močvara | 175 |
| Šuma (stabla), planina i stijene, voda, lava, prepreke (ruševine) | Neprohodno za heroja na konju |

Dijagonalni korak košta 1,41 puta više od ravnog. Među jednako skupim rutama bira se najkraća i najravnija.

Postoji dijagonalno kretanje bez presijecanja blokiranih kutova. Klik odabire odredište i prikazuje rutu; drugi klik ili Space potvrđuje putovanje. Susret zaustavlja prolaz i zahtijeva interakciju. Heroj prelazi jedno polje u sekundi (Travel Step Seconds = 1 u AshenPass.asset, Presentation pacing); figura klizi kontinuirano od polja do polja.

E završava dan i obnavlja dnevni budžet. Tjedan traje 7 dana; zalihe rastu na danima 8, 15, 22 itd. Nove jedinice dodaju se preostalim zalihama.

Fog of war, rudnici, skupljanje resursa na mapi, više heroja, proceduralne mape i neprijateljski strateški potezi nisu dio opisanog prototipa. Njihova potreba i pravila: **otvoreno**.

**Dogovoreni smjer (2026-10-12):** za početak koristiti jednu fiksnu ilustriranu pozadinu Ashen Passa, bez vizualnih tileova; logičku mrežu i prohodnost prilagoditi slici.

**Implementirano (0.5):** mapa i bojište su world objekti, a UI ostaje uGUI. Mapa je slika podijeljena na kvadratne ćelije; broj piksela po ćeliji je postavka s gumbom Rebuild. Prohodnost se crta logičkim Tilemap slojem preko slike (Grass, Road, Forest, Water, Mountain), a neobojane ćelije su neprohodne. Mape smiju biti veće od ekrana: kamera prati heroja dok putuje, a strelice i povlačenje pomiču pogled; zum još nije implementiran.

**Implementirano (0.6):** uvezena je naslikana karta Map_1, obrezana na 3200 × 1920. Na Full HD prikazuje se piksel za piksel i pomiče se panom. Put vodi od zapadne doline preko dviju čistina, kroz uski planinski prolaz (Pass Wardens), do vrata Ashen Holda i dalje na veliku istočnu čistinu (Black Banner). Dvorac je naslikan na karti, pa grad nema zaseban marker. Logički sloj je prvi nacrt, napravljen automatskom klasifikacijom boja slike i ručnim ispravcima (put, prolaz, dvorac, voda); treba ga pregledati i doraditi paletom.

## 6. Susreti i prva misija: The Ashen Pass

Početak: 1.600 golda, Kael, 12 Roadieja, attack 2, defense 2.

| Redoslijed | Lokacija | Sadržaj | Rezultat / nagrada |
|---|---|---|---|
| 1 | The Willing (13, 14), čistina uz put | 6 Air Guitarista želi se pridružiti | Besplatno pridruživanje; opcionalno, ne blokira put |
| 2 | The Pass Wardens (21, 15), planinski prolaz | 10 Roadieja blokira jedini prolaz | 180 XP, oko 500 golda, možda artefakt |
| 3 | Ashen Hold (32, 13), ispred vrata | Neutralni grad Ashen Clana | Kupnja tri vrste jedinica |
| Opcionalno | Branitelji Ashen Holda | 13 Roadieja + 5 Air Guitarista | Vlasništvo grada, 250 XP, oko 300 golda, možda artefakt |
| 4 | The Black Banner (44, 15), istočna čistina | 18 Roadieja + 9 Air Guitarista + 3 Doom Ridera | 450 XP, oko 900 golda, možda artefakt; završetak misije |

Bitke u prolazu vode se na zelenoj dolini, a kod Ashen Holda i Black Bannera na infernalnoj pustoši. Put do istoka traje više dana.

Prva misija uvodi pridruživanje, obaveznu borbu, opremanje nagrade, novačenje i jači završni izazov. Osvajanje grada dodatna je opcija. Nagrade se dodjeljuju jednom; zatvaranje ili ponovno prikazivanje panela ne smije ih duplicirati.

**Implementirano (0.7):** nagrade bitke su nasumične. Gold je uvijek dodijeljen: iznos susreta × faktor između 0,75 i 1,25. Pobjeda donosi najviše jedan artefakt: 50 % šanse za artefakt; od toga 10 % legendary, 30 % rare, ostalo common. Prednost imaju artefakti koje heroj još nema. Legendary artefakt je zajamčen najkasnije na svaku četvrtu pobjedu (privremeno pravilo za testiranje legendary zvuka; niz se broji kroz sesiju, ne sprema se). Sve vrijednosti su u AshenPass.asset (Battle rewards). Nakon zadnjeg udarca bojište ostaje 2 sekunde na ekranu (Battle Result Seconds u AshenPass.asset, Presentation pacing), zatim se otvara ekran Victory (XP, gubici, level), zatim Rewards (gold i kartica artefakta; legendary kartica ima posebnu boju), a poraz otvara ekran Defeat (retry, povratak na pripremu, nova misija).

Prijedlog budućeg zapisa svake misije: ID, sažetak, početno stanje, mapa, obavezni/opcionalni ciljevi, susreti, dijalozi, nagrade, pobjeda/poraz, audio tok, posebna pravila i kriteriji provjere.

## 7. Vojske i jedinice

**Implementirano:** 7 slotova po vojsci. Jednake vrste se spajaju u stack. Stack čuva vrstu i broj jedinica; u borbi ima ukupni HP. Djelomično razdvajanje stackova nije implementirano.

Aktualne vrijednosti iz UnitDatabase.asset:

| Jedinica | HP | Damage | Attack | Defense | Speed | Neutralna cijena | Tjedni rast | Uloga |
|---|---:|---:|---:|---:|---:|---:|---:|---|
| Roadie | 18 | 3–5 | 5 | 4 | 4 | 70 | 14 | Melee pješaštvo (s flight caseom) |
| Air Guitarist | 13 | 3–6 | 6 | 3 | 5 | 110 | 9 | Ranged, pola štete u melee |
| Doom Rider | 40 | 7–11 | 9 | 8 | 6 | 260 | 4 | Teška konjica |

Roadie je zamijenio Ironbounda, a Air Guitarist Ash Archera (prototip 0.6); statovi i interni ID-evi (`ironbound`, `ash-archer`) ostali su isti. Obojica imaju animaciju hoda od 8 frameova, a Doom Rider za sada koristi placeholder ikonu.

Svaka vrsta ima stabilni ID, naziv, opis, opcionalni portret, opcionalnu animaciju hoda i vlastiti zvuk glasa (SoundDefinition). Svi statovi i reference uređuju se u UnitDatabaseScriptableObject.

Air Guitarist i Roadie imaju izrađen konceptualni art u vanjskoj zbirci. Njihov status je **vizualni koncept**, bez potvrđenih combat statova, sposobnosti, cijene, tiera ili frakcije. Ne zamjenjuju postojeće jedinice bez dizajnerske odluke.

Prijedlog kartice buduće jedinice: frakcija/tier, borbena uloga, statovi, cijena/rast, sposobnosti, kontraigra, silueta, animacije, portret, glasovi i zvuk napada.

## 8. Taktička borba

**Implementirano:** 10 × 7 odd-row heksova. Igračevi stackovi počinju u lijevom dijelu ploče; neprijateljski u desnom. Jedan stack izvršava jednu akciju po rundi.

Inicijativa: speed silazno; pri jednakom speedu igrač prije neprijatelja, zatim stabilni red stacka. Artefaktni speed bonus utječe na igračeve stackove. Zauzete ćelije blokiraju prolaz; domet kretanja određuje speed.

Akcije:

- Move: kretanje na slobodan dohvatljiv heks.
- Attack: ranged napad ili melee; melee može uključiti dolazak do dohvatljive susjedne ćelije.
- Defend: primljena šteta × 0,7.
- Pass: završetak poteza bez akcije; nije odgoda inicijative.

**Implementirano (0.7):** svaki udarac, i uzvrat, ima 15 % šanse za promašaj (Miss Chance u AshenPass.asset): bez štete i gubitaka, na bojištu piše MISS, a zvuk je promašaj mača ili strijele. Promašeni melee napad i dalje izaziva uzvrat.

Melee meta uzvraća ako preživi i nije već uzvratila u toj rundi. Ranged napad na udaljenosti ne izaziva taj melee uzvrat. Air Guitarist u melee radi štetu × 0,5. Prototip nema streljivo, zaklone, magiju ni posebne sposobnosti.

Formula aktualnog damagea:

`delta = unitAttack + heroAttack − targetDefense − targetHeroDefense`

Ako je delta ≥ 0: množitelj = `1 + min(delta, 30) × 0,05`.
Ako je delta < 0: množitelj = `1 − min(−delta, 28) × 0,025`.

Šteta = broj napadača × interpolirana vrijednost između min/max damagea pomoću random rolla × množitelj. Zatim se primjenjuju defend i ranged-melee penal. Zaokruživanje je na najbliži cijeli broj, polovice na paran broj; minimum štete 1. Neprijateljski hero attack/defense trenutno su 1/1 iz misije.

Broj preživjelih = zaokruženi preostali ukupni HP prema gore / HP jedne jedinice. Pobjeda nastupa kad nema živih neprijateljskih stackova; poraz kad nema igračeve vojske. Gubici ostaju nakon pobjede, a preživjele jedinice vraćaju puni HP između borbi. Poraz ne zapisuje trajne gubitke i omogućuje retry s vojskom prije borbe ili povratak na pripremu.

Jednostavni AI bira najbližu metu, zatim niži ukupni HP i stabilni ID. Napada ako može, inače ide prema meti. Nakon svake akcije u borbi (igračeve ili neprijateljeve) slijedi pauza od 1,5 s prije sljedeće akcije, da se potezi ne mogu prokliknuti jedan za drugim (Battle Action Seconds u AshenPass.asset, Presentation pacing). Neprijatelj koji počinje borbu čeka 0,7 s (Enemy Turn Seconds). Težine, napredniji AI, wait, bježanje i predaja: **otvoreno**.

## 9. Heroj, XP i artefakti

Kael ima portret, opis, klasu, frakciju, vojsku, XP, level, osnovne/izvedene statove, inventar i opremu. Svakih 300 XP daje novi level; attack raste svakim levelom, defense svaka dva levela. Detalji su podesivi u HeroDatabase.asset.

Implementirani slotovi opreme: weapon i charm. **Dogovoreni budući smjer (2026-10-12):** hero screen treba imati 10 equipment slotova: Head, Ring 1, Ring 2, Amulet, Left Hand, Right Hand, Torso, Sash, Pants i Boots, uz inventory i 7 slotova vojske. Nova podjela opreme još nije implementirana; kompatibilnost itema, dvoručno oružje i migracija postojećih slotova ostaju otvoreni. Koncept prikazuje 5 × 5 inventory polja kao prijedlog prikaza, ne potvrđeno ograničenje kapaciteta. **Dogovoreni vizualni smjer itema (2026-10-12):** common jednostavan i praktičan, rare atraktivniji i ukrašen, epic izrazito upečatljiv, legendary najraskošniji; izrađene ilustracije za svaki slot i razinu. Gameplay učinci rijetkosti i pravila dropova nisu definirani. Bonusi se računaju iz opreme i ne upisuju trajno u osnovne statove.

| Artefakt | Bonus | Dostupnost |
|---|---|---|
| Fang of Thunder | rare, +2 attack | nasumična nagrada |
| Ashen Ward | common, +2 defense | nasumična nagrada |
| Warborn Sigil | rare, +1 speed | nasumična nagrada |
| Rusted Cleaver | common, +1 attack | nasumična nagrada |
| Studded Wristband | common, +1 defense | nasumična nagrada |
| Axe of the First Riff | legendary, +4 attack, +1 speed | nasumična nagrada |
| Amulet of Endless Feedback | legendary, +1 attack, +3 defense, +1 speed | nasumična nagrada |

Artefakti imaju ID, naziv, opis, ikonu, kompatibilni slot i bonuse u ArtefactDatabaseScriptableObject. Rarity (common, rare, legendary) određuje samo vjerojatnost dropa i izgled kartice. Skill tree, herojske aktivne sposobnosti, epic razina, set bonusi, trgovina i više heroja: **otvoreno**.

## 10. Gold, gradovi i vlasništvo

**Implementirano:** gold je jedini resurs. Početna količina i nagrade dolaze iz misije. Troši se na novačenje; nema definirane dnevne gradske zarade u ovom prototipu.

Ashen Hold ima unaprijed pripremljenu ponudu tri vrste jedinica. Početna zaliha: 14 Roadieja, 9 Air Guitarista, 4 Ridera. UI omogućuje odabir količine i MAX prema zalihi, zlatu i prihvatljivoj vojsci. Kupnja mora biti potpuna ili odbijena bez djelomičnog trošenja.

| Vlasništvo | Home / garnizon | Visiting | Cijene |
|---|---|---|---|
| Ashen Clan | Zaštićena klanska vojska | Igračev heroj i vojska | Osnovne cijene |
| Igrač | Igračev garnizon, opcionalni home heroj | Igračev visiting heroj | ceil(osnovna cijena × 0,55) |

Aktualne owned cijene: Roadie 39, Air Guitarist 61, Rider 143. Osvajanje traži pobjedu nad braniteljima. U vlastitom gradu moguć je transfer cijelog stacka između redova, uz spajanje jednakih tipova i provjeru kapaciteta. Postavljanje Kaela kao home heroja spaja vojsku s home vojskom; prije novačenja i putovanja treba ga vratiti u visiting stanje.

**Dogovoreni budući smjer:** neki vlasnici omogućuju ulaganje u građevine za povoljnije cijene; grad može biti preuzet borbom ili nagodbom; vlastiti grad omogućuje ulaganje u gradnju i povoljniju proizvodnju.

**Otvoreno:** uvjeti nagodbe i procjena snage, odnosi s klanovima, vrste/cijene građevina, vrijeme gradnje, trajanje popusta, posljedice ulaganja nakon osvajanja i dodatni resursi. Aktualnih 55% je prototipna vrijednost, ne konačno pravilo ekonomije.

## 11. Audio kao glavni UX

**Dogovoreni smjer:** originalni heavy metal s herojskom i mračnom atmosferom; Manowar/Bathory služe kao atmosferske reference. Ambient i galop petlje, dio kratkih efekata i glasovi su placeholder sadržaj; snimljeni zvukovi i licence su u AUDIO_CREDITS.md.

| Stanje / događaj | Zvučna namjera | Aktualno stanje |
|---|---|---|
| Stajanje na mapi | Miran metal ambient | Petlja Spirit Horse (privremena, samo za interno testiranje) utišana dok heroj stoji; nastavlja gdje je stala |
| Grad | Gradska tema | Snimljena srednjovjekovna petlja (Market Day) dok je otvoren grad |
| Jahanje | Metal galop i kopita | Glazba mape se kratkim fadeom pojača dok heroj jaše; snimljena petlja kopita preko nje (sintetizirani galop sloj svira samo kad misija nema glazbu mape) |
| Friendly susret | Pozitivan, upbeat lick | Snimljeni gitarski akord |
| Enemy susret | Prijeteći lick | Snimljeni distorzirani power chord |
| Borba | Metal intro i borbena tema | Snimljena tema: intro jednom, zatim petlja |
| Napad jedinice | Prepoznatljiv glas | Voice cue po vrsti |
| Udarac mačem / strijelom | Zamah ili ispaljivanje, zatim pogodak ili promašaj | Snimljeni sword swing/hit/miss i arrow fire/hit/miss; veliki pogodak dodaje heavy hit |
| Pobjeda | Kratka metal završnica | Snimljeni gitarski tapping stinger, zatim victory tema (rock/metal) s dužim fade inom koja odsvira do kraja uz duži fade out |
| Poraz | Odgovarajuća završnica | Snimljena defeat tema dok je ekran poraza otvoren |
| Nagrade / legendary item | Zvuk plijena; posebna potvrda za legendary | Gold cue; snimljeni zbor za legendary |
| Level, gold, oprema, klik | Kratka potvrda radnje | Kratki efekti |

**Implementirano:** zvuk ide kroz MLin Audio System. GameAudio prima uspješne gameplay događaje, AudioDatabaseScriptableObject ima po jedno polje za svaki događaj i glazbeni sloj, a svaka jedinica izravno pokazuje na svoj glas. Svako polje pokazuje na SoundDefinition asset (clipovi i varijacije, glasnoća, pitch, prioritet, limit istovremenih instanci, cooldown, mixer grupa). Audio ne određuje pravila borbe. Ambient, gallop i battle slojevi (100 BPM, 9,6 s) kreću na istom DSP trenutku i miješaju se fadeom prema putovanju i borbi. Glasovi jedinica zamjenjuju jedan drugoga, a glazba se stišava 6 dB dok jedinica govori (ducking pravilo Voice → Music).

Postoje odvojeni kanali Music/Effects/Voice na mixeru (dosadašnje spremljene postavke igrača su zadržane), mute preko Master kanala, utišavanje kad prozor izgubi fokus i F3 razvojni panel za preslušavanje svakog cuea. Zamjena zvuka u SoundDefinition assetu ili u AudioDatabaseu vrijedi nakon novog Play Modea ili novog builda; New Mission ne učitava baze ponovno.

Prijedlog produkcije: kompatibilne snimljene petlje, više varijanti lickova, originalni glasovi i glazbeni prijelazi na beat/bar kad igra dobije zasebne pjesme (audio sustav ih već podržava). Klik i udarac moraju dobiti brz odgovor. Treba definirati prag velikog udarca i pravila za kombinaciju kill/retaliation/received cueova te testirati zamor tijekom cijele misije.

## 12. Art i animacija

**Dogovoreni smjer:** slikarski heroic fantasy prema dosadašnjim referencama na Franka Frazettu; čitljive siluete u maloj veličini i metal motivi.

| Asset | Lokacija u vanjskoj zbirci | Status |
|---|---|---|
| Main menu — mračni jahač | assets/ui/main-menu/dark-rider-main-menu-v1-1920x1080.png | Konceptualna pozadina menija: mišićavi jahač skrivenog lica na crnom konju, fantasy krajolik i udaljeni dvorac; kompozicijska referenca Heroes III i slikarski smjer Franka Frazette. Full HD izvoz 1920 × 1080 interpoliran iz generiranog izvornika 1672 × 941, koji je također sačuvan. Bez teksta/gumba, gameplay uloge ili Unity integracije; izrađeno 2026-10-14 |
| Heroj na konju, udesno | assets/hero_horse_right | 4 PNG framea i sheet |
| Heroj na konju, udesno | assets/hero_horse_right_8frames; kopija: C:/Users/jaksa/Downloads/HOMA_ART/Hero_Horse_WorldMap_Walk_Right_8Frames | Konceptualna animacija: 8 RGBA frameova 444 × 444 i sheet; predloženo 90 ms/frame. Kopija svih frameova, sheeta i README-a provjerena 2026-10-12; nije ovim korakom integrirano u Unity |
| Air Guitarist | assets/air-guitarist | Koncept, više 8-frame walk varijanti, sheetovi i GIF previewi |
| Equipment ikone — 4 razine | assets/items/metal-equipment-v1 | 40 konceptualnih transparentnih PNG ikona 512 × 512: 10 slotova × common/rare/epic/legendary; 4 izvorna sheeta, overview i manifest. Left Hand je ilustriran štitom, Right Hand mačem; ilustracije nisu potvrđena pravila kompatibilnosti. Bez statova, loot pravila ili integracije |
| Hero screen | assets/ui/hero-screen/hero-screen-v1.png | Koncept UI-a: portret/statovi, 10 equipment slotova prema korisnikovu popisu, inventory 5 × 5 i red sa 7 army slotova. Nije integrirano; tekst, slotovi i ikone kategorija ucrtani su u koncept |
| Ashen Hold — town screen | assets/town/ashen-hold/ashen-hold-town-screen-v1.png | Konceptualna slika grada s dvorcem, odvojenim home/visiting redovima (portret + 7 army slotova) i vizualno odvojenim zgradama za Ironbound, Ash Archer i Doom Rider. Zgrade su prijedlog vizualnog odabira regrutacije, ne implementirano pravilo proizvodnje/gradnje. Nije integrirano; klikovi, portreti i sadržaj slotova zahtijevaju zaseban UI |
| Ashen Pass — battle pozadine | assets/battle/ashen-pass | Dvije generirane konceptualne pozadine: battle-green-v1.png (zelena šumska čistina) i battle-infernal-v1.png (pepeljasto/infernalno bojište). Otvorena sredina za zasebne heksove i jedinice; nisu integrirane u gameplay |
| UI okviri — drvo i kovano željezo | assets/ui/wood-iron-v1 | Tri generirana transparentna PNG okvira: screen s kuglicama, manji dialog, veći menu panel. Pripremljene preporučene 9-slice granice u README-u; nije integrirano niti provjereno u Unityju |
| Ashen Pass — fiksna mapa | assets/maps/ashen-pass/ashen-pass-background-v1.png; izvoz: C:/Users/jaksa/Downloads/HOMA_ART/Map_1.png | Generirana konceptualna pozadina: zapadna šuma i čistine, planinski prolaz, Ashen Hold, istočno pepeljasto područje; bez heroja/vojski i bez grida. Izvoz Map_1.png povećan 2026-10-12 s 1605 × 980 na 3210 × 1960 Lanczos interpolacijom, bez generiranja novih detalja; original izvoza sačuvan kao Map_1-original-1605x980.png. Nije ovim korakom integrirana; prilagodba mreže i prohodnosti tek slijedi |
| Roadie | assets/roadie; kopija: C:/Users/jaksa/Downloads/HOMA_ART/Roadie_Walk_Right_8Frames | Vizualni koncept s flight caseom i kabelima; izrađeno 8 PNG walk frameova udesno, sheet i GIF preview na 8 fps. Kopija provjerena 2026-10-12; bez potvrđene Unity integracije |

**Izvoz grupa animacijskih frameova (2026-10-12):** u `C:/Users/jaksa/Downloads/HOMA_ART` kopirano je svih 6 grupa iz vanjske zbirke, s izvornim imenima datoteka, pripadajućim sheetovima i dostupnim GIF previewima te promptovima/README-ima. Originali su sačuvani. Sadržaj kopija provjeren je SHA-256 usporedbom i brojem frameova:

| Folder u HOMA_ART | Broj PNG frameova |
|---|---:|
| Hero_Horse_WorldMap_Walk_Right_4Frames | 4 |
| Hero_Horse_WorldMap_Walk_Right_8Frames | 8 |
| Air_Guitarist_Walk_Right_With_Cape_8Frames | 8 |
| Air_Guitarist_Walk_Right_Air_Guitar_No_Cape_8Frames | 8 |
| Air_Guitarist_Walk_Right_Air_Guitar_Lively_v2_8Frames | 8 |
| Roadie_Walk_Right_8Frames | 8 |

Ukupno 44 pojedinačna framea; ovo je organizacija i izvoz konceptualnog arta, bez promjena gameplay sadržaja, statova ili Unity integracije.

Air Guitarist hoda i svira zamišljenu gitaru, bez stvarnog instrumenta i bez plašta. Posljednja varijanta ima izraženije naginjanje/head bob, ali izmjena nogu još traži doradu. Roadiejev koncept nosi opremu, s prepoznatljivim osjećajem težine.

Ovi asseti nisu ovom revizijom potvrđeni kao integrirani u Unity gameplay. Trenutni prototip koristi placeholder/fallback grafiku.

Prijedlog standarda: transparentna pozadina, jednak scale/pivot/baseline, bez rezanja siluete, dosljedna kamera i svjetlo, čitljivost na ciljanoj veličini, pouzdan loop. Odrediti dimenzije u igri, smjerove, idle/attack/hit/death animacije, atlas i pravila imenovanja prije masovne proizvodnje.

## 13. UI i kontrole

**Implementirano:** svi produkcijski ekrani koriste Canvas/uGUI, TextMeshPro i spremljene prefabe. I F3 razvojni panel je uGUI prefab; IMGUI se više ne koristi.

Ekrani: main menu, adventure, susret, combat, town, hero/inventory/equipment, victory, defeat, options i obavijesti/HUD.

**Main menu (implementirano):** pozadina u temi svijeta i glazba mape. New Game je ruka na desnoj strani: hover napinje biceps, klik diže metal rogove, zatim fade to black slike i zvuka, nekoliko sekundi crnila i intro na crnom: tri strofe ("As the day breaks, he is already awake / A yearning in his soul / On the journey he must go", "Answers he seeks not / But power of freedom / A stream of life", "But first / He must uncover") ispisuju se slovo po slovo, liniju po liniju na sredini ekrana; prve dvije stoje 5 sekundi, zadnja (s pauzom od 3 sekunde poslije "But first") 2 sekunde, pa svaka nestaje fade outom. Zatim se sporim fade inom pojavljuje veći naslov "The Secret of Metal" i s njim kreće glazba mape s dugim fade inom; par sekundi nakon što se naslov pojavi, crni ekran s naslovom izgori kao papir (ugljenisani rub, žar, iskre) i ispod se otkrije mapa s početkom igre. Load Game, Credits i Quit su zasad placeholderi koji imaju samo hover i zvuk klika. Dijalog (portrait box ili balon) na mapi zatvara se klikom bilo gdje.

Adventure slojevi: Environment (Grass/Road/Forest/Mountain/Water), Heroes, Enemies, Friendly Units, Towns i Route. Combat ima odvojene heksove i Player/Enemy Units.

| Kontrola | Radnja |
|---|---|
| Klik pa potvrda klikom / Space | Planiranje i izvršenje putovanja |
| E | Završetak dana |
| H | Equipment ikone — 4 razine | assets/items/metal-equipment-v1 | 40 konceptualnih transparentnih PNG ikona 512 × 512: 10 slotova × common/rare/epic/legendary; 4 izvorna sheeta, overview i manifest. Left Hand je ilustriran štitom, Right Hand mačem; ilustracije nisu potvrđena pravila kompatibilnosti. Bez statova, loot pravila ili integracije |
| Hero screen |
| Escape | Options ili povratak iz grada/hero screena |
| M | Mute |
| F3 | Razvojni panel: preslušavanje cueova i prečaci kampanje |
| Klik heksa / neprijatelja | Kretanje / napad u borbi |

Dizajnerski kriteriji: jasan aktivni stack i dohvatljiva polja, prikaz golda i movementa, čitljive količine/cijene, razlog odbijene radnje, vidljive nagrade/gubici, modal koji blokira pozadinski input. Zvuk treba nadopuniti informacije koje igrač vidi. Remapping, gamepad, lokalizacija, skaliranje teksta i proširena pristupačnost: **otvoreno**.

## 14. Spremanje i kontinuitet

**Implementirano:** manual save/load i autosave u sigurnim stanjima nakon kretanja, kupnje, opreme i pobjeda. Save schema v3 koristi stabilne ID-eve; saveovi starijih verzija nastali su na staroj karti i odbijaju se (igra tada kreće novu misiju). Zamjena koristi privremenu datoteku i .bak; neispravan load ne smije zamijeniti aktivnu kampanju.

Lokacija: `%USERPROFILE%/AppData/LocalLow/Iron Oath Studio/HeroesOfMetalAge/campaign.json`.

Nova misija resetira stanje u memoriji; sljedeći autosave zamjenjuje postojeći save. Nastavak usred borbe, više save slotova, cloud saves i konačna politika kompatibilnosti: **otvoreno**.

## 15. Produkcija, balans i provjera

Podesivi sadržaj: AudioDatabase, UnitDatabase, ArtefactDatabase, HeroDatabase i AshenPass mission settings. Izmjena pravila ostaje odvojena od vizualizacije i audia. Arhitektura je opisana u HOMA_ARCHITECTURE.md.

Trenutni prototip ima igrivu misiju i refaktorirane slojeve, podatkovne baze, prefab UI i spremanje. STATUS.md sadrži raniju evidenciju provjera; ovaj dokument ne ponavlja tvrdnju da su testovi sada pokrenuti.

Prijedlog razvojnih etapa:

1. **Konsolidacija prototipa:** korisnikov playthrough, jasna vidljivost aktera, razumljivo novačenje, stabilni saveovi.
2. **Zvučni i vizualni vertikalni segment:** finalizirati mali reprezentativni dio s originalnim petljama, glasovima i integriranim animacijama.
3. **Gradovi i klanovi:** definirati pa implementirati ulaganja, gradnju i nagodbe.
4. **Širenje sadržaja:** nove jedinice/heroji/frakcije, mape i kampanja nakon potvrđenog osnovnog iskustva.

Predložene playtest metrike: vrijeme do prvog zanimljivog događaja, trajanje borbe, broj dana do boss susreta, preostalo zlato, gubici po borbi, korištenje osvajanja, razumljivost zvučnih signala i zamor od ponavljanja. Ciljane vrijednosti zasad nisu određene.

Kriteriji prve misije: pridruživanje, pobjeda na prolazu, opremanje nagrade, kupnja u gradu, finalna pobjeda; dodatno poraz/retry, tjedni rast, zaštita klanskog garnizona, owned transferi i home/visiting stanje. Audio provjeriti u kontinuitetu, UI vizualno u buildu i Editoru. Automatske provjere moraju koristiti izolirane saveove.

## 16. Odluke za sljedeće dizajnerske razgovore

| Prioritet | Otvorena odluka | Zašto je potrebna |
|---|---|---|
| 1 | Roster: Roadie i Air Guitarist su uvedeni (prototip 0.6); koji metal arhetip zamjenjuje Doom Ridera i koje jedinice dolaze dalje? | Određuje odnos nove grafike i gameplaya |
| 1 | Uloge Air Guitarista i Roadieja | Koncepti još nemaju pravila ni frakciju |
| 1 | Konačan audio identitet i pragovi intenziteta | Središte korisničkog iskustva |
| 2 | Opseg prve dovršene verzije | Broj misija, frakcija, heroja i jedinica |
| 2 | Pravila gradnje, ulaganja i nagodbe | Potrebna prije širenja ekonomije |
| 2 | Lore i ton: ozbiljan epic metal ili više humora | Usklađuje tekstove, glasove i art |
| 3 | Magija, skillovi, posebne sposobnosti | Mijenja kompleksnost borbe |
| 3 | Kampanja/skirmish, platforme, multiplayer | Određuje dugoročni scope |

## 17. Dnevnik promjena

| Datum | Revizija | Promjena | Provjera |
|---|---|---|---|
| 2026-10-10 | 0.1 | Početni GDD: vizija iz razgovora, postojeća misija/pravilnici/baze/kod, katalog arta i otvorene odluke; uvedeno održavanje uz razvoj | Pregled lokalnih izvora; bez novog Unity builda |
| 2026-10-12 | 0.1 — art mape | Korisnik odabrao fiksnu sliku s naknadnom prilagodbom logičkog grida; izrađena prva konceptualna pozadina Ashen Passa u vanjskoj zbirci | Vizualni pregled generirane slike; bez Unity integracije ili builda |
| 2026-10-12 | 0.1 — UI art | Izrađen vanjski set triju drvenih UI okvira s metalnim ukrasima i transparentnom sredinom, za 9-slice upotrebu | Vizualni pregled i provjera alpha uzoraka; bez Unity integracije/builda |
| 2026-10-12 | 0.1 — battle art | Izrađene dvije battle pozadine za zeleni i infernalni dio Ashen Passa; kopije spremljene i u Downloads | Vizualni pregled; bez Unity integracije/builda |
| 2026-10-12 | 0.1 — town art | Izrađen koncept town screena Ashen Holda s home/visiting vojskama i zgradama za odabir postojećih jedinica | Vizualni pregled; bez funkcionalnih klikova, promjene ekonomije ili Unity integracije/builda |
| 2026-10-12 | 0.1 — hero screen | Izrađen hero screen koncept i zabilježen korisnikov budući raspored 10 equipment slotova; inventory 5 × 5 ostaje vizualni prijedlog | Vizualni pregled; bez promjena gameplaya, saveova ili Unity integracije/builda |
| 2026-10-12 | 0.2 | Audio prebačen na MLin Audio System: cueovi kao SoundDefinition asseti, mixer kanali Music/Effects/Voice, ducking glazbe dok jedinica govori, sinkronizirani slojevi, utišavanje bez fokusa. F3 panel prebačen na uGUI. Kod refaktoriran bez promjene pravila igre | EditMode testovi i scenarij cijele misije u buildu |
| 2026-10-12 | 0.3 | Mapa i bojište prebačeni u world space (URP 2D): slika mape (za sada generirani placeholder) s logičkim Tilemap slojem prohodnosti i podesivom veličinom ćelije (Rebuild), kamera koja prati heroja uz pan strelicama i povlačenjem, bojište kao fiksna slika s hex mrežom, animacije figura, brojevi štete i trešnja kamere. Pravila igre nepromijenjena | EditMode testovi i scenarij cijele misije u buildu |
| 2026-10-13 | 0.7 | Victory tema nakon stingera (duži fade in i fade out); glazba mape Spirit Horse (privremena) tiša dok heroj stoji, glasnija dok jaše; heroj prelazi jedno polje u sekundi | EditMode testovi i scenarij cijele misije u buildu |
| 2026-10-13 | 0.7 | Pauza od 1,5 s nakon svake akcije u borbi (Battle Action Seconds) | EditMode testovi i scenarij cijele misije u buildu |
| 2026-10-13 | 0.7 | Glazba mape i grada po misiji (overworld, Market Day; underworld tema pripremljena za buduću podzemnu mapu); pauza od 2 s prije ekrana pobjede/poraza (Battle Result Seconds) | EditMode testovi i scenarij cijele misije u buildu |
| 2026-10-13 | 0.7 | Snimljeni zvukovi za borbu (intro i petlja), poraz, kopita, mač i strijelu (zamah/pogodak/promašaj), susrete, pobjedu i legendary item; ekrani Victory, Rewards i Defeat; nasumične nagrade (uvijek gold, najviše jedan artefakt, rarity, zajamčen legendary najkasnije svake četvrte pobjede); promašaji u borbi | EditMode testovi i scenarij cijele misije u buildu |
| 2026-10-15 | 0.7 | Intro: strofe koje se ispisuju slovo po slovo, naslov "The Secret of Metal" uz fade in glazbe mape i crni ekran koji izgori kao papir (shader) i otkrije mapu | Kompilacija izvan Editora i provjera vremenske logike; EditMode testovi i izgled spaljivanja još nisu provjereni u Unityju |
| 2026-10-15 | 0.7 | Main menu s animiranim New Game gumbom (hover/klik animacije i zvukovi), fade to black, crnilo, placeholder intro i fade in u misiju; dijalog na mapi zatvara se klikom bilo gdje | Kompilacija svih assemblyja izvan Editora; EditMode testovi i scenarij još nisu pokrenuti |
| 2026-10-12 | 0.4 | Full HD (1920 × 1080) kao ciljna i dizajn rezolucija; uvezena naslikana karta Map_1 (50 × 30 ćelija po 64 px) s prvim nacrtom logičkog sloja i susretima na putu; model kretanja kao u Heroes 3 (bodovi ×100, cijene po terenu, dijagonala ×1,41, šuma neprohodna); veći prozor karte; heroj na konju s animacijom; Roadie i Air Guitarist umjesto Ironbounda i Ash Archera; pozadine bojišta po susretu; stari saveovi više nisu kompatibilni | EditMode testovi i scenarij cijele misije u buildu na 1920 × 1080 |

| 2026-10-12 | 0.1 — equipment art | Izrađeno 40 item ikona za 10 equipment slotova i 4 razine vizualne kvalitete; rare amulet dorađen radi kompletnog lanca; pripremljeni zasebni PNG-ovi i pregled | Vizualni pregled svih ikona; provjerene dimenzije i transparentni kutovi; bez gameplay statova ili Unity integracije/builda |
