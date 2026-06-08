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
}