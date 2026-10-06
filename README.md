# Romme Cup Variety

[![Release](https://img.shields.io/github/v/release/domezos2024/RommeCupVariety)](https://github.com/domezos2024/RommeCupVariety/releases/latest)
[![Lizenz: MIT](https://img.shields.io/badge/Lizenz-MIT-blue.svg)](LICENSE)
![Unity 6](https://img.shields.io/badge/Unity-6000.3-black?logo=unity)
![Android 8.0+](https://img.shields.io/badge/Android-8.0%2B-3DDC84?logo=android&logoColor=white)

Zwei klassische Legespiele am realistischen 3D-Spieltisch für Android (Querformat), gebaut mit Unity 6:

- **Zahlensteine** (Rommé mit Steinen) – 106 Steine (1–13 in Schwarz, Rot, Blau, Orange, je doppelt, 2 Joker), Erstauslage 30 Punkte.
- **Rommé** – 110 Karten (zwei französische Blätter mit roter und blauer Rückseite, 6 Joker), Erstauslage 40 Punkte, Hand-Rommé doppelt.

Solo gegen 1–3 Computergegner oder per **Bluetooth mit 2–4 Geräten** – ohne Internet, ohne Konto, ohne Werbung.

![Menü](Docs/screenshots/01_menu.png)

## Download

Die aktuelle APK gibt es unter **[Releases](https://github.com/domezos2024/RommeCupVariety/releases/latest)**.
APK auf dem Android-Gerät öffnen und die Installation aus unbekannten Quellen erlauben (Android 8.0 oder neuer).

## Funktionen

- Prozedural erzeugte Elfenbein-Steine mit gravierten Zahlen, Holztisch mit Filzeinlage und Messingintarsie, zweistufige Bänke, verdeckter Vorrat.
- Französisches Blatt mit großen, gut lesbaren Eckindizes, Bildkarten, Joker und Papierstruktur; Stapel- und Ablagehöhe nach Kartenzahl.
- Licht, weiche Schatten, Reflexionen, Raumnebel und Staub im Lampenlicht.
- Austeilen, Flugbögen und synthetisierte Klänge (Steinklacken, Kartenschnipsen, Mischen, Applaus).
- Startspieler per Auslosung bzw. rotierender Geber, Tipp-Funktion, Zug zurücknehmen, Sortieren nach Farbe oder Wert.
- Bluetooth-Mehrspieler: Gastgeber eröffnet, Mitspieler treten bei; verlässt jemand die Partie, übernimmt der Computer.

| Zahlensteine | Rommé |
|---|---|
| ![Zahlensteine](Docs/screenshots/07_rummi_tipp.png) | ![Rommé](Docs/screenshots/14_romme_spielmitte.png) |

## Bauen und Testen

Voraussetzung: Unity **6000.3.25f1** mit Android Build Support (Built-in Render Pipeline, Linear Color Space).

```bash
Unity -batchmode -quit -projectPath . -executeMethod RommeCup.EditorTools.SelfTest.Run
```

| Aufgabe | Methode |
|---|---|
| Selbsttest (Logik, Karten-/Steinvorschau in `Builds/`) | `RommeCup.EditorTools.SelfTest.Run` (ohne `-nographics`) |
| Android-APK | `-buildTarget Android -executeMethod RommeCup.EditorTools.BuildScript.BuildAndroid` |
| Windows-Testbuild | `RommeCup.EditorTools.BuildScript.BuildWindowsTest` |
| UI/UX-Autotest | `RommeCup.exe -autotest -shots <Ordner>` erzeugt Screenshots und `report.txt` |

Der Bluetooth-Mehrspieler lässt sich mit zwei Android-Emulatoren testen (virtuelles Bluetooth über netsim).

## Projektstruktur

| Pfad | Inhalt |
|---|---|
| `Assets/Scripts/Core` | Grafik-Grundlagen, Umgebung, Sound, UI-Theme, Logging |
| `Assets/Scripts/App` | App-Start, Hauptmenü, gemeinsame Screens, Autotest |
| `Assets/Scripts/Rummikub` | Zahlensteine: Logik, Sitzung, Ansicht |
| `Assets/Scripts/Karten` | Rommé: Logik, KI, Sitzung, Kartengrafik |
| `Assets/Scripts/Net` | Bluetooth-Anbindung |
| `Assets/Plugins/Android` | Java-Plugin für Bluetooth (RFCOMM) |
| `Assets/Editor` | Build-Skripte und Selbsttest |

## Lizenz

[MIT](LICENSE) © 2026 Michael Bergfeld

## Hinweis zu Marken

„Rummikub“ ist eine eingetragene Marke der Kodkod (M.T.) Ltd. Dieses Projekt ist ein unabhängiges Hobbyprojekt und steht in keiner Verbindung zu den Markeninhabern.
