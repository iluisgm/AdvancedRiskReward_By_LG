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
}