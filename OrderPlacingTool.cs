//------------------------------------------------------------------------------
// OrderPlacingTool.cs
//------------------------------------------------------------------------------
// Renders a fully-styled “Trade Manager” panel matching your mock-up.
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using TradingPlatform.BusinessLayer;
using TradingPlatform.BusinessLayer.Chart;
using TradingPlatform.BusinessLayer.Native;
using TradingPlatform.BusinessLayer.Utils;

#nullable disable
namespace OrderPlacingTool
{
    public class OrderPlacingTool : Indicator
    {
        //── USER SETTINGS ───────────────────────────────────────────────────────────
        [InputParameter("Risk Amount", 0, 1, 1_000_000, 1)]
        public int RiskAmount { get; set; } = 100;

        [InputParameter("Reward Multiplier", 1, 0.1, 10, 0.1)]
        public double RewardMultiplier { get; set; } = 1.0;

        [InputParameter("X Offset", 2)]
        public int XShift { get; set; } = 30;

        [InputParameter("Y Offset", 3)]
        public int YShift { get; set; } = 30;

        [InputParameter("Market Order Mode", 4)]
        public bool MarketOrderMode { get; set; } = false;

        [InputParameter("Auto Adjust SL/TP on Fill", 5)]
        public bool AutoAdjustOnFill { get; set; } = true;

        [InputParameter("UI Scale", 6, 0.5, 2, 0.1)]
        public double UIScale { get; set; } = 1.0;


        //── LAYOUT CONSTANTS ─────────────────────────────────────────────────────────
        const int panelW = 320;
        const int headerH = 36;
        const int row1H = 37;
        const int row2H = 36;
        const int row3H = 36;
        const int row4H = 44;
        const int btnRadius = 16;
        const int gutter = 8;
        const int radioSize = 14;

        //── COLORS & FONTS ───────────────────────────────────────────────────────────
        readonly Color pipsAndCurrency = Color.FromArgb(184, 205, 228);
        readonly Color panelBack = Color.FromArgb(20, 30, 40);
        readonly Color headerBack = Color.FromArgb(41, 50, 60);
        readonly Color borderCol = Color.Gray;
        readonly PairColor sellCol = new PairColor
        {
            Color1 = Color.FromArgb(0xD1, 0x4F, 0x4A),  // #D14F4A
            Color2 = Color.FromArgb(0xB6, 0x3F, 0x3A)   // #B63F3A
        };
        readonly PairColor buyCol = new PairColor
        {
            Color1 = Color.FromArgb(0x2F, 0xA4, 0x66),  // #2FA466
            Color2 = Color.FromArgb(0x27, 0x86, 0x53)   // #278653
        };
        readonly PairColor beCol = new PairColor
        {
            Color1 = Color.FromArgb(216, 108, 0),    // darker “back”
            Color2 = Color.FromArgb(196, 100, 0)     // darker “border”
        };
        readonly PairColor partCol = new PairColor { Color1 = Color.Green, Color2 = Color.DarkGreen };
        readonly PairColor smallCol = new PairColor { Color1 = Color.FromArgb(41, 50, 60), Color2 = Color.Gray };

        readonly Font titleFont = new Font("Segoe UI", 14, FontStyle.Bold);
        readonly Font mainFont = new Font("Segoe UI", 12, FontStyle.Bold);
        readonly Font smallFont = new Font("Segoe UI", 12, FontStyle.Regular);
        readonly Brush textBrush = Brushes.White;

        readonly Color textBoxBackCol = Color.FromArgb(41, 50, 60);

        // static so nested Button can reference
        private static readonly StringFormat CenterFormat = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        private static readonly StringFormat LeftFormat = new StringFormat
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Center
        };

        //── RUNTIME STATE ────────────────────────────────────────────────────────────
        Brush sellBack, buyBack, beBack, partBack, smallBack;
        Pen sellPen, buyPen, bePen, partPen, smallPen;

        Button sellBtn, buyBtn;
        Button[] lotRadios;
        Rectangle cashBox;
        private Rectangle rrBtnRect;
        Button beBtn, partBtn;
        Button btnAll, btnProfit, btnLoss, btnStop;
        Rectangle labelCloseTrades, labelCloseOrders;
        Brush textBoxBack;

        int quantity = 100;
        double pipL = 2936.0, pipR = 2795.0;
        double cashAmt, beVal = 0.0;

        // ── ENTRY ORDER BUTTONS ─────────────────────────────────────────────────────
        private Button limitOrderBtn, stopOrderBtn;
        private bool buyArmed, sellArmed, rrArmed;

        enum LotMode { None, Cash, RiskBal, RiskEq }
        LotMode lotMode = LotMode.Cash;
        
        Rectangle beValueBox;

        private bool isBuyFlow, isSellFlow;
        private bool isLimitFlow, isStopFlow;
        private double entryPrice, stopPrice;
        // 1) Add these two fields at the top of your class:
        private Side lastSide;
        private double lastEntryPrice;
        //──────────────────────────────────────────────────────────────────────────────
        public OrderPlacingTool()
        {
            Name = "Trade Manager";
            Description = "In-chart Trade Manager panel";
            SeparateWindow = false;
            AddLineSeries("dummy", Color.Transparent, 1, LineStyle.Solid);
            BuildBrushesAndPens();
        }


        // reuse your TradeParams class exactly as you had it:
        private class TradeParams
        {
            public Symbol symbol;
            public Account account;
            public string orderTypeId;
            public Side side;
            public double lotSize;
            public double price;
            public double slPrice;
            public double tpPrice;
            public void Reset()
            {
                orderTypeId = null;
                side = Side.Buy;
                lotSize = price = slPrice = tpPrice = 0;
            }
        }
        private TradeParams tradeParams = new TradeParams();
        void BuildBrushesAndPens()
        {
            sellBack = new SolidBrush(sellCol.Color1);
            sellPen = new Pen(sellCol.Color2);
            buyBack = new SolidBrush(buyCol.Color1);
            buyPen = new Pen(buyCol.Color2);
            beBack = new SolidBrush(beCol.Color1);
            bePen = new Pen(beCol.Color2);
            partBack = new SolidBrush(partCol.Color1);
            partPen = new Pen(partCol.Color2);
            smallBack = new SolidBrush(smallCol.Color1);
            smallPen = new Pen(smallCol.Color2);
            textBoxBack = new SolidBrush(textBoxBackCol);
        }
        

        private void OnOrderClosed(OrderHistory hist)
        {
            if (hist.Account == CurrentChart.Account
     && hist.Symbol == Symbol
     && ( hist.Status == OrderStatus.Cancelled))
            {
                lastEntryPrice = 0;
                beVal = 0;
            }
        }

        protected override void OnSettingsUpdated()
        {

            
            //base.OnSettingsUpdated();
            BuildBrushesAndPens();
            LayoutUI();
                      
        }

        private void OnOrderFilled(OrderHistory hist)
        {
            // only act if the user enabled auto-adjust, it’s our account/symbol, and it just filled
            if (!AutoAdjustOnFill
                || hist.Account != CurrentChart.Account
                || hist.Symbol != this.Symbol
                || hist.Status != OrderStatus.Filled)
                return;

            // find the newly‐filled position(s) and move SL→BE
            foreach (var pos in Core.Instance.Positions)
            {
                if (pos.Account == CurrentChart.Account
                 && pos.Symbol == this.Symbol
                 && pos.Id == hist.PositionId)
                {
                    Core.Instance.AdvancedTradingOperations.AdjustSlTp(pos);
                }
            }
        }


        protected override void OnInit()
        {
            base.OnInit();                           // ← make sure the base wiring happens
            tradeParams.symbol = this.Symbol;       // ← grab the chart’s symbol
            tradeParams.account = this.CurrentChart.Account;

            LayoutUI();                            // initial layout
            CurrentChart.MouseClick += CurrentChart_MouseClick;
            Core.Instance.OrdersHistoryAdded += OnOrderClosed;
            Core.Instance.OrdersHistoryAdded += OnOrderFilled;
        }

        /// <summary>
        /// (Re)positions *all* of your buttons, boxes, radios, etc.
        /// based on the current xShift / yShift fields.
        /// </summary>
        void LayoutUI()
        {

            int X = XShift, Y = YShift;

            const int headerBtnW = 120;  // <-- desired width
            const int breakBtnW = 100;
            const int breakBtnH = 30;

            // first—lay out your cash box
            const int cashBoxWidth = 80;
            int startLotY = Y + headerH + row1H + row2H + gutter;
            cashBox = new Rectangle(
                X + panelW - gutter - /* USD-label width */ 40 - cashBoxWidth,
                startLotY + 1 * (row3H + gutter) + 4,
                cashBoxWidth,
                row3H - 8
            );

            // Row1: SELL | qty | BUY
            sellBtn = new Button(
                "SELL",
                X + gutter,              // left edge
                Y + headerH,             // top edge
                X + gutter + headerBtnW, // right edge = left + width
                Y + headerH + row1H,     // bottom edge
                sellBack, sellPen, mainFont, textBrush
            );

            // push the BUY button to the right of center by the same width:
            buyBtn = new Button(
                "BUY",
                X + panelW - gutter - headerBtnW, // left edge = panel right minus gutter minus width
                Y + headerH,                       // top edge
                X + panelW - gutter,              // right edge
                Y + headerH + row1H,              // bottom edge
                buyBack, buyPen, mainFont, textBrush
            );

            // ── Entry “Limit” / “Stop” buttons around the qty display ───────────────────
            const int entryBtnW = 50, entryBtnH = 20;
            float qtyCenterY = Y + headerH + row1H / 2f;
            int btnX1 = X + (panelW - entryBtnW) / 2;

            // 1) “Limit” above the 100
            int limitY2 = (int)qtyCenterY - 4;
            int limitY1 = limitY2 - entryBtnH;
            limitOrderBtn = new Button(
                "LIMIT",
                btnX1, limitY1,
                btnX1 + entryBtnW, limitY2,
                partBack, partPen, smallFont, textBrush
            );

            // 2) “Stop” below the 100
            int stopY1 = (int)qtyCenterY + 4;
            int stopY2 = stopY1 + entryBtnH;
            stopOrderBtn = new Button(
                "STOP",
                btnX1, stopY1,
                btnX1 + entryBtnW, stopY2,
                beBack, bePen, smallFont, textBrush
            );

            // Row3: Lot-Calc radios + cash input
            int radioX = X + gutter;
            //int startLotY = Y + headerH + row1H + row2H + gutter;
            int rowHeight = row3H + gutter;
            int lotCount = 4;  // we have 4 radio buttons

            lotRadios = new[]
            {
    new Button("None",
        radioX, startLotY + 0*rowHeight,
        radioX + radioSize, startLotY + 0*rowHeight + radioSize,
        smallBack, smallPen, smallFont, textBrush,
        circle: true),

    new Button("Cash Amount",
        radioX, startLotY + 1*rowHeight,
        radioX + radioSize, startLotY + 1*rowHeight + radioSize,
        smallBack, smallPen, smallFont, textBrush,
        circle: true),
    new Button(
    "Risk Balance",
    radioX,
    startLotY + 2 * rowHeight,
    radioX + radioSize,
    startLotY + 2 * rowHeight + radioSize,
    smallBack, smallPen, smallFont, textBrush,
    circle: true
),
new Button(
    "Risk Equity",
    radioX,
    startLotY + 3 * rowHeight,
    radioX + radioSize,
    startLotY + 3 * rowHeight + radioSize,
    smallBack, smallPen, smallFont, textBrush,
    circle: true
)

};
            // default to the “Cash Amount” radio being selected:
            lotRadios[(int)LotMode.Cash].IsChecked = true;

            int usdPadding = 2;
            var usdSize = new Size(40, cashBox.Height);
            var usdRect = new Rectangle(
                cashBox.Right - 4,
                cashBox.Y - usdPadding / 2,
                usdSize.Width + usdPadding,
                cashBox.Height
            );

            // then compute rrBtnRect here:
            int rrW = 40, rrH = 20;
            // anchor to top of USD, then pull down slightly
            const int rrPullDown = 8;
            rrBtnRect = new Rectangle(
                usdRect.X,
                usdRect.Y - rrH + rrPullDown,
                rrW,
                rrH
            );

            // width of the text‐box
            //const int cashBoxWidth = 80;
            // 8px gutter to the right edge, plus say another 4px padding for the “USD” label
            int usdLabelWidth = 40;

            // Row4: Break-Even & Partial now pushed below the entire Lot-Calc block
            int BY = startLotY
       + lotCount * rowHeight
       + gutter    // original space
       + gutter;   // extra space to push everything down
            // instantiate the two buttons at the new BY
            beBtn = new Button("Move SL To BE",
X + gutter, BY,
X + gutter + breakBtnW, BY + breakBtnH,
        beBack, bePen, smallFont, textBrush
    );

            partBtn = new Button("Adjust SL/TP",
X + panelW - gutter - breakBtnW, BY,
X + panelW - gutter, BY + breakBtnH,
                 partBack, partPen, smallFont, textBrush
            );

            //
            // reserve space for the "0.0" box between the two buttons
            //
            beValueBox = new Rectangle(
                beBtn.X2 + gutter,    // immediately to the right of Move SL To BE
                beBtn.Y1,             // same top as those buttons
                50,                   // whatever width you like
                row4H                 // same height
            );

            int PY = BY + row4H;

            // place labels immediately below the BE & Partial-Close buttons
            int labelY = BY + row4H + (gutter / 2);

            labelCloseTrades = new Rectangle(
                X + gutter,
                labelY,
                120,
                20
            );
            labelCloseOrders = new Rectangle(
                X + panelW - 140,
                labelY,
                120,
                20
            );

            // Row6 Buttons (immediately under the labels)
            int btnRowY = labelY + 20 + gutter;  // 20px label height + gutter

            // width of each small button
            const int smallBtnW = 60;

            // 1) All
            btnAll = new Button("All",
                X + gutter, btnRowY,
                X + gutter + smallBtnW, btnRowY + row2H,
                smallBack, smallPen, smallFont, textBrush
            );

            // 2) Profit (gutter to the right of All)
            btnProfit = new Button("Profit",
                btnAll.X2 + gutter, btnRowY,
                btnAll.X2 + gutter + smallBtnW, btnRowY + row2H,
                smallBack, smallPen, smallFont, textBrush
            );

            // 3) Loss
            btnLoss = new Button("Loss",
                btnProfit.X2 + gutter, btnRowY,
                btnProfit.X2 + gutter + smallBtnW, btnRowY + row2H,
                smallBack, smallPen, smallFont, textBrush
            );

            // 4) Stop
            btnStop = new Button("Stop",
                btnLoss.X2 + gutter, btnRowY,
                btnLoss.X2 + gutter + smallBtnW, btnRowY + row2H,
                smallBack, smallPen, smallFont, textBrush
            );





            // Subscribe for clicks if needed:
            //CurrentChart.MouseClick += CurrentChart_MouseClick;
        }

        public override void Dispose()
        {
            CurrentChart.MouseClick -= CurrentChart_MouseClick;
            Core.Instance.OrdersHistoryAdded -= OnOrderClosed;
            Core.Instance.OrdersHistoryAdded -= OnOrderFilled;
            base.Dispose();
        }

        private IDrawing GetPSCPosition(string name)
{
    return CurrentChart.Drawings
        .GetAll(Symbol)
        .Where(d =>
            SettingItemExtensions
                .GetItemByName(((ICustomizable)d).Settings, "CustomName")
                ?.Value?.ToString() == name)
        .LastOrDefault();
}


        private double GetDrawingPrice(IDrawing d, string pointName)
        {
            if (d == null) return 0;
            var sd = SettingItemExtensions.GetItemByName(((ICustomizable)d).Settings, pointName)
                     as SettingItemDouble;
            return sd != null ? (double)sd.Value : 0.0;
        }

        protected override void OnUpdate(UpdateArgs args) 
        {
            // Always keep our “cashAmt” in sync with the user-set Risk Amount
            cashAmt = RiskAmount;
            // find the PSC drawing:
            var longPos = CurrentChart.Drawings
                        .GetAll(Symbol)
                        .FirstOrDefault(d =>
                            SettingItemExtensions
                                .GetItemByName(((ICustomizable)d).Settings, "CustomName")
                                ?.Value?.ToString() == "Long Position"
                        );
            var shortPos = CurrentChart.Drawings
                        .GetAll(Symbol)
                        .FirstOrDefault(d =>
                            SettingItemExtensions
                                .GetItemByName(((ICustomizable)d).Settings, "CustomName")
                                ?.Value?.ToString() == "Short Position"
                        );

            if (longPos != null)
            {
                pipR = GetDrawingPrice(longPos, "TopPoint");    // TP
                pipL = GetDrawingPrice(longPos, "BottomPoint"); // SL
            }
            else if (shortPos != null)
            {
                pipR = GetDrawingPrice(shortPos, "BottomPoint");
                pipL = GetDrawingPrice(shortPos, "TopPoint");
            }
            if (lastEntryPrice != 0)
            {
                // choose the correct “current” price stream
                double current = lastSide == Side.Buy ? Symbol.Bid : Symbol.Ask;
                // new: round to integer ticks
                beVal = Math.Round(
                    (lastSide == Side.Buy
                        ? current - lastEntryPrice
                        : lastEntryPrice - current)
                    / Symbol.TickSize,
                    0
                );
            }
            else
            {
                beVal = 0;
            }
        }

        public override void OnPaintChart(PaintChartEventArgs args)
        {
            if (HistoricalData.Count != Count)
                return;

            var g = args.Graphics;
            // ── 1) Scale *only* our UI ──────────────────────────────────────────
            float s = (float)UIScale;
            var stateUI = g.Save();           // save before scaling
            g.ScaleTransform(s, s);

            var r = args.Rectangle;
            int X = XShift, Y = YShift;



            int panelBottom = btnStop.Y2 + gutter + row2H;
            // our rectangle starts at Y-4, so its height is panelBottom - (Y - 4)
            float panelHeight = panelBottom - (Y + 4);

            var rect = new RectangleF(
                X - 4,
                Y - 4,
                panelW + 8,
                panelHeight
            );

            // 1) Panel background & rounded border
            using (var path = new GraphicsPath())
            {
                float d = btnRadius;
                path.AddArc(rect.X, rect.Y, d, d, 180, 90);
                path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
                path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
                path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
                path.CloseAllFigures();

                using (var br = new SolidBrush(panelBack))
                    g.FillPath(br, path);
                g.DrawPath(new Pen(borderCol), path);
            }
            // 2) Header bar
            var hdr = new Rectangle(X, Y, panelW, headerH);
            using (var br = new SolidBrush(headerBack))
                g.FillRectangle(br, hdr);
            g.DrawString("Trade Manager", titleFont, textBrush,
                         X + panelW / 2, Y + headerH / 2, CenterFormat);

            // 3) Row1: SELL / qty / BUY
            sellBtn.Draw(g, btnRadius);
            buyBtn.Draw(g, btnRadius);
            //g.DrawString(quantity.ToString(), mainFont, textBrush,
                         //X + panelW / 2, Y + headerH + row1H / 2, CenterFormat);

            // now draw Limit / Stop entry buttons
            limitOrderBtn.Draw(g, btnRadius);
            stopOrderBtn.Draw(g, btnRadius);

            // fetch live prices
            double bidPrice = Symbol.Bid;
            double askPrice = Symbol.Ask;

            // compute the Y position of the button‐label center
            float sellLabelY = sellBtn.Y1 + sellBtn.Height / 2f;
            float buyLabelY = buyBtn.Y1 + buyBtn.Height / 2f;

            // pick a Y just a font‐height below that
            float priceY = sellLabelY + smallFont.Height - 8;

            // X centers
            float sellX = sellBtn.X1 + sellBtn.Width / 2f;
            float buyX = buyBtn.X1 + buyBtn.Width / 2f;

            // draw them
            g.DrawString(
                bidPrice.ToString("F2"),
                smallFont,
                textBrush,
                sellX,
                priceY,
                CenterFormat
            );
            g.DrawString(
                askPrice.ToString("F2"),
                smallFont,
                textBrush,
                buyX,
                priceY,
                CenterFormat
            );

            // 4) Row2: Pips bar
            var p2 = new Rectangle(
    X + gutter,
    Y + headerH + row1H + 10,      // ← moved down 10px
    panelW - gutter * 2,
    row2H);
            using (var path = RoundedRect(p2, btnRadius))
            {
                using (var br = new SolidBrush(textBoxBackCol))
                    g.FillPath(br, path);
                g.DrawPath(Pens.Gray, path);
            }





            // left (Stop Loss price, two decimals)
            g.DrawString(pipL.ToString("F2"), mainFont, textBrush,
                         p2.X + (p2.Width / 3) / 2, p2.Y + row2H / 2, CenterFormat);
            // ── SL / Price / TP labels, spaced out ──

            // compute the vertical center of the bar:
            float yPipsBar = p2.Y + row2H / 2f;
            // measure “Price” so we can offset SL/TP outside its bounds:
            const float gap = 12f;  // tweak this for more/less space
            string priceLabel = "Price";
            SizeF priceSz = g.MeasureString(priceLabel, mainFont);
            float centerX = p2.X + p2.Width / 2f;

            // draw “Price” in the middle
            using (var priceBrush = new SolidBrush(pipsAndCurrency))
                g.DrawString(priceLabel, mainFont, priceBrush,
                             new PointF(centerX, yPipsBar), CenterFormat);

            // measure SL/TP
            string slLabel = "SL";
            SizeF slSz = g.MeasureString(slLabel, smallFont);
            string tpLabel = "TP";
            SizeF tpSz = g.MeasureString(tpLabel, smallFont);

            // draw SL in red to the left of “Price”
            float slX = centerX - (priceSz.Width / 2f) - (slSz.Width / 2f) - gap;
            g.DrawString(slLabel, smallFont, Brushes.Red,
                         new PointF(slX, yPipsBar), CenterFormat);

            // draw TP in green to the right of “Price”
            float tpX = centerX + (priceSz.Width / 2f) + (tpSz.Width / 2f) + gap;
            g.DrawString(tpLabel, smallFont, Brushes.Green,
                         new PointF(tpX, yPipsBar), CenterFormat);

            // right (Take Profit price, two decimals)
            g.DrawString(pipR.ToString("F2"), mainFont, textBrush,
                         p2.Right - (p2.Width / 3) / 2, p2.Y + row2H / 2, CenterFormat);


            // 5) Row3: Lot-Calc + Cash

            int startLotY = Y + headerH + row1H + row2H + gutter;

            // 1) Calculate the bottom of the Pips bar:
            int pipBarBottom = Y + headerH + row1H + row2H + 20;   // ← +4px
            float lotLabelY = pipBarBottom + gutter;

            g.DrawString(
                "Lot Calc.",
                mainFont,
                textBrush,
                X + gutter,
                lotLabelY,
                LeftFormat
            );

            // 3) “Quantity” from PSC sizing
            //    compute SL ticks based on your PSC-drawn entry & stop points:
            var psc = GetPSCPosition("Long Position") ?? GetPSCPosition("Short Position");
            double slTicks = 0;
            if (psc != null)
            {
                double entry = GetDrawingPrice(psc, "MiddlePoint");
                double sl = GetDrawingPrice(psc, entry == GetDrawingPrice(psc, "BottomPoint")
                ? "TopPoint"
                : "BottomPoint");
                slTicks = Math.Abs((entry - sl) / Symbol.TickSize);
            }
            double qtyValue = GetVolumeByFixedAmount(Symbol, RiskAmount, slTicks);
            string qtyDisplay = qtyValue.ToString("F2");
            using (var pctBrush = new SolidBrush(buyCol.Color1))
            {
                var lmt = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
                g.DrawString(
                qtyDisplay,
                smallFont,
                pctBrush,
                X + panelW - gutter,
                lotLabelY,
                lmt
                );
            }

            // ── draw the rounded box behind "USD" ──
            var usdPadding = 2;
            var usdSize = new Size(40, cashBox.Height);
            var usdRect = new Rectangle(
                cashBox.Right - 4,               // shift left by 4px if you like
                cashBox.Y - usdPadding / 2,
                usdSize.Width + usdPadding,
                cashBox.Height
            );

            using (var path = RoundedRect(new RectangleF(usdRect.X, usdRect.Y, usdRect.Width, usdRect.Height), btnRadius))
            using (var br = new SolidBrush(textBoxBackCol))
            {
                g.FillPath(br, path);
                g.DrawPath(Pens.Gray, path);
            }

            // ── draw the USD text centered ──
            using (var usdBrush = new SolidBrush(pipsAndCurrency))
            {
                g.DrawString(
                    "USD",
                    smallFont,
                    usdBrush,
                    usdRect.X + usdRect.Width / 2f,
                    usdRect.Y + usdRect.Height / 2f,
                    CenterFormat
                );
            }


            // draw the R:R button background & border
            using (var path = RoundedRect(rrBtnRect, btnRadius))
            using (var br = new SolidBrush(textBoxBackCol))
            {
                g.FillPath(br, path);
                g.DrawPath(Pens.Gray, path);
            }

            // now draw each character in its color
            var font = smallFont;
            var fmt = CenterFormat;
            // measure widths
            float wR = g.MeasureString("R", font).Width;
            float wC = g.MeasureString(":", font).Width;
            // starting X so that "R:R" is centered in rrBtnRect
            float totalW = wR + wC + wR;
            float startX = rrBtnRect.X + (rrBtnRect.Width - totalW) / 2;
            float centerY = rrBtnRect.Y + rrBtnRect.Height / 2;

            using (var path = RoundedRect(rrBtnRect, btnRadius))
            using (var br = new SolidBrush(textBoxBackCol))
            {
                g.FillPath(br, path);
                g.DrawPath(Pens.Gray, path);
            }

            // left R (red)
            g.DrawString("R", font, Brushes.Red,
                         new PointF(startX + wR / 2, centerY), fmt);
            // colon (white)
            g.DrawString(":", font, Brushes.White,
                         new PointF(startX + wR + wC / 2, centerY), fmt);
            // right R (green)
            g.DrawString("R", font, Brushes.Green,
                         new PointF(startX + wR + wC + wR / 2, centerY), fmt);

            


            // 4) RADIO BUTTONS (one gutter below the label)
            // 4) RADIO BUTTONS (one gutter below the label)
            int radioStartY = (int)(lotLabelY + smallFont.Height + gutter / 32);
            for (int i = 0; i < lotRadios.Length; i++)
            {
                var b = lotRadios[i];
                b.Y1 = radioStartY + i * (radioSize + gutter);
                b.Y2 = b.Y1 + radioSize;
                b.DrawCircle(g, btnRadius);
                g.DrawString(
                  b.Text,
                  smallFont,
                  textBrush,
                  b.X1 + radioSize + 4,
                  b.Y1 + radioSize / 2,
                  LeftFormat
                );
            }

            // 5) CASH BOX (positioned alongside the “Cash Amount” radio)
            cashBox.Y = radioStartY + 1 * (radioSize + gutter) + 4;
            var cashRectF = new RectangleF(cashBox.X, cashBox.Y, cashBox.Width, cashBox.Height);
            using (var path = RoundedRect(cashRectF, btnRadius))
            {
                using (var br = new SolidBrush(textBoxBackCol))
                    g.FillPath(br, path);
                g.DrawPath(Pens.Gray, path);
            }
            g.DrawString(
              cashAmt.ToString("F2"),
              smallFont,
              textBrush,
              cashBox.X + cashBox.Width / 2,
              cashBox.Y + cashBox.Height / 2,
              CenterFormat
            );

            // ── DRAW ONE DIVIDER UNDER THE RADIOS ──
            int radiosBottomY = radioStartY + lotRadios.Length * (radioSize + gutter);
            int dividerY = radiosBottomY + (gutter / 2);
            using (var divPen = new Pen(Color.Gray, 1))
                g.DrawLine(
                  divPen,
                  X + gutter, dividerY,
                  X + panelW - gutter, dividerY
                );

            // ── PUSH EVERYTHING BELOW THIS LINE DOWN BY ONE GUTTER ──

            int extra = 4;
            int sectionOffset = gutter + extra;

            var saved = g.Save();
            g.TranslateTransform(0, sectionOffset);

            int BY = dividerY + sectionOffset;
            int breakLabelY = BY - mainFont.Height + gutter + 5;
            g.DrawString(
              "Break Even & Partial Close",
              mainFont,
              textBrush,
              X + gutter,
              breakLabelY,
              LeftFormat
            );

            const int breakBtnH = 30;
            beBtn.Y1 = BY; beBtn.Y2 = BY + breakBtnH;
            partBtn.Y1 = BY; partBtn.Y2 = BY + breakBtnH;
            beBtn.Draw(g, btnRadius);
            partBtn.Draw(g, btnRadius);


            // 7) MIDDLE BE-VALUE BOX (size it to exactly fill the gap)
            beValueBox = new Rectangle(
                beBtn.X2 + gutter,
                BY,
                partBtn.X1 - beBtn.X2 - (gutter * 2),
                breakBtnH
            );
            var beRectF = new RectangleF(beValueBox.X, beValueBox.Y, beValueBox.Width, beValueBox.Height);
            using (var path = RoundedRect(beRectF, btnRadius))
            {
                using (var br = new SolidBrush(textBoxBackCol))
                    g.FillPath(br, path);
                g.DrawPath(Pens.Gray, path);
            }

            //── draw BE value with dynamic color ─────────────────────────────────────────
            Brush beBrush = beVal < 0
                ? Brushes.Red
                : beVal > 0
                    ? Brushes.Green
                    : textBrush;   // zero stays white (or whatever your textBrush is)

            g.DrawString(
                beVal.ToString("F0"),
                smallFont,
                beBrush,
                beValueBox.X + beValueBox.Width / 2,
                beValueBox.Y + beValueBox.Height / 2,
                CenterFormat
            );
            int labelY = BY + row4H + gutter / 2;

            // ── divider above the Close-Trades/Close-Orders block ──
            int tradesDividerY = labelY - (gutter / 2);
            using (var divPen = new Pen(Color.Gray, 1))
                g.DrawLine(divPen,
                    X + gutter, tradesDividerY,
                    X + panelW - gutter, tradesDividerY
                );

            // ── now push the Close-Trades labels & buttons down by gutter + extra2 ──
            int extra2 = 6;                       // add 4 more pixels
            int blockOffset = gutter + extra2;      // 8 + 4 = 12px
            int labelZ = tradesDividerY + blockOffset;

            g.DrawString(
              "Close Trades",
              mainFont,
              textBrush,
              X + gutter,
              labelZ,
              LeftFormat
            );

            g.DrawString(
              "Close Orders",
              mainFont,
              textBrush,
              X + panelW - gutter - 120,
              labelZ,
              LeftFormat
            );


            // 4) And immediately under *that*, draw your All/Profit/Loss/Stop buttons…
            //    spacing them only by half a gutter (so they sit tightly under the labels)
            const int smallBtnW = 60;
            const int btnCount = 4;
            const int spacing = gutter;  // 8px

            // total width of 4 buttons + 3 gutters:
            int totalBtnsW = smallBtnW * btnCount + spacing * (btnCount - 1);

            // compute left edge so the whole group is centered:
            int btnsStartX = X + (panelW - totalBtnsW) / 2;

            // vertical position:
            int btnRowY = labelY + mainFont.Height + gutter / 2;

            // assign each button’s X1/X2 and Y1/Y2:
            btnAll.X1 = btnsStartX;
            btnAll.X2 = btnAll.X1 + smallBtnW;
            btnAll.Y1 = btnRowY;
            btnAll.Y2 = btnRowY + row2H;

            btnProfit.X1 = btnAll.X2 + spacing;
            btnProfit.X2 = btnProfit.X1 + smallBtnW;
            btnProfit.Y1 = btnRowY;
            btnProfit.Y2 = btnRowY + row2H;

            btnLoss.X1 = btnProfit.X2 + spacing;
            btnLoss.X2 = btnLoss.X1 + smallBtnW;
            btnLoss.Y1 = btnRowY;
            btnLoss.Y2 = btnRowY + row2H;

            btnStop.X1 = btnLoss.X2 + spacing;
            btnStop.X2 = btnStop.X1 + smallBtnW;
            btnStop.Y1 = btnRowY;
            btnStop.Y2 = btnRowY + row2H;

            // finally draw them:
            btnAll.Draw(g, btnRadius);
            btnProfit.Draw(g, btnRadius);
            btnLoss.Draw(g, btnRadius);
            btnStop.Draw(g, btnRadius);

            g.Restore(stateUI);
        }

        private void ResetAllButtons()
        {
            limitOrderBtn.Reset("LIMIT");
            stopOrderBtn.Reset("STOP");
        }

        private void PlaceOrderFromPSC()
        {
            // 1) grab your PSC drawing
            var longPos = GetPSCPosition("Long Position");
            var shortPos = GetPSCPosition("Short Position");
            var psc = longPos ?? shortPos;
            if (psc == null) return;

            bool isLong = longPos != null;
            Side side = isLong ? Side.Buy : Side.Sell;

            // 2) read absolute prices from PSC
            double entryPrice = GetDrawingPrice(psc, "MiddlePoint");
            double slPrice = GetDrawingPrice(psc, isLong ? "BottomPoint" : "TopPoint");
            double tpPrice = GetDrawingPrice(psc, isLong ? "TopPoint" : "BottomPoint");

            // 3) calculate quantity
            double slTicks = Math.Abs((entryPrice - slPrice) / Symbol.TickSize);
            double qty = GetVolumeByFixedAmount(Symbol, RiskAmount, slTicks);

            // 4) build absolute SL/TP holders
            var slHolder = SlTpHolder.CreateSL(
                slPrice,
                PriceMeasurement.Absolute,
                false, double.NaN, double.NaN
            );
            var tpHolder = SlTpHolder.CreateTP(
                tpPrice,
                PriceMeasurement.Absolute,
                double.NaN, double.NaN
            );

            // 5) pick order type
            double price = 0;
            double triggerPrice = 0;
            string orderTypeId;

            if (MarketOrderMode)
            {
                orderTypeId = OrderType.Market.ToString();
            }
            else
            {
                double marketPrice = isLong ? Symbol.Ask : Symbol.Bid;
                if ((isLong && marketPrice < entryPrice) ||
                    (!isLong && marketPrice > entryPrice))
                {
                    orderTypeId = OrderType.Stop.ToString();
                    triggerPrice = entryPrice;
                }
                else
                {
                    orderTypeId = OrderType.Limit.ToString();
                    price = entryPrice;
                }
            }

            // 6) construct & send
            var req = new PlaceOrderRequestParameters
            {
                Symbol = Symbol,
                Account = CurrentChart.Account,
                OrderTypeId = orderTypeId,
                Side = side,
                Quantity = qty,
                Price = price,
                TriggerPrice = triggerPrice,
                StopLoss = slHolder,
                TakeProfit = tpHolder,
                TimeInForce = TimeInForce.GTC
            };

            Core.Instance.PlaceOrder(req);
        }




        void CurrentChart_MouseClick(object _, ChartMouseNativeEventArgs e)
        {
            var ne = (NativeMouseEventArgs)e;
            // use native chart coords directly
            int rawX = ne.X;
            int rawY = ne.Y;

            // 1) price should come from rawY (chart-native)
            double clickedPrice = CurrentChart.MainWindow
                                      .CoordinatesConverter
                                      .GetPrice(rawY);

            // 2) hit-testing on your scaled UI needs logical coords:
            int x = (int)(rawX / UIScale);
            int y = (int)(rawY / UIScale);
            // first check your R:R button
            if (rrBtnRect.Contains(x, y))
            {
                rrArmed = !rrArmed;
                if (rrArmed)
                {
                    // de-arm the others
                    buyArmed = sellArmed = false;
                    buyBtn.ResetText();
                    sellBtn.ResetText();
                }
                else
                {
                    // run or cancel?
                    PlaceOrderFromPSC();
                }
                return;
            }

            

            // 1) LIMIT toggle
            if (limitOrderBtn.Contains(x, y))
            {
                limitOrderBtn.isClicked = !limitOrderBtn.isClicked;
                limitOrderBtn.Text = limitOrderBtn.isClicked ? "Cancel" : "LIMIT";
                if (limitOrderBtn.isClicked)
                    stopOrderBtn.Reset("STOP");
                else
                    tradeParams.Reset();
                return;
            }

            // 2) STOP toggle
            if (stopOrderBtn.Contains(x, y))
            {
                stopOrderBtn.isClicked = !stopOrderBtn.isClicked;
                stopOrderBtn.Text = stopOrderBtn.isClicked ? "Cancel" : "STOP";
                if (stopOrderBtn.isClicked)
                    limitOrderBtn.Reset("LIMIT");
                else
                    tradeParams.Reset();
                return;
            }

            // 3) LIMIT price picking
            if (limitOrderBtn.isClicked)
            {
                if (tradeParams.price == 0)
                    tradeParams.price = clickedPrice;
                else if (tradeParams.slPrice == 0)
                    tradeParams.slPrice = clickedPrice;

                if (tradeParams.price != 0 && tradeParams.slPrice != 0)
                {
                    double tickSize = tradeParams.symbol.TickSize;
                    // safely using the symbol you stored in OnInit
                    double slTicks = Math.Abs((tradeParams.price - tradeParams.slPrice) / tickSize);

                    double tpTicks = slTicks * RewardMultiplier;
                    double qty = GetVolumeByFixedAmount(Symbol, RiskAmount, slTicks);

                    var req = new PlaceOrderRequestParameters
                    {
                        Symbol = Symbol,
                        Account = CurrentChart.Account,
                        OrderTypeId = OrderType.Limit,
                        Side = tradeParams.price > tradeParams.slPrice ? Side.Buy : Side.Sell,
                        Price = tradeParams.price,
                        Quantity = qty,
                        StopLoss = SlTpHolder.CreateSL(slTicks, PriceMeasurement.Offset, false, double.NaN, double.NaN),
                        TakeProfit = SlTpHolder.CreateTP(tpTicks, PriceMeasurement.Offset, double.NaN, double.NaN)
                    };
                    Core.Instance.PlaceOrder(req);

                    ResetAllButtons();
                    tradeParams.Reset();
                }
                return;
            }

            // 4) STOP price picking
            if (stopOrderBtn.isClicked)
            {
                if (tradeParams.price == 0)
                    tradeParams.price = clickedPrice;
                else if (tradeParams.slPrice == 0)
                    tradeParams.slPrice = clickedPrice;

                if (tradeParams.price != 0 && tradeParams.slPrice != 0)
                {
                    double slTicks = Math.Abs((tradeParams.price - tradeParams.slPrice) / Symbol.TickSize);
                    double tpTicks = slTicks * RewardMultiplier;
                    double qty = GetVolumeByFixedAmount(Symbol, RiskAmount, slTicks);

                    var req = new PlaceOrderRequestParameters
                    {
                        Symbol = Symbol,
                        Account = CurrentChart.Account,
                        OrderTypeId = OrderType.Stop,
                        Side = tradeParams.price > Symbol.Bid ? Side.Buy : Side.Sell,
                        TriggerPrice = tradeParams.price,
                        Quantity = qty,
                        StopLoss = SlTpHolder.CreateSL(slTicks, PriceMeasurement.Offset, false, double.NaN, double.NaN),
                        TakeProfit = SlTpHolder.CreateTP(tpTicks, PriceMeasurement.Offset, double.NaN, double.NaN),
                        TimeInForce = TimeInForce.GTC
                    };
                    Core.Instance.PlaceOrder(req);

                    ResetAllButtons();
                    tradeParams.Reset();
                }
                return;
            }
            // BUY BUTTON CLICKED?
            if (buyBtn.Contains(x, y))
            {
                // toggle your “armed” flag
                buyArmed = !buyArmed;

                // **right here** update the button’s label
                buyBtn.Text = buyArmed ? "Cancel" : "BUY";

                if (buyArmed)
                {
                    // start your buy‐flow
                    isBuyFlow = true;
                    entryPrice = Symbol.Ask;
                    stopPrice = 0;

                    // de‐arm the other buttons if you like
                    sellArmed = rrArmed = false;
                    sellBtn.Text = "SELL";      // reset their labels
                    
                }
                else
                {
                    // user cancelled the buy
                    isBuyFlow = false;
                }

                return;
            }

            // We’re in BUY‐capture mode, and this is the *second* click anywhere outside BUY:
            if (isBuyFlow && stopPrice == 0)
            {
                stopPrice = clickedPrice;

                // compute risk parameters
                double slTicks = Math.Abs((entryPrice - stopPrice) / Symbol.TickSize);
                double tpTicks = slTicks * RewardMultiplier;
                double qty = GetVolumeByFixedAmount(Symbol, RiskAmount, slTicks);

                // place a MARKET‐BUY
                var req = new PlaceOrderRequestParameters
                {
                    Symbol = Symbol,
                    Account = CurrentChart.Account,
                    OrderTypeId = OrderType.Market,
                    Side = Side.Buy,    // ← explicitly BUY
                    Quantity = qty,
                    StopLoss = SlTpHolder.CreateSL(slTicks, PriceMeasurement.Offset, false, double.NaN, double.NaN),
                    TakeProfit = SlTpHolder.CreateTP(tpTicks, PriceMeasurement.Offset, double.NaN, double.NaN)
                };
                Core.PlaceOrder(req);
                buyArmed = false;
                buyBtn.Text = "BUY";
                lastSide = Side.Buy;
                lastEntryPrice = entryPrice;   // the price you captured on the first click
                // reset
                isBuyFlow = false;
                return;
            }

            // SELL BUTTON CLICKED?
            if (sellBtn.Contains(x, y))
            {
                // toggle “armed” state
                sellArmed = !sellArmed;
                sellBtn.Text = sellArmed ? "Cancel" : "SELL";

                if (sellArmed)
                {
                    // start your sell‐flow
                    isSellFlow = true;
                    entryPrice = Symbol.Bid;
                    stopPrice = 0;

                    // de‐arm the other buttons
                    buyArmed = rrArmed = false;
                    buyBtn.Text = "BUY";
                }
                else
                {
                    // user hit “Cancel”
                    isSellFlow = false;
                }
                return;
            }

            if (isSellFlow && stopPrice == 0)
            {
                stopPrice = clickedPrice;
                double slTicks = Math.Abs((stopPrice - entryPrice) / Symbol.TickSize);
                double tpTicks = slTicks * RewardMultiplier;
                double qty = GetVolumeByFixedAmount(Symbol, RiskAmount, slTicks);

                var req = new PlaceOrderRequestParameters
                {
                    Symbol = Symbol,
                    Account = CurrentChart.Account,
                    OrderTypeId = OrderType.Market,
                    Side = Side.Sell,
                    Quantity = qty,
                    StopLoss = SlTpHolder.CreateSL(slTicks, PriceMeasurement.Offset, false, double.NaN, double.NaN),
                    TakeProfit = SlTpHolder.CreateTP(tpTicks, PriceMeasurement.Offset, double.NaN, double.NaN)
                };
                Core.Instance.PlaceOrder(req);

                // ─── reset SELL button ────────────────────────────────────────────
                sellArmed = false;
                sellBtn.Text = "SELL";

                lastSide = Side.Sell;
                lastEntryPrice = entryPrice;
                isSellFlow = false;
                return;
            }

            // Adjust SL / TP on all current positions
            if (partBtn.Contains(x, y))
            {
                foreach (var pos in Core.Instance.Positions)
                {
                    if (pos.Account == CurrentChart.Account && pos.Symbol == this.Symbol)
                        Core.Instance.AdvancedTradingOperations.AdjustSlTp(pos);
                }
                return;
            }
        

            // flatten‐all button
            if (btnAll.Contains(x, y))
            {
                Core.AdvancedTradingOperations.Flatten();
                return;
            }

            if (beBtn.Contains(x, y))
            {
                // grab every open position for this account & symbol
                foreach (var pos in Core.Instance.Positions)
                {
                    if (pos.Account == CurrentChart.Account && pos.Symbol == this.Symbol)
                        Core.Instance.AdvancedTradingOperations.BreakEven(pos);
                }
                return;
            }

            //–– Close only profitable positions
            if (btnProfit.Contains(x, y))
            {
                foreach (var pos in Core.Instance.Positions)
                {
                    if (pos.Account == CurrentChart.Account
                     && pos.Symbol == this.Symbol
                     && pos.GrossPnL.Value > 0)                                   // only winners
                        Core.Instance.AdvancedTradingOperations.Flatten(pos.Id); // close it
                }
                return;
            }

            //–– Close only losing positions
            if (btnLoss.Contains(x, y))
            {
                foreach (var pos in Core.Instance.Positions)
                {
                    if (pos.Account == CurrentChart.Account
                     && pos.Symbol == this.Symbol
                     && pos.GrossPnL.Value < 0)                                   // only losers
                        Core.Instance.AdvancedTradingOperations.Flatten(pos.Id); // close it
                }
                return;
            }

            //–– Cancel all pending Stop orders
            if (btnStop.Contains(x, y))
            {
                foreach (var ord in Core.Instance.Orders)
                {
                    // only Stop orders for this symbol/account
                    if (ord.Account == CurrentChart.Account
                     && ord.Symbol == this.Symbol
                     && ord.OrderTypeId == OrderType.Stop.ToString()
                     && ord.Status != OrderStatus.Inactive)
                        Core.Instance.AdvancedTradingOperations.CancelOrders();
                }
                return;
            }
        }


        private double GetVolumeByFixedAmount(Symbol symbol, int amountToRisk, double slTicks)
        {
            if (DoubleExtensions.IsNanOrDefault(symbol.Bid) || DoubleExtensions.IsNanOrDefault(symbol.TickSize))
                return 0;
            double num1 = symbol.GetTickCost(symbol.Bid) * slTicks;
            double num2 = (double)amountToRisk / num1;
            var loggers = Core.Instance.Loggers;
            loggers.Log($"symbol={symbol}, amountToRisk={amountToRisk}, slTicks={slTicks}, tickSize={symbol.TickSize}, tickCost={symbol.GetTickCost(symbol.Bid)}, calculatedLotSize={num2}", LoggingLevel.System, null);
            double minLot = symbol.MinLot;
            double maxLot = symbol.MaxLot;
            double lotStep = symbol.LotStep;
            double adjusted = Math.Max(0, num2 - minLot);
            int steps = (int)(adjusted / lotStep);
            double volumeByFixedAmount = minLot + steps * lotStep;
            if (volumeByFixedAmount < minLot) volumeByFixedAmount = minLot;
            if (volumeByFixedAmount > maxLot) volumeByFixedAmount = maxLot;
            return volumeByFixedAmount;
        }


        /// <summary>
        /// Returns a rounded‐corner rectangle path.
        /// </summary>
        GraphicsPath RoundedRect(RectangleF rect, float radius)
        {
            float d = radius * 1;
            var path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseAllFigures();
            return path;
        }


        //── Button helper ───────────────────────────────────────────────────────────
        //── Button helper ────────────────────────────────────────────────────────────
        private class Button
        {
            public bool isClicked;
            public bool IsChecked;
            public string Text;
            public int X1, Y1, X2, Y2;
            private readonly Brush back;
            private readonly Pen border;
            private readonly Font font;
            private readonly Brush txtBrush;
            private readonly bool isCircle;

            // <-- notice the final "bool circle = false" parameter
            public Button(
                string text,
                int x1, int y1,
                int x2, int y2,
                Brush back,
                Pen border,
                Font font,
                Brush txtBrush,
                bool circle = false    // default false, but allows you to pass circle:true
            )
            {
                Text = text;
                X1 = x1;
                Y1 = y1;
                X2 = x2;
                Y2 = y2;
                this.back = back;
                this.border = border;
                this.font = font;
                this.txtBrush = txtBrush;
                this.isCircle = circle;
                isClicked = false;
            }

            public void Reset(string newText)
            {
                this.Text = newText;
                this.IsChecked = false;   // if you want to also clear any “checked” state
                isClicked = false;
            }

            public void ResetText()
            {
                Text = this.Text;  // store “BUY”/“SELL”/“R:R” in a field
                isClicked = false;
            }

            public int Width => X2 - X1;
            public int Height => Y2 - Y1;

            public void Draw(Graphics g, int radius = 0)
            {
                if (isCircle)
                    return;

                using (var path = new GraphicsPath())
                {
                    float d = radius;
                    path.AddArc(X1, Y1, d, d, 180, 90);
                    path.AddArc(X2 - d, Y1, d, d, 270, 90);
                    path.AddArc(X2 - d, Y2 - d, d, d, 0, 90);
                    path.AddArc(X1, Y2 - d, d, d, 90, 90);
                    path.CloseAllFigures();

                    g.FillPath(back, path);
                    g.DrawPath(border, path);
                }

                g.DrawString(
                    Text,
                    font,
                    txtBrush,
                    X1 + Width / 2,
                    Y1 + Height / 2,
                    OrderPlacingTool.CenterFormat
                );
            }

            public void DrawCircle(Graphics g, int _)
            {
                int dia = Height;
                var outer = new Rectangle(X1, Y1, dia, dia);

                // draw the outline
                g.DrawEllipse(border, outer);

                // fill only if this is a radio and it's checked
                if (isCircle && IsChecked)
                {
                    // make the fill circle half the diameter
                    int fillDia = dia / 2;
                    // center it in the outer circle
                    int offset = (dia - fillDia) / 2;
                    var inner = new Rectangle(X1 + offset, Y1 + offset, fillDia, fillDia);

                    using (var white = new SolidBrush(Color.White))
                        g.FillEllipse(white, inner);
                }
            }       

            public bool Contains(int x, int y)
                => x >= X1 && x < X2 && y >= Y1 && y < Y2;
        }

    }
}
