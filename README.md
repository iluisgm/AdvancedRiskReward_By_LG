# Advanced Risk Reward — Master Documentation
### NinjaTrader 8 Custom Toolkit by Luis Fernando Garrido Miranda
**Version Reference:** Drawing Tool v1.6 · Strategy v4.6 (Managed) · Strategy v5.4-BETA (Unmanaged)**
**Last Updated:** 2026-04-10
**Author:** Luis Fernando Garrido Miranda
**Contact:** X: @iluisgm | PayPal: lfgm00 | USDT ERC-20: `0xEF0E0eFe33bE9487ca1D017Ceb5b1C70a3bf36BC`

---

## Table of Contents

1. [What Is Advanced Risk Reward?](#1-what-is-advanced-risk-reward)
2. [Drawing Tool — AdvancedRiskReward_LuisGarrido.cs](#2-drawing-tool--advancedriskreward_luisgarridocs)
   - 2.1 [What It Is](#21-what-it-is)
   - 2.2 [What It Does](#22-what-it-does)
   - 2.3 [Installation](#23-installation)
   - 2.4 [Properties Reference](#24-properties-reference)
   - 2.5 [On-Chart Buttons](#25-on-chart-buttons)
   - 2.6 [Public Accessor Methods (Strategy API)](#26-public-accessor-methods-strategy-api)
   - 2.7 [Modes Explained](#27-modes-explained)
   - 2.8 [How To Use — Step by Step](#28-how-to-use--step-by-step)
3. [Drawing Tool Source Code](#3-drawing-tool-source-code)
4. [Drawing Tool Changelog](#4-drawing-tool-changelog)
5. [Strategy — DrawingToolSyncStrategy.cs and DrawingToolSyncStrategy_BETA.cs](#5-strategy--drawingtooljsyncstrategycs-and-drawingtooljsyncstrategy_betacs)
   - 5.1 [What They Are](#51-what-they-are)
   - 5.2 [Why Two Versions Exist](#52-why-two-versions-exist)
   - 5.3 [DrawingToolSyncStrategy (v4.6 — Managed Orders)](#53-drawingtooljsyncstrategy-v46--managed-orders)
   - 5.4 [DrawingToolSyncStrategy_BETA (v5.4 — Unmanaged Orders)](#54-drawingtooljsyncstrategy_beta-v54--unmanaged-orders)
   - 5.5 [Chart Trader Button Panel](#55-chart-trader-button-panel)
   - 5.6 [Native Button Intercept Behavior](#56-native-button-intercept-behavior)
   - 5.7 [Autopilot System](#57-autopilot-system)
   - 5.8 [Trailing Stop System](#58-trailing-stop-system)
   - 5.9 [Properties Reference — BETA](#59-properties-reference--beta)
6. [BETA Strategy Source Code](#6-beta-strategy-source-code)
7. [Strategy Changelog](#7-strategy-changelog)
8. [Bug History and Fixes](#8-bug-history-and-fixes)
   - 8.1 [Drawing Tool Bugs](#81-drawing-tool-bugs)
   - 8.2 [Strategy Bugs](#82-strategy-bugs)
9. [Critical Information for Future Developers and AI Agents](#9-critical-information-for-future-developers-and-ai-agents)

---

## 1. What Is Advanced Risk Reward?

**Advanced Risk Reward** is a two-component proprietary trading toolkit for NinjaTrader 8, built in C#/NinjaScript. It extends NinjaTrader's native risk/reward ruler into a fully integrated execution system.

The toolkit consists of:

**Component 1 — `AdvancedRiskReward_LuisGarrido.cs` (Drawing Tool)**
A custom chart drawing tool that replaces the standard NT8 risk/reward ruler. It provides a multi-level stop/target visualization system, automatic contract sizing based on a fixed dollar risk budget, and an interactive on-chart button panel. It exposes a rich public API that the companion strategy reads to execute trades.

**Component 2 — `DrawingToolSyncStrategy_BETA.cs` (Strategy)**
A NinjaTrader Strategy (not an Indicator) that acts as an execution bridge. It continuously polls the drawing tool's public methods, intercepts NinjaTrader's native Chart Trader buttons, injects a custom WPF control panel into the Chart Trader interface, and manages the full lifecycle of unmanaged orders — entry, bracket (SL + TP), partial exit, scale-in, trailing stops, and autopilot level-based stop ratcheting.

**How they work together:**

```
┌──────────────────────────────────────┐
│   DRAWING TOOL (on-chart visual)     │
│   User drags Entry / SL / TP lines  │
│   Calculates contract sizing         │
│   Exposes public API methods         │
└──────────────┬───────────────────────┘
               │  polled via DrawObjects on every tick
               ▼
┌──────────────────────────────────────┐
│   STRATEGY (execution engine)        │
│   Reads drawing tool prices/qty      │
│   Submits entry orders               │
│   Places SL + TP brackets via OCO   │
│   Syncs live prices (magnet mode)    │
│   Manages autopilot trailing stop    │
│   WPF Chart Trader panel injection   │
└──────────────────────────────────────┘
```

The critical design constraint is that **`AtmStrategyCreate()` cannot be called from a DrawingTool** — only a Strategy can manage positions and intercept Chart Trader buttons. This is why the toolkit is split into two cooperating files.

**Inspiration:**
- YosewCapital's Risk Reward tool (visual concept)
- Bruno Meza's Advanced Risk Reward tool (unmanaged order pattern)
- TradeSaber's `OrderEntryButtons.cs` (WPF injection pattern)

---

## 2. Drawing Tool — AdvancedRiskReward_LuisGarrido.cs

### 2.1 What It Is

`AdvancedRiskReward_LuisGarrido` is a NinjaTrader 8 custom `DrawingTool` (extends `NinjaTrader.NinjaScript.DrawingTools.DrawingTool`). It lives in the `DrawingTools` namespace and is installed in:

```
Documents\NinjaTrader 8\bin\Custom\DrawingTools\
```

It appears in the NinjaTrader drawing tool list under the name **"Advanced Risk Reward V1 By Luis Garrido"**.

### 2.2 What It Does

The drawing tool provides a professional-grade visual risk/reward ruler with the following capabilities:

**Visual Layer:**
- Three draggable anchors: **Entry**, **Risk (FSL/Stop)**, and **Reward (Target)**
- A horizontal entry line spanning the full chart width
- A Final Stop Loss (FSL) line at the stop anchor with a label showing: contracts, dollar risk, and optionally Ticks / Points / Currency / Percent
- A Final Take Profit line at the reward anchor
- Shaded colored zones between entry↔stop (red) and entry↔target (green)
- Prominent anchor circles at Entry, FSL, and Final TP lines
- Partial stop loss lines (SL1, SL2, …) between entry and FSL, driven by `StopRatio`
- Partial take profit lines (TP1, TP2, …) between entry and final TP, driven by `ProfitRatio`; each shows its own RR ratio and optional Ticks/Currency/Percent
- Configurable font size for all labels

**Calculation Engine:**
- Automatically calculates `riskPerUnit = |entry - stop| × PointValue`
- Auto-sizes contracts: `qty = round(StopLossBudget / riskPerUnit)`, clamped to `[1, MaxContracts]`
- Syncs the calculated contract quantity to NinjaTrader's Chart Trader quantity selector in real time
- Resets Chart Trader quantity to 1 when the drawing is removed

**Interactivity:**
- On-chart button panel (collapsible) for FLIP, AUTO CALC, AUTO TRACK, SOURCE, STOP MODE
- Full hover detection: entire tool area (lines, zones, circles, buttons) triggers hover state
- PRO mode tracking: entry line follows the selected price source (LastPrice, Close, High, Low, Open) in real time
- HARD/FLEX stop modes for different tracking behaviors
- StopTrackOnFill: disables tracking when a position is detected, re-arms when flat

**Strategy Bridge:**
- `ChangeVersion` counter increments on every anchor move — used by the strategy for efficient polling without expensive price comparisons
- Complete public accessor API for all prices and quantities

### 2.3 Installation

1. Copy `AdvancedRiskReward_LuisGarrido.cs` to:
   ```
   Documents\NinjaTrader 8\bin\Custom\DrawingTools\
   ```
2. In NinjaTrader: **Tools → Edit NinjaScript → Compile** (or it compiles automatically on restart)
3. The tool appears in the drawing toolbar under its registered name
4. The companion strategy requires drawing tool **v1.4 or later** for public accessor methods

### 2.4 Properties Reference

#### Risk Reward Settings

| Property | Type | Default | Description |
|---|---|---|---|
| `ProfitRatio` | double | 3.0 | Number of partial TP levels between entry and the final target (also determines how many R multiples the target represents). E.g. `3` creates TP1 (1R), TP2 (2R), TP3 (3R/Final). |
| `StopRatio` | double | 2.0 | Number of partial SL levels between entry and the FSL. E.g. `2` creates SL1 and SL2 (= FSL). |
| `StopLoss Budget (USD)` | double | — | Dollar amount you are willing to risk per trade. Used with Auto Order Qty to compute contract count. |
| `Show Partial TPs` | bool | true | Toggles visibility of intermediate TP lines (TP1, TP2, etc.). When off, only the final TP line is drawn. |
| `Font Size` | int | 11 | Size of all on-chart text labels. Range: 6–32. |
| `Text Alignment` | enum | InsideLeft | Where price/RR labels are positioned relative to the drawing area. Options: InsideLeft, InsideRight, ExtremeLeft, ExtremeRight, Off. |

#### TP Display (checkboxes per partial TP label)

| Property | Description |
|---|---|
| `Show Currency` | Show dollar P&L on each partial TP label |
| `Show Ticks` | Show tick distance from entry on each partial TP label |
| `Show Points` | Show point distance from entry on each partial TP label |
| `Show Percent` | Show percentage move from entry on each partial TP label |

#### SL Display (checkboxes per partial SL label)

| Property | Description |
|---|---|
| `Show Currency` | Show dollar risk on each partial SL label |
| `Show Ticks` | Show tick distance from entry on each partial SL label |
| `Show Points` | Show point distance from entry on each partial SL label |
| `Show Percent` | Show percentage move from entry on each partial SL label |

#### ~ Plus Settings

| Property | Type | Default | Description |
|---|---|---|---|
| `Auto Order Qty` | bool | false | When enabled, automatically calculates contract size and syncs it to NT8's Chart Trader quantity selector |
| `Max Contracts` | int | 10 | Hard cap on auto-calculated contract count, prevents oversizing |

#### ~ PRO Settings

| Property | Type | Default | Description |
|---|---|---|---|
| `Track Last Price` | bool | false | Enables AUTO TRACK mode — entry anchor follows the selected Price Source in real time |
| `Stop Mode` | enum | Flex | FLEX = stop stays fixed, contracts recalculate as entry moves; HARD = stop moves with entry (fixed distance) |
| `Price Source` | enum | LastPrice | Which price the AUTO TRACK follows: LastPrice, Close, High, Low, Open |
| `Stop Track On Fill` | bool | false | When enabled, AUTO TRACK disables automatically when a position is detected on the instrument; re-arms when position closes |
| `Button Position` | enum | NearEntry | Where the on-chart button panel is anchored: NearEntry, NearTP, NearFSL, CenterTop, CenterBottom |
| `Button Opacity %` | int | 80 | Transparency of the button panel (0 = fully transparent, 100 = fully opaque) |
| `Show Buttons On Hover` | bool | false | When enabled, the button panel auto-shows on mouse hover over any part of the drawing and auto-hides when mouse leaves |

#### Line Settings

| Property | Description |
|---|---|
| `EntryLineStroke` | Color and width of the entry horizontal line |
| `StopLineStroke` | Color and width of the FSL (stop) horizontal line |
| `TargetLineStroke` | Color and width of the final TP horizontal line |
| `StopBackground` | Color of the shaded stop loss zone |
| `ProfitBackground` | Color of the shaded profit zone |
| `PartialStopLines` | Color and style of the intermediate SL level lines |
| `IsExtendedLinesLeft` | Extend all horizontal lines to the left edge of the chart |
| `IsExtendedLinesRight` | Extend all horizontal lines to the right edge of the chart |

### 2.5 On-Chart Buttons

The button panel is **hidden by default**. It can be expanded by:
- Clicking the **◀** collapse arrow button (reveals the panel)
- Enabling **Show Buttons On Hover** — the panel auto-appears when the mouse is anywhere over the tool

| Button | Function |
|---|---|
| **◀ / ▶** | Minimize / expand the button panel |
| **FLIP** | Instantly mirrors the drawing — flips SL and TP to the opposite side of entry. A long setup becomes a short setup and vice versa, preserving the same risk distance. |
| **AUTO CALC** | Toggles Auto Order Qty on/off at runtime without opening the properties panel |
| **AUTO TRACK** | Toggles Track Last Price on/off at runtime |
| **SOURCE** | Cycles through price sources (LastPrice → Close → High → Low → Open → LastPrice). Only available when AUTO TRACK is ON. |
| **STOP MODE** | Toggles between FLEX and HARD stop modes. Only available when AUTO TRACK is ON. |

**Button Positioning:** The `ButtonPosition` property determines where the panel is anchored on the chart. Options:
- `NearEntry` — anchored at the entry line
- `NearTP` — anchored at the final TP line
- `NearFSL` — anchored at the stop loss line
- `CenterTop` — fixed at the top center of the chart panel
- `CenterBottom` — fixed at the bottom center of the chart panel


### 2.7 Modes Explained

**SIMPLE Mode** — Base functionality. The tool provides visual RR visualization with lines, shaded zones, labels, and partial SL/TP levels. No order interaction.

**PLUS Mode** — Adds the Auto Order Qty engine. When `AutoOrderQty = true`, the tool computes the exact contract count needed to risk exactly `StopLoss` dollars, bounded by `MaxContracts`. The result is pushed to NT8's Chart Trader quantity selector in real time, so the trader can see and use the pre-calculated size before entering.

**PRO Mode** — Adds live price tracking and dynamic stop management. When `TrackLastPrice = true`, the entry anchor automatically follows the selected `PriceSource` (e.g. last price) on every render frame. The `StopMode` determines how the stop behaves as the entry moves:

- **FLEX:** The stop stays at its fixed price level. As the entry moves, the risk distance changes, so contracts are recalculated dynamically.
- **HARD:** The stop moves in lockstep with the entry, maintaining a fixed distance. Contracts remain constant.

**StopTrackOnFill:** A safety feature within PRO mode. When enabled, AUTO TRACK automatically disengages the moment a position is detected on the instrument (indicating an entry was filled). This prevents the tool from continuing to move the entry line after the trade is live. When the position closes (goes flat), tracking automatically re-arms for the next setup.

### 2.8 How To Use — Step by Step

**Basic Usage (Visual Only):**
1. Select the drawing tool from the NT8 drawing toolbar
2. Click to place the **Entry** anchor at your intended entry price
3. Drag the **Risk anchor** to your stop loss price (below entry for longs, above for shorts)
4. The tool automatically calculates and places the **Reward anchor** at `ProfitRatio × risk distance`
5. All labels (RR ratio, partial SL/TP levels, FSL risk display) render automatically

**Auto-Sizing Contracts:**
1. Set `Stop Loss Budget (USD)` to your dollar risk per trade (e.g. $200)
2. Enable `Auto Order Qty` in Plus Settings (or press the AUTO CALC button on chart)
3. The FSL label will now display the calculated contract count
4. NT8's Chart Trader quantity will automatically update to match

**PRO Mode — Auto Track:**
1. Enable `Track Last Price` in PRO Settings (or press the AUTO TRACK button on chart)
2. The entry line now follows the last traded price in real time
3. Use the SOURCE button to change which price it tracks (Last/Close/High/Low/Open)
4. Use the STOP MODE button to toggle between FLEX and HARD stop behavior
5. Enable `Stop Track On Fill` if you want tracking to automatically turn off when filled

**FLIP:**
Press the FLIP button at any time to mirror the entire setup. If you drew a long setup, it becomes a short setup instantly, with entry and stop symmetric around the former entry price.

---

*Document generated: 2026-04-10*
*Drawing Tool: v1.6 | Strategy (Managed): v4.6 | Strategy (BETA/Unmanaged): v5.4*
*Author: Luis Fernando Garrido Miranda | Built with Claude Sonnet 4.6 by Anthropic*
#   A d v a n c e d - R i s k - t o - R e w a r d - b y - L G 
 
 
