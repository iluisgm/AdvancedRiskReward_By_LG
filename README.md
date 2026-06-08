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

### 2.6 Public Accessor Methods (Strategy API)

These methods are called by `DrawingToolSyncStrategy_BETA` on every tick to read the drawing tool's current state. They are available from drawing tool **v1.4 onward**.

```csharp
/// Returns all TP prices as double[]:
/// [0] = 1R price (first partial), [1] = 2R, ... [n] = Final TP
/// Prices computed as: entry + i × riskPerUnit (exact, no rounding drift)
double[] GetTPPrices()

/// Returns all SL prices as double[]:
/// [0] = SL1 (first partial), [1] = SL2, ... [n-1] = FSL
double[] GetSLPrices()

/// Returns the entry anchor price, rounded to instrument tick size
double GetEntryPrice()

/// Returns the FSL (Final Stop Loss) anchor price, rounded to tick size
double GetStopPrice()

/// Returns the RewardAnchor price (Final TP), rounded to tick size
double GetFinalTPPrice()

/// Returns true if Entry > Stop (long trade), false if Entry < Stop (short trade)
bool IsLongTrade()

/// Returns calculated contract quantity:
/// qty = round(StopLossBudget / riskPerContract), clamped [1, MaxContracts]
/// Returns 1 if Auto Order Qty is off or risk cannot be calculated
int GetCalculatedQty()

/// Returns dollar risk per single contract: |entry - stop| × PointValue
double GetRiskPerContract()
```

**Change Detection:**

```csharp
/// Monotonically increasing counter.
/// Increments on every anchor drag, flip, or auto-track update.
/// Strategy polls this and only recalculates when value has changed.
public int ChangeVersion { get; set; }
```

**CRITICAL:** `ChangeVersion` must have a **public setter** (not `private set`) because NT8's XML deserializer requires a public setter on all serialized properties. `private set` causes a deserialization error when saving or loading drawing tool templates. This was a production bug fixed in v1.5.

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

## 3. Drawing Tool Source Code

The complete source of `AdvancedRiskReward_LuisGarrido.cs` at **v1.6** is preserved below for reference, context, and future AI agent consumption.

```csharp
// ============================================================================
//  Advanced Risk Reward By Luis Garrido
//  NinjaTrader 8 Drawing Tool — Version 1.6
// ============================================================================
//
//  DESCRIPTION:
//  A professional-grade Risk/Reward drawing tool for NinjaTrader 8.
//  Extends the standard RR ruler with multi-level stop and target management,
//  automatic contract sizing based on a fixed dollar risk budget, real-time
//  Chart Trader synchronization, and on-chart interactive buttons.
//
//  FEATURES:
//  - Visual risk/reward ruler with configurable profit ratio (ProfitRatio)
//  - Multiple partial Stop Loss levels (StopRatio) between entry and main stop
//  - Multiple partial Take Profit levels between entry and final target
//  - FSL (Final Stop Loss) line: always shows contracts and risk budget,
//    plus optional Currency / Ticks / Points / Percent via SL Display settings
//  - Partial TP labels: RR ratio plus optional Currency / Ticks / Points /
//    Percent via TP Display settings
//  - Shaded zones for stop loss and profit areas
//  - Prominent anchor circles on Entry, Final TP, and FSL lines
//  - Configurable font size for all chart labels
//  - [PLUS] Auto Order Qty: calculates and syncs the exact number of contracts
//    needed to risk a fixed dollar amount to the Chart Trader quantity selector
//  - [PLUS] Max Contracts cap to prevent oversizing
//  - Resets Chart Trader quantity to 1 when the drawing is removed
//  - [PRO] Auto Track: entry line follows selected price source in real time
//  - [PRO] Price Source: LastPrice / Close / High / Low / Open
//  - [PRO] Stop Mode FLEX: SL stays fixed, contracts recalculate dynamically
//  - [PRO] Stop Mode HARD: SL moves with entry, fixed distance and contracts
//  - [PRO] Stop Track On Fill: auto-disables tracking when a position opens
//  - [PRO] FLIP: mirrors SL/TP to opposite side keeping same risk distance
//  - [PRO] Show Buttons On Hover: button panel auto-shows when mouse is over
//    any part of the drawing tool (lines, shaded zones, anchors, buttons)
//    and auto-hides when the mouse leaves
//
//  ON-CHART BUTTONS (collapsible panel):
//  - ◀/▶        : minimize / expand the button panel
//  - FLIP        : instantly flip long/short direction
//  - AUTO CALC   : toggle contract auto-sizing on/off
//  - AUTO TRACK  : toggle price tracking on/off
//  - SOURCE      : cycle price source (only when AUTO TRACK is ON)
//  - STOP MODE   : toggle FLEX/HARD (only when AUTO TRACK is ON)
//  Button panel hidden by default — click ◀ to expand, or enable hover mode
//  Button position: NearEntry / NearTP / NearFSL / CenterTop / CenterBottom
//  Button opacity: configurable 0-100%
//
//  MODES:
//  - SIMPLE : Visual RR tool, no order logic            (current)
//  - PLUS   : Contract sizing + Chart Trader sync       (current)
//  - PRO    : Entry follows price, HARD/FLEX stop mgmt  (current)
//
// ----------------------------------------------------------------------------
//  CHANGELOG:
//
//  v1.6 (2026-04-08)
//  - FIX: StopTrackOnFill now re-arms auto-tracking when position goes flat —
//    previously, after a fill disabled tracking, userOverrideTracking stayed
//    true and TrackLastPrice stayed false permanently until the user manually
//    clicked AUTO TRACK again. Now, when StopTrackOnFill is enabled and the
//    account returns to flat (no position on the instrument), both flags reset
//    automatically so tracking resumes for the next trade setup.
//  - FIX: Price tracking no longer executes one extra render frame after fill
//    detection — the tracking code block is now guarded so it skips immediately
//    on the same frame that StopTrackOnFill disables tracking.
//
//  v1.5 (2026-04-03)
//  - FIX: ChangeVersion property changed from { get; private set; } to
//    { get; set; } — NT8's XML deserializer requires a public setter on all
//    serialized properties; private set caused "Cannot deserialize" error
//    when saving or loading drawing tool templates
//
//  v1.4 (2026-04-03)
//  - NEW: Public accessor methods for DrawingToolSyncStrategy integration:
//    GetTPPrices()      — returns double[] of all TP prices (partial + final)
//    GetSLPrices()      — returns double[] of all SL prices (partial + FSL)
//    GetEntryPrice()    — entry anchor rounded to tick size
//    GetStopPrice()     — FSL anchor rounded to tick size
//    GetFinalTPPrice()  — final TP anchor rounded to tick size
//    IsLongTrade()      — true if entry > stop (long), false if short
//    GetCalculatedQty() — contract qty from Auto Order Qty logic
//    GetRiskPerContract() — dollar risk per single contract at FSL distance
//  - These methods replicate the math already used in OnRender and
//    UpdateOrderQty, ensuring the strategy reads identical values
//
//  v1.3 (2026-04-02)
//  - NEW: Added public ChangeVersion property — monotonically increasing
//    counter that increments on every anchor move (drag, flip, auto-track).
//    Used by DrawingToolSyncStrategy for efficient change detection polling.
//
//  v1.2 (2026-03-24)
//  - FIX: TextLayout and TextFormat DirectX objects in DrawPriceText,
//    DrawPriceTextPartials, and DrawPartialStopText were not disposed after
//    each render frame — wrapped in using blocks to prevent unmanaged
//    resource leak on every chart update
//  - FIX: PriceSource.LastPrice had no explicit case in the switch statement
//    and silently fell through to default — added explicit case for clarity
//    and safety against future enum additions
//  - FIX: Contract calculation in GetPriceString could divide by zero if
//    PointValue is zero — guarded with denominator check
//  - FIX: StopTrackOnFill block in OnRender was mis-indented, making it
//    appear to be at the outer scope rather than nested inside the
//    TrackLastPrice guard block
//
//  v1.1 (2026-03-20)
//  - FIX: Drawing tool disappeared in "All Charts" mode when scrolling past
//    all anchor points — IsVisibleOnChart now correctly keeps the tool visible
//    as long as extended lines reach the visible window
//  - NEW: Button panel now hidden by default to reduce chart noise
//  - NEW: "Show Buttons On Hover" setting (~ PRO Settings) — when enabled,
//    the full button panel auto-appears when mouse is anywhere over the tool
//    (entry/stop/target lines, shaded zones, anchor circles, or buttons)
//    and auto-hides when the mouse moves away
//  - FIX: Buttons were disappearing when moving mouse from a tool line onto
//    the button panel — hover state now correctly maintained over buttons
//  - FIX: Partial TP labels showed Ticks/Points/Currency of the full target
//    distance on every level — each label now uses its own interpolated price
//    calculated in price space (not pixel space), so TP1 matches FSL exactly
//    and TP2 = 2x, TP3 = 3x, etc. Currency, Ticks, Points and Percent all
//    now correctly reflect each partial level's actual distance from entry
//  - FIX: Partial TP prices were still 1 tick off due to floating-point
//    rounding when interpolating from entry→target — prices are now computed
//    as entry + i × riskPerUnit (same math as SetReward), guaranteeing
//    TP1 = FSL distance exactly with zero drift
//
//  v1.0 (initial release)
//  - Full feature release: visual RR ruler, partial SL/TP levels, shaded
//    zones, anchor circles, Auto Order Qty, Chart Trader sync, PRO mode
//    buttons, FLIP, AUTO CALC, AUTO TRACK, SOURCE, STOP MODE
//
// ----------------------------------------------------------------------------
//  Vibe Coded by Luis Fernando Garrido Miranda
//  using LLM Claude Sonnet 4.6 by Anthropic
//
//  Inspired by the Risk Reward tool of YosewCapital and the
//  Advanced Risk Reward tool by Bruno Meza.
//
//  Social media:
//  X: @iluisgm
//
//  DONATE — if this tool saves you time or money, consider supporting:
//  PayPal  : lfgm00
//  Crypto  : USDT (ERC-20 Network)
//            0xEF0E0eFe33bE9487ca1D017Ceb5b1C70a3bf36BC
//
// ----------------------------------------------------------------------------
//  DISCLAIMER:
//  Any investments, trades and/or speculations made using this tool are done
//  solely at your own risk. Past results do not guarantee future performance.
// ============================================================================
#region Using declarations

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using NinjaTrader.Cbi;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.Tools;

#endregion

namespace NinjaTrader.NinjaScript.DrawingTools
{
	public enum StopMode       { Flex, Hard }
	public enum PriceSource    { LastPrice, Close, High, Low, Open }
	public enum ButtonPosition { NearEntry, NearTP, NearFSL, CenterTop, CenterBottom }

	public class AdvancedRiskReward_LuisGarrido : DrawingTool
	{
		private const int		cursorSensitivity		= 15;
		private ChartAnchor		editingAnchor;
		private double			entryPrice;
		private bool			needsRatioUpdate		= true;
		private double			profitRatio				= 3;
		private double			stopRatio				= 2;
		private double			risk;
		private double			reward;
		private double			stopPrice;
		private double			targetPrice;
		private double			textleftPoint;
		private double			textRightPoint;

		/// <summary>
		/// Monotonically increasing counter that increments whenever anchors move.
		/// Used by DrawingToolSyncStrategy to detect changes without expensive
		/// price comparisons on every tick. Strategy polls this value and only
		/// recalculates when it differs from its last-seen value.
		/// </summary>
		[Browsable(false)]
		public int ChangeVersion { get; set; }  // public setter required for NT8 XML deserializer

		[Browsable(false)]
		private bool DrawTarget { get { return (RiskAnchor != null && !RiskAnchor.IsEditing) || (RewardAnchor != null && !RewardAnchor.IsEditing); } }

		[Display(Order = 1)]
		public ChartAnchor		EntryAnchor			{ get; set; }
		[Display(Order = 2)]
		public ChartAnchor		RiskAnchor			{ get; set; }
		[Browsable(false)]
		public ChartAnchor		RewardAnchor		{ get; set; }

		public override object Icon { get { return Icons.DrawRiskReward; } }

		// ── ProfitRatio (renamed from Ratio) ──────────────────────────────────────
		[Range(0, double.MaxValue)]
		[NinjaScriptProperty]
		[Display(Name = "Profit Ratio", GroupName = "Risk Reward Settings", Order = 1)]
		public double ProfitRatio
		{
			get { return profitRatio; }
			set
			{
				if (profitRatio.ApproxCompare(value) == 0)
					return;
				profitRatio			= value;
				needsRatioUpdate	= true;
			}
		}

		// ── StopRatio (NEW) ───────────────────────────────────────────────────────
		[Range(1, double.MaxValue)]
		[NinjaScriptProperty]
		[Display(Name = "Stop Ratio", GroupName = "Risk Reward Settings", Order = 2)]
		public double StopRatio
		{
			get { return stopRatio; }
			set
			{
				if (stopRatio.ApproxCompare(value) == 0)
					return;
				stopRatio = value;
			}
		}

		// ── Dollar stop loss budget ───────────────────────────────────────────────
		[Display(Name = "Stop Loss Budget (USD)", GroupName = "Risk Reward Settings", Order = 0)]
		public double StopLoss { get; set; }

		// ── Show partial TP levels flag ───────────────────────────────────────────
		[Display(Name = "Show Partial TPs", GroupName = "Risk Reward Settings", Order = 3)]
		public bool partialFlag { get; set; }

		// ── Line strokes ──────────────────────────────────────────────────────────
		[Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptDrawingToolAnchor",                    GroupName = "Line Settings", Order = 3)]
		public Stroke AnchorLineStroke			{ get; set; }

		[Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptDrawingToolRiskRewardLineStrokeEntry", GroupName = "Line Settings", Order = 6)]
		public Stroke EntryLineStroke			{ get; set; }

		[Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptDrawingToolRiskRewardLineStrokeRisk",  GroupName = "Line Settings", Order = 4)]
		public Stroke StopLineStroke			{ get; set; }

		[Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptDrawingToolRiskRewardLineStrokeReward",GroupName = "Line Settings", Order = 5)]
		public Stroke TargetLineStroke			{ get; set; }

		[Display(Name = "Stop Background",   GroupName = "Line Settings", Order = 7)]
		public Stroke StopLineStrokeBack		{ get; set; }

		[Display(Name = "Profit Background", GroupName = "Line Settings", Order = 8)]
		public Stroke TargetLineStrokeBack		{ get; set; }

		// ── Partial stop line stroke (configurable, red by default) ──────────────
		[Display(Name = "Partial Stop Lines", GroupName = "Line Settings", Order = 9)]
		public Stroke PartialStopLineStroke		{ get; set; }

		// ── Extend lines ──────────────────────────────────────────────────────────
		[Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptDrawingToolFibonacciRetracementsExtendLinesRight", GroupName = "Line Settings", Order = 2)]
		public bool IsExtendedLinesRight		{ get; set; }

		[Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptDrawingToolFibonacciRetracementsExtendLinesLeft",  GroupName = "Line Settings", Order = 1)]
		public bool IsExtendedLinesLeft			{ get; set; }

		// ── Text / display options ────────────────────────────────────────────────
		[Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptDrawingToolTextAlignment",        GroupName = "Risk Reward Settings", Order = 4)]
		public TextLocation TextAlignment		{ get; set; }

		[Range(6, 32)]
		[Display(Name = "Font Size", GroupName = "Risk Reward Settings", Order = 5)]
		public int LabelFontSize				{ get; set; }

		// ── TP display checkboxes ─────────────────────────────────────────────────
		[Display(Name = "Show Currency", GroupName = "TP Display", Order = 1)]
		public bool TpShowCurrency      { get; set; }
		[Display(Name = "Show Ticks",    GroupName = "TP Display", Order = 2)]
		public bool TpShowTicks         { get; set; }
		[Display(Name = "Show Points",   GroupName = "TP Display", Order = 3)]
		public bool TpShowPoints        { get; set; }
		[Display(Name = "Show Percent",  GroupName = "TP Display", Order = 4)]
		public bool TpShowPercent       { get; set; }

		// ── SL display checkboxes ─────────────────────────────────────────────────
		[Display(Name = "Show Currency", GroupName = "SL Display", Order = 1)]
		public bool SlShowCurrency      { get; set; }
		[Display(Name = "Show Ticks",    GroupName = "SL Display", Order = 2)]
		public bool SlShowTicks         { get; set; }
		[Display(Name = "Show Points",   GroupName = "SL Display", Order = 3)]
		public bool SlShowPoints        { get; set; }
		[Display(Name = "Show Percent",  GroupName = "SL Display", Order = 4)]
		public bool SlShowPercent       { get; set; }

		// ── Plus Settings ─────────────────────────────────────────────────────────
		[Display(Name = "Auto Order Qty", GroupName = "~ Plus Settings", Order = 1)]
		public bool AutoOrderQty        { get; set; }
		[Range(1, int.MaxValue)]
		[Display(Name = "Max Contracts",  GroupName = "~ Plus Settings", Order = 2)]
		public int MaxContracts         { get; set; }

		[Display(Name = "Track Last Price", GroupName = "~ PRO Settings", Order = 1)]
		public bool TrackLastPrice      { get; set; }

		[Display(Name = "Stop Mode",        GroupName = "~ PRO Settings", Order = 2)]
		public StopMode StopMode        { get; set; }

		[Display(Name = "Price Source",     GroupName = "~ PRO Settings", Order = 3)]
		public PriceSource PriceSource  { get; set; }

		[Display(Name = "Stop Track On Fill", GroupName = "~ PRO Settings", Order = 4)]
		public bool StopTrackOnFill     { get; set; }

		[Display(Name = "Button Position",  GroupName = "~ PRO Settings", Order = 5)]
		public ButtonPosition ButtonPosition { get; set; }

		[Range(0, 100)]
		[Display(Name = "Button Opacity %", GroupName = "~ PRO Settings", Order = 6)]
		public int ButtonOpacity { get; set; }

		[Display(Name = "Show Buttons On Hover", GroupName = "~ PRO Settings", Order = 7)]
		public bool ShowButtonsOnHover { get; set; }

		public override IEnumerable<ChartAnchor> Anchors { get { return new[] { EntryAnchor, RiskAnchor, RewardAnchor }; } }

		public override bool SupportsAlerts { get { return true; } }

		// ═══════════════════════════════════════════════════════════════════════════
		//  TEXT HELPERS
		// ═══════════════════════════════════════════════════════════════════════════

		private void DrawPriceText(ChartAnchor anchor, Point point, double price,
			ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale)
		{
			if (TextAlignment == TextLocation.Off)
				return;

			ChartBars chartBars = GetAttachedToChartBars();
			if (chartBars == null) return;

			if (!IsUserDrawn)
				price = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(anchor.Price);

			string priceString = GetPriceString(price, chartBars);

			Stroke color;
			textleftPoint  = RiskAnchor.GetPoint(chartControl, chartPanel, chartScale).X;
			textRightPoint = EntryAnchor.GetPoint(chartControl, chartPanel, chartScale).X;

			if      (anchor == RewardAnchor) color = TargetLineStroke;
			else if (anchor == RiskAnchor)   color = StopLineStroke;
			else if (anchor == EntryAnchor)  color = EntryLineStroke;
			else                             color = AnchorLineStroke;

			// FIX v1.2: wrap TextFormat and TextLayout in using to prevent
			// unmanaged DirectX resource leak on every render frame
			using (SharpDX.DirectWrite.TextFormat textFormat = GetLabelTextFormat())
			using (SharpDX.DirectWrite.TextLayout textLayout = new SharpDX.DirectWrite.TextLayout(
				Core.Globals.DirectWriteFactory, priceString, textFormat, chartPanel.H, textFormat.FontSize))
			{
				point.X = ResolveTextX(point, textLayout, chartPanel, chartControl, chartScale);
				RenderTarget.DrawTextLayout(
					new SharpDX.Vector2((float)point.X, (float)point.Y),
					textLayout, color.BrushDX, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
			}
		}

		private void DrawPriceTextPartials(ChartAnchor anchor, Point point, double price,
			ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, int numero)
		{
			if (TextAlignment == TextLocation.Off)
				return;

			ChartBars chartBars = GetAttachedToChartBars();
			if (chartBars == null) return;

			if (!IsUserDrawn)
				price = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(anchor.Price);

			string priceString = GetPriceStringPartials(price, chartBars, numero);

			Stroke color;
			textleftPoint  = RiskAnchor.GetPoint(chartControl, chartPanel, chartScale).X;
			textRightPoint = EntryAnchor.GetPoint(chartControl, chartPanel, chartScale).X;

			if      (anchor == RewardAnchor) color = TargetLineStroke;
			else if (anchor == RiskAnchor)   color = StopLineStroke;
			else if (anchor == EntryAnchor)  color = EntryLineStroke;
			else                             color = AnchorLineStroke;

			// FIX v1.2: wrap TextFormat and TextLayout in using to prevent
			// unmanaged DirectX resource leak on every render frame
			using (SharpDX.DirectWrite.TextFormat textFormat = GetLabelTextFormat())
			using (SharpDX.DirectWrite.TextLayout textLayout = new SharpDX.DirectWrite.TextLayout(
				Core.Globals.DirectWriteFactory, priceString, textFormat, chartPanel.H, textFormat.FontSize))
			{
				point.X = ResolveTextX(point, textLayout, chartPanel, chartControl, chartScale);
				RenderTarget.DrawTextLayout(
					new SharpDX.Vector2((float)point.X, (float)point.Y),
					textLayout, color.BrushDX, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
			}
		}

		// label for partial stop levels
		private void DrawPartialStopText(Point point, double price, int levelNumber,
			ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale)
		{
			if (TextAlignment == TextLocation.Off)
				return;

			ChartBars chartBars = GetAttachedToChartBars();
			if (chartBars == null) return;

			double yValueEntry = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(EntryAnchor.Price);
			double tickSize    = AttachedTo.Instrument.MasterInstrument.TickSize;
			double pointValue  = AttachedTo.Instrument.MasterInstrument.PointValue;

			// SL partial label — driven by SL Display checkboxes
			var slParts = new System.Collections.Generic.List<string>();
			slParts.Add("SL" + levelNumber.ToString());
			double partialCurrency = Math.Abs((yValueEntry - price) * pointValue);
			double partialTicks    = Math.Round(Math.Abs((yValueEntry - price) / tickSize), 0);
			double partialPoints   = Math.Round(Math.Abs(yValueEntry - price), 2);
			double partialPct      = Math.Abs((yValueEntry - price) / yValueEntry);
			if (SlShowCurrency) slParts.Add("-$" + partialCurrency.ToString("F2"));
			if (SlShowTicks)    slParts.Add("-"  + partialTicks.ToString("F0") + " Ticks");
			if (SlShowPoints)   slParts.Add("-"  + partialPoints.ToString("F2") + " Points");
			if (SlShowPercent)  slParts.Add("-"  + partialPct.ToString("P2", Core.Globals.GeneralOptions.CurrentCulture));
			string label = string.Join(" | ", slParts);

			textleftPoint  = RiskAnchor.GetPoint(chartControl, chartPanel, chartScale).X;
			textRightPoint = EntryAnchor.GetPoint(chartControl, chartPanel, chartScale).X;

			// FIX v1.2: wrap TextFormat and TextLayout in using to prevent
			// unmanaged DirectX resource leak on every render frame
			using (SharpDX.DirectWrite.TextFormat textFormat = GetLabelTextFormat())
			using (SharpDX.DirectWrite.TextLayout textLayout = new SharpDX.DirectWrite.TextLayout(
				Core.Globals.DirectWriteFactory, label, textFormat, chartPanel.H, textFormat.FontSize))
			{
				point.X = ResolveTextX(point, textLayout, chartPanel, chartControl, chartScale);
				RenderTarget.DrawTextLayout(
					new SharpDX.Vector2((float)point.X, (float)point.Y),
					textLayout, PartialStopLineStroke.BrushDX, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
			}
		}

		// ── Shared X-position resolver (consolidates duplicated switch blocks) ────
		private double ResolveTextX(Point point, SharpDX.DirectWrite.TextLayout textLayout,
			ChartPanel chartPanel, ChartControl chartControl, ChartScale chartScale)
		{
			double x = point.X;

			if (RiskAnchor.Time <= EntryAnchor.Time)
			{
				if (!IsExtendedLinesLeft && !IsExtendedLinesRight)
					switch (TextAlignment)
					{
						case TextLocation.InsideLeft:   x = textleftPoint; break;
						case TextLocation.InsideRight:  x = textRightPoint - textLayout.Metrics.Width; break;
						case TextLocation.ExtremeLeft:  x = textleftPoint; break;
						case TextLocation.ExtremeRight: x = textRightPoint - textLayout.Metrics.Width; break;
					}
				else if (IsExtendedLinesLeft && !IsExtendedLinesRight)
					switch (TextAlignment)
					{
						case TextLocation.InsideLeft:   x = textleftPoint; break;
						case TextLocation.InsideRight:  x = textRightPoint - textLayout.Metrics.Width; break;
						case TextLocation.ExtremeLeft:  x = chartPanel.X; break;
						case TextLocation.ExtremeRight: x = textRightPoint - textLayout.Metrics.Width; break;
					}
				else if (!IsExtendedLinesLeft && IsExtendedLinesRight)
					switch (TextAlignment)
					{
						case TextLocation.InsideLeft:   x = textleftPoint; break;
						case TextLocation.InsideRight:  x = textRightPoint - textLayout.Metrics.Width; break;
						case TextLocation.ExtremeLeft:  x = textleftPoint; break;
						case TextLocation.ExtremeRight: x = chartPanel.W - textLayout.Metrics.Width; break;
					}
				else
					switch (TextAlignment)
					{
						case TextLocation.InsideLeft:   x = textleftPoint; break;
						case TextLocation.InsideRight:  x = textRightPoint - textLayout.Metrics.Width; break;
						case TextLocation.ExtremeRight: x = chartPanel.W - textLayout.Metrics.Width; break;
						case TextLocation.ExtremeLeft:  x = chartPanel.X; break;
					}
			}
			else
			{
				if (!IsExtendedLinesLeft && !IsExtendedLinesRight)
					switch (TextAlignment)
					{
						case TextLocation.InsideLeft:   x = textRightPoint; break;
						case TextLocation.InsideRight:  x = textleftPoint - textLayout.Metrics.Width; break;
						case TextLocation.ExtremeLeft:  x = textRightPoint; break;
						case TextLocation.ExtremeRight: x = textleftPoint - textLayout.Metrics.Width; break;
					}
				else if (IsExtendedLinesLeft && !IsExtendedLinesRight)
					switch (TextAlignment)
					{
						case TextLocation.InsideLeft:   x = textRightPoint; break;
						case TextLocation.InsideRight:  x = textleftPoint - textLayout.Metrics.Width; break;
						case TextLocation.ExtremeLeft:  x = chartPanel.X; break;
						case TextLocation.ExtremeRight: x = textleftPoint - textLayout.Metrics.Width; break;
					}
				else if (!IsExtendedLinesLeft && IsExtendedLinesRight)
					switch (TextAlignment)
					{
						case TextLocation.InsideLeft:   x = textRightPoint; break;
						case TextLocation.InsideRight:  x = textleftPoint - textLayout.Metrics.Width; break;
						case TextLocation.ExtremeLeft:  x = textRightPoint; break;
						case TextLocation.ExtremeRight: x = chartPanel.W - textLayout.Metrics.Width; break;
					}
				else
					switch (TextAlignment)
					{
						case TextLocation.InsideLeft:   x = textRightPoint; break;
						case TextLocation.InsideRight:  x = textleftPoint - textLayout.Metrics.Width; break;
						case TextLocation.ExtremeRight: x = chartPanel.W - textLayout.Metrics.Width; break;
						case TextLocation.ExtremeLeft:  x = chartPanel.X; break;
					}
			}
			return x;
		}

		// ═══════════════════════════════════════════════════════════════════════════
		//  PRICE STRING BUILDERS
		// ═══════════════════════════════════════════════════════════════════════════

		private string GetPriceString(double price, ChartBars chartBars)
		{
			double yValueEntry = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(EntryAnchor.Price);
			double tickSize    = AttachedTo.Instrument.MasterInstrument.TickSize;
			double pointValue  = AttachedTo.Instrument.MasterInstrument.PointValue;
			double pct         = price > yValueEntry ? 1 : price == yValueEntry ? 0 : -1;
			double contracts   = 0.0;

			// FIX v1.2: guard against division by zero when pointValue is 0
			double denom = Math.Abs((price - yValueEntry) * pointValue);
			contracts = denom > 0 ? Math.Round(StopLoss / denom, 2) : 0;

			if (pct == 0)
				return "ENTRY";

			// FSL label — C: and Risk: always show, rest driven by checkboxes
			var fslParts = new System.Collections.Generic.List<string>();
			fslParts.Add("FSL");
			fslParts.Add("C:" + contracts.ToString("F2"));
			fslParts.Add("Risk:$" + StopLoss.ToString("F0"));
			double fslCur  = Math.Round(Math.Abs((price - yValueEntry) * pointValue), 2);
			double fslTick = Math.Round(Math.Abs((price - yValueEntry) / tickSize), 0);
			double fslPts  = Math.Round(Math.Abs(price - yValueEntry), 2);
			double fslPct  = Math.Abs((price - yValueEntry) / yValueEntry);
			if (SlShowCurrency) fslParts.Add("-$" + fslCur.ToString("F2"));
			if (SlShowTicks)    fslParts.Add("-"  + fslTick.ToString("F0") + " Ticks");
			if (SlShowPoints)   fslParts.Add("-"  + fslPts.ToString("F2") + " Points");
			if (SlShowPercent)  fslParts.Add("-"  + fslPct.ToString("P2", Core.Globals.GeneralOptions.CurrentCulture));
			return string.Join(" | ", fslParts);
		}

		private string GetPriceStringPartials(double price, ChartBars chartBars, int numero)
		{
			double yValueEntry = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(EntryAnchor.Price);
			double tickSize    = AttachedTo.Instrument.MasterInstrument.TickSize;
			double pointValue  = AttachedTo.Instrument.MasterInstrument.PointValue;
			// Currency = actual dollar value at this partial price level (price is already the correct 1R/2R/etc. price)
			double tpCur = Math.Round(Math.Abs((price - yValueEntry) * pointValue), 2);

			// TP label — driven by checkboxes
			var tpParts = new System.Collections.Generic.List<string>();
			tpParts.Add("RR 1:" + numero.ToString());
			double tpTick = Math.Round(Math.Abs((price - yValueEntry) / tickSize), 0);
			double tpPts  = Math.Round(Math.Abs(price - yValueEntry), 2);
			double tpPct  = Math.Abs((price - yValueEntry) / yValueEntry);
			if (TpShowCurrency) tpParts.Add("$" + tpCur.ToString("F2"));
			if (TpShowTicks)    tpParts.Add(tpTick.ToString("F0") + " Ticks");
			if (TpShowPoints)   tpParts.Add(tpPts.ToString("F2") + " Points");
			if (TpShowPercent)  tpParts.Add(tpPct.ToString("P2", Core.Globals.GeneralOptions.CurrentCulture));
			return string.Join(" | ", tpParts);
		}

		// ═══════════════════════════════════════════════════════════════════════════
		//  SELECTION / ALERTS / VISIBILITY / MIN-MAX
		// ═══════════════════════════════════════════════════════════════════════════

		public override IEnumerable<AlertConditionItem> GetAlertConditionItems()
		{
			return Anchors.Select(anchor => new AlertConditionItem
			{
				Name                  = anchor.DisplayName,
				ShouldOnlyDisplayName = true,
				Tag                   = anchor
			});
		}

		public override Cursor GetCursor(ChartControl chartControl, ChartPanel chartPanel,
			ChartScale chartScale, Point point)
		{
			switch (DrawingState)
			{
				case DrawingState.Building: return Cursors.Pen;
				case DrawingState.Moving:   return IsLocked ? Cursors.No : Cursors.SizeAll;
				case DrawingState.Editing:  return IsLocked ? Cursors.No : (editingAnchor == EntryAnchor ? Cursors.SizeNESW : Cursors.SizeNWSE);
				default:
					Point entryAnchorPixelPoint = EntryAnchor.GetPoint(chartControl, chartPanel, chartScale);
					float px = (float)point.X, py = (float)point.Y;

					// Button rects — set isHovered=true and return early (buttons stay visible)
					if (btnRectsValid)
					{
						if (minBtnRect.Contains(px, py))                              { isHovered = true; return Cursors.Arrow; }
						if (flipBtnRect.Contains(px, py))                             { isHovered = true; return Cursors.Arrow; }
						if (calcBtnRect.Contains(px, py))                             { isHovered = true; return Cursors.Arrow; }
						if (trackBtnRect.Contains(px, py))                            { isHovered = true; return Cursors.Arrow; }
						if (TrackLastPrice && srcBtnRect.Contains(px, py))            { isHovered = true; return Cursors.Arrow; }
						if (TrackLastPrice && stopModeBtnRect.Contains(px, py))       { isHovered = true; return Cursors.Arrow; }
					}

					// Anchor circles
					ChartAnchor closest = GetClosestAnchor(chartControl, chartPanel, chartScale, cursorSensitivity, point);
					if (closest != null)
					{
						isHovered = true;
						return IsLocked ? Cursors.Arrow : (closest == EntryAnchor ? Cursors.SizeNESW : Cursors.SizeNWSE);
					}

					// Entry <-> Stop anchor line
					Point  stopAnchorPixelPoint = RiskAnchor.GetPoint(chartControl, chartPanel, chartScale);
					Vector anchorsVector        = stopAnchorPixelPoint - entryAnchorPixelPoint;
					if (MathHelper.IsPointAlongVector(point, entryAnchorPixelPoint, anchorsVector, cursorSensitivity))
					{
						isHovered = true;
						return IsLocked ? Cursors.Arrow : Cursors.SizeAll;
					}

					if (!DrawTarget)
					{
						isHovered = false;
						return null;
					}

					Point  targetPoint        = RewardAnchor.GetPoint(chartControl, chartPanel, chartScale);
					Vector targetToEntryVector = targetPoint - entryAnchorPixelPoint;

					// Entry <-> Target anchor line
					if (MathHelper.IsPointAlongVector(point, entryAnchorPixelPoint, targetToEntryVector, cursorSensitivity))
					{
						isHovered = true;
						return IsLocked ? Cursors.Arrow : Cursors.SizeAll;
					}

					// Horizontal lines — check if mouse Y is within cursorSensitivity of any of the 3 lines
					// and X is within the drawn line's horizontal extent
					ChartPanel cp      = chartControl.ChartPanels[chartScale.PanelIndex];
					double lineStartX  = IsExtendedLinesLeft  ? cp.X : Math.Min(entryAnchorPixelPoint.X, Math.Min(stopAnchorPixelPoint.X, targetPoint.X));
					double lineEndX    = IsExtendedLinesRight ? cp.X + cp.W : Math.Max(entryAnchorPixelPoint.X, Math.Max(stopAnchorPixelPoint.X, targetPoint.X));

					if (px >= lineStartX && px <= lineEndX)
					{
						if (Math.Abs(py - entryAnchorPixelPoint.Y) <= cursorSensitivity ||
							Math.Abs(py - stopAnchorPixelPoint.Y)  <= cursorSensitivity ||
							Math.Abs(py - targetPoint.Y)           <= cursorSensitivity)
						{
							isHovered = true;
							return IsLocked ? Cursors.Arrow : Cursors.SizeAll;
						}

						// Inside the shaded zones (between entry and stop, or entry and target)
						double entryY  = entryAnchorPixelPoint.Y;
						double stopY   = stopAnchorPixelPoint.Y;
						double targetY = targetPoint.Y;
						double zoneMinY = Math.Min(Math.Min(entryY, stopY), targetY);
						double zoneMaxY = Math.Max(Math.Max(entryY, stopY), targetY);
						if (py >= zoneMinY && py <= zoneMaxY)
						{
							isHovered = true;
							return null;
						}
					}

					isHovered = false;
					return null;
			}
		}

		public override Point[] GetSelectionPoints(ChartControl chartControl, ChartScale chartScale)
		{
			ChartPanel chartPanel = chartControl.ChartPanels[chartScale.PanelIndex];
			Point entryPoint      = EntryAnchor.GetPoint(chartControl, chartPanel, chartScale);
			Point stopPoint       = RiskAnchor.GetPoint(chartControl, chartPanel, chartScale);

			if (!DrawTarget)
				return new[] { entryPoint, stopPoint };

			Point targetPoint = RewardAnchor.GetPoint(chartControl, chartPanel, chartScale);
			return new[] { entryPoint, stopPoint, targetPoint };
		}

		public override bool IsAlertConditionTrue(AlertConditionItem conditionItem, Condition condition,
			ChartAlertValue[] values, ChartControl chartControl, ChartScale chartScale)
		{
			ChartAnchor chartAnchor = conditionItem.Tag as ChartAnchor;
			if (chartAnchor == null) return false;

			ChartPanel chartPanel = chartControl.ChartPanels[PanelIndex];
			double alertY         = chartScale.GetYByValue(chartAnchor.Price);
			Point  entryPoint     = EntryAnchor.GetPoint(chartControl, chartPanel, chartScale);
			Point  stopPoint      = RiskAnchor.GetPoint(chartControl, chartPanel, chartScale);
			Point  targetPoint    = RewardAnchor.GetPoint(chartControl, chartPanel, chartScale);

			double anchorMinX  = DrawTarget ? new[] { entryPoint.X, stopPoint.X, targetPoint.X }.Min() : new[] { entryPoint.X, stopPoint.X }.Min();
			double anchorMaxX  = DrawTarget ? new[] { entryPoint.X, stopPoint.X, targetPoint.X }.Max() : new[] { entryPoint.X, stopPoint.X }.Max();
			double lineStartX  = IsExtendedLinesLeft  ? chartPanel.X : anchorMinX;
			double lineEndX    = IsExtendedLinesRight ? chartPanel.X + chartPanel.W : anchorMaxX;

			double firstBarX   = chartControl.GetXByTime(values[0].Time);
			double firstBarY   = chartScale.GetYByValue(values[0].Value);

			if (lineEndX < firstBarX) return false;

			Point lineStartPoint = new Point(lineStartX, alertY);
			Point lineEndPoint   = new Point(lineEndX,   alertY);
			Point barPoint       = new Point(firstBarX, firstBarY);

			MathHelper.PointLineLocation pointLocation =
				MathHelper.GetPointLineLocation(lineStartPoint, lineEndPoint, barPoint);

			switch (condition)
			{
				case Condition.Greater:      return pointLocation == MathHelper.PointLineLocation.LeftOrAbove;
				case Condition.GreaterEqual: return pointLocation == MathHelper.PointLineLocation.LeftOrAbove || pointLocation == MathHelper.PointLineLocation.DirectlyOnLine;
				case Condition.Less:         return pointLocation == MathHelper.PointLineLocation.RightOrBelow;
				case Condition.LessEqual:    return pointLocation == MathHelper.PointLineLocation.RightOrBelow || pointLocation == MathHelper.PointLineLocation.DirectlyOnLine;
				case Condition.Equals:       return pointLocation == MathHelper.PointLineLocation.DirectlyOnLine;
				case Condition.NotEqual:     return pointLocation != MathHelper.PointLineLocation.DirectlyOnLine;
				case Condition.CrossAbove:
				case Condition.CrossBelow:
					Predicate<ChartAlertValue> predicate = v =>
					{
						double barX = chartControl.GetXByTime(v.Time);
						double barY = chartScale.GetYByValue(v.Value);
						Point  stepBarPoint = new Point(barX, barY);
						MathHelper.PointLineLocation ptLocation =
							MathHelper.GetPointLineLocation(lineStartPoint, lineEndPoint, stepBarPoint);
						if (condition == Condition.CrossAbove)
							return ptLocation == MathHelper.PointLineLocation.LeftOrAbove;
						return ptLocation == MathHelper.PointLineLocation.RightOrBelow;
					};
					return MathHelper.DidPredicateCross(values, predicate);
			}
			return false;
		}

		public override bool IsVisibleOnChart(ChartControl chartControl, ChartScale chartScale,
			DateTime firstTimeOnChart, DateTime lastTimeOnChart)
		{
			if (DrawingState == DrawingState.Building)
				return true;

			if (Anchors.Any(a => a.Time >= firstTimeOnChart && a.Time <= lastTimeOnChart))
				return true;

			DateTime minAnchorTime = Anchors.Min(a => a.Time);
			DateTime maxAnchorTime = Anchors.Max(a => a.Time);

			if (minAnchorTime <= lastTimeOnChart && maxAnchorTime >= firstTimeOnChart)
				return true;

			if (IsExtendedLinesRight && maxAnchorTime <= firstTimeOnChart)
				return true;

			if (IsExtendedLinesLeft && minAnchorTime >= lastTimeOnChart)
				return true;

			return false;
		}

		public override void OnCalculateMinMax()
		{
			MinValue = double.MaxValue;
			MaxValue = double.MinValue;

			if (!IsVisible) return;

			if (Anchors.Any(a => !a.IsEditing))
				foreach (ChartAnchor anchor in Anchors)
				{
					if (anchor.DisplayName == RewardAnchor.DisplayName && !DrawTarget)
						continue;
					MinValue = Math.Min(anchor.Price, MinValue);
					MaxValue = Math.Max(anchor.Price, MaxValue);
				}
		}

		// ═══════════════════════════════════════════════════════════════════════════
		//  MOUSE EVENTS
		// ═══════════════════════════════════════════════════════════════════════════

		public override void OnMouseDown(ChartControl chartControl, ChartPanel chartPanel,
			ChartScale chartScale, ChartAnchor dataPoint)
		{
			// ── Toggle buttons — intercept BEFORE DrawingState switch ───────────────
			if (btnRectsValid && DrawingState != DrawingState.Building)
			{
				Point  mp  = dataPoint.GetPoint(chartControl, chartPanel, chartScale);
				float  mpx = (float)mp.X, mpy = (float)mp.Y;

				// 0. MINIMIZE — always clickable
				if (minBtnRect.Contains(mpx, mpy))
				{
					isPanelMinimized = !isPanelMinimized;
					return;
				}

				// 1-5 only work when panel is expanded
				if (!isPanelMinimized)
				{
					if (flipBtnRect.Contains(mpx, mpy))
					{
						FlipDrawing();
						return;
					}
					if (calcBtnRect.Contains(mpx, mpy))
					{
						AutoOrderQty = !AutoOrderQty;
						return;
					}
					if (trackBtnRect.Contains(mpx, mpy))
					{
						TrackLastPrice       = !TrackLastPrice;
						userOverrideTracking = false;
						return;
					}
					if (TrackLastPrice && srcBtnRect.Contains(mpx, mpy))
					{
						PriceSource = (PriceSource)(((int)PriceSource + 1) % 5);
						return;
					}
					if (TrackLastPrice && stopModeBtnRect.Contains(mpx, mpy))
					{
						StopMode = (StopMode == StopMode.Flex) ? StopMode.Hard : StopMode.Flex;
						return;
					}
				}
			}

			switch (DrawingState)
			{
				case DrawingState.Building:
					if (EntryAnchor.IsEditing)
					{
						dataPoint.CopyDataValues(EntryAnchor);
						dataPoint.CopyDataValues(RiskAnchor);
						EntryAnchor.IsEditing = false;
						entryPrice = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(EntryAnchor.Price);
					}
					else if (RiskAnchor.IsEditing)
					{
						dataPoint.CopyDataValues(RiskAnchor);
						RiskAnchor.IsEditing = false;
						stopPrice = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(RiskAnchor.Price);
						SetReward();
						RewardAnchor.Time      = EntryAnchor.Time;
						RewardAnchor.SlotIndex = EntryAnchor.SlotIndex;
						RewardAnchor.IsEditing = false;
					}
					if (!EntryAnchor.IsEditing && !RiskAnchor.IsEditing && !RewardAnchor.IsEditing)
					{
						DrawingState = DrawingState.Normal;
						IsSelected   = false;
					}
					break;

				case DrawingState.Normal:
					Point point  = dataPoint.GetPoint(chartControl, chartPanel, chartScale);
					editingAnchor = GetClosestAnchor(chartControl, chartPanel, chartScale, cursorSensitivity, point);
					if (editingAnchor != null)
					{
						editingAnchor.IsEditing = true;
						DrawingState = DrawingState.Editing;
					}
					else if (GetCursor(chartControl, chartPanel, chartScale, point) == null)
						IsSelected = false;
					else
						DrawingState = DrawingState.Moving;
					break;
			}
		}

		// ── Shared: draw anchor circle ─────────────────────────────────────────────
		private void DrawAnchorCircle(float x, float y, SharpDX.Direct2D1.Brush borderBrush)
		{
			var ellipse = new SharpDX.Direct2D1.Ellipse(new SharpDX.Vector2(x, y), 7f, 7f);
			using (var fillBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget,
				new SharpDX.Color4(1f, 1f, 1f, 0.90f)))
			{
				RenderTarget.FillEllipse(ellipse, fillBrush);
				RenderTarget.DrawEllipse(ellipse, borderBrush, 2f);
			}
		}

		// ── Shared: TextFormat at user font size ──────────────────────────────────
		private SharpDX.DirectWrite.TextFormat GetLabelTextFormat()
		{
			var font = new SimpleFont("Arial", LabelFontSize) { Bold = false };
			var tf   = font.ToDirectWriteTextFormat();
			tf.TextAlignment = SharpDX.DirectWrite.TextAlignment.Leading;
			tf.WordWrapping  = SharpDX.DirectWrite.WordWrapping.NoWrap;
			return tf;
		}

		// ── PRO: flip drawing ─────────────────────────────────────────────────────
		private void FlipDrawing()
		{
			if (AttachedTo == null || EntryAnchor == null || RiskAnchor == null) return;
			double entry    = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(EntryAnchor.Price);
			double stop     = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(RiskAnchor.Price);
			double distance = entry - stop;
			RiskAnchor.Price = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(entry + distance);
			SetReward();
			ChangeVersion++;
		}

		// ── PRO: draw on-chart toggle buttons ─────────────────────────────────────
		private void DrawToggleButtons(ChartControl chartControl, ChartPanel chartPanel,
			ChartScale chartScale, Point entryPoint, double lineEndX)
		{
			SharpDX.DirectWrite.TextFormat tf = GetLabelTextFormat();
			tf.TextAlignment = SharpDX.DirectWrite.TextAlignment.Center;

			float btnW = 130f, btnH = 18f, pad = 2f;
			float baseX, baseY;

			switch (ButtonPosition)
			{
				case ButtonPosition.NearTP:
					Point tpPt = RewardAnchor.GetPoint(chartControl, chartPanel, chartScale);
					baseX = (float)lineEndX + 6f;
					baseY = (float)tpPt.Y - btnH - pad;
					break;
				case ButtonPosition.NearFSL:
					Point slPt = RiskAnchor.GetPoint(chartControl, chartPanel, chartScale);
					baseX = (float)lineEndX + 6f;
					baseY = (float)slPt.Y - btnH - pad;
					break;
				case ButtonPosition.CenterTop:
					baseX = chartPanel.X + (chartPanel.W - btnW) / 2f;
					baseY = chartPanel.Y + 6f;
					break;
				case ButtonPosition.CenterBottom:
					baseX = chartPanel.X + (chartPanel.W - btnW) / 2f;
					baseY = chartPanel.Y + chartPanel.H - (btnH + pad) * 5 - 10f;
					break;
				default: // NearEntry
					baseX = (float)lineEndX + 6f;
					baseY = (float)entryPoint.Y - btnH - pad;
					break;
			}

			// Rects — order: FLIP(0) CALC(1) TRACK(2) SOURCE(3*) STOP(4*)
			float minBtnW   = 20f;
			minBtnRect      = new SharpDX.RectangleF(baseX, baseY - btnH - pad, minBtnW, btnH);
			flipBtnRect     = new SharpDX.RectangleF(baseX, baseY,                  btnW, btnH);
			calcBtnRect     = new SharpDX.RectangleF(baseX, baseY + (btnH+pad),     btnW, btnH);
			trackBtnRect    = new SharpDX.RectangleF(baseX, baseY + (btnH+pad) * 2, btnW, btnH);
			srcBtnRect      = new SharpDX.RectangleF(baseX, baseY + (btnH+pad) * 3, btnW, btnH);
			stopModeBtnRect = new SharpDX.RectangleF(baseX, baseY + (btnH+pad) * 4, btnW, btnH);
			btnRectsValid   = true;

			float panelH = TrackLastPrice ? (btnH+pad)*5 : (btnH+pad)*3;
			panelRect    = new SharpDX.RectangleF(baseX, baseY, btnW, panelH);

			// Always draw minimize toggle button
			var minColor = new SharpDX.Color4(0.15f, 0.15f, 0.15f, 0.90f);
			string minLabel = isPanelMinimized ? "▶" : "◀";
			using (var minBrush  = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, minColor))
			using (var minWhite  = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, SharpDX.Color4.White))
			using (var minTf     = GetLabelTextFormat())
			using (var minLayout = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory,
				minLabel, minTf, minBtnRect.Width, minBtnRect.Height))
			{
				RenderTarget.FillRectangle(minBtnRect, minBrush);
				RenderTarget.DrawTextLayout(
					new SharpDX.Vector2(minBtnRect.X, minBtnRect.Y + (minBtnRect.Height - minTf.FontSize) / 2f - 1f),
					minLayout, minWhite, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
			}

			if (isPanelMinimized) return;

			// If hover mode is on, only show the full panel when mouse is over the tool
			if (ShowButtonsOnHover && !isHovered) return;

			// Colors
			float alpha    = ButtonOpacity / 100f;
			var flipColor  = new SharpDX.Color4(0.45f, 0.10f, 0.65f, alpha);
			var calcColor  = AutoOrderQty
				? new SharpDX.Color4(0.10f, 0.50f, 0.80f, alpha)
				: new SharpDX.Color4(0.25f, 0.25f, 0.25f, alpha);
			var trackColor = TrackLastPrice
				? new SharpDX.Color4(0.05f, 0.55f, 0.05f, alpha)
				: new SharpDX.Color4(0.25f, 0.25f, 0.25f, alpha);
			var srcColor   = new SharpDX.Color4(0.00f, 0.50f, 0.55f, alpha);
			var stopColor  = (StopMode == StopMode.Hard)
				? new SharpDX.Color4(0.70f, 0.35f, 0.00f, alpha)
				: new SharpDX.Color4(0.10f, 0.40f, 0.70f, alpha);

			string flipLabel      = "⇅ FLIP";
			string calcLabel      = "AUTO CALC: "  + (AutoOrderQty    ? "ON" : "OFF");
			string trackLabel     = "AUTO TRACK: " + (TrackLastPrice   ? "ON" : "OFF");
			string srcLabel       = "SOURCE: "     + PriceSource.ToString().ToUpper();
			string stopModeLabel  = "STOP MODE: "  + (StopMode == StopMode.Flex ? "FLEX" : "HARD");

			using (var flipBrush  = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, flipColor))
			using (var calcBrush  = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, calcColor))
			using (var trackBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, trackColor))
			using (var srcBrush   = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, srcColor))
			using (var stopBrush  = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, stopColor))
			using (var white      = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, SharpDX.Color4.White))
			using (var flipL  = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, flipLabel,     tf, btnW, btnH))
			using (var calcL  = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, calcLabel,     tf, btnW, btnH))
			using (var trackL = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, trackLabel,    tf, btnW, btnH))
			using (var srcL   = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, srcLabel,      tf, btnW, btnH))
			using (var stopL  = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, stopModeLabel, tf, btnW, btnH))
			{
				float ty(SharpDX.RectangleF r) => r.Y + (btnH - tf.FontSize) / 2f - 1f;

				RenderTarget.FillRectangle(flipBtnRect,  flipBrush);
				RenderTarget.DrawTextLayout(new SharpDX.Vector2(baseX, ty(flipBtnRect)),  flipL,  white, SharpDX.Direct2D1.DrawTextOptions.NoSnap);

				RenderTarget.FillRectangle(calcBtnRect,  calcBrush);
				RenderTarget.DrawTextLayout(new SharpDX.Vector2(baseX, ty(calcBtnRect)),  calcL,  white, SharpDX.Direct2D1.DrawTextOptions.NoSnap);

				RenderTarget.FillRectangle(trackBtnRect, trackBrush);
				RenderTarget.DrawTextLayout(new SharpDX.Vector2(baseX, ty(trackBtnRect)), trackL, white, SharpDX.Direct2D1.DrawTextOptions.NoSnap);

				if (TrackLastPrice)
				{
					RenderTarget.FillRectangle(srcBtnRect,      srcBrush);
					RenderTarget.DrawTextLayout(new SharpDX.Vector2(baseX, ty(srcBtnRect)),      srcL,  white, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
					RenderTarget.FillRectangle(stopModeBtnRect, stopBrush);
					RenderTarget.DrawTextLayout(new SharpDX.Vector2(baseX, ty(stopModeBtnRect)), stopL, white, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
				}
			}
		}

		// ── Plus: sync contracts to Chart Trader Order Qty ────────────────────────
		private int    lastQtySet       = 0;
		private ChartControl lastCC     = null;

		// ── PRO mode tracking state ───────────────────────────────────────────────
		private bool   userOverrideTracking = false;  // true when user manually drags entry

		// ── On-chart button rects ─────────────────────────────────────────────────
		private SharpDX.RectangleF flipBtnRect      = new SharpDX.RectangleF();
		private SharpDX.RectangleF calcBtnRect      = new SharpDX.RectangleF();
		private SharpDX.RectangleF trackBtnRect     = new SharpDX.RectangleF();
		private SharpDX.RectangleF srcBtnRect       = new SharpDX.RectangleF();
		private SharpDX.RectangleF stopModeBtnRect  = new SharpDX.RectangleF();
		private bool               btnRectsValid    = false;
		private SharpDX.RectangleF panelRect        = new SharpDX.RectangleF();
		private SharpDX.RectangleF minBtnRect       = new SharpDX.RectangleF();
		private bool               isPanelMinimized = true;
		private bool               isHovered        = false;

		private void UpdateOrderQty(ChartControl chartControl)
		{
			if (!AutoOrderQty) return;
			if (AttachedTo == null || EntryAnchor == null || RiskAnchor == null) return;
			lastCC = chartControl;
			double entry  = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(EntryAnchor.Price);
			double stop   = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(RiskAnchor.Price);
			double pv     = AttachedTo.Instrument.MasterInstrument.PointValue;
			double rpc    = Math.Abs((entry - stop) * pv);
			if (rpc <= 0) return;
			int qty = (int)Math.Round(StopLoss / rpc, MidpointRounding.AwayFromZero);
			qty = Math.Max(1, Math.Min(qty, MaxContracts));
			if (qty == lastQtySet) return;
			lastQtySet = qty;
			try
			{
				int q = qty;
				chartControl.Dispatcher.InvokeAsync((Action)(() =>
				{
					try
					{
						var qs = Window.GetWindow(chartControl.Parent)
								.FindFirst("ChartTraderControlQuantitySelector")
								 as NinjaTrader.Gui.Tools.QuantityUpDown;
						if (qs != null) qs.Value = q;
					}
					catch { }
				}));
			}
			catch { }
		}

		public override void OnMouseMove(ChartControl chartControl, ChartPanel chartPanel,
			ChartScale chartScale, ChartAnchor dataPoint)
		{
			if (IsLocked && DrawingState != DrawingState.Building || !IsVisible)
				return;

			if (DrawingState == DrawingState.Building)
			{
				if      (EntryAnchor.IsEditing)  dataPoint.CopyDataValues(EntryAnchor);
				else if (RiskAnchor.IsEditing)   dataPoint.CopyDataValues(RiskAnchor);
				else if (RewardAnchor.IsEditing) dataPoint.CopyDataValues(RewardAnchor);
			}
			else if (DrawingState == DrawingState.Editing && editingAnchor != null)
			{
				dataPoint.CopyDataValues(editingAnchor);
				if (editingAnchor != EntryAnchor)
				{
					if (editingAnchor != RewardAnchor && ProfitRatio.ApproxCompare(0) != 0)
						SetReward();
					else if (ProfitRatio.ApproxCompare(0) != 0)
						SetRisk();
				}
			}
			else if (DrawingState == DrawingState.Moving)
			{
				foreach (ChartAnchor anchor in Anchors)
					anchor.MoveAnchor(InitialMouseDownAnchor, dataPoint, chartControl, chartPanel, chartScale, this);
			}

			entryPrice  = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(EntryAnchor.Price);
			stopPrice   = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(RiskAnchor.Price);
			targetPrice = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(RewardAnchor.Price);
			ChangeVersion++;
			UpdateOrderQty(chartControl);
		}

		public override void OnMouseUp(ChartControl chartControl, ChartPanel chartPanel,
			ChartScale chartScale, ChartAnchor dataPoint)
		{
			if (DrawingState == DrawingState.Building) return;

			if (DrawingState == DrawingState.Editing || DrawingState == DrawingState.Moving)
				DrawingState = DrawingState.Normal;

			if (editingAnchor != null)
			{
				if (editingAnchor == EntryAnchor)
				{
					SetReward();
					if (ProfitRatio.ApproxCompare(0) != 0)
						SetRisk();
				}
				editingAnchor.IsEditing = false;
			}
			editingAnchor = null;
			ChangeVersion++;
		}

		// ═══════════════════════════════════════════════════════════════════════════
		//  RENDER
		// ═══════════════════════════════════════════════════════════════════════════

		public override void OnRender(ChartControl chartControl, ChartScale chartScale)
		{
			if (!IsVisible) return;
			if (Anchors.All(a => a.IsEditing)) return;

			if (needsRatioUpdate && DrawTarget)
				SetReward();

			// ── PRO: Track last price ─────────────────────────────────────────────

			// FIX v1.6: StopTrackOnFill — re-arm check runs BEFORE the main
			// tracking guard so it can detect position-closed while
			// userOverrideTracking is still true and TrackLastPrice is false.
			if (StopTrackOnFill && userOverrideTracking && !TrackLastPrice
				&& AttachedTo != null
				&& DrawingState == DrawingState.Normal)
			{
				try
				{
					var acct = Account.All.FirstOrDefault(a => a.ConnectionStatus == ConnectionStatus.Connected);
					if (acct != null)
					{
						var pos = acct.Positions.FirstOrDefault(p =>
							p.Instrument == AttachedTo.Instrument && p.Quantity > 0);
						if (pos == null)
						{
							// Position closed (flat) — re-arm tracking automatically
							userOverrideTracking = false;
							TrackLastPrice       = true;
						}
					}
				}
				catch { }
			}

			if (TrackLastPrice && !userOverrideTracking
				&& DrawingState == DrawingState.Normal
				&& AttachedTo != null && chartControl.BarsArray != null
				&& chartControl.BarsArray.Count > 0)
			{
				// FIX v1.6: StopTrackOnFill — disable tracking when position opens
				if (StopTrackOnFill && AttachedTo != null)
				{
					try
					{
						var acct = Account.All.FirstOrDefault(a => a.ConnectionStatus == ConnectionStatus.Connected);
						if (acct != null)
						{
							var pos = acct.Positions.FirstOrDefault(p =>
								p.Instrument == AttachedTo.Instrument && p.Quantity > 0);
							if (pos != null)
							{
								// Position is open — disable tracking
								userOverrideTracking = true;
								TrackLastPrice       = false;
							}
						}
					}
					catch { }
				}

				// FIX v1.6: skip price-tracking on the same frame that
				// StopTrackOnFill just disabled it (TrackLastPrice is now false)
				if (TrackLastPrice)
				{
					var    _bars     = chartControl.BarsArray[0].Bars;
					int    _lastIdx  = _bars.Count - 1;
					double lastPrice;

					// FIX v1.2: added explicit case for PriceSource.LastPrice instead of
					// relying on the default fall-through
					switch (PriceSource)
					{
						case PriceSource.LastPrice: lastPrice = _bars.GetClose(_lastIdx); break;
						case PriceSource.Close:     lastPrice = _bars.GetClose(_lastIdx); break;
						case PriceSource.High:      lastPrice = _bars.GetHigh(_lastIdx);  break;
						case PriceSource.Low:       lastPrice = _bars.GetLow(_lastIdx);   break;
						case PriceSource.Open:      lastPrice = _bars.GetOpen(_lastIdx);  break;
						default:                    lastPrice = _bars.GetClose(_lastIdx); break;
					}

					if (lastPrice > 0)
					{
						lastPrice = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(lastPrice);
						if (StopMode == StopMode.Hard)
						{
							// HARD: move SL with entry to keep fixed tick distance
							double slDistance    = RiskAnchor.Price - EntryAnchor.Price;
							RiskAnchor.Price     = AttachedTo.Instrument.MasterInstrument
													.RoundToTickSize(lastPrice + slDistance);
						}
						EntryAnchor.Price = lastPrice;
						SetReward();       // recalculate TP from new entry in both modes
						ChangeVersion++;
						UpdateOrderQty(chartControl); // keep Chart Trader qty in sync
					}
				}
			}

			ChartPanel chartPanel = chartControl.ChartPanels[PanelIndex];
			Point entryPoint      = EntryAnchor.GetPoint(chartControl, chartPanel, chartScale);
			Point stopPoint       = RiskAnchor.GetPoint(chartControl, chartPanel, chartScale);
			Point targetPoint     = RewardAnchor.GetPoint(chartControl, chartPanel, chartScale);

			AnchorLineStroke.RenderTarget        = RenderTarget;
			EntryLineStroke.RenderTarget         = RenderTarget;
			StopLineStroke.RenderTarget          = RenderTarget;
			TargetLineStroke.RenderTarget        = RenderTarget;
			TargetLineStrokeBack.RenderTarget    = RenderTarget;
			StopLineStrokeBack.RenderTarget      = RenderTarget;
			PartialStopLineStroke.RenderTarget   = RenderTarget;

			RenderTarget.AntialiasMode = SharpDX.Direct2D1.AntialiasMode.PerPrimitive;
			RenderTarget.DrawLine(entryPoint.ToVector2(), stopPoint.ToVector2(),
				AnchorLineStroke.BrushDX, AnchorLineStroke.Width, AnchorLineStroke.StrokeStyle);

			double anchorMinX = DrawTarget
				? new[] { entryPoint.X, stopPoint.X, targetPoint.X }.Min()
				: new[] { entryPoint.X, stopPoint.X }.Min();
			double anchorMaxX = DrawTarget
				? new[] { entryPoint.X, stopPoint.X, targetPoint.X }.Max()
				: new[] { entryPoint.X, stopPoint.X }.Max();
			double lineStartX = IsExtendedLinesLeft  ? chartPanel.X : anchorMinX;
			double lineEndX   = IsExtendedLinesRight ? chartPanel.X + chartPanel.W : anchorMaxX;

			// ── TP interpolated points (for partial TP lines) ─────────────────────
			double distance              = Math.Sqrt(Math.Pow(targetPoint.X - entryPoint.X, 2) + Math.Pow(targetPoint.Y - entryPoint.Y, 2));
			int    numTPInterpolated     = (int)profitRatio;
			double segmentLengthTP       = distance / numTPInterpolated;
			// risk = one R unit in price — same value SetReward() uses
			double riskPerUnit           = entryPrice - stopPrice;

			List<Point>  tpInterpolatedPoints = new List<Point>();
			List<double> tpInterpolatedPrices = new List<double>(); // price-space — exact R-multiples
			tpInterpolatedPoints.Add(entryPoint);
			tpInterpolatedPrices.Add(entryPrice);

			for (int i = 1; i < numTPInterpolated; i++)
			{
				double fraction      = i * segmentLengthTP / distance;
				double interpolatedX = entryPoint.X + fraction * (targetPoint.X - entryPoint.X);
				double interpolatedY = entryPoint.Y + fraction * (targetPoint.Y - entryPoint.Y);
				tpInterpolatedPoints.Add(new Point((int)interpolatedX, (int)interpolatedY));
				// Price = entry + i * 1R — mirrors SetReward() exactly, no rounding drift
				double partialPrice  = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(
					entryPrice + i * riskPerUnit);
				tpInterpolatedPrices.Add(partialPrice);
			}
			tpInterpolatedPoints.Add(targetPoint);
			tpInterpolatedPrices.Add(targetPrice);

			// ── Stop interpolated points (partial SL lines) ───────────────────────
			// We divide the distance from entry to main stop into StopRatio equal segments.
			// Level i (1-based) is drawn at i/StopRatio of the way from entry to stop.
			int    numSLLevels    = (int)stopRatio;
			double stopDistanceY  = stopPoint.Y - entryPoint.Y;  // positive = stop is below entry (price axis inverted on screen)
			double stopDistanceX  = stopPoint.X - entryPoint.X;

			List<Point> slInterpolatedPoints = new List<Point>();
			for (int i = 1; i <= numSLLevels; i++)
			{
				double fraction      = (double)i / numSLLevels;
				double interpolatedX = entryPoint.X + fraction * stopDistanceX;
				double interpolatedY = entryPoint.Y + fraction * stopDistanceY;
				slInterpolatedPoints.Add(new Point((int)interpolatedX, (int)interpolatedY));
			}

			SharpDX.Direct2D1.Brush tmpBrush = IsInHitTest ? chartControl.SelectionBrush : AnchorLineStroke.BrushDX;

			if (DrawTarget)
			{
				// Anchor line: entry → target
				RenderTarget.DrawLine(entryPoint.ToVector2(), targetPoint.ToVector2(),
					tmpBrush, AnchorLineStroke.Width, AnchorLineStroke.StrokeStyle);

				// ── Target (TP) line ──────────────────────────────────────────────
				TargetLineStroke.RenderTarget = RenderTarget;
				SharpDX.Vector2 targetStartVector = new SharpDX.Vector2((float)lineStartX, (float)targetPoint.Y);
				SharpDX.Vector2 targetEndVector   = new SharpDX.Vector2((float)lineEndX,   (float)targetPoint.Y);

				tmpBrush = IsInHitTest ? chartControl.SelectionBrush : TargetLineStroke.BrushDX;
				RenderTarget.DrawLine(targetStartVector, targetEndVector,
					tmpBrush, TargetLineStroke.Width, TargetLineStroke.StrokeStyle);

				if (!partialFlag)
					DrawPriceTextPartials(RewardAnchor, targetPoint, targetPrice,
						chartControl, chartPanel, chartScale, (int)profitRatio);

				// ── Entry line ────────────────────────────────────────────────────
				SharpDX.Vector2 entryStartVector = new SharpDX.Vector2((float)lineStartX, (float)entryPoint.Y);
				SharpDX.Vector2 entryEndVector   = new SharpDX.Vector2((float)lineEndX,   (float)entryPoint.Y);

				tmpBrush = IsInHitTest ? chartControl.SelectionBrush : EntryLineStroke.BrushDX;
				RenderTarget.DrawLine(entryStartVector, entryEndVector,
					tmpBrush, EntryLineStroke.Width, EntryLineStroke.StrokeStyle);
				DrawPriceText(EntryAnchor, entryPoint, entryPrice, chartControl, chartPanel, chartScale);
				DrawToggleButtons(chartControl, chartPanel, chartScale, entryPoint, lineEndX);
				DrawAnchorCircle((float)entryPoint.X,  (float)entryPoint.Y,  EntryLineStroke.BrushDX);
				DrawAnchorCircle((float)targetPoint.X, (float)targetPoint.Y, TargetLineStroke.BrushDX);

				// ── Main stop (SL) line ───────────────────────────────────────────
				SharpDX.Vector2 stopStartVector = new SharpDX.Vector2((float)lineStartX, (float)stopPoint.Y);
				SharpDX.Vector2 stopEndVector   = new SharpDX.Vector2((float)lineEndX,   (float)stopPoint.Y);

				tmpBrush = IsInHitTest ? chartControl.SelectionBrush : StopLineStroke.BrushDX;
				RenderTarget.DrawLine(stopStartVector, stopEndVector,
					tmpBrush, StopLineStroke.Width, StopLineStroke.StrokeStyle);
				// FSL label is drawn by the partial SL loop on the last level
				DrawAnchorCircle((float)stopPoint.X, (float)stopPoint.Y, StopLineStroke.BrushDX);

				// ── Shaded rectangles ─────────────────────────────────────────────
				SharpDX.RectangleF stopRectangle = new SharpDX.RectangleF(
					stopStartVector.X, entryStartVector.Y,
					stopEndVector.X - entryStartVector.X,
					stopEndVector.Y  - entryEndVector.Y);
				SharpDX.RectangleF targetRectangle = new SharpDX.RectangleF(
					targetStartVector.X, entryStartVector.Y,
					targetEndVector.X - entryStartVector.X,
					targetEndVector.Y - entryStartVector.Y);

				RenderTarget.FillRectangle(stopRectangle,   StopLineStrokeBack.BrushDX);
				RenderTarget.FillRectangle(targetRectangle, TargetLineStrokeBack.BrushDX);

				// ── Partial TP lines ──────────────────────────────────────────────
				if (partialFlag)
				{
					for (int i = 1; i < tpInterpolatedPoints.Count; i++)
					{
						SharpDX.Vector2 partialStartVector = new SharpDX.Vector2((float)lineStartX, (float)tpInterpolatedPoints[i].Y);
						SharpDX.Vector2 partialEndVector   = new SharpDX.Vector2((float)lineEndX,   (float)tpInterpolatedPoints[i].Y);
						tmpBrush = IsInHitTest ? chartControl.SelectionBrush : TargetLineStroke.BrushDX;
						RenderTarget.DrawLine(partialStartVector, partialEndVector,
							tmpBrush, TargetLineStroke.Width, TargetLineStroke.StrokeStyle);
						// Use price-space price — exact, no pixel rounding error
						DrawPriceTextPartials(RewardAnchor, tpInterpolatedPoints[i], tpInterpolatedPrices[i],
							chartControl, chartPanel, chartScale, i);
					}
				}

				// ── Partial SL lines ──────────────────────────────────────────────
				// Draw SL1 … SL(n-1) between entry and main stop.
				// The last level (SLn) coincides with the main stop anchor and is
				// already drawn above, so we draw all levels including the last to
				// show consistent labels, but skip re-drawing the physical line for
				// the last one to avoid overdrawing.
				for (int i = 0; i < slInterpolatedPoints.Count; i++)
				{
					int    levelNumber     = i + 1;                          // 1-based label
					Point  slPoint         = slInterpolatedPoints[i];
					bool   isLastLevel     = (levelNumber == numSLLevels);   // coincides with main stop

					// Draw the dashed partial stop line (skip if it's the main stop line, already drawn)
					if (!isLastLevel)
					{
						SharpDX.Vector2 slStartVector = new SharpDX.Vector2((float)lineStartX, (float)slPoint.Y);
						SharpDX.Vector2 slEndVector   = new SharpDX.Vector2((float)lineEndX,   (float)slPoint.Y);
						tmpBrush = IsInHitTest ? chartControl.SelectionBrush : PartialStopLineStroke.BrushDX;
						RenderTarget.DrawLine(slStartVector, slEndVector,
							tmpBrush, PartialStopLineStroke.Width, PartialStopLineStroke.StrokeStyle);
					}

					// Compute the actual price at this interpolated screen Y
					double slPrice = chartScale.GetValueByY((float)slPoint.Y);
					slPrice = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(slPrice);

					// Last level = main stop → draw FSL label; others draw SLn label
					if (isLastLevel)
						DrawPriceText(RiskAnchor, slPoint, slPrice, chartControl, chartPanel, chartScale);
					else
						DrawPartialStopText(slPoint, slPrice, levelNumber,
							chartControl, chartPanel, chartScale);
				}
			}
		}

		// ═══════════════════════════════════════════════════════════════════════════
		//  STATE
		// ═══════════════════════════════════════════════════════════════════════════

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description              = "Advanced Risk Reward By Luis Garrido";
				Name                     = "Advanced Risk Reward V1 By Luis Garrido";
				ProfitRatio              = 3;
				StopRatio                = 2;
				StopLoss                 = 500;
				AnchorLineStroke         = new Stroke(Brushes.DarkGray,   DashStyleHelper.Solid, 1f, 50);
				EntryLineStroke          = new Stroke(Brushes.Goldenrod,  DashStyleHelper.Solid, 2f);
				StopLineStroke           = new Stroke(Brushes.Crimson,    DashStyleHelper.Solid, 2f);
				TargetLineStroke         = new Stroke(Brushes.SeaGreen,   DashStyleHelper.Solid, 2f);
				StopLineStrokeBack       = new Stroke(Brushes.Crimson,    DashStyleHelper.Solid, 2f, 20);
				TargetLineStrokeBack     = new Stroke(Brushes.SeaGreen,   DashStyleHelper.Solid, 2f, 20);
				// Partial stop lines: red, dashed so they are visually distinct from the main stop
				PartialStopLineStroke    = new Stroke(Brushes.Crimson,    DashStyleHelper.Dash,  1f);
				EntryAnchor              = new ChartAnchor { IsEditing = true, DrawingTool = this };
				RiskAnchor               = new ChartAnchor { IsEditing = true, DrawingTool = this };
				RewardAnchor             = new ChartAnchor { IsEditing = true, DrawingTool = this };
				EntryAnchor.DisplayName  = Custom.Resource.NinjaScriptDrawingToolRiskRewardAnchorEntry;
				RiskAnchor.DisplayName   = Custom.Resource.NinjaScriptDrawingToolRiskRewardAnchorRisk;
				RewardAnchor.DisplayName = Custom.Resource.NinjaScriptDrawingToolRiskRewardAnchorReward;
				partialFlag              = true;
				TpShowCurrency           = true;
				TpShowTicks              = false;
				TpShowPoints             = false;
				TpShowPercent            = false;
				SlShowCurrency           = true;
				SlShowTicks              = false;
				SlShowPoints             = false;
				SlShowPercent            = false;
				AutoOrderQty             = false;
				MaxContracts             = 10;
				TrackLastPrice           = false;
				StopMode                 = StopMode.Flex;
				PriceSource              = PriceSource.LastPrice;
				StopTrackOnFill          = true;
				ButtonPosition           = ButtonPosition.NearEntry;
				ButtonOpacity            = 90;
				ShowButtonsOnHover       = false;
				LabelFontSize            = 11;
			}
			else if (State == State.Terminated)
			{
				if (AutoOrderQty && lastCC != null)
				{
					try
					{
						ChartControl cc = lastCC;
						cc.Dispatcher.InvokeAsync((Action)(() =>
						{
							try
							{
								var qs = Window.GetWindow(cc.Parent)
										.FindFirst("ChartTraderControlQuantitySelector")
										 as NinjaTrader.Gui.Tools.QuantityUpDown;
								if (qs != null) qs.Value = 1;
							}
							catch { }
						}));
					}
					catch { }
				}
				Dispose();
			}
		}

		// ═══════════════════════════════════════════════════════════════════════════
		//  REWARD / RISK CALCULATION
		// ═══════════════════════════════════════════════════════════════════════════

		[EditorBrowsable(EditorBrowsableState.Never)]
		public void SetReward()
		{
			if (Anchors == null || AttachedTo == null) return;

			entryPrice          = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(EntryAnchor.Price);
			stopPrice           = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(RiskAnchor.Price);
			risk                = entryPrice - stopPrice;
			reward              = risk * ProfitRatio;
			targetPrice         = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(entryPrice + reward);
			RewardAnchor.Price  = targetPrice;
			RewardAnchor.IsEditing = false;
			needsRatioUpdate    = false;
		}

		[EditorBrowsable(EditorBrowsableState.Never)]
		public void SetRisk()
		{
			if (Anchors == null || AttachedTo == null) return;

			entryPrice          = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(EntryAnchor.Price);
			targetPrice         = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(RewardAnchor.Price);
			reward              = targetPrice - entryPrice;
			risk                = reward / ProfitRatio;
			stopPrice           = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(entryPrice - risk);
			RiskAnchor.Price    = stopPrice;
			RiskAnchor.IsEditing = false;
			needsRatioUpdate    = false;
		}

		// ═══════════════════════════════════════════════════════════════════════════
		//  PUBLIC ACCESSORS FOR STRATEGY INTEGRATION  (v1.4)
		//
		//  These methods expose the same math used in OnRender and UpdateOrderQty
		//  so that DrawingToolSyncStrategy can read TP/SL prices and contract qty
		//  without duplicating calculations. All prices are rounded to tick size.
		// ═══════════════════════════════════════════════════════════════════════════

		/// <summary>
		/// Returns the entry anchor price, rounded to tick size.
		/// </summary>
		public double GetEntryPrice()
		{
			if (AttachedTo == null || EntryAnchor == null) return 0;
			return AttachedTo.Instrument.MasterInstrument.RoundToTickSize(EntryAnchor.Price);
		}

		/// <summary>
		/// Returns the FSL (Final Stop Loss) anchor price, rounded to tick size.
		/// </summary>
		public double GetStopPrice()
		{
			if (AttachedTo == null || RiskAnchor == null) return 0;
			return AttachedTo.Instrument.MasterInstrument.RoundToTickSize(RiskAnchor.Price);
		}

		/// <summary>
		/// Returns the final TP anchor price, rounded to tick size.
		/// </summary>
		public double GetFinalTPPrice()
		{
			if (AttachedTo == null || RewardAnchor == null) return 0;
			return AttachedTo.Instrument.MasterInstrument.RoundToTickSize(RewardAnchor.Price);
		}

		/// <summary>
		/// Returns true if this is a long trade (entry > stop), false for short.
		/// </summary>
		public bool IsLongTrade()
		{
			return GetEntryPrice() > GetStopPrice();
		}

		/// <summary>
		/// Returns all Take Profit prices as an array, from closest to entry
		/// through to the final TP. Includes partial TP levels and final TP.
		///
		/// The math mirrors OnRender exactly:
		///   partialPrice = entryPrice + i * riskPerUnit
		///   where riskPerUnit = entryPrice - stopPrice (signed; positive for longs)
		///
		/// For a ProfitRatio of 3 with a long trade:
		///   [0] = entry + 1R  (partial TP1)
		///   [1] = entry + 2R  (partial TP2)
		///   [2] = entry + 3R  (final TP = RewardAnchor)
		/// </summary>
		public double[] GetTPPrices()
		{
			if (AttachedTo == null || EntryAnchor == null || RiskAnchor == null || RewardAnchor == null)
				return new double[0];

			double entry = GetEntryPrice();
			double stop  = GetStopPrice();
			double finalTP = GetFinalTPPrice();
			int numTP = (int)profitRatio;
			if (numTP < 1) return new double[] { finalTP };

			double riskPerUnit = entry - stop; // positive for longs, negative for shorts
			var prices = new System.Collections.Generic.List<double>();

			// Partial TP levels: i = 1 to numTP-1
			for (int i = 1; i < numTP; i++)
			{
				double p = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(
					entry + i * riskPerUnit);
				prices.Add(p);
			}

			// Final TP (= RewardAnchor price)
			prices.Add(finalTP);

			return prices.ToArray();
		}

		/// <summary>
		/// Returns all Stop Loss level prices as an array, from closest to entry
		/// through to the FSL. The math mirrors OnRender exactly:
		///   slPrice = entry + fraction * (stop - entry)
		///   where fraction = i / numSLLevels
		///
		/// For a StopRatio of 2 with a long trade (entry=100, stop=90):
		///   [0] = 95  (partial SL at 50%)
		///   [1] = 90  (FSL = RiskAnchor)
		/// </summary>
		public double[] GetSLPrices()
		{
			if (AttachedTo == null || EntryAnchor == null || RiskAnchor == null)
				return new double[0];

			double entry = GetEntryPrice();
			double stop  = GetStopPrice();
			int numSL = (int)stopRatio;
			if (numSL < 1) return new double[] { stop };

			var prices = new System.Collections.Generic.List<double>();

			for (int i = 1; i <= numSL; i++)
			{
				double fraction = (double)i / numSL;
				double p = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(
					entry + fraction * (stop - entry));
				prices.Add(p);
			}

			return prices.ToArray();
		}

		/// <summary>
		/// Returns the dollar risk per single contract at the FSL distance.
		/// This is: |entryPrice - stopPrice| * PointValue
		/// </summary>
		public double GetRiskPerContract()
		{
			if (AttachedTo == null) return 0;
			double entry = GetEntryPrice();
			double stop  = GetStopPrice();
			double pv    = AttachedTo.Instrument.MasterInstrument.PointValue;
			return Math.Abs((entry - stop) * pv);
		}

		/// <summary>
		/// Returns the calculated contract quantity based on the Auto Order Qty
		/// logic: qty = round(StopLoss / riskPerContract), clamped [1, MaxContracts].
		/// This is the same math used in UpdateOrderQty().
		/// </summary>
		public int GetCalculatedQty()
		{
			double rpc = GetRiskPerContract();
			if (rpc <= 0) return 1;
			int qty = (int)Math.Round(StopLoss / rpc, MidpointRounding.AwayFromZero);
			return Math.Max(1, Math.Min(qty, MaxContracts));
		}
	}

	// ═══════════════════════════════════════════════════════════════════════════════
	//  STATIC DRAW HELPERS  (mirrors original pattern)
	// ═══════════════════════════════════════════════════════════════════════════════
	public static partial class Draw
	{
		private static AdvancedRiskReward_LuisGarrido AdvancedRiskReward_LuisGarrido(NinjaScriptBase owner, string tag,
			bool isAutoScale,
			int entryBarsAgo, DateTime entryTime, double entryY,
			int stopBarsAgo,  DateTime stopTime,  double stopY,
			int targetBarsAgo,DateTime targetTime,double targetY,
			double profitRatio, double stopRatio,
			bool isStop, bool isGlobal, string templateName)
		{
			if (owner == null)
				throw new ArgumentException("owner");
			if (entryBarsAgo == int.MinValue && entryTime == Core.Globals.MinDate)
				throw new ArgumentException("entry value required");
			if (stopBarsAgo  == int.MinValue && stopTime  == Core.Globals.MinDate &&
				targetBarsAgo == int.MinValue && targetTime == Core.Globals.MinDate)
				throw new ArgumentException("a stop or target value is required");

			if (isGlobal && tag[0] != GlobalDrawingToolManager.GlobalDrawingToolTagPrefix)
				tag = string.Format("{0}{1}", GlobalDrawingToolManager.GlobalDrawingToolTagPrefix, tag);

			AdvancedRiskReward_LuisGarrido tool = DrawingTool.GetByTagOrNew(owner,
				typeof(AdvancedRiskReward_LuisGarrido), tag, templateName) as AdvancedRiskReward_LuisGarrido;

			if (tool == null) return null;

			DrawingTool.SetDrawingToolCommonValues(tool, tag, isAutoScale, owner, isGlobal);

			ChartAnchor entryAnchor = DrawingTool.CreateChartAnchor(owner, entryBarsAgo, entryTime, entryY);

			tool.ProfitRatio = profitRatio;
			tool.StopRatio   = stopRatio;

			if (isStop)
			{
				ChartAnchor stopAnchor = DrawingTool.CreateChartAnchor(owner, stopBarsAgo, stopTime, stopY);
				entryAnchor.CopyDataValues(tool.EntryAnchor);
				entryAnchor.CopyDataValues(tool.RewardAnchor);
				stopAnchor.CopyDataValues(tool.RiskAnchor);
				tool.SetReward();
			}
			else
			{
				ChartAnchor targetAnchor = DrawingTool.CreateChartAnchor(owner, targetBarsAgo, targetTime, targetY);
				entryAnchor.CopyDataValues(tool.EntryAnchor);
				entryAnchor.CopyDataValues(tool.RiskAnchor);
				targetAnchor.CopyDataValues(tool.RewardAnchor);
				tool.SetRisk();
			}

			tool.SetState(State.Active);
			return tool;
		}

		public static AdvancedRiskReward_LuisGarrido AdvancedRiskReward_LuisGarrido(NinjaScriptBase owner, string tag,
			bool isAutoScale, DateTime entryTime, double entryY,
			DateTime endTime, double endY, double profitRatio, double stopRatio, bool isStop)
		{
			return isStop
				? AdvancedRiskReward_LuisGarrido(owner, tag, isAutoScale,
					int.MinValue, entryTime, entryY,
					int.MinValue, endTime, endY,
					0, Core.Globals.MinDate, 0,
					profitRatio, stopRatio, true, false, null)
				: AdvancedRiskReward_LuisGarrido(owner, tag, isAutoScale,
					int.MinValue, entryTime, entryY,
					0, Core.Globals.MinDate, 0,
					int.MinValue, endTime, endY,
					profitRatio, stopRatio, false, false, null);
		}

		public static AdvancedRiskReward_LuisGarrido AdvancedRiskReward_LuisGarrido(NinjaScriptBase owner, string tag,
			bool isAutoScale, int entryBarsAgo, double entryY,
			int endBarsAgo, double endY, double profitRatio, double stopRatio, bool isStop)
		{
			return isStop
				? AdvancedRiskReward_LuisGarrido(owner, tag, isAutoScale,
					entryBarsAgo, Core.Globals.MinDate, entryY,
					endBarsAgo,   Core.Globals.MinDate, endY,
					0, Core.Globals.MinDate, 0,
					profitRatio, stopRatio, true, false, null)
				: AdvancedRiskReward_LuisGarrido(owner, tag, isAutoScale,
					entryBarsAgo, Core.Globals.MinDate, entryY,
					0, Core.Globals.MinDate, 0,
					endBarsAgo,   Core.Globals.MinDate, endY,
					profitRatio, stopRatio, false, false, null);
		}

		public static AdvancedRiskReward_LuisGarrido AdvancedRiskReward_LuisGarrido(NinjaScriptBase owner, string tag,
			bool isAutoScale, DateTime entryTime, double entryY,
			DateTime endTime, double endY, double profitRatio, double stopRatio,
			bool isStop, bool isGlobal, string templateName)
		{
			return isStop
				? AdvancedRiskReward_LuisGarrido(owner, tag, isAutoScale,
					int.MinValue, entryTime, entryY,
					int.MinValue, endTime, endY,
					0, Core.Globals.MinDate, 0,
					profitRatio, stopRatio, true, isGlobal, templateName)
				: AdvancedRiskReward_LuisGarrido(owner, tag, isAutoScale,
					int.MinValue, entryTime, entryY,
					0, Core.Globals.MinDate, 0,
					int.MinValue, endTime, endY,
					profitRatio, stopRatio, false, isGlobal, templateName);
		}

		public static AdvancedRiskReward_LuisGarrido AdvancedRiskReward_LuisGarrido(NinjaScriptBase owner, string tag,
			bool isAutoScale, int entryBarsAgo, double entryY,
			int endBarsAgo, double endY, double profitRatio, double stopRatio,
			bool isStop, bool isGlobal, string templateName)
		{
			return isStop
				? AdvancedRiskReward_LuisGarrido(owner, tag, isAutoScale,
					entryBarsAgo, Core.Globals.MinDate, entryY,
					endBarsAgo,   Core.Globals.MinDate, endY,
					0, Core.Globals.MinDate, 0,
					profitRatio, stopRatio, true, isGlobal, templateName)
				: AdvancedRiskReward_LuisGarrido(owner, tag, isAutoScale,
					entryBarsAgo, Core.Globals.MinDate, entryY,
					0, Core.Globals.MinDate, 0,
					endBarsAgo,   Core.Globals.MinDate, endY,
					profitRatio, stopRatio, false, isGlobal, templateName);
		}
	}
}```

---

## 4. Drawing Tool Changelog

The changelog below mirrors the canonical changelog embedded in the file header, presented here in a structured format for easy reference.

### v1.6 — 2026-04-08 *(Current)*

**Bug Fixes:**
- **StopTrackOnFill re-arm:** After a fill disabled tracking, `userOverrideTracking` stayed `true` and `TrackLastPrice` stayed `false` permanently until the user manually clicked AUTO TRACK again. Fixed: when `StopTrackOnFill` is enabled and the account returns to flat (no position on the instrument), both flags now reset automatically so tracking resumes for the next trade setup.
- **Extra render frame after fill:** The price tracking code block was executing one extra render frame after fill detection. The tracking block is now guarded so it skips immediately on the same frame that `StopTrackOnFill` disables tracking.

---

### v1.5 — 2026-04-03

**Bug Fix:**
- **ChangeVersion deserialization crash:** `ChangeVersion` property had `{ get; private set; }`. NT8's XML deserializer requires a public setter on all serialized properties. `private set` caused a "Cannot deserialize" error when saving or loading drawing tool templates. Fixed to `{ get; set; }`.

---

### v1.4 — 2026-04-03

**New Features:**
- Added complete public accessor method API for `DrawingToolSyncStrategy` integration:
  - `GetTPPrices()` — all TP prices (partial + final)
  - `GetSLPrices()` — all SL prices (partial + FSL)
  - `GetEntryPrice()` — entry anchor rounded to tick size
  - `GetStopPrice()` — FSL anchor rounded to tick size
  - `GetFinalTPPrice()` — final TP anchor rounded to tick size
  - `IsLongTrade()` — direction boolean
  - `GetCalculatedQty()` — contract qty from Auto Order Qty logic
  - `GetRiskPerContract()` — dollar risk per single contract
- All methods replicate the math already used in `OnRender` and `UpdateOrderQty`, ensuring the strategy reads identical values to what is displayed on chart

---

### v1.3 — 2026-04-02

**New Features:**
- Added public `ChangeVersion` property — a monotonically increasing counter that increments on every anchor move (drag, flip, auto-track update). Used by `DrawingToolSyncStrategy` for efficient change detection polling without needing to compare prices on every tick.

---

### v1.2 — 2026-03-24

**Bug Fixes:**
- **DirectX resource leak:** `TextLayout` and `TextFormat` DirectX objects in `DrawPriceText`, `DrawPriceTextPartials`, and `DrawPartialStopText` were not disposed after each render frame. Wrapped in `using` blocks to prevent unmanaged resource leak on every chart update.
- **PriceSource.LastPrice silent fallthrough:** `PriceSource.LastPrice` had no explicit case in the switch statement and silently fell through to `default`. Added explicit case for safety against future enum additions.
- **Divide-by-zero guard:** Contract calculation in `GetPriceString` could divide by zero if `PointValue` is zero. Added denominator check.
- **Mis-indented `StopTrackOnFill` block:** The block in `OnRender` appeared to be at outer scope rather than nested inside the `TrackLastPrice` guard block due to incorrect indentation.

---

### v1.1 — 2026-03-20

**Bug Fixes:**
- **IsVisibleOnChart (All Charts scroll):** Drawing tool was disappearing in "All Charts" mode when scrolling past all anchor points. `IsVisibleOnChart` now correctly keeps the tool visible as long as extended lines reach the visible window.
- **Button hover stability:** Buttons were disappearing when moving mouse from a tool line onto the button panel. Hover state now correctly maintained over the buttons themselves.
- **Partial TP label price accuracy:** Partial TP labels showed Ticks/Points/Currency of the full target distance on every level. Each label now uses its own interpolated price calculated in price space (not pixel space), so TP1 matches exactly 1R and TP2 = 2R, etc.
- **Tick rounding drift on partial TP prices:** Partial TP prices were still 1 tick off due to floating-point rounding when interpolating from entry→target. Prices are now computed as `entry + i × riskPerUnit` (same math as `SetReward`), guaranteeing TP1 = FSL distance exactly with zero drift.

**New Features:**
- Button panel now **hidden by default** to reduce chart noise
- **Show Buttons On Hover** setting: when enabled, the full button panel auto-appears when mouse is anywhere over the tool (entry/stop/target lines, shaded zones, anchor circles, or buttons) and auto-hides when the mouse moves away

---

### v1.0 — Initial Release

- Full feature release: visual RR ruler, partial SL/TP levels, shaded zones, anchor circles
- Auto Order Qty with Max Contracts cap
- Chart Trader quantity sync
- PRO mode: FLIP, AUTO CALC, AUTO TRACK, SOURCE, STOP MODE buttons
- HARD/FLEX stop modes
- StopTrackOnFill toggle
- Button panel: NearEntry / NearTP / NearFSL / CenterTop / CenterBottom positioning
- PriceSource selector: LastPrice / Close / High / Low / Open

---

## 5. Strategy — DrawingToolSyncStrategy.cs and DrawingToolSyncStrategy_BETA.cs

### 5.1 What They Are

Both files are NinjaTrader 8 **Strategy** classes (not Indicators). This distinction is critical: only a Strategy can intercept Chart Trader native buttons, manage positions, and submit/modify orders.

The strategy is installed in:
```
Documents\NinjaTrader 8\bin\Custom\Strategies\
```

Both files can coexist in the same NT8 installation without conflict because they compile to separate class names.

### 5.2 Why Two Versions Exist

The original strategy (`DrawingToolSyncStrategy`, v4.x) used NT8's **managed order API** (`SetStopLoss` / `SetProfitTarget` / `EnterLong` / `EnterShort`). This approach worked for simple entry/exit but had a fundamental architectural limitation: **managed orders cannot reliably update bracket quantities after partial exits or scale-ins**.

The root cause was that `SetStopLoss` / `SetProfitTarget` are declarative — they tell NT8 "the bracket should be X" but NT8 controls when and how it enforces that. After a partial fill (COVER), the bracket quantities did not reliably update even after many workarounds (continuous re-application on every tick, deferred refresh flags, etc.).

The BETA rewrite (`DrawingToolSyncStrategy_BETA`, v5.x) uses NT8's **unmanaged order API** (`IsUnmanaged = true`, `SubmitOrderUnmanaged`, `ChangeOrder`). This gives direct, deterministic control over every order's quantity and price. `ChangeOrder(stopOrder, newQty, 0, stopPrice)` updates the bracket quantity immediately and reliably.

The managed v4.6 is **preserved untouched** as a fallback during testing. The BETA is the active development branch.

### 5.3 DrawingToolSyncStrategy (v4.6 — Managed Orders)

**Architecture:** Uses `SetStopLoss()` + `SetProfitTarget()` + `EnterLong()` / `EnterShort()`. Brackets are re-applied on every `OnBarUpdate` tick while in position to force NT8 to reconcile bracket quantities.

**Key Settings:**
- `EntryHandling = AllEntries` with dynamic signal naming (`SyncEntry_0`, `SyncEntry_1`...) to prevent NT8's UniqueEntries signal exhaustion
- `StopTargetHandling.ByStrategyPosition` for a single bracket covering full position
- `Calculate.OnEachTick` for responsiveness

**Limitations:**
- Bracket qty after COVER partial exits is unreliable — NT8 does not guarantee the bracket matches remaining position after `ExitLong/Short`
- No unmanaged order control; cannot guarantee timing of bracket updates

**Status:** Stable, preserved for reference. Not actively developed.

### 5.4 DrawingToolSyncStrategy_BETA (v5.4 — Unmanaged Orders)

**Architecture:** `IsUnmanaged = true`. All orders submitted via `SubmitOrderUnmanaged()`. Brackets modified via `ChangeOrder()`. Entry and exit fully controlled by the strategy.

**Order Lifecycle:**

```
Entry Button Press
       │
       ▼
SubmitOrderUnmanaged(entry)          ← Market or Limit at entry price
       │
       ▼
OnExecutionUpdate (entry fill)
       │
       ├──► SubmitOrderUnmanaged(stop, StopMarket, stopPrice)  ]
       └──► SubmitOrderUnmanaged(tp,   Limit,      tpPrice)    ]  OCO pair
                  │
                  ▼
         Position is live, bracket protects it
                  │
       ┌──────────┤──────────────────────┐
       │          │                      │
    COVER       SCALE-IN           MAGNET DRAG
       │          │                      │
  Market exit  Market entry         ChangeOrder()
       │          │                on price only
  ChangeOrder  ChangeOrder()
  (qty--)      (qty++)
```

**OCO (One-Cancels-Other):** The stop and TP orders are submitted with a shared OCO group string. When one fills (e.g. TP hits), NT8 automatically cancels the other (SL). The OCO string is session-unique to prevent collision on strategy restart: `"SRCO_{DateTime.Now.Ticks}_{counter}"`.

**Bracket Quantity Management:**
- `bracketQty` tracks the current position size as known by the strategy
- On each entry fill (including partial fills): `bracketQty` is set to the filled quantity
- On each add-on fill: `bracketQty += filledQty`; `ChangeOrder(stop/tp, bracketQty)` called immediately
- On each partial exit fill: `bracketQty -= filledQty`; `ChangeOrder(stop/tp, bracketQty)` called immediately
- `ReconcileBracketQty()` runs every tick and compares `bracketQty` to `Position.Quantity` (NT8 ground truth). If diverged (e.g. due to manual fills), it calls `UpdateBracketQty()` to re-sync. In-flight guards prevent race conditions.

**Drawing Tool Discovery:**
On every tick, `FindDrawingTool()` iterates `DrawObjects`, finds all `AdvancedRiskReward_LuisGarrido` instances, and selects the one with the highest `ChangeVersion`. This ensures the most recently modified tool is always used and avoids stale references.

**Change Detection:**
`HasDrawingToolChanged()` compares `activeDrawingTool.ChangeVersion` to `trackedChangeVersion`. If different, returns `true` and updates the tracked version. Called from `SyncBracketPrices()` to avoid unnecessary `ChangeOrder` calls when nothing moved.

**Terminal State Guard:**
`SafeChangeOrder()` wraps every `ChangeOrder()` call. It checks `Order.IsTerminalState(order.OrderState)` before calling. This prevents exceptions from attempting to modify filled, cancelled, or rejected orders.

**Limit Entry Monitor:**
When the entry is submitted as a Limit order and the user removes the drawing tool or flips its direction while the limit is pending, `MonitorLimitEntry()` automatically calls `CancelOrder(entryOrder)`.

### 5.5 Chart Trader Button Panel

The strategy injects a custom WPF control panel into NinjaTrader's Chart Trader using the **TradeSaber pattern** (`ChartControl.Dispatcher.InvokeAsync` from `OnStateChange` at `State.Historical`). The panel is wrapped in a dark gold border (`RGB(180,150,50)`) and contains:

```
┌──────────────────────────────────────┐
│     RISK-REWARD STATUS    [teal hdr] │
├─────────────────────────────────────-┤  ← slate separator
│      LONG / SHORT                    │  Entry button (auto-detects direction)
├──────────────────────────────────────┤  ← slate separator
│      MAGNET ON / MAGNET OFF          │
├──────────────────────────────────────┤  ← slate separator
│      TP TARGET            [teal hdr] │
│  1RR   │  Last RR  │  NoTP           │  TP selector (locked while in position)
├──────────────────────────────────────┤  ← slate separator
│      STOPWATCH            [teal hdr] │
│  ⏱  00:04:37  /  Last: 00:04:37     │
├──────────────────────────────────────┤  ← slate separator
│      QUICK BUTTONS        [teal hdr] │
│  BREAKEVEN    │  CLOSE POS           │
│  STOP +N      │  TP +N               │
│  STOP -N      │  TP -N               │
│       COVER X                        │
├──────────────────────────────────────┤  ← slate separator
│      AUTOPILOT            [teal hdr] │
│           ● OFF                      │  Status indicator (green=ON, red=OFF)
│  AGGRESSIVE   │  RUNNER              │  Autopilot mode selector
│  TRAIL xT     │  TRAIL xB            │  Trail mode selector
└──────────────────────────────────────┘
```

**Button Behaviors:**

| Button | Flat | In Position |
|---|---|---|
| LONG/SHORT | Enabled — submits entry | Disabled — shows "LONG ACTIVE"/"SHORT ACTIVE" |
| MAGNET ON/OFF | Enabled — toggles sync | Enabled — toggles sync |
| TP Selector (1RR/Last/NoTP) | Enabled | Disabled |
| BREAKEVEN | Disabled | Enabled — moves stop to entry fill price |
| CLOSE POS | Disabled | Enabled — closes full position at market |
| STOP +N / STOP -N | Disabled | Enabled — adjusts stop by `SlAdjustTicks` ticks |
| TP +N / TP -N | Disabled | Enabled — adjusts TP by `TpAdjustTicks` ticks |
| COVER X | Disabled | Enabled — exits `PartialExitQty` contracts at market |
| AUTOPILOT (Aggressive/Runner) | Enabled (pre-arm) | Enabled |
| TRAIL xT / TRAIL xB | Enabled (pre-arm) | Enabled |

**Trade Timer (Stopwatch):**
Starts on the first entry fill. Displays elapsed time in `HH:MM:SS` format while in position. When position closes, freezes as "Last: HH:MM:SS" in gray until the next entry.

### 5.6 Native Button Intercept Behavior

The strategy hooks NT8's native Chart Trader buy/sell buttons via WPF `PreviewMouseLeftButtonDown` tunneling events. Ask/Bid prices are captured on the UI thread at click time and passed to the NinjaScript thread via `nativeCapturedPrice`.

**Three-rule intercept logic:**

| Condition | Action |
|---|---|
| Flat + drawing tool present | Execute strategy entry (Market or Limit, reads direction from `IsLongTrade()`) |
| Flat + NO drawing tool | Pass-through (NT8 handles normally) |
| In position + SAME direction | Scale-in (adds to position) |
| In position + OPPOSITE direction | **Blocked** (prevents accidental reversal) |

### 5.7 Autopilot System

The Autopilot is a level-based trailing stop that uses the drawing tool's price ladder as stop targets. Available from v5.4-BETA.

**Level Array:** Built on entry from drawing tool data:
```
[SL price, Entry price, TP1 price, TP2 price, ..., Final TP price]
  index 0      index 1    index 2    index 3          index N
```

**Level "reached" detection:** A level is considered reached when:
- **Long:** `High[0] >= level price` (wick touch)
- **Short:** `Low[0] <= level price` (wick touch)

**AGGRESSIVE Mode** — moves stop 1 level behind the highest touched level:
```
Price reaches TP1 (index 2) → stop moves to Entry (index 1)
Price reaches TP2 (index 3) → stop moves to TP1  (index 2)
Price reaches TP3 (index 4) → stop moves to TP2  (index 3)
```

**RUNNER Mode** — moves stop 2 levels behind the highest touched level (gives more room):
```
Price reaches TP2 (index 3) → stop moves to Entry (index 1)
Price reaches TP3 (index 4) → stop moves to TP1  (index 2)
Price reaches TP4 (index 5) → stop moves to TP2  (index 3)
```

**Safety rules:**
- The ratchet is **one-way** — stop only moves toward profit, never back
- Stop never moves worse than the original FSL (index 0)
- Autopilot disables MAGNET when active (they both own the stop; only one can be active)
- Activating AGGRESSIVE or RUNNER disables TRAIL and disables MAGNET
- Pressing BreakEven, Stop+, or Stop- disables Autopilot
- Autopilot resets to Off when position goes flat
- **Pre-arm support:** User can select Autopilot mode while flat; it activates automatically on entry fill

### 5.8 Trailing Stop System

Two independent trail modes available from v5.3-BETA:

**TRAIL xT (Tick Trail):**
- Maintains a high-water mark initialized to `Close[0]` at activation
- On each tick: if price moves favorably beyond the high-water mark, the mark is updated and the stop is moved to `highWater - TrailTicks × TickSize` (long) or `highWater + TrailTicks × TickSize` (short)
- Stop only moves toward profit (one-way ratchet)
- Configurable via `TrailTicks` property (default: 8)

**TRAIL xB (Bar Trail):**
- Calculates stop as the lowest Low (long) or highest High (short) of the last `TrailBars` bars
- Recalculated on every tick; the stop naturally ratchets as bars form
- Configurable via `TrailBars` property (default: 3)

**Mutual exclusivity:** Both trail modes are mutually exclusive with each other and with Autopilot. Activating any trail mode disables MAGNET sync (trail owns the stop). Activating MAGNET disables trail. Trail state resets to Off when position goes flat.

### 5.9 Properties Reference — BETA

| Property | Group | Default | Description |
|---|---|---|---|
| `Entry Order Type` | Order Settings | Market | Market = immediate fill; LimitAtEntry = limit at drawing tool entry price |
| `TP Line Selection` | Order Settings | LastTP | FirstTP = 1R target; LastTP = final RR target; NoTP = stop only, no TP bracket |
| `Enable Live Sync` | Order Settings | true | When ON, dragging SL/TP on the drawing tool auto-modifies live exit orders (magnet) |
| `SL Adjust Ticks` | Button Settings | 4 | Number of ticks per press for STOP +N / STOP -N buttons |
| `TP Adjust Ticks` | Button Settings | 4 | Number of ticks per press for TP +N / TP -N buttons |
| `Partial Exit Qty` | Button Settings | 1 | Number of contracts the COVER button exits per press |
| `Trail Ticks` | Button Settings | 8 | Tick offset for TRAIL xT mode |
| `Trail Bars` | Button Settings | 3 | Lookback bars for TRAIL xB mode |
| `Print Debug` | Debug | false | Enables debug print output to NT8 Output window |

---

## 6. BETA Strategy Source Code

The complete source of `DrawingToolSyncStrategy_BETA.cs` at **v5.4-BETA** is preserved below for reference, context, and future AI agent consumption.

```csharp
// ============================================================================
//  DrawingToolSyncStrategy_BETA
//  NinjaTrader 8 Strategy — Version 5.4-BETA
// ============================================================================
//
//  CHANGELOG v5.3:
//  [UI] Added "RISK-REWARD STATUS" teal section header above the LONG/SHORT
//       entry button to visually group the direction indicator.
//  [UI] All panel separators updated from teal to Slate Blue-Gray
//       RGB(60,80,100) for improved contrast against teal section headers.
//
//  DESCRIPTION:
//  A strategy that acts as an execution bridge between the
//  AdvancedRiskReward_LuisGarrido drawing tool and NinjaTrader's order system.
//  Reads entry/SL/TP prices from the drawing tool, submits entry orders, and
//  places individual SL + TP exit orders using the UNMANAGED order approach.
//
//  THIS IS A BETA TEST FILE — runs alongside the original DrawingToolSyncStrategy.
//  The original managed-order strategy remains untouched.
//
//  ARCHITECTURE OVERVIEW:
//
//  ┌─────────────────────────────────────────────────────────────────┐
//  │                      DRAWING TOOL  (v1.4+)                     │
//  │  AdvancedRiskReward_LuisGarrido                                │
//  │  ├── GetEntryPrice()     (entry price, tick-rounded)           │
//  │  ├── GetStopPrice()      (FSL price, tick-rounded)             │
//  │  ├── GetTPPrices()       (all TP levels: [0]=1R .. [n]=final)  │
//  │  ├── GetFinalTPPrice()   (last TP = RewardAnchor)              │
//  │  ├── GetCalculatedQty()  (contract qty from risk budget)       │
//  │  ├── IsLongTrade()       (direction)                           │
//  │  └── ChangeVersion       (increments on every anchor move)     │
//  └───────────────────────────┬─────────────────────────────────────┘
//                              │ polled via DrawObjects
//                              ▼
//  ┌─────────────────────────────────────────────────────────────────┐
//  │                    THIS STRATEGY  (v5.0-BETA)                  │
//  │  DrawingToolSyncStrategy_BETA                                  │
//  │                                                                │
//  │  ORDER MODEL: UNMANAGED APPROACH                               │
//  │  ┌──────────────────────────────────────────────────────────┐   │
//  │  │  IsUnmanaged = true                                      │   │
//  │  │  Full direct control over every order via:               │   │
//  │  │  • SubmitOrderUnmanaged() — submit new orders            │   │
//  │  │  • ChangeOrder()          — modify qty/price             │   │
//  │  │  • CancelOrder()          — cancel working orders        │   │
//  │  │                                                          │   │
//  │  │  On entry fill:                                          │   │
//  │  │  1. Submit SL as StopMarket (full qty)                   │   │
//  │  │  2. Submit TP as Limit (full qty) — tied via OCO string  │   │
//  │  │  3. OCO = native: TP fill cancels SL and vice versa     │   │
//  │  │                                                          │   │
//  │  │  On COVER (partial exit):                                │   │
//  │  │  1. Submit market exit for coverQty                      │   │
//  │  │  2. ChangeOrder(stop, reducedQty)  — INSTANT             │   │
//  │  │  3. ChangeOrder(tp, reducedQty)    — INSTANT             │   │
//  │  │                                                          │   │
//  │  │  On SCALE-IN (add to position):                          │   │
//  │  │  1. Submit market/limit entry for addQty                 │   │
//  │  │  2. ChangeOrder(stop, increasedQty) — INSTANT            │   │
//  │  │  3. ChangeOrder(tp, increasedQty)   — INSTANT            │   │
//  │  │                                                          │   │
//  │  │  On MAGNET SYNC / STOP± / TP±:                           │   │
//  │  │  ChangeOrder(order, sameQty, newPrice) — INSTANT         │   │
//  │  └──────────────────────────────────────────────────────────┘   │
//  │                                                                │
//  │  OnBarUpdate (Calculate.OnEachTick)                             │
//  │  ├── FindDrawingTool()      → scan DrawObjects, best version   │
//  │  ├── ProcessUIFlags()       → consume volatile button flags    │
//  │  ├── MonitorLimitEntry()    → cancel limit if tool changes     │
//  │  ├── SyncBracketPrices()    → magnet: ChangeOrder SL/TP price  │
//  │  ├── MonitorPosition()      → detect flat → reset state        │
//  │  └── UpdateButtonStates()   → WPF button enable/color update   │
//  │                                                                │
//  │  OnOrderUpdate                                                 │
//  │  ├── Track order objects (entryOrder, stopOrder, tpOrder)       │
//  │  └── Detect terminal states → null out references              │
//  │                                                                │
//  │  OnExecutionUpdate                                             │
//  │  ├── Entry fill → submit SL + TP brackets                     │
//  │  ├── Entry partial fill → submit/update brackets for filled    │
//  │  ├── Add-on fill → ChangeOrder to increase bracket qty         │
//  │  ├── Partial exit fill → ChangeOrder to decrease bracket qty   │
//  │  └── Cover guard release                                       │
//  │                                                                │
//  │  OnStateChange (Historical / Terminated)                       │
//  │  ├── Inject WPF buttons into ChartTrader (TradeSaber pattern)  │
//  │  └── Clean up on removal (TabChanged, Dispose)                 │
//  └─────────────────────────────────────────────────────────────────┘
//
//  CHART TRADER BUTTONS (TradeSaber WPF injection pattern):
//  ┌──────────────────────────────────┐
//  │     RISK-REWARD STATUS           │  row 0  — teal section header (NEW)
//  ├ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┤  row 1  — slate separator (NEW)
//  │  LONG / SHORT (auto-detect)      │  row 2  — entry button
//  ├ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┤  row 3  — slate separator
//  │   MAGNET ON / MAGNET OFF         │  row 4
//  ├ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┤  row 5  — slate separator
//  │       TP TARGET                  │  row 6  — teal header
//  │ 1RR    │ Last RR  │ NoTP         │  row 7  — TP selector
//  ├ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┤  row 8  — slate separator
//  │       STOPWATCH                  │  row 9  — teal header
//  │  ⏱  00:04:37  /  Last: 00:04:37 │  row 10 — timer value
//  ├ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┤  row 11 — slate separator
//  │       QUICK BUTTONS              │  row 12 — teal header
//  │ BREAKEVEN    │ CLOSE POS         │  row 13
//  │ STOP +N      │ TP +N             │  row 14
//  │ STOP -N      │ TP -N             │  row 15
//  │       COVER X                   │  row 16
//  ├ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┤  row 17 — slate separator
//  │       AUTOPILOT                  │  row 18 — teal header
//  │     ● OFF  /  ● ON              │  row 19 — status indicator
//  │ AGGRESSIVE   │ RUNNER            │  row 20 — autopilot mode selector
//  │  TRAIL Xt   │  TRAIL Xb          │  row 21 — trail mode selector
//  └──────────────────────────────────┘
//
//  NATIVE CHART TRADER BUTTON INTERCEPT:
//  Buy Mkt / Sell Mkt / Buy Ask / Sell Ask / Buy Bid / Sell Bid are hooked.
//  When flat + drawing tool present → strategy entry.
//  When flat + NO drawing tool      → pass-through (NT8 handles normally).
//  When in position + same direction → scale-in (add to position).
//  When in position + opposite dir   → blocked (prevent accidental reversal).
//
//  THREAD SAFETY:
//  WPF button clicks set volatile bool flags on the UI thread.
//  OnBarUpdate reads and clears them on the NinjaScript thread.
//  All order methods are called only from OnBarUpdate/OnExecutionUpdate.
//
// ----------------------------------------------------------------------------
//  CHANGELOG:
//
//  v5.4-BETA (2026-04-09)
//  - NEW: AUTOPILOT section — level-based trailing stop that uses the drawing
//    tool's price ladder (SL → Entry → TP1 → TP2 → ... → FinalTP) to
//    automatically ratchet the stop loss as price advances through each level.
//
//    • AGGRESSIVE mode — moves the stop to 1 level behind the highest level
//      that price has touched (wick). E.g. price reaches TP1 → stop moves to
//      Entry; price reaches TP2 → stop moves to TP1.
//
//    • RUNNER mode — moves the stop to 2 levels behind the highest level that
//      price has touched. Gives more room for the trade to run. E.g. price
//      reaches TP2 → stop moves to Entry; price reaches TP3 → stop moves to TP1.
//
//    Level "reached" is defined by wick touch (High[0] >= level for longs,
//    Low[0] <= level for shorts). The ratchet is one-way — stop only moves
//    toward profit, never back.
//
//  - NEW: AUTOPILOT panel in Chart Trader — replaces the old TRAIL row at the
//    bottom of QUICK BUTTONS with a dedicated section below COVER:
//      Row 17: separator
//      Row 18: "AUTOPILOT" teal section header
//      Row 19: status indicator (● ON green / ● OFF red) — purely visual,
//              shows ON when any of the 4 modes is active
//      Row 20: AGGRESSIVE | RUNNER toggle buttons (amber = active)
//      Row 21: TRAIL xT | TRAIL xB (relocated from QUICK BUTTONS section)
//
//    All 4 buttons are mutually exclusive — only one can be active at a time.
//
//  - NEW: Mutual exclusivity rules:
//    • Activating AGGRESSIVE or RUNNER → disables TRAIL and MAGNET
//    • Activating TRAIL → disables AUTOPILOT (Aggressive/Runner)
//    • Activating MAGNET → disables AUTOPILOT (Aggressive/Runner)
//    • Pressing BreakEven, Stop+, or Stop- → disables AUTOPILOT
//
//  - NEW: Pre-arm support — user can select an autopilot mode while flat;
//    the logic activates automatically on entry fill.
//
//  - NEW: Auto-reset — autopilot mode resets to OFF when position goes flat.
//
//  - FIX: SyncBracketPrices() now also skips stop-price sync when autopilot is
//    active (same guard as trail mode), so autopilot owns the stop exclusively.
//
//  - [UI] Dark gold border (RGB 180,150,50) wraps the entire strategy panel
//    from RISK-REWARD STATUS header down to the last TRAIL button, providing
//    a clear visual boundary for the injected controls.
//
//  v5.3-BETA (2026-04-08)
//  - NEW: Trailing stop feature — two independent modes selectable via buttons
//    in the Chart Trader panel (row 3, between MAGNET and QUICK BUTTONS):
//
//    • TRAIL Xt (Tick Trail) — ratchets the stop loss behind a high-water mark.
//      On each tick, if price moves favorably beyond the stored extreme the stop
//      is moved to: extreme − TrailTicks (long) / extreme + TrailTicks (short).
//      The stop only ever moves in the profitable direction (never backwards).
//      High-water mark is initialised to Close[0] at the moment trail is toggled ON.
//
//    • TRAIL Xb (Bar Trail) — places the stop at the lowest Low (long) or highest
//      High (short) of the last TrailBars bars, recalculated on every tick.
//      No high-water mark needed; the bar-range naturally ratchets as bars form.
//
//    Both modes are mutually exclusive (selecting one turns off the other).
//    Activating either mode automatically disables MAGNET (trail owns the stop).
//    Activating MAGNET automatically disables the active trail mode.
//    Trail state resets to Off when the position goes flat (ResetState).
//    SyncBracketPrices() skips stop-price sync when any trail mode is active.
//    New parameters: TrailTicks (default 8), TrailBars (default 3).
//    Button colours: amber = active, dark-grey = inactive.
//
//  v5.2-BETA (2026-04-08)
//  - FIX: coverInFlight flag never reset on cover order rejection/cancellation.
//    Root cause: coverInFlight was only set to false inside OnExecutionUpdate
//    on PartialExit fill. If the cover order was rejected or cancelled by the
//    exchange (without filling), coverInFlight stayed true permanently, blocking
//    all future COVER button presses until the position was fully closed.
//    Fix: Added reset of coverInFlight in OnOrderUpdate when PartialExit reaches
//    terminal state via Cancelled or Rejected (not Filled).
//
//  v5.1-BETA (2026-04-07)
//  - FIX: OCO ID reuse error on strategy restart within the same NT8 session.
//    Root cause: ocoCounter reset to 0 on each new strategy instance, causing
//    "SyncOCO_1" to collide with a previously used OCO group. NT8 blocks this
//    reuse and rejects the stop order, leaving the trade unprotected (no SL).
//    Fix: generate a unique ocoSessionPrefix from DateTime.Now.Ticks at
//    SetDefaults. OCO format is now "SRCO_{ticks}_{counter}" — globally unique
//    across all strategy restarts within the same NT8 session.
//  - FIX: Manual/external order fills no longer leave bracket qty stale.
//    A manually placed chart order (e.g. Buy Stop Limit used as a manual cover)
//    fills outside strategy knowledge, so bracketQty was never updated.
//    This caused the stop/TP to cover the wrong quantity and risk creating an
//    unintended reverse position.
//    Fix: Added ReconcileBracketQty() called from OnBarUpdate after
//    MonitorPosition(). Reads Position.Quantity (NT8 ground truth) on every
//    tick and calls UpdateBracketQty() whenever it diverges from bracketQty.
//    In-flight guards (entryOrder, addOnOrder, coverInFlight) prevent a race
//    condition where Position.Quantity updates before OnExecutionUpdate fires,
//    which would otherwise cause double-count (AddOn) or double-subtract
//    (PartialExit) in bracketQty.
//
//  v5.0-BETA (2026-04-07)
//  - COMPLETE REWRITE: Switched from Managed orders to UNMANAGED approach.
//    IsUnmanaged = true — full direct control over all orders.
//  - NEW: Entry via SubmitOrderUnmanaged() (Market or Limit).
//  - NEW: SL bracket via SubmitOrderUnmanaged() as StopMarket order.
//  - NEW: TP bracket via SubmitOrderUnmanaged() as Limit order.
//  - NEW: SL + TP tied via OCO string — native cancellation on fill.
//  - NEW: COVER partial exit instantly updates bracket qty via ChangeOrder().
//    This is the definitive fix for the qty-not-updating bug in managed orders.
//  - NEW: Scale-in instantly increases bracket qty via ChangeOrder().
//  - NEW: Stop+/Stop-/TP+/TP-/Breakeven via ChangeOrder() on price.
//  - NEW: Magnet sync via ChangeOrder() on price.
//  - NEW: OnOrderUpdate() tracks all order object references as required
//    by the unmanaged approach.
//  - NEW: Partial entry fill handling — brackets submitted on first partial,
//    updated via ChangeOrder on each subsequent fill, finalized on Filled.
//  - NEW: Limit entry auto-cancel when drawing tool removed or direction
//    flipped while limit is working.
//  - NEW: Terminal state guard on every ChangeOrder call.
//  - REMOVED: SetStopLoss / SetProfitTarget (not available in unmanaged).
//  - REMOVED: StopTargetHandling / EntriesPerDirection / EntryHandling
//    (not used in unmanaged mode).
//  - REMOVED: Continuous bracket refresh on every tick (no longer needed —
//    qty is explicit via ChangeOrder).
//  - KEPT: All WPF injection, button layout, tab switching (from v4.6).
//  - KEPT: All volatile bool flags + ProcessUIFlags() structure (from v4.6).
//  - KEPT: FindDrawingTool(), HasDrawingToolChanged() (from v4.6).
//  - KEPT: UpdateButtonStates(), TriggerImmediateUpdate() (from v4.6).
//  - KEPT: Native ChartTrader button intercepts (from v4.6).
//  - KEPT: Fallback timer, DebugPrint() (from v4.6).
//  - KEPT: Trade timer / stopwatch (from v4.6).
//  - KEPT: TP selector (1RR / Last RR / NoTP) (from v4.6).
//  - KEPT: Magnet toggle (from v4.6).
//  - KEPT: COVER guard via coverInFlight flag (from v4.6).
//
//  REQUIRES:
//  - AdvancedRiskReward_LuisGarrido.cs v1.4+ with public accessor methods
//
// ============================================================================
#region Using declarations

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using NinjaTrader.Cbi;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript.DrawingTools;

#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
	// ═══════════════════════════════════════════════════════════════════════════
	//  ENUMS (duplicated here so this file is fully standalone)
	// ═══════════════════════════════════════════════════════════════════════════

	public enum EntryOrderTypeBeta
	{
		Market,
		LimitAtEntry
	}

	public enum TPLineChoiceBeta
	{
		FirstTP,
		LastTP,
		NoTP
	}

	public enum TrailModeBeta
	{
		Off,
		Ticks,
		Bars
	}

	public enum AutopilotModeBeta
	{
		Off,
		Aggressive,
		Runner
	}

	// ═══════════════════════════════════════════════════════════════════════════
	//  STRATEGY CLASS
	// ═══════════════════════════════════════════════════════════════════════════

	public class DrawingToolSyncStrategy_BETA : Strategy
	{
		// ═══════════════════════════════════════════════════════════════════════
		//  CONFIGURABLE PROPERTIES
		// ═══════════════════════════════════════════════════════════════════════

		[NinjaScriptProperty]
		[Display(Name = "Entry Order Type", GroupName = "1. Order Settings", Order = 1,
			Description = "Market = immediate fill; LimitAtEntry = limit at drawing tool entry price")]
		public EntryOrderTypeBeta EntryType { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "TP Line Selection", GroupName = "1. Order Settings", Order = 2,
			Description = "FirstTP = nearest TP (1R); LastTP = furthest TP (final RR); NoTP = stop loss only, no profit target")]
		public TPLineChoiceBeta TPSelection { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Enable Live Sync", GroupName = "1. Order Settings", Order = 3,
			Description = "When ON, dragging SL/TP on the drawing tool auto-modifies live exit orders")]
		public bool EnableLiveSync { get; set; }

		[NinjaScriptProperty]
		[Range(1, 500)]
		[Display(Name = "SL Adjust Ticks", GroupName = "2. Button Settings", Order = 1,
			Description = "Ticks to add/subtract when pressing the SL adjustment buttons")]
		public int SlAdjustTicks { get; set; }

		[NinjaScriptProperty]
		[Range(1, 500)]
		[Display(Name = "TP Adjust Ticks", GroupName = "2. Button Settings", Order = 2,
			Description = "Ticks to add/subtract when pressing the TP adjustment buttons")]
		public int TpAdjustTicks { get; set; }

		[NinjaScriptProperty]
		[Range(1, 100)]
		[Display(Name = "Partial Exit Qty", GroupName = "2. Button Settings", Order = 3,
			Description = "Number of contracts to exit when pressing the COVER X button")]
		public int PartialExitQty { get; set; }

		[NinjaScriptProperty]
		[Range(1, 500)]
		[Display(Name = "Trail Ticks", GroupName = "2. Button Settings", Order = 4,
			Description = "Ticks behind the price high-water mark to place the trailing stop (Tick Trail mode)")]
		public int TrailTicks { get; set; }

		[NinjaScriptProperty]
		[Range(1, 50)]
		[Display(Name = "Trail Bars", GroupName = "2. Button Settings", Order = 5,
			Description = "Bars to look back for the stop level (Bar Trail mode: lowest Low for longs, highest High for shorts)")]
		public int TrailBars { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Print Debug", GroupName = "3. Advanced", Order = 1,
			Description = "Print debug messages to NinjaTrader output window")]
		public bool PrintDebug { get; set; }

		// ═══════════════════════════════════════════════════════════════════════
		//  INTERNAL STATE
		// ═══════════════════════════════════════════════════════════════════════

		// Drawing tool tracking
		private AdvancedRiskReward_LuisGarrido activeDrawingTool = null;
		private int    trackedChangeVersion = -1;
		private bool   hasPosition          = false;
		private bool   isLong               = true;

		// Unmanaged order tracking
		private Order  entryOrder           = null;
		private Order  stopOrder            = null;
		private Order  tpOrder              = null;
		private Order  coverOrder           = null;
		private Order  addOnOrder           = null;
		private int    ocoCounter           = 0;
		private string ocoSessionPrefix     = "";          // set once on init; survives restarts
		private string ActiveOCO            => ocoSessionPrefix + "_" + ocoCounter;

		// Price tracking for bracket modifications
		private double lastSyncedStop       = 0;
		private double lastSyncedTarget     = 0;
		private double entryFillPrice       = 0;

		// Trailing stop state
		private TrailModeBeta trailMode      = TrailModeBeta.Off;
		private double        trailHighWater = 0;
		private int    bracketQty           = 0;  // current bracket order qty

		// Autopilot level-trail state
		private AutopilotModeBeta autopilotMode           = AutopilotModeBeta.Off;
		private int               autopilotHighestLevelIdx = -1;

		// Track direction at entry for limit order cancellation
		private bool   entryDirectionLong   = true;

		// ═══════════════════════════════════════════════════════════════════════
		//  THREAD-SAFE UI → NINJASCRIPT FLAGS  (volatile bools)
		// ═══════════════════════════════════════════════════════════════════════

		private volatile bool submitEntryRequested    = false;
		private volatile bool breakEvenRequested      = false;
		private volatile bool closePositionRequested  = false;
		private volatile bool adjustStopUpRequested   = false;
		private volatile bool adjustStopDownRequested = false;
		private volatile bool adjustTPUpRequested     = false;
		private volatile bool adjustTPDownRequested   = false;
		private volatile bool coverRequested          = false;
		private volatile bool magnetToggleRequested   = false;
		private volatile bool trailTicksRequested     = false;
		private volatile bool trailBarsRequested      = false;
		private volatile bool coverInFlight           = false;
		private volatile bool autopilotAggressiveReq  = false;
		private volatile bool autopilotRunnerReq      = false;

		// Native button intercept flags
		private volatile bool nativeBuyMktRequested   = false;
		private volatile bool nativeSellMktRequested  = false;
		private volatile bool nativeBuyAskRequested   = false;
		private volatile bool nativeSellAskRequested  = false;
		private volatile bool nativeBuyBidRequested   = false;
		private volatile bool nativeSellBidRequested  = false;

		private double nativeCapturedPrice = 0;

		// Magnet state
		private volatile bool magnetActive = true;

		// ═══════════════════════════════════════════════════════════════════════
		//  WPF UI REFERENCES
		// ═══════════════════════════════════════════════════════════════════════

		private Chart                        chartWindow            = null;
		private System.Windows.Controls.Grid chartTraderGrid        = null;
		private System.Windows.Controls.Grid chartTraderButtonsGrid = null;
		private System.Windows.Controls.Grid lowerButtonsGrid       = null;
		private System.Windows.Controls.Border panelBorder            = null;
		private RowDefinition                addedRow               = null;
		private bool                         panelActive            = false;

		private System.Windows.Controls.Button btnEntry     = null;
		private System.Windows.Controls.Button btnBreakeven = null;
		private System.Windows.Controls.Button btnClose     = null;
		private System.Windows.Controls.Button btnStopUp    = null;
		private System.Windows.Controls.Button btnStopDown  = null;
		private System.Windows.Controls.Button btnTPUp      = null;
		private System.Windows.Controls.Button btnTPDown    = null;
		private System.Windows.Controls.Button btnCover     = null;
		private System.Windows.Controls.Button btnMagnet    = null;

		private System.Windows.Controls.Button btnTP1RR     = null;
		private System.Windows.Controls.Button btnTPLast    = null;
		private System.Windows.Controls.Button btnTPNone    = null;

		private System.Windows.Controls.Button btnTrailTicks = null;
		private System.Windows.Controls.Button btnTrailBars  = null;

		private System.Windows.Controls.TextBlock lblAutopilotStatus = null;
		private System.Windows.Controls.Button btnAutopilotAggressive = null;
		private System.Windows.Controls.Button btnAutopilotRunner     = null;

		private System.Windows.Controls.TextBlock lblTPHeader        = null;
		private System.Windows.Controls.TextBlock lblStopwatchHeader = null;
		private System.Windows.Controls.TextBlock lblTradeTimer      = null;
		private DispatcherTimer timerClock   = null;
		private DateTime        tradeStartTime;
		private bool            timerRunning = false;
		private string          lastTradeDuration = "--:--:--";

		private System.Windows.Controls.Button nativeBuyMkt  = null;
		private System.Windows.Controls.Button nativeSellMkt = null;
		private System.Windows.Controls.Button nativeBuyAsk  = null;
		private System.Windows.Controls.Button nativeSellAsk = null;
		private System.Windows.Controls.Button nativeBuyBid  = null;
		private System.Windows.Controls.Button nativeSellBid = null;
		private System.Windows.Controls.Button nativeClose   = null;

		private System.Windows.Controls.TabItem tabItem  = null;
		private ChartTab                         chartTab = null;

		private DispatcherTimer fallbackTimer = null;

		// ═══════════════════════════════════════════════════════════════════════
		//  LIFECYCLE: OnStateChange
		// ═══════════════════════════════════════════════════════════════════════

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description             = "BETA v5.4: Unmanaged order approach for AdvancedRiskReward drawing tool sync";
				Name                    = "DrawingToolSyncStrategy_BETA";
				Calculate               = Calculate.OnEachTick;
				IsOverlay               = true;
				IsUnmanaged             = true;          // ← UNMANAGED MODE
				IsExitOnSessionCloseStrategy = true;
				ExitOnSessionCloseSeconds    = 30;
				IsFillLimitOnTouch      = false;
				MaximumBarsLookBack     = MaximumBarsLookBack.TwoHundredFiftySix;
				OrderFillResolution     = OrderFillResolution.Standard;
				StartBehavior           = StartBehavior.WaitUntilFlat;
				TimeInForce             = TimeInForce.Gtc;
				TraceOrders             = false;
				RealtimeErrorHandling   = RealtimeErrorHandling.IgnoreAllErrors;
				BarsRequiredToTrade     = 1;
				IsInstantiatedOnEachOptimizationIteration = true;

				// Custom defaults
				EntryType       = EntryOrderTypeBeta.Market;
				TPSelection     = TPLineChoiceBeta.LastTP;
				EnableLiveSync  = true;
				SlAdjustTicks   = 4;
				TpAdjustTicks   = 4;
				PartialExitQty  = 1;
				TrailTicks      = 8;
				TrailBars       = 3;
				PrintDebug      = false;

				// Generate a session-unique prefix for OCO IDs so they can never
				// collide with IDs from a previous strategy instance in the same
				// NT8 session (NT8 tracks all OCO strings globally per session).
				ocoSessionPrefix = "SRCO_" + DateTime.Now.Ticks;
			}
			else if (State == State.Historical)
			{
				if (ChartControl != null)
				{
					ChartControl.Dispatcher.InvokeAsync(() =>
					{
						CreateWPFControls();
					});
				}
			}
			else if (State == State.Realtime)
			{
				StartFallbackTimer();
			}
			else if (State == State.Terminated)
			{
				StopFallbackTimer();

				if (ChartControl != null)
				{
					ChartControl.Dispatcher.InvokeAsync(() =>
					{
						DisposeWPFControls();
					});
				}

				ResetState();
			}
		}

		// ═══════════════════════════════════════════════════════════════════════
		//  CORE ENGINE: OnBarUpdate
		// ═══════════════════════════════════════════════════════════════════════

		protected override void OnBarUpdate()
		{
			if (State != State.Realtime)
				return;
			if (CurrentBar < BarsRequiredToTrade)
				return;

			// Step 1: Find drawing tool
			FindDrawingTool();

			// Step 2: Process button flags
			ProcessUIFlags();

			// Step 3: Monitor limit entry (auto-cancel if tool removed/flipped)
			MonitorLimitEntry();

			// Step 4: Sync bracket prices (magnet)
			SyncBracketPrices();

			// Step 4b: Apply trailing stop (takes over stop when trail is active)
			ApplyTrailingStop();

			// Step 4c: Apply autopilot level-trail (takes over stop when autopilot is active)
			ApplyAutopilotStop();

			// Step 5: Monitor position state
			MonitorPosition();

			// Step 5b: Reconcile bracket qty against live position (catches manual fills)
			ReconcileBracketQty();

			// Step 6: Update button states (run BEFORE BlockAtmOrders so a
			// failure in the ATM sweep can never freeze the UI)
			UpdateButtonStates();

			// Step 7: Block ATM-template orders (leave manual single orders alone)
			BlockAtmOrders();
		}

		// ═══════════════════════════════════════════════════════════════════════
		//  DRAWING TOOL DISCOVERY
		// ═══════════════════════════════════════════════════════════════════════

		private void FindDrawingTool()
		{
			AdvancedRiskReward_LuisGarrido bestTool = null;
			int bestVersion = -1;

			foreach (IDrawingTool drawObj in DrawObjects)
			{
				var tool = drawObj as AdvancedRiskReward_LuisGarrido;
				if (tool == null) continue;

				if (tool.ChangeVersion > bestVersion)
				{
					bestTool    = tool;
					bestVersion = tool.ChangeVersion;
				}
			}

			activeDrawingTool = bestTool;
			if (activeDrawingTool == null)
				trackedChangeVersion = -1;
		}

		private bool HasDrawingToolChanged()
		{
			if (activeDrawingTool == null) return false;
			if (activeDrawingTool.ChangeVersion == trackedChangeVersion) return false;
			trackedChangeVersion = activeDrawingTool.ChangeVersion;
			return true;
		}

		// ═══════════════════════════════════════════════════════════════════════
		//  TP PRICE HELPER
		// ═══════════════════════════════════════════════════════════════════════

		private double GetSelectedTPPrice()
		{
			if (activeDrawingTool == null) return 0;

			if (TPSelection == TPLineChoiceBeta.FirstTP)
			{
				double[] tpPrices = activeDrawingTool.GetTPPrices();
				if (tpPrices != null && tpPrices.Length > 0)
					return tpPrices[0];
				return activeDrawingTool.GetFinalTPPrice();
			}
			else // LastTP
			{
				return activeDrawingTool.GetFinalTPPrice();
			}
		}

		// ═══════════════════════════════════════════════════════════════════════
		//  LIMIT ENTRY MONITOR — auto-cancel if tool removed or flipped
		// ═══════════════════════════════════════════════════════════════════════

		private void MonitorLimitEntry()
		{
			if (entryOrder == null) return;
			if (Order.IsTerminalState(entryOrder.OrderState)) return;

			// Cancel if tool removed
			if (activeDrawingTool == null)
			{
				DebugPrint("Limit entry cancelled — drawing tool removed");
				CancelOrder(entryOrder);
				return;
			}

			// Cancel if direction flipped
			bool toolIsLong = activeDrawingTool.IsLongTrade();
			if (toolIsLong != entryDirectionLong)
			{
				DebugPrint("Limit entry cancelled — direction flipped");
				CancelOrder(entryOrder);
				return;
			}
		}

		// ═══════════════════════════════════════════════════════════════════════
		//  BRACKET PRICE SYNC (Magnet)
		// ═══════════════════════════════════════════════════════════════════════

		private void SyncBracketPrices()
		{
			if (!hasPosition) { HasDrawingToolChanged(); return; }
			if (!EnableLiveSync || !magnetActive || activeDrawingTool == null)
			{
				HasDrawingToolChanged();
				return;
			}

			// Read new prices from drawing tool — stop sync only when trail and autopilot are not active
			if (trailMode == TrailModeBeta.Off && autopilotMode == AutopilotModeBeta.Off)
			{
				double newStop = activeDrawingTool.GetStopPrice();
				if (newStop > 0 && Math.Abs(newStop - lastSyncedStop) > TickSize / 2)
				{
					DebugPrint("MagnetSyncSL: " + lastSyncedStop + " → " + newStop);
					lastSyncedStop = newStop;
					SafeChangeOrder(stopOrder, bracketQty, 0, newStop);
				}
			}

			if (TPSelection != TPLineChoiceBeta.NoTP)
			{
				double newTP = GetSelectedTPPrice();
				if (newTP > 0 && Math.Abs(newTP - lastSyncedTarget) > TickSize / 2)
				{
					DebugPrint("MagnetSyncTP: " + lastSyncedTarget + " → " + newTP);
					lastSyncedTarget = newTP;
					SafeChangeOrder(tpOrder, bracketQty, newTP, 0);
				}
			}

			HasDrawingToolChanged();
		}

		// ═══════════════════════════════════════════════════════════════════════
		//  TRAILING STOP ENGINE
		// ═══════════════════════════════════════════════════════════════════════

		private void ApplyTrailingStop()
		{
			if (trailMode == TrailModeBeta.Off) return;
			if (!hasPosition || stopOrder == null) return;
			if (Order.IsTerminalState(stopOrder.OrderState)) return;

			double newStop = 0;

			if (trailMode == TrailModeBeta.Ticks)
			{
				// ── Tick trail: ratchet stop behind the best price seen since activation ──
				if (isLong)
				{
					if (Close[0] > trailHighWater) trailHighWater = Close[0];
					newStop = Instrument.MasterInstrument.RoundToTickSize(
						trailHighWater - TrailTicks * TickSize);
					if (newStop <= lastSyncedStop) return;   // no improvement
				}
				else
				{
					if (trailHighWater == 0 || Close[0] < trailHighWater)
						trailHighWater = Close[0];
					newStop = Instrument.MasterInstrument.RoundToTickSize(
						trailHighWater + TrailTicks * TickSize);
					if (lastSyncedStop > 0 && newStop >= lastSyncedStop) return;  // no improvement
				}
			}
			else if (trailMode == TrailModeBeta.Bars)
			{
				// ── Bar trail: stop behind the lowest Low / highest High of last N bars ──
				int lookback = Math.Min(TrailBars, CurrentBar + 1);
				if (lookback < 1) return;

				if (isLong)
				{
					double lowest = double.MaxValue;
					for (int i = 0; i < lookback; i++)
						if (Low[i] < lowest) lowest = Low[i];
					newStop = Instrument.MasterInstrument.RoundToTickSize(lowest);
					if (newStop <= lastSyncedStop) return;   // no improvement
				}
				else
				{
					double highest = double.MinValue;
					for (int i = 0; i < lookback; i++)
						if (High[i] > highest) highest = High[i];
					newStop = Instrument.MasterInstrument.RoundToTickSize(highest);
					if (lastSyncedStop > 0 && newStop >= lastSyncedStop) return;  // no improvement
				}
			}

			if (newStop <= 0) return;

			DebugPrint("Trail[" + trailMode + "]: stop " + lastSyncedStop + " → " + newStop);
			lastSyncedStop = newStop;
			SafeChangeOrder(stopOrder, bracketQty, 0, newStop);
		}

		// ═══════════════════════════════════════════════════════════════════════
		//  AUTOPILOT LEVEL-TRAIL ENGINE
		// ═══════════════════════════════════════════════════════════════════════

		/// <summary>
		/// Builds an ordered array of price levels from the drawing tool
		/// [SL, Entry, TP1, TP2, ..., FinalTP] and moves the stop to the
		/// level behind the highest level price has touched.
		/// Aggressive = 1 level behind; Runner = 2 levels behind.
		/// </summary>
		private void ApplyAutopilotStop()
		{
			if (autopilotMode == AutopilotModeBeta.Off) return;
			if (!hasPosition || stopOrder == null || activeDrawingTool == null) return;
			if (Order.IsTerminalState(stopOrder.OrderState)) return;

			// ── Build ordered level array: [SL, Entry, TP1, TP2, ..., FinalTP] ──
			double slPrice    = activeDrawingTool.GetStopPrice();
			double entryPrice = activeDrawingTool.GetEntryPrice();
			double[] tpPrices = activeDrawingTool.GetTPPrices();

			if (slPrice <= 0 || entryPrice <= 0) return;

			var levels = new System.Collections.Generic.List<double>();
			levels.Add(slPrice);      // index 0 = SL
			levels.Add(entryPrice);   // index 1 = Entry

			if (tpPrices != null)
			{
				for (int i = 0; i < tpPrices.Length; i++)
					levels.Add(tpPrices[i]);   // index 2+ = TP levels
			}

			if (levels.Count < 3) return;  // need at least SL + Entry + 1 TP

			// ── Find highest level index that price has touched (wick) ──
			int highestReached = autopilotHighestLevelIdx;

			for (int i = 0; i < levels.Count; i++)
			{
				bool reached;
				if (isLong)
					reached = High[0] >= levels[i];
				else
					reached = Low[0] <= levels[i];

				if (reached && i > highestReached)
					highestReached = i;
			}

			// No improvement from last check
			if (highestReached <= autopilotHighestLevelIdx) return;
			autopilotHighestLevelIdx = highestReached;

			// ── Calculate target stop index ──
			int offset = (autopilotMode == AutopilotModeBeta.Aggressive) ? 1 : 2;
			int targetIdx = highestReached - offset;

			// Clamp: never move stop worse than original SL (index 0)
			if (targetIdx < 0) targetIdx = 0;

			// Don't move stop if target is still the SL level (no improvement)
			if (targetIdx == 0) return;

			double newStop = Instrument.MasterInstrument.RoundToTickSize(levels[targetIdx]);

			// One-way ratchet: only move stop toward profit
			if (isLong)
			{
				if (newStop <= lastSyncedStop) return;
			}
			else
			{
				if (lastSyncedStop > 0 && newStop >= lastSyncedStop) return;
			}

			DebugPrint("Autopilot[" + autopilotMode + "]: level " + highestReached
				+ "/" + (levels.Count - 1) + " → stop to level " + targetIdx
				+ " price=" + newStop);
			lastSyncedStop = newStop;
			SafeChangeOrder(stopOrder, bracketQty, 0, newStop);
		}

		// ═══════════════════════════════════════════════════════════════════════
		//  SAFE ORDER MODIFICATION HELPERS
		// ═══════════════════════════════════════════════════════════════════════

		/// <summary>
		/// Safely calls ChangeOrder only if the order is alive (not terminal).
		/// </summary>
		private void SafeChangeOrder(Order order, int qty, double limitPrice, double stopPrice)
		{
			if (order == null) return;
			if (Order.IsTerminalState(order.OrderState)) return;
			if (qty <= 0) return;

			try
			{
				ChangeOrder(order, qty, limitPrice, stopPrice);
			}
			catch (Exception ex)
			{
				DebugPrint("SafeChangeOrder ERROR: " + ex.Message);
			}
		}

		/// <summary>
		/// Safely cancels an order if it is alive.
		/// </summary>
		private void SafeCancelOrder(Order order)
		{
			if (order == null) return;
			if (Order.IsTerminalState(order.OrderState)) return;

			try
			{
				CancelOrder(order);
			}
			catch (Exception ex)
			{
				DebugPrint("SafeCancelOrder ERROR: " + ex.Message);
			}
		}

		// ═══════════════════════════════════════════════════════════════════════
		//  UI FLAG PROCESSOR
		// ═══════════════════════════════════════════════════════════════════════

		private void ProcessUIFlags()
		{
			// ── Single direction entry button ────────────────────────────────
			if (submitEntryRequested)
			{
				submitEntryRequested = false;
				if (activeDrawingTool != null && !hasPosition && entryOrder == null)
				{
					SubmitEntry(activeDrawingTool.IsLongTrade());
				}
			}

			if (breakEvenRequested)
			{
				breakEvenRequested = false;
				if (hasPosition)
				{
					magnetActive = false;
					autopilotMode = AutopilotModeBeta.Off;
					MoveStopToBreakeven();
				}
			}

			if (closePositionRequested)
			{
				closePositionRequested = false;
				if (hasPosition)
					ClosePosition();
			}

			if (adjustStopUpRequested)
			{
				adjustStopUpRequested = false;
				if (hasPosition)
				{
					autopilotMode = AutopilotModeBeta.Off;
					AdjustStopByTicks(SlAdjustTicks);
				}
			}

			if (adjustStopDownRequested)
			{
				adjustStopDownRequested = false;
				if (hasPosition)
				{
					autopilotMode = AutopilotModeBeta.Off;
					AdjustStopByTicks(-SlAdjustTicks);
				}
			}

			if (adjustTPUpRequested)
			{
				adjustTPUpRequested = false;
				if (hasPosition)
					AdjustTPByTicks(TpAdjustTicks);
			}

			if (adjustTPDownRequested)
			{
				adjustTPDownRequested = false;
				if (hasPosition)
					AdjustTPByTicks(-TpAdjustTicks);
			}

			if (coverRequested)
			{
				coverRequested = false;
				if (hasPosition && !coverInFlight)
				{
					coverInFlight = true;
					ExitPartial();
				}
			}

			// ── NATIVE BUTTON INTERCEPTS ──────────────────────────────────────
			if (nativeBuyMktRequested)
			{
				nativeBuyMktRequested = false;
				if (activeDrawingTool != null)
				{
					if (!hasPosition && entryOrder == null)
						SubmitEntryMarket(true);
					else if (hasPosition && isLong)
						SubmitAddOn(true, 0);
				}
			}
			if (nativeSellMktRequested)
			{
				nativeSellMktRequested = false;
				if (activeDrawingTool != null)
				{
					if (!hasPosition && entryOrder == null)
						SubmitEntryMarket(false);
					else if (hasPosition && !isLong)
						SubmitAddOn(false, 0);
				}
			}
			if (nativeBuyAskRequested)
			{
				nativeBuyAskRequested = false;
				if (activeDrawingTool != null)
				{
					if (!hasPosition && entryOrder == null)
						SubmitEntryAtPrice(true, nativeCapturedPrice);
					else if (hasPosition && isLong)
						SubmitAddOn(true, nativeCapturedPrice);
				}
			}
			if (nativeSellAskRequested)
			{
				nativeSellAskRequested = false;
				if (activeDrawingTool != null)
				{
					if (!hasPosition && entryOrder == null)
						SubmitEntryAtPrice(false, nativeCapturedPrice);
					else if (hasPosition && !isLong)
						SubmitAddOn(false, nativeCapturedPrice);
				}
			}
			if (nativeBuyBidRequested)
			{
				nativeBuyBidRequested = false;
				if (activeDrawingTool != null)
				{
					if (!hasPosition && entryOrder == null)
						SubmitEntryAtPrice(true, nativeCapturedPrice);
					else if (hasPosition && isLong)
						SubmitAddOn(true, nativeCapturedPrice);
				}
			}
			if (nativeSellBidRequested)
			{
				nativeSellBidRequested = false;
				if (activeDrawingTool != null)
				{
					if (!hasPosition && entryOrder == null)
						SubmitEntryAtPrice(false, nativeCapturedPrice);
					else if (hasPosition && !isLong)
						SubmitAddOn(false, nativeCapturedPrice);
				}
			}

			if (magnetToggleRequested)
			{
				magnetToggleRequested = false;
				magnetActive = !magnetActive;
				if (magnetActive)
				{
					trailMode = TrailModeBeta.Off;        // magnet takes over stop management
					autopilotMode = AutopilotModeBeta.Off; // magnet takes over stop management
				}
				DebugPrint("Magnet toggled: " + (magnetActive ? "ON" : "OFF"));
				UpdateButtonStates();
			}

			if (trailTicksRequested)
			{
				trailTicksRequested = false;
				if (trailMode == TrailModeBeta.Ticks)
				{
					trailMode = TrailModeBeta.Off;   // toggle off
				}
				else
				{
					trailMode      = TrailModeBeta.Ticks;
					magnetActive   = false;           // trail takes over stop management
					autopilotMode  = AutopilotModeBeta.Off; // trail takes over stop management
					trailHighWater = isLong ? Close[0] : Close[0];
				}
				DebugPrint("Trail mode: " + trailMode);
				UpdateButtonStates();
			}

			if (trailBarsRequested)
			{
				trailBarsRequested = false;
				if (trailMode == TrailModeBeta.Bars)
				{
					trailMode = TrailModeBeta.Off;   // toggle off
				}
				else
				{
					trailMode    = TrailModeBeta.Bars;
					magnetActive = false;             // trail takes over stop management
					autopilotMode = AutopilotModeBeta.Off; // trail takes over stop management
					trailHighWater = 0;               // not used in bar mode
				}
				DebugPrint("Trail mode: " + trailMode);
				UpdateButtonStates();
			}

			// ── AUTOPILOT TOGGLE HANDLERS ────────────────────────────────────
			if (autopilotAggressiveReq)
			{
				autopilotAggressiveReq = false;
				if (autopilotMode == AutopilotModeBeta.Aggressive)
				{
					autopilotMode = AutopilotModeBeta.Off;   // toggle off
				}
				else
				{
					autopilotMode            = AutopilotModeBeta.Aggressive;
					magnetActive             = false;             // autopilot owns the stop
					trailMode                = TrailModeBeta.Off; // autopilot owns the stop
					autopilotHighestLevelIdx = -1;                // reset ratchet
				}
				DebugPrint("Autopilot mode: " + autopilotMode);
				UpdateButtonStates();
			}

			if (autopilotRunnerReq)
			{
				autopilotRunnerReq = false;
				if (autopilotMode == AutopilotModeBeta.Runner)
				{
					autopilotMode = AutopilotModeBeta.Off;   // toggle off
				}
				else
				{
					autopilotMode            = AutopilotModeBeta.Runner;
					magnetActive             = false;             // autopilot owns the stop
					trailMode                = TrailModeBeta.Off; // autopilot owns the stop
					autopilotHighestLevelIdx = -1;                // reset ratchet
				}
				DebugPrint("Autopilot mode: " + autopilotMode);
				UpdateButtonStates();
			}
		}

		// ═══════════════════════════════════════════════════════════════════════
		//  ENTRY ORDER SUBMISSION (Unmanaged)
		// ═══════════════════════════════════════════════════════════════════════

		private void SubmitEntry(bool goLong)
		{
			if (activeDrawingTool == null || hasPosition || entryOrder != null) return;

			double stopPrice  = activeDrawingTool.GetStopPrice();
			double tpPrice    = GetSelectedTPPrice();
			double entryPrice = activeDrawingTool.GetEntryPrice();
			if (entryPrice <= 0 || stopPrice <= 0) return;
			if (TPSelection != TPLineChoiceBeta.NoTP && tpPrice <= 0) return;

			isLong = goLong;
			entryDirectionLong = goLong;
			int qty = activeDrawingTool.GetCalculatedQty();
			if (qty < 1) qty = 1;

			// Store prices for bracket submission after fill
			lastSyncedStop   = stopPrice;
			lastSyncedTarget = tpPrice;

			DebugPrint("SubmitEntry: " + (isLong ? "LONG" : "SHORT")
				+ " qty=" + qty + " entry=" + entryPrice
				+ " stop=" + stopPrice
				+ " tp=" + (TPSelection == TPLineChoiceBeta.NoTP ? "NONE" : tpPrice.ToString()));

			OrderAction action = isLong ? OrderAction.Buy : OrderAction.SellShort;

			if (EntryType == EntryOrderTypeBeta.Market)
			{
				entryOrder = SubmitOrderUnmanaged(0, action, OrderType.Market,
					qty, 0, 0, "", "SyncEntry");
			}
			else
			{
				entryPrice = Instrument.MasterInstrument.RoundToTickSize(entryPrice);
				entryOrder = SubmitOrderUnmanaged(0, action, OrderType.Limit,
					qty, entryPrice, 0, "", "SyncEntry");
			}
		}

		private void SubmitEntryMarket(bool goLong)
		{
			if (activeDrawingTool == null || hasPosition || entryOrder != null) return;

			double stopPrice = activeDrawingTool.GetStopPrice();
			double tpPrice   = GetSelectedTPPrice();
			if (stopPrice <= 0) return;
			if (TPSelection != TPLineChoiceBeta.NoTP && tpPrice <= 0) return;

			isLong = goLong;
			entryDirectionLong = goLong;
			int qty = activeDrawingTool.GetCalculatedQty();
			if (qty < 1) qty = 1;

			lastSyncedStop   = stopPrice;
			lastSyncedTarget = tpPrice;

			OrderAction action = isLong ? OrderAction.Buy : OrderAction.SellShort;
			entryOrder = SubmitOrderUnmanaged(0, action, OrderType.Market,
				qty, 0, 0, "", "SyncEntry");
		}

		private void SubmitEntryAtPrice(bool goLong, double price)
		{
			if (activeDrawingTool == null || hasPosition || entryOrder != null || price <= 0) return;

			double stopPrice = activeDrawingTool.GetStopPrice();
			double tpPrice   = GetSelectedTPPrice();
			if (stopPrice <= 0) return;
			if (TPSelection != TPLineChoiceBeta.NoTP && tpPrice <= 0) return;

			isLong = goLong;
			entryDirectionLong = goLong;
			int qty = activeDrawingTool.GetCalculatedQty();
			if (qty < 1) qty = 1;
			price = Instrument.MasterInstrument.RoundToTickSize(price);

			lastSyncedStop   = stopPrice;
			lastSyncedTarget = tpPrice;

			OrderAction action = isLong ? OrderAction.Buy : OrderAction.SellShort;
			entryOrder = SubmitOrderUnmanaged(0, action, OrderType.Limit,
				qty, price, 0, "", "SyncEntry");
		}

		// ═══════════════════════════════════════════════════════════════════════
		//  BRACKET SUBMISSION (after entry fill)
		// ═══════════════════════════════════════════════════════════════════════

		/// <summary>
		/// Submits SL + TP bracket orders after entry fill.
		/// Called from OnExecutionUpdate when entry order fills.
		/// </summary>
		private void SubmitBrackets(int qty)
		{
			if (qty <= 0) return;

			ocoCounter++;
			bracketQty = qty;

			OrderAction exitAction = isLong ? OrderAction.Sell : OrderAction.BuyToCover;

			// Submit Stop Loss (StopMarket)
			stopOrder = SubmitOrderUnmanaged(0, exitAction, OrderType.StopMarket,
				qty, 0, lastSyncedStop, ActiveOCO, "SyncStop");

			DebugPrint("Bracket SL submitted: qty=" + qty + " price=" + lastSyncedStop
				+ " OCO=" + ActiveOCO);

			// Submit Take Profit (Limit) — skip in NoTP mode
			if (TPSelection != TPLineChoiceBeta.NoTP && lastSyncedTarget > 0)
			{
				tpOrder = SubmitOrderUnmanaged(0, exitAction, OrderType.Limit,
					qty, lastSyncedTarget, 0, ActiveOCO, "SyncTP");

				DebugPrint("Bracket TP submitted: qty=" + qty + " price=" + lastSyncedTarget
					+ " OCO=" + ActiveOCO);
			}
		}

		/// <summary>
		/// Updates existing bracket qty (for partial entry fills).
		/// </summary>
		private void UpdateBracketQty(int newQty)
		{
			if (newQty <= 0) return;
			bracketQty = newQty;

			SafeChangeOrder(stopOrder, newQty, 0, lastSyncedStop);
			if (TPSelection != TPLineChoiceBeta.NoTP)
				SafeChangeOrder(tpOrder, newQty, lastSyncedTarget, 0);

			DebugPrint("Bracket qty updated to " + newQty);
		}

		// ═══════════════════════════════════════════════════════════════════════
		//  SCALE-IN: Add to existing position
		// ═══════════════════════════════════════════════════════════════════════

		private void SubmitAddOn(bool goLong, double limitPrice)
		{
			if (!hasPosition || activeDrawingTool == null) return;
			if (goLong != isLong) return;  // only same direction
			if (addOnOrder != null && !Order.IsTerminalState(addOnOrder.OrderState)) return;

			int addQty = activeDrawingTool.GetCalculatedQty();
			if (addQty < 1) addQty = 1;

			OrderAction action = isLong ? OrderAction.Buy : OrderAction.SellShort;

			DebugPrint("AddOn: " + (isLong ? "LONG" : "SHORT") + " qty=" + addQty);

			if (limitPrice <= 0)
			{
				addOnOrder = SubmitOrderUnmanaged(0, action, OrderType.Market,
					addQty, 0, 0, "", "SyncAddOn");
			}
			else
			{
				limitPrice = Instrument.MasterInstrument.RoundToTickSize(limitPrice);
				addOnOrder = SubmitOrderUnmanaged(0, action, OrderType.Limit,
					addQty, limitPrice, 0, "", "SyncAddOn");
			}
		}

		// ═══════════════════════════════════════════════════════════════════════
		//  TRADE MANAGEMENT ACTIONS
		// ═══════════════════════════════════════════════════════════════════════

		private void MoveStopToBreakeven()
		{
			if (!hasPosition || stopOrder == null) return;

			double bePrice = Instrument.MasterInstrument.RoundToTickSize(Position.AveragePrice);
			DebugPrint("Breakeven: moving stop to " + bePrice);
			lastSyncedStop = bePrice;
			SafeChangeOrder(stopOrder, bracketQty, 0, bePrice);
		}

		private void AdjustStopByTicks(int ticks)
		{
			if (!hasPosition || stopOrder == null) return;

			double delta   = ticks * TickSize;
			double newStop = isLong
				? lastSyncedStop - delta
				: lastSyncedStop + delta;

			newStop = Instrument.MasterInstrument.RoundToTickSize(newStop);

			DebugPrint("AdjustStop: " + lastSyncedStop + " → " + newStop);
			lastSyncedStop = newStop;
			SafeChangeOrder(stopOrder, bracketQty, 0, newStop);
		}

		private void AdjustTPByTicks(int ticks)
		{
			if (!hasPosition || tpOrder == null) return;
			if (TPSelection == TPLineChoiceBeta.NoTP) return;

			double delta = ticks * TickSize;
			double newTP = isLong
				? lastSyncedTarget + delta
				: lastSyncedTarget - delta;

			newTP = Instrument.MasterInstrument.RoundToTickSize(newTP);

			DebugPrint("AdjustTP: " + lastSyncedTarget + " → " + newTP);
			lastSyncedTarget = newTP;
			SafeChangeOrder(tpOrder, bracketQty, newTP, 0);
		}

		private void ClosePosition()
		{
			// Use the ACCOUNT position so we also flatten contracts that came
			// from manual chart orders the strategy never tracked.
			int qty = GetAccountPositionQty();
			if (qty <= 0 && hasPosition) qty = bracketQty;  // fallback
			if (qty <= 0)
			{
				DebugPrint("ClosePosition: nothing to close");
				CancelAllInstrumentOrders();
				return;
			}

			DebugPrint("ClosePosition: cancelling ALL working orders + market exit qty=" + qty);

			// Cancel every working order on this instrument — strategy brackets
			// AND any manual orders the user placed via Chart Trader.
			CancelAllInstrumentOrders();

			OrderAction action = isLong ? OrderAction.Sell : OrderAction.BuyToCover;
			SubmitOrderUnmanaged(0, action, OrderType.Market, qty, 0, 0, "", "CloseAll");
		}

		/// <summary>
		/// Cancels every working order on the current instrument across the
		/// account, including manually placed chart orders that the strategy
		/// never submitted. Strategy-owned orders are cancelled via the
		/// existing references so OnOrderUpdate cleanup still fires.
		/// </summary>
		private void CancelAllInstrumentOrders()
		{
			// Strategy-owned brackets first (keeps internal state coherent)
			SafeCancelOrder(stopOrder);
			SafeCancelOrder(tpOrder);
			SafeCancelOrder(entryOrder);
			SafeCancelOrder(addOnOrder);

			if (Account == null || Instrument == null) return;
			try
			{
				var working = Account.Orders
					.Where(o => o != null
						&& o.Instrument != null
						&& o.Instrument == Instrument
						&& !Order.IsTerminalState(o.OrderState))
					.ToList();

				foreach (var o in working)
				{
					try { Account.Cancel(new List<Order> { o }); }
					catch (Exception ex) { DebugPrint("CancelAll: " + ex.Message); }
				}
			}
			catch (Exception ex)
			{
				DebugPrint("CancelAllInstrumentOrders: " + ex.Message);
			}
		}

		private void ExitPartial()
		{
			if (!hasPosition) return;

			int qty = Math.Min(PartialExitQty, Position.Quantity);
			if (qty <= 0) return;

			DebugPrint("ExitPartial: " + qty + " contracts");

			OrderAction action = isLong ? OrderAction.Sell : OrderAction.BuyToCover;
			coverOrder = SubmitOrderUnmanaged(0, action, OrderType.Market,
				qty, 0, 0, "", "PartialExit");
		}

		// ═══════════════════════════════════════════════════════════════════════
		//  POSITION MONITORING
		// ═══════════════════════════════════════════════════════════════════════

		private void MonitorPosition()
		{
			// Use the ACCOUNT position as source of truth — Strategy.Position can
			// drift negative after a CloseAll that flattens manual add-on contracts
			// the strategy never tracked, which would prevent the Flat check.
			bool accountFlat = GetAccountPositionQty() == 0;
			bool stratFlat   = Position.MarketPosition == MarketPosition.Flat;

			if ((accountFlat || stratFlat) && hasPosition)
			{
				DebugPrint("Position went flat — resetting state");
				StopTradeTimer();

				// Cancel any lingering bracket orders (safety)
				SafeCancelOrder(stopOrder);
				SafeCancelOrder(tpOrder);

				ResetState();
			}
		}

		// ═══════════════════════════════════════════════════════════════════════
		//  BRACKET QTY RECONCILIATION (external / manual fills)
		// ═══════════════════════════════════════════════════════════════════════

		/// <summary>
		/// Detects any mismatch between the live NT8 position quantity and the
		/// strategy's internal bracketQty. This covers fills from manually placed
		/// chart orders that the strategy never submitted — e.g. a manual Buy Stop
		/// Limit that partially covers an existing position.
		///
		/// Called from OnBarUpdate after MonitorPosition().
		/// </summary>
		/// <summary>
		/// Returns the absolute quantity of the account-side position for this
		/// strategy's instrument. Unlike Strategy.Position.Quantity (which only
		/// reflects orders the strategy itself submitted), this includes any
		/// fills from manual chart orders the user placed directly.
		/// </summary>
		private int GetAccountPositionQty()
		{
			if (Account == null || Instrument == null) return 0;
			try
			{
				var pos = Account.Positions.FirstOrDefault(p =>
					p.Instrument != null && p.Instrument == Instrument);
				if (pos == null) return 0;
				return Math.Abs(pos.Quantity);
			}
			catch
			{
				return 0;
			}
		}

		/// <summary>
		/// Cancels any working order on this instrument that came from an ATM
		/// template. Detection heuristic: orders with no NinjaScript Strategy
		/// owner AND a non-empty OcoId are virtually always ATM-created
		/// (Chart Trader ATMs always group entry+SL+TP with an OCO id, while
		/// manual single orders carry no OcoId). Strategy-owned orders are
		/// excluded because their Order.Strategy == this strategy.
		/// </summary>
		private void BlockAtmOrders()
		{
			if (Account == null || Instrument == null) return;
			try
			{
				// ATM templates always group their orders under an OCO id.
				// Our own strategy brackets use OCO ids that start with
				// ocoSessionPrefix, so any working order on this instrument
				// with an OCO that does NOT start with that prefix is treated
				// as ATM-created and cancelled. Manual single orders carry no
				// OCO id and are left untouched.
				var atmLike = Account.Orders
					.Where(o => o != null
						&& o.Instrument != null
						&& o.Instrument == Instrument
						&& !string.IsNullOrEmpty(o.Oco)
						&& !o.Oco.StartsWith(ocoSessionPrefix)
						&& !Order.IsTerminalState(o.OrderState))
					.ToList();

				foreach (var o in atmLike)
				{
					try
					{
						DebugPrint("BlockAtmOrders: cancelling ATM order name="
							+ o.Name + " oco=" + o.Oco);
						Account.Cancel(new List<Order> { o });
					}
					catch (Exception ex) { DebugPrint("BlockAtm: " + ex.Message); }
				}
			}
			catch (Exception ex)
			{
				DebugPrint("BlockAtmOrders: " + ex.Message);
			}
		}

		private void ReconcileBracketQty()
		{
			// Only act when we believe we have a position and brackets are live
			if (!hasPosition) return;
			if (stopOrder == null) return;
			if (Order.IsTerminalState(stopOrder.OrderState)) return;

			// ── In-flight guard ───────────────────────────────────────────────
			// NT8 can call OnBarUpdate on a new tick before OnExecutionUpdate
			// fires for a fill on that same tick.  If a strategy order is still
			// working, Position.Quantity may already reflect the fill while
			// bracketQty has not yet been updated by OnExecutionUpdate.
			// Acting here would cause double-count (AddOn) or double-subtract
			// (PartialExit).  Skip until all strategy orders are settled.
			if (entryOrder != null && !Order.IsTerminalState(entryOrder.OrderState)) return;
			if (addOnOrder != null && !Order.IsTerminalState(addOnOrder.OrderState)) return;
			if (coverInFlight) return;

			// Query the ACCOUNT position, not the strategy's virtual Position.
			// Manual chart orders fill into the account but never into
			// Strategy.Position, so Position.Quantity would miss them.
			int liveQty = GetAccountPositionQty();
			if (liveQty <= 0) return;          // flat or not yet reported

			if (liveQty == bracketQty) return; // nothing to do

			DebugPrint("ReconcileBracketQty: bracketQty=" + bracketQty
				+ " → liveQty=" + liveQty + " (external fill detected)");

			bracketQty = liveQty;
			SafeChangeOrder(stopOrder, liveQty, 0, lastSyncedStop);
			if (TPSelection != TPLineChoiceBeta.NoTP && tpOrder != null
				&& !Order.IsTerminalState(tpOrder.OrderState))
			{
				SafeChangeOrder(tpOrder, liveQty, lastSyncedTarget, 0);
			}
		}

		// ═══════════════════════════════════════════════════════════════════════
		//  ORDER UPDATE (required for unmanaged — track order objects)
		// ═══════════════════════════════════════════════════════════════════════

		protected override void OnOrderUpdate(Order order, double limitPrice,
			double stopPrice, int quantity, int filled,
			double averageFillPrice, OrderState orderState, DateTime time,
			ErrorCode error, string comment)
		{
			// ── Assign order references by name ──────────────────────────────
			// NT8 requirement: order objects from SubmitOrderUnmanaged may not
			// be the same reference as in OnOrderUpdate. Always reassign here.

			if (order.Name == "SyncEntry")
				entryOrder = order;
			else if (order.Name == "SyncStop")
				stopOrder = order;
			else if (order.Name == "SyncTP")
				tpOrder = order;
			else if (order.Name == "PartialExit")
				coverOrder = order;
			else if (order.Name == "SyncAddOn")
				addOnOrder = order;

			// ── Null out terminal orders ──────────────────────────────────────
			if (Order.IsTerminalState(orderState))
			{
				if (order.Name == "SyncEntry" && entryOrder == order)
					entryOrder = null;
				else if (order.Name == "SyncStop" && stopOrder == order)
					stopOrder = null;
				else if (order.Name == "SyncTP" && tpOrder == order)
					tpOrder = null;
				else if (order.Name == "PartialExit" && coverOrder == order)
				{
					// If the cover order was cancelled or rejected (never filled),
					// release the coverInFlight lock so the COVER button works again.
					if (orderState == OrderState.Cancelled || orderState == OrderState.Rejected)
						coverInFlight = false;
					coverOrder = null;
				}
				else if (order.Name == "SyncAddOn" && addOnOrder == order)
					addOnOrder = null;
			}
		}

		// ═══════════════════════════════════════════════════════════════════════
		//  EXECUTION UPDATE (detect fills, manage brackets)
		// ═══════════════════════════════════════════════════════════════════════

		protected override void OnExecutionUpdate(Execution execution,
			string executionId, double price, int quantity,
			MarketPosition marketPosition, string orderId, DateTime time)
		{
			if (execution.Order == null) return;

			string orderName = execution.Order.Name;

			// ── Entry fill ───────────────────────────────────────────────────
			if (orderName == "SyncEntry")
			{
				if (execution.Order.OrderState == OrderState.PartFilled)
				{
					int filledSoFar = execution.Order.Filled;

					if (!hasPosition)
					{
						// First partial fill — submit brackets for filled qty
						hasPosition    = true;
						entryFillPrice = execution.Order.AverageFillPrice;
						StartTradeTimer();

						DebugPrint("Entry PART FILLED: price=" + entryFillPrice
							+ " filled=" + filledSoFar);

						SubmitBrackets(filledSoFar);
					}
					else
					{
						// Subsequent partial — update bracket qty
						entryFillPrice = execution.Order.AverageFillPrice;

						DebugPrint("Entry PART FILLED (update): filled=" + filledSoFar);

						UpdateBracketQty(filledSoFar);
					}
				}
				else if (execution.Order.OrderState == OrderState.Filled)
				{
					int totalFilled = execution.Order.Filled;
					entryFillPrice  = execution.Order.AverageFillPrice;

					if (!hasPosition)
					{
						// Full fill — no partial fills occurred
						hasPosition = true;
						StartTradeTimer();

						DebugPrint("Entry FILLED: price=" + entryFillPrice
							+ " qty=" + totalFilled);

						SubmitBrackets(totalFilled);
					}
					else
					{
						// Final fill after partials — update to full qty
						DebugPrint("Entry FILLED (final): qty=" + totalFilled);

						UpdateBracketQty(totalFilled);
					}
				}
			}

			// ── Add-on fill ──────────────────────────────────────────────────
			if (orderName == "SyncAddOn")
			{
				if (execution.Order.OrderState == OrderState.Filled
					|| execution.Order.OrderState == OrderState.PartFilled)
				{
					entryFillPrice = Position.AveragePrice;
					int newBracketQty = bracketQty + execution.Quantity;

					DebugPrint("AddOn FILLED: +" + execution.Quantity
						+ " avgPrice=" + entryFillPrice
						+ " newBracketQty=" + newBracketQty);

					bracketQty = newBracketQty;
					SafeChangeOrder(stopOrder, newBracketQty, 0, lastSyncedStop);
					if (TPSelection != TPLineChoiceBeta.NoTP)
						SafeChangeOrder(tpOrder, newBracketQty, lastSyncedTarget, 0);
				}
			}

			// ── Partial exit (COVER) fill ─────────────────────────────────────
			if (orderName == "PartialExit")
			{
				if (execution.Order.OrderState == OrderState.Filled
					|| execution.Order.OrderState == OrderState.PartFilled)
				{
					coverInFlight = false;

					int exitedQty     = execution.Quantity;
					int newBracketQty = bracketQty - exitedQty;

					DebugPrint("PartialExit FILLED: -" + exitedQty
						+ " newBracketQty=" + newBracketQty
						+ " remaining=" + (Position.MarketPosition != MarketPosition.Flat
							? Position.Quantity.ToString() : "FLAT"));

					if (newBracketQty > 0)
					{
						bracketQty = newBracketQty;
						SafeChangeOrder(stopOrder, newBracketQty, 0, lastSyncedStop);
						if (TPSelection != TPLineChoiceBeta.NoTP)
							SafeChangeOrder(tpOrder, newBracketQty, lastSyncedTarget, 0);
					}
					else
					{
						// Fully exited via cover — cancel remaining brackets
						bracketQty = 0;
						SafeCancelOrder(stopOrder);
						SafeCancelOrder(tpOrder);
					}
				}
			}

			// ── SL or TP fill → position goes flat ───────────────────────────
			// The OCO string handles cancelling the other order automatically.
			// MonitorPosition() in OnBarUpdate will detect flat and reset state.
			if (orderName == "SyncStop" || orderName == "SyncTP")
			{
				if (execution.Order.OrderState == OrderState.Filled)
				{
					DebugPrint(orderName + " FILLED — position should be flat");
					// MonitorPosition() will handle ResetState()
				}
			}

			// ── CloseAll market exit fill → force reset ──────────────────────
			// Strategy.Position may not show Flat (it can go negative if the
			// exit flattened manual add-on contracts), so reset explicitly.
			if (orderName == "CloseAll"
				&& execution.Order.OrderState == OrderState.Filled)
			{
				DebugPrint("CloseAll FILLED — forcing state reset");
				StopTradeTimer();
				SafeCancelOrder(stopOrder);
				SafeCancelOrder(tpOrder);
				ResetState();
			}
		}

		// ═══════════════════════════════════════════════════════════════════════
		//  STATE RESET
		// ═══════════════════════════════════════════════════════════════════════

		private void ResetState()
		{
			hasPosition          = false;
			isLong               = true;
			entryFillPrice       = 0;
			lastSyncedStop       = 0;
			lastSyncedTarget     = 0;
			trackedChangeVersion = -1;
			coverInFlight        = false;
			bracketQty           = 0;
			trailMode            = TrailModeBeta.Off;
			trailHighWater       = 0;
			autopilotMode            = AutopilotModeBeta.Off;
			autopilotHighestLevelIdx = -1;

			// Null out order references (they should already be terminal)
			entryOrder = null;
			stopOrder  = null;
			tpOrder    = null;
			coverOrder = null;
			addOnOrder = null;
		}

		// ═══════════════════════════════════════════════════════════════════════
		//  TRADE TIMER HELPERS
		// ═══════════════════════════════════════════════════════════════════════

		private void StartTradeTimer()
		{
			if (ChartControl == null) return;
			ChartControl.Dispatcher.InvokeAsync((Action)(() =>
			{
				tradeStartTime = DateTime.Now;
				timerRunning   = true;

				if (timerClock == null)
				{
					timerClock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
					timerClock.Tick += (s, e) =>
					{
						try
						{
							if (!timerRunning || lblTradeTimer == null) return;
							TimeSpan elapsed = DateTime.Now - tradeStartTime;
							string formatted = string.Format("{0:D2}:{1:D2}:{2:D2}",
								(int)elapsed.TotalHours,
								elapsed.Minutes,
								elapsed.Seconds);
							lblTradeTimer.Text       = "⏱  " + formatted;
							lblTradeTimer.Foreground = new SolidColorBrush(Color.FromRgb(255, 200, 50));
						}
						catch { }
					};
				}
				timerClock.Start();
			}));
		}

		private void StopTradeTimer()
		{
			if (ChartControl == null) return;
			ChartControl.Dispatcher.InvokeAsync((Action)(() =>
			{
				timerRunning = false;
				if (timerClock != null) timerClock.Stop();

				if (lblTradeTimer != null)
				{
					TimeSpan elapsed = DateTime.Now - tradeStartTime;
					lastTradeDuration = string.Format("{0:D2}:{1:D2}:{2:D2}",
						(int)elapsed.TotalHours,
						elapsed.Minutes,
						elapsed.Seconds);
					lblTradeTimer.Text       = "Last: " + lastTradeDuration;
					lblTradeTimer.Foreground = new SolidColorBrush(Color.FromRgb(140, 140, 140));
				}
			}));
		}

		private void DisposeTradeTimer()
		{
			if (timerClock != null)
			{
				timerClock.Stop();
				timerClock = null;
			}
			timerRunning = false;
		}

		// ═══════════════════════════════════════════════════════════════════════
		//  WPF CHART TRADER BUTTON INJECTION
		//  (TradeSaber proven pattern: State.Historical → CreateWPFControls)
		// ═══════════════════════════════════════════════════════════════════════

		protected void CreateWPFControls()
		{
			chartWindow = Window.GetWindow(ChartControl.Parent) as Chart;
			if (chartWindow == null) return;

			chartTraderGrid = (chartWindow.FindFirst("ChartWindowChartTraderControl")
				as ChartTrader)?.Content as System.Windows.Controls.Grid;

			if (chartTraderGrid == null) return;

			chartTraderButtonsGrid = chartTraderGrid.Children[0]
				as System.Windows.Controls.Grid;

			lowerButtonsGrid = new System.Windows.Controls.Grid();
			lowerButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition());
			lowerButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition());

			lowerButtonsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) }); // Row 0  — RISK-REWARD STATUS header
			lowerButtonsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(4)  }); // Row 1  — separator
			lowerButtonsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(27) }); // Row 2  — entry button
			lowerButtonsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(4)  }); // Row 3  — separator
			lowerButtonsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(25) }); // Row 4  — MAGNET
			lowerButtonsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(4)  }); // Row 5  — separator
			lowerButtonsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) }); // Row 6  — TP TARGET header
			lowerButtonsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(25) }); // Row 7  — TP selector buttons
			lowerButtonsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(4)  }); // Row 8  — separator
			lowerButtonsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) }); // Row 9  — STOPWATCH header
			lowerButtonsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(25) }); // Row 10 — timer value
			lowerButtonsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(4)  }); // Row 11 — separator
			lowerButtonsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) }); // Row 12 — QUICK BUTTONS header
			lowerButtonsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(25) }); // Row 13 — BreakEven | Close Pos
			lowerButtonsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(25) }); // Row 14 — Stop + | TP +
			lowerButtonsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(25) }); // Row 15 — Stop - | TP -
			lowerButtonsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(25) }); // Row 16 — COVER
			lowerButtonsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(4)  }); // Row 17 — separator
			lowerButtonsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) }); // Row 18 — AUTOPILOT header
			lowerButtonsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(22) }); // Row 19 — status indicator (ON/OFF)
			lowerButtonsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(25) }); // Row 20 — AGGRESSIVE | RUNNER
			lowerButtonsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(25) }); // Row 21 — TRAIL xT | TRAIL xB

			addedRow = new RowDefinition() { Height = new GridLength(440) };
			Style basicButtonStyle = Application.Current.FindResource("BasicEntryButton") as Style;

			// ── Teal theme colors (shared across all headers + separators) ────
			var tealBrush         = new SolidColorBrush(Color.FromRgb(20, 80, 90));
			var tealTextBrush     = new SolidColorBrush(Color.FromRgb(180, 220, 225));
			var whiteBrush        = new SolidColorBrush(Colors.White);
			var slateBlueGrayBrush = new SolidColorBrush(Color.FromRgb(60, 80, 100));

			// ── Helper: create a full-width slate separator Rectangle ─────────
			System.Func<System.Windows.Shapes.Rectangle> MakeSep = () =>
				new System.Windows.Shapes.Rectangle
				{
					Height              = 2,
					Fill                = slateBlueGrayBrush,
					HorizontalAlignment = HorizontalAlignment.Stretch,
					VerticalAlignment   = VerticalAlignment.Center,
					Margin              = new Thickness(2, 0, 2, 0)
				};

			// ── Helper: create a full-width teal header TextBlock ─────────────
			System.Func<string, System.Windows.Controls.TextBlock> MakeHeader = (text) =>
				new System.Windows.Controls.TextBlock
				{
					Text                = text,
					FontSize            = 10,
					FontWeight          = FontWeights.Bold,
					HorizontalAlignment = HorizontalAlignment.Stretch,
					VerticalAlignment   = VerticalAlignment.Center,
					TextAlignment       = TextAlignment.Center,
					Padding             = new Thickness(0, 2, 0, 2),
					Foreground          = tealTextBrush,
					Background          = tealBrush
				};

			// ── Row 0: RISK-REWARD STATUS section header ──────────────────────
			var lblRRStatus = MakeHeader("RISK-REWARD STATUS");
			System.Windows.Controls.Grid.SetRow(lblRRStatus, 0);
			System.Windows.Controls.Grid.SetColumn(lblRRStatus, 0);
			System.Windows.Controls.Grid.SetColumnSpan(lblRRStatus, 2);

			// ── Row 1: Separator ─────────────────────────────────────────────
			var sep0 = MakeSep();
			System.Windows.Controls.Grid.SetRow(sep0, 1);
			System.Windows.Controls.Grid.SetColumn(sep0, 0);
			System.Windows.Controls.Grid.SetColumnSpan(sep0, 2);

			// ── Row 2: Entry button ───────────────────────────────────────────
			btnEntry = new System.Windows.Controls.Button()
			{
				Content             = "LONG",
				Height              = 25,
				Margin              = new Thickness(2, 1, 2, 1),
				Padding             = new Thickness(0),
				FontSize            = 11,
				FontWeight          = FontWeights.Bold,
				HorizontalContentAlignment = HorizontalAlignment.Center,
				Background          = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
				Foreground          = whiteBrush
			};
			System.Windows.Controls.Grid.SetRow(btnEntry, 2);
			System.Windows.Controls.Grid.SetColumn(btnEntry, 0);
			System.Windows.Controls.Grid.SetColumnSpan(btnEntry, 2);

			// ── Row 3: Separator ─────────────────────────────────────────────
			var sep1 = MakeSep();
			System.Windows.Controls.Grid.SetRow(sep1, 3);
			System.Windows.Controls.Grid.SetColumn(sep1, 0);
			System.Windows.Controls.Grid.SetColumnSpan(sep1, 2);

			// ── Row 4: MAGNET button ──────────────────────────────────────────
			btnMagnet = new System.Windows.Controls.Button()
			{
				Content             = "MAGNET ON",
				Height              = 25,
				Margin              = new Thickness(2, 1, 2, 1),
				Padding             = new Thickness(0),
				FontSize            = 10,
				FontWeight          = FontWeights.Bold,
				HorizontalContentAlignment = HorizontalAlignment.Center,
				Background          = new SolidColorBrush(Color.FromRgb(0, 150, 80)),
				Foreground          = whiteBrush
			};
			System.Windows.Controls.Grid.SetRow(btnMagnet, 4);
			System.Windows.Controls.Grid.SetColumn(btnMagnet, 0);
			System.Windows.Controls.Grid.SetColumnSpan(btnMagnet, 2);

			// ── Row 5: TRAIL selector — buttons created here, placed later in AUTOPILOT section ──
			btnTrailTicks = new System.Windows.Controls.Button()
			{
				Content             = "TRAIL " + TrailTicks + "T",
				Height              = 25,
				Margin              = new Thickness(2, 1, 1, 1),
				Padding             = new Thickness(0),
				FontSize            = 10,
				FontWeight          = FontWeights.Bold,
				HorizontalContentAlignment = HorizontalAlignment.Center,
				Background          = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
				Foreground          = whiteBrush
			};
			btnTrailBars = new System.Windows.Controls.Button()
			{
				Content             = "TRAIL " + TrailBars + "B",
				Height              = 25,
				Margin              = new Thickness(1, 1, 2, 1),
				Padding             = new Thickness(0),
				FontSize            = 10,
				FontWeight          = FontWeights.Bold,
				HorizontalContentAlignment = HorizontalAlignment.Center,
				Background          = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
				Foreground          = whiteBrush
			};
			var trailSelectorGrid = new System.Windows.Controls.Grid();
			trailSelectorGrid.ColumnDefinitions.Add(new ColumnDefinition());
			trailSelectorGrid.ColumnDefinitions.Add(new ColumnDefinition());
			System.Windows.Controls.Grid.SetColumn(btnTrailTicks, 0);
			System.Windows.Controls.Grid.SetColumn(btnTrailBars,  1);
			trailSelectorGrid.Children.Add(btnTrailTicks);
			trailSelectorGrid.Children.Add(btnTrailBars);
			System.Windows.Controls.Grid.SetRow(trailSelectorGrid, 21);  // AUTOPILOT section
			System.Windows.Controls.Grid.SetColumn(trailSelectorGrid, 0);
			System.Windows.Controls.Grid.SetColumnSpan(trailSelectorGrid, 2);

			// ── Row 17: Separator (between QUICK BUTTONS and AUTOPILOT) ──────
			var sep5 = MakeSep();
			System.Windows.Controls.Grid.SetRow(sep5, 17);
			System.Windows.Controls.Grid.SetColumn(sep5, 0);
			System.Windows.Controls.Grid.SetColumnSpan(sep5, 2);

			// ── Row 18: AUTOPILOT header ─────────────────────────────────────
			var lblAutopilotHeader = MakeHeader("AUTOPILOT");
			System.Windows.Controls.Grid.SetRow(lblAutopilotHeader, 18);
			System.Windows.Controls.Grid.SetColumn(lblAutopilotHeader, 0);
			System.Windows.Controls.Grid.SetColumnSpan(lblAutopilotHeader, 2);

			// ── Row 19: Status indicator (● OFF / ● ON) ─────────────────────
			lblAutopilotStatus = new System.Windows.Controls.TextBlock()
			{
				Text                = "● OFF",
				FontSize            = 10,
				FontWeight          = FontWeights.Bold,
				HorizontalAlignment = HorizontalAlignment.Stretch,
				VerticalAlignment   = VerticalAlignment.Center,
				TextAlignment       = TextAlignment.Center,
				Padding             = new Thickness(0, 2, 0, 2),
				Foreground          = new SolidColorBrush(Color.FromRgb(200, 50, 50)),
				Background          = new SolidColorBrush(Color.FromRgb(30, 30, 30))
			};
			System.Windows.Controls.Grid.SetRow(lblAutopilotStatus, 19);
			System.Windows.Controls.Grid.SetColumn(lblAutopilotStatus, 0);
			System.Windows.Controls.Grid.SetColumnSpan(lblAutopilotStatus, 2);

			// ── Row 20: AGGRESSIVE | RUNNER buttons ─────────────────────────
			btnAutopilotAggressive = new System.Windows.Controls.Button()
			{
				Content             = "AGGRESSIVE",
				Height              = 25,
				Margin              = new Thickness(2, 1, 1, 1),
				Padding             = new Thickness(0),
				FontSize            = 10,
				FontWeight          = FontWeights.Bold,
				HorizontalContentAlignment = HorizontalAlignment.Center,
				Background          = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
				Foreground          = whiteBrush
			};
			btnAutopilotRunner = new System.Windows.Controls.Button()
			{
				Content             = "RUNNER",
				Height              = 25,
				Margin              = new Thickness(1, 1, 2, 1),
				Padding             = new Thickness(0),
				FontSize            = 10,
				FontWeight          = FontWeights.Bold,
				HorizontalContentAlignment = HorizontalAlignment.Center,
				Background          = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
				Foreground          = whiteBrush
			};
			var autopilotSelectorGrid = new System.Windows.Controls.Grid();
			autopilotSelectorGrid.ColumnDefinitions.Add(new ColumnDefinition());
			autopilotSelectorGrid.ColumnDefinitions.Add(new ColumnDefinition());
			System.Windows.Controls.Grid.SetColumn(btnAutopilotAggressive, 0);
			System.Windows.Controls.Grid.SetColumn(btnAutopilotRunner,     1);
			autopilotSelectorGrid.Children.Add(btnAutopilotAggressive);
			autopilotSelectorGrid.Children.Add(btnAutopilotRunner);
			System.Windows.Controls.Grid.SetRow(autopilotSelectorGrid, 20);
			System.Windows.Controls.Grid.SetColumn(autopilotSelectorGrid, 0);
			System.Windows.Controls.Grid.SetColumnSpan(autopilotSelectorGrid, 2);

			// ── Row 5: Separator ─────────────────────────────────────────────
			var sep2 = MakeSep();
			System.Windows.Controls.Grid.SetRow(sep2, 5);
			System.Windows.Controls.Grid.SetColumn(sep2, 0);
			System.Windows.Controls.Grid.SetColumnSpan(sep2, 2);

			// ── Row 6: TP TARGET header ───────────────────────────────────────
			lblTPHeader = MakeHeader("TP TARGET");
			System.Windows.Controls.Grid.SetRow(lblTPHeader, 6);
			System.Windows.Controls.Grid.SetColumn(lblTPHeader, 0);
			System.Windows.Controls.Grid.SetColumnSpan(lblTPHeader, 2);

			// ── Row 5: TP selector — nested Grid for 3 equal columns ────────
			btnTP1RR = new System.Windows.Controls.Button()
			{
				Content             = "1RR",
				Height              = 25,
				Margin              = new Thickness(2, 1, 1, 1),
				Padding             = new Thickness(0),
				FontSize            = 10,
				FontWeight          = FontWeights.Bold,
				HorizontalContentAlignment = HorizontalAlignment.Center,
				Foreground          = whiteBrush
			};
			btnTPLast = new System.Windows.Controls.Button()
			{
				Content             = "Last RR",
				Height              = 25,
				Margin              = new Thickness(1, 1, 1, 1),
				Padding             = new Thickness(0),
				FontSize            = 10,
				FontWeight          = FontWeights.Bold,
				HorizontalContentAlignment = HorizontalAlignment.Center,
				Foreground          = whiteBrush
			};
			btnTPNone = new System.Windows.Controls.Button()
			{
				Content             = "NoTP",
				Height              = 25,
				Margin              = new Thickness(1, 1, 2, 1),
				Padding             = new Thickness(0),
				FontSize            = 10,
				FontWeight          = FontWeights.Bold,
				HorizontalContentAlignment = HorizontalAlignment.Center,
				Foreground          = whiteBrush
			};
			var tpSelectorGrid = new System.Windows.Controls.Grid();
			tpSelectorGrid.ColumnDefinitions.Add(new ColumnDefinition());
			tpSelectorGrid.ColumnDefinitions.Add(new ColumnDefinition());
			tpSelectorGrid.ColumnDefinitions.Add(new ColumnDefinition());
			System.Windows.Controls.Grid.SetColumn(btnTP1RR,  0);
			System.Windows.Controls.Grid.SetColumn(btnTPLast, 1);
			System.Windows.Controls.Grid.SetColumn(btnTPNone, 2);
			tpSelectorGrid.Children.Add(btnTP1RR);
			tpSelectorGrid.Children.Add(btnTPLast);
			tpSelectorGrid.Children.Add(btnTPNone);
			System.Windows.Controls.Grid.SetRow(tpSelectorGrid, 7);
			System.Windows.Controls.Grid.SetColumn(tpSelectorGrid, 0);
			System.Windows.Controls.Grid.SetColumnSpan(tpSelectorGrid, 2);

			// ── Row 8: Separator ─────────────────────────────────────────────
			var sep3 = MakeSep();
			System.Windows.Controls.Grid.SetRow(sep3, 8);
			System.Windows.Controls.Grid.SetColumn(sep3, 0);
			System.Windows.Controls.Grid.SetColumnSpan(sep3, 2);

			// ── Row 9: STOPWATCH header ───────────────────────────────────────
			lblStopwatchHeader = MakeHeader("STOPWATCH");
			System.Windows.Controls.Grid.SetRow(lblStopwatchHeader, 9);
			System.Windows.Controls.Grid.SetColumn(lblStopwatchHeader, 0);
			System.Windows.Controls.Grid.SetColumnSpan(lblStopwatchHeader, 2);

			// ── Row 10: Timer value ────────────────────────────────────────────
			lblTradeTimer = new System.Windows.Controls.TextBlock()
			{
				Text                = "--:--:--",
				FontSize            = 11,
				FontWeight          = FontWeights.Bold,
				HorizontalAlignment = HorizontalAlignment.Stretch,
				VerticalAlignment   = VerticalAlignment.Center,
				TextAlignment       = TextAlignment.Center,
				Padding             = new Thickness(0, 4, 0, 4),
				Foreground          = new SolidColorBrush(Color.FromRgb(140, 140, 140)),
				Background          = new SolidColorBrush(Color.FromRgb(18, 28, 30))
			};
			System.Windows.Controls.Grid.SetRow(lblTradeTimer, 10);
			System.Windows.Controls.Grid.SetColumn(lblTradeTimer, 0);
			System.Windows.Controls.Grid.SetColumnSpan(lblTradeTimer, 2);

			// ── Row 11: Separator ─────────────────────────────────────────────
			var sep4 = MakeSep();
			System.Windows.Controls.Grid.SetRow(sep4, 11);
			System.Windows.Controls.Grid.SetColumn(sep4, 0);
			System.Windows.Controls.Grid.SetColumnSpan(sep4, 2);

			// ── Row 12: QUICK BUTTONS header ──────────────────────────────────
			var lblQuickButtons = MakeHeader("QUICK BUTTONS");
			System.Windows.Controls.Grid.SetRow(lblQuickButtons, 12);
			System.Windows.Controls.Grid.SetColumn(lblQuickButtons, 0);
			System.Windows.Controls.Grid.SetColumnSpan(lblQuickButtons, 2);

			// ── Rows 13-17: Management buttons ───────────────────────────────
			btnBreakeven = CreateButton("BreakEven",              basicButtonStyle);
			btnClose     = CreateButton("Close Pos",              basicButtonStyle);
			btnStopUp    = CreateButton("Stop +" + SlAdjustTicks, basicButtonStyle);
			btnStopDown  = CreateButton("Stop -" + SlAdjustTicks, basicButtonStyle);
			btnTPUp      = CreateButton("TP +" + TpAdjustTicks,   basicButtonStyle);
			btnTPDown    = CreateButton("TP -" + TpAdjustTicks,   basicButtonStyle);

			PlaceButton(btnBreakeven, 13, 0); PlaceButton(btnClose,   13, 1);
			PlaceButton(btnStopUp,   14, 0);  PlaceButton(btnTPUp,    14, 1);
			PlaceButton(btnStopDown, 15, 0);  PlaceButton(btnTPDown,  15, 1);

			btnCover = new System.Windows.Controls.Button()
			{
				Content             = "COVER " + PartialExitQty,
				Height              = 25,
				Margin              = new Thickness(2, 1, 2, 1),
				Padding             = new Thickness(0),
				FontSize            = 10,
				FontWeight          = FontWeights.Bold,
				HorizontalContentAlignment = HorizontalAlignment.Center,
				Background          = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
				Foreground          = whiteBrush
			};
			System.Windows.Controls.Grid.SetRow(btnCover, 16);
			System.Windows.Controls.Grid.SetColumn(btnCover, 0);
			System.Windows.Controls.Grid.SetColumnSpan(btnCover, 2);

			// ── Hook events ───────────────────────────────────────────────────
			btnEntry.Click     += OnButtonClick;
			btnBreakeven.Click += OnButtonClick;  btnClose.Click    += OnButtonClick;
			btnStopUp.Click    += OnButtonClick;  btnStopDown.Click += OnButtonClick;
			btnTPUp.Click      += OnButtonClick;  btnTPDown.Click   += OnButtonClick;
			btnCover.Click       += OnButtonClick;  btnMagnet.Click    += OnButtonClick;
			btnTrailTicks.Click  += OnButtonClick;  btnTrailBars.Click += OnButtonClick;
			btnAutopilotAggressive.Click += OnButtonClick;
			btnAutopilotRunner.Click     += OnButtonClick;
			btnTP1RR.Click       += OnTPSelectorClick;
			btnTPLast.Click    += OnTPSelectorClick;
			btnTPNone.Click    += OnTPSelectorClick;

			// ── Add all children (top → bottom order) ────────────────────────
			lowerButtonsGrid.Children.Add(lblRRStatus);
			lowerButtonsGrid.Children.Add(sep0);
			lowerButtonsGrid.Children.Add(btnEntry);
			lowerButtonsGrid.Children.Add(sep1);
			lowerButtonsGrid.Children.Add(btnMagnet);
			lowerButtonsGrid.Children.Add(sep2);
			lowerButtonsGrid.Children.Add(lblTPHeader);
			lowerButtonsGrid.Children.Add(tpSelectorGrid);
			lowerButtonsGrid.Children.Add(sep3);
			lowerButtonsGrid.Children.Add(lblStopwatchHeader);
			lowerButtonsGrid.Children.Add(lblTradeTimer);
			lowerButtonsGrid.Children.Add(sep4);
			lowerButtonsGrid.Children.Add(lblQuickButtons);
			lowerButtonsGrid.Children.Add(btnBreakeven);  lowerButtonsGrid.Children.Add(btnClose);
			lowerButtonsGrid.Children.Add(btnStopUp);     lowerButtonsGrid.Children.Add(btnStopDown);
			lowerButtonsGrid.Children.Add(btnTPUp);       lowerButtonsGrid.Children.Add(btnTPDown);
			lowerButtonsGrid.Children.Add(btnCover);
			lowerButtonsGrid.Children.Add(sep5);
			lowerButtonsGrid.Children.Add(lblAutopilotHeader);
			lowerButtonsGrid.Children.Add(lblAutopilotStatus);
			lowerButtonsGrid.Children.Add(autopilotSelectorGrid);
			lowerButtonsGrid.Children.Add(trailSelectorGrid);

			// ── Wrap the entire panel in a gold/dark-gold border ─────────────
			panelBorder = new System.Windows.Controls.Border()
			{
				BorderBrush     = new SolidColorBrush(Color.FromRgb(180, 150, 50)),  // dark gold
				BorderThickness = new Thickness(1.5),
				CornerRadius    = new CornerRadius(3),
				Padding         = new Thickness(1),
				Child           = lowerButtonsGrid
			};

			// Set initial TP selector visual state
			UpdateTPSelectorVisual();

			SetManagementButtonsEnabled(false);
			if (btnCover != null) btnCover.IsEnabled = false;
			if (btnEntry != null) btnEntry.IsEnabled  = false;

			HookNativeChartTraderButtons();

			if (TabSelected())
				InsertWPFControls();

			chartWindow.MainTabControl.SelectionChanged += TabChangedHandler;
		}

		private System.Windows.Controls.Button CreateButton(string content, Style style)
		{
			return new System.Windows.Controls.Button()
			{
				Content = content, Height = 25,
				Margin = new Thickness(2, 1, 2, 1), Padding = new Thickness(0),
				Style = style
			};
		}

		private void PlaceButton(System.Windows.Controls.Button btn, int row, int col)
		{
			System.Windows.Controls.Grid.SetRow(btn, row);
			System.Windows.Controls.Grid.SetColumn(btn, col);
		}

		// ═══════════════════════════════════════════════════════════════════════
		//  NATIVE CHART TRADER BUTTON INTERCEPT
		// ═══════════════════════════════════════════════════════════════════════

		private void HookNativeChartTraderButtons()
		{
			if (chartTraderButtonsGrid == null) return;
			foreach (UIElement el in chartTraderButtonsGrid.Children)
			{
				var btn = el as System.Windows.Controls.Button;
				if (btn == null) continue;
				string label = btn.Content as string;
				if (label == null) continue;
				switch (label)
				{
					case "Buy Mkt":  nativeBuyMkt  = btn; btn.PreviewMouseLeftButtonDown += OnNativeBuyMkt;  break;
					case "Sell Mkt": nativeSellMkt = btn; btn.PreviewMouseLeftButtonDown += OnNativeSellMkt; break;
					case "Buy Ask":  nativeBuyAsk  = btn; btn.PreviewMouseLeftButtonDown += OnNativeBuyAsk;  break;
					case "Sell Ask": nativeSellAsk = btn; btn.PreviewMouseLeftButtonDown += OnNativeSellAsk; break;
					case "Buy Bid":  nativeBuyBid  = btn; btn.PreviewMouseLeftButtonDown += OnNativeBuyBid;  break;
					case "Sell Bid": nativeSellBid = btn; btn.PreviewMouseLeftButtonDown += OnNativeSellBid; break;
					case "Close":    nativeClose   = btn; btn.PreviewMouseLeftButtonDown += OnNativeClose;   break;
				}
			}
		}

		private void UnhookNativeChartTraderButtons()
		{
			if (nativeBuyMkt  != null) { nativeBuyMkt.PreviewMouseLeftButtonDown  -= OnNativeBuyMkt;  nativeBuyMkt  = null; }
			if (nativeSellMkt != null) { nativeSellMkt.PreviewMouseLeftButtonDown -= OnNativeSellMkt; nativeSellMkt = null; }
			if (nativeBuyAsk  != null) { nativeBuyAsk.PreviewMouseLeftButtonDown  -= OnNativeBuyAsk;  nativeBuyAsk  = null; }
			if (nativeSellAsk != null) { nativeSellAsk.PreviewMouseLeftButtonDown -= OnNativeSellAsk; nativeSellAsk = null; }
			if (nativeBuyBid  != null) { nativeBuyBid.PreviewMouseLeftButtonDown  -= OnNativeBuyBid;  nativeBuyBid  = null; }
			if (nativeSellBid != null) { nativeSellBid.PreviewMouseLeftButtonDown -= OnNativeSellBid; nativeSellBid = null; }
			if (nativeClose   != null) { nativeClose.PreviewMouseLeftButtonDown   -= OnNativeClose;   nativeClose   = null; }
		}

		// ── Native button handlers ───────────────────────────────────────────
		private void OnNativeBuyMkt(object s, System.Windows.Input.MouseButtonEventArgs e)
		{
			if (hasPosition && !isLong) { e.Handled = true; return; }
			if (activeDrawingTool == null && !hasPosition) return;
			e.Handled = true;
			nativeBuyMktRequested = true;
		}
		private void OnNativeSellMkt(object s, System.Windows.Input.MouseButtonEventArgs e)
		{
			if (hasPosition && isLong) { e.Handled = true; return; }
			if (activeDrawingTool == null && !hasPosition) return;
			e.Handled = true;
			nativeSellMktRequested = true;
		}
		private void OnNativeBuyAsk(object s, System.Windows.Input.MouseButtonEventArgs e)
		{
			if (hasPosition && !isLong) { e.Handled = true; return; }
			if (activeDrawingTool == null && !hasPosition) return;
			e.Handled = true;
			nativeCapturedPrice = GetCurrentAsk();
			nativeBuyAskRequested = true;
		}
		private void OnNativeSellAsk(object s, System.Windows.Input.MouseButtonEventArgs e)
		{
			if (hasPosition && isLong) { e.Handled = true; return; }
			if (activeDrawingTool == null && !hasPosition) return;
			e.Handled = true;
			nativeCapturedPrice = GetCurrentAsk();
			nativeSellAskRequested = true;
		}
		private void OnNativeBuyBid(object s, System.Windows.Input.MouseButtonEventArgs e)
		{
			if (hasPosition && !isLong) { e.Handled = true; return; }
			if (activeDrawingTool == null && !hasPosition) return;
			e.Handled = true;
			nativeCapturedPrice = GetCurrentBid();
			nativeBuyBidRequested = true;
		}
		private void OnNativeSellBid(object s, System.Windows.Input.MouseButtonEventArgs e)
		{
			if (hasPosition && isLong) { e.Handled = true; return; }
			if (activeDrawingTool == null && !hasPosition) return;
			e.Handled = true;
			nativeCapturedPrice = GetCurrentBid();
			nativeSellBidRequested = true;
		}
		private void OnNativeClose(object s, System.Windows.Input.MouseButtonEventArgs e)
		{ if (!hasPosition) return; e.Handled = true; closePositionRequested = true; }

		public void InsertWPFControls()
		{
			if (panelActive) return;
			chartTraderGrid.RowDefinitions.Add(addedRow);
			System.Windows.Controls.Grid.SetRow(panelBorder, chartTraderGrid.RowDefinitions.Count - 1);
			chartTraderGrid.Children.Add(panelBorder);
			panelActive = true;
		}

		public void RemoveWPFControls()
		{
			if (!panelActive) return;
			if (chartTraderButtonsGrid != null || panelBorder != null)
			{
				chartTraderGrid.Children.Remove(panelBorder);
				chartTraderGrid.RowDefinitions.Remove(addedRow);
			}
			panelActive = false;
		}

		public void DisposeWPFControls()
		{
			if (chartWindow != null)
				chartWindow.MainTabControl.SelectionChanged -= TabChangedHandler;

			if (btnEntry     != null) btnEntry.Click     -= OnButtonClick;
			if (btnBreakeven != null) btnBreakeven.Click -= OnButtonClick;
			if (btnClose     != null) btnClose.Click     -= OnButtonClick;
			if (btnStopUp    != null) btnStopUp.Click    -= OnButtonClick;
			if (btnStopDown  != null) btnStopDown.Click  -= OnButtonClick;
			if (btnTPUp      != null) btnTPUp.Click      -= OnButtonClick;
			if (btnTPDown    != null) btnTPDown.Click    -= OnButtonClick;
			if (btnCover      != null) btnCover.Click      -= OnButtonClick;
			if (btnMagnet     != null) btnMagnet.Click     -= OnButtonClick;
			if (btnTrailTicks != null) btnTrailTicks.Click -= OnButtonClick;
			if (btnTrailBars  != null) btnTrailBars.Click  -= OnButtonClick;
			if (btnAutopilotAggressive != null) btnAutopilotAggressive.Click -= OnButtonClick;
			if (btnAutopilotRunner     != null) btnAutopilotRunner.Click     -= OnButtonClick;

			if (btnTP1RR  != null) btnTP1RR.Click  -= OnTPSelectorClick;
			if (btnTPLast != null) btnTPLast.Click -= OnTPSelectorClick;
			if (btnTPNone != null) btnTPNone.Click -= OnTPSelectorClick;

			DisposeTradeTimer();
			UnhookNativeChartTraderButtons();
			RemoveWPFControls();
		}

		// ═══════════════════════════════════════════════════════════════════════
		//  TAB SWITCHING SUPPORT
		// ═══════════════════════════════════════════════════════════════════════

		private bool TabSelected()
		{
			bool tabSelected = false;
			foreach (System.Windows.Controls.TabItem tab in chartWindow.MainTabControl.Items)
			{
				if ((tab.Content as ChartTab).ChartControl == ChartControl
					&& tab == chartWindow.MainTabControl.SelectedItem)
					tabSelected = true;
			}
			return tabSelected;
		}

		private void TabChangedHandler(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
		{
			if (e.AddedItems.Count <= 0) return;
			tabItem = e.AddedItems[0] as System.Windows.Controls.TabItem;
			if (tabItem == null) return;
			chartTab = tabItem.Content as ChartTab;
			if (chartTab == null) return;
			if (TabSelected()) InsertWPFControls();
			else               RemoveWPFControls();
		}

		// ═══════════════════════════════════════════════════════════════════════
		//  BUTTON CLICK HANDLER
		// ═══════════════════════════════════════════════════════════════════════

		private void OnButtonClick(object sender, RoutedEventArgs e)
		{
			var button = sender as System.Windows.Controls.Button;
			if (button == null) return;

			if      (button == btnEntry)     submitEntryRequested   = true;
			else if (button == btnBreakeven) breakEvenRequested     = true;
			else if (button == btnClose)     closePositionRequested = true;
			else if (button == btnStopUp)
			{ magnetActive = false; autopilotMode = AutopilotModeBeta.Off; adjustStopUpRequested = true; TriggerImmediateUpdate(); }
			else if (button == btnStopDown)
			{ magnetActive = false; autopilotMode = AutopilotModeBeta.Off; adjustStopDownRequested = true; TriggerImmediateUpdate(); }
			else if (button == btnTPUp)
			{ magnetActive = false; adjustTPUpRequested = true; TriggerImmediateUpdate(); }
			else if (button == btnTPDown)
			{ magnetActive = false; adjustTPDownRequested = true; TriggerImmediateUpdate(); }
			else if (button == btnCover)      coverRequested      = true;
			else if (button == btnMagnet)     magnetToggleRequested = true;
			else if (button == btnTrailTicks) trailTicksRequested = true;
			else if (button == btnTrailBars)  trailBarsRequested  = true;
			else if (button == btnAutopilotAggressive) autopilotAggressiveReq = true;
			else if (button == btnAutopilotRunner)     autopilotRunnerReq     = true;
		}

		// ── TP Selector handler (runs on UI thread) ─────────────────────────
		private void OnTPSelectorClick(object sender, RoutedEventArgs e)
		{
			var button = sender as System.Windows.Controls.Button;
			if (button == null) return;

			if      (button == btnTP1RR)  TPSelection = TPLineChoiceBeta.FirstTP;
			else if (button == btnTPLast) TPSelection = TPLineChoiceBeta.LastTP;
			else if (button == btnTPNone) TPSelection = TPLineChoiceBeta.NoTP;

			UpdateTPSelectorVisual();
		}

		private void UpdateTPSelectorVisual()
		{
			if (btnTP1RR == null || btnTPLast == null || btnTPNone == null) return;

			var activeColor   = new SolidColorBrush(Color.FromRgb(30, 100, 180));
			var inactiveColor = new SolidColorBrush(Color.FromRgb(60, 60, 60));
			var white         = new SolidColorBrush(Colors.White);
			var dimmed        = new SolidColorBrush(Color.FromRgb(140, 140, 140));

			btnTP1RR.Background  = TPSelection == TPLineChoiceBeta.FirstTP ? activeColor : inactiveColor;
			btnTPLast.Background = TPSelection == TPLineChoiceBeta.LastTP  ? activeColor : inactiveColor;
			btnTPNone.Background = TPSelection == TPLineChoiceBeta.NoTP    ? activeColor : inactiveColor;

			btnTP1RR.Foreground  = TPSelection == TPLineChoiceBeta.FirstTP ? white : dimmed;
			btnTPLast.Foreground = TPSelection == TPLineChoiceBeta.LastTP  ? white : dimmed;
			btnTPNone.Foreground = TPSelection == TPLineChoiceBeta.NoTP    ? white : dimmed;
		}
		// ═══════════════════════════════════════════════════════════════════════

		private void UpdateButtonStates()
		{
			if (ChartControl == null) return;
			bool         inPosition = hasPosition;
			bool         hasTool    = activeDrawingTool != null;
			bool         magnet     = magnetActive;
			bool         longPos    = isLong;
			int          partialQty = PartialExitQty;
			TrailModeBeta trail     = trailMode;
			AutopilotModeBeta autopilot = autopilotMode;

			bool toolIsLong = hasTool ? activeDrawingTool.IsLongTrade() : true;

			ChartControl.Dispatcher.InvokeAsync((Action)(() =>
			{
				try
				{
					SetManagementButtonsEnabled(inPosition);

					// ── Single direction entry button ────────────────────
					if (btnEntry != null)
					{
						if (inPosition)
						{
							btnEntry.Content    = longPos ? "LONG ACTIVE" : "SHORT ACTIVE";
							btnEntry.IsEnabled  = false;
							btnEntry.Background = longPos
								? new SolidColorBrush(Color.FromRgb(0, 120, 60))
								: new SolidColorBrush(Color.FromRgb(150, 30, 30));
							btnEntry.Foreground = new SolidColorBrush(Colors.White);
						}
						else if (hasTool)
						{
							btnEntry.Content    = toolIsLong ? "LONG" : "SHORT";
							btnEntry.IsEnabled  = true;
							btnEntry.Background = toolIsLong
								? new SolidColorBrush(Color.FromRgb(0, 150, 80))
								: new SolidColorBrush(Color.FromRgb(180, 40, 40));
							btnEntry.Foreground = new SolidColorBrush(Colors.White);
						}
						else
						{
							btnEntry.Content    = "NO TOOL";
							btnEntry.IsEnabled  = false;
							btnEntry.Background = new SolidColorBrush(Color.FromRgb(80, 80, 80));
							btnEntry.Foreground = new SolidColorBrush(Colors.White);
						}
					}

					if (btnCover != null)
					{
						btnCover.Content    = "COVER " + partialQty;
						btnCover.IsEnabled  = inPosition;
						btnCover.Foreground = new SolidColorBrush(Colors.White);
						btnCover.Background = inPosition
							? (longPos
								? new SolidColorBrush(Color.FromRgb(180, 40, 40))
								: new SolidColorBrush(Color.FromRgb(0, 150, 80)))
							: new SolidColorBrush(Color.FromRgb(80, 80, 80));
					}

					if (btnMagnet != null)
					{
						btnMagnet.Content    = magnet ? "MAGNET ON" : "MAGNET OFF";
						btnMagnet.Background = magnet
							? new SolidColorBrush(Color.FromRgb(0, 150, 80))
							: new SolidColorBrush(Color.FromRgb(180, 40, 40));
						btnMagnet.Foreground = new SolidColorBrush(Colors.White);
					}

					// ── Trail selector buttons ────────────────────────────
					var trailOn  = new SolidColorBrush(Color.FromRgb(200, 120, 0));  // amber = active
					var trailOff = new SolidColorBrush(Color.FromRgb(60, 60, 60));   // dark  = inactive
					if (btnTrailTicks != null)
					{
						btnTrailTicks.Background = (trail == TrailModeBeta.Ticks) ? trailOn : trailOff;
						btnTrailTicks.Foreground = new SolidColorBrush(Colors.White);
					}
					if (btnTrailBars != null)
					{
						btnTrailBars.Background  = (trail == TrailModeBeta.Bars)  ? trailOn : trailOff;
						btnTrailBars.Foreground  = new SolidColorBrush(Colors.White);
					}

					// ── Autopilot buttons + status ───────────────────────────
					bool anyAutopilotActive = (autopilot != AutopilotModeBeta.Off)
						|| (trail != TrailModeBeta.Off);

					if (lblAutopilotStatus != null)
					{
						lblAutopilotStatus.Text       = anyAutopilotActive ? "● ON" : "● OFF";
						lblAutopilotStatus.Foreground  = anyAutopilotActive
							? new SolidColorBrush(Color.FromRgb(50, 200, 80))   // green
							: new SolidColorBrush(Color.FromRgb(200, 50, 50));  // red
					}

					if (btnAutopilotAggressive != null)
					{
						btnAutopilotAggressive.Background = (autopilot == AutopilotModeBeta.Aggressive) ? trailOn : trailOff;
						btnAutopilotAggressive.Foreground = new SolidColorBrush(Colors.White);
					}
					if (btnAutopilotRunner != null)
					{
						btnAutopilotRunner.Background = (autopilot == AutopilotModeBeta.Runner) ? trailOn : trailOff;
						btnAutopilotRunner.Foreground = new SolidColorBrush(Colors.White);
					}

					// ── TP selector: lock while in position ──────────────
					if (btnTP1RR != null)  btnTP1RR.IsEnabled  = !inPosition;
					if (btnTPLast != null) btnTPLast.IsEnabled = !inPosition;
					if (btnTPNone != null) btnTPNone.IsEnabled = !inPosition;
					UpdateTPSelectorVisual();
				}
				catch { }
			}));
		}

		private void SetManagementButtonsEnabled(bool enabled)
		{
			if (btnBreakeven != null) btnBreakeven.IsEnabled = enabled;
			if (btnClose     != null) btnClose.IsEnabled     = enabled;
			if (btnStopUp    != null) btnStopUp.IsEnabled    = enabled;
			if (btnStopDown  != null) btnStopDown.IsEnabled  = enabled;
			if (btnTPUp      != null) btnTPUp.IsEnabled      = enabled;
			if (btnTPDown    != null) btnTPDown.IsEnabled     = enabled;
		}

		// ═══════════════════════════════════════════════════════════════════════
		//  FALLBACK TIMER
		// ═══════════════════════════════════════════════════════════════════════

		private void StartFallbackTimer()
		{
			if (ChartControl == null) return;
			ChartControl.Dispatcher.InvokeAsync((Action)(() =>
			{
				fallbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
				fallbackTimer.Tick += (s, e) =>
				{
					try { if (ChartControl != null) ChartControl.InvalidateVisual(); }
					catch { }
				};
				fallbackTimer.Start();
			}));
		}

		private void StopFallbackTimer()
		{
			if (fallbackTimer == null || ChartControl == null) return;
			try
			{
				ChartControl.Dispatcher.InvokeAsync((Action)(() =>
				{
					try { if (fallbackTimer != null) { fallbackTimer.Stop(); fallbackTimer = null; } }
					catch { }
				}));
			}
			catch { }
		}

		private void TriggerImmediateUpdate()
		{
			try { if (ChartControl != null) ChartControl.InvalidateVisual(); }
			catch { }
		}

		// ═══════════════════════════════════════════════════════════════════════
		//  DEBUG LOGGING
		// ═══════════════════════════════════════════════════════════════════════

		private void DebugPrint(string message)
		{
			if (PrintDebug)
				Print("[SyncStrategy_BETA] " + message);
		}
	}
}```

---

## 7. Strategy Changelog

### BETA Track (Active Development)

---

#### v5.4-BETA — 2026-04-09 *(Current)*

**New Features:**
- **AUTOPILOT system** — level-based trailing stop using the drawing tool's price ladder (SL → Entry → TP1 → TP2 → ... → FinalTP) to automatically ratchet the stop loss as price advances through each level.
  - **AGGRESSIVE mode:** stop moves to 1 level behind the highest touched level (wick). E.g. price reaches TP1 → stop moves to Entry.
  - **RUNNER mode:** stop moves to 2 levels behind, giving more room to run. E.g. price reaches TP2 → stop moves to Entry.
- **AUTOPILOT panel** in Chart Trader — dedicated section below COVER with status indicator (● ON/OFF), AGGRESSIVE/RUNNER toggle buttons (amber = active), and TRAIL xT/xB relocated there.
- **Mutual exclusivity rules:** AGGRESSIVE/RUNNER disables TRAIL and MAGNET; TRAIL disables AUTOPILOT; MAGNET disables AUTOPILOT; BreakEven/Stop+/Stop- disables AUTOPILOT.
- **Pre-arm support:** autopilot mode can be selected while flat and activates automatically on entry fill.
- **Auto-reset:** autopilot resets to Off when position goes flat.

**Bug Fix:**
- `SyncBracketPrices()` now also skips stop-price sync when autopilot is active (same guard as trail mode), ensuring autopilot exclusively owns the stop when running.

**UI:**
- Dark gold border (`RGB(180,150,50)`) wraps the entire strategy panel for clear visual boundary.

---

#### v5.3-BETA — 2026-04-08

**New Features:**
- **TRAIL xT (Tick Trail):** ratchets stop loss behind a high-water mark. Stop moves to `extreme − TrailTicks` (long) or `extreme + TrailTicks` (short). One-way only.
- **TRAIL xB (Bar Trail):** places stop at lowest Low (long) or highest High (short) of the last `TrailBars` bars, recalculated on every tick.
- Both modes mutually exclusive; activating either disables MAGNET; activating MAGNET disables trail.
- Trail state resets to Off when position goes flat.
- `SyncBracketPrices()` skips stop-price sync when any trail mode is active.
- New parameters: `TrailTicks` (default 8), `TrailBars` (default 3).
- Button colors: amber = active, dark-grey = inactive.

**UI:**
- "RISK-REWARD STATUS" teal section header added above the entry button.
- All panel separators updated from teal to Slate Blue-Gray `RGB(60,80,100)`.

---

#### v5.2-BETA — 2026-04-08

**Bug Fix:**
- **`coverInFlight` never reset on rejection/cancellation.** Root cause: `coverInFlight` was only cleared in `OnExecutionUpdate` on a PartialExit fill. If the cover order was rejected or cancelled by the exchange without filling, `coverInFlight` stayed `true` permanently, blocking all future COVER presses. Fix: `coverInFlight` is now also reset in `OnOrderUpdate` when a PartialExit order reaches terminal state via `Cancelled` or `Rejected`.

---

#### v5.1-BETA — 2026-04-07

**Bug Fixes:**
- **OCO ID collision on strategy restart.** Root cause: `ocoCounter` reset to 0 on each new strategy instance, so `"SyncOCO_1"` collided with a previously used OCO group from the same NT8 session. NT8 tracks OCO strings globally per session and rejects reuse, leaving trades unprotected (no SL). Fix: generate a unique `ocoSessionPrefix = "SRCO_" + DateTime.Now.Ticks` at `SetDefaults`. OCO format is now `"SRCO_{ticks}_{counter}"` — globally unique across all restarts.
- **Manual/external order fills leaving bracket qty stale.** A manually placed chart order (e.g. a manual cover) fills outside strategy knowledge, so `bracketQty` was not updated — causing stop/TP to cover the wrong quantity and potentially creating an unintended reverse position. Fix: `ReconcileBracketQty()` called from `OnBarUpdate` after `MonitorPosition()`. Reads `Position.Quantity` (NT8 ground truth) on every tick and calls `UpdateBracketQty()` whenever it diverges from `bracketQty`. In-flight guards (`entryOrder`, `addOnOrder`, `coverInFlight`) prevent race conditions where `Position.Quantity` updates before `OnExecutionUpdate` fires.

---

#### v5.0-BETA — 2026-04-07

**COMPLETE REWRITE — Managed → Unmanaged Orders**

Root cause of the rewrite: the managed order approach (`SetStopLoss`/`SetProfitTarget`) is declarative and cannot reliably update bracket quantities after partial exits or scale-ins. Unmanaged orders give direct, reliable quantity control.

**New (Unmanaged):**
- `IsUnmanaged = true`
- Entry via `SubmitOrderUnmanaged()` (Market or Limit)
- SL bracket via `SubmitOrderUnmanaged()` as `StopMarket`
- TP bracket via `SubmitOrderUnmanaged()` as `Limit`
- SL + TP tied via OCO string — native cancellation on fill
- COVER partial exit instantly updates bracket qty via `ChangeOrder()`
- Scale-in instantly increases bracket qty via `ChangeOrder()`
- Stop+/Stop-/TP+/TP-/Breakeven via `ChangeOrder()` on price
- Magnet sync via `ChangeOrder()` on price
- `OnOrderUpdate()` tracks all order object references (required by unmanaged approach)
- Partial entry fill handling — brackets submitted on first partial, updated on each subsequent fill, finalized on `Filled`
- Limit entry auto-cancel when drawing tool removed or direction flipped
- Terminal state guard on every `ChangeOrder` call

**Removed:**
- `SetStopLoss` / `SetProfitTarget` (unavailable in unmanaged mode)
- `StopTargetHandling` / `EntriesPerDirection` / `EntryHandling`
- Continuous bracket refresh on every tick

**Kept from v4.6:**
- All WPF injection, button layout, tab switching
- All volatile bool flags + `ProcessUIFlags()` structure
- `FindDrawingTool()`, `HasDrawingToolChanged()`
- `UpdateButtonStates()`, `TriggerImmediateUpdate()`
- Native ChartTrader button intercepts
- Fallback timer, `DebugPrint()`
- Trade timer / stopwatch
- TP selector (1RR / Last RR / NoTP)
- Magnet toggle
- COVER guard via `coverInFlight` flag

---

### Managed Track (v4.x — Preserved, Not Actively Developed)

---

#### v4.6 — 2026-04-06 *(Managed track final)*

- **NEW:** Trade Timer — dedicated row below all buttons. Displays elapsed time `HH:MM:SS` while in position; shows "Last: HH:MM:SS" after going flat.
- **NEW:** TP Selector row — three segmented buttons: 1RR | Last RR | NoTP. Active selection highlighted; all disabled while in position.
- **NEW:** Section separators — four 2px teal Rectangle dividers.
- **NEW:** "QUICK BUTTONS" teal header bar.
- **RESTYLE:** All buttons use consistent `FontSize=10/11`, Bold, centered text, `tealBrush`/`MakeHeader`/`MakeSep` helpers.
- **FIX:** Panel total height recalculated to 312px for 15-row layout.

#### v4.5 — 2026-04-06

- **FIX:** Bracket qty not updating after COVER. Replaced deferred `bracketRefreshPending` with **continuous bracket refresh**: `SetStopLoss`/`SetProfitTarget` re-applied on EVERY tick while in position.
- **FIX:** BREAKEVEN now auto-disengages MAGNET.
- **NEW:** Single direction button (replaces BUY/SELL), auto-detects direction from `IsLongTrade()`.
- **NEW:** Scale-in support. `EntriesPerDirection = 99`, unique signal naming.

#### v4.4 — 2026-04-06

- **FIX:** COVER partial exit bracket qty. `StopTargetHandling.ByStrategyPosition` does not auto-adjust bracket qty after `ExitLong/Short`. Introduced `bracketRefreshPending` flag — `OnExecutionUpdate` sets it; `OnBarUpdate` re-applies Set methods on next tick.

#### v4.3 — 2026-04-06

- **FIX (incorrect):** Removed re-apply of `SetStopLoss`/`SetProfitTarget` in `OnExecutionUpdate`. Assumed `ByStrategyPosition` auto-adjusts — it does not. This was reversed in v4.4.

#### v4.2 — 2026-04-06

- **ROOT CAUSE FIX:** Brackets not creating at all. Changed all `SetStopLoss`/`SetProfitTarget` calls from `fromEntrySignal` overload to signal-less overload. The `fromEntrySignal` overload requires `StopTargetHandling.PerEntryExecution` to match signals. With `ByStrategyPosition` (single bracket full qty), the signal-less overload is correct. The rotating signal name was causing NT8 to fail to match Set methods to the entry.

#### v4.1 — 2026-04-06

- **NEW:** NoTP mode — places only a stop loss bracket, no profit target. User exits manually.
- **FIX:** COVER partial exit re-applies Set methods after partial fill.
- **FIX:** TP adjust buttons disabled in NoTP mode.

#### v4.0 — 2026-04-06

- **COMPLETE REWRITE:** ATM → Managed orders.
- Removed all ATM methods (`AtmStrategyCreate`, `AtmStrategyChangeStopTarget`, `AtmStrategyClose`, etc.)
- `SetStopLoss()` + `SetProfitTarget()` + `EnterLong/Short`
- `StopTargetHandling = ByStrategyPosition`
- Signal name rotation via `tradeCount`
- `OnExecutionUpdate` captures entry fill price for breakeven
- Kept: All WPF injection, button layout, tab switching, volatile bool flags, `FindDrawingTool()`, native button intercepts, fallback timer.

---

## 8. Bug History and Fixes

### 8.1 Drawing Tool Bugs

---

**BUG: Drawing tool disappeared in "All Charts" scroll mode**
- **Version introduced:** v1.0
- **Fixed in:** v1.1
- **Symptom:** When using NT8's "All Charts" view and scrolling such that all three anchors (Entry, Risk, Reward) were off-screen, the drawing tool disappeared entirely, even though extended lines should still be visible.
- **Root cause:** `IsVisibleOnChart` was evaluated only against anchor positions. When all anchors were off the visible window, it returned false even if extended lines crossed the window.
- **Fix:** `IsVisibleOnChart` now also checks whether extended lines (which span the full chart width) reach the visible window, so the tool stays visible when anchors are off-screen.

---

**BUG: Button panel disappeared when mouse moved from line to buttons**
- **Version introduced:** v1.0
- **Fixed in:** v1.1
- **Symptom:** When "Show Buttons On Hover" was enabled, moving the mouse from a chart line (entry/stop/target) onto the button panel itself would briefly hide the buttons (hover state dropped to false, hiding the panel before mouse reached the buttons).
- **Root cause:** The hover detection hit-test was checking against drawing lines and shaded zones but not against the button panel's own bounding area. The momentary gap between leaving a line and entering a button caused a hover-off event.
- **Fix:** Hover state tracking now includes the button panel's bounds, so hover is maintained continuously from line → zone → buttons → back.

---

**BUG: Partial TP label showed wrong distance (always showing full target)**
- **Version introduced:** v1.0
- **Fixed in:** v1.1
- **Symptom:** Each partial TP label (TP1, TP2...) showed the same Currency / Ticks / Points value — the full distance from entry to the final target — regardless of which partial level the label was on.
- **Root cause:** The label rendering code was using the `RewardAnchor` price for all partial levels instead of the interpolated price for each specific level.
- **Fix:** Each partial TP label now uses its own interpolated price: `entry + i × riskPerUnit`. This correctly gives TP1 = 1R distance, TP2 = 2R distance, etc.

---

**BUG: Partial TP prices 1 tick off due to floating-point drift**
- **Version introduced:** v1.0
- **Fixed in:** v1.1
- **Symptom:** Even after computing label prices via interpolation (entry + fraction × (target − entry)), TP1 was 1 tick different from the FSL distance. Labels disagreed slightly from the actual line prices.
- **Root cause:** Linear interpolation from entry to target introduces floating-point rounding errors at the tick boundary. Multiplying a fraction by a large price range accumulates small errors.
- **Fix:** Changed formula to `entry + i × riskPerUnit` where `riskPerUnit = |entry − stop|`. This is the same additive math used by `SetReward()`, guaranteeing TP1 always equals exactly FSL distance with zero drift.

---

**BUG: DirectX TextLayout/TextFormat resource leak**
- **Version introduced:** v1.0
- **Fixed in:** v1.2
- **Symptom:** Over time, with the chart updating on every tick, unmanaged memory consumption grew. The effect was especially notable on fast markets with many ticks per second.
- **Root cause:** `SharpDX.DirectWrite.TextLayout` and `TextFormat` objects created in `DrawPriceText`, `DrawPriceTextPartials`, and `DrawPartialStopText` were not disposed after use. Each render frame allocated new DirectX objects that were never freed.
- **Fix:** All three methods now wrap `TextFormat` and `TextLayout` creation in `using` blocks, ensuring deterministic disposal after every render frame.

---

**BUG: PriceSource.LastPrice silently fell through switch**
- **Version introduced:** v1.0
- **Fixed in:** v1.2
- **Symptom:** `PriceSource.LastPrice` was being handled by the `default` branch of the price-source switch statement, which was potentially undefined or wrong if `default` changed.
- **Root cause:** No explicit `case PriceSource.LastPrice:` was present in the switch.
- **Fix:** Added explicit `case PriceSource.LastPrice:` for clarity and defensive correctness.

---

**BUG: Divide-by-zero in contract calculation when PointValue is zero**
- **Version introduced:** v1.0
- **Fixed in:** v1.2
- **Symptom:** On certain instruments (custom/synthetic) where `PointValue` is not configured, the contract calculation could produce a divide-by-zero exception.
- **Fix:** Added `if (pv == 0) return 0;` guard before the division.

---

**BUG: `ChangeVersion` deserialization crash (template save/load)**
- **Version introduced:** v1.3 (when ChangeVersion was introduced)
- **Fixed in:** v1.5
- **Symptom:** Saving a drawing tool template or loading one caused NT8 to throw a "Cannot deserialize property ChangeVersion" error. The drawing tool could not be serialized to XML templates.
- **Root cause:** `ChangeVersion` was declared with `{ get; private set; }`. NT8's XML deserializer uses reflection to set all `[Browsable]` properties when loading templates and requires a public setter. `private set` makes the property read-only from the deserializer's perspective.
- **Fix:** Changed to `{ get; set; }` (fully public setter).
- **Important learning:** This applies to ALL properties serialized by NT8's drawing tool or strategy template system — they must have public getters AND setters. `private set` will cause deserialization errors.

---

**BUG: StopTrackOnFill never re-armed after position close**
- **Version introduced:** v1.0 (StopTrackOnFill feature)
- **Fixed in:** v1.6
- **Symptom:** After the first trade filled and `StopTrackOnFill` disabled tracking, the tracking remained permanently disabled even after the position closed. The user had to manually press AUTO TRACK again before every new setup.
- **Root cause:** `userOverrideTracking` and `TrackLastPrice` were only set to `false` when the fill was detected, but were never reset when the position returned to flat. The system had no re-arm logic.
- **Fix:** Added a flat-detection check in `OnRender`: when `StopTrackOnFill` is enabled and no position exists on the instrument, `userOverrideTracking` is reset to `false` and `TrackLastPrice` is restored to `true`, allowing tracking to resume for the next setup.

---

**BUG: One extra render frame of tracking after fill detection**
- **Version introduced:** v1.6 (discovered alongside StopTrackOnFill re-arm fix)
- **Fixed in:** v1.6
- **Symptom:** On the same frame that `StopTrackOnFill` detected a fill and disabled tracking, the tracking code block still executed once, causing the entry anchor to jump one extra frame.
- **Fix:** The tracking code block is now guarded with the updated `TrackLastPrice` state, so it skips immediately on the same frame that `StopTrackOnFill` disables tracking.

---

### 8.2 Strategy Bugs

---

**BUG: Brackets not creating at all (managed v4.2)**
- **Root cause (took several versions to find):** `SetStopLoss` / `SetProfitTarget` were called with the `fromEntrySignal` overload, e.g. `SetStopLoss("SyncEntry_0", CalculationMode.Price, price, false)`. This overload requires `StopTargetHandling.PerEntryExecution` and uses the signal name to match the entry order. With `StopTargetHandling.ByStrategyPosition` (the correct setting for a single full-qty bracket), NT8 cannot match the signal-named Set call to the entry. The brackets were silently ignored.
- **Fix:** Changed all 8 call sites to the signal-less overload: `SetStopLoss(CalculationMode.Price, price)`.
- **Lesson:** When using `ByStrategyPosition`, always use signal-less Set methods. Signal-named overloads are only valid with `PerEntryExecution`.

---

**BUG: Bracket qty not updating after COVER partial exit (managed)**
- **Root cause:** `StopTargetHandling.ByStrategyPosition` does NOT automatically adjust bracket order quantities after `ExitLong` / `ExitShort` partial fills. The brackets remain at their original quantity until the strategy explicitly re-calls `SetStopLoss`/`SetProfitTarget`. This means the strategy was covering the wrong quantity and could create unintended positions.
- **Attempted fixes:** v4.1 (re-apply in OnExecutionUpdate — broke bracket creation), v4.3 (removed re-apply incorrectly), v4.4 (deferred flag + next tick re-apply — worked but fragile), v4.5 (continuous re-apply every tick — worked reliably for managed approach).
- **Definitive fix (unmanaged, v5.0):** `ChangeOrder(stop, newQty, ...)` and `ChangeOrder(tp, newQty, ...)` called immediately in `OnExecutionUpdate` after every partial fill. Direct, synchronous, reliable.
- **Lesson:** Managed orders cannot reliably control bracket quantities after partial exits. If you need precise qty control, use unmanaged orders.

---

**BUG: OCO ID collision on strategy restart within same NT8 session (v5.0)**
- **Fixed in:** v5.1
- **Symptom:** After stopping and restarting the strategy (or after a disconnect/reconnect), the first bracket submission was rejected with an OCO ID conflict error. The stop order was rejected, leaving the live trade with no stop loss protection.
- **Root cause:** `ocoCounter` started at 0 on each new strategy instance. `"SyncOCO_1"` from the previous session instance was still tracked by NT8 globally, so the new instance's `"SyncOCO_1"` collided.
- **Fix:** At `SetDefaults`, generate `ocoSessionPrefix = "SRCO_" + DateTime.Now.Ticks`. All OCO IDs become `"SRCO_{ticks}_{counter}"`, unique across the entire NT8 session lifetime.

---

**BUG: Manual fills leaving bracket qty stale (v5.0)**
- **Fixed in:** v5.1
- **Symptom:** A manually placed chart order (e.g. a NT8 native stop limit used as manual cover) filled outside strategy knowledge. `bracketQty` was never updated, causing the bracket to cover the wrong quantity.
- **Root cause:** `OnExecutionUpdate` only tracked fills from strategy-submitted orders. Manual fills from the native NT8 chart interface are not routed through strategy execution events.
- **Fix:** `ReconcileBracketQty()` runs on every tick. Reads `Position.Quantity` (NT8 ground truth) and calls `UpdateBracketQty()` when `Position.Quantity != bracketQty`. In-flight guards (`entryOrder != null`, `addOnOrder != null`, `coverInFlight`) prevent race conditions.

---

**BUG: `coverInFlight` permanently locked after order rejection (v5.0)**
- **Fixed in:** v5.2
- **Symptom:** If the exchange rejected a COVER order (e.g. due to insufficient liquidity), `coverInFlight` stayed `true` forever. All subsequent COVER button presses were silently blocked until the position was fully closed.
- **Root cause:** `coverInFlight` was only reset in `OnExecutionUpdate` on a successful `PartialExit` fill. Order rejection/cancellation never cleared it.
- **Fix:** `coverInFlight = false` added to `OnOrderUpdate` when a PartialExit order reaches a terminal non-filled state (`Cancelled` or `Rejected`).

---

**BUG: BREAKEVEN immediately overwritten by MAGNET sync**
- **Version introduced:** v4.5 (managed)
- **Fixed in:** v4.5
- **Symptom:** Pressing BREAKEVEN moved the stop to the entry price, but on the next tick the Magnet sync read the drawing tool's current SL anchor and moved the stop back to the drawing tool's stop price, canceling the breakeven.
- **Root cause:** MAGNET was still active after BREAKEVEN. On the next tick, `SyncBracketPrices()` detected that the drawing tool's stop differed from `lastSyncedStop` and called `ChangeOrder` / `SetStopLoss` to resync.
- **Fix:** `MoveStopToBreakeven()` now sets `magnetActive = false` before modifying the stop. Same fix applied to Stop+/Stop- buttons.

---

**BUG: Scale-in / add-on orders blocked by UniqueEntries exhaustion (managed)**
- **Version introduced:** Early managed versions using a single signal name for all entries
- **Fixed in:** v4.5
- **Symptom:** After a certain number of entries (or in some cases the second entry), NT8 rejected scale-in attempts silently.
- **Root cause:** `EntryHandling = UniqueEntries` with a static signal name means NT8 will not allow the same signal to re-enter unless flat. Dynamic signal naming (`SyncEntryLong_0`, `SyncEntryLong_1`, ...) circumvents this.
- **Fix (managed):** `EntryHandling = AllEntries` paired with dynamic signal naming (`tradeCount` counter + `StartsWith()` prefix matching in `OnExecutionUpdate`).
- **Fix (unmanaged BETA):** Not applicable — unmanaged orders do not use `EntryHandling`.

---

## 9. Critical Information for Future Developers and AI Agents

This section documents hard-won lessons, NT8-specific quirks, and architectural constraints that are not obvious from the NinjaTrader documentation. All of these were discovered through production debugging during the development of this toolkit.

---

### 9.1 NinjaTrader 8 API Constraints

**`AtmStrategyCreate()` cannot be called from a DrawingTool.**
DrawingTools are purely visual components. Order submission, position management, and Chart Trader interaction all require a Strategy class. This is the core architectural reason the toolkit has two separate files.

**`OnWindowCreated` and `OnWindowDestroyed` are `AddOnBase`-only.**
These lifecycle overrides do not exist on `Strategy`. WPF injection into Chart Trader must be triggered from `OnStateChange` at `State.Historical` using `ChartControl.Dispatcher.InvokeAsync`.

**`OnMouseDoubleClick` does not exist on `DrawingTool`.**
This event is not part of the `DrawingTool` base class API. Do not attempt to override it.

**`Stroke.BrushDX`, not `Stroke.Color`, for stroke color access.**
When accessing stroke colors in DirectX render context (e.g. `RenderTarget.DrawTextLayout`), use `stroke.BrushDX` to get the `SharpDX.Direct2D1.Brush`. `Stroke.Color` is a WPF `Color` struct and will not work in the DirectX render path.

**`GetCursor` should return `Cursors.Arrow`, not `Cursors.Hand`.**
The drawing tool overrides `GetCursor` to return `Cursors.Arrow` over its full hit area. This ensures the hover detection absorbs mouse events without triggering NT8's standard drag cursor, which would interfere with button clicks.

**`DrawingState != DrawingState.Building` vs `DrawingState.Normal`.**
Buttons should respond when `DrawingState != DrawingState.Building`, not only when `DrawingState == DrawingState.Normal`. This allows buttons to respond on the very first click after placing the tool, without requiring the user to first select the tool.

**XML serialization requires public setters on all properties.**
NT8's drawing tool and strategy template system uses reflection-based XML serialization. Any property decorated with `[NinjaScriptProperty]` or that is otherwise serialized MUST have a public setter (`{ get; set; }`). Using `{ get; private set; }` will cause "Cannot deserialize" errors when saving or loading templates. This caught `ChangeVersion` in v1.3 and was not fixed until v1.5.

**Managed orders (`SetStopLoss`/`SetProfitTarget`) cannot reliably control bracket quantities after partial exits.**
This is an architectural limitation of NT8's managed order system, not a bug in user code. After a partial `ExitLong`/`ExitShort`, the bracket quantity may not update to reflect the remaining position. The only reliable solution is to use unmanaged orders (`IsUnmanaged = true`, `SubmitOrderUnmanaged`, `ChangeOrder`).

**OCO strings are tracked globally per NT8 session.**
Once an OCO group string (e.g. `"SyncOCO_1"`) is used in a session, it cannot be reused — not even after the strategy that created it is stopped and restarted. Always generate a session-unique prefix (e.g. from `DateTime.Now.Ticks`) to avoid collisions on strategy restart.

**`OnBarUpdate` only fires on incoming ticks.**
In slow markets, `OnBarUpdate` may not fire for many seconds. Any logic in `OnBarUpdate` (including bracket sync, position monitoring, button state updates) will appear to lag in low-activity periods. The fallback `DispatcherTimer` (250ms interval) exists to force chart redraws in slow markets, but NinjaScript-thread logic still only runs on ticks.

**`Calculate.OnEachTick` is required for real-time responsiveness.**
`Calculate.OnBarClose` or `Calculate.OnPriceChange` are insufficient for order management logic that needs to respond to every market movement. Always use `Calculate.OnEachTick` for execution strategies.

**`Order.IsTerminalState(order.OrderState)` must be checked before every `ChangeOrder`.**
NT8 throws exceptions if you attempt to modify a filled, cancelled, or rejected order. Always wrap `ChangeOrder` calls in a terminal state guard. See `SafeChangeOrder()` in the BETA for the reference implementation.

---

### 9.2 Thread Safety

**WPF runs on the UI thread. NinjaScript runs on the NinjaScript thread. They cannot directly share data.**

The pattern used throughout this toolkit:
1. WPF button `Click` handlers set `volatile bool` flags (e.g. `submitEntryRequested = true`)
2. `OnBarUpdate` (NinjaScript thread) reads the flags via `ProcessUIFlags()`, clears them, and executes the corresponding order logic

`volatile` is required (not just `bool`) to ensure memory visibility across threads without locks. Without `volatile`, the NinjaScript thread may read a stale cached value.

**Never call order methods from WPF event handlers.** All `SubmitOrderUnmanaged`, `ChangeOrder`, `CancelOrder` calls must happen on the NinjaScript thread (inside `OnBarUpdate` or `OnExecutionUpdate`).

**For UI updates (button colors, text):** Always use `ChartControl.Dispatcher.InvokeAsync(() => { ... })`. Direct WPF property access from the NinjaScript thread will cause cross-thread exceptions.

---

### 9.3 Drawing Tool Discovery Pattern

The strategy discovers the drawing tool via `DrawObjects` polling — not by name entry or manual binding. This pattern was chosen because:
- It works automatically with any instance of the drawing tool on the chart
- It requires no user configuration beyond having both files loaded
- It always picks the most recently modified tool (via `ChangeVersion`) if multiple exist

```csharp
// Pattern used in FindDrawingTool()
foreach (IDrawingTool drawObj in DrawObjects)
{
    var tool = drawObj as AdvancedRiskReward_LuisGarrido;
    if (tool == null) continue;
    if (tool.ChangeVersion > bestVersion)
    {
        bestTool = tool;
        bestVersion = tool.ChangeVersion;
    }
}
```

`ChangeVersion` is the key — it increments on every anchor drag, flip, or auto-track update. The strategy can efficiently detect "did anything change?" with a single integer comparison instead of comparing all prices on every tick.

---

### 9.4 WPF Injection Pattern (TradeSaber Pattern)

The WPF injection into Chart Trader follows the pattern established by TradeSaber's `OrderEntryButtons.cs`. Critical points:

1. Injection is triggered from `OnStateChange` at `State.Historical` via `ChartControl.Dispatcher.InvokeAsync`. This fires once when the strategy is loaded onto the chart, before realtime data starts.

2. The strategy must be an `IsOverlay = true` strategy to be attached to the chart panel where Chart Trader lives.

3. Native Chart Trader buttons are hooked using `PreviewMouseLeftButtonDown` (a tunneling WPF routed event), not `Click`. Tunneling events fire before the element's own handlers, enabling interception.

4. The Ask/Bid price at click time is captured on the UI thread (where the WPF event fires), stored in `nativeCapturedPrice`, and consumed by the NinjaScript thread in `ProcessUIFlags()` on the next tick.

5. Cleanup via `DisposeWPFControls()` must unhook all event handlers and remove injected UI elements from the parent grid. Failure to do this causes memory leaks and phantom UI on strategy restart.

---

### 9.5 Partial TP Label Math

**Always compute partial TP prices using additive math, not linear interpolation.**

```csharp
// WRONG — floating-point drift at tick boundaries:
double fraction = (double)i / numTP;
double price = entry + fraction * (target - entry);

// CORRECT — exact, no drift:
double riskPerUnit = entry - stop;   // (negative for shorts)
double tpPrice = entry + i * riskPerUnit;
```

The additive formula guarantees that TP1 is exactly `|entry - stop|` away from entry. The interpolation formula accumulates floating-point error proportional to the distance from entry to the final target, causing visible tick-level discrepancies on the chart.

---

### 9.6 Contract Sizing Math

Auto Order Qty calculation:
```
riskPerContract = |entry - stop| × PointValue
qty = round(StopLossBudget / riskPerContract, MidpointRounding.AwayFromZero)
qty = clamp(qty, 1, MaxContracts)
```

This formula is used in three places:
1. `GetPriceString()` (drawing tool label rendering)
2. `UpdateOrderQty()` (Chart Trader quantity sync)
3. `GetCalculatedQty()` (public accessor for strategy)

All three must use identical math to ensure the strategy reads the same value shown on chart. If any of these diverge, the chart label and the actual submitted order quantity will disagree, confusing the trader.

---

### 9.7 File Naming Convention

As of the current state:

| File | Purpose | Version |
|---|---|---|
| `AdvancedRiskReward_LuisGarrido.cs` | Drawing tool (current, active) | v1.6 |
| `DrawingToolSyncStrategy_BETA.cs` | Strategy BETA — unmanaged orders (active) | v5.4 |
| `DrawingToolSyncStrategy.cs` | Strategy managed — preserved, not developed | v4.6 |

**Future versions of the drawing tool must be named `AdvancedRiskReward_LuisGarrido_V1.X.cs`** to avoid overwriting the stable base while maintaining the class name `AdvancedRiskReward_LuisGarrido` inside the file (so the strategy can still find it).

---

### 9.8 Code Change Protocol

When modifying either file, the following rules apply:

1. **Always read and use the uploaded source file as the base.** Never reconstruct from memory, transcripts, or context. The uploaded file is the authoritative source.
2. **The file header MUST be updated** with the new version number and a complete description of all changes (new features and bug fixes) before delivery.
3. **Changes via targeted `str_replace`** — surgical edits, not full-file rewrites, unless a full rewrite is explicitly requested.
4. **Verify brace balance** after any code addition. Asymmetric braces cause compile errors that can be hard to locate.
5. **Zip files only when explicitly requested** by the user.
6. **Do not generate any code** without first proposing the change, explaining the approach, and receiving explicit confirmation from the user.
7. **Analyze root cause before writing any code.** Jumping to implementation without fully understanding the root cause wastes effort and often produces incorrect fixes.

---

### 9.9 NT8 Help Documentation Note

The NT8 online help guide URL (`ninjatrader.com/support/helpguides/...`) returns HTTP 403 when accessed programmatically. When precise API references are needed, paste the specific sub-page content directly into the conversation rather than attempting to fetch the URL.

---

### 9.10 Summary of Key API Decisions

| Decision | Why |
|---|---|
| `IsUnmanaged = true` (BETA) | Only way to get deterministic bracket qty control after partial exits |
| `DrawObjects` polling for tool discovery | Works automatically, no user config, always finds most recent tool |
| `ChangeVersion` counter on drawing tool | Efficient change detection without per-tick price comparisons |
| `volatile bool` flags for UI→NinjaScript | Required for cross-thread memory visibility without locks |
| `ChartControl.Dispatcher.InvokeAsync` for WPF injection | Only safe way to inject WPF from NinjaScript at strategy load time |
| `PreviewMouseLeftButtonDown` for button intercept | Tunneling event fires before Click; required for intercept |
| Session-unique OCO prefix from `DateTime.Now.Ticks` | Prevents OCO ID collision on strategy restart within NT8 session |
| `ReconcileBracketQty()` every tick against `Position.Quantity` | Catches manual fills that bypass `OnExecutionUpdate` |
| `SafeChangeOrder()` wrapper | Prevents exceptions from modifying terminal orders |
| Additive math for TP partial prices (`entry + i × riskPerUnit`) | Eliminates floating-point drift vs interpolation formula |
| `{ get; set; }` (not `private set`) on all serialized properties | NT8 XML deserializer requires public setters |
| `GetCursor` returns `Cursors.Arrow` | Absorbs hover without triggering drag cursor |
| `DrawingState != DrawingState.Building` for button response | Allows first-click button response without requiring tool selection first |

---

*Document generated: 2026-04-10*
*Drawing Tool: v1.6 | Strategy (Managed): v4.6 | Strategy (BETA/Unmanaged): v5.4*
*Author: Luis Fernando Garrido Miranda | Built with Claude Sonnet 4.6 by Anthropic*
#   A d v a n c e d - R i s k - t o - R e w a r d - b y - L G  
 