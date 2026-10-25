# Lobby typography

Imported unmodified from the official font repositories on 2026-10-25:

- Source Han Sans CN 2.005: Regular for supporting text and editable names, Medium for navigation and secondary controls, Heavy for the hero, map title and primary action. Source: https://github.com/adobe-fonts/source-han-sans/tree/release/SubsetOTF/CN
- Black Ops One: the IRONFRONT stencil wordmark. Source: https://github.com/google/fonts/tree/main/ofl/blackopsone

Both are SIL Open Font License 1.1. Full notices are adjacent to the original font files and included in the player under StreamingAssets/ThirdPartyFonts. Download hashes are recorded in `art/ui/lobby-typography-2026-10-25/font-sources.json` at the repository root.

Run **IRONFRONT / Generate Lobby Fonts** to generate the TextCore FontAsset resources. These retain their source fonts for runtime text generation and Chinese player names. USS binds the actual font faces with normal style, avoiding artificial bold or OS-specific default CJK fallback. The logo and top navigation are shared; other font overrides are scoped to `lobby-view`.

The approved concept is a generated raster image, without original editable type metadata. These fonts approximate its visual weight and military lettering; they are not claimed to be its exact source fonts.
