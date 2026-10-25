# CharPad

A Character Map (`charmap.exe`) replacement for Windows that you can actually read. CharPad does what charmap does: browse a font's characters, pick some and copy them. It adds a resizable window, an adjustable glyph size and a large preview pane.

## Features

**Everything charmap does**

- Browse every character in any installed font, including per-user fonts. Only the font's own glyphs are shown, never a fallback font's substitute.
- Build up a string in **Characters to copy** and copy it. The clipboard gets plain text plus rich text that names the font, so pasting into Word or WordPad keeps the font.
- The status bar shows the code point, the Unicode name and the Alt+ keystroke (for example `U+00A9: COPYRIGHT SIGN · Alt+0169`).
- Symbol fonts (Wingdings, Webdings and so on) are shown once each at their private-use codes, with the byte value, the way charmap shows them: `U+F021 (0x21)`.
- **Advanced view:**
  - **Character set:** limit the grid to what a Windows or DOS code page can encode (Western, Cyrillic, Shift-JIS, DOS 437 and others).
  - **Group by:** filter by Unicode block or Unicode category.
  - **Search for:** find characters by name. Each word matches the start of a word in the name, so `right arrow` finds RIGHTWARDS ARROW but `arrow` doesn't match NARROW. You can also paste a single character or type a `U+` code.
  - **Go to Unicode:** jump to a hex code such as `00A9` or `U+1F600`.

**What CharPad adds**

- A resizable window. The grid reflows to fit, and the window's size and position are remembered.
- An adjustable glyph size from 6 to 300 pt, using the Size box, the A− and A+ buttons, Ctrl+mouse wheel or Ctrl+Plus and Ctrl+Minus.
- A large preview pane, resizable with the splitter, showing the selected glyph with its name, block, category, decimal value, UTF-8 and UTF-16 bytes, HTML entities and keystroke. The details are selectable text, so you can copy them.
- A hover tooltip on each cell with its code and name.
- Character names and blocks for all of Unicode, not just the first 65,536 code points. They come from Windows' built-in ICU library, so no data files ship with the app.
- Registers itself so you can type `charpad` in the Run box (Win+R). See [Launching from Win+R](#launching-from-winr).

## Requirements

- Windows 10 version 1903 or later, or Windows 11. CharPad uses the `icu.dll` that ships with Windows from 1903 onward.
- To run: the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (x64).
- To build: the .NET 10 SDK.

## Keyboard and mouse

| Action | How |
|---|---|
| Move the selection | Arrow keys, Page Up/Down, Home/End (row), Ctrl+Home/End (first/last) |
| Select by mouse | Click, or click and drag across cells |
| Add the selected character to *Characters to copy* | Double-click, Enter, Space, or **Select** (Alt+S) |
| Copy *Characters to copy* | **Copy** (Alt+C). This copies the highlighted text, or all of it if nothing is highlighted, or the selected character if the box is empty. |
| Copy the selected character directly | Ctrl+C while the grid has focus |
| Larger / smaller glyphs | Ctrl+mouse wheel, Ctrl+Plus / Ctrl+Minus, or A+ / A− |
| Reset glyph size (24 pt) | Ctrl+0 |
| Show or hide the advanced view | **Advanced view** (Alt+V) |

## Building and installing

From the project folder (the one containing `CharPad.csproj`):

```powershell
# Debug build to try it out
dotnet run

# Install: a single framework-dependent CharPad.exe (about 0.6 MB) in a folder of your choice
dotnet publish -c Release -r win-x64 --no-self-contained -p:PublishSingleFile=true -o "C:\NullSave\CharPad"
```

Replace the `-o` path with wherever you want CharPad installed. Install it outside the repo and outside any `bin` folder (see the next section). The output is `CharPad.exe` plus an optional `CharPad.pdb` (debug symbols).

> **Use `--no-self-contained`, not `--self-contained false`.** With the .NET 10 SDK, `--self-contained false` still produced a self-contained build for this project: a 130 MB exe that bundles the whole .NET runtime, plus five loose native WPF DLLs. To check what a command will produce before running it, add `-getProperty:SelfContained` to it, which prints `true` or `false` without building anything.

If you'd rather not depend on an installed .NET runtime, publish with `--self-contained` instead. Expect an exe of about 130 MB.

## Launching from Win+R

CharPad registers itself under the per-user **App Paths** registry key every time it starts:

```
HKCU\Software\Microsoft\Windows\CurrentVersion\App Paths\CharPad.exe
    (Default) = <full path to CharPad.exe>
```

After you've run the installed copy once, `charpad` works in the Run box (Win+R), in Start search, and with `start charpad` in a command prompt. This uses App Paths rather than PATH: it needs no admin rights, takes effect immediately and doesn't change your PATH. Typing a bare `charpad` in a cmd or PowerShell window does **not** work, because shells only search PATH.

How it behaves:

- The key is only rewritten when the stored path differs, so moving the install folder just updates it on the next launch.
- Copies running from a folder named `bin` (for example `bin\Debug` or `bin\Release` while developing) never register, so dev builds don't take over the registration.
- If the registry can't be written, CharPad starts normally without registering.

To turn this off, comment out `AppRegistration.Register();` in `App.xaml.cs`. To remove an existing registration:

```powershell
Remove-Item 'HKCU:\Software\Microsoft\Windows\CurrentVersion\App Paths\CharPad.exe'
```

## Settings

The window position and size, font, glyph size, preview width, advanced view and character set are saved when CharPad closes, in:

```
%APPDATA%\CharPad\settings.json
```

Delete the file to go back to the defaults (Arial at 24 pt).

## Project layout

| Path | Contents |
|---|---|
| `App.xaml(.cs)` | Startup: code-page provider registration, App Paths registration, unhandled-error dialog |
| `MainWindow.xaml(.cs)` | The window: font and size controls, filters, search, copy, settings save/restore |
| `Helpers/CharGrid.cs` | The custom character grid: reflow, virtualized drawing, scrolling, keyboard and mouse, tooltips |
| `Helpers/GlyphPainter.cs` | Draws and measures single glyphs directly from the font's glyph table |
| `Helpers/GlyphPreview.cs` | The large preview pane |
| `Helpers/UnicodeInfo.cs` | Character names and blocks via Windows ICU (`icu.dll`); categories via .NET |
| `Helpers/CodePages.cs` | Character-set filtering and Alt+ keystroke lookup |
| `Helpers/FontCatalog.cs` | Lists installed font families (machine-wide and per-user) |
| `Helpers/AppRegistration.cs` | App Paths self-registration |
| `Helpers/Settings.cs` | Saved settings |
| `Pacts/` | Small records: `CodePage`, `FontEntry`, `GlyphLayout`, `CharGroup` |
