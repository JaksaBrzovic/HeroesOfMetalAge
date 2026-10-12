# Heroes of Metal Age — Game Design Document

Verzija: 0.3 · Ažurirano: 12. listopada 2026. · Jezik dokumenta: hrvatski

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

**Implementirano:** kvadratna mreža 18 × 11; taktička borba koristi zasebnu heksagonalnu mrežu. Heroj počinje na koordinati (2, 5), dan 1, sa 16 movement pointsa.

| Teren | Cijena ulaska / ponašanje |
|---|---|
| Otvoreno tlo i cesta | 1 movement point |
| Šuma | 2 movement pointsa |
| Planina i voda | Blokirano |

Postoji dijagonalno kretanje bez presijecanja blokiranih kutova. Klik odabire odredište i prikazuje rutu; drugi klik ili Space potvrđuje putovanje. Susret zaustavlja prolaz i zahtijeva interakciju. Trenutni interval koraka je 0,15 s, podesiv u misiji.

E završava dan i obnavlja dnevni budžet. Tjedan traje 7 dana; zalihe rastu na danima 8, 15, 22 itd. Nove jedinice dodaju se preostalim zalihama.

Fog of war, rudnici, skupljanje resursa na mapi, više heroja, proceduralne mape i neprijateljski strateški potezi nisu dio opisanog prototipa. Njihova potreba i pravila: **otvoreno**.

**Dogovoreni smjer (2026-10-12):** za početak koristiti jednu fiksnu ilustriranu pozadinu Ashen Passa, bez vizualnih tileova; logičku mrežu i prohodnost prilagoditi slici.

**Implementirano (0.5):** mapa i bojište su world objekti, a UI ostaje uGUI. Mapa je slika podijeljena na kvadratne ćelije; broj piksela po ćeliji je postavka s gumbom Rebuild. Prohodnost se crta logičkim Tilemap slojem preko slike (Grass, Road, Forest, Water, Mountain), a neobojane ćelije su neprohodne. Mape smiju biti veće od ekrana: kamera prati heroja dok putuje, a strelice i povlačenje pomiču pogled; zum još nije implementiran. Trenutna slika mape je generirani placeholder napravljen iz starog grida, pa koordinate i prohodnost još odgovaraju prethodnoj verziji. Prava naslikana pozadina još nije uvezena.

## 6. Susreti i prva misija: The Ashen Pass

Početak: 1.600 golda, Kael, 12 Ironbounda, attack 2, defense 2.

| Redoslijed | Lokacija | Sadržaj | Rezultat / nagrada |
|---|---|---|---|
| 1 | The Willing (4, 5) | 6 Ash Archera želi se pridružiti | Besplatno pridruživanje |
| 2 | The Pass Wardens (7, 5) | 10 Ironbounda blokira prolaz | 180 XP, 500 golda, Fang of Thunder |
| 3 | Ashen Hold (11, 5) | Neutralni grad Ashen Clana | Kupnja tri vrste jedinica |
| Opcionalno | Branitelji Ashen Holda | 13 Ironbounda + 5 Ash Archera | Vlasništvo grada, 250 XP |
| 4 | The Black Banner (15, 5) | 18 Ironbounda + 9 Ash Archera + 3 Doom Ridera | 450 XP, 900 golda, Warborn Sigil; završetak misije |

Prva misija uvodi pridruživanje, obaveznu borbu, opremanje nagrade, novačenje i jači završni izazov. Osvajanje grada dodatna je opcija. Nagrade se dodjeljuju jednom; zatvaranje ili ponovno prikazivanje panela ne smije ih duplicirati.

Prijedlog budućeg zapisa svake misije: ID, sažetak, početno stanje, mapa, obavezni/opcionalni ciljevi, susreti, dijalozi, nagrade, pobjeda/poraz, audio tok, posebna pravila i kriteriji provjere.

## 7. Vojske i jedinice

**Implementirano:** 7 slotova po vojsci. Jednake vrste se spajaju u stack. Stack čuva vrstu i broj jedinica; u borbi ima ukupni HP. Djelomično razdvajanje stackova nije implementirano.

Aktualne vrijednosti iz UnitDatabase.asset:

| Jedinica | HP | Damage | Attack | Defense | Speed | Neutralna cijena | Tjedni rast | Uloga |
|---|---:|---:|---:|---:|---:|---:|---:|---|
| Ironbound | 18 | 3–5 | 5 | 4 | 4 | 70 | 14 | Melee pješaštvo |
| Ash Archer | 13 | 3–6 | 6 | 3 | 5 | 110 | 9 | Ranged, pola štete u melee |
| Doom Rider | 40 | 7–11 | 9 | 8 | 6 | 260 | 4 | Teška konjica |

Svaka vrsta ima stabilni ID, naziv, opis, opcionalni portret i vlastiti zvuk glasa (SoundDefinition). Svi statovi i reference uređuju se u UnitDatabaseScriptableObject.

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

Melee meta uzvraća ako preživi i nije već uzvratila u toj rundi. Ranged napad na udaljenosti ne izaziva taj melee uzvrat. Ash Archer u melee radi štetu × 0,5. Prototip nema streljivo, zaklone, magiju ni posebne sposobnosti.

Formula aktualnog damagea:

`delta = unitAttack + heroAttack − targetDefense − targetHeroDefense`

Ako je delta ≥ 0: množitelj = `1 + min(delta, 30) × 0,05`.
Ako je delta < 0: množitelj = `1 − min(−delta, 28) × 0,025`.

Šteta = broj napadača × interpolirana vrijednost između min/max damagea pomoću random rolla × množitelj. Zatim se primjenjuju defend i ranged-melee penal. Zaokruživanje je na najbliži cijeli broj, polovice na paran broj; minimum štete 1. Neprijateljski hero attack/defense trenutno su 1/1 iz misije.

Broj preživjelih = zaokruženi preostali ukupni HP prema gore / HP jedne jedinice. Pobjeda nastupa kad nema živih neprijateljskih stackova; poraz kad nema igračeve vojske. Gubici ostaju nakon pobjede, a preživjele jedinice vraćaju puni HP između borbi. Poraz ne zapisuje trajne gubitke i omogućuje retry s vojskom prije borbe ili povratak na pripremu.

Jednostavni AI bira najbližu metu, zatim niži ukupni HP i stabilni ID. Napada ako može, inače ide prema meti. Trenutni razmak AI poteza je 0,7 s. Težine, napredniji AI, wait, bježanje i predaja: **otvoreno**.

## 9. Heroj, XP i artefakti

Kael ima portret, opis, klasu, frakciju, vojsku, XP, level, osnovne/izvedene statove, inventar i opremu. Svakih 300 XP daje novi level; attack raste svakim levelom, defense svaka dva levela. Detalji su podesivi u HeroDatabase.asset.

Implementirani slotovi opreme: weapon i charm. **Dogovoreni budući smjer (2026-10-12):** hero screen treba imati 10 equipment slotova: Head, Ring 1, Ring 2, Amulet, Left Hand, Right Hand, Torso, Sash, Pants i Boots, uz inventory i 7 slotova vojske. Nova podjela opreme još nije implementirana; kompatibilnost itema, dvoručno oružje i migracija postojećih slotova ostaju otvoreni. Koncept prikazuje 5 × 5 inventory polja kao prijedlog prikaza, ne potvrđeno ograničenje kapaciteta. **Dogovoreni vizualni smjer itema (2026-10-12):** common jednostavan i praktičan, rare atraktivniji i ukrašen, epic izrazito upečatljiv, legendary najraskošniji; izrađene ilustracije za svaki slot i razinu. Gameplay učinci rijetkosti i pravila dropova nisu definirani. Bonusi se računaju iz opreme i ne upisuju trajno u osnovne statove.

| Artefakt | Bonus | Dostupnost |
|---|---|---|
| Fang of Thunder | +2 attack | Pass Wardens nagrada |
| Ashen Ward | +2 defense | Development panel za testiranje |
| Warborn Sigil | +1 speed | Black Banner nagrada |

Artefakti imaju ID, naziv, opis, ikonu, kompatibilni slot i bonuse u ArtefactDatabaseScriptableObject. Skill tree, herojske aktivne sposobnosti, rarity, set bonusi, trgovina i više heroja: **otvoreno**.

## 10. Gold, gradovi i vlasništvo

**Implementirano:** gold je jedini resurs. Početna količina i nagrade dolaze iz misije. Troši se na novačenje; nema definirane dnevne gradske zarade u ovom prototipu.

Ashen Hold ima unaprijed pripremljenu ponudu tri vrste jedinica. Početna zaliha: 14 Ironbounda, 9 Archera, 4 Ridera. UI omogućuje odabir količine i MAX prema zalihi, zlatu i prihvatljivoj vojsci. Kupnja mora biti potpuna ili odbijena bez djelomičnog trošenja.

| Vlasništvo | Home / garnizon | Visiting | Cijene |
|---|---|---|---|
| Ashen Clan | Zaštićena klanska vojska | Igračev heroj i vojska | Osnovne cijene |
| Igrač | Igračev garnizon, opcionalni home heroj | Igračev visiting heroj | ceil(osnovna cijena × 0,55) |

Aktualne owned cijene: Ironbound 39, Archer 61, Rider 143. Osvajanje traži pobjedu nad braniteljima. U vlastitom gradu moguć je transfer cijelog stacka između redova, uz spajanje jednakih tipova i provjeru kapaciteta. Postavljanje Kaela kao home heroja spaja vojsku s home vojskom; prije novačenja i putovanja treba ga vratiti u visiting stanje.

**Dogovoreni budući smjer:** neki vlasnici omogućuju ulaganje u građevine za povoljnije cijene; grad može biti preuzet borbom ili nagodbom; vlastiti grad omogućuje ulaganje u gradnju i povoljniju proizvodnju.

**Otvoreno:** uvjeti nagodbe i procjena snage, odnosi s klanovima, vrste/cijene građevina, vrijeme gradnje, trajanje popusta, posljedice ulaganja nakon osvajanja i dodatni resursi. Aktualnih 55% je prototipna vrijednost, ne konačno pravilo ekonomije.

## 11. Audio kao glavni UX

**Dogovoreni smjer:** originalni heavy metal s herojskom i mračnom atmosferom; Manowar/Bathory služe kao atmosferske reference. Aktualne WAV petlje, efekti i glasovi su placeholder sadržaj.

| Stanje / događaj | Zvučna namjera | Aktualno stanje |
|---|---|---|
| Stajanje na mapi | Miran metal ambient | Ambient sloj |
| Jahanje | Metal galop | Galop sloj koji se uključuje fadeom |
| Friendly susret | Pozitivan, upbeat lick | Kratki cue |
| Enemy susret | Prijeteći lick | Kratki cue |
| Početak borbe | Metal intro | Intro cue i combat sloj |
| Napad jedinice | Prepoznatljiv glas | Voice cue po vrsti |
| Mali / veliki zadani udarac | Različit intenzitet | Odvojeni hit cueovi |
| Primljeni udarac | Jasna prijetnja/gubitak | Received hit cue |
| Pobjeda / poraz | Odgovarajuća završnica | Odvojeni cueovi |
| Level, gold, oprema, klik | Kratka potvrda radnje | Kratki efekti |

**Implementirano:** zvuk ide kroz MLin Audio System. GameAudio prima uspješne gameplay događaje, AudioDatabaseScriptableObject ima po jedno polje za svaki događaj i glazbeni sloj, a svaka jedinica izravno pokazuje na svoj glas. Svako polje pokazuje na SoundDefinition asset (clipovi i varijacije, glasnoća, pitch, prioritet, limit istovremenih instanci, cooldown, mixer grupa). Audio ne određuje pravila borbe. Ambient, gallop i battle slojevi (100 BPM, 9,6 s) kreću na istom DSP trenutku i miješaju se fadeom prema putovanju i borbi. Glasovi jedinica zamjenjuju jedan drugoga, a glazba se stišava 6 dB dok jedinica govori (ducking pravilo Voice → Music).

Postoje odvojeni kanali Music/Effects/Voice na mixeru (dosadašnje spremljene postavke igrača su zadržane), mute preko Master kanala, utišavanje kad prozor izgubi fokus i F3 razvojni panel za preslušavanje svakog cuea. Zamjena zvuka u SoundDefinition assetu ili u AudioDatabaseu vrijedi nakon novog Play Modea ili novog builda; New Mission ne učitava baze ponovno.

Prijedlog produkcije: kompatibilne snimljene petlje, više varijanti lickova, originalni glasovi i glazbeni prijelazi na beat/bar kad igra dobije zasebne pjesme (audio sustav ih već podržava). Klik i udarac moraju dobiti brz odgovor. Treba definirati prag velikog udarca i pravila za kombinaciju kill/retaliation/received cueova te testirati zamor tijekom cijele misije.

## 12. Art i animacija

**Dogovoreni smjer:** slikarski heroic fantasy prema dosadašnjim referencama na Franka Frazettu; čitljive siluete u maloj veličini i metal motivi.

| Asset | Lokacija u vanjskoj zbirci | Status |
|---|---|---|
| Heroj na konju, udesno | assets/hero_horse_right | 4 PNG framea i sheet |
| Heroj na konju, udesno | assets/hero_horse_right_8frames | 8 RGBA frameova 444 × 444 i sheet; predloženo 90 ms/frame |
| Air Guitarist | assets/air-guitarist | Koncept, više 8-frame walk varijanti, sheetovi i GIF previewi |
| Equipment ikone — 4 razine | assets/items/metal-equipment-v1 | 40 konceptualnih transparentnih PNG ikona 512 × 512: 10 slotova × common/rare/epic/legendary; 4 izvorna sheeta, overview i manifest. Left Hand je ilustriran štitom, Right Hand mačem; ilustracije nisu potvrđena pravila kompatibilnosti. Bez statova, loot pravila ili integracije |
| Hero screen | assets/ui/hero-screen/hero-screen-v1.png | Koncept UI-a: portret/statovi, 10 equipment slotova prema korisnikovu popisu, inventory 5 × 5 i red sa 7 army slotova. Nije integrirano; tekst, slotovi i ikone kategorija ucrtani su u koncept |
| Ashen Hold — town screen | assets/town/ashen-hold/ashen-hold-town-screen-v1.png | Konceptualna slika grada s dvorcem, odvojenim home/visiting redovima (portret + 7 army slotova) i vizualno odvojenim zgradama za Ironbound, Ash Archer i Doom Rider. Zgrade su prijedlog vizualnog odabira regrutacije, ne implementirano pravilo proizvodnje/gradnje. Nije integrirano; klikovi, portreti i sadržaj slotova zahtijevaju zaseban UI |
| Ashen Pass — battle pozadine | assets/battle/ashen-pass | Dvije generirane konceptualne pozadine: battle-green-v1.png (zelena šumska čistina) i battle-infernal-v1.png (pepeljasto/infernalno bojište). Otvorena sredina za zasebne heksove i jedinice; nisu integrirane u gameplay |
| UI okviri — drvo i kovano željezo | assets/ui/wood-iron-v1 | Tri generirana transparentna PNG okvira: screen s kuglicama, manji dialog, veći menu panel. Pripremljene preporučene 9-slice granice u README-u; nije integrirano niti provjereno u Unityju |
| Ashen Pass — fiksna mapa | assets/maps/ashen-pass/ashen-pass-background-v1.png | Generirana konceptualna pozadina: zapadna šuma i čistine, planinski prolaz, Ashen Hold, istočno pepeljasto područje; bez heroja/vojski i bez grida. Nije integrirana; prilagodba mreže i prohodnosti tek slijedi |
| Roadie | assets/roadie | Koncept s flight caseom i kabelima; walk animacija u izradi u drugom chatu pri pregledu |

Air Guitarist hoda i svira zamišljenu gitaru, bez stvarnog instrumenta i bez plašta. Posljednja varijanta ima izraženije naginjanje/head bob, ali izmjena nogu još traži doradu. Roadiejev koncept nosi opremu, s prepoznatljivim osjećajem težine.

Ovi asseti nisu ovom revizijom potvrđeni kao integrirani u Unity gameplay. Trenutni prototip koristi placeholder/fallback grafiku.

Prijedlog standarda: transparentna pozadina, jednak scale/pivot/baseline, bez rezanja siluete, dosljedna kamera i svjetlo, čitljivost na ciljanoj veličini, pouzdan loop. Odrediti dimenzije u igri, smjerove, idle/attack/hit/death animacije, atlas i pravila imenovanja prije masovne proizvodnje.

## 13. UI i kontrole

**Implementirano:** svi produkcijski ekrani koriste Canvas/uGUI, TextMeshPro i spremljene prefabe. I F3 razvojni panel je uGUI prefab; IMGUI se više ne koristi.

Ekrani: adventure, susret, combat, town, hero/inventory/equipment, victory, defeat, options i obavijesti/HUD.

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

**Implementirano:** manual save/load i autosave u sigurnim stanjima nakon kretanja, kupnje, opreme i pobjeda. Save schema v2 koristi stabilne ID-eve; v1 se migrira. Zamjena koristi privremenu datoteku i .bak; neispravan load ne smije zamijeniti aktivnu kampanju.

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
| 1 | Roster: ostaju li Ironbound/Archer/Rider ili uvodimo metal arhetipove? | Određuje odnos nove grafike i gameplaya |
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

| 2026-10-12 | 0.1 — equipment art | Izrađeno 40 item ikona za 10 equipment slotova i 4 razine vizualne kvalitete; rare amulet dorađen radi kompletnog lanca; pripremljeni zasebni PNG-ovi i pregled | Vizualni pregled svih ikona; provjerene dimenzije i transparentni kutovi; bez gameplay statova ili Unity integracije/builda |
