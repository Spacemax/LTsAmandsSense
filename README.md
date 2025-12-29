# AmandsSense

A loot sensing and highlighting mod for SPT that provides a "sixth sense" to detect nearby items, containers, corpses, and extraction points. This fork targets SPT 4.0.x and modernizes visuals, rarity logic, and reliability.

> Fork of [AmandsSense](https://github.com/Amands2Mello/AmandsSense) by **Amands2Mello**.
> Updated for **SPT 4.0.x** by **LT. of Universi**.

---

## Features

### Loot Detection
Press a key (default: double-tap F) to scan and highlight:
- **Ground loot** – Icons + color by value
- **Containers** – Item count + color by best item inside
- **Corpses** – Faction/role + color by best loot (bosses always gold)
- **Exfils** – Distance and requirement status

### Rarity Colors (easy to read)
- **Gold** Legendary (bosses, top-tier loot)
- **Purple** Epic
- **Blue** Rare
- **Green** Uncommon
- **White** Common
- **Gray** Junk
- **Pink** Wishlist (overrides)

How it works:
- Prices drive the tier; wishlist/rare lists still apply.
- Bosses/goons/rogues/raiders are always Legendary.
- Corpses/containers fall back to Common if loot has no price.

### Visual Indicators
- Tier-colored text fill with dark outline for visibility
- World-space icons for 35+ item categories
- Dynamic point lights and fade-in/fade-out animations
- Optional depth-of-field effect during scan
- Billboarding (indicators face camera)

### Dead Body Intelligence
- Markers positioned above the body using combined collider bounds (no floor clipping)
- Empty bodies shown at reduced opacity
- Faction identification (USEC/BEAR/Scav with role)
- Boss/rogue/raider detection; Common fallback for unpriced loot

### Audio Feedback
- Activation sound when sense is used
- Per-item-type sounds from game audio
- Cooldown feedback (quiet sound when on cooldown)
- Optional rare item sound alert

---

## Installation

1. Download the latest release
2. Extract to your SPT installation folder
3. Ensure the folder structure is:
   ```
   SPT/
   └── BepInEx/
       └── plugins/
           └── AmandsSense/
               ├── AmandsSense.dll
               └── Sense/
                   ├── Items.json
                   ├── images/
                   │   └── (icon PNGs)
                   └── sounds/
                       └── (optional WAV files)
   ```

---

## Configuration

Access settings via **F12** (BepInEx Configuration Manager) under the **AmandsSense** category.

### General Settings

| Setting | Default | Description |
|---------|---------|-------------|
| Enable Sense | OnText | Off / On (icons only) / OnText (icons + labels) |
| Enable Exfil Sense | true | Show extraction point markers |
| Always On | false | Continuous scanning mode |
| Sense Key | F | Activation key |
| Double Click | true | Require double-tap to activate |
| Double Click Window | 0.5s | Time window for double-click |
| Cooldown | 2.0s | Time between activations |
| Duration | 10.0s | How long indicators remain visible |
| Radius | 10m | Scan radius for items |
| Dead Player Radius | 20m | Extended radius for corpses |

### Visual Settings

| Setting | Default | Description |
|---------|---------|-------------|
| Vertical Offset | 0.22 | Height offset for item markers |
| Dead Body Vertical Offset | 0.4 | Height offset for corpse markers (bounds-based) |
| Size | 0.5 | Base indicator scale |
| Size Clamp | 3.0 | Maximum scale at distance |
| Light Intensity | 0.6 | Point light brightness |
| Use DOF | true | Enable depth-of-field effect |

### Dead Body Settings

| Setting | Default | Description |
|---------|---------|-------------|
| Show Empty Bodies | true | Show markers for empty corpses |
| Empty Body Opacity | 0.3 | Opacity for empty body markers |

### Audio Settings

| Setting | Default | Description |
|---------|---------|-------------|
| Audio Volume | 0.5 | Item sound volume |
| Activate Sense Volume | 0.5 | Activation sound volume |
| Show Cooldown Feedback | true | Play sound when on cooldown |

### Colors

All item categories and special types have configurable RGBA colors. Key rarity defaults (Polish palette):
- Junk #7f7f7f, Common #e8e8e8, Uncommon #45c769, Rare #3aa0ff, Epic #b44bff, Legendary #ff9b2f, Wishlist #ff66c4.

---

## Custom Items List

Edit `BepInEx/plugins/Sense/Items.json` to customize:

```json
{
  "RareItems": ["item_template_id_1", "item_template_id_2"],
  "KappaItems": ["kappa_item_id_1", "kappa_item_id_2"],
  "NonFleaExclude": ["excluded_item_id"]
}
```

---

## Custom Sounds

Place custom WAV files in `BepInEx/plugins/Sense/sounds/`:
- `Sense.wav` - Activation sound
- `SenseRare.wav` - Rare item alert sound

---

## Compatibility

- **SPT Version**: 4.0.5+
- **Dependencies**: BepInEx 5.x
- **Multiplayer**: Client-side only (SPT); avoid using in FIKA unless client-side overlays are acceptable

---

## Changelog (high level)

### v3.1.0 (Current)
- Value-based rarity tiers for loose items, containers, and corpses
- Boss/rogue/raider auto-Legendary override
- Common fallback for unpriced loot
- Tier-colored text fill with dark outline (no more white fades)
- Bounds-based corpse positioning (no floor clipping)

### v3.0.0 (SPT 4.0.x Update)
- Migrated to SPT 4.0.x APIs
- Fixed dead body markers appearing in floor
- Re-enabled wishlist highlighting
- Added empty body visual differentiation
- Added cooldown feedback
- Added configurable double-click window
- Modernized codebase with proper error handling
- Updated to SDK-style project

### v2.0.0
- Original release by Amands2Mello

---

## Credits

- **Original Author**: [Amands2Mello](https://github.com/Amands2Mello)
- **SPT 4.0.x Update**: LT. of Universi
- **License**: MIT

---

## Building from Source

```bash
# Clone the repository
git clone https://github.com/Spacemax/LTsAmandsSense.git

# Build
cd LTsAmandsSense/AmandsSense
dotnet build -c Release

# Custom SPT path
dotnet build -c Release -p:SPTDir="D:\\YourPath\\SPT\\"
```

Output: `bin/Release/net471/AmandsSense.dll`

---

## Links

- [Original Mod](https://github.com/Amands2Mello/AmandsSense)
- [This Fork](https://github.com/Spacemax/LTsAmandsSense)
- [SPT Project](https://www.sp-tarkov.com/)
