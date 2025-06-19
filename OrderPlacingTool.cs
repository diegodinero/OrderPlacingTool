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
        private int riskInAmount = 100;
        private double rewardMultiplier = 1.0;
        private int xShift = 30;   // panel left offset
        private int yShift = 30;   // panel top offset

        public override IList<SettingItem> Settings
        {
            get
            {
                var s = base.Settings;
                var sep = s.FirstOrDefault()?.SeparatorGroup;
                s.Add(new SettingItemInteger("riskInAmount", riskInAmount)
                {
                    Text = "Risk Amount",
                    SeparatorGroup = sep
                });
                s.Add(new SettingItemDouble("rewardMultiplier", rewardMultiplier)
                {
                    Text = "Reward Multiplier",
                    Increment = 0.1,
                    DecimalPlaces = 1,
                    SeparatorGroup = sep
                });
                s.Add(new SettingItemInteger("xShift", xShift)
                {
                    Text = "X Offset",
                    SeparatorGroup = sep
                });
                s.Add(new SettingItemInteger("yShift", yShift)
                {
                    Text = "Y Offset",
                    SeparatorGroup = sep
                });
                return s;
            }
            set
            {
                SettingItemExtensions.TryGetValue(value, "riskInAmount", out riskInAmount);
                SettingItemExtensions.TryGetValue(value, "rewardMultiplier", out rewardMultiplier);
                SettingItemExtensions.TryGetValue(value, "xShift", out xShift);
                SettingItemExtensions.TryGetValue(value, "yShift", out yShift);
                base.Settings = value;
                BuildBrushesAndPens();
            }
        }

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
        Button beBtn, partBtn;
        Button btnAll, btnProfit, btnLoss, btnStop;
        Rectangle labelCloseTrades, labelCloseOrders;
        Brush textBoxBack;

        int quantity = 100;
        double pipL = 2936.0, pipR = 2795.0;
        double cashAmt, beVal = 0.0;

        enum LotMode { None, Cash, RiskBal, RiskEq }
        LotMode lotMode = LotMode.Cash;

        Rectangle beValueBox;
        //──────────────────────────────────────────────────────────────────────────────
        public OrderPlacingTool()
        {
            Name = "Trade Manager";
            Description = "In-chart Trade Manager panel";
            SeparateWindow = false;
            AddLineSeries("dummy", Color.Transparent, 1, LineStyle.Solid);
            BuildBrushesAndPens();
        }

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

        protected override void OnInit()
        {
            int X = xShift, Y = yShift;

            const int headerBtnW = 120;  // <-- desired width
            const int breakBtnW = 100;
            const int breakBtnH = 30;

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

            // Row3: Lot-Calc radios + cash input
            int radioX = X + gutter;
            int startLotY = Y + headerH + row1H + row2H + gutter;
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


            // width of the text‐box
            const int cashBoxWidth = 80;
            // 8px gutter to the right edge, plus say another 4px padding for the “USD” label
            int usdLabelWidth = 40;

            cashBox = new Rectangle(
                // panelX + panelW  gives us the right edge of the panel
                // subtract gutter (8px), the USD label width (40px) and the box width
                X + panelW - gutter - usdLabelWidth - cashBoxWidth,
                // same Y as before
                startLotY + 1 * rowHeight + 4,
                cashBoxWidth,
                row3H - 8
            );



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

            partBtn = new Button("Close Part",
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
            CurrentChart.MouseClick += CurrentChart_MouseClick;
        }

        public override void Dispose()
        {
            CurrentChart.MouseClick -= CurrentChart_MouseClick;
            base.Dispose();
        }

        protected override void OnUpdate(UpdateArgs args) 
        {
            // Always keep our “cashAmt” in sync with the user-set Risk Amount
            cashAmt = riskInAmount;
        }

        public override void OnPaintChart(PaintChartEventArgs args)
        {
            if (HistoricalData.Count != Count)
                return;

            var g = args.Graphics;
            var r = args.Rectangle;
            int X = xShift, Y = yShift;



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
            g.DrawString(quantity.ToString(), mainFont, textBrush,
                         X + panelW / 2, Y + headerH + row1H / 2, CenterFormat);
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
            // left
            g.DrawString(pipL.ToString("F1"), mainFont, textBrush,
                         p2.X + (p2.Width / 3) / 2, p2.Y + row2H / 2, CenterFormat);
            // center
            using (var b = new SolidBrush(pipsAndCurrency))
            {
                g.DrawString(
                  "Price",
                  mainFont,
                  b,
                  p2.X + p2.Width / 2,
                  p2.Y + row2H / 2,
                  CenterFormat
                );
            }
            // right
            g.DrawString(pipR.ToString("F1"), mainFont, textBrush,
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

            // 3) PERCENT (right-aligned on same line)
            string pct = (9.14).ToString("F2") + "%";  // replace with your real value
            using (var pctBrush = new SolidBrush(buyCol.Color1))
            {
                var pctFmt = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
                g.DrawString(
                    pct,
                    smallFont,
                    pctBrush,
                    X + panelW - gutter,
                    lotLabelY,
                    pctFmt
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
            
            g.DrawString(
                beVal.ToString("F1"),
                smallFont,
                textBrush,
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

            g.Restore(saved);



            // restore clip
            g.SetClip(r, CombineMode.Replace);
        }

        void CurrentChart_MouseClick(object _, ChartMouseNativeEventArgs __) { /* … */ }

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
