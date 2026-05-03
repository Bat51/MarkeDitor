# MarkeDitor - Plan d'implémentation

## Architecture générale

- **Framework** : WinUI 3 (Windows App SDK 1.5+) / .NET 8
- **Pattern** : MVVM avec CommunityToolkit.Mvvm
- **Éditeur de texte** : Monaco Editor embarqué via WebView2 (coloration syntaxique Markdown, numéros de ligne, minimap, recherche/remplacement inclus)
- **Preview** : WebView2 avec HTML généré par Markdig
- **Parsing Markdown** : Markdig (avec extensions GFM, tables, etc.)

## Packages NuGet

- `Microsoft.WindowsAppSDK` (1.5+)
- `Microsoft.Windows.SDK.BuildTools`
- `CommunityToolkit.Mvvm` (MVVM)
- `CommunityToolkit.WinUI.UI.Controls` (TreeView, TabView, etc.)
- `Markdig` (parsing Markdown → HTML)
- `Microsoft.Web.WebView2` (Monaco + Preview)

## Structure du projet

```
MarkeDitor/
├── MarkeDitor.sln
├── MarkeDitor/
│   ├── MarkeDitor.csproj
│   ├── App.xaml / App.xaml.cs
│   ├── MainWindow.xaml / MainWindow.xaml.cs
│   │
│   ├── ViewModels/
│   │   ├── MainViewModel.cs          # VM principal (gestion tabs, commandes globales)
│   │   ├── EditorTabViewModel.cs     # VM par onglet (contenu, état dirty, chemin fichier)
│   │   └── FileExplorerViewModel.cs  # VM explorateur de fichiers
│   │
│   ├── Views/
│   │   ├── EditorView.xaml           # Split view : Monaco (WebView2) + Preview (WebView2)
│   │   ├── FileExplorerView.xaml     # TreeView des fichiers .md
│   │   └── ToolbarView.xaml          # Barre d'outils formatage
│   │
│   ├── Services/
│   │   ├── IMarkdownService.cs       # Interface parsing Markdown
│   │   ├── MarkdownService.cs        # Implémentation Markdig
│   │   ├── IFileService.cs           # Interface opérations fichier
│   │   ├── FileService.cs            # Implémentation lecture/écriture fichiers
│   │   └── IDialogService.cs         # Dialogues Open/Save
│   │   └── DialogService.cs
│   │
│   ├── Models/
│   │   ├── FileItem.cs               # Modèle pour l'arbre de fichiers
│   │   └── EditorState.cs            # État de l'éditeur (curseur, sélection)
│   │
│   ├── Assets/
│   │   ├── Monaco/                   # Fichiers Monaco Editor (JS/CSS embarqués)
│   │   │   ├── monaco-editor.html    # Page HTML hôte pour Monaco
│   │   │   └── preview.html          # Page HTML hôte pour la preview
│   │   └── Styles/
│   │       └── preview.css           # CSS pour le rendu preview
│   │
│   ├── Helpers/
│   │   ├── DebounceHelper.cs         # Debounce pour la mise à jour live
│   │   └── MonacoInterop.cs          # Communication C# ↔ Monaco (JS interop)
│   │
│   └── Package.appxmanifest
```

## Layout principal (MainWindow)

```
┌─────────────────────────────────────────────────────────┐
│  Menu : File | Edit | View                              │
├──────┬──────────────────────────────────────────────────┤
│      │  Toolbar : B I H1 H2 H3 Link Img Code List ...  │
│ File ├──────────────────────────────────────────────────┤
│ Tree │  TabView : [File1.md] [File2.md] [+]            │
│      ├─────────────────────┬────────────────────────────┤
│      │                     │                            │
│      │   Monaco Editor     │   HTML Preview             │
│      │   (WebView2)        │   (WebView2)               │
│      │                     │                            │
│      │                     │                            │
├──────┴─────────────────────┴────────────────────────────┤
│  StatusBar : Ln 12, Col 34  |  245 mots  |  UTF-8      │
└─────────────────────────────────────────────────────────┘
```

## Étapes d'implémentation (ordre)

### Étape 1 — Squelette du projet
- Créer la solution WinUI 3 (.NET 8, packaged)
- Installer les packages NuGet
- Mettre en place la structure de dossiers
- MainWindow avec layout de base (Grid avec les zones)

### Étape 2 — Éditeur Monaco via WebView2
- Intégrer Monaco Editor (via CDN ou fichiers locaux)
- Créer la page HTML hôte (`monaco-editor.html`)
- Implémenter `MonacoInterop.cs` : communication C# ↔ JS
  - `SetContent(string)` : injecter du texte dans Monaco
  - `GetContent()` : récupérer le texte
  - `OnContentChanged` : événement quand le texte change
  - `InsertText(string)` : insérer du texte à la position du curseur
  - `GetCursorPosition()` : position ligne/colonne

### Étape 3 — Preview Markdown live
- Implémenter `MarkdownService` avec Markdig
- Créer `preview.html` avec CSS stylé
- Connecter l'événement `OnContentChanged` de Monaco → Markdig → WebView2 preview
- Ajouter un debounce (300ms) pour ne pas surcharger le rendu

### Étape 4 — Opérations fichier
- Implémenter `FileService` (lecture/écriture fichiers)
- Implémenter `DialogService` (dialogues Open/Save natifs)
- Commandes : Nouveau, Ouvrir, Sauvegarder, Sauvegarder Sous
- Gestion de l'état "dirty" (modifications non sauvegardées)

### Étape 5 — Onglets multiples
- Intégrer TabView du WinUI 3 Community Toolkit
- `EditorTabViewModel` par onglet
- Gestion ouverture/fermeture d'onglets
- Confirmation avant fermeture si dirty

### Étape 6 — Explorateur de fichiers
- `FileExplorerView` avec TreeView
- Ouvrir un dossier → scanner les .md récursivement
- Double-clic → ouvrir dans un onglet
- Icônes fichier/dossier

### Étape 7 — Barre d'outils
- Boutons : **B**, *I*, H1-H3, Link, Image, Code, Liste, Quote
- Chaque bouton insère la syntaxe Markdown appropriée via `MonacoInterop.InsertText()`
- Raccourcis clavier (Ctrl+B, Ctrl+I, etc.)

### Étape 8 — Barre de statut
- Ligne / Colonne (depuis Monaco)
- Compteur de mots
- Encodage du fichier

### Étape 9 — Finitions
- Raccourcis clavier globaux (Ctrl+S, Ctrl+N, Ctrl+O, Ctrl+Tab)
- Thème sombre/clair synchronisé entre Monaco et la preview
- Gestion du drag & drop de fichiers
- Confirmation à la fermeture si des fichiers non sauvegardés
